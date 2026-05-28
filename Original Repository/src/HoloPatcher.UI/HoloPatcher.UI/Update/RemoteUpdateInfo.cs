using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HoloPatcher.UI.Update
{
    /// <summary>
    /// Strongly typed representation of the JSON payload embedded in Config.cs.
    /// Mirrors the structure produced by holopatcher/config.py.
    /// </summary>
    public sealed class RemoteUpdateInfo
    {
        [JsonPropertyName("holopatcherLatestVersion")]
        public string LatestVersion { get; set; } = string.Empty;

        [JsonPropertyName("holopatcherLatestNotes")]
        public string LatestReleaseNotes { get; set; } = string.Empty;

        [JsonPropertyName("holopatcherDownloadLink")]
        public string ReleaseDownloadPage { get; set; } = string.Empty;

        [JsonPropertyName("holopatcherDirectLinks")]
        public Dictionary<string, Dictionary<string, List<string>>> ReleaseDirectLinks { get; set; }
            = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.OrdinalIgnoreCase);

        [JsonPropertyName("holopatcherLatestBetaVersion")]
        public string LatestBetaVersion { get; set; } = string.Empty;

        [JsonPropertyName("holopatcherLatestBetaNotes")]
        public string LatestBetaNotes { get; set; } = string.Empty;

        [JsonPropertyName("holopatcherBetaDownloadLink")]
        public string BetaDownloadPage { get; set; } = string.Empty;

        [JsonPropertyName("holopatcherBetaDirectLinks")]
        public Dictionary<string, Dictionary<string, List<string>>> BetaDirectLinks { get; set; }
            = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Returns the set of mirrors for the current platform/architecture.
        /// </summary>
        public IReadOnlyList<string> GetPlatformMirrors(bool useBetaChannel)
        {
            string platformKey = DetectPlatformKey();
            string archKey = DetectArchitectureKey();
            Dictionary<string, Dictionary<string, List<string>>> table = useBetaChannel ? BetaDirectLinks : ReleaseDirectLinks;

            if (table is null ||
                !table.TryGetValue(platformKey, out Dictionary<string, List<string>> architectures) ||
                architectures is null ||
                !architectures.TryGetValue(archKey, out List<string> mirrors) ||
                mirrors is null ||
                mirrors.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No direct download links were provided for {platformKey}/{archKey}.");
            }

            return mirrors;
        }

        /// <summary>
        /// Gets the latest version string for the selected channel.
        /// </summary>
        public string GetChannelVersion(bool useBetaChannel)
        {
            string version = useBetaChannel ? LatestBetaVersion : LatestVersion;
            if (string.IsNullOrWhiteSpace(version))
            {
                version = LatestVersion;
            }
            return version;
        }

        /// <summary>
        /// Gets the release notes associated with the selected channel.
        /// </summary>
        public string GetChannelNotes(bool useBetaChannel)
        {
            string notes = useBetaChannel ? LatestBetaNotes : LatestReleaseNotes;
            if (string.IsNullOrWhiteSpace(notes))
            {
                return string.Empty;
            }
            return notes;
        }

        /// <summary>
        /// Gets the fallback download page for manual downloads.
        /// </summary>
        public string GetDownloadPage(bool useBetaChannel)
        {
            string url = useBetaChannel ? BetaDownloadPage : ReleaseDownloadPage;
            if (string.IsNullOrWhiteSpace(url))
            {
                url = ReleaseDownloadPage;
            }
            return url;
        }

        private static string DetectPlatformKey()
        {
            if (OperatingSystem.IsWindows())
            {
                return "Windows";
            }

            if (OperatingSystem.IsLinux())
            {
                return "Linux";
            }

            if (OperatingSystem.IsMacOS() || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "Darwin";
            }

            throw new PlatformNotSupportedException("Unsupported operating system for automatic updates.");
        }

        private static string DetectArchitectureKey()
        {
            if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64 || RuntimeInformation.ProcessArchitecture == Architecture.X64)
            {
                return "64bit";
            }

            return "32bit";
        }
    }

    internal static class RemoteUpdateInfoParser
    {
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        public static RemoteUpdateInfo FromDictionary(IReadOnlyDictionary<string, object> raw)
        {
            if (raw is null || raw.Count == 0)
            {
                throw new InvalidOperationException("Remote update payload was empty.");
            }

            // If the dictionary already contains the typed payload we can shortcut.
            if (raw.TryGetValue("__typed", out object typed) && typed is RemoteUpdateInfo cached)
            {
                return cached;
            }

            // Serialize and rehydrate into the strongly typed model to ensure consistent parsing
            // regardless of whether the dictionary contains JsonElements or CLR objects.
            string json = JsonSerializer.Serialize(raw, SerializerOptions);
            RemoteUpdateInfo info = JsonSerializer.Deserialize<RemoteUpdateInfo>(json, SerializerOptions);
            if (info is null)
            {
                throw new InvalidOperationException("Failed to parse remote update payload.");
            }

            return info;
        }
    }
}

