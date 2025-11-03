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
using ItemNotNullAttribute = JetBrains.Annotations.ItemNotNullAttribute;
using NotNullAttribute = JetBrains.Annotations.NotNullAttribute;
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
            set => CaseInsensitivePathing = Utility.UtilityHelper.GetOperatingSystem() != OSPlatform.Windows;
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
        public static bool EnableFileWatcher { get; private set; }
        public bool enableFileWatcher { get => EnableFileWatcher; set => EnableFileWatcher = value; }
        [NotNull][ItemNotNull] public static List<ModComponent> AllComponents { get; set; } = new List<ModComponent>();
        [NotNull]
        [ItemNotNull]
        public List<ModComponent> allComponents
        {
            get => AllComponents;
            set => AllComponents = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Singleton VirtualFileSystemProvider for all dry-run validation and file state simulation.
        /// Initialized once and reused throughout the application lifecycle.
        /// </summary>
        [NotNull]
        public static Services.FileSystem.VirtualFileSystemProvider VirtualFileSystemProvider { get; private set; } = new Services.FileSystem.VirtualFileSystemProvider();

        [CanBeNull] public static ModComponent CurrentComponent { get; set; }
        [CanBeNull]
        public ModComponent currentComponent
        {
            get => CurrentComponent;
            set
            {
                if (CurrentComponent == value)
                {
                    return;
                }

                CurrentComponent = value;
                OnPropertyChanged(nameof(currentComponent));
            }
        }
        public static string PreambleContent { get; set; } = "# Installation Guide\n\nWelcome to the KOTOR Mod Installation Guide. This guide will help you install this mod build for Knights of the Old Republic.\n\n:::warning\nImportant\n:   Please read through these instructions carefully before beginning the installation process.\n:::\n\n### Prerequisites\n\n- A fresh installation of Knights of the Old Republic\n- KOTORModSync - an automated installer that handles TSLPatcher and HoloPatcher installations\n- Approximately 7GB free disk space for mod archives (before extraction)\n\n### Installation Process\n\nKOTORModSync will automatically handle extraction and installation of mods. You just need to:\n\n1. Ensure your game directory is not read-only\n2. Configure your source (mod archives) and destination (game directory) paths\n3. Select the mods you want to install\n4. Let the installer handle the rest\n\n:::warning\nZeroing Step\n:   If you have previously installed mods, it's recommended to perform a fresh install. Uninstall the game, delete all remaining files in the game directory, and reinstall before proceeding.\n:::\n\n### Known Issues\n\n- Some users may experience rare crashes when entering new areas. If this occurs, temporarily disable 'Frame Buffer Effects' and 'Soft Shadows' in Advanced Graphics Options.";
        public string preambleContent
        {
            get => PreambleContent;
            set => PreambleContent = value ?? string.Empty;
        }
        public static string EpilogueContent { get; set; } = "## Post-Installation Notes\n\n### Launch Options\n\nAfter installation, launch the game directly from the executable, not through the Steam interface (if using widescreen support).\n\n### Troubleshooting\n\nIf you encounter issues:\n\n- **Crash on load**: Try disabling 'Frame Buffer Effects' in Advanced Graphics Options\n- **Character stuck after combat**: Enable v-sync or set your monitor to 60hz\n- **Rare crashes**: Update your graphics drivers\n\nFor additional support, please consult the troubleshooting section in the main documentation.";
        public string epilogueContent
        {
            get => EpilogueContent;
            set => EpilogueContent = value ?? string.Empty;
        }
        public static string WidescreenWarningContent { get; set; } = ":::note\nWidescreen Support\n:   This build includes optional widescreen support. Widescreen mods must be installed before applying the 4GB patcher. Please see the widescreen section below for details.\n:::";
        public string widescreenWarningContent
        {
            get => WidescreenWarningContent;
            set => WidescreenWarningContent = value ?? string.Empty;
        }
        public static string AspyrExclusiveWarningContent { get; set; } = ":::warning\nAspyr Version Required\n:   The following mods require the Aspyr patch version of KOTOR 2. If you are using the legacy version, these mods should be skipped.\n:::";
        public string aspyrExclusiveWarningContent
        {
            get => AspyrExclusiveWarningContent;
            set => AspyrExclusiveWarningContent = value ?? string.Empty;
        }
        public static string InstallationWarningContent { get; set; } = "WARNING! While there is code in place to prevent incorrect instructions from running, the program cannot predict every possible mistake a user could make in a config file." + Environment.NewLine + " You should back up your Install directory before proceeding." + Environment.NewLine + Environment.NewLine + " Are you sure you're ready to continue?";
        public string installationWarningContent
        {
            get => InstallationWarningContent;
            set => InstallationWarningContent = value ?? string.Empty;
        }
        public static string TargetGame { get; set; } = string.Empty;
        public string targetGame
        {
            get => TargetGame;
            set
            {
                if (!string.IsNullOrWhiteSpace(value) && !MainConfig.IsValidTargetGame(value))
                {
                    Logger.LogWarning($"Invalid target game '{value}'. Valid values are 'K1' or 'TSL'. Value will be stored as-is but may cause issues.");
                }
                TargetGame = value ?? string.Empty;
            }
        }
        public static bool IsValidTargetGame(string game)
        {
            if (string.IsNullOrWhiteSpace(game))
            {
                return false;
            }

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
                if (SourcePath == value)
                {
                    return;
                }

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
                if (DestinationPath == value)
                {
                    return;
                }

                DestinationPath = value;
                OnPropertyChanged(nameof(destinationPathFullName));
            }
        }
        [CanBeNull] public string destinationPathFullName => DestinationPath?.FullName;

        /// <summary>Maximum cache size in megabytes for distributed cache storage (default 10GB)</summary>
        public static long MaxCacheSizeMB { get; set; } = 10240;

        public long maxCacheSizeMB
        {
            get => MaxCacheSizeMB;
            set => MaxCacheSizeMB = value;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName][CanBeNull] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
