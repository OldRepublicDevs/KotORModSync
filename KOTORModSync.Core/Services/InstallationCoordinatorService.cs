// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KOTORModSync.Core.Services.FileSystem;
using KOTORModSync.Core.Services.ImmutableCheckpoint;
using Logger = KOTORModSync.Core.Logger;

namespace KOTORModSync.Core.Services
{
	/// <summary>
	/// Coordinates the installation of multiple mod components with checkpoint support.
	/// </summary>
	public class InstallationCoordinatorService
	{
		private CheckpointService _checkpointService;
		private string _currentSessionId;
		private bool _widescreenNotificationShown;

		public event EventHandler<ComponentInstallEventArgs> ComponentInstallStarted;
		public event EventHandler<ComponentInstallEventArgs> ComponentInstallCompleted;
		public event EventHandler<ComponentInstallEventArgs> ComponentInstallFailed;
		public event EventHandler<InstallationErrorEventArgs> InstallationError;
		public event EventHandler<WidescreenNotificationEventArgs> WidescreenNotificationRequested;




		/// <summary>
		/// Executes installation of multiple components with automatic checkpoint creation.
		/// </summary>
		public async Task<ModComponent.InstallExitCode> ExecuteComponentsWithCheckpointsAsync(
			List<ModComponent> components,
			string destinationPath,
			IFileSystemProvider fileSystemProvider,
			IProgress<InstallProgress> progress = null,
			CancellationToken cancellationToken = default)
		{
			try
			{
				// Initialize checkpoint service
				_checkpointService = new CheckpointService(destinationPath);

				// Wire up progress events
				_checkpointService.Progress += (sender, e) =>
				{
					progress?.Report(new InstallProgress
					{
						Phase = InstallPhase.CreatingCheckpoint,
						Message = e.Message,
						Current = e.Current,
						Total = e.Total
					});
				};

				// Start installation session and capture baseline
				_currentSessionId = await _checkpointService.StartInstallationSessionAsync(cancellationToken);

				await Logger.LogAsync($"[Installation] Started session: {_currentSessionId}");


				int componentIndex = 0;
				foreach ( var component in components.Where(c => c.IsSelected) )
				{
					componentIndex++;

					if ( cancellationToken.IsCancellationRequested )
					{
						await Logger.LogWarningAsync("[Installation] Installation cancelled by user");
						await _checkpointService.CompleteSessionAsync(keepCheckpoints: true);
						return ModComponent.InstallExitCode.UserCancelledInstall;
					}


					if ( component.WidescreenOnly && !_widescreenNotificationShown )
					{
						await Logger.LogAsync("[Installation] First widescreen component detected, requesting notification");

						var widescreenArgs = new WidescreenNotificationEventArgs
						{
							Component = component,
							ComponentIndex = componentIndex,
							TotalComponents = components.Count
						};

						WidescreenNotificationRequested?.Invoke(this, widescreenArgs);


						if ( widescreenArgs.UserCancelled )
						{
							await Logger.LogWarningAsync("[Installation] User cancelled installation at widescreen notification");
							await _checkpointService.CompleteSessionAsync(keepCheckpoints: true);
							return ModComponent.InstallExitCode.UserCancelledInstall;
						}

						_widescreenNotificationShown = true;
						await Logger.LogAsync("[Installation] Widescreen notification acknowledged, continuing");
					}

					ComponentInstallStarted?.Invoke(this, new ComponentInstallEventArgs(component, componentIndex, components.Count));

					progress?.Report(new InstallProgress
					{
						Phase = InstallPhase.InstallingComponent,
						Message = $"Installing {component.Name} ({componentIndex}/{components.Count})",
						Current = componentIndex,
						Total = components.Count,
						ComponentName = component.Name
					});

					try
					{

						var exitCode = await component.ExecuteInstructionsAsync(
							component.Instructions,
							components,
							cancellationToken,
							fileSystemProvider
						);

						if ( exitCode != ModComponent.InstallExitCode.Success )
						{
							await Logger.LogErrorAsync($"[Installation] Component '{component.Name}' failed with exit code: {exitCode}");

							// Offer rollback option
							var errorArgs = new InstallationErrorEventArgs
							{
								Component = component,
								ErrorCode = exitCode,
								CanRollback = true,
								SessionId = _currentSessionId
							};

							InstallationError?.Invoke(this, errorArgs);

							if ( errorArgs.RollbackRequested )
							{
								await RollbackInstallationAsync(progress, cancellationToken);
								return ModComponent.InstallExitCode.UserCancelledInstall;
							}

							ComponentInstallFailed?.Invoke(this, new ComponentInstallEventArgs(component, componentIndex, components.Count));


							if ( exitCode == ModComponent.InstallExitCode.UserCancelledInstall || exitCode == ModComponent.InstallExitCode.UnknownError )
							{
								await _checkpointService.CompleteSessionAsync(keepCheckpoints: true);
								return exitCode;
							}

							continue;
						}

						// Create checkpoint after successful installation
						progress?.Report(new InstallProgress
						{
							Phase = InstallPhase.CreatingCheckpoint,
							Message = $"Creating checkpoint for {component.Name}",
							Current = componentIndex,
							Total = components.Count,
							ComponentName = component.Name
						});

						string checkpointId = await _checkpointService.CreateCheckpointAsync(
							component.Name,
							component.Guid.ToString(),
							cancellationToken
						);

						await Logger.LogAsync($"[Installation] Created checkpoint: {checkpointId} for component: {component.Name}");

						ComponentInstallCompleted?.Invoke(this, new ComponentInstallEventArgs(component, componentIndex, components.Count)
						{
							CheckpointId = checkpointId
						});
					}
					catch ( OperationCanceledException )
					{
						await Logger.LogWarningAsync("[Installation] Installation cancelled");
						await _checkpointService.CompleteSessionAsync(keepCheckpoints: true);
						return ModComponent.InstallExitCode.UserCancelledInstall;
					}
					catch ( Exception ex )
					{
						await Logger.LogErrorAsync($"[Installation] Unexpected error installing component '{component.Name}': {ex.Message}");

						var errorArgs = new InstallationErrorEventArgs
						{
							Component = component,
							Exception = ex,
							CanRollback = true,
							SessionId = _currentSessionId
						};

						InstallationError?.Invoke(this, errorArgs);

						if ( errorArgs.RollbackRequested )
						{
							await RollbackInstallationAsync(progress, cancellationToken);
							return ModComponent.InstallExitCode.UserCancelledInstall;
						}

						ComponentInstallFailed?.Invoke(this, new ComponentInstallEventArgs(component, componentIndex, components.Count));
					}
				}


				await _checkpointService.CompleteSessionAsync(keepCheckpoints: true);
				await Logger.LogAsync("[Installation] Installation completed successfully");

				return ModComponent.InstallExitCode.Success;
			}
			catch ( Exception ex )
			{
				await Logger.LogErrorAsync($"[Installation] Fatal error: {ex.Message}");
				return ModComponent.InstallExitCode.UnknownError;
			}
		}

		/// <summary>
		/// Rolls back the current installation to the baseline (pre-installation state).
		/// </summary>
		public async Task RollbackInstallationAsync(
			IProgress<InstallProgress> progress = null,
			CancellationToken cancellationToken = default)
		{
			if ( _checkpointService == null || string.IsNullOrEmpty(_currentSessionId) )
				return;

			await Logger.LogAsync("[Installation] Rolling back installation to baseline...");

			progress?.Report(new InstallProgress
			{
				Phase = InstallPhase.RollingBack,
				Message = "Rolling back installation..."
			});

			// Get all checkpoints in the session
			var checkpoints = await _checkpointService.ListCheckpointsAsync(_currentSessionId);

			if ( checkpoints.Any() )
			{
				// Restore to baseline (checkpoint 0 / before any mods)
				// Since we track from baseline, we just need to restore backwards through all checkpoints
				var firstCheckpoint = checkpoints.OrderBy(c => c.Sequence).FirstOrDefault();
				if ( firstCheckpoint != null )
				{
					progress?.Report(new InstallProgress
					{
						Phase = InstallPhase.RollingBack,
						Message = "Restoring to baseline state...",
						Current = 0,
						Total = checkpoints.Count
					});

					// Restore to the state before first checkpoint (baseline)
					await _checkpointService.RestoreCheckpointAsync(
						firstCheckpoint.Id,
						cancellationToken
					);
				}
			}

			await _checkpointService.CompleteSessionAsync(keepCheckpoints: false);
			await Logger.LogAsync("[Installation] Rollback completed");
		}

		/// <summary>
		/// Lists all available checkpoint sessions.
		/// </summary>
		public async Task<List<CheckpointSession>> ListAvailableSessionsAsync()
		{
			if ( _checkpointService == null )
				return new List<CheckpointSession>();

			return await _checkpointService.ListSessionsAsync();
		}

		/// <summary>
		/// Rolls back to a specific checkpoint in a session.
		/// </summary>
		public static async Task RestoreToCheckpointAsync(
			string sessionId,
			string checkpointId,
			string destinationPath,
			IProgress<InstallProgress> progress = null,
			CancellationToken cancellationToken = default)
		{
			var tempCheckpointService = new CheckpointService(destinationPath);

			// Verify session exists
			var sessions = await tempCheckpointService.ListSessionsAsync();
			var session = sessions.FirstOrDefault(s => s.Id == sessionId);

			if ( session == null )
				throw new ArgumentException($"Session {sessionId} not found");

			progress?.Report(new InstallProgress
			{
				Phase = InstallPhase.RollingBack,
				Message = "Restoring checkpoint...",
				Current = 0,
				Total = 1
			});

			await tempCheckpointService.RestoreCheckpointAsync(
				checkpointId,
				cancellationToken
			);

			await Logger.LogAsync($"[Installation] Restored to checkpoint: {checkpointId}");
		}
	}

	#region Event Args

	public class ComponentInstallEventArgs : EventArgs
	{
		public ModComponent Component { get; }
		public int ComponentIndex { get; }
		public int TotalComponents { get; }
		public string CheckpointId { get; set; }

		public ComponentInstallEventArgs(ModComponent component, int componentIndex, int totalComponents)
		{
			Component = component;
			ComponentIndex = componentIndex;
			TotalComponents = totalComponents;
		}
	}

	public class InstallationErrorEventArgs : EventArgs
	{
		public ModComponent Component { get; set; }
		public ModComponent.InstallExitCode ErrorCode { get; set; }
		public Exception Exception { get; set; }
		public bool CanRollback { get; set; }
		public bool RollbackRequested { get; set; }
		public string SessionId { get; set; }
	}

	public class WidescreenNotificationEventArgs : EventArgs
	{
		public ModComponent Component { get; set; }
		public int ComponentIndex { get; set; }
		public int TotalComponents { get; set; }
		public bool UserCancelled { get; set; }
		public bool DontShowAgain { get; set; }
	}

	public class InstallProgress
	{
		public InstallPhase Phase { get; set; }
		public string Message { get; set; }
		public int Current { get; set; }
		public int Total { get; set; }
		public string ComponentName { get; set; }
	}

	public enum InstallPhase
	{
		Initializing,
		InstallingComponent,
		CreatingCheckpoint,
		RollingBack,
		Completed
	}

	#endregion
}

