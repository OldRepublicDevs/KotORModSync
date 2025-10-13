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

namespace KOTORModSync.Services
{
	/// <summary>
	/// Service responsible for showing file dialogs and managing dialog interactions
	/// </summary>
	public class DialogService
	{
		private readonly Window _parentWindow;

		public DialogService(Window parentWindow)
		{
			_parentWindow = parentWindow
			                ?? throw new ArgumentNullException(nameof(parentWindow));
		}

		/// <summary>
		/// Shows a file or folder picker dialog
		/// </summary>
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

		/// <summary>
		/// Shows a save file dialog
		/// </summary>
		public async Task<string> ShowSaveFileDialogAsync(string suggestedFileName = null, string defaultExtension = "toml")
		{
			try
			{
				IStorageFile file = await _parentWindow.StorageProvider.SaveFilePickerAsync(
					new FilePickerSaveOptions
					{
						DefaultExtension = defaultExtension,
						FileTypeChoices = new List<FilePickerFileType> { FilePickerFileTypes.All },
						ShowOverwritePrompt = true,
						SuggestedFileName = suggestedFileName ?? $"my_file.{defaultExtension}",
					}
				);

				string filePath = file?.TryGetLocalPath();
				if ( !string.IsNullOrEmpty(filePath) )
				{
					await Logger.LogAsync($"Selected file: {filePath}");
					return filePath;
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}

			return null;
		}

		/// <summary>
		/// Gets a storage folder from a path
		/// </summary>
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

