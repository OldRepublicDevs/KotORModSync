// Copyright 2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Core.Services.Download
{
    /// <summary>
    /// Builds canonical network distribution payloads (descriptor + metadata) for a local file.
    /// </summary>
    public static class DownloadCacheDistributionBuilder
    {
        /// <summary>
        /// Builds a <see cref="DistributionPayload"/> for the specified source file.
        /// </summary>
        /// <param name="sourceFilePath">Absolute path to the payload file.</param>
        /// <param name="trackerUrls">Optional tracker URLs to embed in the descriptor.</param>
        /// <param name="pieceLength">
        /// Optional piece length override. If not supplied the canonical size is selected via
        /// <see cref="DownloadCacheOptimizer.DeterminePieceSize(long)"/>.
        /// </param>
        /// <param name="includeDescriptor">
        /// When false, omits building the outer descriptor document to minimize overhead
        /// (the computed <see cref="DistributionPayload.ContentId"/> remains available).
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public static async Task<DistributionPayload> BuildAsync(
            string sourceFilePath,
            IEnumerable<string> trackerUrls = null,
            int? pieceLength = null,
            bool includeDescriptor = true,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourceFilePath))
            {
                throw new ArgumentException("Source file path must be provided.", nameof(sourceFilePath));
            }

            var fileInfo = new FileInfo(sourceFilePath);
            if (!fileInfo.Exists)
            {
                throw new FileNotFoundException("Source file for distribution payload was not found.", sourceFilePath);
            }

            int resolvedPieceLength = pieceLength ?? DownloadCacheOptimizer.DeterminePieceSize(fileInfo.Length);
            if (resolvedPieceLength <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pieceLength), "Piece length must be positive.");
            }

            // Compute piece hashes (SHA-1) in canonical order.
            var pieceHashList = new List<byte[]>();
            byte[] piecesBuffer;
            using (FileStream readStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                piecesBuffer = await ComputePieceHashesAsync(readStream, resolvedPieceLength, pieceHashList, cancellationToken)
                    .ConfigureAwait(false);
            }

            // Compose canonical "info" dictionary.
            var infoDict = new SortedDictionary<string, object>(StringComparer.Ordinal)
            {
                ["length"] = fileInfo.Length,
                ["name"] = fileInfo.Name,
                ["piece length"] = resolvedPieceLength,
                ["pieces"] = piecesBuffer,
                ["private"] = 0,
            };

            byte[] infoBytes = CanonicalBencoding.BencodeCanonical(infoDict);
#if NET48
			byte[] infoHash;
			using ( var sha1 = SHA1.Create() )
			{
				infoHash = sha1.ComputeHash(infoBytes);
			}
#else
            byte[] infoHash = NetFrameworkCompatibility.HashDataSHA1(infoBytes);
#endif
            string contentId = BitConverter.ToString(infoHash).Replace("-", "").ToLowerInvariant();

            byte[] descriptorBytes = Array.Empty<byte>();
            IReadOnlyList<string> trackers = Array.Empty<string>();

            if (includeDescriptor)
            {
                (descriptorBytes, trackers) = BuildDescriptor(infoDict, trackerUrls);
            }

            return new DistributionPayload(
                contentId,
                fileInfo.Length,
                resolvedPieceLength,
                pieceHashList,
                infoBytes,
                descriptorBytes,
                trackers);
        }

        private static async Task<byte[]> ComputePieceHashesAsync(
            FileStream stream,
            int pieceLength,
            List<byte[]> pieceHashList,
            CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[pieceLength];
            using (var piecesWriter = new MemoryStream())
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

#if NET48
					int bytesRead = await stream.ReadAsync(buffer, 0, pieceLength);
#else
                    int bytesRead = await stream.ReadAsync(buffer, 0, pieceLength, cancellationToken).ConfigureAwait(false);
#endif
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    byte[] slice = new byte[bytesRead];
                    Buffer.BlockCopy(buffer, 0, slice, 0, bytesRead);
#if NET48
					using ( var sha1 = SHA1.Create() )
					{
						byte[] pieceHash = sha1.ComputeHash(slice);
						pieceHashList.Add(pieceHash);
						piecesWriter.Write(pieceHash, 0, pieceHash.Length);
					}
#else
                    byte[] pieceHash = NetFrameworkCompatibility.HashDataSHA1(slice);
                    pieceHashList.Add(pieceHash);
                    await piecesWriter.WriteAsync(pieceHash, 0, pieceHash.Length, cancellationToken).ConfigureAwait(false);
#endif
                }

                return piecesWriter.ToArray();
            }
        }

        private static (byte[] descriptorBytes, IReadOnlyList<string> trackers) BuildDescriptor(
            SortedDictionary<string, object> infoDict,
            IEnumerable<string> trackerUrls)
        {
            var descriptor = new SortedDictionary<string, object>(StringComparer.Ordinal)
            {
                ["info"] = infoDict,
                ["creation date"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ["created by"] = "KOTORModSync",
            };

            var distinctTrackers = trackerUrls?
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Select(u => u.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList() ?? new List<string>();

            if (distinctTrackers.Count > 0)
            {
                descriptor["announce"] = distinctTrackers[0];
                if (distinctTrackers.Count > 1)
                {
                    var announceList = new List<object>();
                    foreach (string tracker in distinctTrackers)
                    {
                        announceList.Add(new List<object> { tracker });
                    }
                    descriptor["announce-list"] = announceList;
                }
            }

            byte[] descriptorBytes = CanonicalBencoding.BencodeCanonical(descriptor);
            return (descriptorBytes, new ReadOnlyCollection<string>(distinctTrackers));
        }
    }

    /// <summary>
    /// Immutable payload describing the canonical network descriptor for a local file.
    /// </summary>
    public sealed class DistributionPayload
    {
        internal DistributionPayload(
            string contentId,
            long originalLength,
            int pieceLength,
            IReadOnlyList<byte[]> pieceHashes,
            byte[] infoBytes,
            byte[] descriptorBytes,
            IReadOnlyList<string> trackers)
        {
            ContentId = contentId;
            OriginalLength = originalLength;
            PieceLength = pieceLength;
            PieceHashes = new ReadOnlyCollection<byte[]>(pieceHashes.ToArray());
            InfoBytes = infoBytes ?? Array.Empty<byte>();
            DescriptorBytes = descriptorBytes ?? Array.Empty<byte>();
            Trackers = trackers ?? Array.Empty<string>();
        }

        public string ContentId { get; }

        public long OriginalLength { get; }

        public int PieceLength { get; }

        public IReadOnlyList<byte[]> PieceHashes { get; }

        public byte[] InfoBytes { get; }

        public byte[] DescriptorBytes { get; }

        public IReadOnlyList<string> Trackers { get; }

        public bool HasDescriptor => DescriptorBytes.Length > 0;

        public async Task WriteDescriptorAsync(string descriptorPath, CancellationToken cancellationToken = default)
        {
            if (!HasDescriptor)
            {
                throw new InvalidOperationException("Descriptor bytes were not generated for this payload.");
            }

            if (string.IsNullOrWhiteSpace(descriptorPath))
            {
                throw new ArgumentException("Descriptor path must be provided.", nameof(descriptorPath));
            }

            string directory = Path.GetDirectoryName(descriptorPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

#if NET48
			await Task.Run(() => File.WriteAllBytes(descriptorPath, DescriptorBytes), cancellationToken);
#else
            await NetFrameworkCompatibility.WriteAllBytesAsync(descriptorPath, DescriptorBytes, cancellationToken).ConfigureAwait(false);
#endif
        }
    }
}

