// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.


using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using KOTORModSync.Core;

namespace KOTORModSync.Models
{

	public sealed class AppSettings
	{

		[JsonPropertyName("theme")]
		[CanBeNull]
		public string Theme { get; set; } = "/Styles/KotorStyle.axaml";

		[JsonPropertyName("sourcePath")]
		[CanBeNull]
		public string SourcePath { get; set; }

		[JsonPropertyName("destinationPath")]
		[CanBeNull]
		public string DestinationPath { get; set; }

		[JsonPropertyName("debugLogging")]
		public bool DebugLogging { get; set; }

		[JsonPropertyName("attemptFixes")]
		public bool AttemptFixes { get; set; } = true;

		[JsonPropertyName("noAdmin")]
		public bool NoAdmin { get; set; }

		[JsonPropertyName("caseInsensitivePathing")]
		public bool CaseInsensitivePathing { get; set; } = true;

		[JsonPropertyName("archiveDeepCheck")]
		public bool ArchiveDeepCheck { get; set; }

		[JsonPropertyName("useMultiThreadedIO")]
		public bool UseMultiThreadedIO { get; set; }

		[JsonPropertyName("useCopyForMoveActions")]
		public bool UseCopyForMoveActions { get; set; }

		[JsonPropertyName("lastOutputDirectory")]
		[CanBeNull]
		public string LastOutputDirectory { get; set; }

		public AppSettings()
		{
		}

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

		public void ApplyToMainConfig([NotNull] MainConfig mainConfig, [NotNull] out string theme)
		{
			if ( mainConfig is null )
				throw new ArgumentNullException(nameof(mainConfig));

			if ( !string.IsNullOrEmpty(SourcePath) && Directory.Exists(SourcePath) )
				mainConfig.sourcePath = new DirectoryInfo(SourcePath);

			if ( !string.IsNullOrEmpty(DestinationPath) && Directory.Exists(DestinationPath) )
				mainConfig.destinationPath = new DirectoryInfo(DestinationPath);

			mainConfig.debugLogging = DebugLogging;
			mainConfig.attemptFixes = AttemptFixes;
			mainConfig.noAdmin = NoAdmin;
			mainConfig.caseInsensitivePathing = CaseInsensitivePathing;
			mainConfig.archiveDeepCheck = ArchiveDeepCheck;
			mainConfig.useMultiThreadedIO = UseMultiThreadedIO;
			mainConfig.useCopyForMoveActions = UseCopyForMoveActions;

			if ( !string.IsNullOrEmpty(LastOutputDirectory) && Directory.Exists(LastOutputDirectory) )
				mainConfig.lastOutputDirectory = new DirectoryInfo(LastOutputDirectory);

			theme = Theme ?? "/Styles/KotorStyle.axaml";
		}
	}

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

		public static void SaveSettings([NotNull] AppSettings settings)
		{
			if ( settings is null )
				throw new ArgumentNullException(nameof(settings));

			try
			{

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

