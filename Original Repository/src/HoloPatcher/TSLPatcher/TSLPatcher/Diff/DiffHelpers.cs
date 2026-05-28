using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KOTORModSync;
using KOTORModSync.Common;
using KOTORModSync.Formats.ERF;
using KOTORModSync.Installation;
using KOTORModSync.Mods;
using KOTORModSync.Resource;
using KOTORModSync.TSLPatcher;
using KOTORModSync.Tools;
using JetBrains.Annotations;

namespace KOTORModSync.Diff
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:133-262
    // Helper functions for diff operations

    /// <summary>
    /// Determine which source file should be copied to tslpatchdata.
    /// </summary>
    public static class DiffHelpers
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:133-152
        // Original: def _determine_tslpatchdata_source(file1_path: Path, file2_path: Path) -> str:
        /// <summary>
        /// Determine which source file should be copied to tslpatchdata.
        /// </summary>
        public static string DetermineTslpatchdataSource(string file1Path, string file2Path)
        {
            // For now, implement 2-way logic (use vanilla/base version)
            return $"vanilla ({file1Path.Replace('\\', '/')})";
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:155-261
        // Original: def _determine_destination_for_source(...) -> str:
        /// <summary>
        /// Determine the proper TSLPatcher destination based on resource resolution order.
        /// </summary>
        public static string DetermineDestinationForSource(
            string sourcePath,
            string resourceName = null,
            bool verbose = true,
            Action<string> logFunc = null,
            string locationType = null,
            string sourceFilepath = null)
        {
            if (logFunc == null)
            {
                logFunc = _ => { };
            }

            string displayName = resourceName ?? Path.GetFileName(sourcePath);

            // PRIORITY 1: Use explicit location_type if provided (resolution-aware path)
            if (!string.IsNullOrEmpty(locationType))
            {
                if (locationType == "Override folder")
                {
                    if (verbose)
                    {
                        logFunc($"    +-- Resolution: {displayName} found in Override");
                        logFunc("    +-- Destination: Override (highest priority)");
                    }
                    return "Override";
                }

                if (locationType == "Modules (.mod)")
                {
                    // Resource is in a .mod file - patch directly to that .mod
                    string actualFilepath = sourceFilepath ?? sourcePath;
                    string destination = $"modules\\{Path.GetFileName(actualFilepath)}";
                    if (verbose)
                    {
                        logFunc($"    +-- Resolution: {displayName} found in {Path.GetFileName(actualFilepath)}");
                        logFunc($"    +-- Destination: {destination} (patch .mod directly)");
                    }
                    return destination;
                }

                if (locationType == "Modules (.rim)" || locationType == "Modules (.rim/_s.rim/_dlg.erf)")
                {
                    // Resource is in read-only .rim/.erf - redirect to corresponding .mod
                    string actualFilepath = sourceFilepath ?? sourcePath;
                    string moduleRoot = Module.NameToRoot(actualFilepath);
                    string destination = $"modules\\{moduleRoot}.mod";
                    if (verbose)
                    {
                        logFunc($"    +-- Resolution: {displayName} found in {Path.GetFileName(actualFilepath)} (read-only)");
                        logFunc($"    +-- Destination: {destination} (.mod overrides .rim/.erf)");
                    }
                    return destination;
                }

                if (locationType == "Chitin BIFs")
                {
                    // Resource only in BIFs - must go to Override (can't modify BIFs)
                    if (verbose)
                    {
                        logFunc($"    +-- Resolution: {displayName} found in Chitin BIFs (read-only)");
                        logFunc("    +-- Destination: Override (BIFs cannot be modified)");
                    }
                    return "Override";
                }

                // Unknown location type - log warning and fall through to path inference
                if (verbose)
                {
                    logFunc($"    +-- Warning: Unknown location_type '{locationType}', using path inference");
                }
            }

            // FALLBACK: Path-based inference (for non-resolution-aware code paths)
            string[] parentNames = sourceFilepath != null
                ? Path.GetDirectoryName(sourceFilepath)?.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries) ?? new string[0]
                : Path.GetDirectoryName(sourcePath)?.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries) ?? new string[0];

            HashSet<string> parentNamesLower = new HashSet<string>(parentNames.Select(p => p.ToLowerInvariant()));

            if (parentNamesLower.Contains("override"))
            {
                // Determine if it's a read-only source (RIM/ERF)
                if (!IsReadonlySource(sourcePath))
                {
                    // MOD file - can patch directly
                    string destination = $"modules\\{Path.GetFileName(sourcePath)}";
                    if (verbose)
                    {
                        logFunc($"    +-- Path inference: {displayName} in writable .mod");
                        logFunc($"    +-- Destination: {destination} (patch directly)");
                    }
                    return destination;
                }
                // Read-only module file - redirect to .mod
                string moduleRoot = Module.NameToRoot(sourcePath);
                string dest = $"modules\\{moduleRoot}.mod";
                if (verbose)
                {
                    logFunc($"    +-- Path inference: {displayName} in read-only {Path.GetExtension(sourcePath)}");
                    logFunc($"    +-- Destination: {dest} (.mod overrides read-only)");
                }
                return dest;
            }

            // BIF/chitin sources go to Override
            if (IsReadonlySource(sourcePath))
            {
                if (verbose)
                {
                    logFunc($"    +-- Path inference: {displayName} in read-only BIF/chitin");
                    logFunc("    +-- Destination: Override (read-only source)");
                }
                return "Override";
            }

            // Default to Override for other cases
            if (verbose)
            {
                logFunc($"    +-- Path inference: {displayName} (no specific location detected)");
                logFunc("    +-- Destination: Override (default)");
            }
            return "Override";
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py
        // Original: def _is_readonly_source(source_path: Path) -> bool:
        /// <summary>
        /// Check if a source path is read-only (BIF, RIM, ERF).
        /// </summary>
        private static bool IsReadonlySource(string sourcePath)
        {
            if (string.IsNullOrEmpty(sourcePath))
            {
                return false;
            }

            string ext = Path.GetExtension(sourcePath).ToLowerInvariant();
            return ext == ".bif" || ext == ".rim" || ext == ".erf";
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:264-320
        // Original: def _ensure_capsule_install(...) -> None:
        /// <summary>
        /// Ensure a capsule (.mod) exists in tslpatchdata and is listed before patching.
        /// </summary>
        public static void EnsureCapsuleInstall(
            ModificationsByType modificationsByType,
            string capsuleDestination,
            string capsulePath = null,
            Action<string> logFunc = null,
            [CanBeNull] IncrementalTSLPatchDataWriter incrementalWriter = null)
        {
            if (logFunc == null)
            {
                logFunc = _ => { };
            }

            string normalizedDestination = capsuleDestination.Replace("/", "\\");
            string capsuleFilename = Path.GetFileName(normalizedDestination);
            string capsuleSuffix = Path.GetExtension(capsuleFilename).ToLowerInvariant();

            if (capsuleSuffix != ".mod")
            {
                return;
            }

            string capsuleFolder = Path.GetDirectoryName(normalizedDestination) ?? ".";

            string filenameLower = capsuleFilename.ToLowerInvariant();
            string folderLower = capsuleFolder.ToLowerInvariant();

            bool alreadyPresent = modificationsByType.Install.Any(
                installFile => installFile.Destination.ToLowerInvariant() == folderLower &&
                              installFile.SaveAs.ToLowerInvariant() == filenameLower);

            if (!alreadyPresent)
            {
                var installEntry = new InstallFile(
                    capsuleFilename,
                    replaceExisting: true,
                    destination: capsuleFolder);
                modificationsByType.Install.Add(installEntry);
            }

            // Handle incremental writer
            if (incrementalWriter == null || alreadyPresent)
            {
                return;
            }

            // If capsule path exists as a file, use it directly
            if (!string.IsNullOrEmpty(capsulePath) && File.Exists(capsulePath))
            {
                incrementalWriter.AddInstallFile(capsuleFolder, capsuleFilename, capsulePath);
                logFunc($"    Staged module '{capsuleFilename}' for installation");
                return;
            }

            // Otherwise, add to install folder and create empty MOD if needed
            // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:314-319
            incrementalWriter.AddInstallFile(capsuleFolder, capsuleFilename, null);
            string outputPath = Path.Combine(incrementalWriter.TslpatchdataPath, capsuleFilename);
            if (!File.Exists(outputPath))
            {
                // Create empty MOD file
                var emptyMod = new ERF(ERFType.MOD);
                ERFAuto.WriteErf(emptyMod, outputPath, ResourceType.MOD);
                logFunc($"    Created empty module '{capsuleFilename}' in tslpatchdata");
            }
        }
    }
}
