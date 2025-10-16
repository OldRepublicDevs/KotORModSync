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
using KOTORModSync.Core.Services.ImmutableCheckpoint;

namespace KOTORModSync.Dialogs
{
	public partial class CheckpointManagementDialog : Window
	{
		private readonly ObservableCollection<SessionViewModel> _sessions = new ObservableCollection<SessionViewModel>();
		private readonly ObservableCollection<CheckpointViewModel> _checkpoints = new ObservableCollection<CheckpointViewModel>();
		private readonly string _destinationPath;
		private SessionViewModel _selectedSession;
		private CheckpointService _checkpointService;

		public CheckpointManagementDialog()
		{
			InitializeComponent();
		}

		public CheckpointManagementDialog(string destinationPath) : this()
		{
			_destinationPath = destinationPath;
			_checkpointService = new CheckpointService(destinationPath);

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
				var sessions = await _checkpointService.ListSessionsAsync();

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

		private void UpdateStorageInfo(List<CheckpointSession> sessions)
		{
			var storageText = this.FindControl<TextBlock>("StorageInfoText");
			if ( storageText == null )
				return;

			try
			{
				string checkpointBaseDir = Path.Combine(_destinationPath, ".kotor_modsync", "checkpoints");

				if ( !Directory.Exists(checkpointBaseDir) )
				{
					storageText.Text = "Total storage: 0 B";
					return;
				}

				long totalSize = 0;

				string objectsDir = Path.Combine(checkpointBaseDir, "objects");
				if ( Directory.Exists(objectsDir) )
				{
					var objectFiles = Directory.GetFiles(objectsDir, "*", SearchOption.AllDirectories);
					totalSize += objectFiles.Sum(f => new FileInfo(f).Length);
				}

				string sessionsDir = Path.Combine(checkpointBaseDir, "sessions");
				if ( Directory.Exists(sessionsDir) )
				{
					var sessionFiles = Directory.GetFiles(sessionsDir, "*", SearchOption.AllDirectories);
					totalSize += sessionFiles.Sum(f => new FileInfo(f).Length);
				}

				string sizeText = FormatBytes(totalSize);
				int sessionCount = sessions.Count;
				int totalCheckpoints = sessions.Sum(s => s.CheckpointIds.Count);

				storageText.Text = $"💾 {sessionCount} session(s), {totalCheckpoints} checkpoint(s), {sizeText} total storage";
			}
			catch ( Exception ex )
			{
				Logger.LogError($"Failed to calculate storage: {ex.Message}");
				storageText.Text = "Storage info unavailable";
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
				_ = LoadCheckpointsAsync(session);
			}
		}

		private async Task LoadCheckpointsAsync(SessionViewModel session)
		{
			try
			{
				var titleText = this.FindControl<TextBlock>("SelectedSessionTitle");
				var infoText = this.FindControl<TextBlock>("SelectedSessionInfo");

				if ( titleText != null )
					titleText.Text = session.SessionName;

				var checkpoints = await _checkpointService.ListCheckpointsAsync(session.Session.Id);

				await Dispatcher.UIThread.InvokeAsync(() =>
				{
					if ( infoText != null )
					{
						string status = session.Session.IsComplete ? "✅ Completed" : "⏳ In Progress";
						string storageInfo = $"Storage: {FormatBytes(checkpoints.Sum(c => c.DeltaSize))} deltas";
						infoText.Text = $"{status} | Started: {session.Session.StartTime:g} | {storageInfo}";
						if ( session.Session.EndTime.HasValue )
							infoText.Text += $" | Ended: {session.Session.EndTime.Value:g}";
					}

					_checkpoints.Clear();

					foreach ( var checkpoint in checkpoints.OrderBy(c => c.Sequence) )
					{
						_checkpoints.Add(new CheckpointViewModel(checkpoint, session.Session));
					}
				});
			}
			catch ( Exception ex )
			{
				await Logger.LogErrorAsync($"Failed to load checkpoints: {ex.Message}");
			}
		}

		private async void RollbackButton_Click(object sender, RoutedEventArgs e)
		{
			if ( !(sender is Button button) || !(button.Tag is CheckpointViewModel checkpointVm) )
				return;

			int totalCheckpoints = _checkpoints.Count;
			int checkpointsToUndo = totalCheckpoints - checkpointVm.Checkpoint.Sequence;

			string confirmMessage = $"Are you sure you want to restore to checkpoint #{checkpointVm.Checkpoint.Sequence} ('{checkpointVm.ComponentName}')?";
			confirmMessage += $"\n\nThis will restore your game directory to the state after this mod was installed.";

			if ( checkpointsToUndo > 0 )
			{
				confirmMessage += $"\n\n⚠️ This will undo {checkpointsToUndo} subsequent mod installation(s).";
			}

			confirmMessage += $"\n\nChanges at this checkpoint:";
			confirmMessage += $"\n  • {checkpointVm.Checkpoint.Added.Count} file(s) were added";
			confirmMessage += $"\n  • {checkpointVm.Checkpoint.Modified.Count} file(s) were modified";
			confirmMessage += $"\n  • {checkpointVm.Checkpoint.Deleted.Count} file(s) were deleted";

			if ( checkpointVm.IsAnchor )
			{
				confirmMessage += $"\n\n📍 This is an anchor checkpoint (optimized for fast restoration).";
			}

			var confirmDialog = new ConfirmDialog(
				"Confirm Checkpoint Restoration",
				confirmMessage,
				"Restore",
				"Cancel"
			);

			var result = await confirmDialog.ShowDialog<bool>(this);
			if ( !result )
				return;

			await PerformRollbackAsync(checkpointVm);
		}

		private async Task PerformRollbackAsync(CheckpointViewModel checkpointVm)
		{
			ProgressDialog progressDialog = null;

			try
			{
				progressDialog = new ProgressDialog("Restoring Checkpoint", "Preparing restoration...");
				progressDialog.Show(this);

				var progress = new Progress<InstallProgress>(p =>
				{
					Dispatcher.UIThread.Post(() =>
					{
						if ( progressDialog != null )
						{
							progressDialog.UpdateProgress(p.Message, p.Current, p.Total);
						}
					});
				});

				var coordinatorService = new InstallationCoordinatorService();

				await Task.Run(async () =>
				{
					await InstallationCoordinatorService.RestoreToCheckpointAsync(
						checkpointVm.Session.Id,
						checkpointVm.Checkpoint.Id,
						_destinationPath,
						progress,
						CancellationToken.None
					);
				});

				progressDialog?.Close();
				progressDialog = null;

				await ShowSuccessDialog(
					$"Checkpoint restored successfully!\n\n" +
					$"Your game directory has been restored to the state after '{checkpointVm.ComponentName}' was installed."
				);

				await LoadSessionsAsync();
			}
			catch ( Exception ex )
			{
				progressDialog?.Close();
				await Logger.LogErrorAsync($"Checkpoint restoration failed: {ex.Message}");
				await ShowErrorDialog($"Checkpoint restoration failed:\n\n{ex.Message}");
			}
		}

		private async void CleanupButton_Click(object sender, RoutedEventArgs e)
		{
			var confirmDialog = new ConfirmDialog(
				"Clean Up Checkpoint Storage",
				"This will delete checkpoint data for all completed installation sessions.\n\n" +
				"Completed sessions will be removed, but you'll keep any in-progress sessions.\n\n" +
				"After cleanup, you will no longer be able to rollback completed installations.\n\n" +
				"The system will also garbage collect orphaned files to free up disk space.\n\n" +
				"Continue?",
				"Clean Up",
				"Cancel"
			);

			var result = await confirmDialog.ShowDialog<bool>(this);
			if ( !result )
				return;

			try
			{
				var sessions = await _checkpointService.ListSessionsAsync();
				var completedSessions = sessions.Where(s => s.IsComplete).ToList();

				if ( completedSessions.Count == 0 )
				{
					await ShowSuccessDialog("No completed sessions to clean up.");
					return;
				}

				int cleanedCount = 0;
				long freedSpace = 0;

				foreach ( var session in completedSessions )
				{
					string sessionPath = Path.Combine(_destinationPath, ".kotor_modsync", "checkpoints", "sessions", session.Id);
					if ( Directory.Exists(sessionPath) )
					{
						var files = Directory.GetFiles(sessionPath, "*", SearchOption.AllDirectories);
						freedSpace += files.Sum(f => new FileInfo(f).Length);
					}

					await _checkpointService.DeleteSessionAsync(session.Id);
					cleanedCount++;
				}

				int orphanedObjects = await _checkpointService.GarbageCollectAsync();

				await ShowSuccessDialog(
					$"Cleanup complete!\n\n" +
					$"• Deleted {cleanedCount} completed session(s)\n" +
					$"• Removed {orphanedObjects} orphaned file(s)\n" +
					$"• Freed approximately {FormatBytes(freedSpace)} of disk space"
				);

				await LoadSessionsAsync();
			}
			catch ( Exception ex )
			{
				await Logger.LogErrorAsync($"Cleanup failed: {ex.Message}");
				await ShowErrorDialog($"Cleanup failed:\n\n{ex.Message}");
			}
		}

		private async void RefreshButton_Click(object sender, RoutedEventArgs e)
		{
			await LoadSessionsAsync();

			if ( _selectedSession != null )
			{
				await LoadCheckpointsAsync(_selectedSession);
			}
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
		public CheckpointSession Session { get; }

		public SessionViewModel(CheckpointSession session)
		{
			Session = session;
		}

		public string SessionName => Session.Name;

		public string StartTime => Session.StartTime.ToString("g");

		public string CheckpointCountText
		{
			get
			{
				int count = Session.CheckpointIds.Count;
				string status = Session.IsComplete ? "✅" : "⏳";
				return $"{status} {count} checkpoint(s)";
			}
		}
	}

	public class CheckpointViewModel
	{
		public Checkpoint Checkpoint { get; }
		public CheckpointSession Session { get; }

		public CheckpointViewModel(Checkpoint checkpoint, CheckpointSession session)
		{
			Checkpoint = checkpoint;
			Session = session;
		}

		public string ComponentName => Checkpoint.ComponentName;

		public string Timestamp => Checkpoint.Timestamp.ToString("g");

		public bool IsAnchor => Checkpoint.IsAnchor;

		public string ChangeSummary
		{
			get
			{
				var parts = new List<string>();

				if ( Checkpoint.Added.Count > 0 )
					parts.Add($"✚ {Checkpoint.Added.Count} added");

				if ( Checkpoint.Modified.Count > 0 )
					parts.Add($"✎ {Checkpoint.Modified.Count} modified");

				if ( Checkpoint.Deleted.Count > 0 )
					parts.Add($"✖ {Checkpoint.Deleted.Count} deleted");

				string result = parts.Any() ? string.Join(" | ", parts) : "No changes";

				if ( Checkpoint.IsAnchor )
					result += " | 📍 Anchor";

				if ( Checkpoint.DeltaSize > 0 )
				{
					result += $" | {FormatBytes(Checkpoint.DeltaSize)} delta";
				}

				return result;
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
	}

	#endregion
}
