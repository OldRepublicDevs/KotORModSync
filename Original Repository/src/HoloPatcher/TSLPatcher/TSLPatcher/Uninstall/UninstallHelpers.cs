using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KOTORModSync;
using KOTORModSync.Formats.TLK;
using KOTORModSync.Common;

namespace KOTORModSync.Uninstall
{

    /// <summary>
    /// Helper functions for uninstalling mods.
    /// 1:1 port from Python uninstall.py
    /// </summary>
    public static class UninstallHelpers
    {
        /// <summary>
        /// List of base filenames (without extension) for Aspyr patch files that must be preserved in the override folder.
        /// These files are required by the Aspyr patch and should not be deleted during uninstall operations.
        /// Based on PyKotor's ASPYR_CONTROLLER_BUTTON_TEXTURES list from txi_data.py.
        /// </summary>
        private static readonly HashSet<string> AspyrPatchFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cus_button_a",
            "cus_button_aps",
            "cus_button_b",
            "cus_button_bps",
            "cus_button_x",
            "cus_button_xps",
            "cus_button_y",
            "cus_button_yps"
        };

        /// <summary>
        /// Common texture file extensions that may be associated with Aspyr patch files.
        /// These extensions are checked when determining if a file is an Aspyr patch file.
        /// </summary>
        private static readonly HashSet<string> TextureExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".tpc",  // TPC texture format (primary texture format in KOTOR)
            ".txi",  // TXI texture info files (metadata for textures)
            ".tga",  // TGA texture format (alternative format)
            ".dds"   // DDS texture format (DirectDraw Surface, sometimes used)
        };

        /// <summary>
        /// Uninstalls all mods from the game.
        /// 1:1 port from Python uninstall_all_mods
        ///
        /// What this method really does is delete all the contents of the override folder and delete all .MOD files from
        /// the modules folder. Then it removes all appended TLK entries using
        /// the hardcoded number of entries depending on the game. There are 49,265 TLK entries in KOTOR 1, and 136,329 in TSL.
        ///
        /// The Aspyr patch contains required files in the override folder (controller button textures) which are
        /// preserved during uninstall to prevent breaking the game installation.
        ///
        /// TODO: With the new Replace TLK syntax, the above TLK reinstall isn't possible anymore.
        /// Here, we should write the dialog.tlk and then check it's sha1 hash compared to vanilla.
        /// We could keep the vanilla TLK entries in a tlkdefs file, similar to our nwscript.nss defs.
        /// This implementation would be required regardless in K2 anyway as this function currently isn't determining if the Aspyr patch and/or TSLRCM is installed.
        /// </summary>
        /// <param name="gamePath">The path to the game installation directory</param>
        public static void UninstallAllMods(string gamePath)
        {
            Game game = Installation.Installation.DetermineGame(gamePath)
                       ?? throw new ArgumentException($"Unable to determine game type at path: {gamePath}");

            string overridePath = Installation.Installation.GetOverridePath(gamePath);
            string modulesPath = Installation.Installation.GetModulesPath(gamePath);

            // Remove any TLK changes
            string dialogTlkPath = Path.Combine(gamePath, "dialog.tlk");
            if (File.Exists(dialogTlkPath))
            {
                TLK dialogTlk = new TLKBinaryReader(File.ReadAllBytes(dialogTlkPath)).Load();

                // Trim TLK entries based on game type
                int maxEntries = game == Game.K1 ? 49265 : 136329;
                if (dialogTlk.Entries.Count > maxEntries)
                {
                    dialogTlk.Entries = dialogTlk.Entries.Take(maxEntries).ToList();
                }

                var writer = new TLKBinaryWriter(dialogTlk);
                File.WriteAllBytes(dialogTlkPath, writer.Write());
            }

            // Remove all override files, except Aspyr patch files
            if (Directory.Exists(overridePath))
            {
                foreach (string filePath in Directory.GetFiles(overridePath))
                {
                    // Skip Aspyr patch files - these are required and must be preserved
                    if (IsAspyrPatchFile(filePath))
                    {
                        continue;
                    }

                    try
                    {
                        File.Delete(filePath);
                    }
                    catch (Exception)
                    {
                        // Log or handle deletion errors if needed
                    }
                }
            }

            // Remove any .MOD files
            if (Directory.Exists(modulesPath))
            {
                foreach (string filePath in Directory.GetFiles(modulesPath))
                {
                    if (IsModFile(Path.GetFileName(filePath)))
                    {
                        try
                        {
                            File.Delete(filePath);
                        }
                        catch (Exception)
                        {
                            // Log or handle deletion errors if needed
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks if a filename represents a .MOD file.
        /// </summary>
        /// <param name="filename">The filename to check</param>
        /// <returns>True if the file is a .MOD file, False otherwise</returns>
        private static bool IsModFile(string filename)
        {
            return filename.EndsWith(".mod", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines if a file path points to an Aspyr patch file that should be preserved during uninstall.
        /// Aspyr patch files are controller button textures required by the Aspyr patch for proper game functionality.
        /// </summary>
        /// <param name="filePath">The full path to the file to check</param>
        /// <returns>True if the file is an Aspyr patch file that should be preserved, False otherwise</returns>
        private static bool IsAspyrPatchFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            string fileName = Path.GetFileName(filePath);
            if (string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            // Get the base filename without extension
            string baseName = Path.GetFileNameWithoutExtension(fileName);

            // Check if the base name matches any Aspyr patch file
            if (!AspyrPatchFiles.Contains(baseName))
            {
                return false;
            }

            // Verify the extension is a valid texture extension
            // This ensures we only preserve actual texture files, not accidentally named files
            string extension = Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(extension))
            {
                // If no extension, check if a .tpc file exists (TPC files can have embedded TXI)
                // For safety, we'll preserve files without extensions if the base name matches
                return true;
            }

            return TextureExtensions.Contains(extension);
        }
    }
}
