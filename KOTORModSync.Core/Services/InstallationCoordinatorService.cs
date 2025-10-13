// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KOTORModSync.Core.Services.FileSystem;
using Logger = KOTORModSync.Core.Logger;

namespace KOTORModSync.Core.Services
{
	
	
	
	public class InstallationCoordinatorService
	{
		private CheckpointService _checkpointService;
		private InstallationSession _currentSession;
		private bool _widescreenNotificationShown;

		public event EventHandler<ComponentInstallEventArgs> ComponentInstallStarted;
		public event EventHandler<ComponentInstallEventArgs> ComponentInstallCompleted;
		public event EventHandler<ComponentInstallEventArgs> ComponentInstallFailed;
		public event EventHandler<InstallationErrorEventArgs> InstallationError;
		public event EventHandler<WidescreenNotificationEventArgs> WidescreenNotificationRequested;

		
		
		
		public async Task<ModComponent.InstallExitCode> ExecuteComponentsWithCheckpointsAsync(
			List<ModComponent> components,
			string destinationPath,
			IFileSystemProvider fileSystemProvider,
			IProgress<InstallProgress> progress = null,
			CancellationToken cancellationToken = default)
		{
			try
			{
				
				_checkpointService = new CheckpointService(destinationPath);

				
				_checkpointService.CheckpointProgress += (sender, e) =>
				{
					progress?.Report(new InstallProgress
					{
						Phase = InstallPhase.CreatingCheckpoint,
						Message = $"{e.Operation}: {e.CurrentFile}",
						Current = e.Processed,
						Total = e.Total
					});
				};

				
				var sessionName = $"Installation_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";
				_currentSession = await _checkpointService.StartInstallationSessionAsync(sessionName, cancellationToken);

				await Logger.LogAsync($"[Installation] Started session: {sessionName}");

				
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

							
							var errorArgs = new InstallationErrorEventArgs
							{
								Component = component,
								ErrorCode = exitCode,
								CanRollback = true,
								Session = _currentSession
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

						
						progress?.Report(new InstallProgress
						{
							Phase = InstallPhase.CreatingCheckpoint,
							Message = $"Creating checkpoint for {component.Name}",
							Current = componentIndex,
							Total = components.Count,
							ComponentName = component.Name
						});

						var checkpoint = await _checkpointService.CreateCheckpointAsync(
							component.Name,
							component.Guid,
							cancellationToken
						);

						await Logger.LogAsync($"[Installation] Created checkpoint for component: {component.Name}");

						ComponentInstallCompleted?.Invoke(this, new ComponentInstallEventArgs(component, componentIndex, components.Count)
						{
							Checkpoint = checkpoint
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
							Session = _currentSession
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

		
		
		
		public async Task RollbackInstallationAsync(
			IProgress<InstallProgress> progress = null,
			CancellationToken cancellationToken = default)
		{
			if ( _checkpointService == null || _currentSession == null )
				return;

			await Logger.LogAsync("[Installation] Rolling back installation...");

			progress?.Report(new InstallProgress
			{
				Phase = InstallPhase.RollingBack,
				Message = "Rolling back installation..."
			});

			
			var baseline = _currentSession.Checkpoints.FirstOrDefault();
			if ( baseline != null )
			{
				var rollbackProgress = new Progress<RollbackProgress>(rp =>
				{
					progress?.Report(new InstallProgress
					{
						Phase = InstallPhase.RollingBack,
						Message = rp.CurrentAction,
						Current = rp.CurrentStep,
						Total = rp.TotalSteps,
						ComponentName = rp.CurrentCheckpoint
					});
				});

				await _checkpointService.RollbackToCheckpointAsync(
					baseline.CheckpointId,
					rollbackProgress,
					cancellationToken
				);
			}

			await _checkpointService.CompleteSessionAsync(keepCheckpoints: false);
			await Logger.LogAsync("[Installation] Rollback completed");
		}

		
		
		
		public async Task<List<InstallationSession>> ListAvailableSessionsAsync()
		{
			if ( _checkpointService == null )
				return new List<InstallationSession>();

			return await _checkpointService.ListSessionsAsync();
		}

		
		
		
		public async Task RollbackToCheckpointAsync(
			string sessionId,
			string checkpointId,
			string destinationPath,
			IProgress<InstallProgress> progress = null,
			CancellationToken cancellationToken = default)
		{
			var tempCheckpointService = new CheckpointService(destinationPath);

			
			var sessions = await tempCheckpointService.ListSessionsAsync();
			var session = sessions.FirstOrDefault(s => s.SessionId == sessionId);

			if ( session == null )
				throw new ArgumentException($"Session {sessionId} not found");

			var rollbackProgress = new Progress<RollbackProgress>(rp =>
			{
				progress?.Report(new InstallProgress
				{
					Phase = InstallPhase.RollingBack,
					Message = rp.CurrentAction,
					Current = rp.CurrentStep,
					Total = rp.TotalSteps,
					ComponentName = rp.CurrentCheckpoint
				});
			});

			await tempCheckpointService.RollbackToCheckpointAsync(
				checkpointId,
				rollbackProgress,
				cancellationToken
			);
		}
	}

	#region Event Args

	public class ComponentInstallEventArgs : EventArgs
	{
		public ModComponent Component { get; }
		public int ComponentIndex { get; }
		public int TotalComponents { get; }
		public Checkpoint Checkpoint { get; set; }

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
		public InstallationSession Session { get; set; }
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

