using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using KOTORModSync;
using KOTORModSync.Common;
using KOTORModSync.Config;
using KOTORModSync.Formats.Capsule;
using KOTORModSync.Formats.ERF;
using KOTORModSync.Formats.RIM;
using KOTORModSync.Installation;
using KOTORModSync.Logger;
using KOTORModSync.Memory;
using KOTORModSync.Mods;
using KOTORModSync.Mods.GFF;
using KOTORModSync.Mods.NCS;
using KOTORModSync.Mods.NSS;
using KOTORModSync.Mods.SSF;
using KOTORModSync.Mods.TLK;
using KOTORModSync.Mods.TwoDA;
using KOTORModSync.Reader;
using KOTORModSync.Resource;
using IniParser.Model;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.TSLPatcher
{

    /// <summary>
    /// Main orchestrator for installing TSLPatcher mods.
    /// </summary>
    public class ModInstaller
    {
        private readonly string modPath;
        private readonly string gamePath;
        private readonly string changesIniPath;
        private readonly PatchLogger log;
        [CanBeNull]
        private InstallLogWriter installLog;

        [CanBeNull]
        private PatcherConfig config;
        [CanBeNull]
        private string backup;
        private readonly HashSet<string> processedBackupFiles = new HashSet<string>();

        public Game? Game { get; private set; }
        [CanBeNull]
        public string TslPatchDataPath { get; set; }

        public ModInstaller(
            string modPath,
            string gamePath,
            string changesIniPath,
            [CanBeNull] PatchLogger logger = null)
        {
            this.modPath = modPath ?? throw new ArgumentNullException(nameof(modPath));
            this.gamePath = gamePath ?? throw new ArgumentNullException(nameof(gamePath));
            this.changesIniPath = changesIniPath ?? throw new ArgumentNullException(nameof(changesIniPath));
            log = logger ?? new PatchLogger();

            Game = Installation.Installation.DetermineGame(this.gamePath);

            // Handle legacy syntax - look for changes.ini in various locations
            if (!File.Exists(this.changesIniPath))
            {
                string fileName = Path.GetFileName(this.changesIniPath);
                this.changesIniPath = Path.Combine(this.modPath, fileName);

                if (!File.Exists(this.changesIniPath))
                {
                    this.changesIniPath = Path.Combine(this.modPath, "tslpatchdata", fileName);
                }

                if (!File.Exists(this.changesIniPath))
                {
                    throw new FileNotFoundException(
                        "Could not find the changes ini file on disk.",
                        this.changesIniPath);
                }
            }

            // Initialize install log writer in the mod directory (where changes.ini is located)
            string modDirectory = Path.GetDirectoryName(this.changesIniPath) ?? this.modPath;
            try
            {
                installLog = new InstallLogWriter(modDirectory);
                installLog.WriteHeader(modDirectory, this.gamePath, Game);

                // Subscribe to PatchLogger events to also write to install log
                log.LogAdded += OnPatchLoggerLogAdded;
            }
            catch (Exception ex)
            {
                // Log error but don't fail installation if log file can't be created
                log.AddWarning($"Could not create install log file: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles PatchLogger log events and writes them to the install log file.
        /// </summary>
        private void OnPatchLoggerLogAdded(object sender, PatchLog logEntry)
        {
            if (installLog == null)
            {
                return;
            }

            try
            {
                switch (logEntry.LogType)
                {
                    case LogType.Error:
                        installLog.WriteError(logEntry.Message);
                        break;
                    case LogType.Warning:
                        installLog.WriteWarning(logEntry.Message);
                        break;
                    case LogType.Note:
                    case LogType.Verbose:
                        installLog.WriteInfo(logEntry.Message);
                        break;
                }
            }
            catch
            {
                // Ignore errors when writing to install log to avoid breaking installation
            }
        }

        /// <summary>
        /// Gets the patcher configuration, loading it if necessary.
        /// Matches Python: def config(self) -> PatcherConfig
        /// </summary>
        public PatcherConfig Config()
        {
            if (config != null)
            {
                return config;
            }

            if (!File.Exists(changesIniPath))
            {
                throw new FileNotFoundException($"Changes INI file not found: {changesIniPath}");
            }

            // Python: ini_file_bytes: bytes = self.changes_ini_path.read_bytes()
            // Python: ini_text: str = decode_bytes_with_fallbacks(ini_file_bytes)
            byte[] iniFileBytes = File.ReadAllBytes(changesIniPath);
            string iniText;
            try
            {
                // Try UTF-8 first, then Windows-1252, then ASCII with error handling
                iniText = Encoding.UTF8.GetString(iniFileBytes);
                // Validate it's valid UTF-8 by checking for replacement characters
                if (iniText.Contains('\uFFFD'))
                {
                    throw new DecoderFallbackException("UTF-8 decode failed");
                }
            }
            catch (DecoderFallbackException)
            {
                try
                {
                    // Try Windows-1252 (common for INI files)
                    Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                    iniText = Encoding.GetEncoding("windows-1252").GetString(iniFileBytes);
                }
                catch
                {
                    // Fallback: force decode with error handling
                    log.AddWarning($"Could not determine encoding of '{Path.GetFileName(changesIniPath)}'. Attempting to force load...");
                    iniText = Encoding.UTF8.GetString(iniFileBytes);
                }
            }

            // Parse INI with encoding fallback (matches Python decode_bytes_with_fallbacks)
            // Use unified INI parser (case-sensitive for changes.ini files)
            IniData ini = ConfigReader.ParseIniText(iniText, caseInsensitive: false, sourcePath: changesIniPath);

            ConfigReader reader = new ConfigReader(ini, modPath, log, TslPatchDataPath);
            config = reader.Load(new PatcherConfig());

            // Check required files (Python: if self._config.required_files:)
            if (config.RequiredFiles.Count > 0)
            {
                for (int i = 0; i < config.RequiredFiles.Count; i++)
                {
                    string[] files = config.RequiredFiles[i];
                    foreach (string file in files)
                    {
                        string requiredFilePath = Path.Combine(gamePath, "Override", file);
                        if (!File.Exists(requiredFilePath))
                        {
                            string message = i < config.RequiredMessages.Count
                                ? config.RequiredMessages[i].Trim()
                                : "cannot install - missing a required mod";
                            throw new InvalidOperationException(message);
                        }
                    }
                }
            }

            return config;
        }

        /// <summary>
        /// Creates a backup directory and returns its path.
        /// </summary>
        public (string backupPath, HashSet<string> processedFiles) GetBackup()
        {
            if (backup != null)
            {
                return (backup, processedBackupFiles);
            }

            string backupDir = modPath;
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH.mm.ss");

            // Find the root directory containing tslpatchdata
            // Python: while not backup_dir.joinpath("tslpatchdata").is_dir() and backup_dir.parent.name:
            // Python checks backup_dir.parent.name (which is empty string for root), C# checks if GetDirectoryName is not null/empty
            // Can be null if not found
            string parentDir = Path.GetDirectoryName(backupDir);
            while (!Directory.Exists(Path.Combine(backupDir, "tslpatchdata")) &&
                   !string.IsNullOrEmpty(parentDir) && !string.IsNullOrEmpty(Path.GetFileName(parentDir)))
            {
                backupDir = parentDir;
                parentDir = Path.GetDirectoryName(backupDir);
            }

            // Remove old uninstall directory if it exists
            string uninstallDir = Path.Combine(backupDir, "uninstall");
            if (Directory.Exists(uninstallDir))
            {
                try
                {
                    Directory.Delete(uninstallDir, recursive: true);
                }
                catch (Exception ex)
                {
                    log.AddWarning($"Could not initialize uninstall directory: {ex.Message}");
                }
            }

            // Create new backup directory
            backupDir = Path.Combine(backupDir, "backup", timestamp);
            try
            {
                Directory.CreateDirectory(backupDir);
            }
            catch (Exception ex)
            {
                log.AddWarning($"Could not create backup folder: {ex.Message}");
            }

            log.AddNote($"Using backup directory: '{backupDir}'");
            backup = backupDir;

            return (backup, processedBackupFiles);
        }

        /// <summary>
        /// Installs the mod by applying all patches.
        /// Matches Python: def install(...)
        /// </summary>
        public void Install(
            CancellationToken? cancellationToken = null,
            [CanBeNull] Action<int> progressCallback = null)
        {
            try
            {
                if (Game is null)
                {
                    throw new InvalidOperationException(
                        "Chosen KOTOR directory is not a valid installation - cannot initialize ModInstaller.");
                }

                installLog?.WriteInfo("Starting installation...");

                PatcherMemory memory = new PatcherMemory();
                PatcherConfig cfg = Config();

                installLog?.WriteInfo($"Loading configuration from {Path.GetFileName(changesIniPath)}");
                installLog?.WriteInfo($"Found {cfg.InstallList.Count + cfg.Patches2DA.Count + cfg.PatchesGFF.Count + cfg.PatchesTLK.Modifiers.Count + cfg.PatchesNSS.Count + cfg.PatchesNCS.Count + cfg.PatchesSSF.Count} patches to apply");

                List<PatcherModifications> patchesList = new List<PatcherModifications>();
                patchesList.AddRange(cfg.InstallList);
                // Note: TSLPatcher executes [InstallList] after [TLKList]
                patchesList.AddRange(GetTlkPatches(cfg));
                patchesList.AddRange(cfg.Patches2DA);
                patchesList.AddRange(cfg.PatchesGFF);
                // Note: TSLPatcher runs [CompileList] *after* [HACKList], which is objectively bad, so HoloPatcher here will do the inverse.
                patchesList.AddRange(cfg.PatchesNSS);
                patchesList.AddRange(cfg.PatchesNCS);
                patchesList.AddRange(cfg.PatchesSSF);

                bool finishedPreprocessedScripts = false;
                // Can be null if not found
                string tempScriptFolder = null;

                bool processingInstallList = true;
                bool processing2DA = false;
                bool processingGFF = false;
                bool processingTLK = false;
                bool processingNSS = false;
                bool processingNCS = false;
                bool processingSSF = false;

                foreach (PatcherModifications patch in patchesList)
                {
                    cancellationToken?.ThrowIfCancellationRequested();

                    // Python: if should_cancel is not None and should_cancel.is_set(): sys.exit()
                    if (cancellationToken?.IsCancellationRequested == true)
                    {
                        log.AddNote("ModInstaller.Install() received termination request, cancelling...");
                        Environment.Exit(0);
                    }

                    // Log when we start processing different patch types
                    if (processingInstallList && patch is InstallFile)
                    {
                        installLog?.WriteInfo("Processing InstallList entries...");
                        processingInstallList = false;
                    }
                    else if (!processingTLK && patch is ModificationsTLK)
                    {
                        installLog?.WriteInfo("Processing TLK patches...");
                        processingTLK = true;
                    }
                    else if (!processing2DA && patch is Modifications2DA)
                    {
                        installLog?.WriteInfo("Processing 2DA patches...");
                        processing2DA = true;
                    }
                    else if (!processingGFF && patch is ModificationsGFF)
                    {
                        installLog?.WriteInfo("Processing GFF patches...");
                        processingGFF = true;
                    }
                    else if (!processingNSS && patch is ModificationsNSS)
                    {
                        installLog?.WriteInfo("Processing NSS script patches...");
                        processingNSS = true;
                    }
                    else if (!processingNCS && patch is ModificationsNCS)
                    {
                        installLog?.WriteInfo("Processing NCS script patches...");
                        processingNCS = true;
                    }
                    else if (!processingSSF && patch is ModificationsSSF)
                    {
                        installLog?.WriteInfo("Processing SSF sound patches...");
                        processingSSF = true;
                    }

                    // Must run preprocessed scripts directly before GFFList so we don't interfere with !FieldPath assignments to 2DAMEMORY.
                    // Python: if not finished_preprocessed_scripts and isinstance(patch, ModificationsNSS):
                    //         self._prepare_compilelist(config, self.log, memory, self.game)
                    //         finished_preprocessed_scripts = True
                    if (!finishedPreprocessedScripts && patch is ModificationsNSS)
                    {
                        tempScriptFolder = PrepareCompileList(cfg, memory);
                        finishedPreprocessedScripts = true;
                    }

                    try
                    {
                        string outputContainerPath = Path.Combine(gamePath, patch.Destination);

                        HandleCapsuleResult result = HandleCapsuleAndBackup(patch, outputContainerPath);

                        if (!ShouldPatch(patch, result.Exists, result.Capsule))
                        {
                            continue;
                        }

                        // Can be null if not found
                        byte[] dataToPatch = LookupResource(patch, outputContainerPath, existsAtOutput: result.Exists, capsule: result.Capsule);

                        if (dataToPatch is null)
                        {
                            log.AddError($"Could not locate resource to {patch.Action.ToLower().Trim()}: '{patch.SourceFile}'");
                            continue;
                        }

                        if (dataToPatch.Length == 0)
                        {
                            log.AddNote($"'{patch.SourceFile}' has no content/data and is completely empty.");
                        }

                        object patchedData = patch.PatchResource(dataToPatch, memory, log, Game.Value);

                        // If PatchResource returns the boolean true, it means skip
                        if (patchedData is bool b && b)
                        {
                            log.AddNote($"Skipping '{patch.SourceFile}' - patch_resource determined that this file can be skipped.");
                            continue;
                        }

                        if (patchedData is byte[] patchedDataBytes)
                        {
                            if (result.Capsule != null)
                            {
                                HandleOverrideType(patch);
                                HandleModRimShadow(patch);

                                (string resName, ResourceType resType) = ResourceIdentifier.FromPath(patch.SaveAs).Unpack();
                                result.Capsule.Add(resName, resType, patchedDataBytes);
                                result.Capsule.Save();
                            }
                            else
                            {
                                // Python: output_container_path.mkdir(exist_ok=True, parents=True)
                                Directory.CreateDirectory(outputContainerPath);
                                string destinationPath = Path.Combine(outputContainerPath, patch.SaveAs);
                                File.WriteAllBytes(destinationPath, patchedDataBytes);
                            }

                            log.CompletePatch();
                        }
                    }
                    catch (Exception ex)
                    {
                        // Python: exc_type, exc_msg = universal_simplify_exception(e)
                        string excType = ex.GetType().Name;
                        string excMsg = ex.Message;
                        string fmtExcStr = $"{excType}: {excMsg}";
                        string msg = $"An error occurred in patchlist {patch.GetType().Name}:{Environment.NewLine}{fmtExcStr}{Environment.NewLine}";
                        log.AddError(msg);
                        // Python: RobustLogger().exception(msg) - log to file/console
                        System.Diagnostics.Debug.WriteLine($"Exception: {msg}{ex}");
                    }

                    progressCallback?.Invoke(patchesList.IndexOf(patch) + 1);
                }

                // Python: if config.save_processed_scripts == 0 and temp_script_folder is not None and temp_script_folder.is_dir():
                if (cfg.SaveProcessedScripts == 0 && tempScriptFolder != null && Directory.Exists(tempScriptFolder))
                {
                    log.AddNote($"Cleaning temporary script folder at '{tempScriptFolder}' (hint: use 'SaveProcessedScripts=1' in [Settings] to keep these scripts)");
                    try
                    {
                        Directory.Delete(tempScriptFolder, recursive: true);
                    }
                    catch
                    {
                        // Ignore errors when deleting temp folder
                    }
                }

                // Python: num_patches_completed: int = config.patch_count()
                int numPatchesCompleted = cfg.PatchCount();
                log.AddNote($"Successfully completed {numPatchesCompleted} {(numPatchesCompleted == 1 ? "patch" : "total patches")}.");
                installLog?.WriteInfo("Installation completed successfully");
            }
            catch (Exception ex)
            {
                // Ensure errors are logged to install log even if installation fails
                installLog?.WriteError($"Installation failed: {ex.Message}");
                throw;
            }
            finally
            {
                // Always dispose the install log to ensure it's flushed and closed
                installLog?.Dispose();
            }
        }

        /// <summary>
        /// Prepares NSS compilation by copying scripts to temp folder and preprocessing tokens.
        /// Matches Python: def _prepare_compilelist(...)
        /// </summary>
        [CanBeNull]
        private string PrepareCompileList(PatcherConfig config, PatcherMemory memory)
        {
            // Python: tslpatchdata should be read-only, this allows us to replace memory tokens while ensuring include scripts work correctly.
            if (config.PatchesNSS.Count == 0)
            {
                return null;
            }

            // Move nwscript.nss to Override if there are any nss patches to do
            string nwscriptPath = Path.Combine(modPath, "nwscript.nss");
            if (File.Exists(nwscriptPath))
            {
                var fileInstall = new InstallFile("nwscript.nss", replaceExisting: true);
                if (!config.InstallList.Contains(fileInstall))
                {
                    config.InstallList.Add(fileInstall);
                }
            }

            // Copy all .nss files in the mod path, to a temp working directory
            string tempScriptFolder = Path.Combine(modPath, "temp_nss_working_dir");
            if (Directory.Exists(tempScriptFolder))
            {
                try
                {
                    Directory.Delete(tempScriptFolder, recursive: true);
                }
                catch
                {
                    // Ignore errors
                }
            }
            Directory.CreateDirectory(tempScriptFolder);

            // Copy .nss files
            foreach (string file in Directory.GetFiles(modPath))
            {
                if (Path.GetExtension(file).Equals(".nss", StringComparison.OrdinalIgnoreCase) && File.Exists(file))
                {
                    string destFile = Path.Combine(tempScriptFolder, Path.GetFileName(file));
                    File.Copy(file, destFile, true);
                }
            }

            // Process the strref/2damemory in each script
            string[] scripts = Directory.GetFiles(tempScriptFolder, "*.nss", SearchOption.TopDirectoryOnly);
            log.AddVerbose($"Preprocessing #StrRef# and #2DAMEMORY# tokens for all {scripts.Length} scripts, before running [CompileList]");

            foreach (string script in scripts)
            {
                if (!File.Exists(script) || !Path.GetExtension(script).Equals(".nss", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                log.AddVerbose($"Parsing tokens in '{Path.GetFileName(script)}'...");
                byte[] scriptBytes = File.ReadAllBytes(script);
                string scriptText = Encoding.GetEncoding("windows-1252").GetString(scriptBytes);
                var mutableContent = new MutableString(scriptText);

                // Apply token replacement
                var nssMod = new ModificationsNSS(Path.GetFileName(script), false);
                nssMod.Apply(mutableContent, memory, log, Game.Value);

                // Write back with windows-1252 encoding
                File.WriteAllText(script, mutableContent.Value, Encoding.GetEncoding("windows-1252"));
            }

            // Store the location of the temp folder in each nss patch
            foreach (ModificationsNSS nssPatch in config.PatchesNSS)
            {
                nssPatch.TempScriptFolder = tempScriptFolder;
            }

            return tempScriptFolder;
        }

        /// <summary>
        /// Handle capsule file and create backup.
        /// Matches Python: def handle_capsule_and_backup(...)
        /// </summary>
        private HandleCapsuleResult HandleCapsuleAndBackup(
            PatcherModifications patch,
            string outputContainerPath)
        {
            // Can be null if not found
            Capsule capsule = null;
            bool exists = false;

            if (IsCapsuleFile(patch.Destination))
            {
                // Python: module_root: str = Installation.get_module_root(output_container_path)
                string moduleRoot = Installation.Installation.GetModuleRoot(outputContainerPath);
                string[] tslrcmOmittedRims = { "702KOR", "401DXN" };

                // Python: if module_root.upper() not in tslrcm_omitted_rims and is_rim_file(output_container_path):
                if (!tslrcmOmittedRims.Contains(moduleRoot.ToUpperInvariant()) && IsRimFile(outputContainerPath))
                {
                    log.AddWarning($"This mod is patching RIM file Modules/{Path.GetFileName(outputContainerPath)}!\nPatching RIMs is highly incompatible, not recommended, and widely considered bad practice. Please request the mod developer to fix this.");
                }

                // Python: if not output_container_path.is_file():
                if (!File.Exists(outputContainerPath))
                {
                    // Python: if is_mod_file(output_container_path):
                    if (IsModFile(outputContainerPath))
                    {
                        string modulesPath = Installation.Installation.GetModulesPath(gamePath);
                        log.AddNote(
                            $"IMPORTANT! The module at path '{outputContainerPath}' did not exist, building one in the 'Modules' folder immediately from the following files:" +
                            $"\n    Modules/{moduleRoot}.rim" +
                            $"\n    Modules/{moduleRoot}_s.rim" +
                            (Game != null && (Game.Value == Common.Game.TSL || Game.Value == Common.Game.K2) ? $"\n    Modules/{moduleRoot}_dlg.erf" : "")
                        );
                        try
                        {
                            RimToMod(outputContainerPath, modulesPath, moduleRoot);
                        }
                        catch (Exception ex)
                        {
                            string msg = $"Failed to build module '{Path.GetFileName(outputContainerPath)}': {ex.Message}";
                            log.AddError(msg);
                            throw;
                        }
                    }
                    else
                    {
                        // Python: raise FileNotFoundError(errno.ENOENT, msg, str(output_container_path))
                        string msg = $"The capsule '{patch.Destination}' did not exist, or permission issues occurred, when attempting to {patch.Action.ToLower().TrimEnd()} '{patch.SourceFile}'. Skipping file...";
                        throw new FileNotFoundException(msg, outputContainerPath);
                    }
                }

                capsule = new Capsule(outputContainerPath, createIfNotExist: false);
                (string backupPath, HashSet<string> processedFiles) = GetBackup();
                CreateBackupHelper(outputContainerPath, backupPath, processedFiles, Path.GetDirectoryName(patch.Destination) ?? "");

                // Python: exists = capsule.contains(*ResourceIdentifier.from_path(patch.saveas).unpack())
                (string resName, ResourceType resType) = ResourceIdentifier.FromPath(patch.SaveAs).Unpack();
                exists = capsule.Contains(resName, resType);
            }
            else
            {
                // Python: create_backup(self.log, output_container_path.joinpath(patch.saveas), *self.backup(), patch.destination)
                string fullPath = Path.Combine(outputContainerPath, patch.SaveAs);
                (string backupPath, HashSet<string> processedFiles) = GetBackup();
                CreateBackupHelper(fullPath, backupPath, processedFiles, patch.Destination);

                // Python: exists = output_container_path.joinpath(patch.saveas).is_file()
                exists = File.Exists(fullPath);
            }

            return new HandleCapsuleResult { Exists = exists, Capsule = capsule };
        }

        private class HandleCapsuleResult
        {
            public bool Exists { get; set; }
            [CanBeNull]
            public Capsule Capsule { get; set; }
        }

        /// <summary>
        /// Creates a backup of the provided file.
        /// Matches Python: def create_backup(...) in mods/install.py
        /// </summary>
        private void CreateBackupHelper(string destinationFilePath, string backupFolderPath, HashSet<string> processedFiles, [CanBeNull] string subdirectoryPath = null)
        {
            string destinationFileStr = destinationFilePath;
            string destinationFileStrLower = destinationFileStr.ToLowerInvariant();

            string backupFilepath;
            if (!string.IsNullOrEmpty(subdirectoryPath))
            {
                string subdirectoryBackupPath = Path.Combine(backupFolderPath, subdirectoryPath);
                backupFilepath = Path.Combine(subdirectoryBackupPath, Path.GetFileName(destinationFilePath));
                Directory.CreateDirectory(subdirectoryBackupPath);
            }
            else
            {
                backupFilepath = Path.Combine(backupFolderPath, Path.GetFileName(destinationFilePath));
            }

            // Python: if destination_file_str_lower not in processed_files:
            if (!processedFiles.Contains(destinationFileStrLower))
            {
                // Write a list of files that should be removed in order to uninstall the mod
                string uninstallFolder = Path.Combine(Path.GetDirectoryName(backupFolderPath) ?? backupFolderPath, "..", "uninstall");
                uninstallFolder = Path.GetFullPath(uninstallFolder);
                string uninstallStrLower = uninstallFolder.ToLowerInvariant();

                if (!processedFiles.Contains(uninstallStrLower))
                {
                    Directory.CreateDirectory(uninstallFolder);

                    // Python: game_folder: CaseAwarePath = destination_filepath.parents[len(subdir_temp.parts)] if subdir_temp else destination_filepath.parent
                    string gameFolder;
                    if (!string.IsNullOrEmpty(subdirectoryPath))
                    {
                        // Calculate game folder by going up the directory tree based on subdirectory depth
                        string[] subdirParts = subdirectoryPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
                        string currentPath = destinationFilePath;
                        for (int i = 0; i < subdirParts.Length; i++)
                        {
                            currentPath = Path.GetDirectoryName(currentPath) ?? currentPath;
                        }
                        gameFolder = currentPath;
                    }
                    else
                    {
                        gameFolder = Path.GetDirectoryName(destinationFilePath) ?? gamePath;
                    }

                    CreateUninstallScripts(backupFolderPath, uninstallFolder, gameFolder);
                    processedFiles.Add(uninstallStrLower);
                }

                // Python: if destination_filepath.is_file():
                if (File.Exists(destinationFilePath))
                {
                    // Check if the backup path exists and generate a new one if necessary
                    int i = 2;
                    string filestem = Path.GetFileNameWithoutExtension(backupFilepath);
                    string suffix = Path.GetExtension(backupFilepath);
                    string backupDir = Path.GetDirectoryName(backupFilepath) ?? backupFolderPath;

                    while (File.Exists(backupFilepath))
                    {
                        backupFilepath = Path.Combine(backupDir, $"{filestem} ({i}){suffix}");
                        i++;
                    }

                    log.AddNote($"Backing up '{destinationFileStr}'...");
                    try
                    {
                        File.Copy(destinationFilePath, backupFilepath, true);
                    }
                    catch (Exception ex)
                    {
                        log.AddWarning($"Failed to create backup of '{destinationFileStr}': {ex.Message}");
                    }
                }
                else
                {
                    // Write the file path to remove these files.txt in backup directory
                    string removalFilesTxt = Path.Combine(backupFolderPath, "remove these files.txt");
                    string line = (File.Exists(removalFilesTxt) ? "\n" : "") + destinationFileStr;
                    File.AppendAllText(removalFilesTxt, line);
                }

                // Add the lowercased path string to the processed_files set
                processedFiles.Add(destinationFileStrLower);
            }
        }

        /// <summary>
        /// Loads a resource file using BinaryReader (matches Python load_resource_file).
        /// </summary>
        [NotNull]
        private static byte[] LoadResourceFile(string sourcePath)
        {
            // Python: with BinaryReader.from_auto(source) as reader: return reader.read_all()
            using (var reader = KOTORModSync.Common.RawBinaryReader.FromFile(sourcePath))
            {
                return reader.ReadAll();
            }
        }

        /// <summary>
        /// Looks up the file/resource that is expected to be patched.
        /// Matches Python: def lookup_resource(...)
        /// </summary>
        [CanBeNull]
        public byte[] LookupResource(
            PatcherModifications patch,
            string outputContainerPath,
            bool existsAtOutput,
            [CanBeNull] Capsule capsule)
        {
            try
            {
                // Python logic: if patch.replace_file or not exists_at_output_location:
                //   return self.load_resource_file(self.mod_path / patch.sourcefolder / patch.sourcefile)
                if (patch.ReplaceFile || !existsAtOutput)
                {
                    // Path resolution: mod_path / sourcefolder / sourcefile
                    // mod_path is typically the tslpatchdata folder (parent of changes.ini).
                    // If sourcefolder = ".", this resolves to mod_path itself (tslpatchdata folder).
                    string sourcePath = Path.Combine(modPath, patch.SourceFolder, patch.SourceFile);
                    return LoadResourceFile(sourcePath);
                }

                // Python: if capsule is None:
                //   return self.load_resource_file(output_container_path / patch.saveas)
                if (capsule is null)
                {
                    string targetPath = Path.Combine(outputContainerPath, patch.SaveAs);
                    return LoadResourceFile(targetPath);
                }

                // Python: return capsule.resource(*ResourceIdentifier.from_path(patch.saveas).unpack())
                (string resName, ResourceType resType) = ResourceIdentifier.FromPath(patch.SaveAs).Unpack();
                return capsule.GetResource(resName, resType);
            }
            catch (Exception ex)
            {
                // Python: self.log.add_error(f"Could not load source file to {patch.action.lower().strip()}:{os.linesep}{universal_simplify_exception(e)}")
                log.AddError($"Could not load source file to {patch.Action.ToLower().Trim()}:{Environment.NewLine}{ex.Message}");
                return null;
            }
        }

        public bool ShouldPatch(
            PatcherModifications patch,
            bool exists,
            [CanBeNull] Capsule capsule = null)
        {
            string localFolder = patch.Destination == "." ? new DirectoryInfo(gamePath).Name : patch.Destination;
            string containerType = capsule is null ? "folder" : "archive";

            // Python uses action[:-1] which removes last character, not just trailing whitespace
            string actionBase = patch.Action.Length > 0 ? patch.Action.Substring(0, patch.Action.Length - 1) : patch.Action;

            if (patch.ReplaceFile && exists)
            {
                string saveAsStr = patch.SaveAs != patch.SourceFile ? $"'{patch.SaveAs}' in" : "in";
                log.AddNote($"{actionBase}ing '{patch.SourceFile}' and replacing existing file {saveAsStr} the '{localFolder}' {containerType}");
                return true;
            }

            if (!patch.SkipIfNotReplace && !patch.ReplaceFile && exists)
            {
                log.AddNote($"{actionBase}ing existing file '{patch.SaveAs}' in the '{localFolder}' {containerType}");
                return true;
            }

            if (patch.SkipIfNotReplace && !patch.ReplaceFile && exists)
            {
                log.AddNote($"'{patch.SaveAs}' already exists in the '{localFolder}' {containerType}. Skipping file...");
                return false;
            }

            // If capsule doesn't exist on disk, return false (matches Python: capsule.filepath().is_file())
            if (capsule != null && !capsule.Path.IsFile())
            {
                log.AddError($"The capsule '{patch.Destination}' did not exist when attempting to {patch.Action.ToLower().TrimEnd()} '{patch.SourceFile}'. Skipping file...");
                return false;
            }
            if (capsule != null && !capsule.ExistedOnDisk && !patch.ReplaceFile)
            {
                log.AddError($"The capsule '{patch.Destination}' did not exist when attempting to {patch.Action.ToLower().TrimEnd()} '{patch.SourceFile}'. Skipping file...");
                return false;
            }

            string saveType = (capsule != null && patch.SaveAs == patch.SourceFile) ? "adding" : "saving";
            string savingAsStr = patch.SaveAs != patch.SourceFile ? $"as '{patch.SaveAs}' in" : "to";
            log.AddNote($"{actionBase}ing '{patch.SourceFile}' and {saveType} {savingAsStr} the '{localFolder}' {containerType}");
            return true;
        }

        /// <summary>
        /// Handles the desired behavior set by the !OverrideType tslpatcher var for the specified patch.
        /// Matches Python: def handle_override_type(...)
        /// </summary>
        private void HandleOverrideType(PatcherModifications patch)
        {
            // Python: override_type: str = patch.override_type.lower().strip()
            string overrideType = patch.OverrideTypeValue.ToLowerInvariant().Trim();
            if (string.IsNullOrEmpty(overrideType) || overrideType == OverrideType.IGNORE)
            {
                return;
            }

            string overrideDir = Path.Combine(gamePath, "Override");
            string overrideResourcePath = Path.Combine(overrideDir, patch.SaveAs);

            // Python: if override_resource_path.is_file():
            if (!File.Exists(overrideResourcePath))
            {
                return;
            }

            if (overrideType == OverrideType.RENAME)
            {
                // Python: renamed_file_path: CaseAwarePath = override_dir / f"old_{patch.saveas}"
                string renamedFilePath = Path.Combine(overrideDir, $"old_{patch.SaveAs}");
                int i = 2;
                string filestem = Path.GetFileNameWithoutExtension(renamedFilePath);

                // Python: while renamed_file_path.is_file():
                while (File.Exists(renamedFilePath))
                {
                    // Python: renamed_file_path = renamed_file_path.parent / f"{filestem} ({i}){renamed_file_path.suffix}"
                    string suffix = Path.GetExtension(renamedFilePath);
                    renamedFilePath = Path.Combine(overrideDir, $"{filestem} ({i}){suffix}");
                    i++;
                }

                try
                {
                    // Python: shutil.move(str(override_resource_path), str(renamed_file_path))
                    File.Move(overrideResourcePath, renamedFilePath);
                    log.AddNote($"Renamed existing Override file '{patch.SaveAs}' to '{Path.GetFileName(renamedFilePath)}' to prevent shadowing.");
                }
                catch (Exception ex)
                {
                    // Python: self.log.add_error(f"Could not rename '{patch.saveas}' to '{renamed_file_path.name}' in the Override folder: {universal_simplify_exception(e)}")
                    log.AddError($"Could not rename '{patch.SaveAs}' to '{Path.GetFileName(renamedFilePath)}' in the Override folder: {ex.Message}");
                }
            }
            else if (overrideType == OverrideType.WARN)
            {
                // Python: self.log.add_warning(f"A resource located at '{override_resource_path}' is shadowing this mod's changes in {patch.destination}!")
                log.AddWarning($"A resource located at '{overrideResourcePath}' is shadowing this mod's changes in {patch.Destination}!");
            }
        }

        /// <summary>
        /// Check if a patch is being installed into a rim and overshadowed by a .mod.
        /// Matches Python: def handle_modrim_shadow(...)
        /// </summary>
        private void HandleModRimShadow(PatcherModifications patch)
        {
            // Python: erfrim_path: CaseAwarePath = self.game_path / patch.destination / patch.saveas
            string erfrimPath = Path.Combine(gamePath, patch.Destination, patch.SaveAs);

            // Python: mod_path: CaseAwarePath = erfrim_path.with_name(f"{Installation.get_module_root(erfrim_path.name)}.mod")
            string moduleRoot = Installation.Installation.GetModuleRoot(Path.GetFileName(erfrimPath));
            string modFilePath = Path.Combine(Path.GetDirectoryName(erfrimPath) ?? gamePath, $"{moduleRoot}.mod");

            // Python: if erfrim_path != mod_path and mod_path.is_file():
            if (!erfrimPath.Equals(modFilePath, StringComparison.OrdinalIgnoreCase) && File.Exists(modFilePath))
            {
                log.AddWarning($"This mod intends to install '{patch.SaveAs}' into '{patch.Destination}', but is overshadowed by the existing '{Path.GetFileName(modFilePath)}'!");
            }
        }

        private static bool IsCapsuleFile(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".mod" || ext == ".rim" || ext == ".erf" || ext == ".sav";
        }

        private static bool IsModFile(string path)
        {
            return Path.GetExtension(path).Equals(".mod", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRimFile(string path)
        {
            return Path.GetExtension(path).Equals(".rim", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Creates a MOD file at the given filepath and copies the resources from the corresponding RIM files.
        /// Matches Python: def rim_to_mod(...)
        /// </summary>
        private void RimToMod(string modFilePath, string rimFolderPath, string moduleRoot)
        {
            if (!IsModFile(modFilePath))
            {
                throw new ArgumentException("Specified file must end with the .mod extension", nameof(modFilePath));
            }

            string filepathRim = Path.Combine(rimFolderPath, $"{moduleRoot}.rim");
            string filepathRimS = Path.Combine(rimFolderPath, $"{moduleRoot}_s.rim");
            string filepathDlgErf = Path.Combine(rimFolderPath, $"{moduleRoot}_dlg.erf");

            var mod = new ERF(ERFType.MOD, true);

            // Load main RIM using Capsule
            if (File.Exists(filepathRim))
            {
                var rimCapsule = new Capsule(filepathRim, createIfNotExist: false);
                foreach (CapsuleResource res in rimCapsule)
                {
                    mod.SetData(res.ResName, res.ResType, res.Data);
                }
            }

            // Load _s RIM if exists
            if (File.Exists(filepathRimS))
            {
                var rimSCapsule = new Capsule(filepathRimS, createIfNotExist: false);
                foreach (CapsuleResource res in rimSCapsule)
                {
                    mod.SetData(res.ResName, res.ResType, res.Data);
                }
            }

            // Load _dlg.erf if exists (TSL only)
            if ((Game is null || Game.Value == Common.Game.TSL || Game.Value == Common.Game.K2) && File.Exists(filepathDlgErf))
            {
                var erfCapsule = new Capsule(filepathDlgErf, createIfNotExist: false);
                foreach (CapsuleResource res in erfCapsule)
                {
                    mod.SetData(res.ResName, res.ResType, res.Data);
                }
            }

            // Write MOD file
            var writer = new ERFBinaryWriter(mod);
            using (FileStream fs = File.Create(modFilePath))
            {
                writer.Write(fs);
            }
        }

        /// <summary>
        /// Gets TLK patches from the configuration.
        /// Returns main TLK patches and female dialog patches if applicable.
        /// </summary>
        [NotNull]
        private List<PatcherModifications> GetTlkPatches(PatcherConfig config)
        {
            var tlkPatches = new List<PatcherModifications>();

            if (config.PatchesTLK.Modifiers.Count == 0)
            {
                return tlkPatches;
            }

            // Add main TLK patches
            tlkPatches.Add(config.PatchesTLK);

            // Check if female dialog file exists
            string femaleDialogFilename = "dialogf.tlk";
            string femaleDialogFilePath = Path.Combine(gamePath, femaleDialogFilename);

            if (File.Exists(femaleDialogFilePath))
            {
                // Create a deep copy of the TLK patches for female dialog
                var femaleTlkPatches = new Mods.TLK.ModificationsTLK(
                    config.PatchesTLK.SourceFile,
                    config.PatchesTLK.ReplaceFile);

                // Copy all modifiers
                foreach (Mods.TLK.ModifyTLK modifier in config.PatchesTLK.Modifiers)
                {
                    femaleTlkPatches.Modifiers.Add(modifier);
                }

                // Copy other properties
                femaleTlkPatches.SourceFolder = config.PatchesTLK.SourceFolder;
                femaleTlkPatches.Destination = config.PatchesTLK.Destination;
                femaleTlkPatches.OverrideTypeValue = config.PatchesTLK.OverrideTypeValue;
                femaleTlkPatches.SkipIfNotReplace = config.PatchesTLK.SkipIfNotReplace;

                // Use female source file if it exists, otherwise use main source file
                string femaleSourceFile = config.PatchesTLK.SourcefileF;
                if (!string.IsNullOrEmpty(femaleSourceFile))
                {
                    string femaleSourcePath = Path.Combine(modPath, config.PatchesTLK.SourceFolder, femaleSourceFile);
                    if (File.Exists(femaleSourcePath))
                    {
                        femaleTlkPatches.SourceFile = femaleSourceFile;
                    }
                }

                // Set save as to female dialog filename
                femaleTlkPatches.SaveAs = femaleDialogFilename;

                tlkPatches.Add(femaleTlkPatches);
            }

            return tlkPatches;
        }

        /// <summary>
        /// Creates uninstall scripts (PowerShell and Bash) in the uninstall folder.
        /// Matches Python: def create_uninstall_scripts(...)
        /// </summary>
        private static void CreateUninstallScripts([NotNull] string backupDir, [NotNull] string uninstallFolder, [NotNull] string mainFolder)
        {
            // PowerShell script - using StringBuilder to avoid verbatim string parsing issues
            string ps1Path = Path.Combine(uninstallFolder, "uninstall.ps1");
            var ps1Script = new StringBuilder();
            ps1Script.AppendLine("#!/usr/bin/env pwsh");
            ps1Script.AppendLine("$backupParentFolder = Get-Item -Path \"..$([System.IO.Path]::DirectorySeparatorChar)backup\"");
            ps1Script.AppendLine("$mostRecentBackupFolder = Get-ChildItem -LiteralPath $backupParentFolder.FullName -Directory | ForEach-Object {");
            ps1Script.AppendLine("    $dirName = $_.Name");
            ps1Script.AppendLine("    try {");
            ps1Script.AppendLine("        [datetime]$dt = [datetime]::ParseExact($dirName, \"yyyy-MM-dd_HH.mm.ss\", $null)");
            ps1Script.AppendLine("        Write-Host \"Found backup '$dirName'\"");
            ps1Script.AppendLine("        return [PSCustomObject]@{");
            ps1Script.AppendLine("            Directory = $_.FullName");
            ps1Script.AppendLine("            DateTime = $dt");
            ps1Script.AppendLine("        }");
            ps1Script.AppendLine("    } catch {");
            ps1Script.AppendLine("        if ($dirName -and $dirName -ne '' -and -not ($dirName -match \"^\\s*$\")) {");
            ps1Script.AppendLine("            Write-Host \"Ignoring directory '$dirName'. $($_.Exception.Message)\"");
            ps1Script.AppendLine("        }");
            ps1Script.AppendLine("    }");
            ps1Script.AppendLine("} | Sort-Object DateTime -Descending | Select-Object -ExpandProperty Directory -First 1");
            ps1Script.AppendLine("if ($null -eq $mostRecentBackupFolder -or -not $mostRecentBackupFolder -or -not (Test-Path -LiteralPath $mostRecentBackupFolder -ErrorAction SilentlyContinue)) {");
            ps1Script.AppendLine("    $mostRecentBackupFolder = \"" + backupDir.Replace("\\", "\\\\") + "\"");
            ps1Script.AppendLine("    if (-not (Test-Path -LiteralPath $mostRecentBackupFolder -ErrorAction SilentlyContinue)) {");
            ps1Script.AppendLine("        Write-Host \"No backups found in '$($backupParentFolder.FullName)'\"");
            ps1Script.AppendLine("        Pause");
            ps1Script.AppendLine("        exit");
            ps1Script.AppendLine("    }");
            ps1Script.AppendLine("    Write-Host \"Using hardcoded backup dir: '$mostRecentBackupFolder'\"");
            ps1Script.AppendLine("} else {");
            ps1Script.AppendLine("    Write-Host \"Selected backup folder '$mostRecentBackupFolder'\"");
            ps1Script.AppendLine("}");
            ps1Script.AppendLine();
            ps1Script.AppendLine("$deleteListFile = $mostRecentBackupFolder + \"$([System.IO.Path]::DirectorySeparatorChar)remove these files.txt\"");
            ps1Script.AppendLine("$existingFiles = New-Object System.Collections.Generic.HashSet[string]");
            ps1Script.AppendLine("if (-not (Test-Path -LiteralPath $deleteListFile -ErrorAction SilentlyContinue)) {");
            ps1Script.AppendLine("    Write-Host \"Delete file list not found.\"");
            ps1Script.AppendLine("    #exit");
            ps1Script.AppendLine("} else {");
            ps1Script.AppendLine("    $filesToDelete = Get-Content -LiteralPath $deleteListFile");
            ps1Script.AppendLine("    foreach ($file in $filesToDelete) {");
            ps1Script.AppendLine("        if ($file) { # Check if $file is non-null and non-empty");
            ps1Script.AppendLine("            if (Test-Path -LiteralPath $file -ErrorAction SilentlyContinue) {");
            ps1Script.AppendLine("                # Check if the path is not a directory");
            ps1Script.AppendLine("                if (-not (Get-Item -LiteralPath $file).PSIsContainer) {");
            ps1Script.AppendLine("                    $existingFiles.Add($file) | Out-Null");
            ps1Script.AppendLine("                }");
            ps1Script.AppendLine("            } else {");
            ps1Script.AppendLine("                Write-Host \"WARNING! $file no longer exists! Running this script is no longer recommended!\"");
            ps1Script.AppendLine("            }");
            ps1Script.AppendLine("        }");
            ps1Script.AppendLine("    }");
            ps1Script.AppendLine("}");
            ps1Script.AppendLine();
            ps1Script.AppendLine();
            ps1Script.AppendLine("$numberOfExistingFiles = $existingFiles.Count");
            ps1Script.AppendLine();
            ps1Script.AppendLine("$allItemsInBackup = Get-ChildItem -LiteralPath $mostRecentBackupFolder -Recurse | Where-Object { $_.Name -ne 'remove these files.txt' }");
            ps1Script.AppendLine("$filesInBackup = ($allItemsInBackup | Where-Object { -not $_.PSIsContainer })");
            ps1Script.AppendLine("$folderCount = ($allItemsInBackup | Where-Object { $_.PSIsContainer }).Count");
            ps1Script.AppendLine();
            ps1Script.AppendLine("# Display relative file paths if file count is less than 6");
            ps1Script.AppendLine("if ($filesInBackup.Count -lt 6) {");
            ps1Script.AppendLine("    $allItemsInBackup |");
            ps1Script.AppendLine("    Where-Object { -not $_.PSIsContainer } |");
            ps1Script.AppendLine("    ForEach-Object {");
            ps1Script.AppendLine("        $relativePath = $_.FullName -replace [regex]::Escape($mostRecentBackupFolder), \"\"");
            ps1Script.AppendLine("        Write-Host $relativePath.TrimStart(\"\\\")");
            ps1Script.AppendLine("    }");
            ps1Script.AppendLine("}");
            ps1Script.AppendLine();
            ps1Script.AppendLine("$validConfirmations = @(\"y\", \"yes\")");
            ps1Script.AppendLine("$confirmation = Read-Host \"Really uninstall $numberOfExistingFiles files and restore the most recent backup (containing $($filesInBackup.Count) files and $folderCount folders)? (y/N)\"");
            ps1Script.AppendLine("if ($confirmation.Trim().ToLower() -notin $validConfirmations) {");
            ps1Script.AppendLine("    Write-Host \"Operation cancelled.\"");
            ps1Script.AppendLine("    exit");
            ps1Script.AppendLine("}");
            ps1Script.AppendLine();
            ps1Script.AppendLine("$deletedCount = 0");
            ps1Script.AppendLine("foreach ($file in $existingFiles) {");
            ps1Script.AppendLine("    if ($file -and (Test-Path -LiteralPath $file -ErrorAction SilentlyContinue)) {");
            ps1Script.AppendLine("        Remove-Item $file -Force");
            ps1Script.AppendLine("        Write-Host \"Removed $file...\"");
            ps1Script.AppendLine("        $deletedCount++");
            ps1Script.AppendLine("    }");
            ps1Script.AppendLine("}");
            ps1Script.AppendLine();
            ps1Script.AppendLine("if ($deletedCount -ne 0) {");
            ps1Script.AppendLine("    Write-Host \"Deleted $deletedCount files.\"");
            ps1Script.AppendLine("}");
            ps1Script.AppendLine();
            ps1Script.AppendLine("foreach ($file in $filesInBackup) {");
            ps1Script.AppendLine("    try {");
            ps1Script.AppendLine("        $relativePath = $file.FullName.Substring($mostRecentBackupFolder.Length)");
            ps1Script.AppendLine("        $destinationPath = Join-Path \"" + mainFolder.Replace("\\", "\\\\") + "\" -ChildPath $relativePath");
            ps1Script.AppendLine();
            ps1Script.AppendLine("        # Create the directory structure if it doesn't exist");
            ps1Script.AppendLine("        $destinationDir = [System.IO.Path]::GetDirectoryName($destinationPath)");
            ps1Script.AppendLine("        if (-not (Test-Path -LiteralPath $destinationDir)) {");
            ps1Script.AppendLine("            New-Item -LiteralPath $destinationDir -ItemType Directory -Force");
            ps1Script.AppendLine("        }");
            ps1Script.AppendLine();
            ps1Script.AppendLine("        # Copy the file to the destination");
            ps1Script.AppendLine("        Copy-Item -LiteralPath $file.FullName -Destination $destinationPath -Force");
            ps1Script.AppendLine("        Write-Host \"Restoring backup of '$($file.Name)' to '$destinationDir'...\"");
            ps1Script.AppendLine("    } catch {");
            ps1Script.AppendLine("        Write-Host \"Failed to restore backup of $($file.Name) because of: $($_.Exception.Message)\"");
            ps1Script.AppendLine("    }");
            ps1Script.AppendLine("}");
            ps1Script.AppendLine("Pause");

            File.WriteAllText(ps1Path, ps1Script.ToString(), Encoding.UTF8);

            // Bash script
            string shPath = Path.Combine(uninstallFolder, "uninstall.sh");
            var shScript = new StringBuilder();
            shScript.AppendLine("#!/bin/bash");
            shScript.AppendLine();
            shScript.AppendLine("backupParentFolder=\"../backup\"");
            shScript.AppendLine("mostRecentBackupFolder=$(ls -d \"$backupParentFolder\"/* | while read -r dir; do");
            shScript.AppendLine("    dirName=$(basename \"$dir\")");
            shScript.AppendLine("    if [[ \"$dirName\" =~ ^[0-9]{4}-[0-9]{2}-[0-9]{2}_[0-9]{2}\\.[0-9]{2}\\.[0-9]{2}$ ]]; then");
            shScript.AppendLine("        # Convert the directory name to a sortable format YYYYMMDDHHMMSS and echo both the sortable format and the original directory");
            shScript.AppendLine("        echo \"${dirName:0:4}${dirName:5:2}${dirName:8:2}${dirName:11:2}${dirName:14:2}${dirName:17:2} $dir\"");
            shScript.AppendLine("    else");
            shScript.AppendLine("        if [[ -n \"$dirName\" && ! \"$dirName\" =~ ^[[:space:]]*$ ]]; then");
            shScript.AppendLine("            echo \"Ignoring directory '$dirName'\" >&2");
            shScript.AppendLine("        fi");
            shScript.AppendLine("    fi");
            shScript.AppendLine("done | sort -r | awk 'NR==1 {print $2}')");
            shScript.AppendLine();
            shScript.AppendLine();
            shScript.AppendLine();
            shScript.AppendLine("if [[ ! -d \"$mostRecentBackupFolder\" ]]; then");
            shScript.AppendLine("    mostRecentBackupFolder=\"" + backupDir.Replace("\\", "/") + "\"");
            shScript.AppendLine("    if [[ ! -d \"$mostRecentBackupFolder\" ]]; then");
            shScript.AppendLine("        echo \"No backups found in '$backupParentFolder'\"");
            shScript.AppendLine("        read -rp \"Press enter to continue...\"");
            shScript.AppendLine("        exit 1");
            shScript.AppendLine("    fi");
            shScript.AppendLine("    echo \"Using hardcoded backup dir: '$mostRecentBackupFolder'\"");
            shScript.AppendLine("else");
            shScript.AppendLine("    echo \"Selected backup folder '$mostRecentBackupFolder'\"");
            shScript.AppendLine("fi");
            shScript.AppendLine();
            shScript.AppendLine("existingFiles=()");
            shScript.AppendLine("deleteListFile=\"$mostRecentBackupFolder/remove these files.txt\"");
            shScript.AppendLine("if [[ ! -f \"$deleteListFile\" ]]; then");
            shScript.AppendLine("    echo \"File list not found.\"");
            shScript.AppendLine("    #exit 1");
            shScript.AppendLine("else");
            shScript.AppendLine("    declare -a filesToDelete");
            shScript.AppendLine("    mapfile -t filesToDelete < \"$deleteListFile\"");
            shScript.AppendLine("    echo \"Building file lists...\"");
            shScript.AppendLine("    for file in \"${filesToDelete[@]}\"; do");
            shScript.AppendLine("        normalizedFile=$(echo \"$file\" | tr '\\\\' '/')");
            shScript.AppendLine("        if [[ -n \"$file\" && -f \"$file\" ]]; then");
            shScript.AppendLine("            existingFiles+=(\"$file\")");
            shScript.AppendLine("        else");
            shScript.AppendLine("            echo \"WARNING! $file no longer exists! Running this script is no longer recommended!\"");
            shScript.AppendLine("        fi");
            shScript.AppendLine("    done");
            shScript.AppendLine("fi");
            shScript.AppendLine();
            shScript.AppendLine();
            shScript.AppendLine("fileCount=$(find \"$mostRecentBackupFolder\" -type f ! -name 'remove these files.txt' | wc -l)");
            shScript.AppendLine("folderCount=$(find \"$mostRecentBackupFolder\" -type d | wc -l)");
            shScript.AppendLine();
            shScript.AppendLine("# Display relative file paths if file count is less than 6");
            shScript.AppendLine("if [[ $fileCount -lt 6 ]]; then");
            shScript.AppendLine("    find \"$mostRecentBackupFolder\" -type f ! -name 'remove these files.txt' | sed \"s|^$mostRecentBackupFolder/||\"");
            shScript.AppendLine("fi");
            shScript.AppendLine();
            shScript.AppendLine("read -rp \"Really uninstall ${#existingFiles[@]} files and restore the most recent backup (containing $fileCount files and $folderCount folders)? \" confirmation");
            shScript.AppendLine("if [[ \"$confirmation\" != \"y\" && \"$confirmation\" != \"yes\" ]]; then");
            shScript.AppendLine("    echo \"Operation cancelled.\"");
            shScript.AppendLine("    exit 1");
            shScript.AppendLine("fi");
            shScript.AppendLine();
            shScript.AppendLine("deletedCount=0");
            shScript.AppendLine("for file in \"${existingFiles[@]}\"; do");
            shScript.AppendLine("    if [[ -f \"$file\" ]]; then");
            shScript.AppendLine("        rm -f \"$file\"");
            shScript.AppendLine("        echo \"Removed $file...\"");
            shScript.AppendLine("        ((deletedCount++))");
            shScript.AppendLine("    fi");
            shScript.AppendLine("done");
            shScript.AppendLine();
            shScript.AppendLine("if [[ $deletedCount -ne 0 ]]; then");
            shScript.AppendLine("    echo \"Deleted $deletedCount files.\"");
            shScript.AppendLine("fi");
            shScript.AppendLine();
            shScript.AppendLine("while IFS= read -r -d $'\\0' file; do");
            shScript.AppendLine("    relativePath=${file#$mostRecentBackupFolder}");
            shScript.AppendLine("    destinationPath=\"" + mainFolder.Replace("\\", "/") + "/$relativePath\"");
            shScript.AppendLine("    destinationDir=$(dirname \"$destinationPath\")");
            shScript.AppendLine("    if [[ ! -d \"$destinationDir\" ]]; then");
            shScript.AppendLine("        mkdir -p \"$destinationDir\"");
            shScript.AppendLine("    fi");
            shScript.AppendLine("    cp \"$file\" \"$destinationPath\" && echo \"Restoring backup of '$(basename $file)' to '$destinationDir'...\"");
            shScript.AppendLine("done < <(find \"$mostRecentBackupFolder\" -type f ! -name 'remove these files.txt' -print0)");
            shScript.AppendLine();
            shScript.AppendLine("read -rp \"Press enter to continue...\"");

            File.WriteAllText(shPath, shScript.ToString(), Encoding.UTF8);
        }
    }
}
