// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace KOTORModSync.Dialogs
{
	/// <summary>
	/// Interface that all wizard pages must implement
	/// </summary>
	public interface IWizardPage
	{
		/// <summary>
		/// Page title shown in the header
		/// </summary>
		string Title { get; }

		/// <summary>
		/// Page subtitle/description shown in the header
		/// </summary>
		string Subtitle { get; }

		/// <summary>
		/// The actual content control for this page
		/// </summary>
		Control Content { get; }

		/// <summary>
		/// Whether the user can navigate back from this page
		/// </summary>
		bool CanNavigateBack { get; }

		/// <summary>
		/// Whether the user can navigate forward from this page
		/// </summary>
		bool CanNavigateForward { get; }

		/// <summary>
		/// Whether the user can cancel from this page
		/// </summary>
		bool CanCancel { get; }

		/// <summary>
		/// Called when navigating TO this page
		/// </summary>
		Task OnNavigatedToAsync(CancellationToken cancellationToken);

		/// <summary>
		/// Called when navigating FROM this page
		/// </summary>
		Task OnNavigatingFromAsync(CancellationToken cancellationToken);

		/// <summary>
		/// Validates the page before allowing navigation
		/// Returns (isValid, errorMessage)
		/// </summary>
		Task<(bool isValid, string errorMessage)> ValidateAsync(CancellationToken cancellationToken);
	}
}

