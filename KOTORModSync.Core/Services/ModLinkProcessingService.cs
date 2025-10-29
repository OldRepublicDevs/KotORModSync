// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using KOTORModSync.Core.Services.Download;

namespace KOTORModSync.Core.Services
{

	public class ModLinkProcessingService
	{
		private readonly DownloadCacheService _downloadCacheService;

		public ModLinkProcessingService( DownloadCacheService downloadCacheService = null )
		{
			_downloadCacheService = downloadCacheService ?? new DownloadCacheService();

			try
			{
				_downloadCacheService.SetDownloadManager( Download.DownloadHandlerFactory.CreateDownloadManager() );
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, "Failed to configure default DownloadManager for ModLinkProcessingService" );
			}
		}

		public async Task<int> ProcessComponentModLinksAsync(
			List<ModComponent> components,
			string downloadDirectory,
			IProgress<Download.DownloadProgress> progress = null,
			CancellationToken cancellationToken = default )
		{
			if (components == null || components.Count == 0)
				return 0;

			if (string.IsNullOrWhiteSpace( downloadDirectory ))
				return 0;

			int successCount = 0;

			foreach (ModComponent component in components)
			{
				try
				{
					if (component.ModLinkFilenames == null || component.ModLinkFilenames.Count == 0)
						continue;

					int initialInstructionCount = component.Instructions.Count;

					bool generated = await AutoInstructionGenerator.GenerateInstructionsFromUrlsAsync(
						component,
						_downloadCacheService,


						cancellationToken )

.ConfigureAwait( false );

					if (generated && component.Instructions.Count > initialInstructionCount)
					{
						successCount++;
						int newInstructions = component.Instructions.Count - initialInstructionCount;
						await Logger.LogAsync( $"Added {newInstructions} placeholder instruction(s) for '{component.Name}': {component.InstallationMethod}" ).ConfigureAwait( false );
					}
				}
				catch (Exception ex)


				{
					await Logger.LogExceptionAsync( ex, $"Error processing component '{component.Name}'" ).ConfigureAwait( false );
				}
			}

			if (successCount > 0)


			{
				await Logger.LogAsync( $"Processed ModLinks and generated placeholder instructions for {successCount} component(s)." ).ConfigureAwait( false );
			}

			return successCount;
		}


		public int ProcessComponentModLinksSync(
			List<ModComponent> components,
			string downloadDirectory,
			IProgress<Download.DownloadProgress> progress = null,
			CancellationToken cancellationToken = default )
		{
			Task<int> task = ProcessComponentModLinksAsync( components, downloadDirectory, progress, cancellationToken );
			task.Wait( cancellationToken );
			return task.Result;
		}
	}
}