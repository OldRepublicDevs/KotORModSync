// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using KOTORModSync.Core;
using KOTORModSync.Dialogs;
using JetBrains.Annotations;

namespace KOTORModSync.Services
{

	public class DialogService
	{
		private readonly Window _parentWindow;
		private static readonly string[] tomlFileExtensions = new[] { "*.toml", "*.tml" };

		private static readonly string[] yamlFileExtensions = new[] { "*.yaml", "*.yml" };

		private static readonly string[] jsonFileExtensions = new[] { "*.json" };

		private static readonly string[] xmlFileExtensions = new[] { "*.xml" };

		private static readonly string[] mdFileExtensions = new[] { "*.md", "*.markdown", "*.mdown", "*.mkdn", "*.mkd", "*.mdtxt", "*.mdtext", "*.text" };

		public DialogService(Window parentWindow)
		{
			_parentWindow = parentWindow
							?? throw new ArgumentNullException(nameof(parentWindow));
		}

		public async Task<string[]> ShowFileDialogAsync(
			bool isFolderDialog,
			bool allowMultiple = false,
			IStorageFolder startFolder = null,
			string windowName = null)
		{
			try
			{
				if ( !(_parentWindow?.StorageProvider != null) )
				{
					await Logger.LogErrorAsync($"Could not open {(isFolderDialog ? "folder" : "file")} dialog - storage provider not available");
					return null;
				}

				if ( isFolderDialog )
				{
					IReadOnlyList<IStorageFolder> result = await _parentWindow.StorageProvider.OpenFolderPickerAsync(
						new FolderPickerOpenOptions
						{
							Title = windowName ?? "Choose the folder",
							AllowMultiple = allowMultiple,
							SuggestedStartLocation = startFolder,
						}
					);
					return result.Select(s => s.TryGetLocalPath()).ToArray();
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
					);
					string[] files = result.Select(s => s.TryGetLocalPath()).ToArray();
					if ( files.Length > 0 )
						return files;
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
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
			[CanBeNull] IStorageFolder startFolder = null)
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
				);

				return file?.TryGetLocalPath();
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
				await InformationDialog.ShowInformationDialogAsync(_parentWindow, $"Error opening save file dialog: {ex.Message}.");
				return null;
			}
		}

		public async Task<IStorageFolder> GetStorageFolderFromPathAsync(string path)
		{
			try
			{
				if ( string.IsNullOrEmpty(path) )
					return null;

				return await _parentWindow.StorageProvider.TryGetFolderFromPathAsync(path);
			}
			catch ( Exception ex )
			{
				Logger.LogVerbose($"Error getting storage folder from path: {ex.Message}");
				return null;
			}
		}
	}
}

