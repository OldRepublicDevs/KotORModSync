// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Threading;

using KOTORModSync.Core;
using KOTORModSync.Core.Services;
using KOTORModSync.Core.Services.Download;
using KOTORModSync.Core.Utility;
using KOTORModSync.Dialogs;

using static KOTORModSync.Core.Services.DownloadCacheService;

namespace KOTORModSync.Services
{
	public class DownloadOrchestrationService
	{
		private readonly DownloadCacheService _cacheService;
		private readonly Window _parentWindow;
		private readonly MainConfig _mainConfig;
		private DownloadProgressWindow _currentDownloadWindow;

		private int _totalComponentsToDownload;
		private int _completedComponents;

		public bool IsDownloadInProgress { get; private set; }
		public int TotalComponentsToDownload => _totalComponentsToDownload;
		public int CompletedComponents => _completedComponents;

		public event EventHandler DownloadStateChanged;

		public DownloadOrchestrationService(
			DownloadCacheService cacheService,
			MainConfig mainConfig,
			Window parentWindow)
		{
			_cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
			_mainConfig = mainConfig ?? throw new ArgumentNullException(nameof(mainConfig));
			_parentWindow = parentWindow ?? throw new ArgumentNullException(nameof(parentWindow));
		}

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Bug", "S2583:Conditionally executed code should be reachable", Justification = "Incorrect lint warning.")]
        public async Task StartDownloadSessionAsync(Action onScanComplete, bool sequential = true)
		{
			try
			{
				if (_currentDownloadWindow != null)
				{
					if (!_currentDownloadWindow.IsVisible)
					{
						_currentDownloadWindow.Show();
					}

					_currentDownloadWindow.Activate();
					_ = _currentDownloadWindow.Focus();

					await Logger.LogVerboseAsync("[DownloadOrchestration] Download window already exists, reusing existing window").ConfigureAwait(false);
					return;
				}

				if (IsDownloadInProgress)
				{
					await Logger.LogWarningAsync("[DownloadOrchestration] Download session already in progress, ignoring request").ConfigureAwait(false);
					return;
				}

				if (_mainConfig.sourcePath is null || !Directory.Exists(_mainConfig.sourcePath.FullName))
				{
					await InformationDialog.ShowInformationDialogAsync(
						_parentWindow,
						message: "Please set your Mod Directory in Settings before downloading mods.")
						.ConfigureAwait(true);
					return;
				}

				if (_mainConfig.allComponents.Count == 0)
				{
					await InformationDialog.ShowInformationDialogAsync(
						_parentWindow,
						message: "Please load a file (TOML or Markdown) before downloading mods.")
						.ConfigureAwait(true);
					return;
				}

				var selectedComponents = _mainConfig.allComponents
					.Where(c => c.IsSelected && c.ModLinkFilenames.Count > 0)
					.ToList();

				if (selectedComponents.Count == 0)
				{
					await InformationDialog.ShowInformationDialogAsync(
						_parentWindow,
						message: "No selected mods have download links available.")
						.ConfigureAwait(true);
					return;
				}

				_totalComponentsToDownload = selectedComponents.Count;
				_completedComponents = 0;
				IsDownloadInProgress = true;
				DownloadStateChanged?.Invoke(this, EventArgs.Empty);

				DownloadProgressWindow progressWindow = null;
				await Dispatcher.UIThread.InvokeAsync(() =>
				{
					progressWindow = new DownloadProgressWindow();
					_currentDownloadWindow = progressWindow;
				});

				if (progressWindow is null)
				{
					await Logger.LogErrorAsync("[DownloadOrchestration] Failed to create DownloadProgressWindow").ConfigureAwait(false);
					return;
				}

				progressWindow.Closed += async (sender, e) =>
				{
					_currentDownloadWindow = null;
					IsDownloadInProgress = false;
					await Logger.LogVerboseAsync("[DownloadOrchestration] Download window closed, clearing reference").ConfigureAwait(false);
				};

				int timeoutMinutes = progressWindow.DownloadTimeoutMinutes;

				await Logger.LogVerboseAsync(string.Format(System.Globalization.CultureInfo.InvariantCulture, "[DownloadOrchestration] Using download timeout: {0} minutes", timeoutMinutes)).ConfigureAwait(false);

				var downloadManager = DownloadHandlerFactory.CreateDownloadManager(
					httpClient: null,
					nexusModsApiKey: MainConfig.NexusModsApiKey,
					timeoutMinutes: timeoutMinutes);
				_cacheService.SetDownloadManager(downloadManager);

				progressWindow.DownloadControlRequested += async (sender, args) =>
				{
					try
					{
						switch (args.Action)
						{
							case DownloadControlAction.Retry:
								await HandleRetryDownloadAsync(args.Progress, selectedComponents, downloadManager, progressWindow).ConfigureAwait(false);
								break;
							case DownloadControlAction.Stop:
								await HandleStopDownloadAsync(args.Progress).ConfigureAwait(true);
								break;
							case DownloadControlAction.Resume:
								await HandleResumeDownloadAsync(args.Progress, selectedComponents, downloadManager, progressWindow).ConfigureAwait(false);
								break;
							case DownloadControlAction.Start:
								await HandleStartDownloadAsync(args.Progress, selectedComponents, downloadManager, progressWindow).ConfigureAwait(false);
								break;
						}
					}
					catch (Exception ex)
					{
						await Logger.LogErrorAsync($"[DownloadControl] Failed to handle {args.Action}: {ex.Message}").ConfigureAwait(false);
						args.Progress.Status = DownloadStatus.Failed;
						args.Progress.ErrorMessage = $"Control action failed: {ex.Message}";
					}
				};

				await Dispatcher.UIThread.InvokeAsync(() => progressWindow.Show());

				_ = Task.Run(async () =>
				{
					try
					{
						await Logger.LogVerboseAsync($"[DownloadOrchestration] Processing {selectedComponents.Count} components").ConfigureAwait(false);

						Dispatcher.UIThread.Post(() =>
						{
							foreach (ModComponent component in selectedComponents)
							{
								foreach (string url in component.ModLinkFilenames.Keys)
								{
									var initialProgress = new DownloadProgress
									{
										ModName = component.Name,
										Url = url,
										Status = DownloadStatus.Pending,
										StatusMessage = "Waiting to start...",
										ProgressPercentage = 0,
									};
									progressWindow.AddDownload(initialProgress);
								}
							}
						});

						await Logger.LogVerboseAsync(sequential
							? "[DownloadOrchestration] Starting sequential download processing"
							: "[DownloadOrchestration] Starting concurrent download processing").ConfigureAwait(false);

						if (sequential)
						{
							foreach (ModComponent component in selectedComponents)
							{
								try
								{
									await ProcessComponentDownloadAsync(component, downloadManager, progressWindow).ConfigureAwait(false);
								}
								catch (Exception ex)
								{
									await Logger.LogExceptionAsync(ex, $"Error downloading component '{component.Name}'").ConfigureAwait(false);
								}
							}
						}
						else
						{
							await Task.WhenAll(selectedComponents.Select(async component =>
							{
								try
								{
									await ProcessComponentDownloadAsync(component, downloadManager, progressWindow).ConfigureAwait(false);
								}
								catch (Exception ex)
								{
									await Logger.LogExceptionAsync(ex, $"Error downloading component '{component.Name}'").ConfigureAwait(false);
								}
							})).ConfigureAwait(true);
						}

						progressWindow.MarkCompleted();

						IsDownloadInProgress = false;
						await Dispatcher.UIThread.InvokeAsync(() => DownloadStateChanged?.Invoke(this, EventArgs.Empty));

						await Logger.LogVerboseAsync("[DownloadOrchestration] Running post-download validation").ConfigureAwait(false);
						await Dispatcher.UIThread.InvokeAsync(() => onScanComplete?.Invoke());
					}
					catch (Exception ex)
					{
						await Logger.LogExceptionAsync(ex, "Error during mod download").ConfigureAwait(false);
						await Dispatcher.UIThread.InvokeAsync(async () =>
						{
							IsDownloadInProgress = false;
							DownloadStateChanged?.Invoke(this, EventArgs.Empty);
							await InformationDialog.ShowInformationDialogAsync(_parentWindow,
								$"An error occurred while downloading mods:{Environment.NewLine}{Environment.NewLine}{ex.Message}").ConfigureAwait(true);
						}).ConfigureAwait(true);
					}
				}, _currentDownloadWindow?.CancellationToken ?? CancellationToken.None);
			}
			catch (Exception ex)
			{
				await Logger.LogExceptionAsync(ex, "Error starting download session").ConfigureAwait(false);
				IsDownloadInProgress = false;
				DownloadStateChanged?.Invoke(this, EventArgs.Empty);
				await Dispatcher.UIThread.InvokeAsync(async () =>
				{
					await InformationDialog.ShowInformationDialogAsync(_parentWindow,
						$"An error occurred while starting downloads:{Environment.NewLine}{Environment.NewLine}{ex.Message}").ConfigureAwait(true);
				}).ConfigureAwait(true);
			}
		}

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
        private async Task ProcessComponentDownloadAsync(
			ModComponent component,
			DownloadManager downloadManager,
			DownloadProgressWindow progressWindow)
		{
			await Logger.LogVerboseAsync($"[DownloadOrchestration] Pre-resolving URLs for: {component.Name}").ConfigureAwait(false);
			var urlToFilenames = await _cacheService.PreResolveUrlsAsync(component, downloadManager, sequential: false, progressWindow.CancellationToken).ConfigureAwait(false);

			foreach (var kvp in urlToFilenames)
			{
				string url = kvp.Key;
				List<string> filenames = kvp.Value;

				if (filenames.Count > 0)
				{
					string firstFilename = filenames[0];
					string fullPath = Path.Combine(_mainConfig.sourcePath.FullName, firstFilename);

					progressWindow.UpdateDownloadProgress(new DownloadProgress
					{
						ModName = component.Name,
						Url = url,
						Status = DownloadStatus.Pending,
						StatusMessage = $"Ready to download: {firstFilename}",
						ProgressPercentage = 0,
						FilePath = fullPath,
						StartTime = DateTime.Now,
					});

					await Logger.LogVerboseAsync($"[DownloadOrchestration] Resolved URL to {filenames.Count} file(s): {url} -> {firstFilename}").ConfigureAwait(false);
				}
			}

			await Logger.LogVerboseAsync($"[DownloadOrchestration] Starting download for: {component.Name}").ConfigureAwait(false);

			var progressReporter = new Progress<DownloadProgress>(progress =>
			{
				progressWindow.UpdateDownloadProgress(progress);
			});

			var cacheEntries = await _cacheService.ResolveOrDownloadAsync(
				component,
				_mainConfig.sourcePath.FullName,
				progressReporter,
				sequential: false,
				progressWindow.CancellationToken).ConfigureAwait(false);

			await Logger.LogVerboseAsync($"[DownloadOrchestration] Processed component '{component.Name}': {cacheEntries.Count} cache entries").ConfigureAwait(false);

			if (cacheEntries.Count > 0 && cacheEntries.Any(e => e.IsArchiveFile))
			{
				var firstArchive = cacheEntries.First(e => e.IsArchiveFile);
				if (!string.IsNullOrEmpty(firstArchive.FileName) && MainConfig.SourcePath != null)
				{
					string fullPath = Path.Combine(MainConfig.SourcePath.FullName, firstArchive.FileName);
					if (File.Exists(fullPath))
					{
						bool generated = AutoInstructionGenerator.GenerateInstructions(component, fullPath);
						if (generated)
						{
							await Logger.LogVerboseAsync($"[DownloadOrchestration] Auto-generated instructions for '{component.Name}'").ConfigureAwait(false);
						}
					}
				}
			}

			Interlocked.Increment(ref _completedComponents);
			await Dispatcher.UIThread.InvokeAsync(() => DownloadStateChanged?.Invoke(this, EventArgs.Empty));
		}

		public void CancelAllDownloads(bool closeWindow = false)
		{
			try
			{
				if (_currentDownloadWindow != null && _currentDownloadWindow.IsVisible)
				{
					_currentDownloadWindow.CancelDownloads();
					Logger.Log($"Download cancellation requested by user (closeWindow: {closeWindow})");

					if (closeWindow)
					{

						_ = Task.Run(async () =>
						{
							await Task.Delay(100).ConfigureAwait(true);
							await Dispatcher.UIThread.InvokeAsync(() =>
							{
								try
								{
									_currentDownloadWindow?.Close();
									_currentDownloadWindow = null;
								}
								catch (Exception ex)
								{
									Logger.LogWarning($"Error closing download window: {ex.Message}");
								}
							});
						});
					}
				}

				IsDownloadInProgress = false;
				DownloadStateChanged?.Invoke(this, EventArgs.Empty);
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "Error cancelling downloads");
			}
		}

		public async Task ShowDownloadStatusAsync()
		{
			try
			{

				if (_currentDownloadWindow != null && _currentDownloadWindow.IsVisible)
				{
					_currentDownloadWindow.Activate();
					_ = _currentDownloadWindow.Focus();
					return;
				}

				int downloadedCount = _mainConfig.allComponents.Count(c => c.IsSelected && c.IsDownloaded);
				int totalSelected = _mainConfig.allComponents.Count(c => c.IsSelected);

				string statusMessage;
				if (totalSelected == 0)
				{
					statusMessage = "No mods are currently selected for installation.";
				}
				else if (downloadedCount == totalSelected)
				{
					statusMessage = $"All {totalSelected} selected mod(s) are downloaded and ready for installation!";
				}
				else
				{
					int missing = totalSelected - downloadedCount;
					statusMessage = "Download Status:\n\n" +
									$"• Downloaded: {downloadedCount}/{totalSelected}\n" +
									$"• Missing: {missing}\n\n" +
									"Click 'Fetch Downloads' to automatically download missing mods.";
				}

				await InformationDialog.ShowInformationDialogAsync(_parentWindow, statusMessage).ConfigureAwait(true);
			}
			catch (Exception ex)
			{
				await Logger.LogExceptionAsync(ex, "Error showing download status").ConfigureAwait(false);
			}
		}

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
        private async Task HandleRetryDownloadAsync(
			DownloadProgress progress,
			IReadOnlyList<ModComponent> components,
			DownloadManager downloadManager,
			DownloadProgressWindow progressWindow)
		{
			try
			{
				await Logger.LogAsync($"[HandleRetryDownload] Starting retry for: {progress.ModName} ({progress.Url})").ConfigureAwait(false);

				ModComponent matchingComponent = components.FirstOrDefault(c => string.Equals(c.Name, progress.ModName, StringComparison.Ordinal) && c.ModLinkFilenames.Keys.Any(link => string.Equals(link, progress.Url, StringComparison.Ordinal)));

				if (matchingComponent is null)
				{
					await Logger.LogErrorAsync($"[HandleRetryDownload] Could not find matching component for {progress.ModName}").ConfigureAwait(false);
					progress.Status = DownloadStatus.Failed;
					progress.ErrorMessage = "Could not find matching component for retry";
					return;
				}

				progressWindow.ResetCancellationToken();

				var progressReporter = new Progress<DownloadProgress>(update =>
				{

					progress.Status = update.Status;
					progress.StatusMessage = update.StatusMessage;
					progress.ProgressPercentage = update.ProgressPercentage;
					progress.BytesDownloaded = update.BytesDownloaded;
					progress.TotalBytes = update.TotalBytes;
					progress.FilePath = update.FilePath;
					progress.StartTime = update.StartTime;
					progress.EndTime = update.EndTime;
					progress.ErrorMessage = update.ErrorMessage;
					progress.Exception = update.Exception;
				});

				var urlToProgressMap = new Dictionary<string, DownloadProgress>(StringComparer.Ordinal) { { progress.Url, progress } };
				List<DownloadResult> results = await downloadManager.DownloadAllWithProgressAsync(
					urlToProgressMap,
					_mainConfig.sourcePath.FullName,
					progressReporter,
					progressWindow.CancellationToken).ConfigureAwait(false);

				if (results.Count > 0 && results[0].Success)
				{
					await Logger.LogAsync($"[HandleRetryDownload] Retry successful for {progress.ModName}").ConfigureAwait(false);

					string filePath = results[0].FilePath;

					// Validate that we have a valid file path
					if (string.IsNullOrWhiteSpace(filePath))
					{
						await Logger.LogErrorAsync($"[HandleRetryDownload] Download succeeded but FilePath is empty for {progress.ModName}").ConfigureAwait(false);
						return;
					}

					string fileName = Path.GetFileName(filePath);
					bool isArchive = ArchiveHelper.IsArchive(filePath);

					var cacheEntry = new DownloadCacheEntry
					{
						Url = progress.Url,
						FileName = fileName,
						IsArchiveFile = isArchive,
					};


					DownloadCacheService.AddOrUpdate(progress.Url, cacheEntry);
					AddOrUpdate(progress.Url, cacheEntry);

					if (isArchive && matchingComponent.Instructions.Count == 0)
					{
						bool generated = AutoInstructionGenerator.GenerateInstructions(matchingComponent, filePath);
						if (generated)
						{
							await Logger.LogVerboseAsync($"[HandleRetryDownload] Auto-generated instructions for '{matchingComponent.Name}'").ConfigureAwait(false);
						}
					}
				}
				else
				{
					string errorMessage = results.Count > 0 ? results[0].Message : "Unknown error during retry";
					await Logger.LogErrorAsync($"[HandleRetryDownload] Retry failed: {errorMessage}").ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				await Logger.LogExceptionAsync(ex, $"[HandleRetryDownload] Exception during retry for {progress.ModName}").ConfigureAwait(false);
				progress.Status = DownloadStatus.Failed;
				progress.ErrorMessage = $"Retry failed: {ex.Message}";
				progress.Exception = ex;
			}
		}

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
        public static async Task<string> DownloadModFromUrlAsync(string url, ModComponent component, CancellationToken cancellationToken = default)
		{
			try
			{
				await Logger.LogVerboseAsync($"[DownloadOrchestration] Starting single download from: {url}").ConfigureAwait(false);

				var progress = new DownloadProgress
				{
					ModName = component?.Name ?? "Unknown Mod",
					Url = url,
					Status = DownloadStatus.Pending,
					StatusMessage = "Preparing download...",
					ProgressPercentage = 0,
				};

				var downloadManager = DownloadHandlerFactory.CreateDownloadManager();

				string guidString = Guid.NewGuid().ToString("N");
				string shortGuid = guidString.Substring(0, Math.Min(8, guidString.Length));
				string tempDir = Path.Combine(Path.GetTempPath(), "KOTORModSync_AutoGen_" + shortGuid);
				_ = Directory.CreateDirectory(tempDir);

				var urlToProgressMap = new Dictionary<string, DownloadProgress>(StringComparer.Ordinal) { { url, progress } };
				var progressReporter = new Progress<DownloadProgress>(update =>
				{

					progress.Status = update.Status;
					progress.StatusMessage = update.StatusMessage;
					progress.ProgressPercentage = update.ProgressPercentage;
					progress.BytesDownloaded = update.BytesDownloaded;
					progress.TotalBytes = update.TotalBytes;
					progress.FilePath = update.FilePath;
					progress.StartTime = update.StartTime;
					progress.EndTime = update.EndTime;
					progress.ErrorMessage = update.ErrorMessage;
					progress.Exception = update.Exception;
				});
				List<DownloadResult> results = await downloadManager.DownloadAllWithProgressAsync(
					urlToProgressMap, tempDir, progressReporter, cancellationToken).ConfigureAwait(false);

				if (results.Count > 0 && results[0].Success)
				{
					await Logger.LogVerboseAsync($"[DownloadOrchestration] Download successful: {results[0].FilePath}").ConfigureAwait(false);
					return results[0].FilePath;
				}
				else
				{
					string errorMessage = results.Count > 0 ? results[0].Message : "Unknown error";
					await Logger.LogErrorAsync($"[DownloadOrchestration] Download failed: {errorMessage}").ConfigureAwait(false);

					try
					{
						Directory.Delete(tempDir, recursive: true);
					}
					catch (Exception ex)
					{
						await Logger.LogWarningAsync($"[DownloadOrchestration] Failed to clean up temp directory: {ex.Message}").ConfigureAwait(false);
					}

					return null;
				}
			}
			catch (Exception ex)
			{
				await Logger.LogExceptionAsync(ex, $"[DownloadOrchestration] Exception during download from {url}").ConfigureAwait(false);
				return null;
			}
		}

		private static async Task HandleStopDownloadAsync(DownloadProgress progress)
		{
			try
			{
				await Logger.LogAsync($"[HandleStopDownload] Stop requested for: {progress.ModName} ({progress.Url})").ConfigureAwait(false);

				progress.Status = DownloadStatus.Failed;
				progress.StatusMessage = "Stopped by user";
				progress.ErrorMessage = "Download stopped - click retry to restart";

				await Logger.LogAsync($"[HandleStopDownload] Download marked as stopped: {progress.ModName}").ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				await Logger.LogErrorAsync($"[HandleStopDownload] Failed to stop download: {ex.Message}").ConfigureAwait(false);
				progress.Status = DownloadStatus.Failed;
				progress.ErrorMessage = $"Failed to stop: {ex.Message}";
			}
		}

		private async Task HandleResumeDownloadAsync(
			DownloadProgress progress,
			IReadOnlyList<ModComponent> components,
			DownloadManager downloadManager,
			DownloadProgressWindow progressWindow)
		{
			try
			{
				await Logger.LogAsync($"[HandleResumeDownload] Resuming download: {progress.ModName} ({progress.Url})").ConfigureAwait(false);

				progress.Status = DownloadStatus.Pending;
				progress.StatusMessage = "Retrying download...";
				progress.ErrorMessage = null;
				progress.Exception = null;

				await HandleRetryDownloadAsync(progress, components, downloadManager, progressWindow).ConfigureAwait(false);

				await Logger.LogAsync($"[HandleResumeDownload] Download resumed: {progress.ModName}").ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				await Logger.LogErrorAsync($"[HandleResumeDownload] Failed to resume download: {ex.Message}").ConfigureAwait(false);
				progress.Status = DownloadStatus.Failed;
				progress.ErrorMessage = $"Failed to resume: {ex.Message}";
			}
		}

		private async Task HandleStartDownloadAsync(
			DownloadProgress progress,
			IReadOnlyList<ModComponent> components,
			DownloadManager downloadManager,
			DownloadProgressWindow progressWindow)
		{
			try
			{
				await Logger.LogAsync($"[HandleStartDownload] Starting download: {progress.ModName} ({progress.Url})").ConfigureAwait(false);

				progress.Status = DownloadStatus.Pending;
				progress.StatusMessage = "Starting download...";
				progress.ErrorMessage = null;
				progress.Exception = null;

				await HandleRetryDownloadAsync(progress, components, downloadManager, progressWindow).ConfigureAwait(false);

				await Logger.LogAsync($"[HandleStartDownload] Download started: {progress.ModName}").ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				await Logger.LogErrorAsync($"[HandleStartDownload] Failed to start download: {ex.Message}").ConfigureAwait(false);
				progress.Status = DownloadStatus.Failed;
				progress.ErrorMessage = $"Failed to start: {ex.Message}";
			}
		}

        /// <summary>
        /// Resolves filenames for a URL using cache, and only downloads if files don't exist on disk.
        /// This is the preferred method for auto-generate instructions flow.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
        public async Task<List<string>> ResolveAndCacheModFilesAsync(ModComponent component, CancellationToken cancellationToken = default)
		{
			try
			{
				await Logger.LogVerboseAsync($"[DownloadOrchestration] Resolving and caching files for component: {component.Name}").ConfigureAwait(false);

				// Step 1: Use PreResolveUrlsAsync to get cached/resolved filenames
				IReadOnlyDictionary<string, List<string>> resolvedUrls = await _cacheService.PreResolveUrlsAsync(
					component,
					_cacheService.DownloadManager,
					sequential: false,
					cancellationToken).ConfigureAwait(false);

				if (resolvedUrls.Count == 0)
				{
					await Logger.LogWarningAsync($"[DownloadOrchestration] No URLs resolved for component '{component.Name}'").ConfigureAwait(false);
					return new List<string>();
				}

				await Logger.LogVerboseAsync($"[DownloadOrchestration] Resolved {resolvedUrls.Count} URL(s) with filenames").ConfigureAwait(false);

				// Step 2: Check which files exist on disk
				var existingFiles = new List<string>();
				var urlsNeedingDownload = new List<string>();

				foreach (KeyValuePair<string, List<string>> kvp in resolvedUrls)
				{
					string url = kvp.Key;
					List<string> filenames = kvp.Value;

					if (filenames is null || filenames.Count == 0)


					{
						await Logger.LogWarningAsync($"[DownloadOrchestration] No filenames resolved for URL: {url}").ConfigureAwait(false);
						continue;
					}

					// Check if any of the files exist
					bool anyFileExists = false;
					foreach (string filename in filenames)
					{
						string filePath = Path.Combine(_mainConfig.sourcePath.FullName, filename);
						if (File.Exists(filePath))
						{
							existingFiles.Add(filePath);
							anyFileExists = true;


							await Logger.LogVerboseAsync($"[DownloadOrchestration] File already exists: {filename}").ConfigureAwait(false);
						}
					}

					if (!anyFileExists)
					{
						urlsNeedingDownload.Add(url);
						await Logger.LogVerboseAsync($"[DownloadOrchestration] Files missing for URL: {url}").ConfigureAwait(false);
					}
				}

				// Step 3: Download missing files if needed
				if (urlsNeedingDownload.Count > 0)


				{
					await Logger.LogAsync($"[DownloadOrchestration] Downloading {urlsNeedingDownload.Count} missing file(s) for '{component.Name}'").ConfigureAwait(false);

					IReadOnlyList<DownloadCacheEntry> downloadedEntries = await _cacheService.ResolveOrDownloadAsync(
						component,
						_mainConfig.sourcePath.FullName,
						progress: null,
						sequential: false,
						cancellationToken
					).ConfigureAwait(false);

					foreach (var fileName in downloadedEntries.Select(entry => entry.FileName).Where(fn => !string.IsNullOrEmpty(fn)))
					{
						string filePath = Path.Combine(_mainConfig.sourcePath.FullName, fileName);
						if (File.Exists(filePath) && !existingFiles.Contains(filePath, StringComparer.Ordinal))
						{
							existingFiles.Add(filePath);
							await Logger.LogVerboseAsync($"[DownloadOrchestration] Downloaded file: {fileName}").ConfigureAwait(false);
						}
					}
				}
				else
				{
					await Logger.LogVerboseAsync($"[DownloadOrchestration] All files already exist on disk for '{component.Name}'").ConfigureAwait(false);
				}

				return existingFiles;
			}
			catch (Exception ex)
			{
				await Logger.LogExceptionAsync(ex, $"[DownloadOrchestration] Failed to resolve/download files for component '{component.Name}'").ConfigureAwait(false);
				return new List<string>();
			}
		}

		/// <summary>
		/// Gets download URLs for a component from its ModLinkFilenames
		/// </summary>
		/// <param name="component">The component to get URLs for</param>
		/// <returns>List of download URLs</returns>
		private static async Task<List<string>> GetDownloadUrlsForComponentAsync(ModComponent component)
		{
			try
			{
				if (component?.ModLinkFilenames is null)
				{
					await Logger.LogWarningAsync("[DownloadOrchestration] Component or ModLinkFilenames is null").ConfigureAwait(false);
					return new List<string>();
				}

				List<string> urls = component.ModLinkFilenames.Keys
					.Where(url => !string.IsNullOrWhiteSpace(url))
					.ToList();

				await Logger.LogVerboseAsync($"[DownloadOrchestration] Found {urls.Count} download URLs for component: {component.Name}").ConfigureAwait(false);
				return urls;
			}
			catch (Exception ex)
			{
				await Logger.LogExceptionAsync(ex, $"[DownloadOrchestration] Error getting download URLs for component: {component?.Name}").ConfigureAwait(false);
				return new List<string>();
			}
		}

        /// <summary>
        /// Downloads a single ModComponent using the specified progress window
        /// </summary>
        /// <param name="component">The component to download</param>
        /// <param name="progressWindow">The progress window to use</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
        public async Task DownloadSingleComponentAsync(ModComponent component, DownloadProgressWindow progressWindow)
		{
			if (component is null)
			{
				await Logger.LogWarningAsync("[DownloadOrchestration] Cannot download null component").ConfigureAwait(false);
				return;
			}

			if (progressWindow is null)
			{
				await Logger.LogWarningAsync("[DownloadOrchestration] Progress window is null").ConfigureAwait(false);
				return;
			}

			try
			{
				await Logger.LogVerboseAsync($"[DownloadOrchestration] Starting single component download for: {component.Name}").ConfigureAwait(false);

				// Get download URLs for this component
				var urls = await GetDownloadUrlsForComponentAsync(component).ConfigureAwait(false);

				if (urls.Count == 0)
				{
					await Logger.LogWarningAsync($"[DownloadOrchestration] No download URLs found for component: {component.Name}").ConfigureAwait(false);
					return;
				}

				// Create download progress items
				var downloadItems = new List<DownloadProgress>();
				foreach (string url in urls)
				{
					var progress = new DownloadProgress
					{
						ModName = component.Name,
						Url = url,
						Status = DownloadStatus.Pending,
						StatusMessage = "Queued for download",
					};
					downloadItems.Add(progress);
					progressWindow.AddDownload(progress);
				}

				// Start downloads
				foreach (var progress in downloadItems)
				{
					try
					{
						await _cacheService.DownloadManager.DownloadFileAsync(
							progress.Url,
							progress,
							progressWindow.CancellationToken).ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						await Logger.LogExceptionAsync(ex, $"Failed to download {progress.Url}").ConfigureAwait(false);
						progress.Status = DownloadStatus.Failed;
						progress.ErrorMessage = ex.Message;
					}
				}

				await Logger.LogVerboseAsync($"[DownloadOrchestration] Single component download completed for: {component.Name}").ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				await Logger.LogExceptionAsync(ex, $"[DownloadOrchestration] Error downloading single component: {component.Name}").ConfigureAwait(false);
			}
		}

        /// <summary>
        /// Handles download control events (start, stop, retry, etc.)
        /// </summary>
        /// <param name="progress">The download progress item</param>
        /// <param name="action">The action to perform</param>
        /// <param name="progressWindow">The progress window</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
        public async Task HandleDownloadControlAsync(
			DownloadProgress progress,
			DownloadControlAction action,
			DownloadProgressWindow progressWindow)
		{
			if (progress is null || progressWindow is null)
				return;

			try
			{
				switch (action)
				{
					case DownloadControlAction.Start:
						if (progress.Status == DownloadStatus.Pending)
						{
							progress.Status = DownloadStatus.InProgress;
							progress.StatusMessage = "Starting download...";

							// Start the actual download
							_ = Task.Run(async () =>
							{
								try
								{
									await _cacheService.DownloadManager.DownloadFileAsync(
										progress.Url,
										progress,
										progressWindow.CancellationToken).ConfigureAwait(false);
								}
								catch (Exception ex)
								{
									progress.Status = DownloadStatus.Failed;
									progress.ErrorMessage = ex.Message;
								}
							}, _currentDownloadWindow?.CancellationToken ?? CancellationToken.None);
						}
						break;

					case DownloadControlAction.Stop:
						if (progress.Status == DownloadStatus.InProgress)
						{
							progress.Status = DownloadStatus.Failed;
							progress.StatusMessage = "Download stopped by user";
							progress.ErrorMessage = "Download was stopped by user";
						}
						break;

					case DownloadControlAction.Retry:
						if (progress.Status == DownloadStatus.Failed || progress.Status == DownloadStatus.Skipped)
						{
							progress.Status = DownloadStatus.Pending;
							progress.StatusMessage = "Queued for retry";
							progress.ErrorMessage = null;

							// Retry the download
							_ = Task.Run(async () =>
							{
								try
								{
									await _cacheService.DownloadManager.DownloadFileAsync(
										progress.Url,
										progress,
										progressWindow.CancellationToken).ConfigureAwait(false);
								}
								catch (Exception ex)
								{
									progress.Status = DownloadStatus.Failed;
									progress.ErrorMessage = ex.Message;
								}
							}, _currentDownloadWindow?.CancellationToken ?? CancellationToken.None);
						}
						break;
				}
			}
			catch (Exception ex)
			{
				await Logger.LogExceptionAsync(ex, $"Error handling download control action: {action}").ConfigureAwait(false);
			}
		}
	}
}