// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
namespace KOTORModSync.Core
{
	public static class Logger
	{
		public const string LogFileName = "kotormodsync_";
		private static bool s_isInitialized;
		private static readonly object s_initializationLock = new object();
		private static readonly SemaphoreSlim s_semaphore = new SemaphoreSlim(1);
		public static event Action<string> Logged = delegate { };
		public static event Action<Exception> ExceptionLogged = delegate { };

		// Circular buffer for recent log messages (for error context)
		private const int LogHistorySize = 200;
		private static readonly LinkedList<string> s_logHistory = new LinkedList<string>();
		private static readonly object s_logHistoryLock = new object();
		public static void Initialize()
		{
			if ( s_isInitialized )
			{
				return;
			}
			lock ( s_initializationLock )
			{
				s_isInitialized = true;

				// Set console encoding to UTF-8 to properly display Unicode characters
				try
				{
					Console.OutputEncoding = System.Text.Encoding.UTF8;
				}
				catch ( Exception )
				{
					// If setting UTF-8 fails (e.g., on some limited environments), continue anyway
				}

				Log($"Logging initialized at {DateTime.Now}");
				AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
				TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
			}
		}
		[NotNull]
		private static async Task LogInternalAsync(
			[CanBeNull] string internalMessage,
			bool verbose = false,
			ConsoleColor? color = null
		)
		{
			if ( verbose && !MainConfig.DebugLogging )
				return;
			internalMessage = internalMessage ?? string.Empty;
			await s_semaphore.WaitAsync().ConfigureAwait(false);
			try
			{
				string logMessage = $"[{DateTime.Now}] {internalMessage}";
				string consoleMessage = $"[{DateTime.Now:HH:mm:ss}] {internalMessage}";
				if ( color.HasValue )
					Console.ForegroundColor = color.Value;
				await Console.Out.WriteLineAsync(consoleMessage).ConfigureAwait(false);
				if ( color.HasValue )
					Console.ResetColor();
				string formattedDate = DateTime.Now.ToString("yyyy-MM-dd");
				CancellationToken token = new CancellationTokenSource(delay: TimeSpan.FromMinutes(2)).Token;
				bool fileWritten = false;
				while ( !fileWritten )
				{
					try
					{
						string logDir = Path.Combine(Utility.Utility.GetBaseDirectory(), "Logs");
						if ( !Directory.Exists(logDir) )
							_ = Directory.CreateDirectory(logDir);
						string logFilePath = Path.Combine(
							logDir,
							LogFileName + formattedDate + ".log"
						);
						using ( var writer = new StreamWriter(logFilePath, append: true, System.Text.Encoding.UTF8) )
						{
							await writer.WriteLineAsync(logMessage).ConfigureAwait(false);
							fileWritten = true;
						}
					}
					catch ( IOException ex )
					{
						Console.WriteLine($"IOException occurred while writing log message: {ex.Message}");
						await Task.Delay(millisecondsDelay: 100, token).ConfigureAwait(false);
					}
					catch ( Exception ex )
					{
						Console.WriteLine($"Unexpected exception occurred while writing log message: {ex}");
						break;
					}
					token.ThrowIfCancellationRequested();
				}
				Logged?.Invoke(logMessage);

				// Add to log history for error context
				lock ( s_logHistoryLock )
				{
					s_logHistory.AddLast(consoleMessage);
					if ( s_logHistory.Count > LogHistorySize )
					{
						s_logHistory.RemoveFirst();
					}
				}
			}
			catch ( Exception ex )
			{
				Console.WriteLine($"Exception occurred in LogInternalAsync: {ex}");
			}
			finally
			{
				_ = s_semaphore.Release();
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
			lock ( s_logHistoryLock )
			{
				int takeCount = Math.Min(count, s_logHistory.Count);
				return s_logHistory.Skip(s_logHistory.Count - takeCount).ToList();
			}
		}

		/// <summary>
		/// Clears the log history buffer.
		/// </summary>
		public static void ClearLogHistory()
		{
			lock ( s_logHistoryLock )
			{
				s_logHistory.Clear();
			}
		}
		public static void Log([CanBeNull] string message = null, bool fileOnly = false) =>
			_ = LogInternalAsync(message, fileOnly);
		[NotNull]
		public static Task LogAsync([CanBeNull] string message) => LogInternalAsync(message);
		public static void LogVerbose([CanBeNull] string message) =>
			_ = LogInternalAsync($"[Verbose] {message}", verbose: true, ConsoleColor.DarkGray);
		[NotNull]
		public static Task LogVerboseAsync([CanBeNull] string message) =>
			LogInternalAsync($"[Verbose] {message}", verbose: true, color: ConsoleColor.DarkGray);
		public static void LogWarning([NotNull] string message) => _ = LogInternalAsync(
			$"[Warning] {message}",
			color: ConsoleColor.Yellow
		);
		[NotNull]
		public static Task LogWarningAsync([NotNull] string message) =>
			LogInternalAsync($"[Warning] {message}", color: ConsoleColor.Yellow);
		public static void LogError([CanBeNull] string message) =>
			_ = LogInternalAsync($"[Error] {message}", color: ConsoleColor.Red);
		[NotNull]
		public static Task LogErrorAsync([CanBeNull] string message) =>
			LogInternalAsync($"[Error] {message}", color: ConsoleColor.Red);
		public static void LogException([CanBeNull] Exception ex, [CanBeNull] string customMessage = null) =>
			_ = LogExceptionAsync(ex, customMessage);
		[NotNull]
		public static async Task LogExceptionAsync([CanBeNull] Exception ex, [CanBeNull] string customMessage = null)
		{
			ex = ex ?? new ApplicationException();
			await LogErrorAsync(customMessage ?? $"Unhandled exception: {ex.GetType()?.Name}").ConfigureAwait(false);
			await LogInternalAsync($"Exception: {ex.GetType()?.Name} - {ex.Message}", color: ConsoleColor.Red).ConfigureAwait(false);
			await LogInternalAsync($"Stack trace:{Environment.NewLine}{ex.StackTrace}", color: ConsoleColor.Magenta).ConfigureAwait(false);
			ExceptionLogged?.Invoke(ex);
		}
		private static void CurrentDomain_UnhandledException(
			[NotNull] object sender,
			UnhandledExceptionEventArgs e
		)
		{
			if ( !(e?.ExceptionObject is Exception ex) )
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
			if ( e?.Exception is null )
			{
				LogError("appdomain's unhandledexception did not have a valid exception handle?");
				return;
			}
			foreach ( Exception ex in e?.Exception.InnerExceptions )
			{
				LogException(ex);
			}
			e?.SetObserved();
		}
	}
}
