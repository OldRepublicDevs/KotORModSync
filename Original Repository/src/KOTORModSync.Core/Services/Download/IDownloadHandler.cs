// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KOTORModSync.Core.Services.Download
{
    public interface IDownloadHandler
    {
        bool CanHandle(string url);

        Task<List<string>> ResolveFilenamesAsync(string url, CancellationToken cancellationToken = default);

        Task<DownloadResult> DownloadAsync(
            string url,
            string destinationDirectory,
            IProgress<DownloadProgress> progress = null,
            List<string> targetFilenames = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves provider-specific metadata for content identification.
        /// Metadata should be normalized before returning (use NormalizeMetadata).
        /// </summary>
        Task<Dictionary<string, object>> GetFileMetadataAsync(string url, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the provider key for this handler (e.g., "deadlystream", "mega", "nexus", "direct").
        /// </summary>
        string GetProviderKey();
    }
}
