// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

using NUnit.Framework;

namespace KOTORModSync.Tests
{
    /// <summary>
    /// Base class for parameterized tests that provides configurable logging and test directory management.
    /// Child classes can specify their logging and directory needs through abstract methods.
    /// </summary>
    public abstract class BaseParameterizedTest
    {
        private static readonly List<string> _allTestDirectories = new List<string>();
        private static readonly object _lockObject = new object();
        protected string LogFilePath { get; private set; }

        /// <summary>
        /// Override to specify the test category/type for logging purposes
        /// </summary>
        protected abstract string TestCategory { get; }

        /// <summary>
        /// Override to specify whether to create a temp directory for this test type
        /// </summary>
        protected virtual bool RequiresTempDirectory => false;

        /// <summary>
        /// Override to specify whether to preserve test results after completion
        /// </summary>
        protected virtual bool PreserveTestResults => true;

        [OneTimeTearDown]
        public static void GlobalTearDown()
        {
            TestOutputHelper.WriteLine($"");
            TestOutputHelper.WriteLine($"=== FINAL TEST DIRECTORY SUMMARY ===");
            TestOutputHelper.WriteLine($"Total test directories created: {_allTestDirectories.Count}");
            TestOutputHelper.WriteLine($"NOTE: All directories are created in your KOTORModSync.Tests folder in the codebase!");
            lock (_lockObject)
            {
                var uniqueDirectories = _allTestDirectories.Distinct(StringComparer.Ordinal).ToList();
                foreach (string dir in uniqueDirectories)
                {
                    TestOutputHelper.WriteLine($"TEST DIRECTORY: {dir}");
                }
            }
            TestOutputHelper.WriteLine($"=== END FINAL TEST DIRECTORY SUMMARY ===");
        }
        protected StreamWriter LogWriter { get; private set; }
        protected string TestFileDirectory { get; private set; }
        protected string TestTempDirectory { get; private set; }

        [SetUp]
        public virtual void SetUp()
        {
            // Get the test method name and class name
            TestContext testContext = TestContext.CurrentContext;
            string testName = testContext.Test.Name;
            string className = GetType().Name;

            // Use the current directory where the executable is located
            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            string currentDir = Path.GetDirectoryName(assemblyPath) ?? "";

            // Use the current directory as the test file directory
            TestFileDirectory = currentDir;

            // Create temp directory only if required by the test type
            if (RequiresTempDirectory)
            {
                TestTempDirectory = Path.Combine(TestFileDirectory, $"test_temp_{TestCategory}");
                Directory.CreateDirectory(TestTempDirectory);
                WriteLog($"Test temp directory created: {TestTempDirectory}");
            }

            // Track this directory globally for final summary
            lock (_lockObject)
            {
                if (TestTempDirectory != null)
                {
                    _allTestDirectories.Add(TestTempDirectory);
                }

                if (TestFileDirectory != null)
                {
                    _allTestDirectories.Add(TestFileDirectory);
                }
            }

            // Create hierarchical directory structure for better organization
            string logDir = GetLogDirectoryPath(testName);
            Directory.CreateDirectory(logDir);

            // Create timestamp for unique log file names
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string fileName = $"{GetLogFileName(testName, timestamp)}";

            LogFilePath = Path.Combine(logDir, fileName);
            LogWriter = new StreamWriter(LogFilePath);

            WriteLog($"=== {TestCategory} Test Started ===");
            WriteLog($"Test: {testName}");
            WriteLog($"Class: {className}");
            WriteLog($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            WriteLog($"Log File: {LogFilePath}");
            WriteLog($"Test File: {testContext.Test.FullName}");
            WriteLog($"");

            // CRITICAL: Output test directory info to console at start
            TestOutputHelper.WriteLine($"=== {TestCategory.ToUpperInvariant()} TEST DIRECTORY INFO ===");
            TestOutputHelper.WriteLine($"Test File Directory (in codebase): {TestFileDirectory}");
            if (TestTempDirectory != null)
            {
                TestOutputHelper.WriteLine($"Test Temp Directory (in codebase): {TestTempDirectory}");
            }

            TestOutputHelper.WriteLine($"Log File (in codebase): {LogFilePath}");
            TestOutputHelper.WriteLine($"NOTE: These directories are created in your KOTORModSync.Tests folder!");
            TestOutputHelper.WriteLine($"=== END {TestCategory.ToUpperInvariant()} TEST DIRECTORY INFO ===");
        }

        [TearDown]
        public virtual void TearDown()
        {
            WriteLog($"");
            WriteLog($"=== {TestCategory} Test Completed ===");
            WriteLog($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            WriteLog($"Log file preserved: {LogFilePath}");

            // Preserve test results only if configured to do so
            if (PreserveTestResults)
            {
                PreserveTestResultsFiles();
            }

            // CRITICAL: Log absolute paths at the end of EVERY test
            WriteLog($"");
            WriteLog($"=== CRITICAL PATHS FOR DEBUGGING ===");
            WriteLog($"ABSOLUTE TEST FILE DIRECTORY: {TestFileDirectory}");
            if (TestTempDirectory != null)
            {
                WriteLog($"ABSOLUTE TEST TEMP DIRECTORY: {TestTempDirectory}");
            }

            WriteLog($"ABSOLUTE LOG FILE PATH: {LogFilePath}");
            WriteLog($"=== END CRITICAL PATHS ===");

            // CRITICAL: Also output to console so it's visible during test execution
            TestOutputHelper.WriteLine($"");
            TestOutputHelper.WriteLine($"=== CRITICAL PATHS FOR DEBUGGING ===");
            TestOutputHelper.WriteLine($"ABSOLUTE TEST FILE DIRECTORY: {TestFileDirectory}");
            if (TestTempDirectory != null)
            {
                TestOutputHelper.WriteLine($"ABSOLUTE TEST TEMP DIRECTORY: {TestTempDirectory}");
            }

            TestOutputHelper.WriteLine($"ABSOLUTE LOG FILE PATH: {LogFilePath}");
            TestOutputHelper.WriteLine($"=== END CRITICAL PATHS ===");

            LogWriter?.Flush();
            LogWriter?.Close();
            LogWriter?.Dispose();
            LogWriter = null;
        }

        /// <summary>
        /// Preserves all test results, debug files, and generated content for analysis.
        /// This ensures nothing relevant to test results is lost during teardown.
        /// Only preserves if the test type is configured to preserve results.
        /// </summary>
        private void PreserveTestResultsFiles()
        {
            try
            {
                // Always preserve the test file directory contents
                if (TestFileDirectory != null && Directory.Exists(TestFileDirectory))
                {
                    var testDirFiles = Directory.GetFiles(TestFileDirectory, "*", SearchOption.TopDirectoryOnly)
                        .Where(f => Path.GetFileName(f).Contains(GetType().Name, StringComparison.Ordinal) ||
                                    Path.GetFileName(f).Contains("diff_", StringComparison.Ordinal) ||
                                    Path.GetFileName(f).Contains("debug_", StringComparison.Ordinal) ||
                                    Path.GetFileName(f).Contains("generated_", StringComparison.Ordinal) ||
                                    Path.GetFileName(f).Contains("regenerated_", StringComparison.Ordinal) ||
                                    Path.GetFileName(f).Contains("from_markdown_", StringComparison.Ordinal) ||
                                    Path.GetFileName(f).Contains("from_toml_", StringComparison.Ordinal) ||
                                    string.Equals(Path.GetExtension(f), ".log", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(Path.GetExtension(f), ".toml", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(Path.GetExtension(f), ".md", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (testDirFiles.Count > 0)
                    {
                        WriteLog($"Preserving {testDirFiles.Count} result files in test directory:");
                        foreach (string file in testDirFiles)
                        {
                            WriteLog($"  - {Path.GetFileName(file)}");
                        }
                    }
                }

                // Always preserve the temp directory contents (contains generated TOML, debug files, etc.)
                if (TestTempDirectory != null && Directory.Exists(TestTempDirectory))
                {
                    string[] tempFiles = Directory.GetFiles(TestTempDirectory, "*", SearchOption.AllDirectories);
                    if (tempFiles.Length > 0)
                    {
                        WriteLog($"Preserving {tempFiles.Length} debug files in temp directory: {TestTempDirectory}");
                        foreach (string file in tempFiles)
                        {
                            string relativePath = Path.GetRelativePath(TestTempDirectory, file);
                            WriteLog($"  - {relativePath}");
                        }
                    }
                    else
                    {
                        WriteLog($"Test temp directory is empty: {TestTempDirectory}");
                        // Don't delete empty directories - preserve them for debugging
                    }
                }

                // Log summary of what was preserved
                WriteLog($"=== PRESERVATION SUMMARY ===");
                WriteLog($"Log file: {LogFilePath}");
                if (TestTempDirectory != null)
                {
                    WriteLog($"Debug directory: {TestTempDirectory}");
                }
                WriteLog($"Test file directory: {TestFileDirectory}");
                WriteLog($"All test results preserved for analysis");
            }
            catch (Exception ex)
            {
                WriteLogException(ex, "test results preservation");
            }
        }

        /// <summary>
        /// Writes a message to the log file with timestamp.
        /// </summary>
        /// <param name="message">The message to log</param>
        protected void WriteLog(string message)
        {
            if (LogWriter != null)
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
                LogWriter.WriteLine($"[{timestamp}] {message}");
                LogWriter.Flush();
            }
        }

        /// <summary>
        /// Writes a message to both the log file and console.
        /// Uses TestOutputHelper for maximum real-time output visibility during test execution.
        /// </summary>
        /// <param name="message">The message to log</param>
        protected void WriteLogAndConsole(string message)
        {
            WriteLog(message);
            // Use TestOutputHelper for maximum real-time output visibility
            TestOutputHelper.WriteLine(message);
        }

        /// <summary>
        /// Writes an exception to the log file with full details.
        /// </summary>
        /// <param name="ex">The exception to log</param>
        /// <param name="context">Optional context about where the exception occurred</param>
        protected void WriteLogException(Exception ex, string context = "")
        {
            WriteLog($"EXCEPTION{(context != null ? $" in {context}" : "")}: {ex.GetType().Name}");
            WriteLog($"Message: {ex.Message}");
            WriteLog($"Stack Trace:");
            WriteLog(ex.StackTrace ?? "No stack trace available");

            if (ex.InnerException != null)
            {
                WriteLog($"Inner Exception: {ex.InnerException.GetType().Name}");
                WriteLog($"Inner Message: {ex.InnerException.Message}");
            }
        }

        /// <summary>
        /// Gets a debug file path in the test file directory with a unique name.
        /// </summary>
        /// <param name="fileName">The base file name</param>
        /// <returns>Full path to the debug file</returns>
        protected string GetDebugFilePath(string fileName)
        {
            if (TestFileDirectory == null)
            {
                throw new InvalidOperationException("TestFileDirectory is not initialized");
            }

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff", CultureInfo.InvariantCulture);
            string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);
            string uniqueFileName = $"{nameWithoutExt}_{timestamp}{extension}";

            return Path.Combine(TestFileDirectory, uniqueFileName);
        }

        /// <summary>
        /// Gets a debug file path in the test temp directory with a unique name.
        /// </summary>
        /// <param name="fileName">The base file name</param>
        /// <returns>Full path to the debug file</returns>
        protected string GetTempDebugFilePath(string fileName)
        {
            if (TestTempDirectory == null)
            {
                throw new InvalidOperationException("TestTempDirectory is not initialized");
            }

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff", CultureInfo.InvariantCulture);
            string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);
            string uniqueFileName = $"{nameWithoutExt}_{timestamp}{extension}";

            return Path.Combine(TestTempDirectory, uniqueFileName);
        }

        /// <summary>
        /// Marks a file as important for test results preservation.
        /// Call this method for any file that should be preserved during teardown.
        /// </summary>
        /// <param name="filePath">The path to the file to preserve</param>
        /// <param name="description">Description of what this file contains</param>
        protected void MarkFileForPreservation(string filePath, string description)
        {
            if (File.Exists(filePath))
            {
                WriteLog($"Marking file for preservation: {Path.GetFileName(filePath)} - {description}");
            }
        }

        /// <summary>
        /// Creates a hierarchical directory path for organizing log files by category, class, game type, mod category, and mod name.
        /// The structure is derived from the test name patterns used by the parameterized test classes.
        /// </summary>
        protected virtual string GetLogDirectoryPath(string testName)
        {
            TestNameInfo testInfo = ParseTestName(testName);

            // Ensure TestFileDirectory is not null
            string baseDir = TestFileDirectory ?? "";

            // Create hierarchical path: TestCategory/ClassName/GameType/ModCategory/ModName/
            string logDir = Path.Combine(baseDir, "logs", TestCategory, GetType().Name, testInfo.GameType, testInfo.ModCategory, testInfo.ModName);
            return logDir;
        }

        /// <summary>
        /// Parses a test name to extract structured information about the test.
        /// This handles the naming patterns used by parameterized test classes.
        /// </summary>
        protected virtual TestNameInfo ParseTestName(string testName)
        {
            var info = new TestNameInfo();
            string workingName = testName;

            // Extract game type (K1, K2, etc.) from test name prefix
            if (workingName.StartsWith("K1_", StringComparison.OrdinalIgnoreCase))
            {
                info.GameType = "K1";
                workingName = workingName.Substring(3);
            }
            else if (workingName.StartsWith("K2_", StringComparison.OrdinalIgnoreCase))
            {
                info.GameType = "K2";
                workingName = workingName.Substring(3);
            }
            else
            {
                info.GameType = "Unknown";
            }

            // Extract mod category from the test name based on common patterns
            // These patterns come from the markdown file names in mod-builds/content/
            info.ModCategory = ExtractModCategory(workingName);

            // Clean the working name by removing the category pattern
            workingName = RemoveModCategoryFromName(workingName, info.ModCategory);

            // The remaining name is the mod name (could be a component name for individual tests)
            info.ModName = workingName;

            return info;
        }

        /// <summary>
        /// Extracts the mod category from a test name based on common patterns found in markdown file names.
        /// </summary>
        private static string ExtractModCategory(string testName)
        {
            // Check for compound categories first (more specific patterns)
            if (testName.Contains("_spoiler-free_mobile_", StringComparison.OrdinalIgnoreCase))
            {
                return "spoiler-free_mobile";
            }

            if (testName.Contains("_spoiler-free_", StringComparison.OrdinalIgnoreCase))
            {
                return "spoiler-free";
            }

            if (testName.Contains("_full_mobile_", StringComparison.OrdinalIgnoreCase))
            {
                return "full_mobile";
            }

            if (testName.Contains("_full_", StringComparison.OrdinalIgnoreCase))
            {
                return "full";
            }

            if (testName.Contains("_mobile_", StringComparison.OrdinalIgnoreCase))
            {
                return "mobile";
            }

            if (testName.Contains("_delete_", StringComparison.OrdinalIgnoreCase))
            {
                return "delete";
            }

            if (testName.Contains("_android_", StringComparison.OrdinalIgnoreCase))
            {
                return "android";
            }

            if (testName.Contains("_ios_", StringComparison.OrdinalIgnoreCase))
            {
                return "ios";
            }

            if (testName.Contains("_widescreen_", StringComparison.OrdinalIgnoreCase))
            {
                return "widescreen";
            }

            return "general";
        }

        /// <summary>
        /// Removes the mod category pattern from the test name to get the clean mod name.
        /// </summary>
        private static string RemoveModCategoryFromName(string testName, string modCategory)
        {
            if (string.Equals(modCategory, "general", StringComparison.Ordinal))
            {
                return testName;
            }

            // Replace the category pattern with underscore
            string pattern = $"_{modCategory}_";
            return testName.Replace(pattern, "_", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Contains structured information about a parsed test name.
        /// </summary>
        protected class TestNameInfo
        {
            public string GameType { get; set; } = "Unknown";
            public string ModCategory { get; set; } = "general";
            public string ModName { get; set; } = "";
        }

        /// <summary>
        /// Creates a clean log filename with timestamp.
        /// </summary>
        protected virtual string GetLogFileName(string testName, string timestamp)
        {
            TestNameInfo testInfo = ParseTestName(testName);

            // Use the parsed mod name for a cleaner filename
            string fileName = $"{testInfo.ModName}_{timestamp}.log";

            // Sanitize filename by removing invalid characters
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(invalidChar, '_');
            }

            return fileName;
        }
    }
}
