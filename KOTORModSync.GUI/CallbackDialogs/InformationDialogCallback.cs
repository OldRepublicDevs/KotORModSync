// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Threading.Tasks;
using Avalonia.Controls;
using JetBrains.Annotations;
using KOTORModSync.Core.Utility;
using KOTORModSync.Dialogs;

namespace KOTORModSync.CallbackDialogs
{
	internal sealed class InformationDialogCallback : CallbackObjects.IInformationDialogCallback
	{
		private readonly Window _topLevelWindow;
		public InformationDialogCallback([CanBeNull] Window topLevelWindow) => _topLevelWindow = topLevelWindow;

		public async Task ShowInformationDialog([CanBeNull] string message) =>
			await InformationDialog.ShowInformationDialogAsync(_topLevelWindow, message);
	}
}
