// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using KOTORModSync.Core;

namespace KOTORModSync.Models
{
	/// <summary>
	/// Application settings that persist across sessions.
	/// </summary>
	public sealed class AppSettings
	{
		/// <summary>
		/// Selected theme path (e.g., "/Styles/KotorStyle.axaml" or "/Styles/Kotor2Style.axaml")
		/// </summary>
		[JsonPropertyName("theme")]
		[CanBeNull]
		public string Theme { get; set; } = "/Styles/KotorStyle.axaml";

		/// <summary>
		/// Path to the mods directory
		/// </summary>
		[JsonPropertyName("sourcePath")]
		[CanBeNull]
		public string SourcePath { get; set; }

		/// <summary>
		/// Path to the KOTOR installation directory
		/// </summary>
		[JsonPropertyName("destinationPath")]
		[CanBeNull]
		public string DestinationPath { get; set; }

		/// <summary>
		/// Enable verbose/debug logging
		/// </summary>
		[JsonPropertyName("debugLogging")]
		public bool DebugLogging { get; set; }

		/// <summary>
		/// Attempt to automatically fix config errors
		/// </summary>
		[JsonPropertyName("attemptFixes")]
		public bool AttemptFixes { get; set; } = true;

		/// <summary>
		/// Don't ask for admin/sudo permissions
		/// </summary>
		[JsonPropertyName("noAdmin")]
		public bool NoAdmin { get; set; }

		/// <summary>
		/// Mock case-insensitive filesystem (Unix only)
		/// </summary>
		[JsonPropertyName("caseInsensitivePathing")]
		public bool CaseInsensitivePathing { get; set; } = true;

		/// <summary>
		/// Perform deep archive checking
		/// </summary>
		[JsonPropertyName("archiveDeepCheck")]
		public bool ArchiveDeepCheck { get; set; }

		/// <summary>
		/// Use multi-threaded I/O operations
		/// </summary>
		[JsonPropertyName("useMultiThreadedIO")]
		public bool UseMultiThreadedIO { get; set; }

		/// <summary>
		/// Use copy operations instead of move operations
		/// </summary>
		[JsonPropertyName("useCopyForMoveActions")]
		public bool UseCopyForMoveActions { get; set; }

		/// <summary>
		/// Path to the last output directory used
		/// </summary>
		[JsonPropertyName("lastOutputDirectory")]
		[CanBeNull]
		public string LastOutputDirectory { get; set; }

		/// <summary>
		/// Creates default settings
		/// </summary>
		public AppSettings()
		{
		}

		/// <summary>
		/// Loads settings from MainConfig and MainWindow
		/// </summary>
		public static AppSettings FromCurrentState([NotNull] MainConfig mainConfig, [CanBeNull] string currentTheme)
		{
			if ( mainConfig is null )
				throw new ArgumentNullException(nameof(mainConfig));

			return new AppSettings
			{
				Theme = currentTheme ?? "/Styles/KotorStyle.axaml",
				SourcePath = mainConfig.sourcePathFullName,
				DestinationPath = mainConfig.destinationPathFullName,
				DebugLogging = mainConfig.debugLogging,
				AttemptFixes = mainConfig.attemptFixes,
				NoAdmin = mainConfig.noAdmin,
				CaseInsensitivePathing = mainConfig.caseInsensitivePathing,
				ArchiveDeepCheck = mainConfig.archiveDeepCheck,
				UseMultiThreadedIO = mainConfig.useMultiThreadedIO,
				UseCopyForMoveActions = mainConfig.useCopyForMoveActions,
				LastOutputDirectory = mainConfig.lastOutputDirectory?.FullName
			};
		}

		/// <summary>
		/// Applies settings to MainConfig and returns Theme value
		/// </summary>
		public void ApplyToMainConfig([NotNull] MainConfig mainConfig, [NotNull] out string theme)
		{
			if ( mainConfig is null )
				throw new ArgumentNullException(nameof(mainConfig));

			// Apply directory paths
			if ( !string.IsNullOrEmpty(SourcePath) && Directory.Exists(SourcePath) )
				mainConfig.sourcePath = new DirectoryInfo(SourcePath);

			if ( !string.IsNullOrEmpty(DestinationPath) && Directory.Exists(DestinationPath) )
				mainConfig.destinationPath = new DirectoryInfo(DestinationPath);

			// Apply MainConfig settings
			mainConfig.debugLogging = DebugLogging;
			mainConfig.attemptFixes = AttemptFixes;
			mainConfig.noAdmin = NoAdmin;
			mainConfig.caseInsensitivePathing = CaseInsensitivePathing;
			mainConfig.archiveDeepCheck = ArchiveDeepCheck;
			mainConfig.useMultiThreadedIO = UseMultiThreadedIO;
			mainConfig.useCopyForMoveActions = UseCopyForMoveActions;

			if ( !string.IsNullOrEmpty(LastOutputDirectory) && Directory.Exists(LastOutputDirectory) )
				mainConfig.lastOutputDirectory = new DirectoryInfo(LastOutputDirectory);

			// Return theme value that needs to be set in MainWindow
			theme = Theme ?? "/Styles/KotorStyle.axaml";
		}
	}

	/// <summary>
	/// Manages loading and saving application settings
	/// </summary>
	public static class SettingsManager
	{
		private static readonly string SettingsDirectory = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			"KOTORModSync"
		);

		private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "settings.json");

		private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
		{
			WriteIndented = true,
			PropertyNameCaseInsensitive = true
		};

		/// <summary>
		/// Loads settings from disk, or returns default settings if file doesn't exist
		/// </summary>
		[NotNull]
		public static AppSettings LoadSettings()
		{
			try
			{
				if ( !File.Exists(SettingsFilePath) )
				{
					Logger.LogVerbose($"No settings file found at '{SettingsFilePath}', using defaults");
					return new AppSettings();
				}

				string json = File.ReadAllText(SettingsFilePath);
				AppSettings settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);

				if ( settings is null )
				{
					Logger.LogWarning("Failed to deserialize settings, using defaults");
					return new AppSettings();
				}

				Logger.LogVerbose($"Settings loaded from '{SettingsFilePath}'");
				return settings;
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, customMessage: "Failed to load settings, using defaults");
				return new AppSettings();
			}
		}

		/// <summary>
		/// Saves settings to disk
		/// </summary>
		public static void SaveSettings([NotNull] AppSettings settings)
		{
			if ( settings is null )
				throw new ArgumentNullException(nameof(settings));

			try
			{
				// Ensure directory exists
				if ( !Directory.Exists(SettingsDirectory) )
					_ = Directory.CreateDirectory(SettingsDirectory);

				string json = JsonSerializer.Serialize(settings, JsonOptions);
				File.WriteAllText(SettingsFilePath, json);

				Logger.LogVerbose($"Settings saved to '{SettingsFilePath}'");
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, customMessage: "Failed to save settings");
			}
		}
	}
}

