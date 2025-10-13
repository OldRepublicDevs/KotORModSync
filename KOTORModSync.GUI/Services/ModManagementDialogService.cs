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
using KOTORModSync.Core.Services;
using KOTORModSync.Dialogs;

namespace KOTORModSync
{
	public class ModManagementDialogService : IModManagementDialogService
	{
		private readonly Window _parentWindow;
		private readonly ModManagementService _modManagementService;
		private readonly Func<List<ModComponent>> _getComponents;
		private readonly Action<List<ModComponent>> _updateComponents;

		public ModManagementDialogService(
			Window parentWindow,
			ModManagementService modManagementService,
			Func<List<ModComponent>> getComponents,
			Action<List<ModComponent>> updateComponents)
		{
			_parentWindow = parentWindow ?? throw new ArgumentNullException(nameof(parentWindow));
			_modManagementService = modManagementService ?? throw new ArgumentNullException(nameof(modManagementService));
			_getComponents = getComponents ?? throw new ArgumentNullException(nameof(getComponents));
			_updateComponents = updateComponents ?? throw new ArgumentNullException(nameof(updateComponents));
		}

		public async Task<string[]> ShowFileDialog(bool isFolderDialog, string windowName, bool allowMultiple = false)
		{
			try
			{
				if ( isFolderDialog )
				{
					IReadOnlyList<IStorageFolder> folders = await _parentWindow.StorageProvider.OpenFolderPickerAsync(
						new FolderPickerOpenOptions
						{
							Title = windowName ?? "Choose folder",
							AllowMultiple = allowMultiple
						}
					);
					return folders.Select(f => f.TryGetLocalPath()).ToArray();
				}
				else
				{
					IReadOnlyList<IStorageFile> files = await _parentWindow.StorageProvider.OpenFilePickerAsync(
						new FilePickerOpenOptions
						{
							Title = windowName ?? "Choose file(s)",
							AllowMultiple = allowMultiple,
							FileTypeFilter = new[] { FilePickerFileTypes.All }
						}
					);
					return files.Select(f => f.TryGetLocalPath()).ToArray();
				}
			}
			catch ( Exception ex )
			{
				await ShowInformationDialog($"Error opening file dialog: {ex.Message}");
				return null;
			}
		}

		public async Task<string> ShowSaveFileDialog(string suggestedFileName)
		{
			try
			{
				IStorageFile file = await _parentWindow.StorageProvider.SaveFilePickerAsync(
					new FilePickerSaveOptions
					{
						Title = "Save file",
						DefaultExtension = "toml",
						SuggestedFileName = suggestedFileName ?? "file.toml",
						FileTypeChoices = new List<FilePickerFileType> { FilePickerFileTypes.All }
					}
				);

				return file?.TryGetLocalPath();
			}
			catch ( Exception ex )
			{
				await ShowInformationDialog($"Error opening save file dialog: {ex.Message}");
				return null;
			}
		}

		public async Task ShowInformationDialog(string message) => await InformationDialog.ShowInformationDialogAsync(_parentWindow, message);

		public async Task<bool?> ShowConfirmationDialog(string message, string yesButtonText = "Yes", string noButtonText = "No")
			=> await ConfirmationDialog.ShowConfirmationDialogAsync(_parentWindow, message);

		public IReadOnlyList<ModComponent> GetComponents()
			=> _getComponents()?.AsReadOnly() ?? new List<ModComponent>().AsReadOnly();

		public void UpdateComponents(List<ModComponent> components)
			=> _updateComponents(components);

		public void RefreshStatistics()
		{

			if ( _parentWindow is ModManagementDialog dialog && dialog.DataContext is ModManagementService.ModStatistics )
				dialog.DataContext = _modManagementService.GetModStatistics();
		}
	}
}
