using System;
using Microsoft.Extensions.Logging;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    /// <summary>
    /// Static logger wrapper for NCS decompiler, compatible with C# 7.3 and .NET 4.6.2.
    /// Provides simple static methods that replace JavaSystem.@out.Println calls.
    /// </summary>
    public static class DecompilerLogger
    {
        private static ILogger _logger;

        /// <summary>
        /// Initialize the logger with a specific logger instance.
        /// If not initialized, falls back to Console.WriteLine for compatibility.
        /// </summary>
        public static void Initialize(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Logs an empty debug line (replaces JavaSystem.@out.Println() with no arguments).
        /// </summary>
        public static void Debug()
        {
            if (_logger != null)
            {
                _logger.LogDebug("");
            }
            else
            {
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Logs a debug message (replaces JavaSystem.@out.Println for debug output).
        /// </summary>
        public static void Debug(string message)
        {
            if (_logger != null)
            {
                _logger.LogDebug(message);
            }
            else
            {
                Console.WriteLine(message);
            }
        }

        /// <summary>
        /// Logs a debug message with formatting (replaces JavaSystem.@out.Println for debug output).
        /// </summary>
        public static void Debug(string format, params object[] args)
        {
            if (_logger != null)
            {
                _logger.LogDebug(format, args);
            }
            else
            {
                Console.WriteLine(format, args);
            }
        }

        /// <summary>
        /// Logs an informational message (replaces JavaSystem.@out.Println for info output).
        /// </summary>
        public static void Info(string message)
        {
            if (_logger != null)
            {
                _logger.LogInformation(message);
            }
            else
            {
                Console.WriteLine(message);
            }
        }

        /// <summary>
        /// Logs an informational message with formatting.
        /// </summary>
        public static void Info(string format, params object[] args)
        {
            if (_logger != null)
            {
                _logger.LogInformation(format, args);
            }
            else
            {
                Console.WriteLine(format, args);
            }
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        public static void Warning(string message)
        {
            if (_logger != null)
            {
                _logger.LogWarning(message);
            }
            else
            {
                Console.Error.WriteLine("WARNING: " + message);
            }
        }

        /// <summary>
        /// Logs a warning message with formatting.
        /// </summary>
        public static void Warning(string format, params object[] args)
        {
            if (_logger != null)
            {
                _logger.LogWarning(format, args);
            }
            else
            {
                Console.Error.WriteLine("WARNING: " + string.Format(format, args));
            }
        }

        /// <summary>
        /// Logs an error message (replaces JavaSystem.@err.Println).
        /// </summary>
        public static void Error(string message)
        {
            if (_logger != null)
            {
                _logger.LogError(message);
            }
            else
            {
                Console.Error.WriteLine(message);
            }
        }

        /// <summary>
        /// Logs an error message with formatting.
        /// </summary>
        public static void Error(string format, params object[] args)
        {
            if (_logger != null)
            {
                _logger.LogError(format, args);
            }
            else
            {
                Console.Error.WriteLine(string.Format(format, args));
            }
        }

        /// <summary>
        /// Logs an error message with exception (replaces JavaSystem.@err.Println with exception).
        /// </summary>
        public static void Error(Exception exception, string message)
        {
            if (_logger != null)
            {
                _logger.LogError(exception, message);
            }
            else
            {
                Console.Error.WriteLine(message);
                if (exception != null)
                {
                    Console.Error.WriteLine(exception.ToString());
                }
            }
        }

        /// <summary>
        /// Logs an error message with exception and formatting.
        /// </summary>
        public static void Error(Exception exception, string format, params object[] args)
        {
            if (_logger != null)
            {
                _logger.LogError(exception, format, args);
            }
            else
            {
                Console.Error.WriteLine(string.Format(format, args));
                if (exception != null)
                {
                    Console.Error.WriteLine(exception.ToString());
                }
            }
        }
    }
}

