// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace KOTORModSync.Core.Services
{
	/// <summary>
	/// Service that filters download URLs/filenames based on system resolution to avoid downloading unnecessary files.
	/// Detects resolution patterns like "1920x1080", "3840x2160" in filenames and filters to match system resolution.
	/// </summary>
	public sealed class ResolutionFilterService
	{
		private static readonly Regex ResolutionPattern = new Regex(@"(\d{3,4})x(\d{3,4})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private readonly Resolution _systemResolution;
		private readonly bool _filterEnabled;

		public ResolutionFilterService(bool enableFiltering = true)
		{
			_filterEnabled = enableFiltering;
			_systemResolution = DetectSystemResolution();
			
			if ( _filterEnabled && _systemResolution != null )
			{
				Logger.LogVerbose($"[ResolutionFilter] System resolution detected: {_systemResolution.Width}x{_systemResolution.Height}");
				Logger.LogVerbose($"[ResolutionFilter] Resolution-based filtering ENABLED");
			}
			else if ( _filterEnabled )
			{
				Logger.LogWarning("[ResolutionFilter] Could not detect system resolution - filtering disabled");
			}
			else
			{
				Logger.LogVerbose("[ResolutionFilter] Resolution-based filtering DISABLED by user");
			}
		}

		/// <summary>
		/// Filters a list of URLs or filenames to only include those matching the system resolution,
		/// or those without any resolution pattern.
		/// </summary>
		/// <param name="urlsOrFilenames">List of URLs or filenames to filter</param>
		/// <returns>Filtered list containing only matching resolution files</returns>
		public List<string> FilterByResolution(List<string> urlsOrFilenames)
		{
			if ( !_filterEnabled || _systemResolution == null || urlsOrFilenames == null || urlsOrFilenames.Count == 0 )
				return urlsOrFilenames ?? new List<string>();

			var filtered = new List<string>();
			var skipped = new List<(string url, Resolution resolution)>();

			foreach ( string item in urlsOrFilenames )
			{
				Resolution detectedResolution = ExtractResolution(item);

				if ( detectedResolution == null )
				{
					// No resolution pattern found - include it (could be a non-resolution-specific file)
					filtered.Add(item);
				}
				else if ( MatchesSystemResolution(detectedResolution) )
				{
					// Resolution matches system - include it
					filtered.Add(item);
					Logger.LogVerbose($"[ResolutionFilter] ✓ Matched: {GetDisplayName(item)} ({detectedResolution.Width}x{detectedResolution.Height})");
				}
				else
				{
					// Resolution doesn't match - skip it
					skipped.Add((item, detectedResolution));
				}
			}

			// Log skipped files
			if ( skipped.Count > 0 )
			{
				Logger.LogVerbose($"[ResolutionFilter] Skipped {skipped.Count} file(s) with non-matching resolutions:");
				foreach ( var (url, resolution) in skipped )
				{
					Logger.LogVerbose($"[ResolutionFilter]   ✗ Skipped: {GetDisplayName(url)} ({resolution.Width}x{resolution.Height} != {_systemResolution.Width}x{_systemResolution.Height})");
				}
			}

			return filtered;
		}

		/// <summary>
		/// Filters a dictionary mapping URLs to filenames based on resolution.
		/// </summary>
		public Dictionary<string, List<string>> FilterResolvedUrls(Dictionary<string, List<string>> urlToFilenames)
		{
			if ( !_filterEnabled || _systemResolution == null || urlToFilenames == null )
				return urlToFilenames ?? new Dictionary<string, List<string>>();

			var filtered = new Dictionary<string, List<string>>();

			foreach ( var kvp in urlToFilenames )
			{
				string url = kvp.Key;
				List<string> filenames = kvp.Value;

				// Check URL for resolution pattern first
				Resolution urlResolution = ExtractResolution(url);
				if ( urlResolution != null && !MatchesSystemResolution(urlResolution) )
				{
					Logger.LogVerbose($"[ResolutionFilter] ✗ Skipped URL with non-matching resolution: {GetDisplayName(url)} ({urlResolution.Width}x{urlResolution.Height})");
					continue;
				}

				// Filter filenames by resolution
				List<string> filteredFilenames = FilterByResolution(filenames);
				if ( filteredFilenames.Count > 0 )
				{
					filtered[url] = filteredFilenames;
				}
			}

			return filtered;
		}

		/// <summary>
		/// Checks if a specific URL or filename should be downloaded based on resolution.
		/// </summary>
		public bool ShouldDownload(string urlOrFilename)
		{
			if ( !_filterEnabled || _systemResolution == null )
				return true;

			Resolution detectedResolution = ExtractResolution(urlOrFilename);
			
			// If no resolution pattern found, allow download
			if ( detectedResolution == null )
				return true;

			// Check if resolution matches
			return MatchesSystemResolution(detectedResolution);
		}

		/// <summary>
		/// Extracts resolution from a URL or filename (e.g., "k1rs_60fps_3840x2160.7z" -> 3840x2160)
		/// </summary>
		private Resolution ExtractResolution(string urlOrFilename)
		{
			if ( string.IsNullOrWhiteSpace(urlOrFilename) )
				return null;

			Match match = ResolutionPattern.Match(urlOrFilename);
			if ( !match.Success )
				return null;

			if ( int.TryParse(match.Groups[1].Value, out int width) &&
				 int.TryParse(match.Groups[2].Value, out int height) )
			{
				return new Resolution { Width = width, Height = height };
			}

			return null;
		}

		/// <summary>
		/// Checks if a resolution matches the system resolution (exact match or common aspect ratio tolerance).
		/// </summary>
		private bool MatchesSystemResolution(Resolution resolution)
		{
			if ( _systemResolution == null || resolution == null )
				return false;

			// Exact match
			if ( resolution.Width == _systemResolution.Width && resolution.Height == _systemResolution.Height )
				return true;

			// Allow aspect ratio match with some tolerance for scaled resolutions
			// For example: 2560x1440 vs 1920x1080 both are 16:9 but we want exact matches
			// So we disable aspect ratio tolerance - require exact match
			return false;
		}

		/// <summary>
		/// Detects the primary display's resolution on the current system.
		/// </summary>
		private Resolution DetectSystemResolution()
		{
			try
			{
				// Try platform-specific detection
				if ( Utility.Utility.GetOperatingSystem() == OSPlatform.Windows )
				{
					return DetectWindowsResolution();
				}
				else if ( Utility.Utility.GetOperatingSystem() == OSPlatform.Linux )
				{
					return DetectLinuxResolution();
				}
				else if ( Utility.Utility.GetOperatingSystem() == OSPlatform.OSX )
				{
					return DetectMacOSResolution();
				}
			}
			catch ( Exception ex )
			{
				Logger.LogWarning($"[ResolutionFilter] Failed to detect system resolution: {ex.Message}");
			}

			return null;
		}

		private Resolution DetectWindowsResolution()
		{
			try
			{
				// Use P/Invoke to get Windows display resolution
				int screenWidth = GetSystemMetrics(0);  // SM_CXSCREEN
				int screenHeight = GetSystemMetrics(1); // SM_CYSCREEN

				if ( screenWidth > 0 && screenHeight > 0 )
				{
					return new Resolution { Width = screenWidth, Height = screenHeight };
				}
			}
			catch ( Exception ex )
			{
				Logger.LogWarning($"[ResolutionFilter] Windows resolution detection failed: {ex.Message}");
			}

			return null;
		}

		private Resolution DetectLinuxResolution()
		{
			try
			{
				// Try xrandr command
				var result = Utility.PlatformAgnosticMethods.TryExecuteCommand("xrandr | grep '*' | awk '{print $1}' | head -n1");
				if ( result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Output) )
				{
					string resolution = result.Output.Trim();
					Match match = ResolutionPattern.Match(resolution);
					if ( match.Success &&
						 int.TryParse(match.Groups[1].Value, out int width) &&
						 int.TryParse(match.Groups[2].Value, out int height) )
					{
						return new Resolution { Width = width, Height = height };
					}
				}

				// Try xdpyinfo as fallback
				result = Utility.PlatformAgnosticMethods.TryExecuteCommand("xdpyinfo | grep dimensions | awk '{print $2}'");
				if ( result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Output) )
				{
					string resolution = result.Output.Trim();
					Match match = ResolutionPattern.Match(resolution);
					if ( match.Success &&
						 int.TryParse(match.Groups[1].Value, out int width) &&
						 int.TryParse(match.Groups[2].Value, out int height) )
					{
						return new Resolution { Width = width, Height = height };
					}
				}
			}
			catch ( Exception ex )
			{
				Logger.LogWarning($"[ResolutionFilter] Linux resolution detection failed: {ex.Message}");
			}

			return null;
		}

		private Resolution DetectMacOSResolution()
		{
			try
			{
				// Use system_profiler to get display resolution
				var result = Utility.PlatformAgnosticMethods.TryExecuteCommand("system_profiler SPDisplaysDataType | grep Resolution | awk '{print $2 \"x\" $4}'");
				if ( result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Output) )
				{
					string resolution = result.Output.Trim();
					Match match = ResolutionPattern.Match(resolution);
					if ( match.Success &&
						 int.TryParse(match.Groups[1].Value, out int width) &&
						 int.TryParse(match.Groups[2].Value, out int height) )
					{
						return new Resolution { Width = width, Height = height };
					}
				}
			}
			catch ( Exception ex )
			{
				Logger.LogWarning($"[ResolutionFilter] macOS resolution detection failed: {ex.Message}");
			}

			return null;
		}

		/// <summary>
		/// Gets a display-friendly name from a URL or path
		/// </summary>
		private string GetDisplayName(string urlOrPath)
		{
			if ( string.IsNullOrWhiteSpace(urlOrPath) )
				return urlOrPath;

			try
			{
				// If it's a URL, extract filename from the end
				if ( urlOrPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
					 urlOrPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase) )
				{
					var uri = new Uri(urlOrPath);
					string filename = System.IO.Path.GetFileName(uri.LocalPath);
					return !string.IsNullOrWhiteSpace(filename) ? filename : urlOrPath;
				}

				// Otherwise treat as filename
				return System.IO.Path.GetFileName(urlOrPath);
			}
			catch
			{
				return urlOrPath;
			}
		}

		// Windows P/Invoke for screen resolution
		[DllImport("user32.dll")]
		private static extern int GetSystemMetrics(int nIndex);

		/// <summary>
		/// Represents a display resolution
		/// </summary>
		private class Resolution
		{
			public int Width { get; set; }
			public int Height { get; set; }

			public override string ToString() => $"{Width}x{Height}";
		}
	}
}

