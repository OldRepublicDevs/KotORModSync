using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KOTORModSync;
using KOTORModSync.Common;
using KOTORModSync.Config;
using KOTORModSync.Installation;
using KOTORModSync.Logger;
using KOTORModSync.Namespaces;
using KOTORModSync.TSLPatcher;
using KOTORModSync.Reader;
using KOTORModSync.Uninstall;
using JetBrains.Annotations;
#if WINDOWS
using Microsoft.Win32;
#endif

namespace HoloPatcher.UI
{

    /// <summary>
    /// Core functionality for HoloPatcher.
    /// Equivalent to holopatcher/KOTORModSync.py
    /// </summary>
    public static class Core
    {
        public const string VersionLabel = "v2.0.0a1";

        /// <summary>
        /// Checks if the version string indicates an alpha/pre-release version.
        /// Matches .NET versioning schema: checks for 'a' followed by digits or 'alpha' (case-insensitive).
        /// Examples: "2.0.0a1", "2.0.0-alpha1", "2.0.0-alpha.1", "v2.0.0a1"
        /// </summary>
        public static bool IsAlphaVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                return false;
            }

            // Remove 'v' prefix if present
            string normalizedVersion = version.TrimStart('v', 'V');

            // Check for 'alpha' (case-insensitive)
            if (normalizedVersion.IndexOf("alpha", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            // Check for 'a' followed by a digit (e.g., "a1", "a2", "a10")
            // This matches patterns like "2.0.0a1" or "2.0.0-a1"
            for (int i = 0; i < normalizedVersion.Length - 1; i++)
            {
                if ((normalizedVersion[i] == 'a' || normalizedVersion[i] == 'A') &&
                    char.IsDigit(normalizedVersion[i + 1]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Exit codes for the application.
        /// </summary>
        public enum ExitCode
        {
            Success = 0,
            UnknownStartupError = 1,
            NumberOfArgs = 2,
            NamespacesIniNotFound = 3,
            NamespaceIndexOutOfRange = 4,
            ChangesIniNotFound = 5,
            AbortInstallUnsafe = 6,
            ExceptionDuringInstall = 7,
            InstallCompletedWithErrors = 8,
            Crash = 9,
            CloseForUpdateProcess = 10
        }

        /// <summary>
        /// Information about a loaded mod.
        /// </summary>
        public class ModInfo
        {
            public string ModPath { get; set; } = string.Empty;
            public List<PatcherNamespace> Namespaces { get; set; } = new List<PatcherNamespace>();
            public ConfigReader ConfigReader { get; set; }
        }

        /// <summary>
        /// Information about a selected namespace.
        /// </summary>
        public class NamespaceInfo
        {
            public ConfigReader ConfigReader { get; set; }
            public LogLevel LogLevel { get; set; }
            public int? GameNumber { get; set; }
            public List<string> GamePaths { get; set; } = new List<string>();
            [CanBeNull]
            public string InfoContent { get; set; }
            /// <summary>
            /// Whether InfoContent is RTF format (true) or plain text/RTE JSON (false)
            /// </summary>
            public bool IsRtf { get; set; }
        }

        /// <summary>
        /// Result of a mod installation.
        /// </summary>
        public class InstallResult
        {
            public TimeSpan InstallTime { get; set; }
            public int NumErrors { get; set; }
            public int NumWarnings { get; set; }
            public int NumPatches { get; set; }
        }

        /// <summary>
        /// Loads a mod from a directory.
        /// </summary>
        public static ModInfo LoadMod(string directoryPath)
        {
            // Python: tslpatchdata_path = CaseAwarePath(directory_path, "tslpatchdata")
            var tslPatchDataPath = new CaseAwarePath(directoryPath, "tslpatchdata");
            // Python: if not tslpatchdata_path.is_dir() and tslpatchdata_path.parent.name.lower() == "tslpatchdata":
            //         tslpatchdata_path = tslpatchdata_path.parent
            if (!tslPatchDataPath.IsDirectory())
            {
                string parentPath = Path.GetDirectoryName(tslPatchDataPath.GetResolvedPath()) ?? "";
                string parentName = Path.GetFileName(parentPath) ?? "";
                if (parentName.ToLowerInvariant() == "tslpatchdata")
                {
                    tslPatchDataPath = new CaseAwarePath(parentPath);
                }
            }

            // Python: mod_path = str(tslpatchdata_path.parent)
            string modPath = tslPatchDataPath.DirectoryName;
            CaseAwarePath namespacePath = tslPatchDataPath.Combine("namespaces.ini");
            CaseAwarePath changesPath = tslPatchDataPath.Combine("changes.ini");

            List<PatcherNamespace> namespaces;
            // Can be null if no config reader is needed
            ConfigReader configReader = null;

            if (namespacePath.IsFile())
            {
                namespaces = NamespaceReader.FromFilePath(namespacePath.GetResolvedPath());
            }
            else if (changesPath.IsFile())
            {
                configReader = ConfigReader.FromFilePath(changesPath.GetResolvedPath(), tslPatchDataPath: tslPatchDataPath.GetResolvedPath());
                namespaces = new List<PatcherNamespace>
            {
                new PatcherNamespace("changes.ini", "info.rtf")
                {
                    Name = "Default",
                    Description = "Default installation"
                }
            };
            }
            else
            {
                throw new FileNotFoundException($"No namespaces.ini or changes.ini found in {tslPatchDataPath}");
            }

            return new ModInfo
            {
                ModPath = modPath,
                Namespaces = namespaces,
                ConfigReader = configReader
            };
        }

        /// <summary>
        /// Loads configuration for a specific namespace.
        /// </summary>
        public static NamespaceInfo LoadNamespaceConfig(
            string modPath,
            List<PatcherNamespace> namespaces,
            string selectedNamespaceName,
            [CanBeNull] ConfigReader configReader = null)
        {
            // Can be null if namespace not found
            PatcherNamespace namespaceOption = namespaces.FirstOrDefault(x => x.Name == selectedNamespaceName);
            if (namespaceOption is null)
            {
                throw new ArgumentException($"Namespace '{selectedNamespaceName}' not found in namespaces list");
            }

            var changesIniPath = new CaseAwarePath(modPath, "tslpatchdata", namespaceOption.ChangesFilePath());
            string tslPatchDataPath = new CaseAwarePath(modPath, "tslpatchdata").GetResolvedPath();

            ConfigReader reader = configReader ?? ConfigReader.FromFilePath(changesIniPath.GetResolvedPath(), tslPatchDataPath: tslPatchDataPath);
            if (configReader is null)
            {
                reader.Load(reader.Config); // Load() populates the Config
            }

            int? gameNumber = reader.Config.GameNumber;
            // Can be null if game number not set
            Game? game = gameNumber.HasValue ? (Game?)gameNumber.Value : null;

            var gamePaths = new List<string>();
            if (game.HasValue)
            {
                // Find KOTOR paths from registry and default locations
                Dictionary<Game, List<string>> detectedPaths = FindKotorPathsFromDefault();
                // Can be null if paths not found
                if (detectedPaths.TryGetValue(game.Value, out List<string> paths))
                {
                    gamePaths.AddRange(paths);
                }
                // If TSL, also include K1 paths
                if (game.Value == Game.TSL && detectedPaths.TryGetValue(Game.K1, out List<string> k1Paths))
                {
                    gamePaths.AddRange(k1Paths);
                }
            }

            // Load info.rtf or info.rte - matches Python's on_namespace_option_chosen
            var infoRtfPath = new CaseAwarePath(modPath, "tslpatchdata", namespaceOption.RtfFilePath());
            string rtfPathStr = infoRtfPath.GetResolvedPath();
            string rtePathStr = Path.ChangeExtension(rtfPathStr, ".rte");
            var infoRtePath = new CaseAwarePath(rtePathStr);

            // Can be null if info file not found
            string infoContent = null;
            bool isRtf = false;
            if (infoRtePath.IsFile())
            {
                // RTE files are JSON formatted - return raw content for parsing
                byte[] data = File.ReadAllBytes(infoRtePath.GetResolvedPath());
                infoContent = DecodeBytesWithFallbacks(data);
                isRtf = false; // RTE is JSON, not RTF
            }
            else if (infoRtfPath.IsFile())
            {
                // RTF files - return RAW RTF content for RichTextBox to render!
                // Unlike Python which strips it because tkinter doesn't support RTF,
                // Avalonia can render RTF directly with RichTextBox
                byte[] data = File.ReadAllBytes(infoRtfPath.GetResolvedPath());
                infoContent = DecodeBytesWithFallbacks(data);
                isRtf = true; // Mark as RTF so ViewModel knows to use RichTextBox
            }

            return new NamespaceInfo
            {
                ConfigReader = reader,
                LogLevel = reader.Config.LogLevel,
                GameNumber = gameNumber,
                GamePaths = gamePaths,
                InfoContent = infoContent,
                IsRtf = isRtf
            };
        }

        /// <summary>
        /// Validates a KOTOR game directory.
        /// </summary>
        public static string ValidateGameDirectory(string directoryPath)
        {
            var directory = new CaseAwarePath(directoryPath);
            if (!directory.IsDirectory())
            {
                throw new ArgumentException($"Invalid KOTOR directory: {directoryPath}");
            }
            return directory.GetResolvedPath();
        }

        /// <summary>
        /// Validates that mod and game paths are ready for installation.
        /// </summary>
        public static bool ValidateInstallPaths(string modPath, string gamePath)
        {
            return !string.IsNullOrEmpty(modPath) &&
                   new CaseAwarePath(modPath).IsDirectory() &&
                   !string.IsNullOrEmpty(gamePath) &&
                   new CaseAwarePath(gamePath).IsDirectory();
        }

        /// <summary>
        /// Gets the description for a namespace by name.
        /// </summary>
        public static string GetNamespaceDescription(List<PatcherNamespace> namespaces, string selectedNamespaceName)
        {
            // Can be null if namespace not found
            PatcherNamespace namespaceOption = namespaces.FirstOrDefault(x => x.Name == selectedNamespaceName);
            return namespaceOption?.Description ?? "";
        }

        /// <summary>
        /// Calculates total number of patches for progress calculation.
        /// </summary>
        public static int CalculateTotalPatches(ModInstaller installer)
        {
            PatcherConfig config = installer.Config();
            // Count TLK patches manually since GetTlkPatches is private
            int tlkPatches = config.PatchesTLK.Modifiers.Count;
            return config.InstallList.Count +
                   tlkPatches +
                   config.Patches2DA.Count +
                   config.PatchesGFF.Count +
                   config.PatchesNSS.Count +
                   config.PatchesNCS.Count +
                   config.PatchesSSF.Count;
        }

        /// <summary>
        /// Gets confirmation message if mod requires it.
        /// </summary>
        [CanBeNull]
        public static string GetConfirmMessage(ModInstaller installer)
        {
            string msg = installer.Config().ConfirmMessage?.Trim() ?? "";
            return !string.IsNullOrEmpty(msg) && msg != "N/A" ? msg : null;
        }

        /// <summary>
        /// Installs a mod.
        /// </summary>
        public static InstallResult InstallMod(
            string modPath,
            string gamePath,
            List<PatcherNamespace> namespaces,
            string selectedNamespaceName,
            PatchLogger logger,
            CancellationToken cancellationToken,
            [CanBeNull] Action<int> progressCallback = null)
        {
            // Can be null if namespace not found
            PatcherNamespace namespaceOption = namespaces.FirstOrDefault(x => x.Name == selectedNamespaceName);
            if (namespaceOption is null)
            {
                throw new ArgumentException($"Namespace '{selectedNamespaceName}' not found in namespaces list");
            }

            string tslPatchDataPath = new CaseAwarePath(modPath, "tslpatchdata").GetResolvedPath();
            string iniFilePath = new CaseAwarePath(tslPatchDataPath, namespaceOption.ChangesFilePath()).GetResolvedPath();
            string namespaceModPath = Path.GetDirectoryName(iniFilePath) ?? tslPatchDataPath;

            var installer = new ModInstaller(namespaceModPath, gamePath, iniFilePath, logger)
            {
                TslPatchDataPath = tslPatchDataPath
            };

            DateTime installStartTime = DateTime.UtcNow;
            installer.Install(cancellationToken, progressCallback);
            TimeSpan totalInstallTime = DateTime.UtcNow - installStartTime;

            int numErrors = logger.Errors.Count();
            int numWarnings = logger.Warnings.Count();
            int numPatches = installer.Config().PatchCount();

            string timeStr = FormatInstallTime(totalInstallTime);
            logger.AddNote(
                $"The installation is complete with {numErrors} errors and {numWarnings} warnings.{Environment.NewLine}" +
                $"Total install time: {timeStr}{Environment.NewLine}" +
                $"Total patches: {numPatches}");

            return new InstallResult
            {
                InstallTime = totalInstallTime,
                NumErrors = numErrors,
                NumWarnings = numWarnings,
                NumPatches = numPatches
            };
        }

        /// <summary>
        /// Validates a mod's configuration.
        /// </summary>
        public static void ValidateConfig(
            string modPath,
            List<PatcherNamespace> namespaces,
            string selectedNamespaceName,
            PatchLogger logger)
        {
            // Can be null if namespace not found
            PatcherNamespace namespaceOption = namespaces.FirstOrDefault(x => x.Name == selectedNamespaceName);
            if (namespaceOption is null)
            {
                throw new ArgumentException($"Namespace '{selectedNamespaceName}' not found in namespaces list");
            }

            string iniFilePath = new CaseAwarePath(modPath, "tslpatchdata", namespaceOption.ChangesFilePath()).GetResolvedPath();
            string tslPatchDataPath = new CaseAwarePath(modPath, "tslpatchdata").GetResolvedPath();

            var reader = ConfigReader.FromFilePath(iniFilePath, logger, tslPatchDataPath: tslPatchDataPath);
            reader.Load(reader.Config);
        }

        /// <summary>
        /// Uninstalls a mod using its backup.
        /// </summary>
        public static bool UninstallMod(
            string modPath,
            string gamePath,
            PatchLogger logger,
            [CanBeNull] Func<string, string, bool> showYesNoDialog = null,
            [CanBeNull] Func<string, string, bool?> showYesNoCancelDialog = null,
            [CanBeNull] Action<string, string> showErrorDialog = null)
        {
            string backupParentFolder = Path.Combine(modPath, "backup");
            if (!Directory.Exists(backupParentFolder))
            {
                throw new DirectoryNotFoundException($"Backup folder not found: {backupParentFolder}");
            }

            var uninstaller = new ModUninstaller(
                new CaseAwarePath(backupParentFolder),
                new CaseAwarePath(gamePath),
                logger);

            return uninstaller.UninstallSelectedMod(
                showErrorDialog: showErrorDialog ?? ((title, msg) => { }),
                showYesNoDialog: showYesNoDialog ?? ((title, msg) => true),
                showYesNoCancelDialog: showYesNoCancelDialog ?? ((title, msg) => true));
        }

        /// <summary>
        /// Formats an installation time as a human-readable string.
        /// </summary>
        public static string FormatInstallTime(TimeSpan installTime)
        {
            int days = (int)installTime.TotalDays;
            int hours = installTime.Hours;
            int minutes = installTime.Minutes;
            int seconds = installTime.Seconds;

            var parts = new List<string>();
            if (days > 0) { parts.Add($"{days} days"); }
            if (hours > 0) { parts.Add($"{hours} hours"); }
            if (minutes > 0 || (days == 0 && hours == 0)) { parts.Add($"{minutes} minutes"); }
            parts.Add($"{seconds} seconds");

            return string.Join(", ", parts);
        }

        /// <summary>
        /// Gets the log file path for a mod.
        /// </summary>
        public static string GetLogFilePath(string modPath)
        {
            return Path.Combine(modPath, "installlog.txt");
        }

        /// <summary>
        /// Finds KOTOR installation paths from default locations and registry.
        /// Matches Python's find_kotor_paths_from_default() implementation.
        /// Searches:
        /// - Windows: Registry (Steam, GOG, retail), common installation directories
        /// - macOS: Steam, App Store locations
        /// - Linux: Steam, Flatpak, WSL paths
        /// </summary>
        private static Dictionary<Game, List<string>> FindKotorPathsFromDefault()
        {
            var paths = new Dictionary<Game, List<string>>
            {
                { Game.K1, new List<string>() },
                { Game.TSL, new List<string>() }
            };

            // Get platform-specific default paths
            Dictionary<Game, List<string>> defaultPaths = GetDefaultPaths();

            // Check each default path for existence
            foreach (KeyValuePair<Game, List<string>> kvp in defaultPaths)
            {
                Game game = kvp.Key;
                foreach (string path in kvp.Value)
                {
                    string expandedPath = ExpandPath(path);
                    if (Directory.Exists(expandedPath) && !paths[game].Contains(expandedPath))
                    {
                        paths[game].Add(expandedPath);
                    }
                }
            }

            // On Windows, also check registry for installed game paths
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                SearchWindowsRegistry(paths);
            }

            return paths;
        }

        /// <summary>
        /// Gets default KOTOR installation paths for the current platform.
        /// </summary>
        private static Dictionary<Game, List<string>> GetDefaultPaths()
        {
            var paths = new Dictionary<Game, List<string>>
            {
                { Game.K1, new List<string>() },
                { Game.TSL, new List<string>() }
            };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows K1 paths
                paths[Game.K1].AddRange(new[]
                {
                    @"C:\Program Files\Steam\steamapps\common\swkotor",
                    @"C:\Program Files (x86)\Steam\steamapps\common\swkotor",
                    @"C:\Program Files\LucasArts\SWKotOR",
                    @"C:\Program Files (x86)\LucasArts\SWKotOR",
                    @"C:\GOG Games\Star Wars - KotOR",
                    @"C:\Amazon Games\Library\Star Wars - Knights of the Old",
                });

                // Windows K2/TSL paths
                paths[Game.TSL].AddRange(new[]
                {
                    @"C:\Program Files\Steam\steamapps\common\Knights of the Old Republic II",
                    @"C:\Program Files (x86)\Steam\steamapps\common\Knights of the Old Republic II",
                    @"C:\Program Files\LucasArts\SWKotOR2",
                    @"C:\Program Files (x86)\LucasArts\SWKotOR2",
                    @"C:\GOG Games\Star Wars - KotOR2",
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // macOS K1 paths
                paths[Game.K1].AddRange(new[]
                {
                    "~/Library/Application Support/Steam/steamapps/common/swkotor/Knights of the Old Republic.app/Contents/Assets",
                    "~/Library/Applications/Steam/steamapps/common/swkotor/Knights of the Old Republic.app/Contents/Assets/",
                });

                // macOS K2/TSL paths
                paths[Game.TSL].AddRange(new[]
                {
                    "~/Library/Application Support/Steam/steamapps/common/Knights of the Old Republic II/Knights of the Old Republic II.app/Contents/Assets",
                    "~/Library/Applications/Steam/steamapps/common/Knights of the Old Republic II/Star Wars™: Knights of the Old Republic II.app/Contents/GameData",
                    "~/Library/Application Support/Steam/steamapps/common/Knights of the Old Republic II/KOTOR2.app/Contents/GameData/",
                    "~/Applications/Knights of the Old Republic 2.app/Contents/Resources/transgaming/c_drive/Program Files/SWKotOR2/",
                    "/Applications/Knights of the Old Republic 2.app/Contents/Resources/transgaming/c_drive/Program Files/SWKotOR2/",
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Linux K1 paths
                paths[Game.K1].AddRange(new[]
                {
                    "~/.local/share/steam/common/steamapps/swkotor",
                    "~/.local/share/steam/common/swkotor",
                    "~/.steam/debian-installation/steamapps/common/swkotor",
                    "~/.steam/root/steamapps/common/swkotor",
                    // Flatpak
                    "~/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/swkotor",
                    // WSL paths
                    "/mnt/c/Program Files/Steam/steamapps/common/swkotor",
                    "/mnt/c/Program Files (x86)/Steam/steamapps/common/swkotor",
                    "/mnt/c/Program Files/LucasArts/SWKotOR",
                    "/mnt/c/Program Files (x86)/LucasArts/SWKotOR",
                    "/mnt/c/GOG Games/Star Wars - KotOR",
                    "/mnt/c/Amazon Games/Library/Star Wars - Knights of the Old",
                });

                // Linux K2/TSL paths
                paths[Game.TSL].AddRange(new[]
                {
                    "~/.local/share/Steam/common/steamapps/Knights of the Old Republic II",
                    "~/.local/share/Steam/common/steamapps/kotor2",
                    "~/.local/share/aspyr-media/kotor2",
                    "~/.local/share/aspyr-media/Knights of the Old Republic II",
                    "~/.local/share/Steam/common/Knights of the Old Republic II",
                    "~/.steam/debian-installation/steamapps/common/Knights of the Old Republic II",
                    "~/.steam/debian-installation/steamapps/common/kotor2",
                    "~/.steam/root/steamapps/common/Knights of the Old Republic II",
                    // Flatpak
                    "~/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Knights of the Old Republic II/steamassets",
                    // WSL paths
                    "/mnt/c/Program Files/Steam/steamapps/common/Knights of the Old Republic II",
                    "/mnt/c/Program Files (x86)/Steam/steamapps/common/Knights of the Old Republic II",
                    "/mnt/c/Program Files/LucasArts/SWKotOR2",
                    "/mnt/c/Program Files (x86)/LucasArts/SWKotOR2",
                    "/mnt/c/GOG Games/Star Wars - KotOR2",
                });
            }

            return paths;
        }

        /// <summary>
        /// Expands path variables like ~ to the user's home directory.
        /// </summary>
        private static string ExpandPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            // Expand ~ to home directory
            if (path.StartsWith("~"))
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                path = Path.Combine(home, path.Substring(1).TrimStart('/', '\\'));
            }

            return Path.GetFullPath(path);
        }

        /// <summary>
        /// Searches the Windows registry for KOTOR installation paths.
        /// Checks Steam, GOG, and retail CD/DVD installations.
        /// </summary>
        private static void SearchWindowsRegistry(Dictionary<Game, List<string>> paths)
        {
#if WINDOWS
            // Registry paths for K1
            var k1RegistryPaths = new (string keyPath, string valueName)[]
            {
                // Steam
                (@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 32370", "InstallLocation"),
                // GOG (64-bit)
                (@"SOFTWARE\WOW6432Node\GOG.com\Games\1207666283", "PATH"),
                // GOG (32-bit)
                (@"SOFTWARE\GOG.com\Games\1207666283", "PATH"),
                // Retail (64-bit)
                (@"SOFTWARE\WOW6432Node\BioWare\SW\KOTOR", "Path"),
                (@"SOFTWARE\WOW6432Node\BioWare\SW\KOTOR", "InternalPath"),
                // Retail (32-bit)
                (@"SOFTWARE\BioWare\SW\KOTOR", "Path"),
                (@"SOFTWARE\BioWare\SW\KOTOR", "InternalPath"),
            };

            // Registry paths for K2/TSL
            var k2RegistryPaths = new (string keyPath, string valueName)[]
            {
                // Steam
                (@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 208580", "InstallLocation"),
                // GOG (64-bit)
                (@"SOFTWARE\WOW6432Node\GOG.com\Games\1421404581", "PATH"),
                // GOG (32-bit)
                (@"SOFTWARE\GOG.com\Games\1421404581", "PATH"),
                // Retail (64-bit)
                (@"SOFTWARE\WOW6432Node\LucasArts\KotOR2", "Path"),
                (@"SOFTWARE\WOW6432Node\LucasArts\KotOR2", "InternalPath"),
                // Retail (32-bit)
                (@"SOFTWARE\LucasArts\KotOR2", "Path"),
                (@"SOFTWARE\LucasArts\KotOR2", "InternalPath"),
            };

            // Search K1 registry paths
            foreach (var (keyPath, valueName) in k1RegistryPaths)
            {
                string path = GetRegistryValue(keyPath, valueName);
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path) && !paths[Game.K1].Contains(path))
                {
                    paths[Game.K1].Add(path);
                }
            }

            // Search K2 registry paths
            foreach (var (keyPath, valueName) in k2RegistryPaths)
            {
                string path = GetRegistryValue(keyPath, valueName);
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path) && !paths[Game.TSL].Contains(path))
                {
                    paths[Game.TSL].Add(path);
                }
            }

            // Search Amazon Games for K1 (stored in HKEY_USERS)
            string amazonK1Path = FindAmazonGamesPath("Star Wars - Knights of the Old");
            if (!string.IsNullOrEmpty(amazonK1Path) && Directory.Exists(amazonK1Path) && !paths[Game.K1].Contains(amazonK1Path))
            {
                paths[Game.K1].Add(amazonK1Path);
            }
#endif
        }

#if WINDOWS
        /// <summary>
        /// Gets a registry value from HKEY_LOCAL_MACHINE.
        /// </summary>
        [CanBeNull]
        private static string GetRegistryValue(string keyPath, string valueName)
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath))
                {
                    if (key != null)
                    {
                        object value = key.GetValue(valueName);
                        if (value is string stringValue)
                        {
                            return stringValue;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Registry access may fail due to permissions or missing keys
            }
            return null;
        }

        /// <summary>
        /// Searches HKEY_USERS for Amazon Games installation paths.
        /// Amazon Games stores installation info under user-specific registry keys.
        /// </summary>
        [CanBeNull]
        private static string FindAmazonGamesPath(string softwareName)
        {
            try
            {
                using (RegistryKey usersKey = Registry.Users)
                {
                    foreach (string sidName in usersKey.GetSubKeyNames())
                    {
                        try
                        {
                            string softwarePath = $@"{sidName}\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\AmazonGames/{softwareName}";
                            using (RegistryKey softwareKey = usersKey.OpenSubKey(softwarePath))
                            {
                                if (softwareKey != null)
                                {
                                    object value = softwareKey.GetValue("InstallLocation");
                                    if (value is string stringValue && !string.IsNullOrEmpty(stringValue))
                                    {
                                        return stringValue;
                                    }
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // Skip inaccessible user keys
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Registry access may fail
            }
            return null;
        }
#endif

        /// <summary>
        /// Decodes bytes with fallback encodings.
        /// </summary>
        private static string DecodeBytesWithFallbacks(byte[] data)
        {
            // Try UTF-8 first
            try
            {
                return Encoding.UTF8.GetString(data);
            }
            catch
            {
                // Fallback to Windows-1252
                try
                {
                    Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                    return Encoding.GetEncoding("windows-1252").GetString(data);
                }
                catch
                {
                    // Final fallback to ASCII
                    return Encoding.ASCII.GetString(data);
                }
            }
        }

        /// <summary>
        /// Strips RTF formatting from text.
        /// Uses RtfStripper class which matches Python's striprtf implementation.
        /// </summary>
        private static string StripRtf(string rtfText)
        {
            return RtfStripper.StripRtf(rtfText);
        }
    }
}


