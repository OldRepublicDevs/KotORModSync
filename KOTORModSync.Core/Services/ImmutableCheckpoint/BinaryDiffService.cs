// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Octodiff.Core;
using Octodiff.Diagnostics;

namespace KOTORModSync.Core.Services.ImmutableCheckpoint
{
    public class BinaryDiffService
    {
        private const int MIN_FILE_SIZE_FOR_DIFF = 1024 * 100;
        private readonly ContentAddressableStore _casStore;

        public BinaryDiffService(ContentAddressableStore casStore)
        {
            _casStore = casStore ?? throw new ArgumentNullException(nameof(casStore));
        }

        public async Task<FileDelta> CreateBidirectionalDeltaAsync(
            string sourceFilePath,
            string targetFilePath,
            string relativePath,
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(sourceFilePath))
            {
                throw new FileNotFoundException($"Source file not found: {sourceFilePath}");
            }

            if (!File.Exists(targetFilePath))
            {
                throw new FileNotFoundException($"Target file not found: {targetFilePath}");
            }

            var sourceInfo = new FileInfo(sourceFilePath);
            var targetInfo = new FileInfo(targetFilePath);

            await Logger.LogVerboseAsync($"[BinaryDiff] Creating bidirectional delta for {relativePath}").ConfigureAwait(false);

            string sourceHash = await ComputeFileHashAsync(sourceFilePath, cancellationToken).ConfigureAwait(false);
            string targetHash = await ComputeFileHashAsync(targetFilePath, cancellationToken).ConfigureAwait(false);

            if (string.Equals(sourceHash, targetHash, StringComparison.Ordinal))
            {
                await Logger.LogVerboseAsync($"[BinaryDiff] Files identical, skipping delta for {relativePath}").ConfigureAwait(false);
                return null;
            }

            string sourceCASHash = await _casStore.StoreFileAsync(sourceFilePath).ConfigureAwait(false);
            string targetCASHash = await _casStore.StoreFileAsync(targetFilePath).ConfigureAwait(false);

            if (targetInfo.Length < MIN_FILE_SIZE_FOR_DIFF)
            {
                await Logger.LogVerboseAsync($"[BinaryDiff] File too small for delta, storing full file: {relativePath}").ConfigureAwait(false);
                return new FileDelta
                {
                    Path = relativePath,
                    SourceHash = sourceHash,
                    TargetHash = targetHash,
                    SourceCASHash = sourceCASHash,
                    TargetCASHash = targetCASHash,
                    SourceSize = sourceInfo.Length,
                    TargetSize = targetInfo.Length,
                    ForwardDeltaSize = targetInfo.Length,
                    ReverseDeltaSize = sourceInfo.Length,
                    Method = "full_copy",
                };
            }

            string forwardDeltaHash;
            long forwardDeltaSize;
            using (var forwardDeltaStream = new MemoryStream())
            {
                forwardDeltaSize = await CreateOctodiffDeltaAsync(
                    sourceFilePath,
                    targetFilePath,
                    forwardDeltaStream,
                    cancellationToken).ConfigureAwait(false);

                forwardDeltaHash = await _casStore.StoreStreamAsync(forwardDeltaStream).ConfigureAwait(false);
            }

            string reverseDeltaHash;
            long reverseDeltaSize;
            using (var reverseDeltaStream = new MemoryStream())
            {
                reverseDeltaSize = await CreateOctodiffDeltaAsync(
                    targetFilePath,
                    sourceFilePath,
                    reverseDeltaStream,
                    cancellationToken).ConfigureAwait(false);

                reverseDeltaHash = await _casStore.StoreStreamAsync(reverseDeltaStream).ConfigureAwait(false);
            }

            await Logger.LogAsync(
                $"[BinaryDiff] Created deltas for {relativePath}: " +
                $"Forward={forwardDeltaSize:N0} bytes, Reverse={reverseDeltaSize:N0} bytes " +
                $"(Original: {targetInfo.Length:N0} bytes, Saved: {targetInfo.Length - forwardDeltaSize:N0} bytes)"
            ).ConfigureAwait(false);

            return new FileDelta
            {
                Path = relativePath,
                SourceHash = sourceHash,
                TargetHash = targetHash,
                SourceCASHash = sourceCASHash,
                TargetCASHash = targetCASHash,
                ForwardDeltaCASHash = forwardDeltaHash,
                ReverseDeltaCASHash = reverseDeltaHash,
                SourceSize = sourceInfo.Length,
                TargetSize = targetInfo.Length,
                ForwardDeltaSize = forwardDeltaSize,
                ReverseDeltaSize = reverseDeltaSize,
                Method = "octodiff",
            };
        }

        public async Task ApplyForwardDeltaAsync(
            FileDelta delta,
            string outputFilePath,
            CancellationToken cancellationToken = default)
        {
            if (delta is null)
            {
                throw new ArgumentNullException(nameof(delta));
            }

            await Logger.LogVerboseAsync($"[BinaryDiff] Applying forward delta for {delta.Path}").ConfigureAwait(false);

            if (string.Equals(delta.Method, "full_copy", StringComparison.Ordinal))
            {
                await _casStore.RetrieveFileAsync(delta.TargetCASHash, outputFilePath).ConfigureAwait(false);
                return;
            }

            using (var sourceStream = _casStore.OpenReadStream(delta.SourceCASHash))
            using (var deltaStream = _casStore.OpenReadStream(delta.ForwardDeltaCASHash))
            {
                await ApplyOctodiffDeltaAsync(sourceStream, deltaStream, outputFilePath, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task ApplyReverseDeltaAsync(
            FileDelta delta,
            string outputFilePath,
            CancellationToken cancellationToken = default)
        {
            if (delta is null)
            {
                throw new ArgumentNullException(nameof(delta));
            }

            await Logger.LogVerboseAsync($"[BinaryDiff] Applying reverse delta for {delta.Path}").ConfigureAwait(false);

            if (string.Equals(delta.Method, "full_copy", StringComparison.Ordinal))
            {
                await _casStore.RetrieveFileAsync(delta.SourceCASHash, outputFilePath).ConfigureAwait(false);
                return;
            }

            using (var targetStream = _casStore.OpenReadStream(delta.TargetCASHash))
            using (var reverseDeltaStream = _casStore.OpenReadStream(delta.ReverseDeltaCASHash))
            {
                await ApplyOctodiffDeltaAsync(targetStream, reverseDeltaStream, outputFilePath, cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task<long> CreateOctodiffDeltaAsync(
            string basisFilePath,
            string newFilePath,
            Stream deltaOutputStream,
            CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                using (var basisStream = new FileStream(basisFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var signatureStream = new MemoryStream())
                {
                    var signatureBuilder = new SignatureBuilder();
                    signatureBuilder.Build(basisStream, new SignatureWriter(signatureStream));
                    signatureStream.Position = 0;

                    using (var newFileStream = new FileStream(newFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        var deltaBuilder = new DeltaBuilder();
                        deltaBuilder.BuildDelta(
                            newFileStream,
                            new SignatureReader(signatureStream, new NullProgressReporter()),
                            new AggregateCopyOperationsDecorator(new BinaryDeltaWriter(deltaOutputStream))
                        );
                    }
                }
            }, cancellationToken).ConfigureAwait(false);

            return deltaOutputStream.Length;
        }

        private static async Task ApplyOctodiffDeltaAsync(
            Stream basisStream,
            Stream deltaStream,
            string outputFilePath,
            CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                string outputDir = Path.GetDirectoryName(outputFilePath);
                if (!string.IsNullOrEmpty(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                using (var outputStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var deltaApplier = new DeltaApplier();
                    deltaApplier.Apply(
                        basisStream,
                        new BinaryDeltaReader(deltaStream, new NullProgressReporter()),
                        outputStream
                    );
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return await ContentAddressableStore.ComputeFileHashAsync(filePath, cancellationToken).ConfigureAwait(false);
        }

        private sealed class NullProgressReporter : IProgressReporter
        {
            public void ReportProgress(string operation, long currentPosition, long total)
            {
            }
        }
    }
}
