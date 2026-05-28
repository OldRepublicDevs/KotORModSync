// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Threading.Tasks;

using KOTORModSync.Core;

namespace KOTORModSync.Services
{
    /// <summary>
    /// Service to manage automatic application updates using NetSparkle.
    /// </summary>
    public sealed class AutoUpdateService : IDisposable
    {
        private readonly IAutoUpdateClient _client;
        private readonly AutoUpdateSettings _settings;
        private bool _isInitialized;
        private bool _disposed;

        public AutoUpdateService()
            : this(new NetSparkleUpdateClient(), AutoUpdateSettings.Default)
        {
        }

        internal AutoUpdateService(IAutoUpdateClient client)
            : this(client, AutoUpdateSettings.Default)
        {
        }

        internal AutoUpdateService(IAutoUpdateClient client, AutoUpdateSettings settings)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public AutoUpdateSettings CurrentSettings => _settings;

        /// <summary>
        /// Initializes the AutoUpdateService and sets up NetSparkle for automatic updates.
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized)
            {
                Logger.Log("AutoUpdateService already initialized.");
                return;
            }

            try
            {
                _client.Initialize(_settings);
                _isInitialized = true;
                Logger.Log("AutoUpdateService initialized successfully.");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Failed to initialize AutoUpdateService");
            }
        }

        /// <summary>
        /// Starts checking for updates automatically in the background.
        /// </summary>
        public void StartUpdateCheckLoop()
        {
            if (!_isInitialized)
            {
                Logger.Log("Cannot start update check loop: AutoUpdateService not initialized.");
                return;
            }

            try
            {
                // MA0134: Explicitly observe result of any async method calls (if any are started in background).
                _ = Task.Run(async () =>
                {
                    // Check for updates once per day
                    await _client.StartLoopAsync(doInitialCheck: true, checkFrequency: TimeSpan.FromHours(24));
                    Logger.Log("Started automatic update check loop.");
                }).ContinueWith(
                    t =>
                    {
                        if (!(t.Exception is null))
                        {
                            Logger.LogException(t.Exception.InnerException ?? t.Exception, "Failed to start update check loop");
                        }
                    }, TaskContinuationOptions.OnlyOnFaulted
                );
            }
            catch (Exception ex)
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
            if (!_isInitialized)
            {
                await Logger.LogAsync("Cannot check for updates: AutoUpdateService not initialized.");
                return false;
            }

            try
            {
                await Logger.LogAsync("Manually checking for updates...");

                AutoUpdateCheckResult updateResult = await _client.CheckForUpdatesQuietlyAsync();
                if (updateResult.UpdateAvailable)
                {
                    await _client.ShowUpdateUiAsync();
                    return true;
                }

                await Logger.LogAsync($"No updates available. Status: {updateResult.StatusMessage}");
                return false;
            }
            catch (Exception ex)
            {
                await Logger.LogExceptionAsync(ex, "Failed to check for updates");
                return false;
            }
        }

        /// <summary>
        /// Stops the automatic update check loop.
        /// </summary>
        public void StopUpdateCheckLoop()
        {
            _client.StopLoop();
            Logger.Log("Stopped automatic update check loop.");
        }


        #region IDisposable

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                StopUpdateCheckLoop();
                _client.Dispose();
                _disposed = true;
                Logger.Log("AutoUpdateService disposed.");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error disposing AutoUpdateService");
            }
        }

        #endregion
    }
}
