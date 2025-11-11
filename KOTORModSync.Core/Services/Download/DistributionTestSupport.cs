// Copyright 2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Core.Services.Download
{
    /// <summary>
    /// Provides reusable helpers to prepare deterministic resources for network distribution integration testing.
    /// </summary>
    public static class DistributionTestSupport
    {
        /// <summary>
        /// Ensures a deterministic file exists at the supplied location, creating the directory tree when needed.
        /// </summary>
        /// <param name="baseDirectory">Root directory that should contain the generated file.</param>
        /// <param name="relativePath">Relative path from the base directory to the file.</param>
        /// <param name="sizeBytes">Desired size in bytes. Ignored when <paramref name="content"/> is non-null.</param>
        /// <param name="content">Optional explicit content to write to the file.</param>
        /// <param name="randomSeed">Optional seed for deterministic pseudo-random data generation.</param>
        /// <returns>The absolute path to the generated file.</returns>
        public static string EnsureTestFile(
            string baseDirectory,
            string relativePath,
            long sizeBytes,
            string content = null,
            int randomSeed = 42)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                throw new ArgumentException("Base directory must be supplied.", nameof(baseDirectory));
            }

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new ArgumentException("Relative path must be supplied.", nameof(relativePath));
            }

            string fullPath = Path.Combine(baseDirectory, relativePath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (content != null)
            {
                File.WriteAllText(fullPath, content);
                return fullPath;
            }

            using (FileStream stream = File.Create(fullPath))
            {
                var random = new Random(randomSeed);
                byte[] buffer = new byte[8192];
                long remaining = sizeBytes;

                while (remaining > 0)
                {
                    int toWrite = (int)Math.Min(remaining, buffer.Length);
                    random.NextBytes(buffer);
                    stream.Write(buffer, 0, toWrite);
                    remaining -= toWrite;
                }
            }

            return fullPath;
        }

        /// <summary>
        /// Writes explicit binary content to a location relative to <paramref name="baseDirectory"/>, creating directories when required.
        /// </summary>
        /// <param name="baseDirectory">Root directory that should contain the generated file.</param>
        /// <param name="relativePath">Relative path from the base directory to the file.</param>
        /// <param name="data">Binary content to persist to disk. Must not be null.</param>
        /// <returns>The absolute path to the generated file.</returns>
        public static string EnsureBinaryTestFile(
            string baseDirectory,
            string relativePath,
            byte[] data)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                throw new ArgumentException("Base directory must be supplied.", nameof(baseDirectory));
            }

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new ArgumentException("Relative path must be supplied.", nameof(relativePath));
            }

            if (data is null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            string fullPath = Path.Combine(baseDirectory, relativePath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(fullPath, data);
            return fullPath;
        }

        /// <summary>
        /// Ensures a copy of an existing file resides in the specified directory, creating directories when necessary.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the existing source file.</param>
        /// <param name="destinationDirectory">Directory where the copy should be placed.</param>
        /// <param name="destinationFileName">Optional destination filename. If omitted, the source filename is used.</param>
        /// <param name="overwrite">Whether to overwrite an existing file with the same name.</param>
        /// <returns>The absolute path to the copied file.</returns>
        public static string EnsureFileCopy(
            string sourcePath,
            string destinationDirectory,
            string destinationFileName = null,
            bool overwrite = true)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("Source path must be supplied.", nameof(sourcePath));
            }

            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException("Source file for copy operation was not found.", sourcePath);
            }

            if (string.IsNullOrWhiteSpace(destinationDirectory))
            {
                throw new ArgumentException("Destination directory must be supplied.", nameof(destinationDirectory));
            }

            if (!Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            string fileName = string.IsNullOrWhiteSpace(destinationFileName)
                ? Path.GetFileName(sourcePath)
                : destinationFileName;

            string destinationPath = Path.Combine(destinationDirectory, fileName);
            File.Copy(sourcePath, destinationPath, overwrite);
            return destinationPath;
        }

        /// <summary>
        /// Checks whether a file exists at the specified path.
        /// </summary>
        /// <param name="path">Absolute or relative path to the file.</param>
        /// <returns><c>true</c> if the file exists; otherwise, <c>false</c>.</returns>
        public static bool FileExists(string path)
        {
            return File.Exists(path);
        }

        /// <summary>
        /// Allows controlled modification of an existing file by providing a writable stream to a caller-supplied delegate.
        /// </summary>
        /// <param name="path">Absolute path to the file that should be modified.</param>
        /// <param name="modifier">Action that performs the modification using the provided stream.</param>
        public static void ModifyFile(string path, Action<FileStream> modifier)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path must be provided.", nameof(path));
            }

            if (modifier is null)
            {
                throw new ArgumentNullException(nameof(modifier));
            }

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                modifier(stream);
            }
        }

        /// <summary>
        /// Generates a network descriptor for a file using the canonical distribution pipeline.
        /// </summary>
        /// <param name="sourceFilePath">Absolute path to the source file.</param>
        /// <param name="outputDirectory">Directory where the descriptor should be created.</param>
        /// <param name="descriptorName">Descriptor filename without extension.</param>
        /// <param name="trackers">Optional tracker endpoints to embed.</param>
        /// <param name="pieceLength">Optional piece length override.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Descriptor path and the payload used to generate it.</returns>
        public static async Task<(string descriptorPath, DistributionPayload payload)> CreateDescriptorAsync(
            string sourceFilePath,
            string outputDirectory,
            string descriptorName,
            IEnumerable<string> trackers = null,
            int? pieceLength = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourceFilePath))
            {
                throw new ArgumentException("Source file must be supplied.", nameof(sourceFilePath));
            }

            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new ArgumentException("Output directory must be supplied.", nameof(outputDirectory));
            }

            if (string.IsNullOrWhiteSpace(descriptorName))
            {
                throw new ArgumentException("Descriptor name must be supplied.", nameof(descriptorName));
            }

            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            DistributionPayload payload = await DownloadCacheDistributionBuilder.BuildAsync(
                sourceFilePath,
                trackers,
                pieceLength,
                includeDescriptor: true,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            string descriptorPath = Path.Combine(outputDirectory, $"{descriptorName}.torrent");
            await payload.WriteDescriptorAsync(descriptorPath, cancellationToken).ConfigureAwait(false);
            return (descriptorPath, payload);
        }
    }
}

