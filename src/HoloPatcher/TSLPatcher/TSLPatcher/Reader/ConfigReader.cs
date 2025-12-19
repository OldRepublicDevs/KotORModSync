using System;
using System.Numerics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KOTORModSync;
using KOTORModSync.Common;
using KOTORModSync.Config;
using KOTORModSync.Formats.GFF;
using KOTORModSync.Formats.SSF;
using KOTORModSync.Logger;
using KOTORModSync.Memory;
using KOTORModSync.Mods;
using KOTORModSync.Mods.GFF;
using KOTORModSync.Mods.NCS;
using KOTORModSync.Mods.NSS;
using KOTORModSync.Mods.SSF;
using KOTORModSync.Mods.TLK;
using KOTORModSync.Mods.TwoDA;
using IniParser;
using IniParser.Model;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Reader
{

    /// <summary>
    /// Reads and parses TSLPatcher configuration files (changes.ini).
    /// Complete implementation matching Python reader.py exactly, including all comments.
    /// </summary>
    public class ConfigReader
    {
        private const string SectionNotFoundError = "The [{0}] section was not found in the ini";
        private const string ReferencesTracebackMsg = ", referenced by '{0}={1}' in [{2}]";

        private readonly HashSet<string> _previouslyParsedSections = new HashSet<string>();
        private readonly IniData _ini;
        private readonly string _modPath;
        // path to the tslpatchdata, optional but we'll use it here for the nwnnsscomp.exe if it exists.
        [CanBeNull]
        private readonly string _tslPatchDataPath;
        private readonly PatchLogger _log;

        public PatcherConfig Config { get; private set; }

        /// <summary>
        /// Initializes a new instance of ConfigReader.
        /// </summary>
        public ConfigReader(
            IniData ini,
            string modPath,
            [CanBeNull] PatchLogger logger = null,
            [CanBeNull] string tslPatchDataPath = null)
        {
            _previouslyParsedSections = new HashSet<string>();
            _ini = ini ?? throw new ArgumentNullException(nameof(ini));
            _modPath = modPath ?? throw new ArgumentNullException(nameof(modPath));
            // path to the tslpatchdata, optional but we'll use it here for the nwnnsscomp.exe if it exists.
            _tslPatchDataPath = tslPatchDataPath;
            _log = logger ?? new PatchLogger();
        }

        /// <summary>
        /// Unified function to load and parse an INI file with preprocessing.
        /// This ensures all INI files are consistently preprocessed (comments stripped) before parsing.
        /// </summary>
        /// <param name="filePath">Path to the INI file to load</param>
        /// <param name="caseInsensitive">Whether section and key names should be case-insensitive (default: false)</param>
        /// <returns>Parsed IniData object</returns>
        public static IniData LoadAndParseIni(string filePath, bool caseInsensitive = false)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"INI file not found: {filePath}", filePath);
            }

            string resolvedFilePath = Path.GetFullPath(filePath);
            string iniText = File.ReadAllText(resolvedFilePath);

            return ParseIniText(iniText, caseInsensitive, resolvedFilePath);
        }

        /// <summary>
        /// Unified function to parse INI text with preprocessing.
        /// This ensures all INI text is consistently preprocessed (comments stripped) before parsing.
        /// </summary>
        /// <param name="iniText">The INI text content to parse</param>
        /// <param name="caseInsensitive">Whether section and key names should be case-insensitive (default: false)</param>
        /// <param name="sourcePath">Optional file path for error messages (default: null)</param>
        /// <returns>Parsed IniData object</returns>
        public static IniData ParseIniText(string iniText, bool caseInsensitive = false, string sourcePath = null)
        {
            if (string.IsNullOrEmpty(iniText))
            {
                throw new ArgumentException("INI text cannot be null or empty", nameof(iniText));
            }

            var parser = new IniParser.Parser.IniDataParser();
            parser.Configuration.AllowDuplicateKeys = true;
            parser.Configuration.AllowDuplicateSections = true;
            parser.Configuration.CaseInsensitive = caseInsensitive;
            parser.Configuration.CommentString = ";";
            parser.Configuration.CommentRegex = new System.Text.RegularExpressions.Regex(@"^[;#]");

            // Preprocess to remove full-line comments and inline comments on section headers
            iniText = PreprocessIniText(iniText);

            try
            {
                return parser.Parse(iniText);
            }
            catch (Exception ex)
            {
                string errorPath = sourcePath ?? "provided text";
                throw new InvalidOperationException($"Error parsing INI file: {errorPath}", ex);
            }
        }

        /// <summary>
        /// Preprocesses INI text to remove comment lines (lines that start with ; or # after trimming whitespace).
        /// This prevents the parser from trying to parse comment lines that look like section headers.
        /// Also handles inline comments on section header lines.
        /// </summary>
        public static string PreprocessIniText(string iniText)
        {
            if (string.IsNullOrEmpty(iniText))
            {
                return iniText;
            }

            var lines = new System.Collections.Generic.List<string>();
            string[] originalLines = iniText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            foreach (string line in originalLines)
            {
                string trimmed = line.TrimStart();

                // Remove lines that are entirely comments (start with ; or # after trimming leading whitespace)
                if (trimmed.Length > 0 && (trimmed[0] == ';' || trimmed[0] == '#'))
                {
                    // This is a full-line comment, skip it
                    continue;
                }

                // Handle inline comments on section header lines (e.g., "[Settings] ; comment")
                // The IniParser library doesn't support inline comments after section headers
                if (trimmed.StartsWith("[") && trimmed.Contains("]"))
                {
                    int closingBracketIndex = trimmed.IndexOf(']');
                    if (closingBracketIndex > 0 && closingBracketIndex < trimmed.Length - 1)
                    {
                        // Check if there's a comment after the closing bracket
                        string afterBracket = trimmed.Substring(closingBracketIndex + 1).TrimStart();
                        if (afterBracket.Length > 0 && (afterBracket[0] == ';' || afterBracket[0] == '#'))
                        {
                            // Remove the comment part, keep only the section header
                            string sectionHeader = trimmed.Substring(0, closingBracketIndex + 1);
                            // Preserve leading whitespace from original line
                            string leadingWhitespace = line.Substring(0, line.Length - line.TrimStart().Length);
                            lines.Add(leadingWhitespace + sectionHeader);
                            continue;
                        }
                    }
                }

                lines.Add(line);
            }

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// Load PatcherConfig from an INI file path.
        ///
        /// Args:
        /// ----
        ///     file_path: The path to the INI file.
        ///     logger: Optional logger instance.
        ///     tslpatchdata_path: Optional path to the tslpatchdata directory.
        ///
        /// Returns:
        /// -------
        ///     A PatcherConfig instance loaded from the file.
        ///
        /// Processing Logic:
        /// ----------------
        ///     - Resolve the file path and load its contents
        ///     - Parse the INI text into a ConfigParser
        ///     - Initialize a PatcherConfig instance
        ///     - Populate its config attribute from the ConfigParser
        ///     - Return the initialized instance
        /// </summary>
        public static ConfigReader FromFilePath(
            string filePath,
            [CanBeNull] PatchLogger logger = null,
            [CanBeNull] string tslPatchDataPath = null)
        {
            string resolvedFilePath = Path.GetFullPath(filePath);

            // Use unified INI loader (case-sensitive for changes.ini files)
            IniData ini = LoadAndParseIni(resolvedFilePath, caseInsensitive: false);

            var instance = new ConfigReader(ini, Path.GetDirectoryName(resolvedFilePath) ?? string.Empty, logger, tslPatchDataPath);
            instance.Config = new PatcherConfig();
            return instance;
        }

        /// <summary>
        /// Load all configuration sections.
        /// </summary>
        public PatcherConfig Load(PatcherConfig config)
        {
            Config = config;
            _previouslyParsedSections.Clear();

            LoadSettings();
            LoadTLKList();
            LoadInstallList();
            Load2DAList();
            LoadGFFList();
            LoadCompileList();
            LoadHackList();
            LoadSSFList();
            _log.AddNote("The ConfigReader finished loading the INI");
            var allSectionsSet = _ini.Sections.Select(s => s.SectionName).ToHashSet();
            var orphanedSections = allSectionsSet.Except(_previouslyParsedSections).ToHashSet();
            if (orphanedSections.Count > 0)
            {
                string orphanedSectionsStr = string.Join("\n", orphanedSections);
                _log.AddNote($"There are some orphaned ini sections found in the changes:\n{orphanedSectionsStr}");
            }

            return Config;
        }

        /// <summary>
        /// Resolves the case-insensitive section name string if found and returns the case-sensitive correct section name.
        /// </summary>
        [CanBeNull]
        private string GetSectionName(string sectionName)
        {
            // Can be null if section not found
            string s = _ini.Sections.FirstOrDefault(section =>
                section.SectionName.Equals(sectionName, StringComparison.OrdinalIgnoreCase))?.SectionName;
            if (s != null)
            {
                _previouslyParsedSections.Add(s);
            }
            return s;
        }

        /// <summary>
        /// Loads [Settings] from ini configuration into memory.
        /// </summary>
        private void LoadSettings()
        {
            // Can be null if section not found
            string settingsSection = GetSectionName("settings");
            if (settingsSection is null)
            {
                _log.AddWarning("[Settings] section missing from ini.");
                return;
            }

            _log.AddNote("Loading [Settings] section from ini...");
            Dictionary<string, string> settingsIni = SectionToDictionary(_ini[settingsSection]);

            Config.WindowTitle = settingsIni.GetValueOrDefault("WindowCaption", "");
            Config.ConfirmMessage = settingsIni.GetValueOrDefault("ConfirmMessage", "");
            foreach ((string key, string value) in settingsIni)
            {
                string lowerKey = key.ToLower();
                if (lowerKey == "required" || (lowerKey.StartsWith("required") && key.Length > "required".Length && !key.Substring("required".Length).ToLower().StartsWith("msg")))
                {
                    if (lowerKey != "required" && !key.Substring("required".Length).All(char.IsDigit))
                    {
                        throw new InvalidOperationException($"Key '{key}' improperly defined in settings ini. Expected (Required) or (RequiredMsg)");
                    }
                    string[] theseFiles = value.Split(',').Select(filename => filename.Trim()).ToArray();
                    Config.RequiredFiles.Add(theseFiles);
                }

                if (lowerKey == "requiredmsg" || (lowerKey.StartsWith("requiredmsg") && key.Length > "requiredmsg".Length))
                {
                    if (lowerKey != "requiredmsg" && !key.Substring("requiredmsg".Length).All(char.IsDigit))
                    {
                        throw new InvalidOperationException($"Key '{key}' improperly defined in settings ini. Expected (Required) or (RequiredMsg)");
                    }
                    Config.RequiredMessages.Add(value.Trim());
                }
            }
            if (Config.RequiredFiles.Count != Config.RequiredMessages.Count)
            {
                throw new InvalidOperationException($"Required files definitions must match required msg count ({Config.RequiredFiles.Count}/{Config.RequiredMessages.Count})");
            }
            Config.SaveProcessedScripts = int.TryParse(settingsIni.GetValueOrDefault("SaveProcessedScripts"), out int sps) ? sps : 0;
            Config.LogLevel = int.TryParse(settingsIni.GetValueOrDefault("LogLevel"), out int logLevelInt)
                ? (LogLevel)logLevelInt
                : LogLevel.Warnings;

            // HoloPatcher optional
            Config.IgnoreFileExtensions = bool.TryParse(settingsIni.GetValueOrDefault("IgnoreExtensions"), out bool ign) && ign;

            // Can be null if key not found
            string lookupGameNumber = settingsIni.GetValueOrDefault("LookupGameNumber");
            if (lookupGameNumber != null)
            {
                lookupGameNumber = lookupGameNumber.Trim();
                if (lookupGameNumber != "1" && lookupGameNumber != "2")
                {
                    throw new InvalidOperationException($"Invalid: 'LookupGameNumber={lookupGameNumber}' in [Settings], must be 1 or 2 representing the KOTOR game.");
                }
                Config.GameNumber = ParseIntValue(lookupGameNumber);
            }
            else
            {
                Config.GameNumber = null;
            }
        }

        /// <summary>
        /// Loads [InstallList] from ini configuration into memory.
        ///
        /// Processing Logic:
        /// ----------------
        ///     - Gets [InstallList] section from ini
        ///     - Loops through section items getting foldername and filenames
        ///     - Gets section for each filename
        ///     - Creates InstallFile object for each filename
        ///     - Adds InstallFile to config install list
        ///     - Optionally loads additional vars from filename section
        /// </summary>
        private void LoadInstallList()
        {
            // Can be null if section not found
            string installListSection = GetSectionName("InstallList");
            if (installListSection is null)
            {
                _log.AddNote("[InstallList] section missing from ini.");
                return;
            }

            _log.AddNote("Loading [InstallList] patches from ini...");
            foreach ((string folderKey, string foldername) in _ini[installListSection].Select(k => (k.KeyName, k.Value)))
            {
                // Can be null if section not found
                string foldernameSection = GetSectionName(folderKey)
                    ?? throw new KeyNotFoundException(string.Format(SectionNotFoundError, foldername) + string.Format(ReferencesTracebackMsg, folderKey, foldername, installListSection));
                Dictionary<string, string> folderSectionDict = SectionToDictionary(_ini[foldernameSection]);
                // !SourceFolder: Relative path from mod_path (which is typically the tslpatchdata folder) to source files.
                // Default value "." refers to mod_path itself (the tslpatchdata folder), not its parent.
                // For example: if mod_path = "C:/Mod/tslpatchdata", then:
                //   - !SourceFolder="." resolves to "C:/Mod/tslpatchdata"
                //   - !SourceFolder="textures" resolves to "C:/Mod/tslpatchdata/textures"
                // Can be null if key not found
                string sourcefolder = folderSectionDict.TryGetValue("!SourceFolder", out string sf) ? sf : ".";
                folderSectionDict.Remove("!SourceFolder");
                foreach ((string fileKey, string filename) in folderSectionDict)
                {
                    var fileInstall = new InstallFile(
                        filename,
                        fileKey.ToLower().StartsWith("replace"))
                    {
                        Destination = foldername,
                        SourceFolder = sourcefolder
                    };
                    Config.InstallList.Add(fileInstall);

                    // Optional according to TSLPatcher readme
                    // Can be null if section not found
                    string fileSectionName = GetSectionName(filename);
                    if (fileSectionName != null)
                    {
                        Dictionary<string, string> fileSectionDict = SectionToDictionary(_ini[fileSectionName]);
                        fileInstall.PopTslPatcherVars(fileSectionDict, foldername, sourcefolder);
                    }
                }
            }
        }

        /// <summary>
        /// Loads TLK patches from the ini file into memory.
        ///
        /// Processing Logic:
        /// ----------------
        ///     - Parses the [TLKList] section to get TLK patch entries
        ///     - Handles different patch syntaxes like file replacements, string references etc
        ///     - Builds ModifyTLK objects for each patch and adds to the patch list
        ///     - Raises errors for invalid syntax or missing files
        /// </summary>
        private void LoadTLKList()
        {
            // Can be null if section not found
            string tlkListSection = GetSectionName("tlklist");
            if (tlkListSection is null)
            {
                _log.AddNote("[TLKList] section missing from ini.");
                return;
            }

            _log.AddNote("Loading [TLKList] patches from ini...");
            Dictionary<string, string> tlkListEdits = SectionToDictionary(_ini[tlkListSection]);

            // Can be null if key not found
            string defaultDestination = tlkListEdits.TryGetValue("!DefaultDestination", out string dd) ? dd : ModificationsTLK.DefaultDestination;
            tlkListEdits.Remove("!DefaultDestination");
            // !DefaultSourceFolder: Relative path from mod_path (which is typically the tslpatchdata folder) to source files.
            // Default value "." refers to mod_path itself (the tslpatchdata folder), not its parent.
            // For example: if mod_path = "C:/Mod/tslpatchdata", then:
            //   - !DefaultSourceFolder="." resolves to "C:/Mod/tslpatchdata"
            //   - !DefaultSourceFolder="textures" resolves to "C:/Mod/tslpatchdata/textures"
            // Can be null if key not found
            string defaultSourcefolder = tlkListEdits.TryGetValue("!DefaultSourceFolder", out string dsf) ? dsf : ".";
            tlkListEdits.Remove("!DefaultSourceFolder");
            Config.PatchesTLK.PopTslPatcherVars(tlkListEdits, defaultDestination, defaultSourcefolder);

            bool syntaxErrorCaught = false;

            void ProcessTlkEntries(string tlkFilename, int dialogTlkIndex, int modTlkIndex, bool isReplacement)
            {
                var modifier = new ModifyTLK(dialogTlkIndex, isReplacement)
                {
                    ModIndex = modTlkIndex
                };
                // Path resolution: mod_path / sourcefolder / tlk_filename
                // mod_path is typically the tslpatchdata folder (parent of changes.ini).
                // If sourcefolder = ".", this resolves to mod_path itself (tslpatchdata folder).
                string basePathRoot = _tslPatchDataPath ?? _modPath;
                string basePath = Config.PatchesTLK.SourceFolder == "."
                    ? basePathRoot
                    : Path.Combine(basePathRoot, Config.PatchesTLK.SourceFolder);
                string tlkFilePath = Path.Combine(basePath, tlkFilename);
                if (!File.Exists(tlkFilePath) && _tslPatchDataPath != null)
                {
                    tlkFilePath = Path.Combine(_modPath, tlkFilename);
                }
                // Make path absolute to match Python behavior (Path objects are always absolute)
                modifier.TlkFilePath = Path.GetFullPath(tlkFilePath);
                Config.PatchesTLK.Modifiers.Add(modifier);
            }

            foreach ((string key, string value) in tlkListEdits)
            {
                string lowerKey = key.ToLower();
                bool replaceFile = lowerKey.StartsWith("replace");
                bool appendFile = lowerKey.StartsWith("append");
                try
                {
                    if (lowerKey.StartsWith("strref"))
                    {
                        ProcessTlkEntries(
                            tlkFilename: Config.PatchesTLK.SourceFile,
                            dialogTlkIndex: ParseIntValue(lowerKey.Substring(6)),
                            modTlkIndex: ParseIntValue(value),
                            isReplacement: false);
                    }
                    else if (replaceFile || appendFile)
                    {
                        // Can be null if section not found
                        string nextSectionName = GetSectionName(value);
                        if (nextSectionName is null)
                        {
                            syntaxErrorCaught = true;
                            throw new InvalidOperationException(string.Format(SectionNotFoundError, value) + string.Format(ReferencesTracebackMsg, key, value, tlkListSection));
                        }

                        Dictionary<string, string> nextSectionDict = SectionToDictionary(_ini[nextSectionName]);
                        Config.PatchesTLK.PopTslPatcherVars(nextSectionDict, defaultDestination, defaultSourcefolder);

                        foreach ((string rawDialogTlkIndex, string rawModTlkIndex) in _ini[nextSectionName].Select(k => (k.KeyName, k.Value)))
                        {
                            int dialogTlkIndex = rawDialogTlkIndex.ToLower().StartsWith("strref") ? ParseIntValue(rawDialogTlkIndex.Substring(6)) : ParseIntValue(rawDialogTlkIndex);
                            int modTlkIndex = rawModTlkIndex.ToLower().StartsWith("strref") ? ParseIntValue(rawModTlkIndex.Substring(6)) : ParseIntValue(rawModTlkIndex);
                            ProcessTlkEntries(
                                tlkFilename: nextSectionName,
                                dialogTlkIndex: dialogTlkIndex,
                                modTlkIndex: modTlkIndex,
                                isReplacement: replaceFile);
                        }
                    }
                    else if (lowerKey.Contains('\\') || lowerKey.Contains('/'))
                    {
                        char delimiter = lowerKey.Contains('\\') ? '\\' : '/';
                        string[] parts = lowerKey.Split(delimiter);
                        string tokenIdStr = parts[0];
                        string propertyName = parts[1];
                        int tokenId = ParseIntValue(tokenIdStr);

                        if (propertyName == "text")
                        {
                            var modifier = new ModifyTLK(tokenId, isReplacement: true) { Text = value };
                            Config.PatchesTLK.Modifiers.Add(modifier);
                        }
                        else if (propertyName == "sound")
                        {
                            var modifier = new ModifyTLK(tokenId, isReplacement: true) { Sound = new ResRef(value) };
                            Config.PatchesTLK.Modifiers.Add(modifier);
                        }
                        else
                        {
                            syntaxErrorCaught = true;
                            throw new InvalidOperationException($"Invalid [TLKList] syntax: '{key}={value}'! Expected '{key}' to be one of ['Sound', 'Text']");
                        }
                    }
                    else
                    {
                        syntaxErrorCaught = true;
                        throw new InvalidOperationException($"Invalid syntax found in [TLKList] '{key}={value}'! Expected '{key}' to be one of ['AppendFile', 'ReplaceFile', '!SourceFile', 'StrRef', 'Text', 'Sound']");
                    }
                }
                catch (Exception ex) when (!syntaxErrorCaught)
                {
                    throw new InvalidOperationException($"Could not parse '{key}={value}' in [TLKList]", ex);
                }
            }
        }

        /// <summary>
        /// Load 2D array patches from ini file into memory.
        ///
        /// Processing Logic:
        /// ----------------
        ///     - Get the section name for the [2DAList] section
        ///     - Load the section into a dictionary
        ///     - Pop the default destination key
        ///     - Iterate through each identifier and file
        ///         - Get the section for the file
        ///         - Create a Modifications2DA object for the file
        ///         - Load the section into a dictionary and populate the object
        ///         - Append the object to the config patches list
        ///         - Iterate through each key and modification ID
        ///             - Get the section for the ID
        ///             - Load the section into a dictionary
        ///             - Discern and add the modifier to the file object.
        /// </summary>
        private void Load2DAList()
        {
            // Can be null if section not found
            string twodaSectionName = GetSectionName("2DAList");
            if (twodaSectionName is null)
            {
                _log.AddNote("[2DAList] section missing from ini.");
                return;
            }

            _log.AddNote("Loading [2DAList] patches from ini...");

            KeyDataCollection twodaSectionData = _ini[twodaSectionName];
            string defaultDestination = twodaSectionData["!DefaultDestination"] ?? Modifications2DA.DefaultDestination;
            string defaultSourceFolder = twodaSectionData["!DefaultSourceFolder"] ?? ".";

            foreach (KeyData tableEntry in twodaSectionData)
            {
                string identifier = tableEntry.KeyName;
                string file = tableEntry.Value;
                if (identifier.StartsWith("!", StringComparison.Ordinal))
                {
                    continue;
                }

                // Can be null if section not found
                string fileSection = GetSectionName(file);
                if (fileSection is null)
                {
                    throw new KeyNotFoundException(string.Format(SectionNotFoundError, file) + string.Format(ReferencesTracebackMsg, identifier, file, twodaSectionName));
                }

                var modifications = new Modifications2DA(file);
                KeyDataCollection fileSectionData = _ini[fileSection];
                Dictionary<string, string> fileSectionDict = SectionToDictionary(fileSectionData);
                modifications.PopTslPatcherVars(fileSectionDict, defaultDestination, defaultSourceFolder);

                foreach (KeyData kv in fileSectionData)
                {
                    string keyLower = kv.KeyName.ToLower();
                    if (keyLower.StartsWith("2damemory") && kv.KeyName.Length > 9 && kv.KeyName.Substring(9).All(char.IsDigit))
                    {
                        int tokenId = ParseIntValue(kv.KeyName.Substring(9));
                        modifications.FileStore2DA[tokenId] = ParseStoreRowValue(kv.Value);
                    }
                    else if (keyLower.StartsWith("strref") && kv.KeyName.Length > 6 && kv.KeyName.Substring(6).All(char.IsDigit))
                    {
                        int tokenId = ParseIntValue(kv.KeyName.Substring(6));
                        modifications.FileStoreTLK[tokenId] = ParseStoreRowValue(kv.Value);
                    }
                }

                Config.Patches2DA.Add(modifications);

                foreach (KeyData kv in fileSectionData)
                {
                    string key = kv.KeyName;
                    string modificationId = kv.Value;
                    if (key.StartsWith("!", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    // Skip modifiers like 2DAMEMORY0, StrRef0, etc. - these are processed by Cells2DA, not as section references
                    string lowerKey = key.ToLower();
                    if (lowerKey.StartsWith("2damemory") || lowerKey.StartsWith("strref"))
                    {
                        continue;
                    }

                    // Can be null if section not found
                    string nextSectionName = GetSectionName(modificationId);
                    if (nextSectionName is null)
                    {
                        throw new KeyNotFoundException(string.Format(SectionNotFoundError, modificationId) + string.Format(ReferencesTracebackMsg, key, modificationId, fileSection));
                    }

                    Dictionary<string, string> modificationIdsDict = SectionToDictionary(_ini[modificationId]);
                    // Can be null if manipulation cannot be created
                    Modify2DA manipulation = Discern2DA(key, modificationId, modificationIdsDict);
                    if (manipulation is null)
                    {
                        // Discern2DA returns null when the modification section is invalid or missing required fields.
                        // This is not necessarily an error - it could be a malformed entry that should be skipped.
                        // Log a warning but continue processing other modifications.
                        _log?.AddWarning(
                            $"Skipping invalid 2DA modification '{modificationId}' in section '{key}'. " +
                            "The modification section may be missing required fields or contain invalid data.");
                        continue;
                    }
                    modifications.Modifiers.Add(manipulation);
                }
            }
        }

        /// <summary>
        /// Loads SSF patches from the ini file into memory.
        ///
        /// Processing Logic:
        /// ----------------
        ///     - Gets the [SSFList] section name from the ini file
        ///     - Checks for [SSFList] section, logs warning if missing
        ///     - Maps sound names to enum values
        ///     - Loops through [SSFList] parsing patches
        ///         - Gets section for each SSF file
        ///         - Creates ModificationsSSF object
        ///         - Parses file section into modifiers
        ///     - Adds ModificationsSSF objects to config patches
        /// </summary>
        private void LoadSSFList()
        {
            // Can be null if section not found
            string ssfListSection = GetSectionName("SSFList");
            if (ssfListSection is null)
            {
                _log.AddNote("[SSFList] section missing from ini.");
                return;
            }

            _log.AddNote("Loading [SSFList] patches from ini...");

            Dictionary<string, string> ssfSectionDict = SectionToDictionary(_ini[ssfListSection]);
            // Can be null if key not found
            string defaultDestination = ssfSectionDict.TryGetValue("!DefaultDestination", out string dd) ? dd : ModificationsSSF.DefaultDestination;
            ssfSectionDict.Remove("!DefaultDestination");
            // !DefaultSourceFolder: Relative path from mod_path (which is typically the tslpatchdata folder) to source files.
            // Default value "." refers to mod_path itself (the tslpatchdata folder), not its parent.
            // For example: if mod_path = "C:/Mod/tslpatchdata", then:
            //   - !DefaultSourceFolder="." resolves to "C:/Mod/tslpatchdata"
            //   - !DefaultSourceFolder="voices" resolves to "C:/Mod/tslpatchdata/voices"
            // Can be null if key not found
            string defaultSourceFolder = ssfSectionDict.TryGetValue("!DefaultSourceFolder", out string dsf) ? dsf : ".";
            ssfSectionDict.Remove("!DefaultSourceFolder");

            foreach ((string identifier, string file) in ssfSectionDict)
            {
                // Can be null if section not found
                string ssfFileSection = GetSectionName(file);
                if (ssfFileSection is null)
                {
                    throw new KeyNotFoundException(string.Format(SectionNotFoundError, file) + string.Format(ReferencesTracebackMsg, identifier, file, ssfListSection));
                }

                bool replace = identifier.ToLower().StartsWith("replace");
                var modifications = new ModificationsSSF(file, replace);
                Config.PatchesSSF.Add(modifications);

                Dictionary<string, string> fileSectionDict = SectionToDictionary(_ini[ssfFileSection]);
                modifications.PopTslPatcherVars(fileSectionDict, defaultDestination, defaultSourceFolder);

                foreach ((string name, string value) in fileSectionDict)
                {
                    TokenUsage newValue;
                    string lowerValue = value.ToLower();
                    if (lowerValue.StartsWith("2damemory"))
                    {
                        int tokenId = ParseIntValue(value.Substring(9));
                        newValue = new TokenUsage2DA(tokenId);
                    }
                    else if (lowerValue.StartsWith("strref"))
                    {
                        int tokenId = ParseIntValue(value.Substring(6));
                        newValue = new TokenUsageTLK(tokenId);
                    }
                    else
                    {
                        newValue = new NoTokenUsage(ParseIntValue(value));
                    }

                    SSFSound sound = ResolveTslPatcherSSFSound(name);
                    var modifier = new ModifySSF(sound, newValue);
                    modifications.Modifiers.Add(modifier);
                }
            }
        }

        /// <summary>
        /// Loads GFF patches from the ini file into memory.
        ///
        /// Processing Logic:
        /// ----------------
        ///     - Gets the "[GFFList]" section from the ini file
        ///     - Loops through each GFF patch defined
        ///         - Gets the section for the individual GFF file
        ///         - Creates a ModificationsGFF object for it
        ///         - Populates variables from the GFF section
        ///         - Loops through each modifier
        ///             - Creates the appropriate modifier object
        ///             - Adds it to the modifications object
        ///     - Adds the fully configured modifications object to the config.
        /// </summary>
        private void LoadGFFList()
        {
            // Can be null if section not found
            string gffListSection = GetSectionName("GFFList");
            if (gffListSection is null)
            {
                _log.AddNote("[GFFList] section missing from ini.");
                return;
            }

            _log.AddNote("Loading [GFFList] patches from ini...");
            Dictionary<string, string> gffSectionDict = SectionToDictionary(_ini[gffListSection]);
            // Can be null if key not found
            string defaultDestination = gffSectionDict.TryGetValue("!DefaultDestination", out string dd) ? dd : ModificationsGFF.DefaultDestination;
            gffSectionDict.Remove("!DefaultDestination");
            // !DefaultSourceFolder: Relative path from mod_path (which is typically the tslpatchdata folder) to source files.
            // Default value "." refers to mod_path itself (the tslpatchdata folder), not its parent.
            // For example: if mod_path = "C:/Mod/tslpatchdata", then:
            //   - !DefaultSourceFolder="." resolves to "C:/Mod/tslpatchdata"
            //   - !DefaultSourceFolder="gff" resolves to "C:/Mod/tslpatchdata/gff"
            // Can be null if key not found
            string defaultSourceFolder = gffSectionDict.TryGetValue("!DefaultSourceFolder", out string dsf) ? dsf : ".";
            gffSectionDict.Remove("!DefaultSourceFolder");

            foreach ((string identifier, string file) in gffSectionDict)
            {
                // Can be null if section not found
                string fileSectionName = GetSectionName(file);
                if (fileSectionName is null)
                {
                    throw new KeyNotFoundException(string.Format(SectionNotFoundError, file) + string.Format(ReferencesTracebackMsg, identifier, file, gffListSection));
                }

                bool replace = identifier.ToLower().StartsWith("replace");
                var modifications = new ModificationsGFF(file, replace: replace);
                Config.PatchesGFF.Add(modifications);

                Dictionary<string, string> fileSectionDict = SectionToDictionary(_ini[fileSectionName]);
                modifications.PopTslPatcherVars(fileSectionDict, defaultDestination, defaultSourceFolder);

                foreach ((string key, string value) in fileSectionDict)
                {
                    // Can be null if modifier cannot be created
                    ModifyGFF modifier;

                    string lowercaseKey = key.ToLower();
                    if (lowercaseKey.StartsWith("addfield"))
                    {
                        // Can be null if section not found
                        string nextGffSection = GetSectionName(value);
                        if (nextGffSection is null)
                        {
                            throw new KeyNotFoundException(string.Format(SectionNotFoundError, value) + string.Format(ReferencesTracebackMsg, key, value, fileSectionName));
                        }

                        Dictionary<string, string> nextSectionDict = SectionToDictionary(_ini[nextGffSection]);
                        modifier = AddFieldGFF(nextGffSection, nextSectionDict);
                    }
                    else if (lowercaseKey.StartsWith("2damemory"))
                    {
                        if (value.ToLower() == "!fieldpath")
                        {
                            // Python: When value is "!FieldPath", check if there's a [!FieldPath] section with Path=
                            // Can be null if section not found
                            string fieldPathSectionName = GetSectionName(value);
                            string path = string.Empty;
                            if (fieldPathSectionName != null)
                            {
                                Dictionary<string, string> fieldPathSection = SectionToDictionary(_ini[fieldPathSectionName]);
                                // Can be null if key not found
                                if (fieldPathSection.TryGetValue("Path", out string pathValue))
                                {
                                    // Python: raw_path: str = ini_data.pop("Path", "").strip()
                                    // Python: path: PureWindowsPath = PureWindowsPath(raw_path)
                                    // Python's ConfigParser reads values as-is, but C# IniParser may escape backslashes
                                    // Unescape double backslashes to match Python's behavior
                                    path = pathValue.Replace("\\\\", "\\");
                                }
                            }
                            modifier = new Memory2DAModifierGFF(
                                file,
                                path,
                                ParseIntValue(key.Substring(9)));
                        }
                        else if (value.ToLower().StartsWith("2damemory"))
                        {
                            modifier = new Memory2DAModifierGFF(
                                file,
                                string.Empty,
                                ParseIntValue(key.Substring(9)),
                                ParseIntValue(value.Substring(9)));
                        }
                        else
                        {
                            throw new InvalidOperationException($"Cannot parse '{key}={value}' in [{identifier}]. GFFList only supports 2DAMEMORY#=!FieldPath and 2DAMEMORY#=2DAMEMORY# assignments");
                        }
                    }
                    else
                    {
                        modifier = ModifyFieldGFF(fileSectionName, key, value);
                    }

                    modifications.Modifiers.Add(modifier);
                }
            }
        }

        /// <summary>
        /// Loads patches from the [CompileList] section of the ini file.
        ///
        /// Processing Logic:
        /// ----------------
        ///     - Parses the [CompileList] section of the ini file into a dictionary
        ///     - Sets a default destination from an optional key
        ///     - Loops through each identifier/file pair
        ///         - Creates a ModificationsNSS object
        ///         - Looks for an optional section for the file
        ///         - Passes any values to populate the patch
        ///     - Adds each patch to the config patches list
        /// </summary>
        private void LoadCompileList()
        {
            // Can be null if section not found
            string compilelistSection = GetSectionName("CompileList");
            if (compilelistSection is null)
            {
                _log.AddNote("[CompileList] section missing from ini.");
                return;
            }

            _log.AddNote("Loading [CompileList] patches from ini...");
            Dictionary<string, string> compilelistSectionDict = SectionToDictionary(_ini[compilelistSection]);
            // Can be null if key not found
            string defaultDestination = compilelistSectionDict.TryGetValue("!DefaultDestination", out string dd) ? dd : ModificationsNSS.DefaultDestination;
            compilelistSectionDict.Remove("!DefaultDestination");
            // !DefaultSourceFolder: Relative path from mod_path (which is typically the tslpatchdata folder) to source files.
            // Default value "." refers to mod_path itself (the tslpatchdata folder), not its parent.
            // For example: if mod_path = "C:/Mod/tslpatchdata", then:
            //   - !DefaultSourceFolder="." resolves to "C:/Mod/tslpatchdata"
            //   - !DefaultSourceFolder="scripts" resolves to "C:/Mod/tslpatchdata/scripts"
            // Can be null if key not found
            string defaultSourceFolder = compilelistSectionDict.TryGetValue("!DefaultSourceFolder", out string dsf) ? dsf : ".";
            compilelistSectionDict.Remove("!DefaultSourceFolder");

            // Path resolution: mod_path / default_source_folder / "nwnnsscomp.exe"
            // mod_path is typically the tslpatchdata folder (parent of changes.ini).
            // If default_source_folder = ".", this resolves to mod_path itself (tslpatchdata folder).
            // Can be null if file doesn't exist
            string nwnnsscompExepath = defaultSourceFolder == "."
                ? Path.Combine(_modPath, "nwnnsscomp.exe")
                : Path.Combine(_modPath, defaultSourceFolder, "nwnnsscomp.exe");
            if (!File.Exists(nwnnsscompExepath))
            {
                nwnnsscompExepath = _tslPatchDataPath != null ? Path.Combine(_tslPatchDataPath, "nwnnsscomp.exe") : null; // TSLPatcher default
            }

            foreach ((string identifier, string file) in compilelistSectionDict)
            {
                bool replace = identifier.ToLower().StartsWith("replace");
                var modifications = new ModificationsNSS(file, replace)
                {
                    Destination = defaultDestination,
                    SourceFolder = defaultSourceFolder
                };

                // Can be null if section not found
                string optionalFileSectionName = GetSectionName(file);
                if (optionalFileSectionName != null)
                {
                    Dictionary<string, string> fileSectionDict = SectionToDictionary(_ini[optionalFileSectionName]);
                    modifications.PopTslPatcherVars(fileSectionDict, defaultDestination, defaultSourceFolder);
                }

                if (nwnnsscompExepath is null)
                {
                    throw new InvalidOperationException($"{nameof(nwnnsscompExepath)}: {nwnnsscompExepath}");
                }
                modifications.NwnnsscompPath = nwnnsscompExepath;
                Config.PatchesNSS.Add(modifications);
            }
        }

        /// <summary>
        /// Loads [HACKList] patches from ini file into memory.
        ///
        /// Processing Logic:
        /// ----------------
        ///     1. Gets the "[HACKList]" section name from the ini file
        ///     2. Loops through each identifier and file in the section
        ///     3. Creates a ModificationsNCS object for each file
        ///     4. Populates the object with offset/value pairs from the file's section
        ///     5. Adds the populated object to the config patches list
        /// </summary>
        private void LoadHackList()
        {
            // Can be null if section not found
            string hacklistSection = GetSectionName("HACKList");
            if (hacklistSection is null)
            {
                _log.AddNote("[HACKList] section missing from ini.");
                return;
            }

            _log.AddNote("Loading [HACKList] patches from ini...");
            Dictionary<string, string> hacklistSectionDict = SectionToDictionary(_ini[hacklistSection]);
            // Can be null if key not found
            string defaultDestination = hacklistSectionDict.TryGetValue("!DefaultDestination", out string dd) ? dd : "Override";
            hacklistSectionDict.Remove("!DefaultDestination");
            // !DefaultSourceFolder: Relative path from mod_path (which is typically the tslpatchdata folder) to source files.
            // Default value "." refers to mod_path itself (the tslpatchdata folder), not its parent.
            // For example: if mod_path = "C:/Mod/tslpatchdata", then:
            //   - !DefaultSourceFolder="." resolves to "C:/Mod/tslpatchdata"
            //   - !DefaultSourceFolder="scripts" resolves to "C:/Mod/tslpatchdata/scripts"
            // Can be null if key not found
            string defaultSourceFolder = hacklistSectionDict.TryGetValue("!DefaultSourceFolder", out string dsf) ? dsf : ".";
            hacklistSectionDict.Remove("!DefaultSourceFolder");

            // Process each NCS file in HACKList
            foreach ((string identifier, string filename) in hacklistSectionDict)
            {
                bool replace = identifier.ToLower().StartsWith("replace");
                var modifications = new ModificationsNCS(filename, replace);

                // Get the file-specific section
                // Can be null if section not found
                string fileSectionName = GetSectionName(filename);
                if (fileSectionName is null)
                {
                    throw new KeyNotFoundException(string.Format(SectionNotFoundError, filename) + string.Format(ReferencesTracebackMsg, identifier, filename, hacklistSection));
                }

                Dictionary<string, string> fileSectionDict = SectionToDictionary(_ini[fileSectionName]);
                modifications.PopTslPatcherVars(fileSectionDict, defaultDestination, defaultSourceFolder);

                // Parse all hack entries for this file
                ParseNCSHackEntries(fileSectionDict, modifications);

                // Add the completed modifications to config
                Config.PatchesNCS.Add(modifications);
            }
        }

        /// <summary>
        /// Parse NCS hack entries from a file section and add them to modifications.
        ///
        /// Args:
        /// ----
        ///     file_section_dict: Dictionary containing offset=value pairs from INI section
        ///     modifications: ModificationsNCS object to populate with ModifyNCS objects
        /// </summary>
        private static void ParseNCSHackEntries(
            Dictionary<string, string> fileSectionDict,
            ModificationsNCS modifications)
        {
            foreach ((string offsetStr, string valueStr) in fileSectionDict)
            {
                // Parse offset (hex or decimal)
                int offset;
                try
                {
                    if (offsetStr.ToLower().StartsWith("0x"))
                    {
                        // Parse as UInt32 first to detect overflow, then convert to Int32
                        uint unsignedValue = Convert.ToUInt32(offsetStr, 16);
                        if (unsignedValue > int.MaxValue)
                        {
                            throw new OverflowException($"Offset value '{offsetStr}' ({unsignedValue}) exceeds Int32.MaxValue ({int.MaxValue})");
                        }
                        offset = (int)unsignedValue;
                    }
                    else
                    {
                        offset = int.Parse(offsetStr, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture);
                    }
                }
                catch (OverflowException ex)
                {
                    throw new OverflowException($"Offset value was either too large or too small for an Int32: '{offsetStr}'", ex);
                }

                // Parse type specifier and value
                string typeSpecifier = "u16"; // Default to 16-bit unsigned
                string parsedValue = valueStr;
                if (valueStr.Contains(':'))
                {
                    string[] parts = valueStr.Split(':', 2);
                    typeSpecifier = parts[0];
                    parsedValue = parts[1];
                }

                string lowerValue = parsedValue.ToLower();

                // Create appropriate hack entry based on value type
                NCSTokenType tokenType;
                int tokenIdOrValue;

                if (lowerValue.StartsWith("strref"))
                {
                    // StrRef token reference
                    tokenIdOrValue = ParseIntValue(parsedValue.Substring(6).Trim());
                    // Check if it's 32-bit (strref32) or 16-bit (strref)
                    tokenType = typeSpecifier.ToLower() == "u32" || typeSpecifier.ToLower() == "i32"
                        ? NCSTokenType.STRREF32
                        : NCSTokenType.STRREF;
                }
                else if (lowerValue.StartsWith("2damemory"))
                {
                    // 2DA memory token reference
                    tokenIdOrValue = ParseIntValue(parsedValue.Substring(9).Trim());
                    // Check if it's 32-bit (2damemory32) or 16-bit (2damemory)
                    tokenType = typeSpecifier.ToLower() == "u32" || typeSpecifier.ToLower() == "i32"
                        ? NCSTokenType.MEMORY_2DA32
                        : NCSTokenType.MEMORY_2DA;
                }
                else
                {
                    // Direct integer values - map type specifier to enum
                    tokenIdOrValue = ParseIntValue(parsedValue);
                    string lowerTypeSpec = typeSpecifier.ToLower();
                    if (lowerTypeSpec == "u8")
                    {
                        tokenType = NCSTokenType.UINT8;
                    }
                    else if (lowerTypeSpec == "u16")
                    {
                        tokenType = NCSTokenType.UINT16;
                    }
                    else if (lowerTypeSpec == "u32")
                    {
                        tokenType = NCSTokenType.UINT32;
                    }
                    else
                    {
                        tokenType = NCSTokenType.UINT16; // Default to 16-bit
                    }
                }

                var modifyNcs = new ModifyNCS(tokenType, offset, tokenIdOrValue);
                modifications.Modifiers.Add(modifyNcs);
            }
        }


        /// <summary>
        /// Parse an integer value from string (hex or decimal).
        ///
        /// Args:
        /// ----
        ///     value_str: String representation of integer (may be hex with 0x prefix)
        ///
        /// Returns:
        /// -------
        ///     int: Parsed integer value
        ///
        /// Raises:
        /// ------
        ///     OverflowException: If the value is too large or too small for an Int32
        /// </summary>
        private static int ParseIntValue(string valueStr)
        {
            try
            {
                if (valueStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    // Parse as UInt32 first to detect overflow, then convert to Int32
                    uint unsignedValue = Convert.ToUInt32(valueStr, 16);
                    if (unsignedValue > int.MaxValue)
                    {
                        throw new OverflowException($"Value '{valueStr}' ({unsignedValue}) exceeds Int32.MaxValue ({int.MaxValue})");
                    }
                    return (int)unsignedValue;
                }
                return int.Parse(valueStr, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (OverflowException ex)
            {
                throw new OverflowException($"Value was either too large or too small for an Int32: '{valueStr}'", ex);
            }
            catch (FormatException ex)
            {
                throw new FormatException($"The value '{valueStr}' is not in a valid format for an integer.", ex);
            }
        }

        /// <summary>
        /// Modifies a field in a GFF based on the key(path) and string value.
        ///
        /// Args:
        /// ----
        ///     identifier: str - The section name (for logging purposes)
        ///     key: str - The key of the field to modify
        ///     string_value: str - The string value to set the field to.
        ///
        /// Returns:
        /// -------
        ///     ModifyFieldGFF - A ModifyFieldGFF object representing the modification
        ///
        /// Processing Logic:
        /// ----------------
        ///     1. Parses the string value into a FieldValue
        ///     2. Handles special cases for keys containing "(strref)", "(lang)" or starting with "2damemory"
        ///     3. Returns a ModifyFieldGFF object representing the modification.
        /// </summary>
        private static ModifyFieldGFF ModifyFieldGFF(
            string identifier,
            string key,
            string strValue)
        {
            FieldValue value = FieldValueFromUnknown(strValue);
            string lowerKey = key.ToLower();
            if (lowerKey.Contains("(strref)"))
            {
                value = new FieldValueConstant(new LocalizedStringDelta(value));
                key = key.Substring(0, lowerKey.IndexOf("(strref)"));
            }
            else if (lowerKey.Contains("(lang"))
            {
                int substringId = ParseIntValue(key.Substring(lowerKey.IndexOf("(lang") + 5, key.Length - lowerKey.IndexOf("(lang") - 6));
                Language language;
                Gender gender;
                LocalizedString.SubstringPair(substringId, out language, out gender);
                var locstring = new LocalizedStringDelta();
                locstring.SetData(language, gender, strValue);
                value = new FieldValueConstant(locstring);
                key = key.Substring(0, lowerKey.IndexOf("(lang"));
            }
            else if (lowerKey.StartsWith("2damemory"))
            {
                string lowerStrValue = strValue.ToLower();
                if (lowerStrValue != "!fieldpath" && !lowerStrValue.StartsWith("2damemory"))
                {
                    throw new InvalidOperationException($"Cannot parse '{key}={value}' in [{identifier}]. GFFList only supports 2DAMEMORY#=!FieldPath assignments");
                }
                value = new FieldValueConstant(string.Empty); // no path at the root
            }

            return new ModifyFieldGFF(key, value, identifier);
        }

        /// <summary>
        /// Parse GFFList's AddField syntax from the ini to determine what fields/structs/lists to add.
        ///
        /// Args:
        /// ----
        ///     identifier: str - Identifier of the section in the current recursion from the ini file
        ///     ini_data: CaseInsensitiveDict - Data from the ini section
        ///     current_path: PureWindowsPath or None - Current path in the GFF
        ///
        /// Returns:
        /// -------
        ///     ModifyGFF - Object containing the field modification
        ///
        /// Processing Logic:
        /// ----------------
        ///     1. Determines the field type from the field type string
        ///     2. Gets the label and optional value, path from the ini data
        ///     3. Construct a current path from the gff root struct based on recursion level and path key.
        ///     3. Handles nested modifiers and structs in lists
        ///     4. Returns an AddFieldGFF or AddStructToListGFF object based on whether a label is provided.
        /// </summary>
        private ModifyGFF AddFieldGFF(
            string identifier,
            Dictionary<string, string> iniData,
            [CanBeNull] string currentPath = null)
        {
            // Parse required values
            // Can be null if key not found
            if (!iniData.TryGetValue("FieldType", out string rawFieldType))
            {
                throw new KeyNotFoundException($"FieldType missing in [{identifier}]");
            }
            iniData.Remove("FieldType");
            // Python: label: str = ini_data.pop("Label").strip() - raises KeyError if missing
            // Can be null if key not found
            if (!iniData.TryGetValue("Label", out string labelRaw))
            {
                throw new KeyNotFoundException($"Label missing in [{identifier}]");
            }
            string label = labelRaw.Trim();
            iniData.Remove("Label");

            // Resolve TSLPatcher -> PyKotor GFFFieldType
            GFFFieldType fieldType = ResolveTslPatcherGFFFieldType(rawFieldType);

            // Handle current GFF path
            // Can be null if key not found
            string rawPath = iniData.TryGetValue("Path", out string rp) ? rp.Trim() : "";
            iniData.Remove("Path");
            string path = rawPath;
            if (string.IsNullOrEmpty(Path.GetFileName(path)) && !string.IsNullOrEmpty(currentPath) && !string.IsNullOrEmpty(Path.GetFileName(currentPath))) // use current recursion path if section doesn't override with Path=
            {
                path = currentPath;
            }
            // Python: if field_type == GFFFieldType.Struct:
            // Python:     path /= ">>##INDEXINLIST##<<"
            // Note: Python appends this BEFORE checking if label is empty, so it's appended to ALL Struct paths
            // However, when label is set, AddFieldGFF.apply will use the full path (including >>##INDEXINLIST##<<)
            // which won't exist. This seems like a Python bug, but we match it exactly.
            // The path resolution in AddFieldGFF.apply uses zip_longest to merge parent and nested paths.
            if (fieldType == GFFFieldType.Struct)
            {
                path = string.IsNullOrEmpty(path) ? ">>##INDEXINLIST##<<" : Path.Combine(path, ">>##INDEXINLIST##<<").Replace("\\", "/");
            }

            var modifiers = new List<ModifyGFF>();
            int? indexInListToken = null;

            foreach ((string iteratedKey, string iteratedValue) in iniData.ToList())
            {
                string lowerKey = iteratedKey.ToLower();
                if (lowerKey.StartsWith("2damemory"))
                {
                    string lowerIteratedValue = iteratedValue.ToLower();
                    if (lowerIteratedValue == "listindex")
                    {
                        indexInListToken = ParseIntValue(iteratedKey.Substring(9));
                    }
                    else if (lowerIteratedValue == "!fieldpath")
                    {
                        // Python: modifier = Memory2DAModifierGFF(identifier, dst_token_id=int(key[9:]), path=path / label)
                        // Python uses PureWindowsPath path concatenation which uses backslashes
                        string combinedPath = string.IsNullOrEmpty(path) ? label : $"{path}\\{label}";
                        var modifier = new Memory2DAModifierGFF(identifier, combinedPath, ParseIntValue(iteratedKey.Substring(9))); // Assign current path to 2damemory.
                        modifiers.Insert(0, modifier);
                    }
                    else if (lowerIteratedValue.StartsWith("2damemory"))
                    {
                        var modifier = new Memory2DAModifierGFF(
                            identifier, Path.Combine(path, label).Replace("\\", "/"), ParseIntValue(iteratedKey.Substring(9)), ParseIntValue(iteratedValue.Substring(9))); // Assign field at path to a value or (path to field's value)
                        modifiers.Insert(0, modifier);
                    }
                }

                // Handle nested AddField's and recurse
                if (lowerKey.StartsWith("addfield"))
                {
                    // Can be null if section not found
                    string nextSectionName = GetSectionName(iteratedValue);
                    if (nextSectionName is null)
                    {
                        throw new KeyNotFoundException(string.Format(SectionNotFoundError, iteratedValue) + string.Format(ReferencesTracebackMsg, iteratedKey, iteratedValue, identifier));
                    }

                    Dictionary<string, string> nextNestedSection = SectionToDictionary(_ini[nextSectionName]);
                    ModifyGFF nestedModifier = AddFieldGFF(
                        identifier: nextSectionName,
                        iniData: nextNestedSection,
                        currentPath: Path.Combine(path, label).Replace("\\", "/"));
                    modifiers.Add(nestedModifier);
                }
            }

            // get AddField value based on this recursion level
            FieldValue value = GetAddFieldValue(iniData, fieldType, identifier);

            // Check if label unset to determine if current ini section is a struct inside a list.
            if (string.IsNullOrEmpty(label) && fieldType == GFFFieldType.Struct)
            {
                var addStruct = new AddStructToListGFF(identifier, value, path, indexInListToken);
                addStruct.Modifiers.AddRange(modifiers);
                return addStruct;
            }

            // if field_type == GFFFieldType.Struct:  # not sure if this is invalid syntax or not.
            //     msg = f"Label={label} cannot be used when FieldType={GFFFieldType.Struct.value}. Error happened in [{identifier}] section in ini."
            //     raise ValueError(msg)
            if (string.IsNullOrEmpty(label))
            {
                throw new InvalidOperationException($"Label must be set for {fieldType} (FieldType={fieldType}). Error happened in [{identifier}] section in ini.");
            }
            var addField = new AddFieldGFF(identifier, label, fieldType, value, path);
            addField.Modifiers.AddRange(modifiers);
            return addField;
        }

        /// <summary>
        /// Gets the value for an addfield from an ini section dictionary.
        ///
        /// Args:
        /// ----
        ///     ini_section_dict: {CaseInsensitiveDict}: The section of the ini, as a dict.
        ///     field_type: {GFFFieldType}: The field type of this addfield section.
        ///     identifier: {str}: The name identifier for the section
        ///
        /// Returns:
        /// -------
        ///     value: {FieldValue | None}: The parsed field value or None
        ///
        /// Processing Logic:
        /// ----------------
        ///     - Parses the "Value" key to get a raw value and parses it based on field type
        ///     - For LocalizedString, see field_value_from_localized_string
        ///     - For GFFList and GFFStruct, constructs empty instances to be filled in later - see pykotor/tslpatcher/mods/gff.py
        ///     - Returns None if value cannot be parsed or field type not supported (config err)
        /// </summary>
        private static FieldValue GetAddFieldValue(
            Dictionary<string, string> iniSectionDict,
            GFFFieldType fieldType,
            string identifier)
        {
            FieldValue value = null;

            // Can be null if key not found
            if (iniSectionDict.TryGetValue("Value", out string rawValue))
            {
                iniSectionDict.Remove("Value");
                // Can be null if value cannot be parsed
                FieldValue retValue = FieldValueFromType(rawValue, fieldType);
                if (retValue is null)
                {
                    throw new InvalidOperationException($"Could not parse fieldtype '{fieldType}' in GFFList section [{identifier}]");
                }
                value = retValue;
            }
            else if (fieldType == GFFFieldType.LocalizedString)
            {
                value = FieldValueFromLocalizedString(iniSectionDict);
            }
            else if (fieldType == GFFFieldType.List)
            {
                value = new FieldValueConstant(new GFFList());
            }
            else if (fieldType == GFFFieldType.Struct)
            {
                // Can be null if key not found
                string rawStructId = iniSectionDict.TryGetValue("TypeId", out string rsi) ? rsi.Trim() : "0"; // 0 is the default struct id.
                iniSectionDict.Remove("TypeId");
                if (!int.TryParse(rawStructId, out int structId))
                {
                    if (rawStructId.ToLower() == "listindex")
                    {
                        return new FieldValueListIndex(rawStructId.ToLower());
                    }
                    throw new InvalidOperationException($"Invalid TypeId: expected int (or 'listindex' literal) but got '{rawStructId}' in [{identifier}]");
                }
                value = new FieldValueConstant(new GFFStruct(structId));
            }

            if (value is null)
            {
                throw new InvalidOperationException($"Could not find valid field return type in [{identifier}] matching field type '{fieldType}'");
            }

            return value;
        }

        /// <summary>
        /// Parses a localized string from an INI section dictionary (usually a GFF section).
        ///
        /// Args:
        /// ----
        ///     ini_section_dict: CaseInsensitiveDict containing localized string data
        ///
        /// Returns:
        /// -------
        ///     FieldValueConstant: Parsed TSLPatcher localized string
        ///
        /// Processing Logic:
        /// ----------------
        ///     1. Pop the "StrRef" key to get the base string reference
        ///     2. Lookup the string reference from memory or use it as-is if not found
        ///     3. Iterate the keys, filtering for language codes
        ///     4. Extract the language, gender and text from each key/value
        ///     5. Normalize the text and set it in the string delta
        ///     6. Return a FieldValueConstant containing the parsed string delta.
        /// </summary>
        private static FieldValue FieldValueFromLocalizedString(
            Dictionary<string, string> iniSectionDict)
        {
            // Handle both "StrRef" and "Value(strref)" formats
            // Can be null if StrRef not found
            string rawStringref = null;
            // Can be null if key not found
            if (iniSectionDict.TryGetValue("StrRef", out string strRef))
            {
                rawStringref = strRef;
                iniSectionDict.Remove("StrRef");
            }
            else
            {
                // Look for "Value(strref)" format
                foreach ((string key, string value) in iniSectionDict.ToList())
                {
                    string lowerKey = key.ToLower();
                    if (lowerKey.StartsWith("value(strref"))
                    {
                        rawStringref = value;
                        iniSectionDict.Remove(key);
                        break;
                    }
                }
            }

            if (rawStringref is null)
            {
                throw new KeyNotFoundException("StrRef or Value(strref) missing in localized string section");
            }

            // Can be null if value cannot be parsed from memory
            FieldValue stringref = FieldValueFromMemory(rawStringref);
            if (stringref is null)
            {
                stringref = new FieldValueConstant(ParseIntValue(rawStringref));
            }
            var lStringDelta = new LocalizedStringDelta(stringref);

            foreach ((string substring, string text) in iniSectionDict.ToList())
            {
                string lowerSubstring = substring.ToLower();
                // Handle both "lang#" and "Value(lang#)" formats
                string langKey = substring;
                if (lowerSubstring.StartsWith("value(lang") && lowerSubstring.EndsWith(")"))
                {
                    // Extract "lang#" from "Value(lang#)"
                    int startIdx = lowerSubstring.IndexOf("(lang") + 5;
                    int endIdx = lowerSubstring.LastIndexOf(")");
                    langKey = "lang" + substring.Substring(startIdx, endIdx - startIdx);
                    iniSectionDict.Remove(substring);
                    iniSectionDict[langKey] = text;
                }
                else if (!lowerSubstring.StartsWith("lang"))
                {
                    continue;
                }

                int substringId = ParseIntValue(langKey.Substring(4));
                Language language;
                Gender gender;
                LocalizedString.SubstringPair(substringId, out language, out gender);
                string formattedText = NormalizeTslPatcherCRLF(text);
                lStringDelta.SetData(language, gender, formattedText);
            }

            return new FieldValueConstant(lStringDelta);
        }

        /// <summary>
        /// Extract field value from memory reference string.
        ///
        /// Args:
        /// ----
        ///     raw_value: String value to parse
        ///
        /// Returns:
        /// -------
        ///     FieldValue | None: FieldValue object or None
        ///
        /// Processing Logic:
        /// ----------------
        ///     - Lowercase the raw value string
        ///     - Check if it starts with "strref" and extract token ID
        ///     - Check if it starts with "2damemory" and extract token ID
        ///     - Return FieldValue memory object with token ID, or None if no match
        /// </summary>
        [CanBeNull]
        private static FieldValue FieldValueFromMemory(string rawValue)
        {
            string lowerValue = rawValue.ToLower();

            if (lowerValue.StartsWith("strref"))
            {
                int tokenId = ParseIntValue(rawValue.Substring(6));
                return new FieldValueTLKMemory(tokenId);
            }

            if (lowerValue.StartsWith("2damemory"))
            {
                int tokenId = ParseIntValue(rawValue.Substring(9));
                return new FieldValue2DAMemory(tokenId);
            }

            return null;
        }

        /// <summary>
        /// Extracts a field value from an unknown string representation.
        ///
        ///     This section determines how to parse ini key/value pairs in gfflist such as:
        ///         EntryList/0/RepliesList/0/TypeId=5
        ///
        /// Args:
        /// ----
        ///     string_value: The string to parse.
        ///
        /// Returns:
        /// -------
        ///     FieldValue: The parsed value represented as a FieldValue object.
        ///
        /// Processing Logic:
        /// ----------------
        ///     - Checks if the value is already cached in memory
        ///     - Tries to parse as int if possible
        ///     - Otherwise parses as float by normalizing the string
        ///     - Checks for Vector3 or Vector4 by counting pipe separators
        ///     - Falls back to returning as string if no other type matches
        ///     - Returns a FieldValueConstant wrapping the extracted value
        /// </summary>
        private static FieldValue FieldValueFromUnknown(string rawValue)
        {
            // StrRef or 2DAMemory
            // Can be null if value cannot be parsed from memory
            FieldValue fieldValueMemory = FieldValueFromMemory(rawValue);
            if (fieldValueMemory != null)
            {
                return fieldValueMemory;
            }

            // Int
            if (int.TryParse(rawValue, out int intVal))
            {
                return new FieldValueConstant(intVal);
            }

            // Float (Python's float is 64-bit double, so we use double here to match)
            string parsedFloatStr = NormalizeTslPatcherFloat(rawValue);
            if (double.TryParse(parsedFloatStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double doubleVal)) // Python's float is 64-bit
            {
                return new FieldValueConstant(doubleVal);
            }

            int numPipeSeps = rawValue.Count(c => c == '|');

            // String
            if (numPipeSeps == 0)
            {
                return new FieldValueConstant(NormalizeTslPatcherCRLF(rawValue));
            }

            // Vector
            bool notAVector = false;
            var components = new List<float>();
            foreach (string x in parsedFloatStr.Split('|'))
            {
                if (float.TryParse(x, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float comp))
                {
                    components.Add(comp);
                    continue;
                }
                notAVector = true;
                break;
            }

            // String containing the character '|'
            if (notAVector)
            {
                return new FieldValueConstant(NormalizeTslPatcherCRLF(rawValue));
            }

            // Three floats
            if (numPipeSeps == 2)
            {
                return new FieldValueConstant(new Vector3(components[0], components[1], components[2]));
            }

            // Four floats
            if (numPipeSeps == 3)
            {
                return new FieldValueConstant(new Vector4(components[0], components[1], components[2], components[3]));
            }

            throw new InvalidOperationException($"Cannot determine type/value from '{rawValue}'");
        }

        /// <summary>
        /// Extracts field value from raw string based on field type.
        ///
        /// Args:
        /// ----
        ///     raw_value (str): {Raw string value from file}
        ///     field_type (GFFFieldType): {Field type enum}.
        ///
        /// Returns:
        /// -------
        ///     FieldValue: {Field value object}
        ///
        /// Processing Logic:
        /// ----------------
        ///     - Checks if value already exists in memory as a 2DAMEMORY or StrRef
        ///     - Otherwise, converts raw_value to appropriate type based on field_type
        ///     - Returns FieldValueConstant object wrapping extracted value.
        /// </summary>
        [CanBeNull]
        private static FieldValue FieldValueFromType(
            string rawValue,
            GFFFieldType fieldType)
        {
            // Can be null if value cannot be parsed from memory
            FieldValue fieldValueMemory = FieldValueFromMemory(rawValue);
            if (fieldValueMemory != null)
            {
                return fieldValueMemory;
            }

            // Can be null if value cannot be determined
            object value = null;

            if (fieldType == GFFFieldType.ResRef)
            {
                value = new ResRef(rawValue);
            }
            else if (fieldType == GFFFieldType.String)
            {
                value = NormalizeTslPatcherCRLF(rawValue);
            }
            else if (fieldType == GFFFieldType.UInt8 || fieldType == GFFFieldType.Int8 ||
                     fieldType == GFFFieldType.UInt16 || fieldType == GFFFieldType.Int16 ||
                     fieldType == GFFFieldType.UInt32 || fieldType == GFFFieldType.Int32 ||
                     fieldType == GFFFieldType.UInt64 || fieldType == GFFFieldType.Int64)
            {
                if (fieldType == GFFFieldType.UInt8 || fieldType == GFFFieldType.Int8)
                {
                    value = byte.Parse(rawValue);
                }
                else if (fieldType == GFFFieldType.UInt16 || fieldType == GFFFieldType.Int16)
                {
                    value = ushort.Parse(rawValue);
                }
                else if (fieldType == GFFFieldType.UInt32 || fieldType == GFFFieldType.Int32)
                {
                    value = ParseIntValue(rawValue);
                }
                else
                {
                    value = long.Parse(rawValue);
                }
            }
            else if (fieldType == GFFFieldType.Single || fieldType == GFFFieldType.Double)
            {
                value = double.Parse(NormalizeTslPatcherFloat(rawValue), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
                if (fieldType == GFFFieldType.Single)
                {
                    value = (float)(double)value;
                }
            }
            else if (fieldType == GFFFieldType.Vector3)
            {
                float[] components = rawValue.Split('|').Select(axis => float.Parse(NormalizeTslPatcherFloat(axis), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                value = new Vector3(components[0], components[1], components[2]);
            }
            else if (fieldType == GFFFieldType.Vector4)
            {
                float[] components = rawValue.Split('|').Select(axis => float.Parse(NormalizeTslPatcherFloat(axis), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                value = new Vector4(components[0], components[1], components[2], components[3]);
            }
            else if (fieldType == GFFFieldType.Binary)
            {
                string trimmed = rawValue.Trim();
                if (trimmed.Replace("1", "").Replace("0", "").Length == 0)
                {
                    value = new byte[trimmed.Length / 8];
                    for (int i = 0; i < trimmed.Length; i += 8)
                    {
                        ((byte[])value)[i / 8] = (byte)Convert.ToInt32(trimmed.Substring(i, Math.Min(8, trimmed.Length - i)), 2);
                    }
                }
                else if (trimmed.ToLower().StartsWith("0x"))
                {
                    string hexString = trimmed.Substring(2);
                    if (hexString.Length % 2 != 0)
                    {
                        hexString = "0" + hexString;
                    }
                    value = Convert.FromHexString(hexString);
                }
                else
                {
                    try
                    {
                        value = Convert.FromBase64String(rawValue);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"The raw value for the binary field specified was invalid: '{rawValue}'", ex);
                    }
                }
            }

            if (value != null)
            {
                return new FieldValueConstant(value);
            }

            return null;
        }

        /// <summary>
        /// Determines the type of 2DA modification based on the key.
        ///
        /// Args:
        /// ----
        ///     key: str - The key identifying the type of modification
        ///     identifier: str - The identifier of the 2DA (section name)
        ///     modifiers: CaseInsensitiveDict[str] - Additional parameters for the modification
        ///
        /// Returns:
        /// -------
        ///     Modify2DA | None - The 2DA modification object or None
        ///
        /// Processing Logic:
        /// ----------------
        ///     - Parses the key to determine modification type
        ///     - Checks for required parameters
        ///     - Constructs the appropriate modification object
        ///     - Returns the modification object or None.
        /// </summary>
        [CanBeNull]
        private Modify2DA Discern2DA(
            string key,
            string identifier,
            Dictionary<string, string> modifiers)
        {
            // Can be null if not set
            string exclusiveColumn;
            // Can be null if target not found
            Target target;
            // Can be null if row label not set
            string rowLabel;
            Dictionary<string, RowValue> cells;
            Dictionary<int, RowValue> store2da;
            Dictionary<int, RowValue> storeTlk;

            // Can be null if modification cannot be created
            Modify2DA modification = null;
            string lowercaseKey = key.ToLower();

            if (lowercaseKey.StartsWith("changerow"))
            {
                target = Target2DA(identifier, modifiers);
                if (target is null)
                {
                    return null;
                }
                (cells, store2da, storeTlk) = Cells2DA(identifier, modifiers);
                modification = new ChangeRow2DA(identifier, target, cells, store2da, storeTlk);
            }
            else if (lowercaseKey.StartsWith("addrow"))
            {
                // Can be null if key not found
                exclusiveColumn = modifiers.TryGetValue("ExclusiveColumn", out string ec) ? ec : null;
                modifiers.Remove("ExclusiveColumn");
                rowLabel = RowLabel2DA(identifier, modifiers);
                (cells, store2da, storeTlk) = Cells2DA(identifier, modifiers);
                modification = new AddRow2DA(identifier, exclusiveColumn, rowLabel, cells, store2da, storeTlk);
            }
            else if (lowercaseKey.StartsWith("copyrow"))
            {
                target = Target2DA(identifier, modifiers);
                if (target is null)
                {
                    return null;
                }
                // Can be null if key not found
                exclusiveColumn = modifiers.TryGetValue("ExclusiveColumn", out string ec2) ? ec2 : null;
                modifiers.Remove("ExclusiveColumn");
                rowLabel = RowLabel2DA(identifier, modifiers);
                (cells, store2da, storeTlk) = Cells2DA(identifier, modifiers);
                modification = new CopyRow2DA(identifier, target, exclusiveColumn, rowLabel, cells, store2da, storeTlk);
            }
            else if (lowercaseKey.StartsWith("addcolumn"))
            {
                modification = ReadAddColumn(modifiers, identifier);
            }
            else
            {
                throw new KeyNotFoundException($"Could not parse key '{key}={identifier}', expecting one of ['ChangeRow=', 'AddColumn=', 'AddRow=', 'CopyRow=']");
            }

            return modification;
        }

        /// <summary>
        /// Loads the add new column to be added to the 2D array.
        ///
        /// Args:
        /// ----
        ///     modifiers: CaseInsensitiveDict[str]: Dictionary of column modifiers.
        ///     identifier: str: Identifier of the column.
        ///
        /// Returns:
        /// -------
        ///     AddColumn2DA: Object containing details of added column.
        ///
        /// Processing Logic:
        /// ----------------
        ///     - Pop 'ColumnLabel' and 'DefaultValue' from modifiers dict
        ///     - Raise error if 'ColumnLabel' or 'DefaultValue' is missing
        ///     - Call column_inserts_2da() to get insert details
        ///     - Return AddColumn2DA object
        /// </summary>
        private static AddColumn2DA ReadAddColumn(
            Dictionary<string, string> modifiers,
            string identifier)
        {
            // Can be null if key not found
            string header = modifiers.TryGetValue("ColumnLabel", out string h) ? h : null;
            modifiers.Remove("ColumnLabel");
            if (header is null)
            {
                throw new KeyNotFoundException($"Missing 'ColumnLabel' in [{identifier}]");
            }
            // Can be null if key not found
            string defaultValue = modifiers.TryGetValue("DefaultValue", out string dv) ? dv : null;
            modifiers.Remove("DefaultValue");
            if (defaultValue is null)
            {
                throw new KeyNotFoundException($"Missing 'DefaultValue' in [{identifier}]");
            }
            defaultValue = defaultValue != "****" ? defaultValue : "";

            (Dictionary<int, RowValue> indexInsert, Dictionary<string, RowValue> labelInsert, Dictionary<int, string> store2da) = ColumnInserts2DA(
                identifier,
                modifiers);
            return new AddColumn2DA(
                identifier,
                header,
                defaultValue,
                indexInsert,
                labelInsert,
                store2da);
        }

        /// <summary>
        /// Gets or creates a 2D target from modifiers.
        ///
        /// Args:
        /// ----
        ///     identifier: str - Identifier for target
        ///     modifiers: CaseInsensitiveDict[str] - Modifiers dictionary
        ///
        /// Returns:
        /// -------
        ///     Target | None: Target object or None
        ///
        /// Processing Logic:
        /// ----------------
        ///     - Checks for RowIndex, RowLabel or LabelIndex key
        ///     - Calls get_target() to create Target object
        ///     - Returns None if no valid key found with warning
        /// </summary>
        [CanBeNull]
        private Target Target2DA(
            string identifier,
            Dictionary<string, string> modifiers)
        {
            Target GetTarget(TargetType targetType, string key, bool isInt)
            {
                // Can be null if key not found
                string rawValue = modifiers.TryGetValue(key, out string rv) ? rv : null;
                modifiers.Remove(key);
                if (rawValue is null)
                {
                    throw new InvalidOperationException($"[2DAList] parse error: '{key}' missing from [{identifier}] in ini.");
                }
                string lowerRawValue = rawValue.ToLower();
                if (lowerRawValue.StartsWith("strref") && rawValue.Length > 6 && rawValue.Substring(6).All(char.IsDigit))
                {
                    int tokenId = ParseIntValue(rawValue.Substring(6));
                    return new Target(targetType, new RowValueTLKMemory(tokenId));
                }
                else if (lowerRawValue.StartsWith("2damemory") && rawValue.Length > 9 && rawValue.Substring(9).All(char.IsDigit))
                {
                    int tokenId = ParseIntValue(rawValue.Substring(9));
                    return new Target(targetType, new RowValue2DAMemory(tokenId));
                }
                else
                {
                    // Python: value = int(raw_value) if is_int else raw_value
                    // For ROW_INDEX, store int directly; for others, store string
                    object value = isInt ? ParseIntValue(rawValue) : (object)rawValue;
                    return new Target(targetType, value);
                }
            }

            if (modifiers.ContainsKey("RowIndex"))
            {
                return GetTarget(TargetType.ROW_INDEX, "RowIndex", isInt: true);
            }
            if (modifiers.ContainsKey("RowLabel"))
            {
                return GetTarget(TargetType.ROW_LABEL, "RowLabel", isInt: false);
            }
            if (modifiers.ContainsKey("LabelIndex"))
            {
                return GetTarget(TargetType.LABEL_COLUMN, "LabelIndex", isInt: false);
            }

            _log.AddWarning($"No line set to be modified in [{identifier}].");
            return null;
        }

        /// <summary>
        /// Parses modifiers to extract 2DA and TLK cell values and row labels.
        ///
        /// Args:
        /// ----
        ///     identifier: str - Section name for this 2DA
        ///     modifiers: CaseInsensitiveDict[str] - Modifiers dictionary
        ///
        /// Returns:
        /// -------
        ///     tuple[dict[str, RowValue], dict[int, RowValue], dict[int, RowValue]] - tuple containing cells dictionary, 2DA store dictionary, TLK store dictionary
        ///
        /// Processing Logic:
        /// ----------------
        ///     1. Loops through each modifier and value
        ///     2. Determines modifier type (cell, 2DA store, TLK store, row label)
        ///     3. Creates appropriate RowValue for cell/store value
        ///     4. Adds cell/store value to return dictionaries
        /// </summary>
        private static (Dictionary<string, RowValue>, Dictionary<int, RowValue>, Dictionary<int, RowValue>) Cells2DA(
            string identifier,
            Dictionary<string, string> modifiers)
        {
            var cells = new Dictionary<string, RowValue>();
            var store2da = new Dictionary<int, RowValue>();
            var storeTlk = new Dictionary<int, RowValue>();

            foreach ((string modifier, string value) in modifiers)
            {
                string lowerModifier = modifier.ToLower().Trim();
                string lowerValue = value.ToLower();

                bool isStore2da = lowerModifier.StartsWith("2damemory") && lowerModifier.Length > 9 && modifier.Substring(9).All(char.IsDigit);
                bool isStoreTlk = modifier.StartsWith("strref", StringComparison.OrdinalIgnoreCase) && modifier.Length > 6 && modifier.Substring(6).All(char.IsDigit);
                bool isRowLabel = lowerModifier == "rowlabel" || lowerModifier == "newrowlabel";

                // Can be null if row value cannot be determined
                RowValue rowValue = null;
                if (lowerValue.StartsWith("2damemory"))
                {
                    int tokenId = ParseIntValue(value.Substring(9));
                    rowValue = new RowValue2DAMemory(tokenId);
                }
                else if (lowerValue.StartsWith("strref"))
                {
                    int tokenId = ParseIntValue(value.Substring(6));
                    rowValue = new RowValueTLKMemory(tokenId);
                }
                else if (lowerValue == "high()")
                {
                    rowValue = modifier == "rowlabel" ? new RowValueHigh(null) : new RowValueHigh(modifier);
                }
                else if (lowerValue == "rowindex")
                {
                    rowValue = new RowValueRowIndex();
                }
                else if (lowerValue == "rowlabel")
                {
                    rowValue = new RowValueRowLabel();
                }
                else if (value == "****")
                {
                    rowValue = new RowValueConstant("");
                }
                else
                {
                    rowValue = new RowValueConstant(value);
                }

                if (isStore2da)
                {
                    int tokenId = ParseIntValue(modifier.Substring(9));
                    // Store tokens support RowIndex, RowLabel, specific column via RowValueRowCell, or memory references
                    if (lowerValue == "rowindex")
                    {
                        store2da[tokenId] = new RowValueRowIndex();
                    }
                    else if (lowerValue == "rowlabel")
                    {
                        store2da[tokenId] = new RowValueRowLabel();
                    }
                    else if (lowerValue.StartsWith("2damemory"))
                    {
                        int token = ParseIntValue(value.Substring(9));
                        store2da[tokenId] = new RowValue2DAMemory(token);
                    }
                    else if (lowerValue.StartsWith("strref"))
                    {
                        int token = ParseIntValue(value.Substring(6));
                        store2da[tokenId] = new RowValueTLKMemory(token);
                    }
                    else
                    {
                        store2da[tokenId] = new RowValueRowCell(value);
                    }
                }
                else if (isStoreTlk)
                {
                    int tokenId = ParseIntValue(modifier.Substring(6));
                    if (lowerValue.StartsWith("2damemory"))
                    {
                        int token = ParseIntValue(value.Substring(9));
                        storeTlk[tokenId] = new RowValue2DAMemory(token);
                    }
                    else if (lowerValue.StartsWith("strref"))
                    {
                        int token = ParseIntValue(value.Substring(6));
                        storeTlk[tokenId] = new RowValueTLKMemory(token);
                    }
                    else
                    {
                        storeTlk[tokenId] = new RowValueRowCell(value);
                    }
                }
                else if (!isRowLabel)
                {
                    cells[modifier] = rowValue;
                }
            }

            return (cells, store2da, storeTlk);
        }

        private static RowValue ParseStoreRowValue(string value)
        {
            string lowerValue = value.ToLower();
            if (lowerValue.StartsWith("2damemory"))
            {
                int tokenId = ParseIntValue(value.Substring(9));
                return new RowValue2DAMemory(tokenId);
            }
            if (lowerValue.StartsWith("strref"))
            {
                int tokenId = ParseIntValue(value.Substring(6));
                return new RowValueTLKMemory(tokenId);
            }
            if (lowerValue == "rowindex")
            {
                return new RowValueRowIndex();
            }
            if (lowerValue == "rowlabel")
            {
                return new RowValueRowLabel();
            }
            if (value == "****")
            {
                return new RowValueConstant("");
            }
            return new RowValueRowCell(value);
        }

        /// <summary>
        /// Returns the row label for a 2D array based on modifiers.
        ///
        /// Args:
        /// ----
        ///     identifier: Identifier for the 2D array
        ///     modifiers: CaseInsensitiveDict of modifiers for the 2D array
        ///
        /// Returns:
        /// -------
        ///     str | None: The row label or None if not found
        ///
        /// Processing Logic:
        /// ----------------
        ///     - Check if 'RowLabel' exists as a key in modifiers
        ///     - If not present, check if 'NewRowLabel' exists as a key
        ///     - Return the value of the key if present, else return None.
        /// </summary>
        [CanBeNull]
        private static string RowLabel2DA(
            string identifier,
            Dictionary<string, string> modifiers)
        {
            // Can be null if key not found
            if (modifiers.TryGetValue("RowLabel", out string rl))
            {
                modifiers.Remove("RowLabel");
                return rl;
            }
            // Can be null if key not found
            if (modifiers.TryGetValue("NewRowLabel", out string nrl))
            {
                modifiers.Remove("NewRowLabel");
                return nrl;
            }
            return null;
        }

        /// <summary>
        /// Extracts specific 2DA patch information from the ini.
        ///
        /// Args:
        /// ----
        ///     identifier: str - Section name being handled
        ///     modifiers: CaseInsensitiveDict[str] - Modifiers to insert values
        ///
        /// Returns:
        /// -------
        ///     tuple[dict[int, RowValue], dict[str, RowValue], dict[int, str]] - Index inserts, label inserts, 2DA store
        ///
        /// Processes Logic:
        /// ---------------
        ///     - Loops through modifiers and extracts value type
        ///     - Assigns row value based on value type
        ///     - Inserts into appropriate return dictionary based on modifier key
        ///     - Returns tuple of inserted values dictionaries.
        /// </summary>
        private static (Dictionary<int, RowValue>, Dictionary<string, RowValue>, Dictionary<int, string>) ColumnInserts2DA(
            string identifier,
            Dictionary<string, string> modifiers)
        {
            var indexInsert = new Dictionary<int, RowValue>();
            var labelInsert = new Dictionary<string, RowValue>();
            var store2da = new Dictionary<int, string>();

            foreach ((string modifier, string value) in modifiers)
            {
                string modifierLowercase = modifier.ToLower();
                string valueLowercase = value.ToLower();

                bool isStore2da = valueLowercase.StartsWith("2damemory");
                bool isStoreTlk = valueLowercase.StartsWith("strref");

                // Can be null if row value cannot be determined
                RowValue rowValue = null;
                if (isStore2da)
                {
                    int tokenId = ParseIntValue(value.Substring(9));
                    rowValue = new RowValue2DAMemory(tokenId);
                }
                else if (isStoreTlk)
                {
                    int tokenId = ParseIntValue(value.Substring(6));
                    rowValue = new RowValueTLKMemory(tokenId);
                }
                else
                {
                    rowValue = new RowValueConstant(value);
                }

                if (modifierLowercase.StartsWith("i"))
                {
                    int index = ParseIntValue(modifier.Substring(1));
                    indexInsert[index] = rowValue;
                }
                else if (modifierLowercase.StartsWith("l"))
                {
                    string label = modifier.Substring(1);
                    labelInsert[label] = rowValue;
                }
                else if (modifierLowercase.StartsWith("2damemory"))
                {
                    int tokenId = ParseIntValue(modifier.Substring(9));
                    store2da[tokenId] = value;
                }
            }

            return (indexInsert, labelInsert, store2da);
        }

        /// <summary>
        /// Normalize a float value string by replacing commas with periods.
        ///
        /// Args:
        /// ----
        ///     value_str: String value to normalize
        ///
        /// Returns:
        /// -------
        ///     str: Normalized string value with commas replaced with periods
        /// </summary>
        private static string NormalizeTslPatcherFloat(string valueStr)
        {
            return valueStr.Replace(",", ".");
        }

        /// <summary>
        /// Normalize line endings in a string value.
        ///
        /// Args:
        /// ----
        ///     value_str: String value to normalize line endings
        ///
        /// Returns:
        /// -------
        ///     str: String with normalized line endings
        ///
        /// Processes line endings:
        ///     - Replaces "&lt;#LF#&gt;" with "\n"
        ///     - Replaces "&lt;#CR#&gt;" with "\r"
        ///     - Returns string with all line endings normalized
        /// </summary>
        private static string NormalizeTslPatcherCRLF(string valueStr)
        {
            return valueStr.Replace("<#LF#>", "\n").Replace("<#CR#>", "\r");
        }

        /// <summary>
        /// Resolves a config string to an SSFSound enum value.
        ///
        /// Args:
        /// ----
        ///     name (str): The config string name
        ///
        /// Returns:
        /// -------
        ///     SSFSound: The resolved SSFSound enum value
        ///
        /// Processing Logic:
        /// ----------------
        ///     - Defines a CaseInsensitiveDict mapping config strings to SSFSound enum values
        ///     - Looks up the provided name in the dict and returns the corresponding SSFSound value.
        /// </summary>
        private static SSFSound ResolveTslPatcherSSFSound(string name)
        {
            var configstrToSsfSound = new Dictionary<string, SSFSound>(StringComparer.OrdinalIgnoreCase)
        {
            { "Battlecry 1", SSFSound.BATTLE_CRY_1 },
            { "Battlecry 2", SSFSound.BATTLE_CRY_2 },
            { "Battlecry 3", SSFSound.BATTLE_CRY_3 },
            { "Battlecry 4", SSFSound.BATTLE_CRY_4 },
            { "Battlecry 5", SSFSound.BATTLE_CRY_5 },
            { "Battlecry 6", SSFSound.BATTLE_CRY_6 },
            { "Selected 1", SSFSound.SELECT_1 },
            { "Selected 2", SSFSound.SELECT_2 },
            { "Selected 3", SSFSound.SELECT_3 },
            { "Attack 1", SSFSound.ATTACK_GRUNT_1 },
            { "Attack 2", SSFSound.ATTACK_GRUNT_2 },
            { "Attack 3", SSFSound.ATTACK_GRUNT_3 },
            { "Pain 1", SSFSound.PAIN_GRUNT_1 },
            { "Pain 2", SSFSound.PAIN_GRUNT_2 },
            { "Low health", SSFSound.LOW_HEALTH },
            { "Death", SSFSound.DEAD },
            { "Critical hit", SSFSound.CRITICAL_HIT },
            { "Target immune", SSFSound.TARGET_IMMUNE },
            { "Place mine", SSFSound.LAY_MINE },
            { "Disarm mine", SSFSound.DISARM_MINE },
            { "Stealth on", SSFSound.BEGIN_STEALTH },
            { "Search", SSFSound.BEGIN_SEARCH },
            { "Pick lock start", SSFSound.BEGIN_UNLOCK },
            { "Pick lock fail", SSFSound.UNLOCK_FAILED },
            { "Pick lock done", SSFSound.UNLOCK_SUCCESS },
            { "Leave party", SSFSound.SEPARATED_FROM_PARTY },
            { "Rejoin party", SSFSound.REJOINED_PARTY },
            { "Poisoned", SSFSound.POISONED }
        };
            return configstrToSsfSound[name];
        }

        /// <summary>
        /// Resolves a TSLPatcher GFF field type to a PyKotor GFFFieldType enum.
        ///
        /// Use this function to work with the ini's FieldType= values in PyKotor.
        ///
        /// Args:
        /// ----
        ///     field_type_num_str: {String containing the field type number}.
        ///
        /// Returns:
        /// -------
        ///     GFFFieldType: {The GFFFieldType enum value corresponding to the input string}
        ///
        /// Processing Logic:
        /// ----------------
        ///     - Defines a dictionary mapping field type number strings to GFFFieldType enum values
        ///     - Looks up the input string in the dictionary
        ///     - Returns the corresponding GFFFieldType value.
        /// </summary>
        private static GFFFieldType ResolveTslPatcherGFFFieldType(string fieldTypeNumStr)
        {
            var fieldnameToFieldtype = new Dictionary<string, GFFFieldType>(StringComparer.OrdinalIgnoreCase)
        {
            { "Binary", GFFFieldType.Binary }, // HoloPatcher only.
            { "Byte", GFFFieldType.UInt8 },
            { "Char", GFFFieldType.Int8 },
            { "Word", GFFFieldType.UInt16 },
            { "Short", GFFFieldType.Int16 },
            { "DWORD", GFFFieldType.UInt32 },
            { "Int", GFFFieldType.Int32 },
            { "Int64", GFFFieldType.Int64 },
            { "Float", GFFFieldType.Single },
            { "Double", GFFFieldType.Double },
            { "ExoString", GFFFieldType.String },
            { "ResRef", GFFFieldType.ResRef },
            { "ExoLocString", GFFFieldType.LocalizedString },
            { "CExoLocString", GFFFieldType.LocalizedString }, // Alias for ExoLocString
            { "Position", GFFFieldType.Vector3 },
            { "Orientation", GFFFieldType.Vector4 },
            { "Struct", GFFFieldType.Struct },
            { "List", GFFFieldType.List }
        };
            return fieldnameToFieldtype[fieldTypeNumStr];
        }

        /// <summary>
        /// Converts a KeyDataCollection to a case-insensitive dictionary.
        /// </summary>
        private static Dictionary<string, string> SectionToDictionary(KeyDataCollection section)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyData keyData in section)
            {
                dict[keyData.KeyName] = keyData.Value;
            }
            return dict;
        }
    }
}
