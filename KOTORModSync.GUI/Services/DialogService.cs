// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Platform.Storage;

using JetBrains.Annotations;

using KOTORModSync.Core;
using KOTORModSync.Dialogs;

namespace KOTORModSync.Services
{

	public class DialogService
	{
		private readonly Window _parentWindow;

		public DialogService( Window parentWindow )
		{
			_parentWindow = parentWindow
							?? throw new ArgumentNullException( nameof( parentWindow ) );
		}

		public async Task<string[]> ShowFileDialogAsync(
			bool isFolderDialog,
			bool allowMultiple = false,
			IStorageFolder startFolder = null,
			string windowName = null )
		{
			try
			{
				if (_parentWindow?.StorageProvider == null)


				{
					await Logger.LogErrorAsync( $"Could not open {(isFolderDialog ? "folder" : "file")} dialog - storage provider not available" ).ConfigureAwait( false );
					return null;
				}

				if (isFolderDialog)
				{
					IReadOnlyList<IStorageFolder> result = await _parentWindow.StorageProvider.OpenFolderPickerAsync(
						new FolderPickerOpenOptions
						{
							Title = windowName ?? "Choose the folder",
							AllowMultiple = allowMultiple,
							SuggestedStartLocation = startFolder,


						}
					).ConfigureAwait( false );
					return result.Select( s => s.TryGetLocalPath() ).ToArray();
				}
				else
				{
					IReadOnlyList<IStorageFile> result = await _parentWindow.StorageProvider.OpenFilePickerAsync(
						new FilePickerOpenOptions
						{
							Title = windowName ?? "Choose the file(s)",
							AllowMultiple = allowMultiple,
							FileTypeFilter = new[] { FilePickerFileTypes.All, FilePickerFileTypes.TextPlain },


						}
					).ConfigureAwait( false );
					string[] files = result.Select( s => s.TryGetLocalPath() ).ToArray();
					if (files.Length > 0)
						return files;
				}
			}
			catch (Exception ex)


			{
				await Logger.LogExceptionAsync( ex ).ConfigureAwait( false );
			}

			return null;
		}

		[NotNull]
		[ItemCanBeNull]
		public async Task<string> ShowSaveFileDialogAsync(
			[CanBeNull] string suggestedFileName = null,
			[CanBeNull] string defaultExtension = "toml",
			[CanBeNull][ItemNotNull] List<FilePickerFileType> fileTypeChoices = null,
			[CanBeNull] string windowName = "Save file as...",
			[CanBeNull] IStorageFolder startFolder = null )
		{
			try
			{
				IStorageFile file = await _parentWindow.StorageProvider.SaveFilePickerAsync(
					new FilePickerSaveOptions
					{
						Title = windowName,
						DefaultExtension = defaultExtension,
						SuggestedFileName = suggestedFileName,
						FileTypeChoices = fileTypeChoices ?? new List<FilePickerFileType> { FilePickerFileTypes.All }


					}
				).ConfigureAwait( false );

				return file?.TryGetLocalPath();
			}
			catch (Exception ex)


			{
				await Logger.LogExceptionAsync( ex ).ConfigureAwait( false );
				await InformationDialog.ShowInformationDialogAsync( _parentWindow, $"Error opening save file dialog: {ex.Message}." ).ConfigureAwait( false );
				return null;
			}
		}

		public async Task<IStorageFolder> GetStorageFolderFromPathAsync( string path )
		{
			try
			{
				if (string.IsNullOrEmpty( path ))
					return null;

				return await _parentWindow.StorageProvider.TryGetFolderFromPathAsync( path ).ConfigureAwait( false );
			}
			catch (Exception ex)
			{
				Logger.LogVerbose( $"Error getting storage folder from path: {ex.Message}" );
				return null;
			}
		}
	}
}