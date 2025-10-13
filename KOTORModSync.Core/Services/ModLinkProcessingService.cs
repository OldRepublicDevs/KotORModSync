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
	/// <summary>
	/// Unified service for processing ModLinks - downloading archives and auto-generating instructions.
	/// This is the central pipeline used by both GUI and tests.
	/// </summary>
	public class ModLinkProcessingService
	{
		private readonly DownloadCacheService _downloadCacheService;

		public ModLinkProcessingService(DownloadCacheService downloadCacheService = null)
		{
			_downloadCacheService = downloadCacheService ?? new DownloadCacheService();
			// Ensure a DownloadManager is configured so the unified pipeline works in the app (not just tests)
			// Tests may inject a preconfigured DownloadCacheService; only configure a default manager when none is set
			try
			{
				// Build default handler set mirroring production ordering
				var httpClient = new HttpClient();
				var handlers = new List<IDownloadHandler>
				{
					new DeadlyStreamDownloadHandler(httpClient),
					new MegaDownloadHandler(),
					new NexusModsDownloadHandler(httpClient, ""),
					new GameFrontDownloadHandler(httpClient),
					new DirectDownloadHandler(httpClient) // catch-all must be last
				};
				_downloadCacheService.SetDownloadManager(new DownloadManager(handlers));
			}
			catch ( Exception ex )
			{
				// Non-fatal: pipeline will still operate for local archives; log for diagnostics
				Logger.LogException(ex, "Failed to configure default DownloadManager for ModLinkProcessingService");
			}
		}

		/// <summary>
		/// Processes ALL ModLinks for ALL components.
		/// Downloads archives (if needed) and auto-generates instructions.
		/// Ensures duplicate instructions are not created.
		/// This is the unified pipeline used throughout the application.
		/// </summary>
		/// <param name="components">Components to process</param>
		/// <param name="downloadDirectory">Directory to download archives to (usually MainConfig.SourcePath)</param>
		/// <param name="progress">Optional progress reporter</param>
		/// <param name="cancellationToken">Cancellation token</param>
		/// <returns>Number of components that had instructions generated/updated</returns>
		public async Task<int> ProcessComponentModLinksAsync(
			List<ModComponent> components,
			string downloadDirectory,
			IProgress<Download.DownloadProgress> progress = null,
			CancellationToken cancellationToken = default)
		{
			if (components == null || components.Count == 0)
				return 0;

			if (string.IsNullOrWhiteSpace(downloadDirectory))
				return 0;

			int successCount = 0;

			foreach (ModComponent component in components)
			{
				try
				{
					// Skip if no ModLinks
					if (component.ModLink == null || component.ModLink.Count == 0)
						continue;

					// Track how many instructions we had before
					int initialInstructionCount = component.Instructions.Count;

					// Process ALL ModLinks (not just the first one)
					// Even if we already have instructions, process all ModLinks
					foreach (string modLink in component.ModLink)
					{
						if (string.IsNullOrWhiteSpace(modLink))
							continue;

						// Skip non-URL links (anchor links, relative paths already converted, etc.)
						if (!modLink.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
							!modLink.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
							continue;

						try
						{
							// Call DownloadCacheService to download/cache the archive
							// This automatically creates basic Extract instructions (avoiding duplicates)
							List<DownloadCacheEntry> cacheEntries = await _downloadCacheService.ResolveOrDownloadAsync(
								component,
								downloadDirectory,
								progress,
								cancellationToken);

							// For each downloaded archive, analyze and generate detailed instructions
							if (cacheEntries != null && cacheEntries.Count > 0)
							{
								foreach (var entry in cacheEntries)
								{
									// Only analyze archives that were actually downloaded
									if (entry.IsArchive && !string.IsNullOrEmpty(entry.FilePath) && System.IO.File.Exists(entry.FilePath))
									{
										// Use AutoInstructionGenerator to analyze archive and create detailed instructions
										// This creates TSLPatcher, Move, Choose instructions based on archive contents
										bool generated = AutoInstructionGenerator.GenerateInstructions(component, entry.FilePath);
										if (generated)
										{
											await Logger.LogVerboseAsync($"Auto-generated detailed instructions for '{component.Name}' from {entry.ArchiveName}");
											// Mark as downloaded since we successfully resolved a local archive
											component.IsDownloaded = true;
										}
									}
								}
							}

							// If we got cache entries and instructions were added, log success
							if (cacheEntries != null && cacheEntries.Count > 0 && component.Instructions.Count > initialInstructionCount)
							{
								await Logger.LogVerboseAsync($"Successfully processed ModLink for '{component.Name}': {modLink}");
							}
						}
						catch (Exception ex)
						{
							// Download/processing failed for this link - continue to next ModLink
							await Logger.LogVerboseAsync($"Failed to process ModLink for '{component.Name}': {modLink} - {ex.Message}");
							continue;
						}
					}

					// Check if we successfully generated/updated instructions
					if (component.Instructions.Count > initialInstructionCount)
					{
						successCount++;
						int newInstructions = component.Instructions.Count - initialInstructionCount;
						await Logger.LogAsync($"Added {newInstructions} new instruction(s) for '{component.Name}': {component.InstallationMethod}");
					}
				}
				catch (Exception ex)
				{
					await Logger.LogExceptionAsync(ex, $"Error processing component '{component.Name}'");
				}
			}

			if (successCount > 0)
			{
				await Logger.LogAsync($"Processed ModLinks and generated/updated instructions for {successCount} component(s).");
			}

			return successCount;
		}

		/// <summary>
		/// Synchronous version for test setup and other scenarios where async is not possible.
		/// Blocks until completion.
		/// </summary>
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

