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

namespace KOTORModSync.Core.Services
{
	public class InstallationCoordinatorService
	{
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
			CancellationToken cancellationToken = default )
		{
			try
			{


				int componentIndex = 0;
				foreach (ModComponent component in components.Where( c => c.IsSelected ))
				{
					componentIndex++;

					if (cancellationToken.IsCancellationRequested)


					{
						await Logger.LogWarningAsync( "[Installation] Installation cancelled by user" ).ConfigureAwait( false );
						return ModComponent.InstallExitCode.UserCancelledInstall;
					}


					if (component.WidescreenOnly && !_widescreenNotificationShown)


					{
						await Logger.LogAsync( "[Installation] First widescreen component detected, requesting notification" ).ConfigureAwait( false );

						WidescreenNotificationEventArgs widescreenArgs = new WidescreenNotificationEventArgs
						{
							Component = component,
							ComponentIndex = componentIndex,
							TotalComponents = components.Count
						};

						WidescreenNotificationRequested?.Invoke( this, widescreenArgs );


						if (widescreenArgs.UserCancelled)


						{
							await Logger.LogWarningAsync( "[Installation] User cancelled installation at widescreen notification" ).ConfigureAwait( false );
							return ModComponent.InstallExitCode.UserCancelledInstall;
						}

						_widescreenNotificationShown = true;


						await Logger.LogAsync( "[Installation] Widescreen notification acknowledged, continuing" ).ConfigureAwait( false );
					}

					ComponentInstallStarted?.Invoke( this, new ComponentInstallEventArgs( component, componentIndex, components.Count ) );

					progress?.Report( new InstallProgress
					{
						Phase = InstallPhase.InstallingComponent,
						Message = $"Installing {component.Name} ({componentIndex}/{components.Count})",
						Current = componentIndex,
						Total = components.Count,
						ComponentName = component.Name
					} );

					try
					{

						ModComponent.InstallExitCode exitCode = await component.ExecuteInstructionsAsync(
							component.Instructions,
							components,
							cancellationToken,


							fileSystemProvider
						).ConfigureAwait( false );

						if (exitCode != ModComponent.InstallExitCode.Success)


						{
							await Logger.LogErrorAsync( $"[Installation] Component '{component.Name}' failed with exit code: {exitCode}" ).ConfigureAwait( false );

							InstallationErrorEventArgs errorArgs = new InstallationErrorEventArgs
							{
								Component = component,
								ErrorCode = exitCode,
								CanRollback = false,
								SessionId = null
							};

							InstallationError?.Invoke( this, errorArgs );

							if (errorArgs.RollbackRequested)


							{
								await RollbackInstallationAsync( progress, cancellationToken ).ConfigureAwait( false );
								return ModComponent.InstallExitCode.UserCancelledInstall;
							}

							ComponentInstallFailed?.Invoke( this, new ComponentInstallEventArgs( component, componentIndex, components.Count ) );


							if (exitCode == ModComponent.InstallExitCode.UserCancelledInstall || exitCode == ModComponent.InstallExitCode.UnknownError)
							{
								return exitCode;
							}

							continue;
						}


						ComponentInstallCompleted?.Invoke( this, new ComponentInstallEventArgs( component, componentIndex, components.Count )
						{
							CheckpointId = null
						} );
					}
					catch (OperationCanceledException)


					{
						await Logger.LogWarningAsync( "[Installation] Installation cancelled" )





.ConfigureAwait( false );
						return ModComponent.InstallExitCode.UserCancelledInstall;
					}
					catch (Exception ex)
					{
						await Logger.LogErrorAsync( $"[Installation] Unexpected error installing component '{component.Name}': {ex.Message}" ).ConfigureAwait( false );

						InstallationErrorEventArgs errorArgs = new InstallationErrorEventArgs
						{
							Component = component,
							Exception = ex,
							CanRollback = false,
							SessionId = null
						};

						InstallationError?.Invoke( this, errorArgs );

						if (errorArgs.RollbackRequested)
						{
							await RollbackInstallationAsync( progress, cancellationToken ).ConfigureAwait( false );
							return ModComponent.InstallExitCode.UserCancelledInstall;
						}

						ComponentInstallFailed?.Invoke( this, new ComponentInstallEventArgs( component, componentIndex, components.Count ) );
					}
				}


				await Logger.LogAsync( "[Installation] Installation completed successfully" ).ConfigureAwait( false );

				return ModComponent.InstallExitCode.Success;
			}
			catch (Exception ex)


			{
				await Logger.LogErrorAsync( $"[Installation] Fatal error: {ex.Message}" ).ConfigureAwait( false );
				return ModComponent.InstallExitCode.UnknownError;
			}
		}

		public static async Task RollbackInstallationAsync(
			IProgress<InstallProgress> progress = null,
			CancellationToken cancellationToken = default )


		{
			await Logger.LogAsync( "[Installation] Rollback not available - checkpoint system disabled" ).ConfigureAwait( false );
			return;
		}

		public static async Task<List<CheckpointSession>> ListAvailableSessionsAsync()


		{
			await Task.CompletedTask.ConfigureAwait( false );
			return new List<CheckpointSession>();
		}

		public static async Task RestoreToCheckpointAsync(
			string sessionId,
			string checkpointId,
			string destinationPath,
			IProgress<InstallProgress> progress = null,
			CancellationToken cancellationToken = default )


		{
			await Logger.LogAsync( "[Installation] Checkpoint restore not available - checkpoint system disabled" ).ConfigureAwait( false );
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

		public ComponentInstallEventArgs( ModComponent component, int componentIndex, int totalComponents )
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