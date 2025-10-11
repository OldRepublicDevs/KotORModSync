// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
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
using KOTORModSync.Dialogs;

namespace KOTORModSync.Services
{
	/// <summary>
	/// SINGLE SERVICE for ALL download operations - consolidates all download logic
	/// </summary>
	public class DownloadOrchestrationService
	{
		private readonly DownloadCacheService _cacheService;
		private readonly Window _parentWindow;
		private readonly MainConfig _mainConfig;
		private DownloadProgressWindow _currentDownloadWindow;

		// Download state tracking
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

		/// <summary>
		/// THE MAIN ENTRY POINT - orchestrates downloading all selected mods
		/// </summary>
		public async Task StartDownloadSessionAsync(Action onScanComplete)
		{
			try
			{
				// If download window is already open and visible, just activate it
				if ( _currentDownloadWindow != null && _currentDownloadWindow.IsVisible )
				{
					_currentDownloadWindow.Activate();
					_ = _currentDownloadWindow.Focus();
					await Logger.LogVerboseAsync("[DownloadOrchestration] Download window already open, activating existing window");
					return;
				}

				// Validate preconditions
				if ( _mainConfig.sourcePath == null || !Directory.Exists(_mainConfig.sourcePath.FullName) )
				{
					await InformationDialog.ShowInformationDialog(_parentWindow,
						"Please set your Mod Directory in Settings before downloading mods.");
					return;
				}

				if ( _mainConfig.allComponents.Count == 0 )
				{
					await InformationDialog.ShowInformationDialog(_parentWindow,
						"Please load a file (TOML or Markdown) before downloading mods.");
					return;
				}

				// Get all selected components that have ModLinks
				var selectedComponents = _mainConfig.allComponents
					.Where(c => c.IsSelected && c.ModLink.Count > 0)
					.ToList();

				if ( selectedComponents.Count == 0 )
				{
					await InformationDialog.ShowInformationDialog(_parentWindow,
						"No selected mods have download links available.");
					return;
				}

				// Initialize download state tracking
				_totalComponentsToDownload = selectedComponents.Count;
				_completedComponents = 0;
				IsDownloadInProgress = true;
				DownloadStateChanged?.Invoke(this, EventArgs.Empty);

				// Create and show the download progress window
				var progressWindow = new DownloadProgressWindow();
				_currentDownloadWindow = progressWindow;

				// Clear the reference when the window closes
				progressWindow.Closed += (sender, e) =>
				{
					_currentDownloadWindow = null;
					Logger.LogVerbose("[DownloadOrchestration] Download window closed");
				};

				// Setup download manager with all handlers
				// Get timeout from the window's UI control
				int timeoutMinutes = progressWindow.DownloadTimeoutMinutes;
				await Logger.LogVerboseAsync($"[DownloadOrchestration] Using download timeout: {timeoutMinutes} minutes");

				var httpClient = new System.Net.Http.HttpClient();
				httpClient.Timeout = TimeSpan.FromMinutes(timeoutMinutes);

				var handlers = new List<IDownloadHandler>
			{
				new DeadlyStreamDownloadHandler(httpClient),
				new MegaDownloadHandler(),
				new NexusModsDownloadHandler(httpClient, null),
				new GameFrontDownloadHandler(httpClient),
				new DirectDownloadHandler(httpClient),
			};

				var downloadManager = new DownloadManager(handlers);
				_cacheService.SetDownloadManager(downloadManager);

				// Subscribe to download control events (retry, pause, etc.)
				progressWindow.DownloadControlRequested += async (sender, args) =>
				{
					try
					{
						switch ( args.Action )
						{
							case DownloadControlAction.Retry:
								await HandleRetryDownloadAsync(args.Progress, selectedComponents, downloadManager, progressWindow);
								break;
							case DownloadControlAction.Pause:
								await HandlePauseDownloadAsync(args.Progress, downloadManager);
								break;
							case DownloadControlAction.Resume:
								await HandleResumeDownloadAsync(args.Progress, selectedComponents, downloadManager, progressWindow);
								break;
							case DownloadControlAction.Start:
								await HandleStartDownloadAsync(args.Progress, selectedComponents, downloadManager, progressWindow);
								break;
						}
					}
					catch ( Exception ex )
					{
						await Logger.LogErrorAsync($"[DownloadControl] Failed to handle {args.Action}: {ex.Message}");
						args.Progress.Status = DownloadStatus.Failed;
						args.Progress.ErrorMessage = $"Control action failed: {ex.Message}";
					}
				};

				// Show the window
				progressWindow.Show();

				// Process all components through cache service
				_ = Task.Run(async () =>
				{
					try
					{
						await Logger.LogVerboseAsync($"[DownloadOrchestration] Processing {selectedComponents.Count} components");

						// First, create progress items for all components so the window shows them immediately
						Dispatcher.UIThread.Post(() =>
						{
							foreach ( ModComponent component in selectedComponents )
							{
								foreach ( string url in component.ModLink )
								{
									var initialProgress = new DownloadProgress
									{
										ModName = component.Name,
										Url = url,
										Status = DownloadStatus.Pending,
										StatusMessage = "Waiting to start...",
										ProgressPercentage = 0
									};
									progressWindow.AddDownload(initialProgress);
								}
							}
						});

						// Process all components CONCURRENTLY (not sequentially!)
						await Task.WhenAll(selectedComponents.Select(async component =>
						{
							try
							{
								// Create progress reporter for this component
								var progressReporter = new Progress<DownloadProgress>(progress =>
								{
									progressWindow.UpdateDownloadProgress(progress);
								});

								// THE ONLY DOWNLOAD ENTRY POINT - everything goes through cache service
								var cacheEntries = await _cacheService.ResolveOrDownloadAsync(
									component,
									_mainConfig.sourcePath.FullName,
									progressReporter,
									progressWindow.CancellationToken);

								await Logger.LogVerboseAsync($"[DownloadOrchestration] Processed component '{component.Name}': {cacheEntries.Count} cache entries");

								// Auto-generate instructions for archives
								if ( cacheEntries.Count > 0 && cacheEntries.Any(e => e.IsArchive) )
								{
									var firstArchive = cacheEntries.First(e => e.IsArchive);
									if ( !string.IsNullOrEmpty(firstArchive.FilePath) && File.Exists(firstArchive.FilePath) )
									{
										bool generated = AutoInstructionGenerator.GenerateInstructions(component, firstArchive.FilePath);
										if ( generated )
										{
											await Logger.LogVerboseAsync($"[DownloadOrchestration] Auto-generated instructions for '{component.Name}'");
										}
									}
								}

								// Update progress counter
								Interlocked.Increment(ref _completedComponents);
								await Dispatcher.UIThread.InvokeAsync(() => DownloadStateChanged?.Invoke(this, EventArgs.Empty));
							}
							catch ( Exception ex )
							{
								await Logger.LogExceptionAsync(ex, $"Error downloading component '{component.Name}'");
							}
						}));

						// Mark downloads as completed
						progressWindow.MarkCompleted();

						// Reset download state
						IsDownloadInProgress = false;
						await Dispatcher.UIThread.InvokeAsync(() => DownloadStateChanged?.Invoke(this, EventArgs.Empty));

						// Run validation
						await Logger.LogVerboseAsync("[DownloadOrchestration] Running post-download validation");
						await Dispatcher.UIThread.InvokeAsync(() => onScanComplete?.Invoke());
					}
					catch ( Exception ex )
					{
						await Logger.LogExceptionAsync(ex, "Error during mod download");
						await Dispatcher.UIThread.InvokeAsync(async () =>
						{
							IsDownloadInProgress = false;
							DownloadStateChanged?.Invoke(this, EventArgs.Empty);
							await InformationDialog.ShowInformationDialog(_parentWindow,
								$"An error occurred while downloading mods:\n\n{ex.Message}");
						});
					}
				});
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Error starting download session");
				IsDownloadInProgress = false;
				DownloadStateChanged?.Invoke(this, EventArgs.Empty);
				await InformationDialog.ShowInformationDialog(_parentWindow,
					$"An error occurred while starting downloads:\n\n{ex.Message}");
			}
		}

		/// <summary>
		/// Cancels all ongoing downloads
		/// </summary>
		public void CancelAllDownloads()
		{
			try
			{
				if (_currentDownloadWindow != null && _currentDownloadWindow.IsVisible)
				{
					_currentDownloadWindow.CancelDownloads();
					Logger.Log("Download cancellation requested by user");
				}

				IsDownloadInProgress = false;
				DownloadStateChanged?.Invoke(this, EventArgs.Empty);
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "Error cancelling downloads");
			}
		}

		/// <summary>
		/// Shows download status or activates the download window if running
		/// </summary>
		public async Task ShowDownloadStatusAsync()
		{
			try
			{
				// If download window is active, bring it to the front
				if ( _currentDownloadWindow != null && _currentDownloadWindow.IsVisible )
				{
					_currentDownloadWindow.Activate();
					_ = _currentDownloadWindow.Focus();
					return;
				}

				// Show download status summary
				int downloadedCount = _mainConfig.allComponents.Count(c => c.IsSelected && c.IsDownloaded);
				int totalSelected = _mainConfig.allComponents.Count(c => c.IsSelected);

				string statusMessage;
				if ( totalSelected == 0 )
				{
					statusMessage = "No mods are currently selected for installation.";
				}
				else if ( downloadedCount == totalSelected )
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

				await InformationDialog.ShowInformationDialog(_parentWindow, statusMessage);
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Error showing download status");
			}
		}

		/// <summary>
		/// Handles retry for a failed download
		/// </summary>
		private async Task HandleRetryDownloadAsync(
			DownloadProgress progress,
			IReadOnlyList<ModComponent> components,
			DownloadManager downloadManager,
			DownloadProgressWindow progressWindow)
		{
			try
			{
				await Logger.LogAsync($"[HandleRetryDownload] Starting retry for: {progress.ModName} ({progress.Url})");

				// Find the component that matches this download
				ModComponent matchingComponent = components.FirstOrDefault(c =>
					c.Name == progress.ModName && c.ModLink.Any(link => link == progress.Url));

				if ( matchingComponent == null )
				{
					await Logger.LogErrorAsync($"[HandleRetryDownload] Could not find matching component for {progress.ModName}");
					progress.Status = DownloadStatus.Failed;
					progress.ErrorMessage = "Could not find matching component for retry";
					return;
				}

				// Create a new progress reporter for this retry
				var progressReporter = new Progress<DownloadProgress>(update =>
				{
					// Update the existing progress object
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

				// Download just this one URL
				var urlToProgressMap = new Dictionary<string, DownloadProgress> { { progress.Url, progress } };
				List<DownloadResult> results = await downloadManager.DownloadAllWithProgressAsync(
					urlToProgressMap,
					_mainConfig.sourcePath.FullName,
					progressReporter,
					progressWindow.CancellationToken);

				// Process the result
				if ( results.Count > 0 && results[0].Success )
				{
					await Logger.LogAsync($"[HandleRetryDownload] Retry successful for {progress.ModName}");

					// Update the cache with the new download
					string filePath = results[0].FilePath;
					string fileName = Path.GetFileName(filePath);
					bool isArchive = DownloadCacheService.IsArchive(filePath);

					var cacheEntry = new DownloadCacheEntry
					{
						Url = progress.Url,
						ArchiveName = fileName,
						FilePath = filePath,
						IsArchive = isArchive
					};

					_cacheService.AddOrUpdate(matchingComponent.Guid, progress.Url, cacheEntry);

					// Auto-generate instructions if it's an archive
					if ( isArchive && matchingComponent.Instructions.Count == 0 )
					{
						bool generated = AutoInstructionGenerator.GenerateInstructions(matchingComponent, filePath);
						if ( generated )
						{
							await Logger.LogVerboseAsync($"[HandleRetryDownload] Auto-generated instructions for '{matchingComponent.Name}'");
						}
					}
				}
				else
				{
					string errorMessage = results.Count > 0 ? results[0].Message : "Unknown error during retry";
					await Logger.LogErrorAsync($"[HandleRetryDownload] Retry failed: {errorMessage}");
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, $"[HandleRetryDownload] Exception during retry for {progress.ModName}");
				progress.Status = DownloadStatus.Failed;
				progress.ErrorMessage = $"Retry failed: {ex.Message}";
				progress.Exception = ex;
			}
		}

		/// <summary>
		/// Downloads a single mod from URL (used for auto-generation)
		/// </summary>
		public static async Task<string> DownloadModFromUrlAsync(string url, ModComponent component)
		{
			try
			{
				await Logger.LogVerboseAsync($"[DownloadOrchestration] Starting single download from: {url}");

				var progress = new DownloadProgress
				{
					ModName = component?.Name ?? "Unknown Mod",
					Url = url,
					Status = DownloadStatus.Pending,
					StatusMessage = "Preparing download...",
					ProgressPercentage = 0
				};

				// Create download manager
				var httpClient = new System.Net.Http.HttpClient();
				var handlers = new List<IDownloadHandler>
				{
					new DeadlyStreamDownloadHandler(httpClient),
					new DirectDownloadHandler(httpClient),
					new GameFrontDownloadHandler(httpClient),
					new NexusModsDownloadHandler(httpClient, ""),
					new MegaDownloadHandler()
				};
				var downloadManager = new DownloadManager(handlers);

				// Create temp directory
				string guidString = Guid.NewGuid().ToString("N");
				string shortGuid = guidString.Substring(0, Math.Min(8, guidString.Length));
				string tempDir = Path.Combine(Path.GetTempPath(), "KOTORModSync_AutoGen_" + shortGuid);
				_ = Directory.CreateDirectory(tempDir);

				// Download
				var urlToProgressMap = new Dictionary<string, DownloadProgress> { { url, progress } };
				var progressReporter = new Progress<DownloadProgress>(update =>
				{
					// Forward updates to the existing progress object
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
					urlToProgressMap, tempDir, progressReporter, CancellationToken.None);

				httpClient.Dispose();

				if ( results.Count > 0 && results[0].Success )
				{
					await Logger.LogVerboseAsync($"[DownloadOrchestration] Download successful: {results[0].FilePath}");
					return results[0].FilePath;
				}
				else
				{
					string errorMessage = results.Count > 0 ? results[0].Message : "Unknown error";
					await Logger.LogErrorAsync($"[DownloadOrchestration] Download failed: {errorMessage}");

					// Clean up temp directory
					try
					{
						Directory.Delete(tempDir, recursive: true);
					}
					catch ( Exception ex )
					{
						await Logger.LogWarningAsync($"[DownloadOrchestration] Failed to clean up temp directory: {ex.Message}");
					}

					return null;
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, $"[DownloadOrchestration] Exception during download from {url}");
				return null;
			}
		}

		/// <summary>
		/// Handles pausing a download using cooperative cancellation
		/// </summary>
		private async Task HandlePauseDownloadAsync(DownloadProgress progress, DownloadManager downloadManager)
		{
			try
			{
				await Logger.LogAsync($"[HandlePauseDownload] Pausing download: {progress.ModName} ({progress.Url})");

				// Mark as paused using cooperative cancellation - this is safe
				progress.Status = DownloadStatus.Failed;
				progress.StatusMessage = "Download paused by user";
				progress.ErrorMessage = "Download was paused by user";

				// Use cooperative cancellation - this will signal cancellation but won't kill threads
				downloadManager.CancelAll();

				await Logger.LogAsync($"[HandlePauseDownload] Download paused using cooperative cancellation: {progress.ModName}");
			}
			catch (Exception ex)
			{
				await Logger.LogErrorAsync($"[HandlePauseDownload] Failed to pause download: {ex.Message}");
				progress.Status = DownloadStatus.Failed;
				progress.ErrorMessage = $"Failed to pause: {ex.Message}";
			}
		}

		/// <summary>
		/// Handles resuming a paused download
		/// </summary>
		private async Task HandleResumeDownloadAsync(
			DownloadProgress progress,
			IReadOnlyList<ModComponent> components,
			DownloadManager downloadManager,
			DownloadProgressWindow progressWindow)
		{
			try
			{
				await Logger.LogAsync($"[HandleResumeDownload] Resuming download: {progress.ModName} ({progress.Url})");

				// Reset the progress to pending and retry
				progress.Status = DownloadStatus.Pending;
				progress.StatusMessage = "Retrying download...";
				progress.ErrorMessage = null;
				progress.Exception = null;

				// Use the same retry logic
				await HandleRetryDownloadAsync(progress, components, downloadManager, progressWindow);

				await Logger.LogAsync($"[HandleResumeDownload] Download resumed: {progress.ModName}");
			}
			catch (Exception ex)
			{
				await Logger.LogErrorAsync($"[HandleResumeDownload] Failed to resume download: {ex.Message}");
				progress.Status = DownloadStatus.Failed;
				progress.ErrorMessage = $"Failed to resume: {ex.Message}";
			}
		}

		/// <summary>
		/// Handles starting a pending download
		/// </summary>
		private async Task HandleStartDownloadAsync(
			DownloadProgress progress,
			IReadOnlyList<ModComponent> components,
			DownloadManager downloadManager,
			DownloadProgressWindow progressWindow)
		{
			try
			{
				await Logger.LogAsync($"[HandleStartDownload] Starting download: {progress.ModName} ({progress.Url})");

				// Reset the progress to pending and start
				progress.Status = DownloadStatus.Pending;
				progress.StatusMessage = "Starting download...";
				progress.ErrorMessage = null;
				progress.Exception = null;

				// Use the same retry logic to start the download
				await HandleRetryDownloadAsync(progress, components, downloadManager, progressWindow);

				await Logger.LogAsync($"[HandleStartDownload] Download started: {progress.ModName}");
			}
			catch (Exception ex)
			{
				await Logger.LogErrorAsync($"[HandleStartDownload] Failed to start download: {ex.Message}");
				progress.Status = DownloadStatus.Failed;
				progress.ErrorMessage = $"Failed to start: {ex.Message}";
			}
		}
	}
}

