// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace KOTORModSync.Core.Services.Download
{
    public static class DownloadHelper
    {
        private const int BufferSize = 8192;
        private const int ProgressUpdateIntervalMs = 250;

        /// <summary>
        /// Generates a temporary filename for a download to prevent corruption of existing files.
        /// Format: originalName.random.tmp
        /// </summary>
        public static string GetTempFilePath(string destinationPath)
        {
            string directory = Path.GetDirectoryName(destinationPath);
            string fileName = Path.GetFileName(destinationPath);
            string extension = Path.GetExtension(fileName);
            string nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

            // Use a GUID and take the first 8 characters for uniqueness
            string randomChars = Guid.NewGuid().ToString("N").Substring(0, 8);

            string tempFileName = $"{nameWithoutExtension}.{extension}.{randomChars}.tmp";
            return Path.Combine(directory, tempFileName);
        }

        /// <summary>
        /// Atomically moves a temporary download file to its final destination.
        /// </summary>
        public static void MoveToFinalDestination(string tempPath, string finalPath)
        {
            if (!File.Exists(tempPath))
            {
                throw new FileNotFoundException($"Temporary download file not found: {tempPath}");
            }

            // Platform-specific atomic move
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                // Windows: File.Replace is atomic
                if (File.Exists(finalPath))
                {
                    string backup = finalPath + ".bak";
                    File.Replace(tempPath, finalPath, backup);
                    if (File.Exists(backup))
                    {
                        File.Delete(backup);
                    }
                }
                else
                {
                    File.Move(tempPath, finalPath);
                }
            }
            else
            {
                // POSIX: File.Move with overwrite
                if (File.Exists(finalPath))
                {
                    File.Delete(finalPath);
                }
                File.Move(tempPath, finalPath);
            }
        }

        public static async Task<long> DownloadWithProgressAsync(
            Stream sourceStream,
            string destinationPath,
            long totalBytes,
            string fileName,
            string url,
            IProgress<DownloadProgress> progress = null,
            string modName = null,
            CancellationToken cancellationToken = default)
        {

            DateTime startTime = DateTime.Now;

            try
            {
                using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
                {
                    byte[] buffer = new byte[BufferSize];
                    int bytesRead;
                    long totalBytesRead = 0;
                    DateTimeOffset lastProgressUpdate = DateTimeOffset.UtcNow;

                    while (
                        (bytesRead = await sourceStream.ReadAsync(
                            buffer,
                            0,
                            buffer.Length,
                            cancellationToken).ConfigureAwait(false)) > 0
                        && !cancellationToken.IsCancellationRequested
                    )
                    {

                        cancellationToken.ThrowIfCancellationRequested();
                        await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                        totalBytesRead += bytesRead;

                        DateTimeOffset now = DateTimeOffset.UtcNow;
                        if ((now - lastProgressUpdate).TotalMilliseconds >= ProgressUpdateIntervalMs)
                        {
                            lastProgressUpdate = now;

                            double progressPercentage = totalBytes > 0
                                ? (double)totalBytesRead / totalBytes * 100.0
                                : 0;

                            progress?.Report(new DownloadProgress
                            {
                                ModName = modName,
                                Url = url,
                                Status = DownloadStatus.InProgress,
                                StatusMessage = totalBytes > 0
                                    ? $"Downloading {fileName}... ({totalBytesRead:N0} / {totalBytes:N0} bytes)"
                                    : $"Downloading {fileName}... ({totalBytesRead:N0} bytes)",
                                ProgressPercentage = totalBytes > 0 ? Math.Min(progressPercentage, 100) : 0,
                                BytesDownloaded = totalBytesRead,
                                TotalBytes = totalBytes,
                                StartTime = startTime,
                                FilePath = destinationPath,
                            });

                        }
                    }

                    await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);

                    if (totalBytes > 0)
                    {
                        progress?.Report(new DownloadProgress
                        {
                            ModName = modName,
                            Url = url,
                            Status = DownloadStatus.InProgress,
                            StatusMessage = $"Download complete: {fileName}",
                            ProgressPercentage = 100,
                            BytesDownloaded = totalBytesRead,
                            TotalBytes = totalBytes,
                            StartTime = startTime,
                            FilePath = destinationPath,
                        });
                    }

                    return totalBytesRead;
                }
            }
            catch (OperationCanceledException)
            {
                // Clean up partial file on cancellation
                if (File.Exists(destinationPath))
                {
                    try
                    {
                        File.Delete(destinationPath);
                        await Logger.LogVerboseAsync($"[DownloadHelper] Cleaned up cancelled download: {destinationPath}").ConfigureAwait(false);
                    }
                    catch (Exception deleteEx)
                    {
                        await Logger.LogWarningAsync($"[DownloadHelper] Failed to delete partial file: {deleteEx.Message}").ConfigureAwait(false);
                    }
                }
                throw;
            }
        }
    }
}
