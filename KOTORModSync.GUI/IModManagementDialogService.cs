// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using KOTORModSync.Core;

namespace KOTORModSync
{
	public interface IModManagementDialogService
	{
		/// <summary>
		/// Shows a file dialog for opening files
		/// </summary>
		Task<string[]> ShowFileDialog(bool isFolderDialog, string windowName, bool allowMultiple = false);

		/// <summary>
		/// Shows a file dialog for saving files
		/// </summary>
		Task<string> ShowSaveFileDialog(string suggestedFileName);

		/// <summary>
		/// Shows an information dialog
		/// </summary>
		Task ShowInformationDialog(string message);

		/// <summary>
		/// Shows a confirmation dialog
		/// </summary>
		Task<bool?> ShowConfirmationDialog(string message, string yesButtonText = "Yes", string noButtonText = "No");

		/// <summary>
		/// Gets the current list of components
		/// </summary>
		IReadOnlyList<ModComponent> GetComponents();

		/// <summary>
		/// Updates the components list
		/// </summary>
		void UpdateComponents(List<ModComponent> components);

		/// <summary>
		/// Refreshes the mod statistics display
		/// </summary>
		void RefreshStatistics();
	}
}
