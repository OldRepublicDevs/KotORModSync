using System;
using System.IO;
using System.Text;
using KOTORModSync;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Logger
{

    /// <summary>
    /// Writes installation log files for TSLPatcher mod installations.
    /// Creates installlog.txt in the mod directory for compatibility with KOTORModSync's VerifyInstall() method.
    /// </summary>
    public class InstallLogWriter : IDisposable
    {
        private readonly string _logFilePath;
        private readonly StreamWriter _writer;
        private readonly object _lockObject = new object();
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new InstallLogWriter and creates the log file in the mod directory.
        /// </summary>
        /// <param name="modPath">The TSLPatcher mod directory (where changes.ini is located)</param>
        /// <param name="useRtf">Whether to use RTF format (default: false, uses plain text)</param>
        public InstallLogWriter(string modPath, bool useRtf = false)
        {
            if (string.IsNullOrEmpty(modPath))
            {
                throw new ArgumentNullException(nameof(modPath));
            }

            string logFileName = useRtf ? "installlog.rtf" : "installlog.txt";
            _logFilePath = Path.Combine(modPath, logFileName);

            // Delete existing log files before creating new one
            if (File.Exists(_logFilePath))
            {
                try
                {
                    File.Delete(_logFilePath);
                }
                catch
                {
                    // Ignore errors when deleting existing log
                }
            }

            // Also delete RTF version if we're creating TXT, and vice versa
            string alternateLogPath = Path.Combine(modPath, useRtf ? "installlog.txt" : "installlog.rtf");
            if (File.Exists(alternateLogPath))
            {
                try
                {
                    File.Delete(alternateLogPath);
                }
                catch
                {
                    // Ignore errors when deleting alternate log
                }
            }

            // Create new log file with UTF-8 encoding
            _writer = new StreamWriter(_logFilePath, append: false, Encoding.UTF8);
        }

        /// <summary>
        /// Writes the installation header with metadata.
        /// </summary>
        /// <param name="modPath">The mod directory path</param>
        /// <param name="gamePath">The game directory path</param>
        /// <param name="game">The detected game (K1 or TSL)</param>
        public void WriteHeader(string modPath, string gamePath, Game? game)
        {
            lock (_lockObject)
            {
                _writer.WriteLine("TSLPatcher Installation Log");
                _writer.WriteLine("===========================");
                _writer.WriteLine($"Installation Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                _writer.WriteLine($"Mod Path: {modPath}");
                _writer.WriteLine($"Game Path: {gamePath}");
                _writer.WriteLine($"Game Detected: {(game.HasValue ? game.Value.ToString() : "Unknown")}");
                _writer.WriteLine();
                _writer.Flush();
            }
        }

        /// <summary>
        /// Writes an informational message to the log.
        /// </summary>
        /// <param name="message">The message to write</param>
        public void WriteInfo(string message)
        {
            lock (_lockObject)
            {
                _writer.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
            }
        }

        /// <summary>
        /// Writes an error message to the log in the format required for VerifyInstall() to detect it.
        /// CRITICAL: Must use exact format "Error: [message]" for KOTORModSync compatibility.
        /// </summary>
        /// <param name="message">The error message to write</param>
        public void WriteError(string message)
        {
            lock (_lockObject)
            {
                // CRITICAL: Use exact format "Error: [message]" for VerifyInstall() to detect errors
                _writer.WriteLine($"Error: {message}");
                _writer.Flush(); // Flush immediately on errors to ensure they're saved
            }
        }

        /// <summary>
        /// Writes an error message using the alternative format "[Error] [message]".
        /// </summary>
        /// <param name="message">The error message to write</param>
        public void WriteErrorAlternative(string message)
        {
            lock (_lockObject)
            {
                // Alternative error format also detected by VerifyInstall()
                _writer.WriteLine($"[Error] {message}");
                _writer.Flush(); // Flush immediately on errors to ensure they're saved
            }
        }

        /// <summary>
        /// Writes a warning message to the log.
        /// </summary>
        /// <param name="message">The warning message to write</param>
        public void WriteWarning(string message)
        {
            lock (_lockObject)
            {
                _writer.WriteLine($"Warning: {message}");
            }
        }

        /// <summary>
        /// Writes file operation details to the log.
        /// </summary>
        /// <param name="operation">The operation being performed (e.g., "Installing", "Installed", "Skipped")</param>
        /// <param name="source">The source file path</param>
        /// <param name="destination">The destination file path</param>
        /// <param name="success">Whether the operation succeeded</param>
        /// <param name="errorMessage">Optional error message if operation failed</param>
        public void WriteFileOperation(string operation, string source, string destination, bool success, [CanBeNull] string errorMessage = null)
        {
            if (success)
            {
                WriteInfo($"{operation}: {source} -> {destination}");
            }
            else
            {
                string errorMsg = string.IsNullOrEmpty(errorMessage)
                    ? $"Failed to {operation.ToLower()} {source} to {destination}"
                    : $"Failed to {operation.ToLower()} {source} to {destination}: {errorMessage}";
                WriteError(errorMsg);
            }
        }

        /// <summary>
        /// Writes patch operation details to the log.
        /// </summary>
        /// <param name="patchType">The type of patch (e.g., "2DA", "GFF", "TLK", "NSS", "NCS", "SSF")</param>
        /// <param name="filename">The filename being patched</param>
        /// <param name="description">Description of the patch operation</param>
        public void WritePatchOperation(string patchType, string filename, string description)
        {
            WriteInfo($"Patching {patchType}: {filename} - {description}");
        }

        /// <summary>
        /// Flushes the log file to ensure all data is written to disk.
        /// </summary>
        public void Flush()
        {
            lock (_lockObject)
            {
                _writer?.Flush();
            }
        }

        /// <summary>
        /// Disposes the InstallLogWriter and closes the log file.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                lock (_lockObject)
                {
                    _writer?.Flush();
                    _writer?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
