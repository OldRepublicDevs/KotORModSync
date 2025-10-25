// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
namespace KOTORModSync.Core
{
	[SuppressMessage(
		category: "Performance",
		checkId: "CA1822:Mark members as static",
		Justification = "unique naming scheme used for class"
	)]
	[SuppressMessage(
		category: "CodeQuality",
		checkId: "IDE0079:Remove unnecessary suppression",
		Justification = "<Pending>"
	)]
	[SuppressMessage(category: "ReSharper", checkId: "MemberCanBeMadeStatic.Global")]
	[SuppressMessage(category: "ReSharper", checkId: "InconsistentNaming")]
	[SuppressMessage(category: "ReSharper", checkId: "MemberCanBePrivate.Global")]
	[SuppressMessage(category: "ReSharper", checkId: "UnusedMember.Global")]
	public sealed class MainConfig : INotifyPropertyChanged
	{
		public MainConfig()
		{
			debugLogging = true; // Default to true for debugging persistence issues
			attemptFixes = true;
			noAdmin = false;
			caseInsensitivePathing = true;
			validateAndReplaceInvalidArchives = true;
			filterDownloadsByResolution = true;
		}
		[JetBrains.Annotations.NotNull]
		public static string CurrentVersion => "2.0.0";
		public static class ValidTargetGames
		{
			public const string K1 = "K1";
			public const string TSL = "TSL";
			public const string KOTOR1 = "KOTOR1";
			public const string KOTOR2 = "KOTOR2";
		}
		public static bool NoAdmin { get; private set; }
		public bool noAdmin
		{
			get => NoAdmin;
			set => NoAdmin = value;
		}
		public bool useCopyForMoveActions
		{
			get => UseCopyForMoveActions;
			set => UseCopyForMoveActions = value;
		}
		public static bool UseCopyForMoveActions { get; private set; }
		public static bool UseMultiThreadedIO { get; private set; }
		public bool useMultiThreadedIO { get => UseMultiThreadedIO; set => UseMultiThreadedIO = value; }
		public static bool CaseInsensitivePathing { get; private set; }
		public bool caseInsensitivePathing
		{
			get => CaseInsensitivePathing;
			set => CaseInsensitivePathing = Utility.Utility.GetOperatingSystem() != OSPlatform.Windows;
		}
		public static bool DebugLogging { get; private set; }
		public bool debugLogging { get => DebugLogging; set => DebugLogging = value; }
		public static DirectoryInfo LastOutputDirectory { get; private set; }
		[CanBeNull]
		public DirectoryInfo lastOutputDirectory
		{
			get => LastOutputDirectory;
			set => LastOutputDirectory = value;
		}
		public static bool AttemptFixes { get; private set; }
		public bool attemptFixes { get => AttemptFixes; set => AttemptFixes = value; }
		public static bool ArchiveDeepCheck { get; private set; }
		public bool archiveDeepCheck { get => ArchiveDeepCheck; set => ArchiveDeepCheck = value; }
		public static bool ValidateAndReplaceInvalidArchives { get; private set; }
		public bool validateAndReplaceInvalidArchives { get => ValidateAndReplaceInvalidArchives; set => ValidateAndReplaceInvalidArchives = value; }
		public static bool FilterDownloadsByResolution { get; private set; }
		public bool filterDownloadsByResolution { get => FilterDownloadsByResolution; set => FilterDownloadsByResolution = value; }
		public static string NexusModsApiKey { get; private set; }
		public string nexusModsApiKey { get => NexusModsApiKey; set => NexusModsApiKey = value; }
		public static string FileEncoding { get; private set; } = "utf-8";
		public string fileEncoding { get => FileEncoding; set => FileEncoding = value ?? "utf-8"; }
		public static string SelectedHolopatcherVersion { get; private set; }
		public string selectedHolopatcherVersion { get => SelectedHolopatcherVersion; set => SelectedHolopatcherVersion = value; }
		[JetBrains.Annotations.NotNull][JetBrains.Annotations.ItemNotNull] public static List<ModComponent> AllComponents { get; set; } = new List<ModComponent>();
		[JetBrains.Annotations.NotNull]
		[JetBrains.Annotations.ItemNotNull]
		public List<ModComponent> allComponents
		{
			get => AllComponents;
			set => AllComponents = value ?? throw new ArgumentNullException(nameof(value));
		}
		[CanBeNull] public static ModComponent CurrentComponent { get; set; }
		[CanBeNull]
		public ModComponent currentComponent
		{
			get => CurrentComponent;
			set
			{
				if (CurrentComponent == value) return;
				CurrentComponent = value;
				OnPropertyChanged(nameof(currentComponent));
			}
		}
		public static string BeforeModListContent { get; set; } = string.Empty;
		public string beforeModListContent
		{
			get => BeforeModListContent;
			set => BeforeModListContent = value ?? string.Empty;
		}
		public static string AfterModListContent { get; set; } = string.Empty;
		public string afterModListContent
		{
			get => AfterModListContent;
			set => AfterModListContent = value ?? string.Empty;
		}
		public static string WidescreenSectionContent { get; set; } = "Please install manually the widescreen implementations, e.g. uniws, before continuing.";
		public string widescreenSectionContent
		{
			get => WidescreenSectionContent;
			set => WidescreenSectionContent = value ?? string.Empty;
		}
		public static string AspyrSectionContent { get; set; } = string.Empty;
		public string aspyrSectionContent
		{
			get => AspyrSectionContent;
			set => AspyrSectionContent = value ?? string.Empty;
		}
		public static string TargetGame { get; set; } = string.Empty;
		public string targetGame
		{
			get => TargetGame;
			set
			{
				if ( !string.IsNullOrWhiteSpace(value) && !MainConfig.IsValidTargetGame(value) )
				{
					Logger.LogWarning($"Invalid target game '{value}'. Valid values are 'K1' or 'TSL'. Value will be stored as-is but may cause issues.");
				}
				TargetGame = value ?? string.Empty;
			}
		}
		public static bool IsValidTargetGame(string game)
		{
			if ( string.IsNullOrWhiteSpace(game) )
				return false;
			return game.Equals(ValidTargetGames.K1, StringComparison.OrdinalIgnoreCase)
				|| game.Equals(ValidTargetGames.TSL, StringComparison.OrdinalIgnoreCase)
				|| game.Equals(ValidTargetGames.KOTOR1, StringComparison.OrdinalIgnoreCase)
				|| game.Equals(ValidTargetGames.KOTOR2, StringComparison.OrdinalIgnoreCase);
		}
		public static string FileFormatVersion { get; set; } = "2.0";
		public string fileFormatVersion
		{
			get => FileFormatVersion;
			set => FileFormatVersion = value ?? "2.0";
		}
		public static string BuildName { get; set; } = string.Empty;
		public string buildName
		{
			get => BuildName;
			set => BuildName = value ?? string.Empty;
		}
		public static string BuildAuthor { get; set; } = string.Empty;
		public string buildAuthor
		{
			get => BuildAuthor;
			set => BuildAuthor = value ?? string.Empty;
		}
		public static string BuildDescription { get; set; } = string.Empty;
		public string buildDescription
		{
			get => BuildDescription;
			set => BuildDescription = value ?? string.Empty;
		}
		public static DateTime? LastModified { get; set; }
		[CanBeNull]
		public DateTime? lastModified
		{
			get => LastModified;
			set => LastModified = value;
		}
		[CanBeNull] public static DirectoryInfo SourcePath { get; private set; }
		[CanBeNull]
		public DirectoryInfo sourcePath
		{
			get => SourcePath;
			set
			{
				if (SourcePath == value) return;
				SourcePath = value;
				OnPropertyChanged(nameof(sourcePathFullName));
			}
		}
		[CanBeNull] public string sourcePathFullName => SourcePath?.FullName;
		[CanBeNull] public static DirectoryInfo DestinationPath { get; private set; }
		[CanBeNull]
		public DirectoryInfo destinationPath
		{
			get => DestinationPath;
			set
			{
				if (DestinationPath == value) return;
				DestinationPath = value;
				OnPropertyChanged(nameof(destinationPathFullName));
			}
		}
		[CanBeNull] public string destinationPathFullName => DestinationPath?.FullName;
		public event PropertyChangedEventHandler PropertyChanged;
		private void OnPropertyChanged([CallerMemberName][CanBeNull] string propertyName = null) =>
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
