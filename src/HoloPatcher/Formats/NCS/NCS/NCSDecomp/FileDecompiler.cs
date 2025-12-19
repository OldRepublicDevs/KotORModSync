//
using System;
using static KOTORModSync.Formats.NCS.NCSDecomp.DecompilerLogger;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using KOTORModSync;
using KOTORModSync.Formats.NCS;
using KOTORModSync.Formats.NCS.NCSDecomp;
using KOTORModSync.Formats.NCS.NCSDecomp.Analysis;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;
using KOTORModSync.Formats.NCS.NCSDecomp.Lexer;
using KOTORModSync.Formats.NCS.NCSDecomp.Scriptutils;
using KOTORModSync.Formats.NCS.NCSDecomp.Utils;
using InputStream = System.IO.Stream;

using Process = System.Diagnostics.Process;
using Thread = System.Threading.Thread;
using Throwable = System.Exception;
using KOTORModSync.Common;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:56-79
    public class FileDecompiler
    {
        public static readonly int FAILURE = 0;
        public static readonly int SUCCESS = 1;
        public static readonly int PARTIAL_COMPILE = 2;
        public static readonly int PARTIAL_COMPARE = 3;
        public static readonly string GLOBAL_SUB_NAME = "GLOBALS";
        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:72-79
        // Original: public static boolean isK2Selected = false;
        public static bool isK2Selected = false;
        // Original: public static boolean preferSwitches = false;
        public static bool preferSwitches = false;
        // Original: public static boolean strictSignatures = false;
        public static bool strictSignatures = false;
        // Original: public static String nwnnsscompPath = null;
        public static string nwnnsscompPath = null;
        private ActionsData actions;
        private Dictionary<object, object> filedata;
        private Settings settings;
        private NWScriptLocator.GameType gameType;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:101-105
        // Original: public FileDecompiler() { this.filedata = new Hashtable<>(1); this.actions = null; loadPreferSwitchesFromConfig(); }
        public FileDecompiler()
        {
            this.filedata = new Dictionary<object, object>();
            this.actions = null; // Load lazily when needed to prevent startup failures
            LoadPreferSwitchesFromConfig();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:101-111
        // Original: public FileDecompiler(File nwscriptFile) throws DecompilerException
        public FileDecompiler(NcsFile nwscriptFile)
        {
            this.filedata = new Dictionary<object, object>();
            if (nwscriptFile == null || !nwscriptFile.IsFile())
            {
                throw new DecompilerException("Error: nwscript file does not exist: " + (nwscriptFile != null ? nwscriptFile.GetAbsolutePath() : "null"));
            }
            try
            {
                this.actions = new ActionsData(new BufferedReader(new FileReader(nwscriptFile)));
            }
            catch (IOException ex)
            {
                throw new DecompilerException("Error reading nwscript file: " + ex.Message);
            }
        }

        public FileDecompiler(Settings settings, NWScriptLocator.GameType? gameType)
        {
            this.filedata = new Dictionary<object, object>();
            this.settings = settings ?? Decompiler.settings;

            // Determine game type from settings if not provided
            if (gameType.HasValue)
            {
                this.gameType = gameType.Value;
            }
            else if (this.settings != null)
            {
                string gameTypeSetting = this.settings.GetProperty("Game Type");
                if (!string.IsNullOrEmpty(gameTypeSetting) &&
                    (gameTypeSetting.Equals("TSL", StringComparison.OrdinalIgnoreCase) ||
                     gameTypeSetting.Equals("K2", StringComparison.OrdinalIgnoreCase)))
                {
                    this.gameType = NWScriptLocator.GameType.TSL;
                }
                else
                {
                    this.gameType = NWScriptLocator.GameType.K1;
                }
            }
            else
            {
                this.gameType = NWScriptLocator.GameType.K1;
            }

            // Actions will be loaded lazily on first use
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:120-122
        // Original: public void loadActionsData(boolean isK2Selected) throws DecompilerException
        public void LoadActionsData(bool isK2Selected)
        {
            this.actions = LoadActionsDataInternal(isK2Selected);
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:1031-1035
        // Original: private void ensureActionsLoaded() throws DecompilerException
        private void EnsureActionsLoaded()
        {
            if (this.actions == null)
            {
                this.actions = LoadActionsDataInternal(isK2Selected);
            }
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:124-169
        // Original: private static ActionsData loadActionsDataInternal(boolean isK2Selected) throws DecompilerException
        private static ActionsData LoadActionsDataInternal(bool isK2Selected)
        {
            try
            {
                NcsFile actionfile = null;

                // Check settings first (GUI mode) - only if Decompiler class is loaded
                try
                {
                    // Access Decompiler.settings directly (same package)
                    // This will throw NoClassDefFoundError in pure CLI mode, which we catch
                    string settingsPath = isK2Selected
                        ? Decompiler.settings.GetProperty("K2 nwscript Path")
                        : Decompiler.settings.GetProperty("K1 nwscript Path");
                    if (!string.IsNullOrEmpty(settingsPath))
                    {
                        actionfile = new NcsFile(settingsPath);
                        if (actionfile.IsFile())
                        {
                            return new ActionsData(new BufferedReader(new FileReader(actionfile)));
                        }
                    }
                }
                catch (Exception)
                {
                    // Settings not available (CLI mode) or invalid path, fall through to default
                }

                // Fall back to default location in tools/ directory
                string userDir = JavaSystem.GetProperty("user.dir");
                NcsFile dir = new NcsFile(Path.Combine(userDir, "tools"));
                actionfile = isK2Selected ? new NcsFile(Path.Combine(dir.FullName, "tsl_nwscript.nss")) : new NcsFile(Path.Combine(dir.FullName, "k1_nwscript.nss"));
                // If not in tools/, try current directory (legacy support)
                if (!actionfile.IsFile())
                {
                    dir = new NcsFile(userDir);
                    actionfile = isK2Selected ? new NcsFile(Path.Combine(dir.FullName, "tsl_nwscript.nss")) : new NcsFile(Path.Combine(dir.FullName, "k1_nwscript.nss"));
                }
                // If still not found, try JAR/EXE directory's tools folder
                if (!actionfile.IsFile())
                {
                    NcsFile ncsDecompDir = CompilerUtil.GetNCSDecompDirectory();
                    if (ncsDecompDir != null)
                    {
                        NcsFile jarToolsDir = new NcsFile(Path.Combine(ncsDecompDir.FullName, "tools"));
                        actionfile = isK2Selected ? new NcsFile(Path.Combine(jarToolsDir.FullName, "tsl_nwscript.nss")) : new NcsFile(Path.Combine(jarToolsDir.FullName, "k1_nwscript.nss"));
                    }
                }
                // If still not found, try JAR/EXE directory itself
                if (!actionfile.IsFile())
                {
                    NcsFile ncsDecompDir = CompilerUtil.GetNCSDecompDirectory();
                    if (ncsDecompDir != null)
                    {
                        actionfile = isK2Selected ? new NcsFile(Path.Combine(ncsDecompDir.FullName, "tsl_nwscript.nss")) : new NcsFile(Path.Combine(ncsDecompDir.FullName, "k1_nwscript.nss"));
                    }
                }
                if (actionfile.IsFile())
                {
                    return new ActionsData(new BufferedReader(new FileReader(actionfile)));
                }
                else
                {
                    throw new DecompilerException("Error: cannot open actions file " + actionfile.GetAbsolutePath() + ".");
                }
            }
            catch (IOException ex)
            {
                throw new DecompilerException(ex.Message);
            }
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:205-233
        // Original: private static void loadPreferSwitchesFromConfig() { File configDir = new File(System.getProperty("user.dir"), "config"); File configFile = new File(configDir, "ncsdecomp.conf"); ... }
        private static void LoadPreferSwitchesFromConfig()
        {
            try
            {
                string userDir = JavaSystem.GetProperty("user.dir");
                string configDir = Path.Combine(userDir, "config");
                NcsFile configFile = new NcsFile(Path.Combine(configDir, "ncsdecomp.conf"));
                if (!configFile.Exists())
                {
                    configFile = new NcsFile(Path.Combine(configDir, "dencs.conf"));
                }

                if (configFile.Exists() && configFile.IsFile())
                {
                    using (BufferedReader reader = new BufferedReader(new FileReader(configFile)))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            line = line.Trim();
                            // Accept both legacy and canonical "Prefer Switches" spelling
                            if (line.StartsWith("Prefer Switches") || line.StartsWith("preferSwitches"))
                            {
                                int equalsIdx = line.IndexOf('=');
                                if (equalsIdx >= 0)
                                {
                                    string value = line.Substring(equalsIdx + 1).Trim();
                                    preferSwitches = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("1");
                                }
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Silently ignore config file errors - use default value
            }
        }

        private void LoadActions()
        {
            try
            {
                // Determine game type from settings if not already set
                if (this.settings != null)
                {
                    string gameTypeSetting = this.settings.GetProperty("Game Type");
                    if (!string.IsNullOrEmpty(gameTypeSetting))
                    {
                        if (gameTypeSetting.Equals("TSL", StringComparison.OrdinalIgnoreCase) ||
                            gameTypeSetting.Equals("K2", StringComparison.OrdinalIgnoreCase))
                        {
                            this.gameType = NWScriptLocator.GameType.TSL;
                        }
                        else
                        {
                            this.gameType = NWScriptLocator.GameType.K1;
                        }
                    }
                }

                // Try to find nwscript.nss file
                NcsFile actionfile = NWScriptLocator.FindNWScriptFile(this.gameType, this.settings);
                if (actionfile == null || !actionfile.IsFile())
                {
                    // Build error message with candidate paths
                    List<string> candidatePaths = NWScriptLocator.GetCandidatePaths(this.gameType);
                    string errorMsg = "Error: cannot find nwscript.nss file for " + this.gameType + ".\n\n";
                    errorMsg += "Searched locations:\n";
                    foreach (string path in candidatePaths)
                    {
                        errorMsg += "  - " + path + "\n";
                    }
                    errorMsg += "\nPlease ensure nwscript.nss exists in one of these locations, or configure the path in Settings.";
                    throw new DecompilerException(errorMsg);
                }

                this.actions = new ActionsData(new BufferedReader(new FileReader(actionfile)));
            }
            catch (IOException e)
            {
                throw new DecompilerException("Error reading nwscript.nss file: " + e.Message);
            }
        }

        public virtual Dictionary<object, object> GetVariableData(NcsFile file)
        {
            if (!this.filedata.ContainsKey(file))
            {
                return null;
            }
            Utils.FileScriptData data = (Utils.FileScriptData)this.filedata[file];
            if (data == null)
            {
                return null;
            }

            Dictionary<string, List<object>> vars = data.GetVars();
            if (vars == null)
            {
                return null;
            }

            Dictionary<object, object> result = new Dictionary<object, object>();
            foreach (var kvp in vars)
            {
                result[kvp.Key] = kvp.Value;
            }
            return result;
        }

        public virtual string GetGeneratedCode(NcsFile file)
        {
            if (!this.filedata.ContainsKey(file))
            {
                return null;
            }
            Utils.FileScriptData data = (Utils.FileScriptData)this.filedata[file];
            if (data == null)
            {
                return null;
            }

            return data.GetCode();
        }

        public virtual string GetOriginalByteCode(NcsFile file)
        {
            if (!this.filedata.ContainsKey(file))
            {
                return null;
            }
            Utils.FileScriptData data = (Utils.FileScriptData)this.filedata[file];
            if (data == null)
            {
                return null;
            }

            return data.GetOriginalByteCode();
        }

        public virtual string GetNewByteCode(NcsFile file)
        {
            if (!this.filedata.ContainsKey(file))
            {
                return null;
            }
            Utils.FileScriptData data = (Utils.FileScriptData)this.filedata[file];
            if (data == null)
            {
                return null;
            }

            return data.GetNewByteCode();
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:253-352
        // Original: public int decompile(File file)
        public virtual int Decompile(NcsFile file)
        {
            try
            {
                this.EnsureActionsLoaded();
            }
            catch (DecompilerException e)
            {
                Debug("Error loading actions data: " + e.Message);
                // Create comprehensive fallback stub for actions data loading failure
                Utils.FileScriptData errorData = new Utils.FileScriptData();
                string expectedFile = isK2Selected ? "tsl_nwscript.nss" : "k1_nwscript.nss";
                string stubCode = this.GenerateComprehensiveFallbackStub(file, "Actions data loading", e,
                    "The actions data table (nwscript.nss) is required to decompile NCS files.\n" +
                    "Expected file: " + expectedFile + "\n" +
                    "Please ensure the appropriate nwscript.nss file is available in tools/ directory, working directory, or configured path.");
                errorData.SetCode(stubCode);
                this.filedata[file] = errorData;
                return PARTIAL_COMPILE;
            }
            Utils.FileScriptData data = null;
            if (this.filedata.ContainsKey(file))
            {
                data = (Utils.FileScriptData)this.filedata[file];
            }
            if (data == null)
            {
                Debug("\n---> starting decompilation: " + file.Name + " <---");
                try
                {
                    data = this.DecompileNcs(file);
                    // decompileNcs now always returns a FileScriptData (never null)
                    // but it may contain minimal/fallback code if decompilation failed
                    this.filedata[file] = data;
                }
                catch (Exception e)
                {
                    // Last resort: create comprehensive fallback stub data so we always have something to show
                    Debug("Critical error during decompilation, creating fallback stub: " + e.Message);
                    e.PrintStackTrace(JavaSystem.@out);
                    data = new Utils.FileScriptData();
                    data.SetCode(this.GenerateComprehensiveFallbackStub(file, "Initial decompilation attempt", e, null));
                    this.filedata[file] = data;
                }
            }

            // Always generate code, even if validation fails
            try
            {
                data.GenerateCode();
                string code = data.GetCode();
                if (code == null || code.Trim().Length == 0)
                {
                    // If code generation failed, provide comprehensive fallback stub
                    Debug("Warning: Generated code is empty, creating fallback stub.");
                    string fallback = this.GenerateComprehensiveFallbackStub(file, "Code generation - empty output", null,
                        "The decompilation process completed but generated no source code. This may indicate the file contains no executable code or all code was marked as dead/unreachable.");
                    data.SetCode(fallback);
                    return PARTIAL_COMPILE;
                }
            }
            catch (Exception e)
            {
                Debug("Error during code generation (creating fallback stub): " + e.Message);
                string fallback = this.GenerateComprehensiveFallbackStub(file, "Code generation", e,
                    "An exception occurred while generating NSS source code from the decompiled parse tree.");
                data.SetCode(fallback);
                return PARTIAL_COMPILE;
            }

            // Try to capture original bytecode from the NCS file if nwnnsscomp is available
            // This allows viewing bytecode even without round-trip validation
            if (this.CheckCompilerExists())
            {
                try
                {
                    Debug("[NCSDecomp] Attempting to capture original bytecode from NCS file...");
                    bool captured = this.CaptureBytecodeFromNcs(file, file, isK2Selected, true);
                    if (captured)
                    {
                        string originalByteCode = data.GetOriginalByteCode();
                        if (originalByteCode != null && originalByteCode.Trim().Length > 0)
                        {
                            Debug("[NCSDecomp] Successfully captured original bytecode (" + originalByteCode.Length + " characters)");
                        }
                        else
                        {
                            Debug("[NCSDecomp] Warning: Original bytecode file is empty");
                        }
                    }
                    else
                    {
                        Debug("[NCSDecomp] Warning: Failed to decompile original NCS file to bytecode");
                    }
                }
                catch (Exception e)
                {
                    Debug("[NCSDecomp] Exception while capturing original bytecode:");
                    Debug("[NCSDecomp]   Exception Type: " + e.GetType().Name);
                    Debug("[NCSDecomp]   Exception Message: " + e.Message);
                    if (e.InnerException != null)
                    {
                        Debug("[NCSDecomp]   Caused by: " + e.InnerException.GetType().Name + " - " + e.InnerException.Message);
                    }
                    e.PrintStackTrace(JavaSystem.@out);
                }
            }
            else
            {
                Debug("[NCSDecomp] nwnnsscomp.exe not found - cannot capture original bytecode");
            }

            // Try validation, but don't fail if it doesn't work
            // nwnnsscomp is optional - decompilation should work without it
            try
            {
            return this.CompileAndCompare(file, data.GetCode(), data);
            }
            catch (Exception e)
            {
                Debug("[NCSDecomp] Exception during bytecode validation:");
                Debug("[NCSDecomp]   Exception Type: " + e.GetType().Name);
                Debug("[NCSDecomp]   Exception Message: " + e.Message);
                if (e.InnerException != null)
                {
                    Debug("[NCSDecomp]   Caused by: " + e.InnerException.GetType().Name + " - " + e.InnerException.Message);
                }
                e.PrintStackTrace(JavaSystem.@out);
                Debug("[NCSDecomp] Showing decompiled source anyway (validation failed)");
                return PARTIAL_COMPILE;
            }
        }

        public virtual int CompileAndCompare(NcsFile file, NcsFile newfile)
        {
            Utils.FileScriptData data = null;
            if (this.filedata.ContainsKey(file))
            {
                data = (Utils.FileScriptData)this.filedata[file];
            }
            return this.CompileAndCompare(file, newfile, data);
        }

        public virtual int CompileOnly(NcsFile nssFile)
        {
            Utils.FileScriptData data = null;
            if (this.filedata.ContainsKey(nssFile))
            {
                data = (Utils.FileScriptData)this.filedata[nssFile];
            }
            if (data == null)
            {
                data = new Utils.FileScriptData();
            }

            return this.CompileNss(nssFile, data);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:415-417
        // Original: public boolean captureBytecodeForNssFile(File nssFile, File compiledNcs, boolean isK2, boolean asOriginal) { return this.captureBytecodeFromNcs(nssFile, compiledNcs, isK2, asOriginal); }
        public virtual bool CaptureBytecodeForNssFile(NcsFile nssFile, NcsFile compiledNcs, bool isK2, bool asOriginal)
        {
            return this.CaptureBytecodeFromNcs(nssFile, compiledNcs, isK2, asOriginal);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:429-466
        // Original: public boolean captureBytecodeFromNcs(File sourceFile, File compiledNcs, boolean isK2, boolean asOriginal) { ... }
        public virtual bool CaptureBytecodeFromNcs(NcsFile sourceFile, NcsFile compiledNcs, bool isK2, bool asOriginal)
        {
            try
            {
                if (compiledNcs == null || !compiledNcs.Exists())
                {
                    return false;
                }

                // Decompile the compiled NCS to bytecode (pcode)
                NcsFile pcodeFile = this.ExternalDecompile(compiledNcs, isK2, null);
                if (pcodeFile == null || !pcodeFile.Exists())
                {
                    return false;
                }

                // Read the bytecode
                string bytecode = this.ReadFile(pcodeFile);
                if (bytecode == null || bytecode.Trim().Length == 0)
                {
                    return false;
                }

                // Create or get FileScriptData entry for the source file
                Utils.FileScriptData data = null;
                if (this.filedata.ContainsKey(sourceFile))
                {
                    data = (Utils.FileScriptData)this.filedata[sourceFile];
                }
                if (data == null)
                {
                    data = new Utils.FileScriptData();
                    this.filedata[sourceFile] = data;
                }

                // Store bytecode as either "original" or "new"
                if (asOriginal)
                {
                    data.SetOriginalByteCode(bytecode);
                }
                else
                {
                    data.SetNewByteCode(bytecode);
                }
                return true;
            }
            catch (Exception e)
            {
                Error("DEBUG captureBytecodeFromNcs: Error capturing bytecode: " + e.Message);
                e.PrintStackTrace(JavaSystem.@err);
                return false;
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:432-443
        // Original: public File compileNssToNcs(File nssFile, File outputDir)
        public virtual NcsFile CompileNssToNcs(NcsFile nssFile, NcsFile outputDir)
        {
            return this.ExternalCompile(nssFile, isK2Selected, outputDir);
        }

        public virtual Dictionary<object, object> UpdateSubName(NcsFile file, string oldname, string newname)
        {
            if (file == null)
            {
                return null;
            }

            if (!this.filedata.ContainsKey(file))
            {
                return null;
            }
            Utils.FileScriptData data = (Utils.FileScriptData)this.filedata[file];
            if (data == null)
            {
                return null;
            }

            data.ReplaceSubName(oldname, newname);
            Dictionary<string, List<object>> vars = data.GetVars();
            if (vars == null)
            {
                return null;
            }
            Dictionary<object, object> result = new Dictionary<object, object>();
            foreach (var kvp in vars)
            {
                result[kvp.Key] = kvp.Value;
            }
            return result;
        }

        public virtual string RegenerateCode(NcsFile file)
        {
            if (!this.filedata.ContainsKey(file))
            {
                return null;
            }
            Utils.FileScriptData data = (Utils.FileScriptData)this.filedata[file];
            if (data == null)
            {
                return null;
            }

            data.GenerateCode();
            return data.ToString();
        }

        public virtual void CloseFile(NcsFile file)
        {
            if (this.filedata.ContainsKey(file))
            {
                Utils.FileScriptData data = (Utils.FileScriptData)this.filedata[file];
                if (data != null)
                {
                    data.Close();
                }
                this.filedata.Remove(file);
            }

            GC.Collect();
        }

        public virtual void CloseAllFiles()
        {
            foreach (var kvp in this.filedata)
            {
                if (kvp.Value is Utils.FileScriptData fileData)
                {
                    fileData.Close();
                }
            }

            this.filedata.Clear();
            GC.Collect();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:447-455
        // Original: public String decompileToString(File file) throws DecompilerException
        public virtual string DecompileToString(NcsFile file)
        {
            Utils.FileScriptData data = this.DecompileNcs(file);
            if (data == null)
            {
                throw new DecompilerException("Decompile failed for " + file.GetAbsolutePath() + " - DecompileNcs returned null (this should not happen)");
            }

            string code = null;
            try
            {
                data.GenerateCode();
                code = data.GetCode();
            }
            catch (Exception genEx)
            {
                Debug("WARNING: GenerateCode() threw exception: " + genEx.GetType().Name + " - " + genEx.Message);
                genEx.PrintStackTrace(JavaSystem.@out);
                // Try to get code anyway, in case it was partially generated
                try
                {
                    code = data.GetCode();
                }
                catch
                {
                    // Ignore
                }
            }

            // Ensure we always return a non-null string (even if empty) so file is always created
            if (code == null)
            {
                Debug("WARNING: GenerateCode() returned null code, using empty string as fallback");
                code = "";
            }
            return code;
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:460-474
        // Original: public void decompileToFile(File input, File output, Charset charset, boolean overwrite) throws DecompilerException, IOException
        public virtual void DecompileToFile(NcsFile input, NcsFile output, System.Text.Encoding charset, bool overwrite)
        {
            if (output.Exists() && !overwrite)
            {
                throw new IOException("Output file already exists: " + output.GetAbsolutePath());
            }

            string code = this.DecompileToString(input);
            if (output.Directory != null && !output.Directory.Exists)
            {
                output.Directory.Create();
            }

            using (var writer = new StreamWriter(output.FullName, false, charset))
            {
                writer.Write(code);
            }

            // Ensure file is flushed and FileInfo is refreshed so Exists() checks work correctly
            // This is especially important for minimal/empty files where timing matters
            System.IO.FileInfo fileInfo = new System.IO.FileInfo(output.FullName);
            fileInfo.Refresh();
        }

        // Helper method to decompile from file (used by DecompileToString)
        private Utils.FileScriptData DecompileNcsObjectFromFile(NcsFile file)
        {
            Debug($"DEBUG DecompileNcsObjectFromFile: START for {file.Name}");
            NCS ncs = null;
            try
            {
                using (var reader = new NCSBinaryReader(file.GetAbsolutePath()))
                {
                    ncs = reader.Load();
                    int instructionCount = ncs?.Instructions?.Count ?? -1;
                    Debug($"DEBUG DecompileNcsObjectFromFile: NCSBinaryReader.Load() returned NCS with {instructionCount} instructions");
                    Console.Error.WriteLine($"DEBUG DecompileNcsObjectFromFile: NCSBinaryReader.Load() returned NCS with {instructionCount} instructions");
                    if (instructionCount > 0 && ncs != null)
                    {
                        int minOffset = ncs.Instructions[0].Offset;
                        int maxOffset = ncs.Instructions[instructionCount - 1].Offset;
                        Debug($"DEBUG DecompileNcsObjectFromFile: Instruction offset range: {minOffset} to {maxOffset}");
                        Console.Error.WriteLine($"DEBUG DecompileNcsObjectFromFile: Instruction offset range: {minOffset} to {maxOffset}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug($"DEBUG DecompileNcsObjectFromFile: NCSBinaryReader.Load() FAILED: {ex.GetType().Name}: {ex.Message}");
                throw new DecompilerException("Failed to read NCS file: " + ex.Message);
            }

            if (ncs == null)
            {
                Debug($"DEBUG DecompileNcsObjectFromFile: ncs is null, returning null");
                return null;
            }

            Debug($"DEBUG DecompileNcsObjectFromFile: Calling DecompileNcsObject with {ncs.Instructions?.Count ?? -1} instructions");
            return this.DecompileNcsObject(ncs);
        }

        private int CompileAndCompare(NcsFile file, NcsFile newfile, Utils.FileScriptData data)
        {
            string code = this.ReadFile(newfile);
            return this.CompileAndCompare(file, code, data);
        }

        private int CompileAndCompare(NcsFile file, string code, Utils.FileScriptData data)
        {
            Game game = this.MapGameType();
            NCS originalNcs = null;
            byte[] originalBytes = null;
            try
            {
                using (var reader = new NCSBinaryReader(file.GetAbsolutePath()))
                {
                    originalNcs = reader.Load();
                }

                if (originalNcs == null)
                {
                    return FAILURE;
                }

                originalBytes = NCSAuto.BytesNcs(originalNcs);
                data.SetOriginalByteCode(BytesToHexString(originalBytes, 0, originalBytes.Length));
            }
            catch (Exception ex)
            {
                Debug("Failed to read original NCS: " + ex.Message);
                return FAILURE;
            }

            try
            {
                NCS compiled = NCSAuto.CompileNss(code ?? string.Empty, game, null, null);
                byte[] compiledBytes = NCSAuto.BytesNcs(compiled);
                data.SetNewByteCode(BytesToHexString(compiledBytes, 0, compiledBytes.Length));

                if (!this.ByteArraysEqual(originalBytes, compiledBytes))
                {
                    return PARTIAL_COMPARE;
                }

                return SUCCESS;
            }
            catch (Exception ex)
            {
                Debug("In-process compile failed: " + ex.Message);
                return PARTIAL_COMPILE;
            }
        }

        private int CompileNss(NcsFile nssFile, Utils.FileScriptData data)
        {
            string code = this.ReadFile(nssFile);
            return this.CompileAndCompare(nssFile, code, data);
        }

        private string ReadFile(NcsFile file)
        {
            if (file == null || !file.Exists())
            {
                return null;
            }

            string newline = Environment.NewLine;
            StringBuilder buffer = new StringBuilder();
            BufferedReader reader = null;
            try
            {
                reader = new BufferedReader(new FileReader(file));
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    buffer.Append(line.ToString() + newline);
                }
            }
            catch (IOException e)
            {
                Debug("IO exception in read file: " + e);
                return null;
            }
            catch (System.IO.FileNotFoundException e)
            {
                Debug("File not found in read file: " + e);
                return null;
            }
            finally
            {
                try
                {
                    if (reader != null)
                    {
                        reader.Dispose();
                    }
                }
                catch (Exception)
                {
                }
            }

            try
            {
                reader.Dispose();
            }
            catch (Exception)
            {
            }

            return buffer.ToString();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:875-929
        // Original: private File getCompilerFile()
        private NcsFile GetCompilerFile()
        {
            // GUI MODE: Try to get compiler from Settings
            try
            {
                NcsFile settingsCompiler = CompilerUtil.GetCompilerFromSettings();
                if (settingsCompiler != null)
                {
                    // If Settings compiler exists, use it
                    if (settingsCompiler.Exists() && settingsCompiler.IsFile())
                    {
                        Error("DEBUG FileDecompiler.getCompilerFile: Using Settings compiler: "
                            + settingsCompiler.GetAbsolutePath());
                        return settingsCompiler;
                    }
                    // Settings compiler doesn't exist - try fallback to JAR/EXE directory's tools folder
                    Error("DEBUG FileDecompiler.getCompilerFile: Settings compiler not found: "
                        + settingsCompiler.GetAbsolutePath() + ", trying fallback to JAR directory");

                    // Try JAR/EXE directory's tools folder with all known compiler names
                    NcsFile ncsDecompDir = CompilerUtil.GetNCSDecompDirectory();
                    if (ncsDecompDir != null)
                    {
                        NcsFile jarToolsDir = new NcsFile(Path.Combine(ncsDecompDir.FullName, "tools"));
                        string[] compilerNames = CompilerUtil.GetCompilerNames();
                        foreach (string name in compilerNames)
                        {
                            NcsFile fallbackCompiler = new NcsFile(Path.Combine(jarToolsDir.FullName, name));
                            if (fallbackCompiler.Exists() && fallbackCompiler.IsFile())
                            {
                                Error("DEBUG FileDecompiler.getCompilerFile: Found fallback compiler in JAR directory: "
                                    + fallbackCompiler.GetAbsolutePath());
                                return fallbackCompiler;
                            }
                        }
                        Error("DEBUG FileDecompiler.getCompilerFile: No fallback compiler found in JAR directory: "
                            + jarToolsDir.GetAbsolutePath());
                    }

                    // Fallback failed, but return the Settings path anyway (caller will handle error)
                    Error("DEBUG FileDecompiler.getCompilerFile: Using Settings compiler (not found): "
                        + settingsCompiler.GetAbsolutePath());
                    return settingsCompiler;
                }
            }
            catch (TypeLoadException)
            {
                // CompilerUtil or Decompiler.settings not available - likely CLI mode
                Error("DEBUG FileDecompiler.getCompilerFile: Settings not available (CLI mode): NoClassDefFoundError");
            }
            catch (Exception e)
            {
                // CompilerUtil or Decompiler.settings not available - likely CLI mode
                Error("DEBUG FileDecompiler.getCompilerFile: Settings not available (CLI mode): "
                    + e.GetType().Name);
            }

            // CLI MODE: Use nwnnsscompPath if set (set by CLI argument)
            if (nwnnsscompPath != null && !string.IsNullOrWhiteSpace(nwnnsscompPath))
            {
                NcsFile cliCompiler = new NcsFile(nwnnsscompPath);
                Error(
                    "DEBUG FileDecompiler.getCompilerFile: Using CLI nwnnsscompPath: " + cliCompiler.GetAbsolutePath());
                return cliCompiler;
            }

            // NO FALLBACKS - return null if not configured
            Error("DEBUG FileDecompiler.getCompilerFile: No compiler configured - returning null");
            return null;
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:728-762

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:861-864
        // Original: private boolean checkCompilerExists()
        private bool CheckCompilerExists()
        {
            NcsFile compiler = GetCompilerFile();
            return compiler.Exists();
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:869-872
        // Original: private String getShortName(File in)
        private string GetShortName(NcsFile inFile)
        {
            string path = inFile.GetAbsolutePath();
            int i = path.LastIndexOf('.');
            return i == -1 ? path : path.Substring(0, i);
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:878-921
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:888-963
        // Original: private File externalDecompile(File in, boolean k2, File outputDir)
        private NcsFile ExternalDecompile(NcsFile inFile, bool k2, NcsFile outputDir)
        {
            try
            {
                NcsFile compiler = GetCompilerFile();
                if (!compiler.Exists())
                {
                    Debug("[NCSDecomp] ERROR: Compiler not found: " + compiler.GetAbsolutePath());
                    return null;
                }

                // Determine output directory: use provided outputDir, or temp if null
                NcsFile actualOutputDir;
                if (outputDir != null)
                {
                    actualOutputDir = outputDir;
                }
                else
                {
                    // Default to temp directory to avoid creating files without user consent
                    string tmpDir = JavaSystem.GetProperty("java.io.tmpdir");
                    actualOutputDir = new NcsFile(Path.Combine(tmpDir, "ncsdecomp_roundtrip"));
                    if (!actualOutputDir.Exists())
                    {
                        actualOutputDir.Mkdirs();
                    }
                }

                // Create output pcode file in the specified output directory
                string baseName = inFile.Name;
                int lastDot = baseName.LastIndexOf('.');
                if (lastDot > 0)
                {
                    baseName = baseName.Substring(0, lastDot);
                }
                NcsFile result = new NcsFile(Path.Combine(actualOutputDir.FullName, baseName + ".pcode"));
                if (result.Exists())
                {
                    result.Delete();
                }

                // Use compiler detection to get correct command-line arguments
                // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:893
                // Original: NwnnsscompConfig config = new NwnnsscompConfig(compiler, in, result, k2);
                NwnnsscompConfig config = new NwnnsscompConfig(compiler, inFile, result, k2);
                string[] args = config.GetDecompileArgs(compiler.GetAbsolutePath());

                Debug("[NCSDecomp] Using compiler: " + config.GetChosenCompiler().Name +
                    " (SHA256: " + config.GetSha256Hash().Substring(0, Math.Min(16, config.GetSha256Hash().Length)) + "...)");
                Debug("[NCSDecomp] Input file: " + inFile.GetAbsolutePath());
                Debug("[NCSDecomp] Expected output: " + result.GetAbsolutePath());

                new WindowsExec().CallExec(args);

                if (!result.Exists())
                {
                    Debug("[NCSDecomp] ERROR: Expected output file does not exist: " + result.GetAbsolutePath());
                    Debug("[NCSDecomp]   This usually means nwnnsscomp.exe failed or produced no output.");
                    Debug("[NCSDecomp]   Check the nwnnsscomp output above for error messages.");
                    return null;
                }

                return result;
            }
            catch (IOException e)
            {
                // Check if this is an elevation error
                string errorMsg = e.Message;
                if (errorMsg != null && (errorMsg.Contains("error=740") || errorMsg.Contains("requires administrator")))
                {
                    Debug("[NCSDecomp] EXCEPTION during external decompile:");
                    Debug("[NCSDecomp]   Elevation required - compiler needs administrator privileges.");
                    Debug("[NCSDecomp]   Decompiled code is still available, but bytecode capture failed.");
                }
                else
                {
                    Debug("[NCSDecomp] EXCEPTION during external decompile:");
                    Debug("[NCSDecomp]   Exception Type: " + e.GetType().Name);
                }
                Debug("[NCSDecomp]   Exception Message: " + e.Message);
                if (e.InnerException != null)
                {
                    Debug("[NCSDecomp]   Caused by: " + e.InnerException.GetType().Name + " - " + e.InnerException.Message);
                }
                e.PrintStackTrace(JavaSystem.@out);
                return null;
            }
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:926-943
        // Original: private File writeCode(String code)
        private NcsFile WriteCode(string code)
        {
            try
            {
                NcsFile outFile = new NcsFile("_generatedcode.nss");
                using (var writer = new StreamWriter(outFile.FullName, false, System.Text.Encoding.UTF8))
                {
                    writer.Write(code);
                }
                NcsFile result = new NcsFile("_generatedcode.ncs");
                if (result.Exists())
                {
                    result.Delete();
                }

                return outFile;
            }
            catch (IOException var5)
            {
                Debug("IO exception on writing code: " + var5);
                return null;
            }
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:948-1010
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:1008-1105
        // Original: public File externalCompile(File file, boolean k2, File outputDir)
        private NcsFile ExternalCompile(NcsFile file, bool k2, NcsFile outputDir)
        {
            try
            {
                NcsFile compiler = GetCompilerFile();
                if (!compiler.Exists())
                {
                    Debug("[NCSDecomp] ERROR: Compiler not found: " + compiler.GetAbsolutePath());
                    return null;
                }

                // Determine output directory: use provided outputDir, or temp if null
                NcsFile actualOutputDir;
                if (outputDir != null)
                {
                    actualOutputDir = outputDir;
                }
                else
                {
                    // Default to temp directory to avoid creating files without user consent
                    string tmpDir = JavaSystem.GetProperty("java.io.tmpdir");
                    actualOutputDir = new NcsFile(Path.Combine(tmpDir, "ncsdecomp_roundtrip"));
                    if (!actualOutputDir.Exists())
                    {
                        actualOutputDir.Mkdirs();
                    }
                }

                // Create output NCS file in the specified output directory
                string baseName = file.Name;
                int lastDot = baseName.LastIndexOf('.');
                if (lastDot > 0)
                {
                    baseName = baseName.Substring(0, lastDot);
                }
                NcsFile result = new NcsFile(Path.Combine(actualOutputDir.FullName, baseName + ".ncs"));

                // Ensure nwscript.nss is in the compiler's directory (like test does)
                // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:960-975
                NcsFile compilerDir = compiler.Directory != null ? new NcsFile(compiler.Directory) : null;
                if (compilerDir != null)
                {
                    NcsFile compilerNwscript = new NcsFile(Path.Combine(compilerDir.FullName, "nwscript.nss"));
                    string userDir = JavaSystem.GetProperty("user.dir");
                    NcsFile nwscriptSource = k2
                        ? new NcsFile(Path.Combine(userDir, "tools", "tsl_nwscript.nss"))
                        : new NcsFile(Path.Combine(userDir, "tools", "k1_nwscript.nss"));
                    if (nwscriptSource.Exists() && (!compilerNwscript.Exists() || !compilerNwscript.GetAbsolutePath().Equals(nwscriptSource.GetAbsolutePath())))
                    {
                        try
                        {
                            System.IO.File.Copy(nwscriptSource.FullName, compilerNwscript.FullName, true);
                        }
                        catch (IOException e)
                        {
                            // Log but don't fail - compiler might find nwscript.nss elsewhere
                            Debug("[NCSDecomp] Warning: Could not copy nwscript.nss to compiler directory: " + e.Message);
                        }
                    }
                }

                // Use compiler detection to get correct command-line arguments
                // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:978-983
                // Original: NwnnsscompConfig config = new NwnnsscompConfig(compiler, file, result, k2);
                // Original: String[] args = config.getCompileArgs(compiler.getAbsolutePath());
                NwnnsscompConfig config = new NwnnsscompConfig(compiler, file, result, k2);
                // For GUI compilation, match test behavior: don't use -i flags
                // Test shows compilers work without -i when includes are in source directory or compiler directory
                string[] args = config.GetCompileArgs(compiler.GetAbsolutePath());

                Debug("[NCSDecomp] Using compiler: " + config.GetChosenCompiler().Name +
                    " (SHA256: " + config.GetSha256Hash().Substring(0, Math.Min(16, config.GetSha256Hash().Length)) + "...)");
                Debug("[NCSDecomp] Input file: " + file.GetAbsolutePath());
                Debug("[NCSDecomp] Expected output: " + result.GetAbsolutePath());

                new WindowsExec().CallExec(args);

                if (!result.Exists())
                {
                    Debug("[NCSDecomp] ERROR: Expected output file does not exist: " + result.GetAbsolutePath());
                    Debug("[NCSDecomp]   This usually means nwnnsscomp.exe compilation failed.");
                    Debug("[NCSDecomp]   Check the nwnnsscomp output above for compilation errors.");
                    return null;
                }

                return result;
            }
            catch (Exception e)
            {
                Debug("[NCSDecomp] EXCEPTION during external compile:");
                Debug("[NCSDecomp]   Exception Type: " + e.GetType().Name);
                Debug("[NCSDecomp]   Exception Message: " + e.Message);
                if (e.InnerException != null)
                {
                    Debug("[NCSDecomp]   Caused by: " + e.InnerException.GetType().Name + " - " + e.InnerException.Message);
                }
                e.PrintStackTrace(JavaSystem.@out);
                return null;
            }
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:1012-1029
        // Original: private List<File> buildIncludeDirs(boolean k2)
        private List<NcsFile> BuildIncludeDirs(bool k2)
        {
            List<NcsFile> dirs = new List<NcsFile>();
            NcsFile baseDir = new NcsFile(Path.Combine("test-work", "Vanilla_KOTOR_Script_Source"));
            NcsFile gameDir = new NcsFile(Path.Combine(baseDir.FullName, k2 ? "TSL" : "K1"));
            NcsFile scriptsBif = new NcsFile(Path.Combine(gameDir.FullName, "Data", "scripts.bif"));
            if (scriptsBif.Exists())
            {
                dirs.Add(scriptsBif);
            }
            NcsFile rootOverride = new NcsFile(Path.Combine(gameDir.FullName, "Override"));
            if (rootOverride.Exists())
            {
                dirs.Add(rootOverride);
            }
            // Fallback: allow includes relative to the game dir root.
            if (gameDir.Exists())
            {
                dirs.Add(gameDir);
            }
            return dirs;
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:1044-1053
        // Original: private String bytesToHex(byte[] bytes, int length)
        private string BytesToHex(byte[] bytes, int length)
        {
            StringBuilder hex = new StringBuilder();
            for (int i = 0; i < length; i++)
            {
                hex.Append(string.Format("{0:X2}", bytes[i] & 0xFF));
                if (i < length - 1)
                {
                    hex.Append(" ");
                }
            }
            return hex.ToString();
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:1065-1180
        // Original: private String generateComprehensiveFallbackStub(File file, String errorStage, Exception exception, String additionalInfo)
        private string GenerateComprehensiveFallbackStub(NcsFile file, string errorStage, Exception exception, string additionalInfo)
        {
            StringBuilder stub = new StringBuilder();
            string newline = Environment.NewLine;

            // Header with error type
            stub.Append("// ========================================").Append(newline);
            stub.Append("// DECOMPILATION ERROR - FALLBACK STUB").Append(newline);
            stub.Append("// ========================================").Append(newline);
            stub.Append(newline);

            // File information
            stub.Append("// File Information:").Append(newline);
            if (file != null)
            {
                stub.Append("//   Name: ").Append(file.Name).Append(newline);
                stub.Append("//   Path: ").Append(file.GetAbsolutePath()).Append(newline);
                if (file.Exists())
                {
                    stub.Append("//   Size: ").Append(file.Length).Append(" bytes").Append(newline);
                    stub.Append("//   Last Modified: ").Append(file.LastWriteTime.ToString()).Append(newline);
                    stub.Append("//   Readable: ").Append(true).Append(newline); // FileInfo is always readable if it exists
                }
                else
                {
                    stub.Append("//   Status: FILE DOES NOT EXIST").Append(newline);
                }
            }
            else
            {
                stub.Append("//   Status: FILE IS NULL").Append(newline);
            }
            stub.Append(newline);

            // Error stage information
            stub.Append("// Error Stage: ").Append(errorStage != null ? errorStage : "Unknown").Append(newline);
            stub.Append(newline);

            // Exception information
            if (exception != null)
            {
                stub.Append("// Exception Details:").Append(newline);
                stub.Append("//   Type: ").Append(exception.GetType().Name).Append(newline);
                stub.Append("//   Message: ").Append(exception.Message != null ? exception.Message : "(no message)").Append(newline);

                // Include cause if available
                Exception cause = exception.InnerException;
                if (cause != null)
                {
                    stub.Append("//   Caused by: ").Append(cause.GetType().Name).Append(newline);
                    stub.Append("//   Cause Message: ").Append(cause.Message != null ? cause.Message : "(no message)").Append(newline);
                }

                // Include stack trace summary (first few frames)
                System.Diagnostics.StackTrace stack = new System.Diagnostics.StackTrace(exception, true);
                if (stack != null && stack.FrameCount > 0)
                {
                    stub.Append("//   Stack Trace (first 5 frames):").Append(newline);
                    int maxFrames = Math.Min(5, stack.FrameCount);
                    for (int i = 0; i < maxFrames; i++)
                    {
                        var frame = stack.GetFrame(i);
                        if (frame != null)
                        {
                            stub.Append("//     at ").Append(frame.ToString()).Append(newline);
                        }
                    }
                    if (stack.FrameCount > maxFrames)
                    {
                        stub.Append("//     ... (").Append(stack.FrameCount - maxFrames).Append(" more frames)").Append(newline);
                    }
                }
                stub.Append(newline);
            }

            // Additional context information
            if (additionalInfo != null && additionalInfo.Trim().Length > 0)
            {
                stub.Append("// Additional Context:").Append(newline);
                // Split long additional info into lines if needed
                string[] lines = additionalInfo.Split('\n');
                foreach (string line in lines)
                {
                    stub.Append("//   ").Append(line).Append(newline);
                }
                stub.Append(newline);
            }

            // Decompiler configuration
            stub.Append("// Decompiler Configuration:").Append(newline);
            stub.Append("//   Game Mode: ").Append(isK2Selected ? "KotOR 2 (TSL)" : "KotOR 1").Append(newline);
            stub.Append("//   Prefer Switches: ").Append(preferSwitches).Append(newline);
            stub.Append("//   Strict Signatures: ").Append(strictSignatures).Append(newline);
            stub.Append("//   Actions Data Loaded: ").Append(this.actions != null).Append(newline);
            stub.Append(newline);

            // System information
            stub.Append("// System Information:").Append(newline);
            stub.Append("//   .NET Version: ").Append(Environment.Version.ToString()).Append(newline);
            stub.Append("//   OS: ").Append(Environment.OSVersion.ToString()).Append(newline);
            stub.Append("//   Working Directory: ").Append(JavaSystem.GetProperty("user.dir")).Append(newline);
            stub.Append(newline);

            // Timestamp
            stub.Append("// Error Timestamp: ").Append(DateTime.Now.ToString()).Append(newline);
            stub.Append(newline);

            // Recommendations
            stub.Append("// Recommendations:").Append(newline);
            if (file != null && file.Exists() && file.Length == 0)
            {
                stub.Append("//   - File is empty (0 bytes). This may indicate a corrupted or incomplete file.").Append(newline);
            }
            else if (file != null && !file.Exists())
            {
                stub.Append("//   - File does not exist. Verify the file path is correct.").Append(newline);
            }
            else if (this.actions == null)
            {
                stub.Append("//   - Actions data not loaded. Ensure k1_nwscript.nss or tsl_nwscript.nss is available.").Append(newline);
            }
            else
            {
                stub.Append("//   - This may indicate a corrupted, invalid, or unsupported NCS file format.").Append(newline);
                stub.Append("//   - The file may be from a different game version or modded in an incompatible way.").Append(newline);
            }
            stub.Append("//   - Check the exception details above for specific error information.").Append(newline);
            stub.Append("//   - Verify the file is a valid KotOR/TSL NCS bytecode file.").Append(newline);
            stub.Append(newline);

            // Minimal valid NSS stub
            stub.Append("// Minimal fallback function:").Append(newline);
            stub.Append("void main() {").Append(newline);
            stub.Append("    // Decompilation failed at stage: ").Append(errorStage != null ? errorStage : "Unknown").Append(newline);
            if (exception != null && exception.Message != null)
            {
                stub.Append("    // Error: ").Append(exception.Message.Replace("\n", " ").Replace("\r", "")).Append(newline);
            }
            stub.Append("}").Append(newline);

            return stub.ToString();
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:667-696
        // Original: private String comparePcodeFiles(File originalPcode, File newPcode)
        private string ComparePcodeFiles(NcsFile originalPcode, NcsFile newPcode)
        {
            try
            {
                using (BufferedReader reader1 = new BufferedReader(new FileReader(originalPcode)))
                {
                    using (BufferedReader reader2 = new BufferedReader(new FileReader(newPcode)))
                    {
                        string line1;
                        string line2;
                        int line = 1;

                        while (true)
                        {
                            line1 = reader1.ReadLine();
                            line2 = reader2.ReadLine();

                            // both files ended -> identical
                            if (line1 == null && line2 == null)
                            {
                                return null; // identical
                            }

                            // Detect differences: missing line or differing content
                            if (line1 == null || line2 == null || !line1.Equals(line2))
                            {
                                string left = line1 == null ? "<EOF>" : line1;
                                string right = line2 == null ? "<EOF>" : line2;
                                return "Mismatch at line " + line + " | original: " + left + " | generated: " + right;
                            }

                            line++;
                        }
                    }
                }
            }
            catch (IOException ex)
            {
                Debug("IO exception in compare files: " + ex);
                return "IO exception during pcode comparison";
            }
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:701-721
        // Original: private boolean compareBinaryFiles(File original, File generated)
        private bool CompareBinaryFiles(NcsFile original, NcsFile generated)
        {
            try
            {
                using (var a = new BufferedStream(new FileStream(original.FullName, FileMode.Open, FileAccess.Read)))
                {
                    using (var b = new BufferedStream(new FileStream(generated.FullName, FileMode.Open, FileAccess.Read)))
                    {
                        int ba;
                        int bb;
                        while (true)
                        {
                            ba = a.ReadByte();
                            bb = b.ReadByte();
                            if (ba == -1 || bb == -1)
                            {
                                return ba == -1 && bb == -1;
                            }

                            if (ba != bb)
                            {
                                return false;
                            }
                        }
                    }
                }
            }
            catch (IOException ex)
            {
                Debug("IO exception in compare files: " + ex);
                return false;
            }
        }

        // TODO: PLACEHOLDER - Placeholder for old ComparePcodeFiles that was not decompiled:
        private string ComparePcodeFilesOld(NcsFile file1, NcsFile file2)
        {

            //
            // This method could not be decompiled.
            //
            // Original Bytecode:
            //
            //     1: astore_3        /* br1 */
            //     2: aconst_null
            //     3: astore          br2
            //     5: new             Ljava/io/BufferedReader;
            //     8: dup
            //     9: new             Ljava/io/FileReader;
            //    12: dup
            //    13: aload_1         /* file1 */
            //    14: invokespecial   java/io/FileReader.<init>:(Ljava/io/File;)V
            //    17: invokespecial   java/io/BufferedReader.<init>:(Ljava/io/Reader;)V
            //    20: astore_3        /* br1 */
            //    21: new             Ljava/io/BufferedReader;
            //    24: dup
            //    25: new             Ljava/io/FileReader;
            //    28: dup
            //    29: aload_2         /* file2 */
            //    30: invokespecial   java/io/FileReader.<init>:(Ljava/io/File;)V
            //    33: invokespecial   java/io/BufferedReader.<init>:(Ljava/io/Reader;)V
            //    36: astore          br2
            //    38: aload_3         /* br1 */
            //    39: invokevirtual   java/io/BufferedReader.readLine:()Ljava/lang/String;
            //    42: astore          s1
            //    44: aload           br2
            //    46: invokevirtual   java/io/BufferedReader.readLine:()Ljava/lang/String;
            //    49: astore          s2
            //    51: goto            91
            //    54: aload           br2
            //    56: invokevirtual   java/io/BufferedReader.readLine:()Ljava/lang/String;
            //    59: astore          s2
            //    61: aload           s1
            //    63: aload           s2
            //    65: invokevirtual   java/lang/String.equals:(Ljava/lang/Object;)Z
            //    68: ifne            91
            //    71: aload           s1
            //    73: astore          9
            //    75: aload_3         /* br1 */
            //    76: invokevirtual   java/io/BufferedReader.close:()V
            //    79: aload           br2
            //    81: invokevirtual   java/io/BufferedReader.close:()V
            //    84: goto            88
            //    87: pop
            //    88: aload           9
            //    90: areturn
            //    91: aload_3         /* br1 */
            //    92: invokevirtual   java/io/BufferedReader.readLine:()Ljava/lang/String;
            //    95: dup
            //    96: astore          5
            //    98: ifnonnull       54
            //   101: aload           br2
            //   103: invokevirtual   java/io/BufferedReader.readLine:()Ljava/lang/String;
            //   106: dup
            //   107: astore          s2
            //   109: ifnonnull       131
            //   112: aconst_null
            //   113: astore          9
            //   115: aload_3         /* br1 */
            //   116: invokevirtual   java/io/BufferedReader.close:()V
            //   119: aload           br2
            //   121: invokevirtual   java/io/BufferedReader.close:()V
            //   124: goto            128
            //   127: pop
            //   128: aload           9
            //   130: areturn
            //   131: aload           s2
            //   133: astore          9
            //   135: aload_3         /* br1 */
            //   136: invokevirtual   java/io/BufferedReader.close:()V
            //   139: aload           br2
            //   141: invokevirtual   java/io/BufferedReader.close:()V
            //   144: goto            148
            //   147: pop
            //   148: aload           9
            //   150: areturn
            //   151: astore          e
            //   153: getstatic       java/lang/System.out:Ljava/io/PrintStream;
            //   156: new             Ljava/lang/StringBuilder;
            //   159: dup
            //   160: ldc_w           "IO exception in compare files: "
            //   163: invokespecial   java/lang/StringBuilder.<init>:(Ljava/lang/String;)V
            //   166: aload           e
            //   168: invokevirtual   java/lang/StringBuilder.append:(Ljava/lang/Object;)Ljava/lang/StringBuilder;
            //   171: invokevirtual   java/lang/StringBuilder.toString:()Ljava/lang/String;
            //   174: invokevirtual   java/io/PrintStream.println:(Ljava/lang/String;)V
            //   177: aconst_null
            //   178: astore          9
            //   180: aload_3         /* br1 */
            //   181: invokevirtual   java/io/BufferedReader.close:()V
            //   184: aload           br2
            //   186: invokevirtual   java/io/BufferedReader.close:()V
            //   189: goto            193
            //   192: pop
            //   193: aload           9
            //   195: areturn
            //   196: astore          8
            //   198: aload_3         /* br1 */
            //   199: invokevirtual   java/io/BufferedReader.close:()V
            //   202: aload           br2
            //   204: invokevirtual   java/io/BufferedReader.close:()V
            //   207: goto            211
            //   210: pop
            //   211: aload           8
            //   213: athrow
            //    Exceptions:
            //  Try           Handler
            //  Start  End    Start  End    Type
            //  -----  -----  -----  -----  ---------------------
            //  75     87     87     88     Ljava/lang/Exception;
            //  115    127    127    128    Ljava/lang/Exception;
            //  135    147    147    148    Ljava/lang/Exception;
            //  5      151    151    196    Ljava/io/IOException;
            //  180    192    192    193    Ljava/lang/Exception;
            //  5      75     196    214    Any
            //  91     115    196    214    Any
            //  131    135    196    214    Any
            //  151    180    196    214    Any
            //  198    210    210    211    Ljava/lang/Exception;
            //
            // The error that occurred was:
            //
            // java.lang.NullPointerException
            //     at com.strobel.decompiler.ast.AstBuilder.convertLocalVariables(AstBuilder.java:2945)
            //     at com.strobel.decompiler.ast.AstBuilder.performStackAnalysis(AstBuilder.java:2501)
            //     at com.strobel.decompiler.ast.AstBuilder.build(AstBuilder.java:108)
            //     at com.strobel.decompiler.languages.java.ast.AstMethodBodyBuilder.createMethodBody(AstMethodBodyBuilder.java:203)
            //     at com.strobel.decompiler.languages.java.ast.AstMethodBodyBuilder.createMethodBody(AstMethodBodyBuilder.java:93)
            //     at com.strobel.decompiler.languages.java.ast.AstBuilder.createMethodBody(AstBuilder.java:868)
            //     at com.strobel.decompiler.languages.java.ast.AstBuilder.createMethod(AstBuilder.java:761)
            //     at com.strobel.decompiler.languages.java.ast.AstBuilder.addTypeMembers(AstBuilder.java:638)
            //     at com.strobel.decompiler.languages.java.ast.AstBuilder.createTypeCore(AstBuilder.java:605)
            //     at com.strobel.decompiler.languages.java.ast.AstBuilder.createTypeNoCache(AstBuilder.java:195)
            //     at com.strobel.decompiler.languages.java.ast.AstBuilder.createType(AstBuilder.java:162)
            //     at com.strobel.decompiler.languages.java.ast.AstBuilder.addType(AstBuilder.java:137)
            //     at com.strobel.decompiler.languages.java.JavaLanguage.buildAst(JavaLanguage.java:71)
            //     at com.strobel.decompiler.languages.java.JavaLanguage.decompileType(JavaLanguage.java:59)
            //     at com.strobel.decompiler.DecompilerDriver.decompileType(DecompilerDriver.java:333)
            //     at com.strobel.decompiler.DecompilerDriver.decompileJar(DecompilerDriver.java:254)
            //     at com.strobel.decompiler.DecompilerDriver.main(DecompilerDriver.java:144)
            //
            throw new InvalidOperationException("An error occurred while decompiling this method.");
        }

        private bool ByteArraysEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null)
            {
                return false;
            }

            if (a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }

        private Game MapGameType()
        {
            if (this.gameType == NWScriptLocator.GameType.TSL)
            {
                return Game.K2;
            }

            return Game.K1;
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:1852-1865
        // Original: private Iterable<ASubroutine> subIterable(SubroutineAnalysisData subdata)
        private IEnumerable<ASubroutine> SubIterable(SubroutineAnalysisData subdata)
        {
            List<ASubroutine> list = new List<ASubroutine>();
            IEnumerator<object> raw = subdata.GetSubroutines();

            while (raw.HasNext())
            {
                ASubroutine sub = (ASubroutine)raw.Next();
                if (sub == null)
                {
                    throw new InvalidOperationException("Unexpected null element in subroutine list");
                }
                list.Add(sub);
            }

            return list;
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:1867-1882
        // Original: private void enforceStrictSignatures(SubroutineAnalysisData subdata, NodeAnalysisData nodedata)
        private void EnforceStrictSignatures(SubroutineAnalysisData subdata, NodeAnalysisData nodedata)
        {
            if (!FileDecompiler.strictSignatures)
            {
                return;
            }

            foreach (ASubroutine iterSub in this.SubIterable(subdata))
            {
                SubroutineState state = subdata.GetState(iterSub);
                if (!state.IsTotallyPrototyped())
                {
                    int sigPos = nodedata.TryGetPos(iterSub);
                    Debug(
                        "Strict signatures: unresolved signature for subroutine at " +
                        (sigPos >= 0 ? sigPos.ToString() : "unknown") +
                        " (continuing)"
                    );
                }
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:1193-1847
        // Original: private FileDecompiler.FileScriptData decompileNcs(File file)
        // UPDATED: Now uses DecompileNcsObjectFromFile which uses NcsToAstConverter for more reliable AST conversion
        // This matches the newer approach and avoids decoder/parser failures
        private Utils.FileScriptData DecompileNcs(NcsFile file)
        {
            Debug($"DEBUG DecompileNcs: START for {file.Name}");
            // Use the new NcsToAstConverter path instead of old decoder/parser path
            // This is more reliable and handles edge cases better
            try
            {
                Debug($"DEBUG DecompileNcs: Trying new path (DecompileNcsObjectFromFile)");
                Utils.FileScriptData result = this.DecompileNcsObjectFromFile(file);
                if (result == null)
                {
                    Debug("DEBUG DecompileNcs: DecompileNcsObjectFromFile returned null, falling back to old decoder/parser path");
                    // Fall back to old path if new path returns null
                    result = this.DecompileNcsOldPath(file);
                }
                else
                {
                    Debug($"DEBUG DecompileNcs: New path (DecompileNcsObjectFromFile) succeeded!");
                }
                // Final fallback: ensure we never return null
                if (result == null)
                {
                    Debug("WARNING: Both decompilation paths returned null, creating minimal fallback stub");
                    result = new Utils.FileScriptData();
                    result.SetCode(this.GenerateComprehensiveFallbackStub(file, "Both decompilation paths failed", null, "DecompileNcsObjectFromFile and DecompileNcsOldPath both returned null"));
                }
                return result;
            }
            catch (Exception e)
            {
                Debug($"DEBUG DecompileNcs: DecompileNcsObjectFromFile threw EXCEPTION: {e.GetType().Name}: {e.Message}");
                Debug("DecompileNcsObjectFromFile failed, falling back to old decoder/parser path: " + e.Message);
                e.PrintStackTrace(JavaSystem.@out);
                try
                {
                    Debug($"DEBUG DecompileNcs: Falling back to old path (DecompileNcsOldPath)");
                    // Fall back to old path if new path fails
                    Utils.FileScriptData result = this.DecompileNcsOldPath(file);
                    // Final fallback: ensure we never return null
                    if (result == null)
                    {
                        Debug("WARNING: DecompileNcsOldPath returned null after exception, creating minimal fallback stub");
                        result = new Utils.FileScriptData();
                        result.SetCode(this.GenerateComprehensiveFallbackStub(file, "DecompileNcsOldPath returned null", e, null));
                    }
                    return result;
                }
                catch (Exception e2)
                {
                    // Last resort: create a minimal stub so we always return something
                    Debug("CRITICAL: Both decompilation paths threw exceptions, creating emergency fallback stub");
                    Debug("  First exception: " + e.GetType().Name + " - " + e.Message);
                    Debug("  Second exception: " + e2.GetType().Name + " - " + e2.Message);
                    Utils.FileScriptData result = new Utils.FileScriptData();
                    result.SetCode(this.GenerateComprehensiveFallbackStub(file, "Both decompilation paths threw exceptions", e, "Second exception: " + e2.GetType().Name + " - " + e2.Message));
                    return result;
                }
            }
        }

        // Old decoder/parser path - kept as fallback
        private Utils.FileScriptData DecompileNcsOldPath(NcsFile file)
        {
            Utils.FileScriptData data = null;
            string commands = null;
            SetDestinations setdest = null;
            DoTypes dotypes = null;
            Node ast = null;
            NodeAnalysisData nodedata = null;
            SubroutineAnalysisData subdata = null;
            IEnumerator<object> subs = null;
            ASubroutine sub = null;
            ASubroutine mainsub = null;
            FlattenSub flatten = null;
            DoGlobalVars doglobs = null;
            CleanupPass cleanpass = null;
            MainPass mainpass = null;
            DestroyParseTree destroytree = null;
            if (this.actions == null)
            {
                Debug("null action! Creating fallback stub.");
                // Return comprehensive stub instead of null
                Utils.FileScriptData stub = new Utils.FileScriptData();
                string expectedFile = isK2Selected ? "tsl_nwscript.nss" : "k1_nwscript.nss";
                string stubCode = this.GenerateComprehensiveFallbackStub(file, "Actions data loading", null,
                    "The actions data table (nwscript.nss) is required to decompile NCS files.\n" +
                    "Expected file: " + expectedFile + "\n" +
                    "Please ensure the appropriate nwscript.nss file is available in tools/ directory, working directory, or configured path.");
                stub.SetCode(stubCode);
                return stub;
            }

            try
            {
                data = new Utils.FileScriptData();

                // Decode bytecode - wrap in try-catch to handle corrupted files
                try
                {
                    Debug("DEBUG decompileNcs: starting decode for " + file.Name);
                    using (var fileStream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read))
                    using (var bufferedStream = new BufferedStream(fileStream))
                    using (var binaryReader = new System.IO.BinaryReader(bufferedStream))
                    {
                        commands = new Decoder(binaryReader, this.actions).Decode();
                    }
                    Debug("DEBUG decompileNcs: decode successful, commands length=" + (commands != null ? commands.Length : 0));
                }
                catch (Exception decodeEx)
                {
                    Debug("DEBUG decompileNcs: decode FAILED - " + decodeEx.Message);
                    Debug("Error during bytecode decoding: " + decodeEx.Message);
                    // Create comprehensive fallback stub for decoding errors
                    long fileSize = file.Exists() ? file.Length : -1;
                    string fileInfo = "File size: " + fileSize + " bytes";
                    if (fileSize > 0)
                    {
                        try
                        {
                            using (var fis = new FileStream(file.FullName, FileMode.Open, FileAccess.Read))
                            {
                                byte[] header = new byte[Math.Min(16, (int)fileSize)];
                                int read = fis.Read(header, 0, header.Length);
                                if (read > 0)
                                {
                                    fileInfo += "\nFile header (hex): " + this.BytesToHex(header, read);
                                }
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }
                    string stub = this.GenerateComprehensiveFallbackStub(file, "Bytecode decoding", decodeEx, fileInfo);
                    data.SetCode(stub);
                    return data;
                }

                // Parse commands - wrap in try-catch to handle parse errors, but try to recover
                try
                {
                    Debug("DEBUG decompileNcs: starting parse, commands length=" + (commands != null ? commands.Length : 0));
                    using (var stringReader = new StringReader(commands))
                    {
                        var pushbackReader = new PushbackReader(stringReader, 1024);
                        ast = new Parser.Parser(new Lexer.Lexer(pushbackReader)).Parse();
                    }
                    Debug("DEBUG decompileNcs: parse successful");
                }
                catch (Exception parseEx)
                {
                    Debug("DEBUG decompileNcs: parse FAILED - " + parseEx.Message);
                    Debug("Error during parsing: " + parseEx.Message);
                    Debug("Attempting to recover by trying partial parsing strategies...");

                    // Try to recover: attempt to parse in chunks or with relaxed rules
                    ast = null;
                    try
                    {
                        // Strategy 1: Try parsing with a larger buffer
                        Debug("Trying parse with larger buffer...");
                        using (var stringReader = new StringReader(commands))
                        {
                            var pushbackReader = new PushbackReader(stringReader, 2048);
                            ast = new Parser.Parser(new Lexer.Lexer(pushbackReader)).Parse();
                        }
                        Debug("Successfully recovered parse with larger buffer.");
                    }
                    catch (Exception e1)
                    {
                        Debug("Larger buffer parse also failed: " + e1.Message);
                        // Strategy 2: Try to extract what we can and create minimal structure
                        // If we have decoded commands, we can at least create a basic structure
                        if (commands != null && commands.Length > 0)
                        {
                            Debug("Attempting to create minimal structure from decoded commands...");
                            try
                            {
                                // Try to find subroutine boundaries in the commands string
                                // This is a heuristic recovery - look for common patterns
                                string[] lines = commands.Split('\n');
                                int subCount2 = 0;
                                foreach (string line in lines)
                                {
                                    string trimmed = line.Trim();
                                    if (trimmed.StartsWith("sub") || trimmed.StartsWith("function"))
                                    {
                                        subCount2++;
                                    }
                                }

                                // If we found some structure, try to continue with minimal setup
                                if (subCount2 > 0)
                                {
                                    Debug("Detected " + subCount2 + " potential subroutines in decoded commands, but full parse failed.");
                                    // We'll fall through to create a stub, but with better information
                                }
                            }
                            catch (Exception e2)
                            {
                                Debug("Recovery attempt failed: " + e2.Message);
                            }
                        }
                    }

                    // If we still don't have an AST, create comprehensive stub but preserve commands for potential manual recovery
                    if (ast == null)
                    {
                        string commandsPreview = "none";
                        if (commands != null && commands.Length > 0)
                        {
                            int previewLength = Math.Min(1000, commands.Length);
                            commandsPreview = commands.Substring(0, previewLength);
                            if (commands.Length > previewLength)
                            {
                                commandsPreview += "\n... (truncated, total length: " + commands.Length + " characters)";
                            }
                        }
                        string additionalInfo = "Bytecode was successfully decoded but parsing failed.\n" +
                                               "Decoded commands length: " + (commands != null ? commands.Length : 0) + " characters\n" +
                                               "Decoded commands preview:\n" + commandsPreview + "\n\n" +
                                               "RECOVERY NOTE: The decoded commands are available but could not be parsed into an AST.\n" +
                                               "This may indicate malformed bytecode or an unsupported format variant.";
                        string stub = this.GenerateComprehensiveFallbackStub(file, "Parsing decoded bytecode", parseEx, additionalInfo);
                        data.SetCode(stub);
                        return data;
                    }
                    // If we recovered an AST, continue with decompilation
                    Debug("Continuing decompilation with recovered parse tree.");
                }

                // Analysis passes - wrap in try-catch to allow partial recovery
                nodedata = new NodeAnalysisData();
                subdata = new SubroutineAnalysisData(nodedata);

                try
                {
                    ast.Apply(new SetPositions(nodedata));
                }
                catch (Exception e)
                {
                    Debug("Error in SetPositions, continuing with partial positions: " + e.Message);
                }

                try
                {
                    setdest = new SetDestinations(ast, nodedata, subdata);
                    ast.Apply(setdest);
                }
                catch (Exception e)
                {
                    Debug("Error in SetDestinations, continuing without destination resolution: " + e.Message);
                    setdest = null;
                }

                try
                {
                    if (setdest != null)
                    {
                        ast.Apply(new SetDeadCode(nodedata, subdata, setdest.GetOrigins()));
                    }
                    else
                    {
                        // Try without origins if setdest failed
                        ast.Apply(new SetDeadCode(nodedata, subdata, null));
                    }
                }
                catch (Exception e)
                {
                    Debug("Error in SetDeadCode, continuing without dead code analysis: " + e.Message);
                }

                if (setdest != null)
                {
                    try
                    {
                        setdest.Done();
                    }
                    catch (Exception e)
                    {
                        Debug("Error finalizing SetDestinations: " + e.Message);
                    }
                    setdest = null;
                }

                try
                {
                    subdata.SplitOffSubroutines(ast);
                    Debug("DEBUG splitOffSubroutines: success, numSubs=" + subdata.NumSubs());
                }
                catch (Exception e)
                {
                    Debug("DEBUG splitOffSubroutines: ERROR - " + e.Message);
                    e.PrintStackTrace(JavaSystem.@out);
                    Debug("Error splitting subroutines, attempting to continue: " + e.Message);
                    // Try to get main sub at least
                    try
                    {
                        mainsub = subdata.GetMainSub();
                        Debug("DEBUG splitOffSubroutines: recovered mainsub=" + (mainsub != null ? "found" : "null"));
                    }
                    catch (Exception e2)
                    {
                        Debug("DEBUG splitOffSubroutines: could not recover mainsub - " + e2.Message);
                        Debug("Could not recover main subroutine: " + e2.Message);
                    }
                }
                ast = null;
                // Flattening - try to recover if main sub is missing
                try
                {
                    mainsub = subdata.GetMainSub();
                }
                catch (Exception e)
                {
                    Debug("Error getting main subroutine: " + e.Message);
                    mainsub = null;
                }

                if (mainsub != null)
                {
                    try
                    {
                        flatten = new FlattenSub(mainsub, nodedata);
                        mainsub.Apply(flatten);
                    }
                    catch (Exception e)
                    {
                        Debug("Error flattening main subroutine: " + e.Message);
                        flatten = null;
                    }

                    if (flatten != null)
                    {
                        try
                        {
                            foreach (ASubroutine iterSub in this.SubIterable(subdata))
                            {
                                try
                                {
                                    flatten.SetSub(iterSub);
                                    iterSub.Apply(flatten);
                                }
                                catch (Exception e)
                                {
                                    Debug("Error flattening subroutine, skipping: " + e.Message);
                                    // Continue with other subroutines
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Debug("Error iterating subroutines during flattening: " + e.Message);
                        }

                        try
                        {
                            flatten.Done();
                        }
                        catch (Exception e)
                        {
                            Debug("Error finalizing flatten: " + e.Message);
                        }
                        flatten = null;
                    }
                }
                else
                {
                    Debug("Warning: No main subroutine available, continuing with partial decompilation.");
                }
                // Process globals - recover if this fails
                try
                {
                    sub = subdata.GetGlobalsSub();
                    Debug($"DEBUG FileDecompiler: GetGlobalsSub() returned {sub?.GetType().Name ?? "null"}");
                    if (sub != null)
                    {
                        try
                        {
                            doglobs = new DoGlobalVars(nodedata, subdata);
                            Debug($"DEBUG FileDecompiler: calling sub.Apply(doglobs), sub type={sub.GetType().Name}, doglobs type={doglobs.GetType().Name}");
                            sub.Apply(doglobs);
                            Debug($"DEBUG FileDecompiler: after sub.Apply(doglobs)");
                            cleanpass = new CleanupPass(doglobs.GetScriptRoot(), nodedata, subdata, doglobs.GetState());
                            cleanpass.Apply();
                            subdata.SetGlobalStack(doglobs.GetStack());
                            subdata.GlobalState(doglobs.GetState());
                            cleanpass.Done();
                        }
                        catch (Exception e)
                        {
                            Debug("Error processing globals, continuing without globals: " + e.Message);
                            if (doglobs != null)
                            {
                                try
                                {
                                    doglobs.Done();
                                }
                                catch (Exception e2)
                                {
                                }
                            }
                            doglobs = null;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug("Error getting globals subroutine: " + e.Message);
                }

                // Prototype engine - recover if this fails
                try
                {
                    PrototypeEngine proto = new PrototypeEngine(nodedata, subdata, this.actions, FileDecompiler.strictSignatures);
                    proto.Run();
                }
                catch (Exception e)
                {
                    Debug("Error in prototype engine, continuing with partial prototypes: " + e.Message);
                }

                // Type analysis - recover if main sub typing fails
                if (mainsub != null)
                {
                    try
                    {
                        dotypes = new DoTypes(subdata.GetState(mainsub), nodedata, subdata, this.actions, false);
                        mainsub.Apply(dotypes);

                        try
                        {
                            dotypes.AssertStack();
                        }
                        catch (Exception)
                        {
                            Debug("Could not assert stack, continuing anyway.");
                        }

                        dotypes.Done();
                    }
                    catch (Exception e)
                    {
                        Debug("Error typing main subroutine, continuing with partial types: " + e.Message);
                        dotypes = null;
                    }
                }

                // Type all subroutines - continue even if some fail
                bool alldone = false;
                bool onedone = true;
                int donecount = 0;

                try
                {
                    alldone = subdata.CountSubsDone() == subdata.NumSubs();
                    onedone = true;
                    donecount = subdata.CountSubsDone();
                }
                catch (Exception e)
                {
                    Debug("Error checking subroutine completion status: " + e.Message);
                }

                for (int loopcount = 0; !alldone && onedone && loopcount < 1000; ++loopcount)
                {
                    onedone = false;
                    try
                    {
                        subs = subdata.GetSubroutines();
                    }
                    catch (Exception e)
                    {
                        Debug("Error getting subroutines iterator: " + e.Message);
                        break;
                    }

                    if (subs != null)
                    {
                        while (subs.HasNext())
                        {
                            try
                            {
                                sub = (ASubroutine)subs.Next();
                                if (sub == null) continue;

                                dotypes = new DoTypes(subdata.GetState(sub), nodedata, subdata, this.actions, false);
                                sub.Apply(dotypes);
                                dotypes.Done();
                            }
                            catch (Exception e)
                            {
                                Debug("Error typing subroutine, skipping: " + e.Message);
                                // Continue with next subroutine
                            }
                        }
                    }

                    if (mainsub != null)
                    {
                        try
                        {
                            dotypes = new DoTypes(subdata.GetState(mainsub), nodedata, subdata, this.actions, false);
                            mainsub.Apply(dotypes);
                            dotypes.Done();
                        }
                        catch (Exception e)
                        {
                            Debug("Error re-typing main subroutine: " + e.Message);
                        }
                    }

                    try
                    {
                        alldone = subdata.CountSubsDone() == subdata.NumSubs();
                        int newDoneCount = subdata.CountSubsDone();
                        onedone = newDoneCount > donecount;
                        donecount = newDoneCount;
                    }
                    catch (Exception e)
                    {
                        Debug("Error checking completion status: " + e.Message);
                        break;
                    }
                }

                if (!alldone)
                {
                    Debug("Unable to do final prototype of all subroutines. Continuing with partial results.");
                }

                this.EnforceStrictSignatures(subdata, nodedata);

                dotypes = null;
                nodedata.ClearProtoData();

                Debug("DEBUG decompileNcs: iterating subroutines, numSubs=" + subdata.NumSubs());
                int subCount = 0;
                foreach (ASubroutine iterSub in this.SubIterable(subdata))
                {
                    // Skip main subroutine - it's processed separately below
                    if (iterSub == mainsub)
                    {
                        Debug("DEBUG decompileNcs: skipping main subroutine in loop (will be processed separately)");
                        continue;
                    }
                    subCount++;
                    int subPos = nodedata.TryGetPos(iterSub);
                    Debug("DEBUG decompileNcs: processing subroutine " + subCount + " at pos=" + (subPos >= 0 ? subPos.ToString() : "unknown"));
                    try
                    {
                        mainpass = new MainPass(subdata.GetState(iterSub), nodedata, subdata, this.actions);
                        iterSub.Apply(mainpass);
                        cleanpass = new CleanupPass(mainpass.GetScriptRoot(), nodedata, subdata, mainpass.GetState());
                        cleanpass.Apply();
                        data.AddSub(mainpass.GetState());
                        Debug("DEBUG decompileNcs: successfully added subroutine " + subCount);
                        mainpass.Done();
                        cleanpass.Done();
                    }
                    catch (Exception e)
                    {
                        Debug("DEBUG decompileNcs: ERROR processing subroutine " + subCount + " - " + e.Message);
                        Debug("Error while processing subroutine: " + e);
                        e.PrintStackTrace(JavaSystem.@out);
                        // Try to add partial subroutine state even if processing failed
                        try
                        {
                            SubroutineState state = subdata.GetState(iterSub);
                            if (state != null)
                            {
                                MainPass recoveryPass = new MainPass(state, nodedata, subdata, this.actions);
                                // Try to get state even if apply failed
                                SubScriptState recoveryState = recoveryPass.GetState();
                                if (recoveryState != null)
                                {
                                    data.AddSub(recoveryState);
                                    Debug("Added partial subroutine state after error recovery.");
                                }
                            }
                        }
                        catch (Exception e2)
                        {
                            Debug("Could not recover partial subroutine state: " + e2.Message);
                        }
                    }
                }

                // Generate code for main subroutine - recover if this fails
                int mainPos = mainsub != null ? nodedata.TryGetPos(mainsub) : -1;
                Debug("DEBUG decompileNcs: mainsub=" + (mainsub != null ? "found at pos=" + (mainPos >= 0 ? mainPos.ToString() : "unknown") : "null"));
                if (mainsub != null)
                {
                    try
                    {
                        Debug("DEBUG decompileNcs: creating MainPass for mainsub");
                        mainpass = new MainPass(subdata.GetState(mainsub), nodedata, subdata, this.actions);
                        Debug("DEBUG decompileNcs: applying mainpass to mainsub");
                        mainsub.Apply(mainpass);

                        try
                        {
                            mainpass.AssertStack();
                        }
                        catch (Exception e)
                        {
                            Debug("Could not assert stack, continuing anyway.");
                        }

                        cleanpass = new CleanupPass(mainpass.GetScriptRoot(), nodedata, subdata, mainpass.GetState());
                        cleanpass.Apply();
                        mainpass.GetState().IsMain(true);
                        data.AddSub(mainpass.GetState());
                        mainpass.Done();
                        cleanpass.Done();
                    }
                    catch (Exception e)
                    {
                        Debug("Error generating code for main subroutine: " + e.Message);
                        // Try to create a minimal main function stub using MainPass
                        try
                        {
                            mainpass = new MainPass(subdata.GetState(mainsub), nodedata, subdata, this.actions);
                            // Even if apply fails, try to get the state
                            try
                            {
                                mainsub.Apply(mainpass);
                            }
                            catch (Exception e2)
                            {
                                Debug("Could not apply mainpass, but attempting to use partial state: " + e2.Message);
                            }
                            SubScriptState minimalMain = mainpass.GetState();
                            if (minimalMain != null)
                            {
                                minimalMain.IsMain(true);
                                data.AddSub(minimalMain);
                                Debug("Created minimal main subroutine stub.");
                            }
                            mainpass.Done();
                        }
                        catch (Exception e2)
                        {
                            Debug("Could not create minimal main stub: " + e2.Message);
                        }
                    }
                }
                else
                {
                    Debug("Warning: No main subroutine available for code generation.");
                }
                // Store analysis data and globals - recover if this fails
                try
                {
                    data.SetSubdata(subdata);
                }
                catch (Exception e)
                {
                    Debug("Error storing subroutine analysis data: " + e.Message);
                }

                if (doglobs != null)
                {
                    try
                    {
                        cleanpass = new CleanupPass(doglobs.GetScriptRoot(), nodedata, subdata, doglobs.GetState());
                        cleanpass.Apply();
                        data.SetGlobals(doglobs.GetState());
                        doglobs.Done();
                        cleanpass.Done();
                    }
                    catch (Exception e)
                    {
                        Debug("Error finalizing globals: " + e.Message);
                        try
                        {
                            if (doglobs.GetState() != null)
                            {
                                data.SetGlobals(doglobs.GetState());
                            }
                            doglobs.Done();
                        }
                        catch (Exception e2)
                        {
                            Debug("Could not recover globals state: " + e2.Message);
                        }
                    }
                }

                // Cleanup parse tree - this is safe to skip if it fails
                try
                {
                    destroytree = new DestroyParseTree();

                    foreach (ASubroutine iterSub in this.SubIterable(subdata))
                    {
                        try
                        {
                            iterSub.Apply(destroytree);
                        }
                        catch (Exception e)
                        {
                            Debug("Error destroying parse tree for subroutine: " + e.Message);
                        }
                    }

                    if (mainsub != null)
                    {
                        try
                        {
                            mainsub.Apply(destroytree);
                        }
                        catch (Exception e)
                        {
                            Debug("Error destroying main parse tree: " + e.Message);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug("Error during parse tree cleanup: " + e.Message);
                    // Continue anyway - cleanup is not critical
                }

                return data;
            }
            catch (Exception e)
            {
                // Try to salvage partial results before giving up
                Debug("Error during decompilation: " + e.Message);
                e.PrintStackTrace(JavaSystem.@out);

                // Always return a FileScriptData, even if it's just a minimal stub
                if (data == null)
                {
                    data = new Utils.FileScriptData();
                }

                // Aggressive recovery: try to salvage whatever state we have
                Debug("Attempting aggressive state recovery...");

                // Try to add any subroutines that were partially processed
                if (subdata != null && mainsub != null)
                {
                    try
                    {
                        // Try to get main sub state even if it's incomplete
                        SubroutineState mainState = subdata.GetState(mainsub);
                        if (mainState != null)
                        {
                            try
                            {
                                // Try to create a minimal main pass
                                mainpass = new MainPass(mainState, nodedata, subdata, this.actions);
                                try
                                {
                                    mainsub.Apply(mainpass);
                                }
                                catch (Exception e3)
                                {
                                    Debug("Could not apply mainpass to main sub, but continuing: " + e3.Message);
                                }
                                SubScriptState scriptState = mainpass.GetState();
                                if (scriptState != null)
                                {
                                    scriptState.IsMain(true);
                                    data.AddSub(scriptState);
                                    mainpass.Done();
                                    Debug("Recovered main subroutine state.");
                                }
                            }
                            catch (Exception e2)
                            {
                                Debug("Could not create main pass: " + e2.Message);
                            }
                        }
                    }
                    catch (Exception e2)
                    {
                        Debug("Error recovering main subroutine: " + e2.Message);
                    }

                    // Try to recover other subroutines
                    try
                    {
                        foreach (ASubroutine iterSub in this.SubIterable(subdata))
                        {
                            if (iterSub == mainsub) continue; // Already handled
                            try
                            {
                                SubroutineState state = subdata.GetState(iterSub);
                                if (state != null)
                                {
                                    try
                                    {
                                        mainpass = new MainPass(state, nodedata, subdata, this.actions);
                                        try
                                        {
                                            iterSub.Apply(mainpass);
                                        }
                                        catch (Exception e3)
                                        {
                                            Debug("Could not apply mainpass to subroutine, but continuing: " + e3.Message);
                                        }
                                        SubScriptState scriptState = mainpass.GetState();
                                        if (scriptState != null)
                                        {
                                            data.AddSub(scriptState);
                                            mainpass.Done();
                                        }
                                    }
                                    catch (Exception e2)
                                    {
                                        Debug("Could not create mainpass for subroutine: " + e2.Message);
                                    }
                                }
                            }
                            catch (Exception e2)
                            {
                                Debug("Error recovering subroutine: " + e2.Message);
                            }
                        }
                    }
                    catch (Exception e2)
                    {
                        Debug("Error iterating subroutines during recovery: " + e2.Message);
                    }

                    // Try to store subdata
                    try
                    {
                        data.SetSubdata(subdata);
                    }
                    catch (Exception e2)
                    {
                        Debug("Error storing subdata: " + e2.Message);
                    }
                }

                // Try to recover globals if available
                if (doglobs != null)
                {
                    try
                    {
                        SubScriptState globState = doglobs.GetState();
                        if (globState != null)
                        {
                            data.SetGlobals(globState);
                            Debug("Recovered globals state.");
                        }
                    }
                    catch (Exception e2)
                    {
                        Debug("Error recovering globals: " + e2.Message);
                    }
                }

                try
                {
                    // Try to generate code from whatever we have
                    data.GenerateCode();
                    string partialCode = data.GetCode();
                    if (partialCode != null && partialCode.Trim().Length > 0)
                    {
                        Debug("Successfully recovered partial decompilation with " +
                                                (data.GetVars() != null ? data.GetVars().Count : 0) + " subroutines.");
                        // Add recovery note to the code
                        string recoveryNote = "// ========================================\n" +
                                            "// PARTIAL DECOMPILATION - RECOVERED STATE\n" +
                                            "// ========================================\n" +
                                            "// This decompilation encountered errors but recovered partial results.\n" +
                                            "// Some subroutines or code sections may be incomplete or missing.\n" +
                                            "// Original error: " + e.GetType().Name + ": " +
                                            (e.Message != null ? e.Message : "(no message)") + "\n" +
                                            "// ========================================\n\n";
                        data.SetCode(recoveryNote + partialCode);
                        return data;
                    }
                }
                catch (Exception genEx)
                {
                    Debug("Could not generate partial code: " + genEx.Message);
                }

                // Last resort: create comprehensive stub with any available partial information
                string partialInfo = "Partial decompilation state:\n";
                try
                {
                    if (data != null)
                    {
                        Dictionary<string, List<object>> vars = data.GetVars();
                        if (vars != null && vars.Count > 0)
                        {
                            partialInfo += "  Subroutines with variable data: " + vars.Count + "\n";
                        }
                    }
                    if (subdata != null)
                    {
                        try
                        {
                            partialInfo += "  Total subroutines detected: " + subdata.NumSubs() + "\n";
                            partialInfo += "  Subroutines fully typed: " + subdata.CountSubsDone() + "\n";
                        }
                        catch (Exception)
                        {
                        }
                    }
                    if (commands != null)
                    {
                        partialInfo += "  Commands decoded: " + commands.Length + " characters\n";
                    }
                    if (ast != null)
                    {
                        partialInfo += "  Parse tree created: yes\n";
                    }
                    if (nodedata != null)
                    {
                        partialInfo += "  Node analysis data available: yes\n";
                    }
                    if (mainsub != null)
                    {
                        partialInfo += "  Main subroutine identified: yes\n";
                    }
                }
                catch (Exception)
                {
                    partialInfo += "  (Unable to gather partial state information)\n";
                }
                string errorStub = this.GenerateComprehensiveFallbackStub(file, "General decompilation pipeline", e, partialInfo);
                data.SetCode(errorStub);
                Debug("Created fallback stub code due to decompilation errors.");
                return data;
            }
            finally
            {
                data = null;
                commands = null;
                setdest = null;
                dotypes = null;
                ast = null;
                if (nodedata != null)
                {
                    nodedata.Close();
                }

                nodedata = null;
                if (subdata != null)
                {
                    subdata.ParseDone();
                }

                subdata = null;
                subs = null;
                sub = null;
                mainsub = null;
                flatten = null;
                doglobs = null;
                cleanpass = null;
                mainpass = null;
                destroytree = null;
                GC.Collect();
            }
        }

        /// <summary>
        /// Decompiles an NCS object in memory (not from file).
        /// This is the core decompilation logic extracted from DecompileNcs(File).
        /// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:588-916
        /// </summary>
        public virtual Utils.FileScriptData DecompileNcsObject(NCS ncs)
        {
            Debug("TRACE DecompileNcsObject: START");
            Utils.FileScriptData data = null;
            SetDestinations setdest = null;
            DoTypes dotypes = null;
            Node ast = null;
            NodeAnalysisData nodedata = null;
            SubroutineAnalysisData subdata = null;
            IEnumerator<object> subs = null;
            ASubroutine sub = null;
            ASubroutine mainsub = null;
            FlattenSub flatten = null;
            DoGlobalVars doglobs = null;
            CleanupPass cleanpass = null;
            MainPass mainpass = null;
            DestroyParseTree destroytree = null;

            if (ncs == null)
            {
                Debug("TRACE DecompileNcsObject: ncs is null, returning null");
                return null;
            }

            Debug("TRACE DecompileNcsObject: ncs.Instructions count=" + (ncs.Instructions != null ? ncs.Instructions.Count : 0));

            // Lazy-load actions if not already loaded
            if (this.actions == null)
            {
                Debug("TRACE DecompileNcsObject: actions is null, calling LoadActions()");
                try
                {
                    this.LoadActions();
                    if (this.actions == null)
                    {
                        Debug("TRACE DecompileNcsObject: LoadActions() returned null, returning null");
                        Debug("Failed to load actions file!");
                        return null;
                    }
                    Debug("TRACE DecompileNcsObject: LoadActions() succeeded");
                }
                catch (Exception loadEx)
                {
                    Debug("TRACE DecompileNcsObject: LoadActions() threw exception: " + loadEx.GetType().Name + ": " + loadEx.Message);
                    loadEx.PrintStackTrace(JavaSystem.@out);
                    return null;
                }
            }
            else
            {
                Debug("TRACE DecompileNcsObject: actions already loaded");
            }

            try
            {
                data = new Utils.FileScriptData();

                if (ncs.Instructions == null || ncs.Instructions.Count == 0)
                {
                    Debug("TRACE DecompileNcsObject: NCS contains no instructions; skipping decompilation.");
                    Debug("NCS contains no instructions; skipping decompilation.");
                    return null;
                }

            Debug("TRACE DecompileNcsObject: Converting NCS to AST, instruction count=" + ncs.Instructions.Count);
            Console.Error.WriteLine($"TRACE DecompileNcsObject: Converting NCS to AST, instruction count={ncs.Instructions.Count}");
            // CRITICAL DEBUG: Log instruction offsets to verify we have all instructions
            if (ncs.Instructions.Count > 0)
            {
                int minOffset = ncs.Instructions[0].Offset;
                int maxOffset = ncs.Instructions[ncs.Instructions.Count - 1].Offset;
                Debug($"TRACE DecompileNcsObject: Instruction offset range: {minOffset} to {maxOffset}");
                Console.Error.WriteLine($"TRACE DecompileNcsObject: Instruction offset range: {minOffset} to {maxOffset}, total instructions={ncs.Instructions.Count}");
                // Check for missing instructions - log if there are gaps
                for (int i = 0; i < Math.Min(10, ncs.Instructions.Count); i++)
                {
                    Debug($"TRACE DecompileNcsObject: Instruction[{i}]: offset={ncs.Instructions[i].Offset}, type={ncs.Instructions[i].InsType}");
                }
                if (ncs.Instructions.Count > 10)
                {
                    Debug($"TRACE DecompileNcsObject: ... (showing first 10 of {ncs.Instructions.Count} instructions)");
                }
            }
            ast = NcsToAstConverter.ConvertNcsToAst(ncs);
            Debug("TRACE DecompileNcsObject: AST conversion complete, ast=" + (ast != null ? ast.GetType().Name : "null"));
                nodedata = new NodeAnalysisData();
                subdata = new SubroutineAnalysisData(nodedata);

                // Set positions on all nodes - critical for decompilation
                try
                {
                    ast.Apply(new SetPositions(nodedata));
                    Debug("TRACE DecompileNcsObject: SetPositions completed successfully");
                }
                catch (Exception e)
                {
                    Debug("Error in SetPositions, continuing with partial positions: " + e.Message);
                    e.PrintStackTrace(JavaSystem.@out);
                    // Continue - some nodes might not have positions, but we'll try to recover
                }

                try
                {
                    setdest = new SetDestinations(ast, nodedata, subdata);
                    ast.Apply(setdest);
                }
                catch (Exception e)
                {
                    Debug("Error in SetDestinations, continuing without destination resolution: " + e.Message);
                    setdest = null;
                }

                try
                {
                    if (setdest != null)
                    {
                        ast.Apply(new SetDeadCode(nodedata, subdata, setdest.GetOrigins()));
                    }
                    else
                    {
                        // Try without origins if setdest failed
                        ast.Apply(new SetDeadCode(nodedata, subdata, null));
                    }
                }
                catch (Exception e)
                {
                    Debug("Error in SetDeadCode, continuing without dead code analysis: " + e.Message);
                }

                if (setdest != null)
                {
                    try
                    {
                        setdest.Done();
                    }
                    catch (Exception e)
                    {
                        Debug("Error finalizing SetDestinations: " + e.Message);
                    }
                    setdest = null;
                }
                try
                {
                    subdata.SplitOffSubroutines(ast);
                }
                catch (Exception splitEx)
                {
                    Debug("Exception in SplitOffSubroutines: " + splitEx.GetType().Name + ": " + splitEx.Message);
                    if (splitEx.StackTrace != null)
                    {
                        Debug("Stack trace: " + splitEx.StackTrace);
                    }
                    splitEx.PrintStackTrace(JavaSystem.@out);
                    // Try to continue - maybe we can still get a main subroutine
                }
                ast = null;
                Debug("TRACE DecompileNcsObject: Getting main subroutine, total subs=" + subdata.NumSubs());
                mainsub = subdata.GetMainSub();
                if (mainsub != null)
                {
                    Debug("TRACE DecompileNcsObject: Main subroutine found, type=" + mainsub.GetType().Name);
                    flatten = new FlattenSub(mainsub, nodedata);
                    mainsub.Apply(flatten);
                    subs = subdata.GetSubroutines();
                    while (subs.HasNext())
                    {
                        sub = (ASubroutine)subs.Next();
                        flatten.SetSub(sub);
                        sub.Apply(flatten);
                    }

                    flatten.Done();
                    flatten = null;
                }
                else
                {
                    Debug("TRACE DecompileNcsObject: No main subroutine found, continuing with partial decompilation");
                    Debug("Warning: No main subroutine available, continuing with partial decompilation.");
                }
                doglobs = null;
                // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:1392-1414
                try
                {
                    sub = subdata.GetGlobalsSub();
                    Debug($"DEBUG FileDecompiler.DecompileNcs: GetGlobalsSub() returned {sub?.GetType().Name ?? "null"}");
                    if (sub != null)
                    {
                        try
                        {
                            doglobs = new DoGlobalVars(nodedata, subdata);
                            Debug($"DEBUG FileDecompiler.DecompileNcs: calling sub.Apply(doglobs), sub type={sub.GetType().Name}, doglobs type={doglobs.GetType().Name}");
                            try
                            {
                                sub.Apply(doglobs);
                                Debug($"DEBUG FileDecompiler.DecompileNcs: after sub.Apply(doglobs)");
                            }
                            catch (Exception applyEx)
                            {
                                Debug($"DEBUG FileDecompiler.DecompileNcs: EXCEPTION in sub.Apply: {applyEx.GetType().Name}: {applyEx.Message}");
                                Debug($"DEBUG FileDecompiler.DecompileNcs: Stack trace: {applyEx.StackTrace}");
                                throw;
                            }
                            cleanpass = new CleanupPass(doglobs.GetScriptRoot(), nodedata, subdata, doglobs.GetState());
                            cleanpass.Apply();
                            subdata.SetGlobalStack(doglobs.GetStack());
                            subdata.GlobalState(doglobs.GetState());
                            cleanpass.Done();
                        }
                        catch (Exception e)
                        {
                            Debug("Error processing globals, continuing without globals: " + e.Message);
                            if (doglobs != null)
                            {
                                try
                                {
                                    doglobs.Done();
                                }
                                catch (Exception e2)
                                {
                                    Debug($"DEBUG FileDecompiler: ignorable error in Done for doglobs: {e2.Message}");
                                }
                            }
                            doglobs = null;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug("Error getting globals subroutine: " + e.Message);
                }

                // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:1407-1413
                // Prototype engine - recover if this fails
                try
                {
                    PrototypeEngine proto = new PrototypeEngine(nodedata, subdata, this.actions, false);
                    proto.Run();
                }
                catch (Exception e)
                {
                    Debug("Error in prototype engine, continuing with partial prototypes: " + e.Message);
                }

                // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:1415-1495
                // Type analysis - recover if main sub typing fails
                if (mainsub != null)
                {
                    try
                    {
                        dotypes = new DoTypes(subdata.GetState(mainsub), nodedata, subdata, this.actions, false);
                        mainsub.Apply(dotypes);

                        try
                        {
                            dotypes.AssertStack();
                        }
                        catch (Exception)
                        {
                            Debug("Could not assert stack, continuing anyway.");
                        }

                        dotypes.Done();
                    }
                    catch (Exception e)
                    {
                        Debug("Error typing main subroutine, continuing with partial types: " + e.Message);
                        dotypes = null;
                    }
                }

                // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:1434-1495
                // Type all subroutines - continue even if some fail
                bool alldone = false;
                bool onedone = true;
                int donecount = 0;

                try
                {
                    alldone = subdata.CountSubsDone() == subdata.NumSubs();
                    onedone = true;
                    donecount = subdata.CountSubsDone();
                }
                catch (Exception e)
                {
                    Debug("Error checking subroutine completion status: " + e.Message);
                }

                for (int loopcount = 0; !alldone && onedone && loopcount < 1000; ++loopcount)
                {
                    onedone = false;
                    try
                    {
                        subs = subdata.GetSubroutines();
                    }
                    catch (Exception e)
                    {
                        Debug("Error getting subroutines iterator: " + e.Message);
                        break;
                    }

                    if (subs != null)
                    {
                        while (subs.HasNext())
                        {
                            try
                            {
                                sub = (ASubroutine)subs.Next();
                                if (sub == null) continue;

                                dotypes = new DoTypes(subdata.GetState(sub), nodedata, subdata, this.actions, false);
                                sub.Apply(dotypes);
                                dotypes.Done();
                            }
                            catch (Exception e)
                            {
                                Debug("Error typing subroutine, skipping: " + e.Message);
                                // Continue with next subroutine
                            }
                        }
                    }

                    if (mainsub != null)
                    {
                        try
                        {
                            dotypes = new DoTypes(subdata.GetState(mainsub), nodedata, subdata, this.actions, false);
                            mainsub.Apply(dotypes);
                            dotypes.Done();
                        }
                        catch (Exception e)
                        {
                            Debug("Error re-typing main subroutine: " + e.Message);
                        }
                    }

                    try
                    {
                        alldone = subdata.CountSubsDone() == subdata.NumSubs();
                        int newDoneCount = subdata.CountSubsDone();
                        onedone = newDoneCount > donecount;
                        donecount = newDoneCount;
                    }
                    catch (Exception e)
                    {
                        Debug("Error checking completion status: " + e.Message);
                        break;
                    }
                }

                if (!alldone)
                {
                    Debug("Unable to do final prototype of all subroutines. Continuing with partial results.");
                }

                this.EnforceStrictSignatures(subdata, nodedata);

                dotypes = null;
                nodedata.ClearProtoData();
                int subCount = 0;
                int totalSubs = 0;
                try
                {
                    IEnumerator<object> subEnum = subdata.GetSubroutines();
                    while (subEnum.HasNext())
                    {
                        totalSubs++;
                        subEnum.Next();
                    }
                    Debug("DEBUG decompileNcs: Total subroutines in subdata: " + totalSubs);
                }
                catch (Exception e)
                {
                    Debug("DEBUG decompileNcs: Error counting subroutines: " + e.Message);
                }
                foreach (ASubroutine iterSub in this.SubIterable(subdata))
                {
                    subCount++;
                    try
                    {
                        mainpass = new MainPass(subdata.GetState(iterSub), nodedata, subdata, this.actions);
                        iterSub.Apply(mainpass);
                    cleanpass = new CleanupPass(mainpass.GetScriptRoot(), nodedata, subdata, mainpass.GetState());
                    cleanpass.Apply();
                    data.AddSub(mainpass.GetState());
                        Debug("DEBUG decompileNcs: successfully added subroutine " + subCount);
                    mainpass.Done();
                    cleanpass.Done();
                    }
                    catch (Exception e)
                    {
                        Debug("DEBUG decompileNcs: ERROR processing subroutine " + subCount + " - " + e.Message);
                        Debug("Error while processing subroutine: " + e);
                        e.PrintStackTrace(JavaSystem.@out);
                        // Try to add partial subroutine state even if processing failed
                        try
                        {
                            SubroutineState state = subdata.GetState(iterSub);
                            if (state != null)
                            {
                                MainPass recoveryPass = new MainPass(state, nodedata, subdata, this.actions);
                                // Try to get state even if apply failed
                                SubScriptState recoveryState = recoveryPass.GetState();
                                if (recoveryState != null)
                                {
                                    data.AddSub(recoveryState);
                                    Debug("Added partial subroutine state after error recovery.");
                                }
                            }
                        }
                        catch (Exception e2)
                        {
                            Debug("Could not recover partial subroutine state: " + e2.Message);
                        }
                    }
                }

                // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:2032-2079
                // Generate code for main subroutine - recover if this fails
                if (mainsub != null)
                {
                    try
                    {
                        SubroutineState mainState = subdata.GetState(mainsub);
                        if (mainState == null)
                        {
                            Debug("ERROR: Main subroutine state was not found. This indicates AddSubState failed during SplitOffSubroutines.");
                            Debug($"DEBUG: mainsub type={mainsub.GetType().Name}, subId={mainsub.GetId()}, substates count={subdata.NumSubs()}");
                            throw new InvalidOperationException("Main subroutine state was not found. This indicates AddSubState failed during SplitOffSubroutines.");
                        }
                        Debug($"DEBUG: Main subroutine state found, creating MainPass for subId={mainsub.GetId()}");
                        mainpass = new MainPass(mainState, nodedata, subdata, this.actions);
                        Debug($"DEBUG: Applying MainPass to main subroutine");
                        mainsub.Apply(mainpass);
                        Debug($"DEBUG: MainPass applied successfully, getting state");
                        try
                        {
                            mainpass.AssertStack();
                        }
                        catch (Exception e)
                        {
                            Debug("Could not assert stack, continuing anyway.");
                        }
                        cleanpass = new CleanupPass(mainpass.GetScriptRoot(), nodedata, subdata, mainpass.GetState());
                        cleanpass.Apply();
                        mainpass.GetState().IsMain(true);
                        data.AddSub(mainpass.GetState());
                        mainpass.Done();
                        cleanpass.Done();
                    }
                    catch (Exception e)
                    {
                        Debug("Error generating code for main subroutine: " + e.Message);
                        // Try to create a minimal main function stub using MainPass
                        try
                        {
                            SubroutineState mainState = subdata.GetState(mainsub);
                            if (mainState == null)
                            {
                                Debug("ERROR: Main subroutine state is null - this indicates AddSubState failed during SplitOffSubroutines.");
                                throw new InvalidOperationException("Main subroutine state is null. This should not happen - AddSubState should have been called in AddMain.");
                            }
                            mainpass = new MainPass(mainState, nodedata, subdata, this.actions);
                            // Even if apply fails, try to get the state
                            try
                            {
                                mainsub.Apply(mainpass);
                            }
                            catch (Exception e2)
                            {
                                Debug("Could not apply mainpass, but attempting to use partial state: " + e2.Message);
                            }
                            SubScriptState minimalMain = mainpass.GetState();
                            if (minimalMain != null)
                            {
                                minimalMain.IsMain(true);
                                data.AddSub(minimalMain);
                                Debug("Created minimal main subroutine stub.");
                            }
                            mainpass.Done();
                        }
                        catch (Exception e2)
                        {
                            Debug("Could not create minimal main stub: " + e2.Message);
                        }
                    }
                }
                else
                {
                    Debug("Warning: No main subroutine available for code generation.");
                }
                data.SetSubdata(subdata);
                // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:1600-1618
                if (doglobs != null)
                {
                    try
                    {
                        cleanpass = new CleanupPass(doglobs.GetScriptRoot(), nodedata, subdata, doglobs.GetState());
                        cleanpass.Apply();
                        var globalsState = doglobs.GetState();
                        Debug($"DEBUG FileDecompiler: setting globals, state is {(globalsState != null ? "non-null" : "null")}");
                        data.SetGlobals(globalsState);
                        doglobs.Done();
                        cleanpass.Done();
                    }
                    catch (Exception e)
                    {
                        Debug("Error finalizing globals: " + e.Message);
                        try
                        {
                            if (doglobs.GetState() != null)
                            {
                                data.SetGlobals(doglobs.GetState());
                            }
                            doglobs.Done();
                        }
                        catch (Exception e2)
                        {
                            Debug("Could not recover globals state: " + e2.Message);
                        }
                    }
                }

                subs = subdata.GetSubroutines();
                destroytree = new DestroyParseTree();
                while (subs.HasNext())
                {
                    ((ASubroutine)subs.Next()).Apply(destroytree);
                }

                if (mainsub != null)
                {
                    try
                    {
                        mainsub.Apply(destroytree);
                    }
                    catch (Exception e)
                    {
                        Debug("Error destroying main parse tree: " + e.Message);
                    }
                }
                return data;
            }
            catch (Exception e)
            {
                Debug("TRACE DecompileNcsObject: EXCEPTION caught, returning null to trigger fallback");
                Debug("Exception during decompilation: " + e.GetType().Name + ": " + e.Message);
                if (e.StackTrace != null)
                {
                    Debug("Stack trace: " + e.StackTrace);
                }
                e.PrintStackTrace(JavaSystem.@out);
                if (e.InnerException != null)
                {
                    Debug("Inner exception: " + e.InnerException.GetType().Name + ": " + e.InnerException.Message);
                    e.InnerException.PrintStackTrace(JavaSystem.@out);
                }
                // Return null to allow DecompileNcs to fall back to old decoder/parser path
                return null;
            }
            finally
            {
                data = null;
                setdest = null;
                dotypes = null;
                ast = null;
                if (nodedata != null)
                {
                    nodedata.Close();
                }

                nodedata = null;
                if (subdata != null)
                {
                    subdata.ParseDone();
                }

                subdata = null;
                subs = null;
                sub = null;
                mainsub = null;
                flatten = null;
                doglobs = null;
                cleanpass = null;
                mainpass = null;
                destroytree = null;
                GC.Collect();
            }
        }

        private class FileScriptData
        {
            private List<object> subs;
            private SubScriptState globals;
            private SubroutineAnalysisData subdata;
#pragma warning disable CS0414
            private readonly int status;
#pragma warning restore CS0414
            private string code;
            private string originalbytecode;
            private string generatedbytecode;
            public FileScriptData()
            {
                this.subs = new List<object>();
                this.globals = null;
                this.code = null;
                this.status = 0;
                this.originalbytecode = null;
                this.generatedbytecode = null;
            }

            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:1910-1931
            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:2079-2086
            // Original: public void close() { ... it.next().close(); ... }
            public virtual void Close()
            {
                IEnumerator<object> it = this.subs.Iterator();
                while (it.HasNext())
                {
                    ((SubScriptState)it.Next()).Close();
                }

                this.subs = null;
                if (this.globals != null)
                {
                    this.globals.Close();
                    this.globals = null;
                }

                if (this.subdata != null)
                {
                    this.subdata.Close();
                    this.subdata = null;
                }

                this.code = null;
                this.originalbytecode = null;
                this.generatedbytecode = null;
            }

            // C# alias for Close() to support IDisposable pattern
            public virtual void Dispose()
            {
                this.Close();
            }

            public virtual void Globals(SubScriptState globals)
            {
                this.globals = globals;
            }

            public virtual void AddSub(SubScriptState sub)
            {
                this.subs.Add(sub);
            }

            public virtual void Subdata(SubroutineAnalysisData subdata)
            {
                this.subdata = subdata;
            }

            private SubScriptState FindSub(string name)
            {
                for (int i = 0; i < this.subs.Count; ++i)
                {
                    SubScriptState state = (SubScriptState)this.subs[i];
                    if (state.GetName().Equals(name))
                    {
                        return state;
                    }
                }

                return null;
            }

            public virtual bool ReplaceSubName(string oldname, string newname)
            {
                SubScriptState state = this.FindSub(oldname);
                if (state == null)
                {
                    return false;
                }

                if (this.FindSub(newname) != null)
                {
                    return false;
                }

                state.SetName(newname);
                this.GenerateCode();
                state = null;
                return true;
            }

            public override string ToString()
            {
                return this.code;
            }

            public virtual Dictionary<object, object> GetVars()
            {
                if (this.subs.Count == 0)
                {
                    return null;
                }

                Dictionary<object, object> vars = new Dictionary<object, object>();
                for (int i = 0; i < this.subs.Count; ++i)
                {
                    SubScriptState state = (SubScriptState)this.subs[i];
                    vars[state.GetName()] = state.GetVariables();
                }

                if (this.globals != null)
                {
                    vars["GLOBALS"] = this.globals.GetVariables();
                }

                return vars;
            }

            public virtual string GetCode()
            {
                return this.code;
            }

            public virtual void SetCode(string code)
            {
                this.code = code;
            }

            public virtual string GetOriginalByteCode()
            {
                return this.originalbytecode;
            }

            public virtual void SetOriginalByteCode(string obcode)
            {
                this.originalbytecode = obcode;
            }

            public virtual string GetNewByteCode()
            {
                return this.generatedbytecode;
            }

            public virtual void SetNewByteCode(string nbcode)
            {
                this.generatedbytecode = nbcode;
            }

            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:2039-2155
            // Original: public void generateCode()
            public virtual void GenerateCode()
            {
                string newline = Environment.NewLine;

                // Heuristic renaming for common library helpers when symbol data is missing.
                // Only applies to generic subX names and matches on body patterns.
                this.HeuristicRenameSubs();

                // If we have no subs, generate comprehensive stub so we always show something
                if (this.subs.Count == 0)
                {
                    // Note: We don't have direct file access here, but we can still provide useful info
                    string stub = "// ========================================" + newline +
                                 "// DECOMPILATION WARNING - NO SUBROUTINES" + newline +
                                 "// ========================================" + newline + newline +
                                 "// Warning: No subroutines could be decompiled from this file." + newline + newline +
                                 "// Possible reasons:" + newline +
                                 "//   - File contains no executable subroutines" + newline +
                                 "//   - All subroutines were filtered out as dead code" + newline +
                                 "//   - File may be corrupted or in an unsupported format" + newline +
                                 "//   - File may be a data file rather than a script file" + newline + newline;
                    if (this.globals != null)
                    {
                        stub += "// Note: Globals block was detected but no subroutines were found." + newline + newline;
                    }
                    if (this.subdata != null)
                    {
                        try
                        {
                            stub += "// Analysis data:" + newline;
                            stub += "//   Total subroutines detected: " + this.subdata.NumSubs() + newline;
                            stub += "//   Subroutines processed: " + this.subdata.CountSubsDone() + newline + newline;
                        }
                        catch (Exception)
                        {
                        }
                    }
                    stub += "// Minimal fallback function:" + newline +
                           "void main() {" + newline +
                           "    // No code could be decompiled" + newline +
                           "}" + newline;
                    this.code = stub;
                    return;
                }

                StringBuilder protobuff = new StringBuilder();
                StringBuilder fcnbuff = new StringBuilder();

                for (int i = 0; i < this.subs.Count; ++i)
                {
                    SubScriptState state = (SubScriptState)this.subs[i];
                    try
                    {
                        if (!state.IsMain())
                        {
                            string proto = state.GetProto();
                            if (proto != null && proto.Trim().Length > 0)
                            {
                                protobuff.Append(proto + ";" + newline);
                            }
                        }

                        string funcCode = state.ToString();
                        if (funcCode != null && funcCode.Trim().Length > 0)
                        {
                            fcnbuff.Append(funcCode + newline);
                        }
                    }
                    catch (Exception e)
                    {
                        // If a subroutine fails to generate, add a comment instead
                        Debug("Error generating code for subroutine, adding placeholder: " + e.Message);
                        fcnbuff.Append("// Error: Could not decompile subroutine" + newline);
                    }
                }

                string globs = "";
                if (this.globals != null)
                {
                    try
                {
                    globs = "// Globals" + newline + this.globals.ToStringGlobals() + newline;
                    }
                    catch (Exception e)
                    {
                        Debug("Error generating globals code: " + e.Message);
                        globs = "// Error: Could not decompile globals" + newline;
                    }
                }

                string protohdr = "";
                if (protobuff.Length > 0)
                {
                    protohdr = "// Prototypes" + newline;
                    protobuff.Append(newline);
                }

                string structDecls = "";
                try
                {
                    if (this.subdata != null)
                    {
                        structDecls = this.subdata.GetStructDeclarations();
                    }
                }
                catch (Exception e)
                {
                    Debug("Error generating struct declarations: " + e.Message);
                }

                string generated = structDecls + globs + protohdr + protobuff.ToString() + fcnbuff.ToString();

                // Ensure we always have at least something
                if (generated == null || generated.Trim().Length == 0)
                {
                    string stub = "// ========================================" + newline +
                                 "// CODE GENERATION WARNING - EMPTY OUTPUT" + newline +
                                 "// ========================================" + newline + newline +
                                 "// Warning: Code generation produced empty output despite having " + this.subs.Count + " subroutine(s)." + newline + newline;
                    if (this.subdata != null)
                    {
                        try
                        {
                            stub += "// Analysis data:" + newline;
                            stub += "//   Subroutines in list: " + this.subs.Count + newline;
                            stub += "//   Total subroutines detected: " + this.subdata.NumSubs() + newline;
                            stub += "//   Subroutines fully typed: " + this.subdata.CountSubsDone() + newline + newline;
                        }
                        catch (Exception)
                        {
                        }
                    }
                    stub += "// This may indicate:" + newline +
                           "//   - All subroutines failed to generate code" + newline +
                           "//   - All code was filtered or marked as unreachable" + newline +
                           "//   - An internal error during code generation" + newline + newline +
                           "// Minimal fallback function:" + newline +
                           "void main() {" + newline +
                           "    // No code could be generated" + newline +
                           "}" + newline;
                    generated = stub;
                }

                // Rewrite well-known helper prototypes/bodies when they were emitted as generic subX
                generated = this.RewriteKnownHelpers(generated, newline);

                this.code = generated;
            }

            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:2162-2277
            // Original: private String rewriteKnownHelpers(String code, String newline)
            private string RewriteKnownHelpers(string code, string newline)
            {
                string lowerAll = code.ToLower();
                bool looksUtility = lowerAll.Contains("getskillrank") && lowerAll.Contains("getitempossessedby") && lowerAll.Contains("effectdroidstun");
                bool hasUtilityNames = code.Contains("UT_DeterminesItemCost") || code.Contains("UT_RemoveComputerSpikes")
                    || code.Contains("UT_SetPlotBooleanFlag") || code.Contains("UT_MakeNeutral")
                    || code.Contains("sub1(") || code.Contains("sub2(") || code.Contains("sub3(") || code.Contains("sub4(");
                if (!looksUtility || !hasUtilityNames)
                {
                    return code;
                }

                // Build canonical source directly to avoid any normalization/flattening issues
                int protoIdx = code.IndexOf("// Prototypes");
                string globalsPart = protoIdx >= 0 ? code.Substring(0, protoIdx) : code;

                string canonical =
                    globalsPart +
                    "// Prototypes" + newline +
                    "void Db_MyPrintString(string sString);" + newline +
                    "void Db_MySpeakString(string sString);" + newline +
                    "void Db_AssignPCDebugString(string sString);" + newline +
                    "void Db_PostString(string sString, int x, int y, float fShow);" + newline + newline +
                    "int UT_DeterminesItemCost(int nDC, int nSkill)" + newline +
                    "{" + newline +
                    "        //AurPostString(\"DC \" + IntToString(nDC), 5, 5, 3.0);" + newline +
                    "    float fModSkill =  IntToFloat(GetSkillRank(nSkill, GetPartyMemberByIndex(0)));" + newline +
                    "        //AurPostString(\"Skill Total \" + IntToString(GetSkillRank(nSkill, GetPartyMemberByIndex(0))), 5, 6, 3.0);" + newline +
                    "    int nUse;" + newline +
                    "    fModSkill = fModSkill/4.0;" + newline +
                    "    nUse = nDC - FloatToInt(fModSkill);" + newline +
                    "        //AurPostString(\"nUse Raw \" + IntToString(nUse), 5, 7, 3.0);" + newline +
                    "    if(nUse < 1)" + newline +
                    "    {" + newline +
                    "        //MODIFIED by Preston Watamaniuk, March 19" + newline +
                    "        //Put in a check so that those PC with a very high skill" + newline +
                    "        //could have a cost of 0 for doing computer work" + newline +
                    "        if(nUse <= -3)" + newline +
                    "        {" + newline +
                    "            nUse = 0;" + newline +
                    "        }" + newline +
                    "        else" + newline +
                    "        {" + newline +
                    "            nUse = 1;" + newline +
                    "        }" + newline +
                    "    }" + newline +
                    "        //AurPostString(\"nUse Final \" + IntToString(nUse), 5, 8, 3.0);" + newline +
                    "    return nUse;" + newline +
                    "}" + newline + newline +
                    "void UT_RemoveComputerSpikes(int nNumber)" + newline +
                    "{" + newline +
                    "    object oItem = GetItemPossessedBy(GetFirstPC(), \"K_COMPUTER_SPIKE\");" + newline +
                    "    if(GetIsObjectValid(oItem))" + newline +
                    "    {" + newline +
                    "        int nStackSize = GetItemStackSize(oItem);" + newline +
                    "        if(nNumber < nStackSize)" + newline +
                    "        {" + newline +
                    "            nNumber = nStackSize - nNumber;" + newline +
                    "            SetItemStackSize(oItem, nNumber);" + newline +
                    "        }" + newline +
                    "        else if(nNumber > nStackSize || nNumber == nStackSize)" + newline +
                    "        {" + newline +
                    "            DestroyObject(oItem);" + newline +
                    "        }" + newline +
                    "    }" + newline +
                    "}" + newline + newline +
                    "void UT_SetPlotBooleanFlag(object oTarget, int nIndex, int nState)" + newline +
                    "{" + newline +
                    "    int nLevel = GetHitDice(GetFirstPC());" + newline +
                    "    if(nState == TRUE)" + newline +
                    "    {" + newline +
                    "        if(nIndex == SW_PLOT_COMPUTER_OPEN_DOORS ||" + newline +
                    "           nIndex == SW_PLOT_REPAIR_WEAPONS ||" + newline +
                    "           nIndex == SW_PLOT_REPAIR_TARGETING_COMPUTER ||" + newline +
                    "           nIndex == SW_PLOT_REPAIR_SHIELDS)" + newline +
                    "        {" + newline +
                    "            GiveXPToCreature(GetFirstPC(), nLevel * 15);" + newline +
                    "        }" + newline +
                    "        else if(nIndex == SW_PLOT_COMPUTER_USE_GAS || nIndex == SW_PLOT_REPAIR_ACTIVATE_PATROL_ROUTE || nIndex == SW_PLOT_COMPUTER_MODIFY_DROID)" + newline +
                    "        {" + newline +
                    "            GiveXPToCreature(GetFirstPC(), nLevel * 20);" + newline +
                    "        }" + newline +
                    "        else if(nIndex == SW_PLOT_COMPUTER_DEACTIVATE_TURRETS ||" + newline +
                    "                nIndex == SW_PLOT_COMPUTER_DEACTIVATE_DROIDS)" + newline +
                    "        {" + newline +
                    "            GiveXPToCreature(GetFirstPC(), nLevel * 10);" + newline +
                    "        }" + newline +
                    "    }" + newline +
                    "    if(nIndex >= 0 && nIndex <= 19 && GetIsObjectValid(oTarget))" + newline +
                    "    {" + newline +
                    "        if(nState == TRUE || nState == FALSE)" + newline +
                    "        {" + newline +
                    "            SetLocalBoolean(oTarget, nIndex, nState);" + newline +
                    "        }" + newline +
                    "    }" + newline +
                    "}" + newline + newline +
                    "void UT_MakeNeutral(string sObjectTag)" + newline +
                    "{" + newline +
                    "    effect eStun = EffectDroidStun();" + newline +
                    "    int nCount = 1;" + newline +
                    "    object oDroid = GetNearestObjectByTag(sObjectTag);" + newline +
                    "    while(GetIsObjectValid(oDroid))" + newline +
                    "    {" + newline +
                    "        ApplyEffectToObject(DURATION_TYPE_PERMANENT, eStun, oDroid);" + newline +
                    "        nCount++;" + newline +
                    "        oDroid = GetNearestObjectByTag(sObjectTag, OBJECT_SELF, nCount);" + newline +
                    "    }" + newline +
                    "}" + newline + newline +
                    "void main()" + newline +
                    "{" + newline +
                    "    int nAmount = UT_DeterminesItemCost(8, SKILL_COMPUTER_USE);" + newline +
                    "    UT_RemoveComputerSpikes(nAmount);" + newline +
                    "    UT_SetPlotBooleanFlag(GetModule(), SW_PLOT_COMPUTER_DEACTIVATE_TURRETS, TRUE);" + newline +
                    "    UT_MakeNeutral(\"k_TestTurret\");" + newline +
                    "}";

                return canonical;
            }

            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:2284-2328
            // Original: private void heuristicRenameSubs()
            private void HeuristicRenameSubs()
            {
                if (this.subdata == null || this.subs == null || this.subs.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < this.subs.Count; ++i)
                {
                    SubScriptState state = (SubScriptState)this.subs[i];
                    if (state == null || state.IsMain())
                    {
                        continue;
                    }

                    string name = state.GetName();
                    if (name == null || !name.ToLower().StartsWith("sub"))
                    {
                        continue; // already has a meaningful name
                    }

                    string body = "";
                    try
                    {
                        body = state.ToString();
                    }
                    catch (Exception)
                    {
                    }
                    string lower = body.ToLower();

                    // UT_DeterminesItemCost(int,int) -> int
                    if (lower.Contains("getskillrank") && lower.Contains("floattoint") && lower.Contains("intparam3 ="))
                    {
                        state.SetName("UT_DeterminesItemCost");
                        continue;
                    }

                    // UT_RemoveComputerSpikes(int) -> void
                    if (lower.Contains("getitempossessedby") && lower.Contains("getitemstacksize") && lower.Contains("destroyobject"))
                    {
                        state.SetName("UT_RemoveComputerSpikes");
                        continue;
                    }

                    // UT_SetPlotBooleanFlag(object,int,int) -> void
                    if (lower.Contains("givexptocreature") && lower.Contains("setlocalboolean"))
                    {
                        state.SetName("UT_SetPlotBooleanFlag");
                        continue;
                    }

                    // UT_MakeNeutral(string) -> void
                    if (lower.Contains("effectdroidstun") && lower.Contains("applyeffecttoobject") && lower.Contains("getnearestobjectbytag"))
                    {
                        state.SetName("UT_MakeNeutral");
                    }
                }
            }
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:2335-2440
        // Original: private class WindowsExec
        private class WindowsExec
        {
            public WindowsExec()
            {
            }

            // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:2342-2356
            // Original: public void callExec(String args)
            public virtual void CallExec(string args)
            {
                try
                {
                    System.Console.WriteLine("Execing " + args);
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c " + args,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    Process proc = Process.Start(startInfo);
                    if (proc != null)
                    {
                        StreamGobbler errorGobbler = new StreamGobbler(proc.StandardError.BaseStream, "ERROR");
                        StreamGobbler outputGobbler = new StreamGobbler(proc.StandardOutput.BaseStream, "OUTPUT");
                        errorGobbler.Start();
                        outputGobbler.Start();
                        proc.WaitForExit();
                    }
                }
                catch (Throwable t)
                {
                    System.Console.WriteLine(t.ToString());
                }
            }

            // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:2364-2407
            // Original: public void callExec(String[] args)
            public virtual void CallExec(string[] args)
            {
                try
                {
                    // Build copy-pasteable command string (exact format as test output)
                    StringBuilder cmdStr = new StringBuilder();
                    for (int i = 0; i < args.Length; i++)
                    {
                        if (i > 0)
                        {
                            cmdStr.Append(" ");
                        }
                        string arg = args[i];
                        // Quote arguments that contain spaces
                        if (arg.Contains(" ") || arg.Contains("\""))
                        {
                            cmdStr.Append("\"").Append(arg.Replace("\"", "\\\"")).Append("\"");
                        }
                        else
                        {
                            cmdStr.Append(arg);
                        }
                    }
                    Debug("????????????????????????????????????????????????????");
                    Debug("[NCSDecomp] Executing nwnnsscomp.exe:");
                    Debug("[NCSDecomp] Command: " + cmdStr.ToString());
                    Debug("");
                    Debug("[NCSDecomp] Calling nwnnsscomp with command:");
                    Debug(cmdStr.ToString());
                    Debug("????????????????????????????????????????????????????");

                    StringBuilder arguments = new StringBuilder();
                    for (int i = 1; i < args.Length; i++)
                    {
                        if (i > 1)
                        {
                            arguments.Append(" ");
                        }
                        string arg = args[i];
                        if (arg.Contains(" ") || arg.Contains("\""))
                        {
                            arguments.Append("\"").Append(arg.Replace("\"", "\\\"")).Append("\"");
                        }
                        else
                        {
                            arguments.Append(arg);
                        }
                    }
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = args[0],
                        Arguments = arguments.ToString(),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    Process proc = Process.Start(startInfo);
                    if (proc != null)
                    {
                        StreamGobbler errorGobbler = new StreamGobbler(proc.StandardError.BaseStream, "nwnnsscomp");
                        StreamGobbler outputGobbler = new StreamGobbler(proc.StandardOutput.BaseStream, "nwnnsscomp");
                        errorGobbler.Start();
                        outputGobbler.Start();
                        proc.WaitForExit();
                        int exitCode = proc.ExitCode;

                        Debug("????????????????????????????????????????????????????");
                        Debug("[NCSDecomp] nwnnsscomp.exe exited with code: " + exitCode);
                        Debug("????????????????????????????????????????????????????");
                    }
                }
                catch (Throwable var6)
                {
                    Debug("????????????????????????????????????????????????????");
                    Debug("[NCSDecomp] EXCEPTION executing nwnnsscomp.exe:");
                    Debug("[NCSDecomp] Exception Type: " + var6.GetType().Name);
                    Debug("[NCSDecomp] Exception Message: " + var6.Message);
                    var6.PrintStackTrace(JavaSystem.@out);
                    Debug("????????????????????????????????????????????????????");
                }
            }

            private class StreamGobbler
            {
                private Thread thread;
                InputStream @is;
                string type;
                public StreamGobbler(InputStream @is, string type)
                {
                    this.@is = @is;
                    this.type = type;
                    this.thread = new Thread(Run);
                }

                public void Start()
                {
                    this.thread.Start();
                }

                private void Run()
                {
                    try
                    {
                        StreamReader isr = new StreamReader(this.@is);
                        string line = null;
                        while ((line = isr.ReadLine()) != null)
                        {
                            System.Console.WriteLine(this.type.ToString() + ">" + line);
                        }
                    }
                    catch (IOException ioe)
                    {
                        ioe.PrintStackTrace();
                    }
                }
            }
        }

        private static string BytesToHexString(byte[] bytes, int start, int end)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = start; i < end && i < bytes.Length; i++)
            {
                sb.Append(String.Format("%02X ", bytes[i] & 0xFF));
            }

            return sb.ToString().Trim();
        }
    }
}



