//
using System;
using System.Collections.Generic;
using System.IO;
using JavaSystem = KOTORModSync.Formats.NCS.NCSDecomp.JavaSystem;
using KOTORModSync.Common;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    /// <summary>
    /// Utility class to locate nwscript.nss files for K1 and TSL decompilation.
    /// </summary>
    public static class NWScriptLocator
    {
        public enum GameType
        {
            K1,
            TSL
        }

        /// <summary>
        /// Attempts to locate nwscript.nss file for the specified game type.
        /// </summary>
        /// <param name="gameType">Game type (K1 or TSL)</param>
        /// <param name="settings">Settings instance to check configured path</param>
        /// <returns>FileInfo for nwscript.nss if found, null otherwise</returns>
        public static NcsFile FindNWScriptFile(GameType gameType, Settings settings)
        {
            // 1. Check settings-configured path first
            string settingsPath = settings.GetProperty("NWScript Path");
            if (!string.IsNullOrEmpty(settingsPath))
            {
                NcsFile settingsFile = new NcsFile(settingsPath);
                if (settingsFile.IsFile())
                {
                    return settingsFile;
                }
            }

            // 2. Check current directory
            NcsFile currentDirFile = new NcsFile(Path.Combine(JavaSystem.GetProperty("user.dir"), "nwscript.nss"));
            if (currentDirFile.IsFile())
            {
                return currentDirFile;
            }

            // 2.5. Check common repository locations for nwscript files
            string repoRoot = FindRepositoryRoot();
            if (!string.IsNullOrEmpty(repoRoot))
            {
                // Check vendor/PyKotor/vendor/NorthernLights/Scripts/ (K1)
                string k1Nwscript = Path.Combine(repoRoot, "vendor", "PyKotor", "vendor", "NorthernLights", "Scripts", "k1_nwscript.nss");
                if (gameType == GameType.K1)
                {
                    NcsFile k1File = new NcsFile(k1Nwscript);
                    if (k1File.IsFile())
                    {
                        return k1File;
                    }
                }

                // Check include/ (TSL)
                string k2Nwscript = Path.Combine(repoRoot, "include", "k2_nwscript.nss");
                if (gameType == GameType.TSL)
                {
                    NcsFile k2File = new NcsFile(k2Nwscript);
                    if (k2File.IsFile())
                    {
                        return k2File;
                    }
                }

                // Also check tools/ directory
                string toolsK1 = Path.Combine(repoRoot, "tools", "k1_nwscript.nss");
                string toolsK2 = Path.Combine(repoRoot, "tools", "tsl_nwscript.nss");
                if (gameType == GameType.K1)
                {
                    NcsFile toolsK1File = new NcsFile(toolsK1);
                    if (toolsK1File.IsFile())
                    {
                        return toolsK1File;
                    }
                }
                else
                {
                    NcsFile toolsK2File = new NcsFile(toolsK2);
                    if (toolsK2File.IsFile())
                    {
                        return toolsK2File;
                    }
                }
            }

            // 3. Check vendor directories (relative to project root)
            List<string> candidatePaths = new List<string>();

            // Try to find project root by looking for vendor directory
            string currentDir = JavaSystem.GetProperty("user.dir");
            string searchDir = currentDir;
            for (int i = 0; i < 5; i++) // Search up to 5 levels up
            {
                string vendorPath = Path.Combine(searchDir, "vendor", "PyKotor", "vendor", "KotOR-Scripting-Tool", "NWN Script");
                if (Directory.Exists(vendorPath))
                {
                    candidatePaths.Add(Path.Combine(vendorPath, gameType == GameType.K1 ? "k1" : "k2", "nwscript.nss"));
                    candidatePaths.Add(Path.Combine(vendorPath, "k1", "nwscript.nss")); // Fallback to k1
                    candidatePaths.Add(Path.Combine(vendorPath, "k2", "nwscript.nss")); // Fallback to k2
                    break;
                }
                searchDir = Path.GetDirectoryName(searchDir);
                if (string.IsNullOrEmpty(searchDir))
                {
                    break;
                }
            }

            // Also check if we're already in a subdirectory that has vendor
            string altVendorPath = Path.Combine(currentDir, "..", "..", "..", "vendor", "PyKotor", "vendor", "KotOR-Scripting-Tool", "NWN Script");
            string resolvedAltPath = Path.GetFullPath(altVendorPath);
            if (Directory.Exists(resolvedAltPath))
            {
                candidatePaths.Add(Path.Combine(resolvedAltPath, gameType == GameType.K1 ? "k1" : "k2", "nwscript.nss"));
                candidatePaths.Add(Path.Combine(resolvedAltPath, "k1", "nwscript.nss"));
                candidatePaths.Add(Path.Combine(resolvedAltPath, "k2", "nwscript.nss"));
            }

            // Check each candidate path
            foreach (string candidatePath in candidatePaths)
            {
                NcsFile candidateFile = new NcsFile(candidatePath);
                if (candidateFile.IsFile())
                {
                    return candidateFile;
                }
            }

            // 4. Check game installation directories (if GameDirectoryLocator is available)
            try
            {
                // Use reflection to avoid hard dependency on Kotor.NET
                var gameLocatorType = Type.GetType("Kotor.NET.Helpers.GameDirectoryLocator, Kotor.NET");
                if (gameLocatorType != null)
                {
                    var instanceProperty = gameLocatorType.GetProperty("Instance");
                    if (instanceProperty != null)
                    {
                        object locator = instanceProperty.GetValue(null);
                        var locateMethod = gameLocatorType.GetMethod("Locate");
                        if (locateMethod != null)
                        {
                            var directories = locateMethod.Invoke(locator, null) as System.Array;
                            if (directories != null)
                            {
                                foreach (var dir in directories)
                                {
                                    var pathProperty = dir.GetType().GetProperty("Path");
                                    var engineProperty = dir.GetType().GetProperty("Engine");
                                    if (pathProperty != null && engineProperty != null)
                                    {
                                        string gamePath = pathProperty.GetValue(dir) as string;
                                        object engine = engineProperty.GetValue(dir);

                                        // Check if this is the right game type
                                        bool isK1 = engine != null && engine.ToString().Contains("K1");
                                        bool isTSL = engine != null && engine.ToString().Contains("K2");

                                        if ((gameType == GameType.K1 && isK1) || (gameType == GameType.TSL && isTSL))
                                        {
                                            if (!string.IsNullOrEmpty(gamePath))
                                            {
                                                string nwscriptPath = Path.Combine(gamePath, "nwscript.nss");
                                                NcsFile gameFile = new NcsFile(nwscriptPath);
                                                if (gameFile.IsFile())
                                                {
                                                    return gameFile;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore reflection errors - GameDirectoryLocator might not be available
            }

            return null;
        }

        /// <summary>
        /// Finds the repository root by searching upward for .git directory or .sln file.
        /// </summary>
        private static string FindRepositoryRoot()
        {
            string currentDir = JavaSystem.GetProperty("user.dir");
            DirectoryInfo dir = new DirectoryInfo(currentDir);

            while (dir != null)
            {
                // Check for .git directory or .sln file
                if (Directory.Exists(Path.Combine(dir.FullName, ".git")) ||
                    Directory.GetFiles(dir.FullName, "*.sln").Length > 0)
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }

            // Fallback to current directory if not found
            return currentDir;
        }

        /// <summary>
        /// Gets all possible candidate paths for nwscript.nss (for error messages).
        /// </summary>
        public static List<string> GetCandidatePaths(GameType gameType)
        {
            List<string> paths = new List<string>();

            string currentDir = JavaSystem.GetProperty("user.dir");
            paths.Add(Path.Combine(currentDir, "nwscript.nss"));

            // Repository paths
            string repoRoot = FindRepositoryRoot();
            if (!string.IsNullOrEmpty(repoRoot))
            {
                if (gameType == GameType.K1)
                {
                    paths.Add(Path.Combine(repoRoot, "vendor", "PyKotor", "vendor", "NorthernLights", "Scripts", "k1_nwscript.nss"));
                    paths.Add(Path.Combine(repoRoot, "tools", "k1_nwscript.nss"));
                }
                else
                {
                    paths.Add(Path.Combine(repoRoot, "include", "k2_nwscript.nss"));
                    paths.Add(Path.Combine(repoRoot, "tools", "tsl_nwscript.nss"));
                }
            }

            // Vendor paths
            string searchDir = currentDir;
            for (int i = 0; i < 5; i++)
            {
                string vendorPath = Path.Combine(searchDir, "vendor", "PyKotor", "vendor", "KotOR-Scripting-Tool", "NWN Script");
                if (Directory.Exists(vendorPath))
                {
                    paths.Add(Path.Combine(vendorPath, gameType == GameType.K1 ? "k1" : "k2", "nwscript.nss"));
                    break;
                }
                searchDir = Path.GetDirectoryName(searchDir);
                if (string.IsNullOrEmpty(searchDir))
                {
                    break;
                }
            }

            return paths;
        }
    }
}




