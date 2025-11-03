// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;

using Avalonia.Platform.Storage;

using JetBrains.Annotations;

using KOTORModSync.Core;

namespace KOTORModSync
{
    public interface IModManagementDialogService
    {

        Task<string[]> ShowFileDialog(bool isFolderDialog, string windowName, bool allowMultiple = false);

        Task<string> ShowSaveFileDialogAsync(
            [CanBeNull] string suggestedFileName = null,
            [CanBeNull] string defaultExtension = "toml",
            [CanBeNull][ItemNotNull] List<FilePickerFileType> fileTypeChoices = null,
            [CanBeNull] string windowName = "Save file as...",
            [CanBeNull] IStorageFolder startFolder = null);

        Task ShowInformationDialog(string message);

        Task<bool?> ShowConfirmationDialog(string message, string yesButtonText = "Yes", string noButtonText = "No");

        IReadOnlyList<ModComponent> GetComponents();

        void UpdateComponents(List<ModComponent> components);

        void RefreshStatistics();
    }
}
