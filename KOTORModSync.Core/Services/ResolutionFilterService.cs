// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Core.Services
{

    public sealed partial class ResolutionFilterService
    {
        private static readonly Regex s_resolutionPattern = new Regex(@"(\d{3,4})x(\d{3,4})", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(5));
        private readonly Resolution _systemResolution;
        private readonly bool _filterEnabled;

        public ResolutionFilterService(bool enableFiltering = true)
        {
            _filterEnabled = enableFiltering;
            _systemResolution = DetectSystemResolution();

            if (_filterEnabled && _systemResolution != null)
            {
                Logger.LogVerbose($"[ResolutionFilter] System resolution detected: {_systemResolution.Width}x{_systemResolution.Height}");
                Logger.LogVerbose("[ResolutionFilter] Resolution-based filtering ENABLED");
            }
            else if (_filterEnabled)
            {
                Logger.LogWarning("[ResolutionFilter] Could not detect system resolution - filtering disabled");
            }
            else
            {
                Logger.LogVerbose("[ResolutionFilter] Resolution-based filtering DISABLED by user");
            }
        }

        public List<string> FilterByResolution(List<string> urlsOrFilenames)
        {
            if (!_filterEnabled || _systemResolution is null || urlsOrFilenames is null || urlsOrFilenames.Count == 0)
            {
                return urlsOrFilenames ?? new List<string>();
            }

            var filtered = new List<string>();
            var skipped = new List<(string url, Resolution resolution)>();

            foreach (string item in urlsOrFilenames)
            {
                Resolution detectedResolution = ExtractResolution(item);

                if (detectedResolution is null)
                {

                    filtered.Add(item);
                }
                else if (MatchesSystemResolution(detectedResolution))
                {

                    filtered.Add(item);
                    Logger.LogVerbose($"[ResolutionFilter] ✓ Matched: {GetDisplayName(item)} ({detectedResolution.Width}x{detectedResolution.Height})");
                }
                else
                {

                    skipped.Add((item, detectedResolution));
                }
            }

            if (skipped.Count > 0)
            {
                Logger.LogVerbose($"[ResolutionFilter] Skipped {skipped.Count} file(s) with non-matching resolutions:");
                foreach ((string url, Resolution resolution) in skipped)
                {
                    Logger.LogVerbose($"[ResolutionFilter]   ✗ Skipped: {GetDisplayName(url)} ({resolution.Width}x{resolution.Height} != {_systemResolution.Width}x{_systemResolution.Height})");
                }
            }

            return filtered;
        }

        public Dictionary<string, List<string>> FilterResolvedUrls(Dictionary<string, List<string>> urlToFilenames)
        {
            if (!_filterEnabled || _systemResolution is null || urlToFilenames is null)
            {
                return urlToFilenames ?? new Dictionary<string, List<string>>(

StringComparer.Ordinal);
            }

            var filtered = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, List<string>> kvp in urlToFilenames)
            {
                string url = kvp.Key;
                List<string> filenames = kvp.Value;

                Resolution urlResolution = ExtractResolution(url);
                if (urlResolution != null && !MatchesSystemResolution(urlResolution))
                {
                    Logger.LogVerbose($"[ResolutionFilter] ✗ Skipped URL with non-matching resolution: {GetDisplayName(url)} ({urlResolution.Width}x{urlResolution.Height})");
                    continue;
                }

                List<string> filteredFilenames = FilterByResolution(filenames);
                if (filteredFilenames.Count > 0)
                {
                    filtered[url] = filteredFilenames;
                }
            }

            return filtered;
        }

        public bool ShouldDownload(string urlOrFilename)
        {
            if (!_filterEnabled || _systemResolution is null)
            {
                return true;
            }

            Resolution detectedResolution = ExtractResolution(urlOrFilename);

            if (detectedResolution is null)
            {
                return true;
            }

            return MatchesSystemResolution(detectedResolution);
        }

        private static Resolution ExtractResolution(string urlOrFilename)
        {
            if (string.IsNullOrWhiteSpace(urlOrFilename))
            {
                return null;
            }

            Match match = s_resolutionPattern.Match(urlOrFilename);
            if (!match.Success)
            {
                return null;
            }

            if (int.TryParse(match.Groups[1].Value, out int width) &&
                 int.TryParse(match.Groups[2].Value, out int height))
            {
                return new Resolution { Width = width, Height = height };
            }

            return null;
        }

        private bool MatchesSystemResolution(Resolution resolution)
        {
            if (_systemResolution is null || resolution is null)
            {
                return false;
            }

            return resolution.Width == _systemResolution.Width && resolution.Height == _systemResolution.Height;
        }

        private static Resolution DetectSystemResolution()
        {
            try
            {

                if (Utility.UtilityHelper.GetOperatingSystem() == OSPlatform.Windows)
                {
                    return DetectWindowsResolution();
                }

                if (Utility.UtilityHelper.GetOperatingSystem() == OSPlatform.Linux)
                {
                    return DetectLinuxResolution();
                }

                if (Utility.UtilityHelper.GetOperatingSystem() == OSPlatform.OSX)
                {
                    return DetectMacOsResolution();
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[ResolutionFilter] Failed to detect system resolution: {ex.Message}");
            }

            return null;
        }

        private static Resolution DetectWindowsResolution()
        {
            try
            {

                int screenWidth = GetSystemMetrics(0);
                int screenHeight = GetSystemMetrics(1);

                if (screenWidth > 0 && screenHeight > 0)
                {
                    return new Resolution { Width = screenWidth, Height = screenHeight };
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[ResolutionFilter] Windows resolution detection failed: {ex.Message}");
            }

            return null;
        }

        private static Resolution DetectLinuxResolution()
        {
            try
            {

                (int ExitCode, string Output, string Error) result = Utility.PlatformAgnosticMethods.TryExecuteCommand("xrandr | grep '*' | awk '{print $1}' | head -n1");
                if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Output))
                {
                    string resolution = result.Output.Trim();
                    Match match = s_resolutionPattern.Match(resolution);
                    if (match.Success &&
                         int.TryParse(match.Groups[1].Value, out int width) &&
                         int.TryParse(match.Groups[2].Value, out int height))
                    {
                        return new Resolution { Width = width, Height = height };
                    }
                }

                result = Utility.PlatformAgnosticMethods.TryExecuteCommand("xdpyinfo | grep dimensions | awk '{print $2}'");
                if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Output))
                {
                    string resolution = result.Output.Trim();
                    Match match = s_resolutionPattern.Match(resolution);
                    if (match.Success &&
                         int.TryParse(match.Groups[1].Value, out int width) &&
                         int.TryParse(match.Groups[2].Value, out int height))
                    {
                        return new Resolution { Width = width, Height = height };
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[ResolutionFilter] Linux resolution detection failed: {ex.Message}");
            }

            return null;
        }

        private static Resolution DetectMacOsResolution()
        {
            try
            {

                (int ExitCode, string Output, string Error) result = Utility.PlatformAgnosticMethods.TryExecuteCommand("system_profiler SPDisplaysDataType | grep Resolution | awk '{print $2 \"x\" $4}'");
                if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Output))
                {
                    string resolution = result.Output.Trim();
                    Match match = s_resolutionPattern.Match(resolution);
                    if (match.Success &&
                         int.TryParse(match.Groups[1].Value, out int width) &&
                         int.TryParse(match.Groups[2].Value, out int height))
                    {
                        return new Resolution { Width = width, Height = height };
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[ResolutionFilter] macOS resolution detection failed: {ex.Message}");
            }

            return null;
        }

        private static string GetDisplayName(string urlOrPath)
        {
            if (string.IsNullOrWhiteSpace(urlOrPath))
            {
                return urlOrPath;
            }

            try
            {

                if (urlOrPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                     urlOrPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    var uri = new Uri(urlOrPath);
                    string filename = System.IO.Path.GetFileName(uri.LocalPath);
                    return !string.IsNullOrWhiteSpace(filename) ? filename : urlOrPath;
                }

                return System.IO.Path.GetFileName(urlOrPath);
            }
            catch
            {
                return urlOrPath;
            }
        }

        // Windows-specific P/Invoke - only called when running on Windows
        // The runtime check in DetectWindowsResolution ensures this is only invoked on Windows platforms
        [DllImport("user32.dll", SetLastError = false, EntryPoint = "GetSystemMetrics")]
        private static extern int GetSystemMetricsNative(int nIndex);

        private static int GetSystemMetrics(int nIndex)
        {
            if (UtilityHelper.GetOperatingSystem() != OSPlatform.Windows)
            {
                Logger.LogWarning("Attempted to query Windows system metrics on a non-Windows platform.");
                return 0;
            }

            return GetSystemMetricsNative(nIndex);
        }

        private sealed class Resolution
        {
            public int Width { get; set; }
            public int Height { get; set; }

            public override string ToString() => $"{Width}x{Height}";
        }
    }
}
