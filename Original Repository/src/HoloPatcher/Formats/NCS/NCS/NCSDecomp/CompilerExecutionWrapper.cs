// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerExecutionWrapper.java:1-527
// Copyright 2021-2025 NCSDecomp
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using static KOTORModSync.Formats.NCS.NCSDecomp.DecompilerLogger;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using KOTORModSync.Common;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerExecutionWrapper.java:35-525
    // Original: public class CompilerExecutionWrapper
    /// <summary>
    /// Unified execution wrapper for all nwnnsscomp.exe variants.
    /// This class abstracts away ALL compiler-specific quirks and differences,
    /// providing a completely unified interface. All compiler differences are
    /// handled transparently through file manipulation and environment setup.
    /// </summary>
    public class CompilerExecutionWrapper
    {
        private readonly NcsFile compilerFile;
        private readonly NcsFile sourceFile;
        private readonly NcsFile outputFile; // Used by NwnnsscompConfig internally
        private readonly bool isK2;
        private readonly KnownExternalCompilers.CompilerInfo compiler;
        private readonly NwnnsscompConfig config;
        /// <summary>Process-level environment overrides applied during compiler invocation.</summary>
        private readonly Dictionary<string, string> envOverrides = new Dictionary<string, string>();

        // Files/directories that need cleanup
        private readonly List<NcsFile> copiedIncludeFiles = new List<NcsFile>();
        private readonly List<NcsFile> copiedNwscriptFiles = new List<NcsFile>();
        private NcsFile originalNwscriptBackup = null;
        private NcsFile copiedSourceFile = null; // When using registry spoofing, source is copied to spoofed directory
        private NcsFile actualSourceFile = null; // The actual source file to use (original or copied)

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerExecutionWrapper.java:62-70
        // Original: public CompilerExecutionWrapper(File compilerFile, File sourceFile, File outputFile, boolean isK2) throws IOException
        public CompilerExecutionWrapper(NcsFile compilerFile, NcsFile sourceFile, NcsFile outputFile, bool isK2)
        {
            this.compilerFile = compilerFile;
            this.sourceFile = sourceFile;
            this.outputFile = outputFile;
            this.isK2 = isK2;
            this.config = new NwnnsscompConfig(compilerFile, sourceFile, outputFile, isK2);
            this.compiler = config.GetChosenCompiler();
            BuildEnvironmentOverrides();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerExecutionWrapper.java:79-93
        // Original: public void prepareExecutionEnvironment(List<File> includeDirs) throws IOException
        public void PrepareExecutionEnvironment(List<NcsFile> includeDirs)
        {
            // Pattern 2: nwscript.nss abstraction (must be done first for registry spoofing logic)
            PrepareNwscriptFile();

            // If registry spoofing is needed, copy everything to the spoofed directory
            if (NeedsRegistrySpoofing())
            {
                PrepareRegistrySpoofedEnvironment(includeDirs);
            }
            else
            {
                // Pattern 1: Include file abstraction (normal path)
                PrepareIncludeFiles(includeDirs);
                actualSourceFile = sourceFile;
            }

            // Additional patterns handled automatically during execution
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerExecutionWrapper.java:98-100
        // Original: private boolean needsRegistrySpoofing()
        private bool NeedsRegistrySpoofing()
        {
            return compiler == KnownExternalCompilers.KOTOR_TOOL || compiler == KnownExternalCompilers.KOTOR_SCRIPTING_TOOL;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerExecutionWrapper.java:106-150
        // Original: private void prepareRegistrySpoofedEnvironment(List<File> includeDirs) throws IOException
        private void PrepareRegistrySpoofedEnvironment(List<NcsFile> includeDirs)
        {
            NcsFile toolsDir = compilerFile.GetParentFile(); // tools/ directory (where registry is spoofed)
            if (toolsDir == null || !toolsDir.Exists())
            {
                throw new IOException("Compiler directory does not exist: " + (toolsDir != null ? toolsDir.GetAbsolutePath() : "null"));
            }

            Debug("[INFO] CompilerExecutionWrapper: Preparing registry-spoofed environment in: " + toolsDir.GetAbsolutePath());

            // Copy source file to tools directory
            copiedSourceFile = new NcsFile(toolsDir, sourceFile.Name);
            Debug("[INFO] CompilerExecutionWrapper: COPYING source file: " + sourceFile.GetAbsolutePath() + " -> " + copiedSourceFile.GetAbsolutePath());
            System.IO.File.Copy(sourceFile.GetAbsolutePath(), copiedSourceFile.GetAbsolutePath(), true);
            actualSourceFile = copiedSourceFile;
            Debug("[INFO] CompilerExecutionWrapper: Copied source file to spoofed directory: " + copiedSourceFile.GetAbsolutePath());

            // Copy include files to tools directory
            if (includeDirs != null && includeDirs.Count > 0)
            {
                HashSet<string> neededIncludes = ExtractIncludeFiles(sourceFile);
                foreach (string includeName in neededIncludes)
                {
                    NcsFile destFile = new NcsFile(toolsDir, includeName);
                    // Skip if already exists
                    if (destFile.Exists())
                    {
                        Debug("[INFO] CompilerExecutionWrapper: Include file already exists in spoofed directory: " + includeName);
                        continue;
                    }

                    // Search for include file in include directories
                    foreach (NcsFile includeDir in includeDirs)
                    {
                        if (includeDir != null && includeDir.Exists())
                        {
                            NcsFile includeFile = new NcsFile(includeDir, includeName);
                            if (includeFile.Exists() && includeFile.IsFile())
                            {
                                Debug("[INFO] CompilerExecutionWrapper: COPYING include file: " + includeFile.GetAbsolutePath() + " -> " + destFile.GetAbsolutePath());
                                System.IO.File.Copy(includeFile.GetAbsolutePath(), destFile.GetAbsolutePath(), true);
                                copiedIncludeFiles.Add(destFile);
                                Debug("[INFO] CompilerExecutionWrapper: Copied include file to spoofed directory: " + includeName + " -> " + destFile.GetAbsolutePath());
                                break;
                            }
                        }
                    }
                }
            }

            // nwscript.nss should already be in tools directory from prepareNwscriptFile()
            Debug("[INFO] CompilerExecutionWrapper: Registry-spoofed environment ready. Source: " + actualSourceFile.GetAbsolutePath());
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerExecutionWrapper.java:156-198
        // Original: private void prepareIncludeFiles(List<File> includeDirs) throws IOException
        private void PrepareIncludeFiles(List<NcsFile> includeDirs)
        {
            if (includeDirs == null || includeDirs.Count == 0)
            {
                return;
            }

            // Check if compiler supports -i flag
            bool supportsIncludeFlag = (compiler != KnownExternalCompilers.KOTOR_TOOL
                  && compiler != KnownExternalCompilers.KOTOR_SCRIPTING_TOOL);

            if (!supportsIncludeFlag)
            {
                // Compiler doesn't support -i, copy include files to source directory
                NcsFile sourceDir = sourceFile.GetParentFile();
                if (sourceDir == null)
                {
                    return;
                }

                // Parse source file to find which includes are needed
                HashSet<string> neededIncludes = ExtractIncludeFiles(sourceFile);

                // Copy needed include files from include directories to source directory
                foreach (string includeName in neededIncludes)
                {
                    NcsFile destFile = new NcsFile(sourceDir, includeName);
                    // Skip if already exists in source directory
                    if (destFile.Exists())
                    {
                        continue;
                    }

                    // Search for include file in include directories
                    foreach (NcsFile includeDir in includeDirs)
                    {
                        if (includeDir != null && includeDir.Exists())
                        {
                            NcsFile includeFile = new NcsFile(includeDir, includeName);
                            if (includeFile.Exists() && includeFile.IsFile())
                            {
                                Debug("[INFO] CompilerExecutionWrapper: COPYING include file: " + includeFile.GetAbsolutePath() + " -> " + destFile.GetAbsolutePath());
                                System.IO.File.Copy(includeFile.GetAbsolutePath(), destFile.GetAbsolutePath(), true);
                                copiedIncludeFiles.Add(destFile);
                                Debug("[INFO] CompilerExecutionWrapper: Copied include file: " + includeName + " from " + includeFile.GetAbsolutePath());
                                break;
                            }
                        }
                    }
                }
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerExecutionWrapper.java:205-255
        // Original: private void prepareNwscriptFile() throws IOException
        private void PrepareNwscriptFile()
        {
            NcsFile compilerDir = compilerFile.GetParentFile();
            if (compilerDir == null)
            {
                return;
            }

            NcsFile compilerNwscript = new NcsFile(compilerDir, "nwscript.nss");

            // Determine which nwscript.nss to use
            NcsFile nwscriptSource = DetermineNwscriptSource();
            if (nwscriptSource == null || !nwscriptSource.Exists())
            {
                Debug("[INFO] CompilerExecutionWrapper: Warning: nwscript.nss source not found");
                return;
            }

            // Check if we need to update the compiler's nwscript.nss
            bool needsUpdate = true;
            if (compilerNwscript.Exists())
            {
                // Check if it's the same file (by path comparison - Files.isSameFile equivalent)
                try
                {
                    // For C#, we'll compare by full path since we can't easily check if files are the same
                    // across different drives without reading content
                    if (nwscriptSource.GetAbsolutePath().Equals(compilerNwscript.GetAbsolutePath()))
                    {
                        needsUpdate = false;
                    }
                }
                catch (Exception)
                {
                    // Files might be on different drives, compare by content
                    needsUpdate = true;
                }
            }

            if (needsUpdate)
            {
                // Backup original if it exists and is different
                if (compilerNwscript.Exists())
                {
                    NcsFile backup = new NcsFile(compilerDir, "nwscript.nss.backup");
                    if (backup.Exists())
                    {
                        Debug("[INFO] CompilerExecutionWrapper: DELETING existing backup file: " + backup.GetAbsolutePath());
                        backup.Delete();
                    }
                    Debug("[INFO] CompilerExecutionWrapper: COPYING nwscript.nss to backup: " + compilerNwscript.GetAbsolutePath() + " -> " + backup.GetAbsolutePath());
                    System.IO.File.Copy(compilerNwscript.GetAbsolutePath(), backup.GetAbsolutePath(), true);
                    originalNwscriptBackup = backup;
                    Debug("[INFO] CompilerExecutionWrapper: Created backup of original nwscript.nss: " + backup.GetAbsolutePath());
                }

                // Copy the appropriate nwscript.nss
                Debug("[INFO] CompilerExecutionWrapper: COPYING nwscript.nss (RENAME): " + nwscriptSource.GetAbsolutePath() + " -> " + compilerNwscript.GetAbsolutePath());
                Debug("[INFO] CompilerExecutionWrapper: Source file: " + nwscriptSource.Name + " (K2=" + isK2 + ")");
                System.IO.File.Copy(nwscriptSource.GetAbsolutePath(), compilerNwscript.GetAbsolutePath(), true);
                copiedNwscriptFiles.Add(compilerNwscript);
                Debug("[INFO] CompilerExecutionWrapper: Copied nwscript.nss: " + nwscriptSource.Name + " -> " + compilerNwscript.GetAbsolutePath());
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerExecutionWrapper.java:260-282
        // Original: private File determineNwscriptSource()
        private NcsFile DetermineNwscriptSource()
        {
            NcsFile toolsDir = new NcsFile(new NcsFile(JavaSystem.GetProperty("user.dir")), "tools");

            if (isK2)
            {
                // For K2, use tsl_nwscript.nss
                return new NcsFile(toolsDir, "tsl_nwscript.nss");
            }
            else
            {
                // For K1, check if script needs ASC nwscript (ActionStartConversation with 11 params)
                bool needsAsc = CheckNeedsAscNwscript(sourceFile);
                if (needsAsc)
                {
                    // Try k1_asc_nwscript.nss first, then k1_asc_donotuse_nwscript.nss
                    NcsFile ascNwscript = new NcsFile(toolsDir, "k1_asc_nwscript.nss");
                    if (!ascNwscript.Exists())
                    {
                        ascNwscript = new NcsFile(toolsDir, "k1_asc_donotuse_nwscript.nss");
                    }
                    if (ascNwscript.Exists())
                    {
                        return ascNwscript;
                    }
                }
                // Default to k1_nwscript.nss
                return new NcsFile(toolsDir, "k1_nwscript.nss");
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerExecutionWrapper.java:287-299
        // Original: private boolean checkNeedsAscNwscript(File nssFile)
        private bool CheckNeedsAscNwscript(NcsFile nssFile)
        {
            try
            {
                string content = System.IO.File.ReadAllText(nssFile.GetAbsolutePath(), Encoding.UTF8);
                // Look for ActionStartConversation calls with 11 parameters (10 commas)
                Regex pattern = new Regex(
                      @"ActionStartConversation\s*\(([^,)]*,\s*){10}[^)]*\)",
                      RegexOptions.Multiline);
                return pattern.IsMatch(content);
            }
            catch (Exception e)
            {
                Debug("[INFO] CompilerExecutionWrapper: Failed to check for ASC nwscript requirement: " + e.Message);
                return false;
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerExecutionWrapper.java:304-322
        // Original: private Set<String> extractIncludeFiles(File sourceFile)
        private HashSet<string> ExtractIncludeFiles(NcsFile sourceFile)
        {
            HashSet<string> includes = new HashSet<string>();
            try
            {
                string content = System.IO.File.ReadAllText(sourceFile.GetAbsolutePath(), Encoding.UTF8);
                Regex includePattern = new Regex(@"#include\s+[""<]([^"">]+)["">]");
                MatchCollection matches = includePattern.Matches(content);
                foreach (Match match in matches)
                {
                    string includeName = match.Groups[1].Value;
                    // Normalize: add .nss extension if missing
                    if (!includeName.EndsWith(".nss") && !includeName.EndsWith(".h"))
                    {
                        includeName = includeName + ".nss";
                    }
                    includes.Add(includeName);
                }
            }
            catch (Exception e)
            {
                Debug("[INFO] CompilerExecutionWrapper: Failed to parse includes from source: " + e.Message);
            }
            return includes;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerExecutionWrapper.java:328-354
        // Original: public File getWorkingDirectory()
        public NcsFile GetWorkingDirectory()
        {
            // Some legacy compilers (KOTOR Tool / Scripting Tool) behave more reliably
            // when run from their own directory because they probe for nwscript.nss and
            // other resources relative to the executable instead of the source file.
            if (compiler == KnownExternalCompilers.KOTOR_TOOL || compiler == KnownExternalCompilers.KOTOR_SCRIPTING_TOOL
                  || !SupportsGameFlag())
            {
                NcsFile compilerDir = compilerFile.GetParentFile();
                if (compilerDir != null && compilerDir.Exists())
                {
                    Debug("[INFO] CompilerExecutionWrapper: Using compiler directory as working dir: "
                        + compilerDir.GetAbsolutePath());
                    return compilerDir;
                }
            }

            // Most compilers work best when run from the source file's directory
            NcsFile sourceDir = sourceFile.GetParentFile();
            if (sourceDir != null && sourceDir.Exists())
            {
                return sourceDir;
            }
            // Fallback to compiler directory
            NcsFile compilerDir2 = compilerFile.GetParentFile();
            if (compilerDir2 != null && compilerDir2.Exists())
            {
                return compilerDir2;
            }
            // Final fallback to current directory
            return new NcsFile(JavaSystem.GetProperty("user.dir"));
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerExecutionWrapper.java:361-375
        // Original: public String[] getCompileArgs(List<File> includeDirs)
        public string[] GetCompileArgs(List<NcsFile> includeDirs)
        {
            // If we copied the source file for registry spoofing, we need to create a new config with the copied file
            if (actualSourceFile != null && !actualSourceFile.GetAbsolutePath().Equals(sourceFile.GetAbsolutePath()))
            {
                try
                {
                    // Create a temporary config with the copied source file
                    NwnnsscompConfig spoofedConfig = new NwnnsscompConfig(compilerFile, actualSourceFile, outputFile, isK2);
                    return spoofedConfig.GetCompileArgs(compilerFile.GetAbsolutePath(), includeDirs);
                }
                catch (Exception e)
                {
                    Debug("[INFO] CompilerExecutionWrapper: Failed to create spoofed config, using original: " + e.Message);
                    // Fall back to original config
                    return config.GetCompileArgs(compilerFile.GetAbsolutePath(), includeDirs);
                }
            }
            return config.GetCompileArgs(compilerFile.GetAbsolutePath(), includeDirs);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerExecutionWrapper.java:381-429
        // Original: public void cleanup()
        public void Cleanup()
        {
            // Clean up copied source file (if registry spoofing was used)
            if (copiedSourceFile != null && copiedSourceFile.Exists())
            {
                try
                {
                    Debug("[INFO] CompilerExecutionWrapper: DELETING copied source file: " + copiedSourceFile.GetAbsolutePath());
                    copiedSourceFile.Delete();
                    Debug("[INFO] CompilerExecutionWrapper: Cleaned up copied source file: " + copiedSourceFile.Name);
                }
                catch (Exception e)
                {
                    Debug("[INFO] CompilerExecutionWrapper: Failed to clean up copied source file " + copiedSourceFile.Name + ": " + e.Message);
                }
                copiedSourceFile = null;
            }

            // Clean up copied include files
            foreach (NcsFile copiedFile in copiedIncludeFiles)
            {
                try
                {
                    if (copiedFile.Exists())
                    {
                        Debug("[INFO] CompilerExecutionWrapper: DELETING include file: " + copiedFile.GetAbsolutePath());
                        copiedFile.Delete();
                        Debug("[INFO] CompilerExecutionWrapper: Cleaned up include file: " + copiedFile.Name);
                    }
                }
                catch (Exception e)
                {
                    Debug("[INFO] CompilerExecutionWrapper: Failed to clean up include file " + copiedFile.Name + ": " + e.Message);
                }
            }
            copiedIncludeFiles.Clear();

            // Restore original nwscript.nss if we backed it up
            if (originalNwscriptBackup != null && originalNwscriptBackup.Exists())
            {
                try
                {
                    NcsFile compilerDir = compilerFile.GetParentFile();
                    if (compilerDir != null)
                    {
                        NcsFile compilerNwscript = new NcsFile(compilerDir, "nwscript.nss");
                        if (compilerNwscript.Exists())
                        {
                            Debug("[INFO] CompilerExecutionWrapper: COPYING (RESTORE) nwscript.nss from backup: " + originalNwscriptBackup.GetAbsolutePath() + " -> " + compilerNwscript.GetAbsolutePath());
                            System.IO.File.Copy(originalNwscriptBackup.GetAbsolutePath(), compilerNwscript.GetAbsolutePath(), true);
                            Debug("[INFO] CompilerExecutionWrapper: Restored original nwscript.nss");
                        }
                    }
                    Debug("[INFO] CompilerExecutionWrapper: DELETING backup file: " + originalNwscriptBackup.GetAbsolutePath());
                    originalNwscriptBackup.Delete();
                }
                catch (Exception e)
                {
                    Debug("[INFO] CompilerExecutionWrapper: Failed to restore original nwscript.nss: " + e.Message);
                }
            }

            // Note: We don't delete the copied nwscript.nss files because they might be needed
            // for subsequent compilations. They'll be overwritten on next use.
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerExecutionWrapper.java:434-436
        // Original: public KnownExternalCompilers getCompiler()
        public KnownExternalCompilers.CompilerInfo GetCompiler()
        {
            return compiler;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerExecutionWrapper.java:443-445
        // Original: public java.util.Map<String, String> getEnvironmentOverrides()
        public Dictionary<string, string> GetEnvironmentOverrides()
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> kvp in envOverrides)
            {
                result[kvp.Key] = kvp.Value;
            }
            return result;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerExecutionWrapper.java:452-470
        // Original: private void buildEnvironmentOverrides()
        private void BuildEnvironmentOverrides()
        {
            NcsFile toolsDir = new NcsFile(new NcsFile(JavaSystem.GetProperty("user.dir")), "tools");

            // Only apply overrides for legacy compilers that ignore -g or probe registry
            bool needsRootOverride = compiler == KnownExternalCompilers.KOTOR_TOOL
                  || compiler == KnownExternalCompilers.KOTOR_SCRIPTING_TOOL
                  || !SupportsGameFlag();

            if (!needsRootOverride)
            {
                return;
            }

            string resolvedRoot = toolsDir.GetAbsolutePath();
            envOverrides["NWN_ROOT"] = resolvedRoot;
            envOverrides["NWNDir"] = resolvedRoot;
            envOverrides["KOTOR_ROOT"] = resolvedRoot;
            Debug("[INFO] CompilerExecutionWrapper: Applied environment overrides for legacy compiler. "
                + "NWN_ROOT=" + resolvedRoot + ", compiler=" + compiler.Name);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerExecutionWrapper.java:475-483
        // Original: private boolean supportsGameFlag()
        private bool SupportsGameFlag()
        {
            string[] args = compiler.GetCompileArgs();
            foreach (string arg in args)
            {
                if (arg.Contains("{game_value}") || "-g".Equals(arg) || arg.StartsWith("-g"))
                {
                    return true;
                }
            }
            return false;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerExecutionWrapper.java:506-524
        // Original: public AutoCloseable createRegistrySpoofer()
        public IDisposable CreateRegistrySpoofer()
        {
            // Only KOTOR Tool and KOTOR Scripting Tool require registry spoofing
            if (compiler == KnownExternalCompilers.KOTOR_TOOL || compiler == KnownExternalCompilers.KOTOR_SCRIPTING_TOOL)
            {
                // Use the tools directory as the installation path (where compiler and nwscript files are)
                NcsFile toolsDir = new NcsFile(new NcsFile(JavaSystem.GetProperty("user.dir")), "tools");
                try
                {
                    RegistrySpoofer spoofer = new RegistrySpoofer(toolsDir, isK2);
                    Debug("[INFO] CompilerExecutionWrapper: Created RegistrySpoofer for " + compiler.Name);
                    return spoofer;
                }
                catch (NotSupportedException e)
                {
                    // Not on Windows - fall back to NoOp
                    Debug("[INFO] CompilerExecutionWrapper: Registry spoofing not supported, using NoOp: " + e.Message);
                    return new NoOpRegistrySpoofer();
                }
            }
            else
            {
                // Compiler doesn't need registry spoofing
                return new NoOpRegistrySpoofer();
            }
        }
    }
}
