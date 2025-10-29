// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using JetBrains.Annotations;

using Newtonsoft.Json;

namespace KOTORModSync.Core.Installation
{
	public sealed class InstallSessionManager
	{
		private const string SessionFileName = "install_session.json";
		private const string SessionFolderName = ".kotor_modsync";
		private static readonly JsonSerializerSettings s_serializerSettings = new JsonSerializerSettings
		{
			Formatting = Formatting.Indented,
		};

		private readonly SemaphoreSlim _saveSemaphore = new SemaphoreSlim( 1, 1 );
		private InstallSessionState _state;
		private string _sessionPath;

		public InstallSessionState State => _state;

		public async Task InitializeAsync( [NotNull] IList<ModComponent> components, [NotNull] DirectoryInfo destinationPath )
		{
			if (components == null)
				throw new ArgumentNullException( nameof( components ) );
			if (destinationPath == null)
				throw new ArgumentNullException( nameof( destinationPath ) );

			_sessionPath = GetSessionFilePath( destinationPath );
			EnsureFolderExists( destinationPath );

			if (File.Exists( _sessionPath ))
			{
				string json = File.ReadAllText( _sessionPath, Encoding.UTF8 );
				InstallSessionState existingState = JsonConvert.DeserializeObject<InstallSessionState>( json, s_serializerSettings );
				if (existingState != null && ValidateLoadedState( existingState ))
				{
					_state = existingState;
					SyncComponentsWithState( components );
					return;
				}
			}

			_state = CreateNewState( components, destinationPath.FullName );
			SyncInitialComponentState( components );


			await SaveAsync().ConfigureAwait( false );
		}

		public async Task SaveAsync()
		{
			if (_state == null || string.IsNullOrEmpty( _sessionPath ))
				return;



			await _saveSemaphore.WaitAsync().ConfigureAwait( false );
			try
			{
				string tempPath = _sessionPath + ".tmp";
				string json = JsonConvert.SerializeObject( _state, s_serializerSettings );
				File.WriteAllText( tempPath, json, Encoding.UTF8 );
				File.Copy( tempPath, _sessionPath, overwrite: true );
				File.Delete( tempPath );
			}
			finally
			{
				_ = _saveSemaphore.Release();
			}
		}

		public async Task DeleteSessionAsync()
		{
			if (string.IsNullOrEmpty( _sessionPath ))
				return;



			await _saveSemaphore.WaitAsync().ConfigureAwait( false );
			try
			{
				if (File.Exists( _sessionPath ))
					File.Delete( _sessionPath );
			}
			finally
			{
				_ = _saveSemaphore.Release();
			}
		}

		public ComponentSessionEntry GetComponentEntry( Guid componentId )
		{
			if (_state.Components.TryGetValue( componentId, out ComponentSessionEntry entry ))
				return entry;

			throw new KeyNotFoundException( $"ModComponent {componentId} not found in session state" );
		}


		public void UpdateComponentState( ModComponent component )
		{
			ComponentSessionEntry entry = GetComponentEntry( component.Guid );
			entry.State = component.InstallState;
			entry.LastStartedUtc = component.LastStartedUtc;
			entry.LastCompletedUtc = component.LastCompletedUtc;
		}

		public void UpdateBackupPath( string backupPath ) => _state.BackupPath = backupPath;

		private void SyncComponentsWithState( IList<ModComponent> components )
		{
			foreach (ModComponent component in components)
			{
				if (!_state.Components.TryGetValue( component.Guid, out ComponentSessionEntry entry ))
				{
					entry = new ComponentSessionEntry
					{
						ComponentId = component.Guid,
					};
					_state.Components[component.Guid] = entry;
				}

				component.InstallState = entry.State;
				component.LastStartedUtc = entry.LastStartedUtc;
				component.LastCompletedUtc = entry.LastCompletedUtc;
			}
		}

		private void SyncInitialComponentState( IList<ModComponent> components )
		{
			foreach (ModComponent component in components)
			{
				ComponentSessionEntry entry = new ComponentSessionEntry
				{
					ComponentId = component.Guid,
					State = component.InstallState,
					LastStartedUtc = component.LastStartedUtc,
					LastCompletedUtc = component.LastCompletedUtc,
				};
				_state.Components[component.Guid] = entry;
			}
		}

		private static InstallSessionState CreateNewState( IList<ModComponent> components, string destinationPath )
		{
			return new InstallSessionState
			{
				Version = "2.0",
				SessionId = Guid.NewGuid(),
				CreatedUtc = DateTimeOffset.UtcNow,
				DestinationPath = destinationPath,
				ComponentOrder = components.Select( component => component.Guid ).ToList(),
				Components = new Dictionary<Guid, ComponentSessionEntry>(),
				CurrentRevision = 0,
			};
		}

		private static string GetSessionFilePath( DirectoryInfo destinationPath )
		{
			string folder = Path.Combine( destinationPath.FullName, SessionFolderName );
			return Path.Combine( folder, SessionFileName );
		}

		private static void EnsureFolderExists( DirectoryInfo destinationPath )
		{
			string folder = Path.Combine( destinationPath.FullName, SessionFolderName );
			_ = Directory.CreateDirectory( folder );
		}

		private static bool ValidateLoadedState( InstallSessionState state ) => state != null && state.ComponentOrder != null && state.Components != null;
	}
}