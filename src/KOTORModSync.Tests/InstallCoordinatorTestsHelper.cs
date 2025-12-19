// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using KOTORModSync.Core;
using KOTORModSync.Core.Installation;
using KOTORModSync.Core.Services;
using KOTORModSync.Tests.TestHelpers;

using LibGit2Sharp;

namespace KOTORModSync.Tests
{
    /// <summary>
    /// Helper class for test cleanup operations, extracted from InstallCoordinatorTests
    /// to allow reuse across multiple test classes.
    /// </summary>
    public static class InstallCoordinatorTestsHelper
    {
        /// <summary>
        /// Cleans up a test directory, handling Git file locks with retry logic.
        /// </summary>
        public static void CleanupTestDirectory(DirectoryInfo directory)
        {
            if (directory == null || !directory.Exists)
            {
                return;
            }

            // Clear session to dispose any Git repositories
            InstallCoordinator.ClearSessionForTests(directory);

            // Dispose any Git repositories explicitly
            DisposeGitRepositories(directory.FullName);

            // Force garbage collection to help release file handles
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);

            // Retry deletion with exponential backoff to handle Git file locks
            const int maxRetries = 15;
            const int baseDelayMs = 100;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    // On retries, try to clean up Git files more aggressively
                    if (attempt > 0)
                    {
                        CleanupGitFiles(directory.FullName, attempt);

                        // Additional GC after cleanup attempts
                        if (attempt % 3 == 0)
                        {
                            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
                            GC.WaitForPendingFinalizers();
                        }
                    }

                    Directory.Delete(directory.FullName, recursive: true);
                    return; // Success
                }
                catch (UnauthorizedAccessException) when (attempt < maxRetries - 1)
                {
                    // Exponential backoff: baseDelayMs * 2^attempt, capped at 2 seconds
                    int delayMs = Math.Min(baseDelayMs * (1 << attempt), 2000);
                    System.Threading.Thread.Sleep(delayMs);
                }
                catch (IOException) when (attempt < maxRetries - 1)
                {
                    // Exponential backoff: baseDelayMs * 2^attempt, capped at 2 seconds
                    int delayMs = Math.Min(baseDelayMs * (1 << attempt), 2000);
                    System.Threading.Thread.Sleep(delayMs);
                }
            }

            // If we still can't delete, log a warning but don't fail the test
            try
            {
                System.Diagnostics.Debug.WriteLine($"Warning: Could not delete test directory after {maxRetries} attempts: {directory.FullName}");
            }
            catch
            {
                // Ignore logging errors
            }
        }

        /// <summary>
        /// Disposes any Git repositories in the directory to release file handles.
        /// </summary>
        private static void DisposeGitRepositories(string directoryPath)
        {
            try
            {
                string checkpointDir = Path.Combine(directoryPath, ModComponent.CheckpointFolderName);
                if (!Directory.Exists(checkpointDir))
                {
                    return;
                }

                string gitDir = Path.Combine(checkpointDir, ".git");
                if (!Directory.Exists(gitDir))
                {
                    return;
                }

                // Try to dispose any open Repository instances
                // LibGit2Sharp repositories should be disposed to release file handles
                try
                {
                    using (var repo = new LibGit2Sharp.Repository(gitDir))
                    {
                        // Repository is disposed when leaving using block
                    }
                }
                catch
                {
                    // Repository may not be valid or already disposed - ignore
                }
            }
            catch
            {
                // Ignore errors during disposal attempt
            }
        }

        /// <summary>
        /// Aggressively cleans up Git files that might be locked.
        /// </summary>
        private static void CleanupGitFiles(string directoryPath, int attempt)
        {
            try
            {
                string checkpointDir = Path.Combine(directoryPath, ModComponent.CheckpointFolderName);
                if (!Directory.Exists(checkpointDir))
                {
                    return;
                }

                string gitDir = Path.Combine(checkpointDir, ".git");
                if (!Directory.Exists(gitDir))
                {
                    return;
                }

                // Remove read-only attributes from all files
                foreach (string file in Directory.GetFiles(gitDir, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                    }
                    catch
                    {
                        // Ignore individual file attribute errors
                    }
                }

                // On later attempts, try to delete individual files
                if (attempt >= 3)
                {
                    foreach (string file in Directory.GetFiles(gitDir, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch
                        {
                            // Ignore individual file deletion errors
                        }
                    }

                    // Try to delete empty directories
                    foreach (string dir in Directory.GetDirectories(gitDir, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
                    {
                        try
                        {
                            if (Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Length == 0)
                            {
                                Directory.Delete(dir, recursive: false);
                            }
                        }
                        catch
                        {
                            // Ignore directory deletion errors
                        }
                    }
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
