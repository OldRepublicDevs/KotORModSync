// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.


using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using KOTORModSync.Core;
using KOTORModSync.Core.Services;
using Logger = KOTORModSync.Core.Logger;

namespace KOTORModSync.Dialogs
{
	public partial class InstallationErrorDialog : Window
	{
		public ErrorAction SelectedAction { get; private set; } = ErrorAction.Rollback;

		public InstallationErrorDialog()
		{
			InitializeComponent();
		}

		public InstallationErrorDialog(InstallationErrorEventArgs errorArgs) : this()
		{
			LoadErrorData(errorArgs);
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}

		private void LoadErrorData(InstallationErrorEventArgs errorArgs)
		{
			var componentNameText = this.FindControl<TextBlock>("ComponentNameText");
			var errorDetailsText = this.FindControl<TextBox>("ErrorDetailsText");
			var checkpointInfoText = this.FindControl<TextBlock>("CheckpointInfoText");
			var checkpointInfoPanel = this.FindControl<Border>("CheckpointInfoPanel");
			var errorTitleText = this.FindControl<TextBlock>("ErrorTitleText");
			var rollbackRadio = this.FindControl<RadioButton>("RollbackRadio");

			if ( componentNameText != null )
				componentNameText.Text = errorArgs.Component?.Name ?? "Unknown Component";

			if ( errorDetailsText != null )
			{
				string errorMessage = errorArgs.Exception?.Message
					?? $"Installation failed with error code: {errorArgs.ErrorCode}";

				if ( errorArgs.Exception != null && !string.IsNullOrEmpty(errorArgs.Exception.StackTrace) )
				{
					errorMessage += $"\n\nStack Trace:\n{errorArgs.Exception.StackTrace}";
				}

				errorDetailsText.Text = errorMessage;
			}

			if ( checkpointInfoPanel != null )
				checkpointInfoPanel.IsVisible = errorArgs.CanRollback;

			if ( checkpointInfoText != null && !string.IsNullOrEmpty(errorArgs.SessionId) )
			{
				checkpointInfoText.Text = $"Checkpoints are available for this installation session. " +
					$"Rolling back will restore your game to the state before this installation began.";
			}

			if ( errorTitleText != null )
			{
				errorTitleText.Text = $"Failed while installing: {errorArgs.Component?.Name ?? "Unknown"}";
			}

			if ( rollbackRadio != null )
				rollbackRadio.IsEnabled = errorArgs.CanRollback;
		}

		private void ConfirmButton_Click(object sender, RoutedEventArgs e)
		{
			var rollbackRadio = this.FindControl<RadioButton>("RollbackRadio");
			var continueRadio = this.FindControl<RadioButton>("ContinueRadio");
			var abortRadio = this.FindControl<RadioButton>("AbortRadio");

			if ( rollbackRadio?.IsChecked == true )
				SelectedAction = ErrorAction.Rollback;
			else if ( continueRadio?.IsChecked == true )
				SelectedAction = ErrorAction.Continue;
			else if ( abortRadio?.IsChecked == true )
				SelectedAction = ErrorAction.Abort;

			Close(SelectedAction);
		}

		private void CancelButton_Click(object sender, RoutedEventArgs e)
		{
			SelectedAction = ErrorAction.Abort;
			Close(SelectedAction);
		}

		private void ViewLogButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				if ( !string.IsNullOrEmpty(Logger.LogFileName) && File.Exists(Logger.LogFileName) )
				{
					var process = new System.Diagnostics.Process();
					process.StartInfo = new System.Diagnostics.ProcessStartInfo
					{
						FileName = Logger.LogFileName,
						UseShellExecute = true
					};
					process.Start();
				}
			}
			catch ( Exception ex )
			{
				Logger.LogError($"Failed to open output folder: {ex.Message}");
			}
		}
	}

	public enum ErrorAction
	{
		Rollback,
		Continue,
		Abort
	}
}

