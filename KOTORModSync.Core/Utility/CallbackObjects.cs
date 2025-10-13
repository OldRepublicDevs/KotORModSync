// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Utility
{
	public static class CallbackObjects
	{
		public static IConfirmationDialogCallback ConfirmCallback { get; private set; }
		public static IOptionsDialogCallback OptionsCallback { get; private set; }
		public static IInformationDialogCallback InformationCallback { get; private set; }

		public static void SetCallbackObjects(
			[NotNull] IConfirmationDialogCallback confirmDialog,
			[NotNull] IOptionsDialogCallback optionsDialog,
			[NotNull] IInformationDialogCallback informationDialog
		)
		{
			ConfirmCallback = confirmDialog ?? throw new ArgumentNullException(nameof(confirmDialog));
			OptionsCallback = optionsDialog ?? throw new ArgumentNullException(nameof(optionsDialog));
			InformationCallback = informationDialog ?? throw new ArgumentNullException(nameof(informationDialog));
		}

		public interface IConfirmationDialogCallback
		{
			Task<bool?> ShowConfirmationDialog(string message);
		}

		public interface IOptionsDialogCallback
		{

			Task<string> ShowOptionsDialog(List<string> options);
		}

		public interface IInformationDialogCallback
		{

			Task ShowInformationDialog(string message);
		}
	}
}
