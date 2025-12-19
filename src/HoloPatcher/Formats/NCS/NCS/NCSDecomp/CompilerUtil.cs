// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerUtil.java
// Copyright 2021-2025 NCSDecomp
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using static KOTORModSync.Formats.NCS.NCSDecomp.DecompilerLogger;
using System.IO;


namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerUtil.java:29-363
    // Original: public class CompilerUtil
    /// <summary>
    /// Utility class for compiler path resolution.
    /// </summary>
    public static class CompilerUtil
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerUtil.java:32-38
        // Original: private static final String[] COMPILER_NAMES = { ... }
        private static readonly string[] COMPILER_NAMES = {
            "nwnnsscomp.exe",              // Primary - generic name (highest priority)
            "nwnnsscomp_kscript.exe",      // Secondary - KOTOR Scripting Tool
            "nwnnsscomp_ktool.exe"         // KOTOR Tool variant
        };

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerUtil.java:51-75
        // Original: public static File resolveCompilerPath(String folderPath, String filename)
        public static NcsFile ResolveCompilerPath(string folderPath, string filename)
        {
            if (string.IsNullOrEmpty(folderPath) || string.IsNullOrWhiteSpace(folderPath))
            {
                return null;
            }
            if (string.IsNullOrEmpty(filename) || string.IsNullOrWhiteSpace(filename))
            {
                return null;
            }

            folderPath = folderPath.Trim();
            filename = filename.Trim();

            // Normalize folder path (ensure it's a directory path, not a file path)
            NcsFile folder = new NcsFile(folderPath);
                if (folder.IsFile())
                {
                    // If it's a file, use its parent directory
                    NcsFile parent = folder.GetParentFile();
                if (parent != null)
                {
                    folder = parent;
                }
                else
                {
                    return null;
                }
            }

            return new NcsFile(folder, filename);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerUtil.java:89-108
        // Original: public static File getCompilerFromSettings()
        public static NcsFile GetCompilerFromSettings()
        {
            // Get folder path and filename from Settings
            string folderPath = Decompiler.settings.GetProperty("nwnnsscomp Folder Path", "");
            string filename = Decompiler.settings.GetProperty("nwnnsscomp Filename", "");

            Error("DEBUG CompilerUtil.getCompilerFromSettings: folderPath='" + folderPath + "', filename='" + filename + "'");

            // Use shared resolution function
            NcsFile compilerFile = ResolveCompilerPath(folderPath, filename);

            if (compilerFile == null)
            {
                Error("DEBUG CompilerUtil.getCompilerFromSettings: folderPath or filename is empty/invalid");
                return null;
            }

            Error("DEBUG CompilerUtil.getCompilerFromSettings: compilerFile='" + compilerFile.GetAbsolutePath() + "', exists=" + compilerFile.Exists());

            // Return the file (even if it doesn't exist - caller can check)
            return compilerFile;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerUtil.java:119-125
        // Original: public static File getCompilerFromSettingsOrNull()
        public static NcsFile GetCompilerFromSettingsOrNull()
        {
            NcsFile compiler = GetCompilerFromSettings();
            if (compiler != null && compiler.Exists() && compiler.IsFile())
            {
                return compiler;
            }
            return null;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerUtil.java:143-206
        // Original: public static File resolveCompilerPathWithFallbacks(String cliPath)
        public static NcsFile ResolveCompilerPathWithFallbacks(string cliPath)
        {
            // 1. If CLI path is specified, use it (could be full path or just filename)
            if (!string.IsNullOrEmpty(cliPath) && !string.IsNullOrWhiteSpace(cliPath))
            {
                NcsFile cliFile = new NcsFile(cliPath.Trim());
                if (cliFile.Exists() && cliFile.IsFile())
                {
                    Error("DEBUG CompilerUtil.resolveCompilerPathWithFallbacks: Using CLI path: " + cliFile.GetAbsolutePath());
                    return cliFile;
                }
                // If CLI path is a directory, try known filenames in it
                if (cliFile.IsDirectory())
                {
                    foreach (string name in COMPILER_NAMES)
                    {
                        NcsFile candidate = new NcsFile(cliFile, name);
                        if (candidate.Exists() && candidate.IsFile())
                        {
                            Error("DEBUG CompilerUtil.resolveCompilerPathWithFallbacks: Found in CLI dir: " + candidate.GetAbsolutePath());
                            return candidate;
                        }
                    }
                }
            }

            // 2. Try tools/ directory
            string toolsDir = Path.Combine(JavaSystem.GetProperty("user.dir"), "tools");
            foreach (string name in COMPILER_NAMES)
            {
                    NcsFile candidate = new NcsFile(Path.Combine(toolsDir, name));
                if (candidate.Exists() && candidate.IsFile())
                {
                    Error("DEBUG CompilerUtil.resolveCompilerPathWithFallbacks: Found in tools/: " + candidate.GetAbsolutePath());
                    return candidate;
                }
            }

            // 3. Try current working directory
            string cwd = JavaSystem.GetProperty("user.dir");
            foreach (string name in COMPILER_NAMES)
            {
                NcsFile candidate = new NcsFile(Path.Combine(cwd, name));
                if (candidate.Exists() && candidate.IsFile())
                {
                    Error("DEBUG CompilerUtil.resolveCompilerPathWithFallbacks: Found in cwd: " + candidate.GetAbsolutePath());
                    return candidate;
                }
            }

            // 4. Try NCSDecomp installation directory
            NcsFile ncsDecompDir = GetNCSDecompDirectory();
            NcsFile cwdFile = new NcsFile(cwd);
            if (ncsDecompDir != null && !ncsDecompDir.GetAbsolutePath().Equals(cwdFile.GetAbsolutePath()))
            {
                foreach (string name in COMPILER_NAMES)
                {
                    NcsFile candidate = new NcsFile(ncsDecompDir, name);
                    if (candidate.Exists() && candidate.IsFile())
                    {
                        Error("DEBUG CompilerUtil.resolveCompilerPathWithFallbacks: Found in NCSDecomp dir: " + candidate.GetAbsolutePath());
                        return candidate;
                    }
                }
                // Also try tools/ subdirectory of NCSDecomp directory
                NcsFile ncsToolsDir = new NcsFile(ncsDecompDir, "tools");
                foreach (string name in COMPILER_NAMES)
                {
                    NcsFile candidate = new NcsFile(ncsToolsDir, name);
                    if (candidate.Exists() && candidate.IsFile())
                    {
                        Error("DEBUG CompilerUtil.resolveCompilerPathWithFallbacks: Found in NCSDecomp tools/: " + candidate.GetAbsolutePath());
                        return candidate;
                    }
                }
            }

            Error("DEBUG CompilerUtil.resolveCompilerPathWithFallbacks: No compiler found anywhere");
            return null;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerUtil.java:212-234
        // Original: public static class CompilerResolutionResult
        public class CompilerResolutionResult
        {
            private readonly NcsFile file;
            private readonly bool isFallback;
            private readonly string source;

            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerUtil.java:217-221
            // Original: public CompilerResolutionResult(File file, boolean isFallback, String source)
            public CompilerResolutionResult(NcsFile file, bool isFallback, string source)
            {
                this.file = file;
                this.isFallback = isFallback;
                this.source = source;
            }

            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerUtil.java:223-225
            // Original: public File getFile()
            public NcsFile GetFile()
            {
                return file;
            }

            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerUtil.java:227-229
            // Original: public boolean isFallback()
            public bool IsFallback()
            {
                return isFallback;
            }

            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerUtil.java:231-233
            // Original: public String getSource()
            public string GetSource()
            {
                return source;
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerUtil.java:244-318
        // Original: public static CompilerResolutionResult findCompilerFileWithResult(String configuredPath)
        public static CompilerResolutionResult FindCompilerFileWithResult(string configuredPath)
        {
            string[] compilerNames = COMPILER_NAMES;

            string configuredPathTrimmed = !string.IsNullOrEmpty(configuredPath) ? configuredPath.Trim() : "";
            bool isConfigured = !string.IsNullOrEmpty(configuredPathTrimmed);

            // 1. Try configured path (if set) - all compiler filenames
            if (isConfigured)
            {
                NcsFile configuredDir = new NcsFile(configuredPathTrimmed);
                if (configuredDir.IsDirectory())
                {
                    // If it's a directory, try all filenames in it
                    foreach (string name in compilerNames)
                    {
                        NcsFile candidate = new NcsFile(configuredDir, name);
                        if (candidate.Exists() && candidate.IsFile())
                        {
                            return new CompilerResolutionResult(candidate, false, "Configured directory: " + configuredPathTrimmed);
                        }
                    }
                }
                else
                {
                    // If it's a file, check if it exists
                    if (configuredDir.Exists() && configuredDir.IsFile())
                    {
                        return new CompilerResolutionResult(configuredDir, false, "Configured path: " + configuredPathTrimmed);
                    }
                    // Also try other filenames in the same directory
                    NcsFile parent = configuredDir.GetParentFile();
                    if (parent != null)
                    {
                        foreach (string name in compilerNames)
                        {
                            NcsFile candidate = new NcsFile(parent, name);
                            if (candidate.Exists() && candidate.IsFile())
                            {
                                return new CompilerResolutionResult(candidate, true, "Fallback in configured directory: " + parent.GetAbsolutePath());
                            }
                        }
                    }
                }
            }

            // 2. Try tools/ directory - all compiler filenames
            NcsFile toolsDir = new NcsFile(Path.Combine(JavaSystem.GetProperty("user.dir"), "tools"));
            foreach (string name in compilerNames)
            {
                NcsFile candidate = new NcsFile(toolsDir, name);
                if (candidate.Exists() && candidate.IsFile())
                {
                    return new CompilerResolutionResult(candidate, true, "Fallback: tools/ directory");
                }
            }

            // 3. Try current working directory - all compiler filenames
            NcsFile cwd = new NcsFile(JavaSystem.GetProperty("user.dir"));
            foreach (string name in compilerNames)
            {
                NcsFile candidate = new NcsFile(cwd, name);
                if (candidate.Exists() && candidate.IsFile())
                {
                    return new CompilerResolutionResult(candidate, true, "Fallback: current directory");
                }
            }

            // 4. Try NCSDecomp installation directory - all compiler filenames
            NcsFile ncsDecompDir = GetNCSDecompDirectory();
            if (ncsDecompDir != null && !ncsDecompDir.GetAbsolutePath().Equals(cwd.GetAbsolutePath()))
            {
                foreach (string name in compilerNames)
                {
                    NcsFile candidate = new NcsFile(ncsDecompDir, name);
                    if (candidate.Exists() && candidate.IsFile())
                    {
                        return new CompilerResolutionResult(candidate, true, "Fallback: NCSDecomp directory");
                    }
                }
                // Also try tools/ subdirectory of NCSDecomp directory
                NcsFile ncsToolsDir = new NcsFile(ncsDecompDir, "tools");
                foreach (string name in compilerNames)
                {
                    NcsFile candidate = new NcsFile(ncsToolsDir, name);
                    if (candidate.Exists() && candidate.IsFile())
                    {
                        return new CompilerResolutionResult(candidate, true, "Fallback: NCSDecomp tools/ directory");
                    }
                }
            }

            // Not found
            return null;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerUtil.java:326-353
        // Original: public static File getNCSDecompDirectory()
        public static NcsFile GetNCSDecompDirectory()
        {
            try
            {
                // Try to get the location of the assembly
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                if (assembly != null && !string.IsNullOrEmpty(assembly.Location))
                {
                    NcsFile assemblyFile = new NcsFile(assembly.Location);
                    if (assemblyFile.Exists() && assemblyFile.Directory != null)
                    {
                        return new NcsFile(assemblyFile.Directory);
                    }
                }
            }
            catch (Exception)
            {
                // Fall through to user.dir
            }
            // Fallback to user.dir if we can't determine assembly location
            return new NcsFile(JavaSystem.GetProperty("user.dir"));
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/CompilerUtil.java:361-363
        // Original: public static String[] getCompilerNames()
        public static string[] GetCompilerNames()
        {
            return (string[])COMPILER_NAMES.Clone();
        }
    }
}

