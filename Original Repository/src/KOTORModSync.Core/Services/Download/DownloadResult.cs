// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

namespace KOTORModSync.Core.Services.Download
{
    public sealed class DownloadResult
    {
        public bool Success { get; private set; }
        public string Message { get; private set; }
        public string FilePath { get; private set; }
        public bool WasSkipped { get; private set; }
        public DownloadSource DownloadSource { get; private set; }

        private DownloadResult(bool success, string message, string filePath, bool wasSkipped = false, DownloadSource downloadSource = DownloadSource.Direct)
        {
            Success = success;
            Message = message;
            FilePath = filePath;
            WasSkipped = wasSkipped;
            DownloadSource = downloadSource;
        }

        public static DownloadResult Succeeded(string filePath, string message, DownloadSource downloadSource = DownloadSource.Direct) =>
            new DownloadResult(success: true, message ?? string.Empty, filePath ?? string.Empty, wasSkipped: false, downloadSource);

        public static DownloadResult Succeeded(string filePath) =>
            new DownloadResult(success: true, string.Empty, filePath ?? string.Empty);

        public static DownloadResult Failed(string message) =>
            new DownloadResult(success: false, message ?? string.Empty, string.Empty);

        public static DownloadResult Skipped(string filePath, string message) =>
            new DownloadResult(success: true, message ?? string.Empty, filePath ?? string.Empty, wasSkipped: true);
    }
}
