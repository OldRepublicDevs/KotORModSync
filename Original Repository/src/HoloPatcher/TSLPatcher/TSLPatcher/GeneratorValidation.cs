// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:975-1104
// Original: def _validate_ini_filename, _validate_installation_path, validate_tslpatchdata_arguments: ...
using System;
using System.Collections.Generic;
using System.IO;
using InstallationClass = KOTORModSync.Installation.Installation;
using KOTORModSync.Common;

namespace KOTORModSync.TSLPatcher
{
    /// <summary>
    /// Validation functions for TSLPatcher data generation.
    /// 1:1 port of validation functions from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:975-1104
    /// </summary>
    public static class GeneratorValidation
    {
        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:975-1009
        // Original: def _validate_ini_filename(ini: str | None) -> str:
        /// <summary>
        /// Validate and normalize INI filename.
        /// </summary>
        public static string ValidateIniFilename(string ini)
        {
            if (string.IsNullOrEmpty(ini))
            {
                return "changes.ini";
            }

            // Check for path separators
            if (ini.Contains("/") || ini.Contains("\\") || ini.Contains(Path.DirectorySeparatorChar.ToString()))
            {
                throw new ArgumentException($"--ini must be a filename only (not a path): {ini}");
            }

            // Ensure .ini extension
            if (ini.EndsWith(".ini", StringComparison.OrdinalIgnoreCase))
            {
                return ini;
            }

            // Check for other extensions
            if (ini.Contains("."))
            {
                string ext = Path.GetExtension(ini);
                if (!string.IsNullOrEmpty(ext) && !ext.Equals(".ini", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException($"--ini must have .ini extension (got {ext})");
                }
                // Has a dot but no extension, add .ini
                return ini.EndsWith(".") ? $"{ini}ini" : ini;
            }

            // No extension at all, add .ini
            return $"{ini}.ini";
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:1012-1031
        // Original: def _validate_installation_path(path: Path | None) -> bool:
        /// <summary>
        /// Check if path is a valid KOTOR installation.
        /// </summary>
        public static bool ValidateInstallationPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                return false;
            }

            try
            {
                var installation = new InstallationClass(path);
                // Installation constructor throws if game cannot be determined, so if we get here, it's valid
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"[DEBUG] Installation check failed for '{path}': {e.GetType().Name}: {e.Message}");
                return false;
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:1034-1104
        // Original: def validate_tslpatchdata_arguments(...):
        /// <summary>
        /// Validate --ini and --tslpatchdata arguments.
        /// </summary>
        public static (string validatedIni, DirectoryInfo tslpatchdataPath) ValidateTslpatchdataArguments(
            string ini,
            string tslpatchdata,
            List<object> filesAndFoldersAndInstallations)
        {
            // Early exit if neither argument provided
            if (string.IsNullOrEmpty(ini) && string.IsNullOrEmpty(tslpatchdata))
            {
                return (null, null);
            }

            // Validate argument combination
            if (!string.IsNullOrEmpty(ini) && string.IsNullOrEmpty(tslpatchdata))
            {
                throw new ArgumentException("--ini requires --tslpatchdata to be specified");
            }

            // Set defaults
            if (!string.IsNullOrEmpty(tslpatchdata) && string.IsNullOrEmpty(ini))
            {
                ini = "changes.ini";
            }

            // Should not happen but check anyway
            if (string.IsNullOrEmpty(tslpatchdata))
            {
                return (null, null);
            }

            // Validate and normalize INI filename
            string validatedIni = ValidateIniFilename(ini);

            // Validate at least one provided path is an Installation
            bool hasAnyInstall = false;
            if (filesAndFoldersAndInstallations != null)
            {
                foreach (object p in filesAndFoldersAndInstallations)
                {
                    // Already an Installation object
                    if (p is InstallationClass installation)
                    {
                        hasAnyInstall = true;
                        break;
                    }

                    // Try to verify by constructing an Installation from the path
                    if (p is string pathStr)
                    {
                        try
                        {
                            var _ = new InstallationClass(pathStr);
                            hasAnyInstall = true;
                            break;
                        }
                        catch (Exception)
                        {
                            // Fall back to determine_game heuristic
                            if (ValidateInstallationPath(pathStr))
                            {
                                hasAnyInstall = true;
                                break;
                            }
                        }
                    }
                    else if (p is DirectoryInfo dirInfo)
                    {
                        try
                        {
                            var _ = new InstallationClass(dirInfo.FullName);
                            hasAnyInstall = true;
                            break;
                        }
                        catch (Exception)
                        {
                            if (ValidateInstallationPath(dirInfo.FullName))
                            {
                                hasAnyInstall = true;
                                break;
                            }
                        }
                    }
                }
            }

            if (!hasAnyInstall)
            {
                throw new ArgumentException("--tslpatchdata requires at least one provided path to be a valid KOTOR Installation");
            }

            // Normalize tslpatchdata path
            DirectoryInfo tslpatchdataPath = new DirectoryInfo(Path.GetFullPath(tslpatchdata));
            if (!tslpatchdataPath.Name.Equals("tslpatchdata", StringComparison.OrdinalIgnoreCase))
            {
                tslpatchdataPath = new DirectoryInfo(Path.Combine(tslpatchdataPath.FullName, "tslpatchdata"));
            }

            return (validatedIni, tslpatchdataPath);
        }
    }
}
