// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Controls;

using KOTORModSync.Converters;
using KOTORModSync.Core;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Services
{

	public class InstructionBrowsingService
	{
		private readonly MainConfig _mainConfig;
		private readonly DialogService _dialogService;

		public InstructionBrowsingService( MainConfig mainConfig, DialogService dialogService )
		{
			_mainConfig = mainConfig ?? throw new ArgumentNullException( nameof( mainConfig ) );
			_dialogService = dialogService ?? throw new ArgumentNullException( nameof( dialogService ) );
		}

		public async Task BrowseSourceFilesAsync( Instruction instruction, TextBox sourceTextBox )
		{
			try
			{
				if (instruction is null)
					throw new ArgumentNullException( nameof( instruction ) );

				var startFolder = _mainConfig.sourcePath != null
					? await _dialogService.GetStorageFolderFromPathAsync( _mainConfig.sourcePath.FullName )
.ConfigureAwait( false ) : null;

				string[] filePaths = await _dialogService.ShowFileDialogAsync(
					isFolderDialog: false,
					allowMultiple: true,
					startFolder: startFolder,
					windowName: "Select the files to perform this instruction on"






				).ConfigureAwait( false );

				if (filePaths is null || filePaths.Length == 0)
				{
					await Logger.LogVerboseAsync( "User did not select any files." ).ConfigureAwait( false );
					return;
				}

				await Logger.LogVerboseAsync( $"Selected files: [{string.Join( $",{Environment.NewLine}", filePaths )}]" ).ConfigureAwait( false );

				List<string> files = filePaths.ToList();
				if (files.Count == 0)


				{
					await Logger.LogVerboseAsync( "No files chosen, returning to previous values" ).ConfigureAwait( false );
					return;
				}

				for (int i = 0; i < files.Count; i++)
				{
					string filePath = files[i];
					files[i] = _mainConfig.sourcePath != null
						? UtilityHelper.RestoreCustomVariables( filePath )
						: filePath;
				}

				if (_mainConfig.sourcePath is null)


					await Logger.LogWarningAsync( "Not using custom variables <<kotorDirectory>> and <<modDirectory>> due to directories not being set." ).ConfigureAwait( false );

				instruction.Source = files;

				if (sourceTextBox != null)
				{
					string convertedItems = new ListToStringConverter().Convert(
						files,
						typeof( string ),
						parameter: null,
						CultureInfo.CurrentCulture
					) as string;

					sourceTextBox.Text = convertedItems;
				}
			}
			catch (Exception ex)
			{
				await Logger.LogExceptionAsync( ex, "Error browsing source files" ).ConfigureAwait( false );
			}
		}

		public async Task BrowseSourceFoldersAsync( Instruction instruction, TextBox sourceTextBox )
		{
			try
			{
				if (instruction is null)
					throw new ArgumentNullException( nameof( instruction ) );

				var startFolder = _mainConfig.sourcePath != null
					? await _dialogService.GetStorageFolderFromPathAsync( _mainConfig.sourcePath.FullName )
.ConfigureAwait( false ) : null;

				string[] folderPaths = await _dialogService.ShowFileDialogAsync(
					isFolderDialog: true,
					allowMultiple: true,
					startFolder: startFolder,
					windowName: "Select the folder to perform this instruction on"




				).ConfigureAwait( false );

				if (folderPaths is null || folderPaths.Length == 0)
				{
					await Logger.LogVerboseAsync( "User did not select any folders." ).ConfigureAwait( false );
					return;
				}

				var modifiedFolders = folderPaths.SelectMany(
					thisFolder => new DirectoryInfo( thisFolder )
						.EnumerateDirectories( searchPattern: "*", SearchOption.AllDirectories )
						.Select( folder => folder.FullName + Path.DirectorySeparatorChar + "*.*" )
				).ToList();

				instruction.Source = modifiedFolders;

				if (sourceTextBox != null)
				{
					string convertedItems = new ListToStringConverter().Convert(
						modifiedFolders,
						typeof( string ),
						parameter: null,
						CultureInfo.CurrentCulture
					) as string;

					sourceTextBox.Text = convertedItems;
				}
			}
			catch (Exception ex)
			{
				await Logger.LogExceptionAsync( ex, "Error browsing source folders" ).ConfigureAwait( false );
			}
		}

		public async Task BrowseDestinationAsync( Instruction instruction, TextBox destinationTextBox )
		{
			try
			{
				if (instruction is null)
					throw new ArgumentNullException( nameof( instruction ) );

				var startFolder = _mainConfig.destinationPath != null
					? await _dialogService.GetStorageFolderFromPathAsync( _mainConfig.destinationPath.FullName )
.ConfigureAwait( false ) : null;

				string[] result = await _dialogService.ShowFileDialogAsync(
					isFolderDialog: true,
					allowMultiple: false,
					startFolder: startFolder,
					windowName: "Select destination folder"


				).ConfigureAwait( false );

				if (result is null || result.Length <= 0)
					return;

				string folderPath = result[0];
				if (string.IsNullOrEmpty( folderPath ))


				{
					await Logger.LogVerboseAsync( $"No folder chosen, will continue using '{instruction.Destination}'" ).ConfigureAwait( false );
					return;
				}

				if (_mainConfig.sourcePath is null)


				{
					await Logger.LogAsync( "Directories not set, setting raw folder path without custom variable <<kotorDirectory>>" ).ConfigureAwait( false );
					instruction.Destination = folderPath;
				}
				else
				{
					instruction.Destination = UtilityHelper.RestoreCustomVariables( folderPath );
				}

				if (destinationTextBox != null)
					destinationTextBox.Text = instruction.Destination;
			}
			catch (Exception ex)
			{
				await Logger.LogExceptionAsync( ex, "Error browsing destination" ).ConfigureAwait( false );
			}
		}
	}
}