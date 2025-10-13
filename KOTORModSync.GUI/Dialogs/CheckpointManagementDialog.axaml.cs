// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using KOTORModSync.Core;
using KOTORModSync.Core.Services;

namespace KOTORModSync.Dialogs
{
	public partial class CheckpointManagementDialog : Window
	{
		private readonly ObservableCollection<SessionViewModel> _sessions = new ObservableCollection<SessionViewModel>();
		private readonly ObservableCollection<CheckpointViewModel> _checkpoints = new ObservableCollection<CheckpointViewModel>();
		private readonly string _destinationPath;
		private SessionViewModel _selectedSession;

		public CheckpointManagementDialog()
		{
			InitializeComponent();
		}

		public CheckpointManagementDialog(string destinationPath) : this()
		{
			_destinationPath = destinationPath;

			var sessionsControl = this.FindControl<ItemsControl>("SessionsListControl");
			if ( sessionsControl != null )
				sessionsControl.ItemsSource = _sessions;

			var checkpointsControl = this.FindControl<ItemsControl>("CheckpointsListControl");
			if ( checkpointsControl != null )
				checkpointsControl.ItemsSource = _checkpoints;

			_ = LoadSessionsAsync();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}

		private async Task LoadSessionsAsync()
		{
			try
			{
				var checkpointService = new CheckpointService(_destinationPath);
				var sessions = await checkpointService.ListSessionsAsync();

				await Dispatcher.UIThread.InvokeAsync(() =>
				{
					_sessions.Clear();
					foreach ( var session in sessions )
					{
						_sessions.Add(new SessionViewModel(session));
					}

					UpdateStorageInfo(sessions);
				});
			}
			catch ( Exception ex )
			{
				await Logger.LogErrorAsync($"Failed to load sessions: {ex.Message}");
			}
		}

		private void UpdateStorageInfo(List<InstallationSession> sessions)
		{
			var storageText = this.FindControl<TextBlock>("StorageInfoText");
			if ( storageText == null )
				return;

			try
			{
				long totalSize = 0;
				foreach ( var session in sessions )
				{
					var sessionPath = session.CheckpointRootPath;
					if ( Directory.Exists(sessionPath) )
					{
						var files = Directory.GetFiles(sessionPath, "*", SearchOption.AllDirectories);
						totalSize += files.Sum(f => new FileInfo(f).Length);
					}
				}

				string sizeText = FormatBytes(totalSize);
				storageText.Text = $"Total checkpoint storage: {sizeText}";
			}
			catch
			{
				storageText.Text = "";
			}
		}

		private static string FormatBytes(long bytes)
		{
			string[] sizes = { "B", "KB", "MB", "GB", "TB" };
			double len = bytes;
			int order = 0;
			while ( len >= 1024 && order < sizes.Length - 1 )
			{
				order++;
				len = len / 1024;
			}
			return $"{len:0.##} {sizes[order]}";
		}

		private void SessionItem_PointerPressed(object sender, PointerPressedEventArgs e)
		{
			if ( sender is Border border && border.DataContext is SessionViewModel session )
			{
				_selectedSession = session;
				LoadCheckpoints(session);
			}
		}

		private void LoadCheckpoints(SessionViewModel session)
		{
			var titleText = this.FindControl<TextBlock>("SelectedSessionTitle");
			var infoText = this.FindControl<TextBlock>("SelectedSessionInfo");

			if ( titleText != null )
				titleText.Text = session.SessionName;

			if ( infoText != null )
			{
				string status = session.Session.IsCompleted ? "Completed" : "In Progress";
				infoText.Text = $"Status: {status} | Started: {session.Session.StartTime:g}";
				if ( session.Session.EndTime.HasValue )
					infoText.Text += $" | Ended: {session.Session.EndTime.Value:g}";
			}

			_checkpoints.Clear();

			
			foreach ( var checkpoint in session.Session.Checkpoints.Skip(1) )
			{
				_checkpoints.Add(new CheckpointViewModel(checkpoint, session.Session));
			}
		}

		private async void RollbackButton_Click(object sender, RoutedEventArgs e)
		{
			if ( !(sender is Button button) || !(button.Tag is CheckpointViewModel checkpointVm) )
				return;

			
			var confirmDialog = new ConfirmDialog(
				"Confirm Rollback",
				$"Are you sure you want to rollback to '{checkpointVm.ComponentName}'?\n\n" +
				$"This will undo all mod installations after this checkpoint. This operation cannot be undone.\n\n" +
				$"Changes to be reverted:\n" +
				$"• {checkpointVm.Checkpoint.Changes.AddedFiles.Count} files will be removed\n" +
				$"• {checkpointVm.Checkpoint.Changes.ModifiedFiles.Count} files will be restored",
				"Rollback",
				"Cancel"
			);

			var result = await confirmDialog.ShowDialog<bool>(this);
			if ( !result )
				return;

			
			await PerformRollbackAsync(checkpointVm);
		}

		private async Task PerformRollbackAsync(CheckpointViewModel checkpointVm)
		{
			try
			{
				var progressDialog = new ProgressDialog("Rolling Back", "Preparing rollback...");
				progressDialog.Show(this);

				var progress = new Progress<InstallProgress>(p =>
				{
					Dispatcher.UIThread.Post(() =>
					{
						progressDialog.UpdateProgress(p.Message, p.Current, p.Total);
					});
				});

				var coordinatorService = new InstallationCoordinatorService();

				await Task.Run(async () =>
				{
					await coordinatorService.RollbackToCheckpointAsync(
						checkpointVm.Session.SessionId,
						checkpointVm.Checkpoint.CheckpointId,
						_destinationPath,
						progress,
						CancellationToken.None
					);
				});

				progressDialog.Close();

				await ShowSuccessDialog("Rollback completed successfully!");
				await LoadSessionsAsync();
			}
			catch ( Exception ex )
			{
				await Logger.LogErrorAsync($"Rollback failed: {ex.Message}");
				await ShowErrorDialog($"Rollback failed: {ex.Message}");
			}
		}

		private async void CleanupButton_Click(object sender, RoutedEventArgs e)
		{
			var confirmDialog = new ConfirmDialog(
				"Clean Up Sessions",
				"This will delete checkpoint data for all completed installation sessions.\n\n" +
				"You will no longer be able to rollback these installations.\n\n" +
				"Continue?",
				"Clean Up",
				"Cancel"
			);

			var result = await confirmDialog.ShowDialog<bool>(this);
			if ( !result )
				return;

			try
			{
				var checkpointService = new CheckpointService(_destinationPath);
				var sessions = await checkpointService.ListSessionsAsync();

				int cleanedCount = 0;
				foreach ( var session in sessions.Where(s => s.IsCompleted) )
				{
					await checkpointService.CleanupSessionAsync(session.SessionId);
					cleanedCount++;
				}

				await ShowSuccessDialog($"Cleaned up {cleanedCount} session(s)");
				await LoadSessionsAsync();
			}
			catch ( Exception ex )
			{
				await Logger.LogErrorAsync($"Cleanup failed: {ex.Message}");
				await ShowErrorDialog($"Cleanup failed: {ex.Message}");
			}
		}

		private async void RefreshButton_Click(object sender, RoutedEventArgs e)
		{
			await LoadSessionsAsync();
		}

		private void CloseButton_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		private async Task ShowSuccessDialog(string message)
		{
			var dialog = new MessageDialog("Success", message, "OK");
			await dialog.ShowDialog(this);
		}

		private async Task ShowErrorDialog(string message)
		{
			var dialog = new MessageDialog("Error", message, "OK");
			await dialog.ShowDialog(this);
		}
	}

	#region View Models

	public class SessionViewModel
	{
		public InstallationSession Session { get; }

		public SessionViewModel(InstallationSession session)
		{
			Session = session;
		}

		public string SessionName => Session.SessionName;
		public string CheckpointCountText => $"{Session.Checkpoints.Count - 1} checkpoint(s)"; 
	}

	public class CheckpointViewModel
	{
		public Checkpoint Checkpoint { get; }
		public InstallationSession Session { get; }

		public CheckpointViewModel(Checkpoint checkpoint, InstallationSession session)
		{
			Checkpoint = checkpoint;
			Session = session;
		}

		public string ComponentName => Checkpoint.ComponentName;
		public string ChangeSummary
		{
			get
			{
				var changes = Checkpoint.Changes;
				var parts = new List<string>();
				if ( changes.AddedFiles.Count > 0 )
					parts.Add($"{changes.AddedFiles.Count} added");
				if ( changes.ModifiedFiles.Count > 0 )
					parts.Add($"{changes.ModifiedFiles.Count} modified");
				if ( changes.DeletedFiles.Count > 0 )
					parts.Add($"{changes.DeletedFiles.Count} deleted");

				return parts.Any() ? string.Join(", ", parts) : "No changes";
			}
		}
	}

	#endregion
}

