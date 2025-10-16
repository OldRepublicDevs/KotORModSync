// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Threading.Tasks;
using KOTORModSync.Core;
using NetSparkleUpdater;
using NetSparkleUpdater.Enums;
using NetSparkleUpdater.SignatureVerifiers;
using NetSparkleUpdater.UI.Avalonia;

namespace KOTORModSync.Services
{
	/// <summary>
	/// Service to manage automatic application updates using NetSparkle.
	/// </summary>
	public sealed class AutoUpdateService : IDisposable
	{
		private SparkleUpdater _sparkle;
		private bool _isInitialized;
		private bool _disposed;

		/// <summary>
		/// Gets the URL to the appcast file containing update information.
		/// For GitHub releases, this should be a URL to an appcast.xml file
		/// hosted on GitHub releases or in the repository.
		/// </summary>
		private const string AppCastUrl = "https://raw.githubusercontent.com/th3w1zard1/KOTORModSync/master/appcast.xml";

		/// <summary>
		/// Initializes the AutoUpdateService and sets up NetSparkle for automatic updates.
		/// </summary>
		public void Initialize()
		{
			if ( _isInitialized )
			{
				Logger.Log("AutoUpdateService already initialized.");
				return;
			}

			try
			{
				// Create the UI factory for Avalonia
				var uiFactory = new UIFactory();

				// Initialize NetSparkle with Ed25519 signature verification
				_sparkle = new SparkleUpdater(
					appcastUrl: AppCastUrl,
					signatureVerifier: new Ed25519Checker(SecurityMode.Strict, "jZSQV+2C1HL2Ufek3ekC7gtgOk5ctuDQzngh86OEdlA=")
				)
				{
					UIFactory = uiFactory,
					RelaunchAfterUpdate = true
				};

				_isInitialized = true;
				Logger.Log("AutoUpdateService initialized successfully.");
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Failed to initialize AutoUpdateService");
			}
		}

		/// <summary>
		/// Starts checking for updates automatically in the background.
		/// </summary>
		public void StartUpdateCheckLoop()
		{
			if ( !_isInitialized )
			{
				Logger.Log("Cannot start update check loop: AutoUpdateService not initialized.");
				return;
			}

			try
			{
				// Check for updates once per day
				_sparkle.StartLoop(doInitialCheck: true, checkFrequency: TimeSpan.FromHours(24));
				Logger.Log("Started automatic update check loop.");
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Failed to start update check loop");
			}
		}

		/// <summary>
		/// Manually checks for updates and shows UI if updates are available.
		/// </summary>
		/// <returns>True if updates are available, false otherwise.</returns>
		public async Task<bool> CheckForUpdatesAsync()
		{
			if ( !_isInitialized )
			{
				Logger.Log("Cannot check for updates: AutoUpdateService not initialized.");
				return false;
			}

			try
			{
				Logger.Log("Manually checking for updates...");
				var updateInfo = await _sparkle.CheckForUpdatesQuietly();

				if ( updateInfo.Status == UpdateStatus.UpdateAvailable )
				{
					// Show update UI to user
					await _sparkle.CheckForUpdatesAtUserRequest();
					return true;
				}

				Logger.Log($"No updates available. Status: {updateInfo.Status}");
				return false;
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Failed to check for updates");
				return false;
			}
		}

		/// <summary>
		/// Stops the automatic update check loop.
		/// </summary>
		public void StopUpdateCheckLoop()
		{
			if ( _sparkle != null )
			{
				_sparkle.StopLoop();
				Logger.Log("Stopped automatic update check loop.");
			}
		}


		#region IDisposable

		public void Dispose()
		{
			if ( _disposed )
				return;

			try
			{
				if ( _sparkle != null )
				{
					StopUpdateCheckLoop();
					_sparkle.Dispose();
					_sparkle = null;
				}

				_disposed = true;
				Logger.Log("AutoUpdateService disposed.");
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error disposing AutoUpdateService");
			}
		}

		#endregion
	}
}

