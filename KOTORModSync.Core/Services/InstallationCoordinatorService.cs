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
		// Checkpoint system disabled
		// private CheckpointService _checkpointService;
		// private string _currentSessionId;
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
			// Checkpoint system disabled - clean start-to-finish installation
			/*
			_checkpointService = new CheckpointService(destinationPath);
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
			_currentSessionId = await _checkpointService.StartInstallationSessionAsync(cancellationToken);
			await Logger.LogAsync($"[Installation] Started session: {_currentSessionId}");
			*/


				int componentIndex = 0;
				foreach ( var component in components.Where(c => c.IsSelected) )
				{
					componentIndex++;

				if ( cancellationToken.IsCancellationRequested )
				{
					await Logger.LogWarningAsync("[Installation] Installation cancelled by user");
					// await _checkpointService.CompleteSessionAsync(keepCheckpoints: true);
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
						// await _checkpointService.CompleteSessionAsync(keepCheckpoints: true);
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
							CanRollback = false, // Checkpoint system disabled
							SessionId = null // _currentSessionId
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
							// await _checkpointService.CompleteSessionAsync(keepCheckpoints: true);
							return exitCode;
						}

							continue;
						}

					// Checkpoint system disabled
					/*
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
					*/

					ComponentInstallCompleted?.Invoke(this, new ComponentInstallEventArgs(component, componentIndex, components.Count)
					{
						CheckpointId = null // checkpointId
					});
					}
				catch ( OperationCanceledException )
				{
					await Logger.LogWarningAsync("[Installation] Installation cancelled");
					// await _checkpointService.CompleteSessionAsync(keepCheckpoints: true);
					return ModComponent.InstallExitCode.UserCancelledInstall;
				}
				catch ( Exception ex )
				{
					await Logger.LogErrorAsync($"[Installation] Unexpected error installing component '{component.Name}': {ex.Message}");

					var errorArgs = new InstallationErrorEventArgs
					{
						Component = component,
						Exception = ex,
						CanRollback = false, // Checkpoint system disabled
						SessionId = null // _currentSessionId
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


			// await _checkpointService.CompleteSessionAsync(keepCheckpoints: true);
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
	/// Disabled: Checkpoint system removed.
	/// </summary>
	public async Task RollbackInstallationAsync(
		IProgress<InstallProgress> progress = null,
		CancellationToken cancellationToken = default)
	{
		// Checkpoint system disabled - rollback not available
		await Logger.LogAsync("[Installation] Rollback not available - checkpoint system disabled");
		return;
	}

	/// <summary>
	/// Lists all available checkpoint sessions.
	/// Disabled: Checkpoint system removed.
	/// </summary>
	public async Task<List<CheckpointSession>> ListAvailableSessionsAsync()
	{
		// Checkpoint system disabled
		await Task.CompletedTask; // Keep async signature
		return new List<CheckpointSession>();
	}

	/// <summary>
	/// Rolls back to a specific checkpoint in a session.
	/// Disabled: Checkpoint system removed.
	/// </summary>
	public static async Task RestoreToCheckpointAsync(
		string sessionId,
		string checkpointId,
		string destinationPath,
		IProgress<InstallProgress> progress = null,
		CancellationToken cancellationToken = default)
	{
		// Checkpoint system disabled - restore not available
		await Logger.LogAsync("[Installation] Checkpoint restore not available - checkpoint system disabled");
		return;
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

