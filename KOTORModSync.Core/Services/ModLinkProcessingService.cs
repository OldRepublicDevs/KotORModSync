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

				var httpClient = new HttpClient();
				var handlers = new List<IDownloadHandler>
			{
				new DeadlyStreamDownloadHandler(httpClient),
				new MegaDownloadHandler(),
				new NexusModsDownloadHandler(httpClient, ""),
				new GameFrontDownloadHandler(httpClient),
				new DirectDownloadHandler(httpClient)
			};
				_downloadCacheService.SetDownloadManager(new DownloadManager(handlers));
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

					// Pre-resolve URLs to filenames and generate placeholder instructions
					bool generated = await AutoInstructionGenerator.GenerateInstructionsFromUrlsAsync(
						component,
						_downloadCacheService,
						cancellationToken);

					if ( generated && component.Instructions.Count > initialInstructionCount )
					{
						successCount++;
						int newInstructions = component.Instructions.Count - initialInstructionCount;
						await Logger.LogAsync($"Added {newInstructions} placeholder instruction(s) for '{component.Name}': {component.InstallationMethod}");
					}
				}
				catch ( Exception ex )
				{
					await Logger.LogExceptionAsync(ex, $"Error processing component '{component.Name}'");
				}
			}

			if ( successCount > 0 )
			{
				await Logger.LogAsync($"Processed ModLinks and generated placeholder instructions for {successCount} component(s).");
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

