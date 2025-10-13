// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using KOTORModSync.Core.Services.Download;

namespace KOTORModSync.Core.Services
{

	public class ModLinkProcessingService
	{
		private readonly DownloadCacheService _downloadCacheService;

		public ModLinkProcessingService(DownloadCacheService downloadCacheService = null)
		{
			_downloadCacheService = downloadCacheService ?? new DownloadCacheService();

			try
			{
				// Configure HttpClientHandler for better parallel performance
				var handler = new HttpClientHandler
				{
					MaxConnectionsPerServer = 10, // Allow up to 10 concurrent connections per server
					AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
				};

				var httpClient = new HttpClient(handler)
				{
					Timeout = TimeSpan.FromMinutes(5) // Increase timeout for parallel operations
				};

				var downloadHandlers = new List<IDownloadHandler>
				{
					new DeadlyStreamDownloadHandler(httpClient),
					new MegaDownloadHandler(),
					new NexusModsDownloadHandler(httpClient, ""),
					new GameFrontDownloadHandler(httpClient),
					new DirectDownloadHandler(httpClient)
				};
				_downloadCacheService.SetDownloadManager(new DownloadManager(downloadHandlers));
			}
			catch ( Exception ex )
			{

				Logger.LogException(ex, "Failed to configure default DownloadManager for ModLinkProcessingService");
			}
		}

		public async Task<int> ProcessComponentModLinksAsync(
			List<ModComponent> components,
			string downloadDirectory,
			IProgress<Download.DownloadProgress> progress = null,
			CancellationToken cancellationToken = default)
		{
			if ( components == null || components.Count == 0 )
				return 0;

			if ( string.IsNullOrWhiteSpace(downloadDirectory) )
				return 0;

			int successCount = 0;

			foreach ( ModComponent component in components )
			{
				try
				{

					if ( component.ModLink == null || component.ModLink.Count == 0 )
						continue;

					int initialInstructionCount = component.Instructions.Count;

					foreach ( string modLink in component.ModLink )
					{
						if ( string.IsNullOrWhiteSpace(modLink) )
							continue;

						if ( !modLink.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
							!modLink.StartsWith("https://", StringComparison.OrdinalIgnoreCase) )
							continue;

						try
						{

							List<DownloadCacheEntry> cacheEntries = await _downloadCacheService.ResolveOrDownloadAsync(
								component,
								downloadDirectory,
								progress,
								cancellationToken);

							if ( cacheEntries != null && cacheEntries.Count > 0 )
							{
								foreach ( var entry in cacheEntries )
								{

									if ( entry.IsArchive && !string.IsNullOrEmpty(entry.FilePath) && System.IO.File.Exists(entry.FilePath) )
									{

										bool generated = AutoInstructionGenerator.GenerateInstructions(component, entry.FilePath);
										if ( generated )
										{
											await Logger.LogVerboseAsync($"Auto-generated detailed instructions for '{component.Name}' from {entry.ArchiveName}");

											component.IsDownloaded = true;
										}
									}
								}
							}

							if ( cacheEntries != null && cacheEntries.Count > 0 && component.Instructions.Count > initialInstructionCount )
							{
								await Logger.LogVerboseAsync($"Successfully processed ModLink for '{component.Name}': {modLink}");
							}
						}
						catch ( Exception ex )
						{

							await Logger.LogVerboseAsync($"Failed to process ModLink for '{component.Name}': {modLink} - {ex.Message}");
							continue;
						}
					}

					if ( component.Instructions.Count > initialInstructionCount )
					{
						successCount++;
						int newInstructions = component.Instructions.Count - initialInstructionCount;
						await Logger.LogAsync($"Added {newInstructions} new instruction(s) for '{component.Name}': {component.InstallationMethod}");
					}
				}
				catch ( Exception ex )
				{
					await Logger.LogExceptionAsync(ex, $"Error processing component '{component.Name}'");
				}
			}

			if ( successCount > 0 )
			{
				await Logger.LogAsync($"Processed ModLinks and generated/updated instructions for {successCount} component(s).");
			}

			return successCount;
		}


		public int ProcessComponentModLinksSync(
			List<ModComponent> components,
			string downloadDirectory,
			IProgress<Download.DownloadProgress> progress = null,
			CancellationToken cancellationToken = default)
		{
			Task<int> task = ProcessComponentModLinksAsync(components, downloadDirectory, progress, cancellationToken);
			task.Wait(cancellationToken);
			return task.Result;
		}
	}
}

