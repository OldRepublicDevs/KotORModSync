// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using JetBrains.Annotations;

using KOTORModSync.Core;
using KOTORModSync.Services;

namespace KOTORModSync
{
    public class App : Application
    {
        private AutoUpdateService _autoUpdateService;

        public override void Initialize() => AvaloniaXamlLoader.Load(this);

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                try
                {
                    // Load default theme BEFORE creating MainWindow to ensure control templates are available
                    ThemeManager.UpdateStyle("/Styles/FluentLightStyle.axaml");

                    TaskScheduler.UnobservedTaskException += HandleUnobservedTaskException;

                    // Subscribe to Startup event to apply CLI arguments
                    desktop.Startup += Desktop_Startup;

                    desktop.MainWindow = new MainWindow();
                    Logger.Log("Started main window");

                    // Initialize auto-update service
                    InitializeAutoUpdates();
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex);
                }
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void Desktop_Startup(object sender, ControlledApplicationLifetimeStartupEventArgs e)
        {
            try
            {
                Logger.LogVerbose("[App.Desktop_Startup] Applying CLI arguments to MainConfig");

                // Create MainConfig instance to access instance properties that set static values
                var config = new MainConfig();

                // Apply CLI arguments to MainConfig (this happens after MainWindow is created)
                if (!string.IsNullOrWhiteSpace(CLIArguments.ModDirectory))
                {
                    if (System.IO.Directory.Exists(CLIArguments.ModDirectory))
                    {
                        config.sourcePath = new System.IO.DirectoryInfo(CLIArguments.ModDirectory);
                        Logger.Log($"[App.Desktop_Startup] Set SourcePath from CLI: '{CLIArguments.ModDirectory}'");
                    }
                    else
                    {
                        Logger.LogWarning($"[App.Desktop_Startup] CLI ModDirectory does not exist: '{CLIArguments.ModDirectory}'");
                    }
                }

                if (!string.IsNullOrWhiteSpace(CLIArguments.KotorPath))
                {
                    if (System.IO.Directory.Exists(CLIArguments.KotorPath))
                    {
                        config.destinationPath = new System.IO.DirectoryInfo(CLIArguments.KotorPath);
                        Logger.Log($"[App.Desktop_Startup] Set DestinationPath from CLI: '{CLIArguments.KotorPath}'");
                    }
                    else
                    {
                        Logger.LogWarning($"[App.Desktop_Startup] CLI KotorPath does not exist: '{CLIArguments.KotorPath}'");
                    }
                }

                // Auto-load instruction file if provided via CLI (delay to allow MainWindow to fully initialize)
                if (!string.IsNullOrWhiteSpace(CLIArguments.InstructionFile))
                {
                    if (System.IO.File.Exists(CLIArguments.InstructionFile))
                    {
                        Logger.Log($"[App.Desktop_Startup] Scheduling auto-load of instruction file: '{CLIArguments.InstructionFile}'");

                        // Use dispatcher to load file after UI is fully initialized
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            if (sender is IClassicDesktopStyleApplicationLifetime desktopLifetime
                                && desktopLifetime.MainWindow is MainWindow mainWindow)
                            {
                                mainWindow.AutoLoadInstructionFileAsync(CLIArguments.InstructionFile);
                            }
                        }, Avalonia.Threading.DispatcherPriority.Loaded);
                    }
                    else
                    {
                        Logger.LogWarning($"[App.Desktop_Startup] CLI InstructionFile does not exist: '{CLIArguments.InstructionFile}'");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "[App.Desktop_Startup] Error applying CLI arguments");
            }
        }

        private void InitializeAutoUpdates()
        {
            try
            {
                _autoUpdateService = new AutoUpdateService();
                _autoUpdateService.Initialize();
                _autoUpdateService.StartUpdateCheckLoop();
                Logger.Log("Auto-update service started successfully.");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Failed to initialize auto-update service");
            }
        }

        private void HandleUnobservedTaskException([CanBeNull] object sender, UnobservedTaskExceptionEventArgs e)
        {

            Logger.LogException(e.Exception);
            e.SetObserved();
        }
    }
}
