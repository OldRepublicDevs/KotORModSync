using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HoloPatcher.UI.ViewModels;
using HoloPatcher.UI.Views;

namespace HoloPatcher.UI
{

    public partial class App : Application
    {
        private UpdateManager _updateManager;
        private bool _cleanupRegistered = false;
        private bool _cleanupExecuted = false;
        private readonly object _cleanupLock = new object();

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);

            // Register process exit handler early to catch forced terminations
            // This provides a fallback if ShutdownRequested doesn't fire
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                string title = $"HoloPatcher {Core.VersionLabel}";
                if (Core.IsAlphaVersion(Core.VersionLabel))
                {
                    title += " [ALPHA - NOT FOR PRODUCTION USE]";
                }
                desktop.MainWindow = new MainWindow
                {
                    Title = title,
                    DataContext = new MainWindowViewModel(),
                };

                // Initialize and start update manager on UI thread
                InitializeUpdateManager();

                // Register cleanup handler for normal shutdown
                // This is the primary cleanup path for graceful shutdown
                if (!_cleanupRegistered)
                {
                    desktop.ShutdownRequested += OnShutdownRequested;
                    _cleanupRegistered = true;
                }
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void OnShutdownRequested(object sender, ShutdownRequestedEventArgs e)
        {
            CleanupUpdateManager();
        }

        private void OnProcessExit(object sender, EventArgs e)
        {
            // Fallback cleanup for forced termination or crashes
            // This ensures cleanup even if ShutdownRequested doesn't fire
            CleanupUpdateManager();
        }

        private void InitializeUpdateManager()
        {
            try
            {
                _updateManager = new UpdateManager
                {
                    CheckOnStartup = true,
                    SilentCheck = true,
                    UseBetaChannel = false // Set to true to use beta channel
                };

                // Start update checking
                _updateManager.Start();
            }
            catch (System.Exception ex)
            {
                // Log error but don't crash the app if update system fails
                System.Diagnostics.Debug.WriteLine($"Failed to initialize update manager: {ex.Message}");
            }
        }

        /// <summary>
        /// Safely disposes the update manager. This method is idempotent and thread-safe.
        /// Called from multiple cleanup paths to ensure reliable resource disposal.
        /// Uses double-checked locking pattern to prevent race conditions.
        /// </summary>
        private void CleanupUpdateManager()
        {
            // Early exit if already cleaned up (fast path)
            if (_cleanupExecuted)
            {
                return;
            }

            lock (_cleanupLock)
            {
                // Double-check after acquiring lock
                if (_cleanupExecuted || _updateManager is null)
                {
                    return;
                }

                try
                {
                    _updateManager.Dispose();
                }
                catch (Exception ex)
                {
                    // Log but don't throw - cleanup should never crash the app
                    System.Diagnostics.Debug.WriteLine($"Error disposing update manager: {ex.Message}");
                }
                finally
                {
                    _updateManager = null;
                    _cleanupExecuted = true;
                }
            }
        }
    }
}

