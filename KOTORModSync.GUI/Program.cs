// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;

using Avalonia;
using Avalonia.ReactiveUI;

namespace KOTORModSync
{
    internal static class Program
    {



        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                Core.Logger.Initialize();

                // Parse command-line arguments
                CLIArguments.Parse(args);

                // Initialize telemetry
                Core.Services.TelemetryService.Instance.Initialize();

                // Record session start
                Core.Services.TelemetryService.Instance.RecordSessionStart(
                    componentCount: 0,
                    selectedCount: 0
                );

                // Register graceful shutdown handler for distributed cache engine
                // CRITICAL: Ensures all shared resources stop and ports are released
                AppDomain.CurrentDomain.ProcessExit += async (sender, eventArgs) =>
                {
                    try
                    {
                        await Core.Services.Download.DownloadCacheOptimizer.GracefulShutdownAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Core.Logger.LogError($"[Shutdown] Cache cleanup error: {ex.Message}");
                    }
                };

                var startTime = System.Diagnostics.Stopwatch.StartNew();
                _ = BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
                startTime.Stop();

                // Ensure distributed cache engine is shut down gracefully before final cleanup
                Core.Services.Download.DownloadCacheOptimizer.GracefulShutdownAsync().GetAwaiter().GetResult();

                // Record session end on exit
                Core.Services.TelemetryService.Instance.RecordSessionEnd(
                    durationMs: startTime.Elapsed.TotalMilliseconds,
                    completed: true
                );
                Core.Services.TelemetryService.Instance.Flush();
            }
            catch (Exception ex)
            {
                Core.Logger.LogException(ex);
                Core.Services.TelemetryService.Instance.RecordSessionEnd(
                    durationMs: 0,
                    completed: false
                );
                Core.Services.TelemetryService.Instance.Flush();
                
                // Attempt graceful cache shutdown even on crash
                try
                {
                    Core.Services.Download.DownloadCacheOptimizer.GracefulShutdownAsync().GetAwaiter().GetResult();
                }
                catch
                {
                    // Ignore errors during emergency shutdown
                }
                
                throw;
            }
        }


        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace().UseReactiveUI();
    }
}
