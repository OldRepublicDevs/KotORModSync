// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using JetBrains.Annotations;

using NLog;
using NLog.Targets;
namespace KOTORModSync.Core
{
    public static class Logger
    {
        public const string LogFileName = "kotormodsync_";
        private static bool s_isInitialized;
        private static readonly object s_initializationLock = new object();
        private static readonly NLog.Logger s_logger = LogManager.GetCurrentClassLogger();

        public enum LogType
        {
            Info,
            Warning,
            Error,
            Verbose,
        }
        public static event Action<string> Logged = delegate { };
        public static event Action<Exception> ExceptionLogged = delegate { };
        public static void Initialize()
        {
            if (s_isInitialized)
            {
                return;
            }
            lock (s_initializationLock)
            {
                s_isInitialized = true;

                // Set console encoding to UTF-8 to properly display Unicode characters
                try
                {
                    Console.OutputEncoding = System.Text.Encoding.UTF8;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"[Logger] Failed to set console encoding to UTF-8: {ex}");
                }

                // Configure NLog with debug logging setting
                GlobalDiagnosticsContext.Set("DebugLogging", MainConfig.DebugLogging.ToString().ToLower());

                Log($"Logging initialized at {DateTime.Now}");
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            }
        }
        [NotNull]
        private static async Task LogInternalAsync(
            [CanBeNull] string internalMessage,
            LogLevel level,
            bool fileOnly = false
        )
        {
            internalMessage = internalMessage ?? string.Empty;

            try
            {
                // Log using NLog
                s_logger.Log(level, internalMessage);

                // Invoke the Logged event for backward compatibility
                string logMessage = $"[{DateTime.Now}] {internalMessage}";
                Logged?.Invoke(logMessage);

                // Only output to console/error if not fileOnly
                if (!fileOnly)
                {
                    // Output to appropriate destination based on context
                    if (IsRunningInTestContext())
                    {
                        await Console.Error.WriteLineAsync(logMessage).ConfigureAwait(false);
                        await Console.Error.FlushAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        // Use Console.WriteLine for normal runtime
                        Console.WriteLine(logMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"Exception occurred in LogInternalAsync: {ex}";
                if (IsRunningInTestContext())
                {
                    await Console.Error.WriteLineAsync(errorMessage).ConfigureAwait(false);
                    await Console.Error.FlushAsync().ConfigureAwait(false);
                }
                else
                {
                    await Logger.LogErrorAsync(errorMessage).ConfigureAwait(false);
                }
            }

            // Add a small delay to make it actually async and ensure proper sequencing
            await Task.Delay(1).ConfigureAwait(false);
        }

        /// <summary>
        /// Determines if the code is currently running within a test context.
        /// This is used to choose between test-specific output (for tests)
        /// and Console.WriteLine (for normal runtime).
        /// </summary>
        /// <returns>True if running in test context, false otherwise</returns>
        private static bool IsRunningInTestContext()
        {
            try
            {
                // Check for test-related environment variables or assembly names
                // This is a more generic approach that doesn't require NUnit references
                AppDomain currentDomain = AppDomain.CurrentDomain;
                System.Reflection.Assembly[] assemblies = currentDomain.GetAssemblies();

                // Look for test framework assemblies
                foreach (System.Reflection.Assembly assembly in assemblies)
                {
                    string assemblyName = assembly.GetName().Name?.ToLowerInvariant();
                    if (assemblyName != null &&
                        (assemblyName.Contains("nunit") ||
                         assemblyName.Contains("xunit") ||
                         assemblyName.Contains("mstest") ||
                         assemblyName.Contains("test")))
                    {
                        return true;
                    }
                }

                // Check for test-related environment variables
                string testEnvironment = Environment.GetEnvironmentVariable("DOTNET_TEST_RUNNER");
                if (!string.IsNullOrEmpty(testEnvironment))
                {
                    return true;
                }

                // Check if we're running under a test runner process
                // Use a try-catch to prevent stack overflow issues
                try
                {
                    string processName = System.Diagnostics.Process.GetCurrentProcess().ProcessName.ToLowerInvariant();
                    if (processName.Contains("test") || processName.Contains("nunit") || processName.Contains("xunit"))
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"[Logger] Failed to obtain process name for test context detection: {ex}");
                    // If we can't get the process name, assume we're not in a test
                    // This prevents stack overflow issues
                }

                return false;
            }
            catch
            {
                // If we can't determine the context, assume we're not in a test
                return false;
            }
        }

        /// <summary>
        /// Gets the most recent log messages for error context.
        /// </summary>
        /// <param name="count">Number of recent messages to retrieve (default: 50)</param>
        /// <returns>List of recent log messages, most recent last</returns>
        [NotNull]
        public static List<string> GetRecentLogMessages(int count = 50)
        {
            try
            {
                MemoryTarget memoryTarget = LogManager.Configuration?.FindTargetByName<MemoryTarget>("memory");
                if (memoryTarget is null)
                {
                    return new List<string>();
                }

                IList<string> logs = memoryTarget.Logs;
                int takeCount = Math.Min(count, logs.Count);
                return logs.Skip(logs.Count - takeCount).ToList();
            }
            catch (Exception ex)
            {
                string errorMessage = $"Exception occurred in GetRecentLogMessages: {ex}";
                if (IsRunningInTestContext())
                {
                    Console.Error.WriteLine(errorMessage);
                    Console.Error.Flush();
                }
                else
                {
                    Console.WriteLine(errorMessage);
                }
                return new List<string>();
            }
        }

        /// <summary>
        /// Clears the log history buffer.
        /// </summary>
        public static void ClearLogHistory()
        {
            try
            {
                MemoryTarget memoryTarget = LogManager.Configuration?.FindTargetByName<MemoryTarget>("memory");
                memoryTarget?.Logs.Clear();
            }
            catch (Exception ex)
            {
                string errorMessage = $"Exception occurred in ClearLogHistory: {ex}";
                if (IsRunningInTestContext())
                {
                    Console.Error.WriteLine(errorMessage);
                    Console.Error.Flush();
                }
                else
                {
                    Console.WriteLine(errorMessage);
                }
            }
        }
        public static void Log(
            [CanBeNull] string message = null,
            bool fileOnly = false,
            LogType logType = LogType.Info
        ) =>
            _ = LogAsync(message, fileOnly, logType);

        [NotNull]
        public static Task LogAsync(
            [CanBeNull] string message = "",
            bool fileOnly = false,
            LogType logType = LogType.Info
        ) =>
            LogCoreAsync(message, MapLogTypeToLogLevel(logType), ex: null);

        public static void LogInfo(
            [CanBeNull] string message = null,
            [CanBeNull] Exception ex = null,
            bool fileOnly = false
        ) =>
            _ = LogInfoAsync(message, ex, fileOnly);

        [NotNull]
        public static Task LogInfoAsync(
            [CanBeNull] string message = "",
            [CanBeNull] Exception ex = null,
            bool fileOnly = false
        ) =>
            LogCoreAsync(message, LogLevel.Info, ex);

        public static void LogVerbose([CanBeNull] string message, [CanBeNull] Exception ex = null, bool fileOnly = false) =>
            _ = LogVerboseAsync(message, ex);

        [NotNull]
        public static Task LogVerboseAsync([CanBeNull] string message, [CanBeNull] Exception ex = null, bool fileOnly = false)
        {
            // In test context, verbose logs should be file-only by default to avoid cluttering test output
            // The test platform treats stderr output as warnings, so we suppress verbose logs from console/error
            if (IsRunningInTestContext() && !fileOnly)
            {
                fileOnly = true;
            }
            return LogCoreAsync(message, LogLevel.Debug, ex, fileOnly);
        }

        public static void LogWarning([CanBeNull] string message, [CanBeNull] Exception ex = null, bool fileOnly = false) =>
            _ = LogWarningAsync(message, ex);

        [NotNull]
        public static Task LogWarningAsync([CanBeNull] string message, [CanBeNull] Exception ex = null, bool fileOnly = false) =>
            LogCoreAsync(message, LogLevel.Warn, ex);

        public static void LogError([CanBeNull] string message, [CanBeNull] Exception ex = null, bool fileOnly = false) =>
            _ = LogErrorAsync(message, ex);

        [NotNull]
        public static Task LogErrorAsync([CanBeNull] string message, [CanBeNull] Exception ex = null, bool fileOnly = false) =>
            LogCoreAsync(message, LogLevel.Error, ex);

        public static void LogException([CanBeNull] Exception ex, [CanBeNull] string customMessage = null, bool fileOnly = false) =>
            LogError(customMessage, ex);

        [NotNull]
        public static Task LogExceptionAsync([CanBeNull] Exception ex, [CanBeNull] string customMessage = null, bool fileOnly = false) =>
            LogErrorAsync(customMessage, ex);

        [NotNull]
        private static Task LogCoreAsync(
            [CanBeNull] string message,
            LogLevel level,
            [CanBeNull] Exception ex,
            bool fileOnly = false
        )
        {
            string customMessage = DetermineCustomMessage(message, ex);
            string formattedMessage = ApplyLevelPrefix(customMessage, level);
            return LogCoreInternalAsync(formattedMessage, level, ex, customMessage, fileOnly);
        }

        [NotNull]
        private static async Task LogCoreInternalAsync(
            [NotNull] string formattedMessage,
            LogLevel level,
            [CanBeNull] Exception ex,
            [NotNull] string customMessageForTelemetry,
            bool fileOnly = false
        )
        {
            await LogInternalAsync(formattedMessage, level, fileOnly).ConfigureAwait(false);

            if (ex is null)
            {
                return;
            }

            await LogExceptionDetailsAsync(ex, customMessageForTelemetry).ConfigureAwait(false);
        }

        [NotNull]
        private static string DetermineCustomMessage([CanBeNull] string message, [CanBeNull] Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                return message;
            }

            if (ex is null)
            {
                return string.Empty;
            }

            string exceptionName = ex.GetType().Name;
            return $"Unhandled exception: {exceptionName}";
        }

        [NotNull]
        private static string ApplyLevelPrefix([CanBeNull] string message, LogLevel level)
        {
            string nonNullMessage = message ?? string.Empty;
            string prefix = string.Empty;

            if (level == LogLevel.Warn)
            {
                prefix = "[Warning] ";
            }
            else if (level == LogLevel.Error)
            {
                prefix = "[Error] ";
            }
            else if (level == LogLevel.Debug)
            {
                prefix = "[Verbose] ";
            }

            if (string.IsNullOrEmpty(prefix))
            {
                return nonNullMessage;
            }

            if (nonNullMessage.StartsWith(prefix, StringComparison.Ordinal))
            {
                return nonNullMessage;
            }

            return $"{prefix}{nonNullMessage}";
        }

        private static LogLevel MapLogTypeToLogLevel(LogType logType)
        {
            switch (logType)
            {
                case LogType.Warning:
                    return LogLevel.Warn;
                case LogType.Error:
                    return LogLevel.Error;
                case LogType.Verbose:
                    return LogLevel.Debug;
                default:
                    return LogLevel.Info;
            }
        }

        [NotNull]
        private static async Task LogExceptionDetailsAsync([CanBeNull] Exception ex, [NotNull] string customMessage)
        {
            Exception actualException = ex ?? new ApplicationException();

            await LogInternalAsync(
                $"Exception: {actualException.GetType().Name} - {actualException.Message}",
                LogLevel.Error
            ).ConfigureAwait(false);

            await LogInternalAsync(
                $"Stack trace:{Environment.NewLine}{actualException.StackTrace}",
                LogLevel.Error
            ).ConfigureAwait(false);

            ExceptionLogged?.Invoke(actualException);

            try
            {
                Services.TelemetryService.Instance.RecordError(
                    errorType: actualException.GetType().FullName ?? "UnknownException",
                    errorMessage: string.IsNullOrWhiteSpace(customMessage)
                        ? actualException.Message
                        : customMessage,
                    stackTrace: actualException.StackTrace
                );
            }
            catch (Exception telemetryEx)
            {
                string errorMessage = $"Failed to send exception to telemetry: {telemetryEx.Message}";
                if (IsRunningInTestContext())
                {
                    await Console.Error.WriteLineAsync(errorMessage).ConfigureAwait(false);
                    await Console.Error.FlushAsync().ConfigureAwait(false);
                }
                else
                {
                    await Console.Error.WriteLineAsync(errorMessage).ConfigureAwait(false);
                }
            }
        }
        private static void CurrentDomain_UnhandledException(
            [NotNull] object sender,
            UnhandledExceptionEventArgs e
        )
        {
            if (!(e?.ExceptionObject is Exception ex))
            {
                LogError("current appdomain's unhandled exception did not have a valid exception handle?");
                return;
            }
            LogException(ex);
        }
        private static void TaskScheduler_UnobservedTaskException(
            [NotNull] object sender,
            UnobservedTaskExceptionEventArgs e
        )
        {
            if (e?.Exception is null)
            {
                LogError("appdomain's unhandledexception did not have a valid exception handle?");
                return;
            }
            foreach (Exception ex in e.Exception.InnerExceptions)
            {
                LogException(ex);
            }
            e.SetObserved();
        }
    }
}
