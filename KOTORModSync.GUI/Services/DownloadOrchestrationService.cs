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

				// Create and show the download progress window
				var progressWindow = new DownloadProgressWindow();
				_currentDownloadWindow = progressWindow;

				// Setup download manager with all handlers
				var httpClient = new System.Net.Http.HttpClient();
				httpClient.Timeout = TimeSpan.FromMinutes(10);

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
							foreach (ModComponent component in selectedComponents)
							{
								foreach (string url in component.ModLink)
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

						foreach ( ModComponent component in selectedComponents )
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
						}

						// Mark downloads as completed
						progressWindow.MarkCompleted();

						// Run validation
						await Logger.LogVerboseAsync("[DownloadOrchestration] Running post-download validation");
						await Dispatcher.UIThread.InvokeAsync(() => onScanComplete?.Invoke());
					}
					catch ( Exception ex )
					{
						await Logger.LogExceptionAsync(ex, "Error during mod download");
						await Dispatcher.UIThread.InvokeAsync(async () =>
							await InformationDialog.ShowInformationDialog(_parentWindow,
								$"An error occurred while downloading mods:\n\n{ex.Message}"));
					}
				});
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Error starting download session");
				await InformationDialog.ShowInformationDialog(_parentWindow,
					$"An error occurred while starting downloads:\n\n{ex.Message}");
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
	}
}

