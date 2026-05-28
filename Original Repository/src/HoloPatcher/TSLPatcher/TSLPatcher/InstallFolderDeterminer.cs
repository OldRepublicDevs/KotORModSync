// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:1212-1236
// Original: def determine_install_folders(...): ...
using System;
using System.Collections.Generic;
using System.Linq;
using KOTORModSync.Mods;
using KOTORModSync.Mods.GFF;
using KOTORModSync.Mods.SSF;
using KOTORModSync.Mods.TLK;
using KOTORModSync.Mods.TwoDA;
using KOTORModSync.Common;

namespace KOTORModSync.TSLPatcher
{
    // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:1212-1236
    // Original: def determine_install_folders(...): ...
    public static class InstallFolderDeterminer
    {
        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:1212-1236
        // Original: def determine_install_folders(...): ...
        public static List<InstallFile> DetermineInstallFolders(ModificationsByType modifications)
        {
            var installFolders = new Dictionary<string, List<string>>();

            // Process each modification type
            ProcessTlkModifications(modifications.Tlk, installFolders);
            Process2DaModifications(modifications.Twoda, installFolders);
            ProcessGffModifications(modifications.Gff, installFolders);
            ProcessSsfModifications(modifications.Ssf, installFolders);
            MergeExistingInstallFiles(modifications.Install, installFolders);

            // Convert dict to list of InstallFile objects
            var installFiles = new List<InstallFile>();

            foreach (var kvp in installFolders)
            {
                string folder = kvp.Key;
                foreach (string filename in kvp.Value)
                {
                    installFiles.Add(new InstallFile(filename, null, folder));
                }
            }

            return installFiles;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:1107-1122
        // Original: def _add_file_to_folder(...): ...
        private static void AddFileToFolder(Dictionary<string, List<string>> installFolders, string folder, string filename)
        {
            if (!installFolders.ContainsKey(folder))
            {
                installFolders[folder] = new List<string>();
            }
            if (!installFolders[folder].Contains(filename))
            {
                installFolders[folder].Add(filename);
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:1125-1149
        // Original: def _process_tlk_modifications(...): ...
        private static void ProcessTlkModifications(List<ModificationsTLK> modifications, Dictionary<string, List<string>> installFolders)
        {
            if (modifications == null)
            {
                return;
            }

            foreach (var modTlk in modifications)
            {
                string folder = "."; // TLK files go to game root

                // Only check for appends (TSLPatcher doesn't support replacements)
                bool hasAppends = modTlk.Modifiers != null && modTlk.Modifiers.Any(m => !m.IsReplacement);

                if (hasAppends)
                {
                    AddFileToFolder(installFolders, folder, "append.tlk");
                }
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:1152-1164
        // Original: def _process_2da_modifications(...): ...
        private static void Process2DaModifications(List<Modifications2DA> modifications, Dictionary<string, List<string>> installFolders)
        {
            if (modifications == null)
            {
                return;
            }

            foreach (var mod2da in modifications)
            {
                string folder = !string.IsNullOrEmpty(mod2da.Destination) ? mod2da.Destination : "Override";
                AddFileToFolder(installFolders, folder, mod2da.SourceFile);
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:1166-1180
        // Original: def _process_gff_modifications(...): ...
        private static void ProcessGffModifications(List<ModificationsGFF> modifications, Dictionary<string, List<string>> installFolders)
        {
            if (modifications == null)
            {
                return;
            }

            foreach (var modGff in modifications)
            {
                string folder = !string.IsNullOrEmpty(modGff.Destination) ? modGff.Destination : "Override";
                string filename = !string.IsNullOrEmpty(modGff.SaveAs) ? modGff.SaveAs : modGff.SourceFile;
                AddFileToFolder(installFolders, folder, filename);
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:1182-1194
        // Original: def _process_ssf_modifications(...): ...
        private static void ProcessSsfModifications(List<ModificationsSSF> modifications, Dictionary<string, List<string>> installFolders)
        {
            if (modifications == null)
            {
                return;
            }

            foreach (var modSsf in modifications)
            {
                string folder = !string.IsNullOrEmpty(modSsf.Destination) ? modSsf.Destination : "Override";
                AddFileToFolder(installFolders, folder, modSsf.SourceFile);
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:1196-1210
        // Original: def _merge_existing_install_files(...): ...
        private static void MergeExistingInstallFiles(List<InstallFile> existingInstall, Dictionary<string, List<string>> installFolders)
        {
            if (existingInstall == null)
            {
                return;
            }

            foreach (var installFile in existingInstall)
            {
                string folder = !string.IsNullOrEmpty(installFile.Destination) && installFile.Destination != "." 
                    ? installFile.Destination 
                    : "Override";
                string filename = !string.IsNullOrEmpty(installFile.SaveAs) 
                    ? installFile.SaveAs 
                    : installFile.SourceFile;
                AddFileToFolder(installFolders, folder, filename);
            }
        }
    }
}
