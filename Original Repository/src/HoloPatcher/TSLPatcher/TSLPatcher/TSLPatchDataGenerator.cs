// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:68-129
// Original: class TSLPatchDataGenerator: ...
using System;
using System.Numerics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KOTORModSync.Mods;
using KOTORModSync.Mods.GFF;
using KOTORModSync.Mods.SSF;
using KOTORModSync.Mods.TLK;
using KOTORModSync.Mods.TwoDA;
using KOTORModSync.Memory;
using KOTORModSync.Formats.GFF;
using KOTORModSync.Formats.TwoDA;
using KOTORModSync.Formats.TLK;
using KOTORModSync.Formats.SSF;
using KOTORModSync.Resource;
using KOTORModSync;
using KOTORModSync.Formats.LIP;
using GFFContent = KOTORModSync.Formats.GFF.GFFContent;
using TLKAuto = KOTORModSync.Formats.TLK.TLKAuto;
using TwoDAAuto = KOTORModSync.Formats.TwoDA.TwoDAAuto;
using GFFAuto = KOTORModSync.Formats.GFF.GFFAuto;
using SSFAuto = KOTORModSync.Formats.SSF.SSFAuto;
using LIPAuto = KOTORModSync.Formats.LIP.LIPAuto;
using KOTORModSync.Common;

namespace KOTORModSync.TSLPatcher
{
    // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:68-129
    // Original: class TSLPatchDataGenerator: ...
    public class TSLPatchDataGenerator
    {
        private readonly DirectoryInfo _tslpatchdataPath;

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:71-79
        // Original: def __init__(self, tslpatchdata_path: Path): ...
        public TSLPatchDataGenerator(DirectoryInfo tslpatchdataPath)
        {
            _tslpatchdataPath = tslpatchdataPath;
            if (!_tslpatchdataPath.Exists)
            {
                _tslpatchdataPath.Create();
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:81-129
        // Original: def generate_all_files(...): ...
        public Dictionary<string, FileInfo> GenerateAllFiles(
            ModificationsByType modifications,
            DirectoryInfo baseDataPath = null)
        {
            var generatedFiles = new Dictionary<string, FileInfo>();

            // Generate TLK files
            if (modifications.Tlk != null && modifications.Tlk.Count > 0)
            {
                var tlkFiles = GenerateTlkFiles(modifications.Tlk, baseDataPath);
                foreach (var kvp in tlkFiles)
                {
                    generatedFiles[kvp.Key] = kvp.Value;
                }
            }

            // Generate 2DA files
            if (modifications.Twoda != null && modifications.Twoda.Count > 0)
            {
                var twodaFiles = Generate2DaFiles(modifications.Twoda, baseDataPath);
                foreach (var kvp in twodaFiles)
                {
                    generatedFiles[kvp.Key] = kvp.Value;
                }
            }

            // Generate GFF files
            if (modifications.Gff != null && modifications.Gff.Count > 0)
            {
                var gffFiles = GenerateGffFiles(modifications.Gff, baseDataPath);
                foreach (var kvp in gffFiles)
                {
                    generatedFiles[kvp.Key] = kvp.Value;
                }
            }

            // Generate SSF files
            if (modifications.Ssf != null && modifications.Ssf.Count > 0)
            {
                var ssfFiles = GenerateSsfFiles(modifications.Ssf, baseDataPath);
                foreach (var kvp in ssfFiles)
                {
                    generatedFiles[kvp.Key] = kvp.Value;
                }
            }

            // Copy missing files from install folders if base_data_path provided
            if (modifications.Install != null && modifications.Install.Count > 0 && baseDataPath != null)
            {
                var installFiles = CopyInstallFiles(modifications.Install, baseDataPath);
                foreach (var kvp in installFiles)
                {
                    generatedFiles[kvp.Key] = kvp.Value;
                }
            }

            return generatedFiles;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:289-338
        // Original: def _generate_tlk_files(...): ...
        private Dictionary<string, FileInfo> GenerateTlkFiles(
            List<ModificationsTLK> tlkModifications,
            DirectoryInfo baseDataPath)
        {
            var generated = new Dictionary<string, FileInfo>();

            if (tlkModifications == null || tlkModifications.Count == 0)
            {
                return generated;
            }

            foreach (var modTlk in tlkModifications)
            {
                // All modifiers should be appends (no replacements per TSLPatcher design)
                var appends = modTlk.Modifiers.Where(m => !m.IsReplacement).ToList();

                // Warn if any replacements are found
                var replacements = modTlk.Modifiers.Where(m => m.IsReplacement).ToList();
                if (replacements.Count > 0)
                {
                    Console.WriteLine($"[WARNING] Found {replacements.Count} replacement modifiers in TLK - TSLPatcher only supports appends!");
                }

                // Create append.tlk with all modifiers
                if (appends.Count > 0)
                {
                    var appendTlk = new TLK();
                    appendTlk.Resize(appends.Count);

                    // Sort by token_id (which is the index in append.tlk)
                    var sortedAppends = appends.OrderBy(m => m.TokenId).ToList();

                    for (int appendIdx = 0; appendIdx < sortedAppends.Count; appendIdx++)
                    {
                        var modifier = sortedAppends[appendIdx];
                        string text = modifier.Text ?? "";
                        string soundStr = modifier.Sound ?? "";
                        appendTlk.Replace(appendIdx, text, soundStr);
                    }

                    var appendPath = new FileInfo(Path.Combine(_tslpatchdataPath.FullName, "append.tlk"));
                    TLKAuto.WriteTlk(appendTlk, appendPath.FullName, ResourceType.TLK);
                    generated["append.tlk"] = appendPath;
                }
            }

            return generated;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:340-391
        // Original: def _generate_2da_files(...): ...
        private Dictionary<string, FileInfo> Generate2DaFiles(
            List<Modifications2DA> twodaModifications,
            DirectoryInfo baseDataPath)
        {
            var generated = new Dictionary<string, FileInfo>();

            if (twodaModifications == null || twodaModifications.Count == 0)
            {
                return generated;
            }

            foreach (var mod2da in twodaModifications)
            {
                string filename = mod2da.SourceFile;
                var outputPath = new FileInfo(Path.Combine(_tslpatchdataPath.FullName, filename));

                // Try to load base 2DA file from baseDataPath
                if (baseDataPath != null)
                {
                    // Try Override first, then other locations
                    var potentialPaths = new[]
                    {
                        new FileInfo(Path.Combine(baseDataPath.FullName, "Override", filename)),
                        new FileInfo(Path.Combine(baseDataPath.FullName, filename))
                    };

                    bool found = false;
                    foreach (var potentialPath in potentialPaths)
                    {
                        if (potentialPath.Exists)
                        {
                            try
                            {
                                // Copy using TwoDA reader/writer to ensure proper format
                                var twodaObj = new TwoDABinaryReader(potentialPath.FullName).Load();
                                TwoDAAuto.WriteTwoDA(twodaObj, outputPath.FullName, ResourceType.TwoDA);
                                generated[filename] = outputPath;
                                found = true;
                                break;
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"[ERROR] Failed to read base 2DA {potentialPath.FullName}: {e.Message}");
                                continue;
                            }
                        }
                    }

                    if (!found)
                    {
                        Console.WriteLine($"[WARNING] Could not find base 2DA file for {filename} - TSLPatcher may fail without it");
                    }
                }
            }

            return generated;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:393-450
        // Original: def _generate_gff_files(...): ...
        private Dictionary<string, FileInfo> GenerateGffFiles(
            List<ModificationsGFF> gffModifications,
            DirectoryInfo baseDataPath)
        {
            var generated = new Dictionary<string, FileInfo>();

            if (gffModifications == null || gffModifications.Count == 0)
            {
                return generated;
            }

            foreach (var modGff in gffModifications)
            {
                bool replaceFile = modGff.ReplaceFile;

                // Get the actual filename (might be different from sourcefile)
                string filename = !string.IsNullOrEmpty(modGff.SaveAs) ? modGff.SaveAs : modGff.SourceFile;

                // CRITICAL: ALL files go directly in tslpatchdata root, NOT in subdirectories
                var outputPath = new FileInfo(Path.Combine(_tslpatchdataPath.FullName, filename));

                // Try to load base file if baseDataPath provided, otherwise create new
                GFF baseGff = LoadOrCreateGff(baseDataPath, filename);

                // For replace operations, apply modifications
                // For patch operations, just copy the base file as-is
                if (replaceFile)
                {
                    // Apply all modifications to generate the replacement file
                    ApplyGffModifications(baseGff, modGff.Modifiers);
                }

                // Write the GFF file
                GFFAuto.WriteGff(baseGff, outputPath.FullName, ResourceType.GFF);
                generated[filename] = outputPath;
            }

            return generated;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:452-488
        // Original: def _load_or_create_gff(...): ...
        private GFF LoadOrCreateGff(DirectoryInfo baseDataPath, string filename)
        {
            // Try to load base file if baseDataPath provided
            if (baseDataPath != null)
            {
                var potentialBase = new FileInfo(Path.Combine(baseDataPath.FullName, filename));
                if (potentialBase.Exists)
                {
                    try
                    {
                        var baseGff = new GFFBinaryReader(potentialBase.FullName).Load();
                        return baseGff;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"[DEBUG] Could not load base GFF {potentialBase.FullName}: {e.GetType().Name}: {e.Message}");
                    }
                }
            }

            // Create new GFF structure
            string ext = Path.GetExtension(filename).TrimStart('.').ToUpperInvariant();
            GFFContent gffContent;
            try
            {
                if (Enum.TryParse<GFFContent>(ext, out GFFContent parsedContent))
                {
                    gffContent = parsedContent;
                }
                else
                {
                    gffContent = GFFContent.GFF;
                }
            }
            catch
            {
                gffContent = GFFContent.GFF;
            }

            return new GFF(gffContent);
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:911-972
        // Original: def _generate_ssf_files(...): ...
        private Dictionary<string, FileInfo> GenerateSsfFiles(
            List<ModificationsSSF> ssfModifications,
            DirectoryInfo baseDataPath)
        {
            var generated = new Dictionary<string, FileInfo>();

            if (ssfModifications == null || ssfModifications.Count == 0)
            {
                return generated;
            }

            foreach (var modSsf in ssfModifications)
            {
                bool replaceFile = modSsf.ReplaceFile;

                // Create new SSF or load base
                SSF ssf = null;
                if (baseDataPath != null)
                {
                    var potentialBase = new FileInfo(Path.Combine(baseDataPath.FullName, modSsf.SourceFile));
                    if (potentialBase.Exists)
                    {
                        try
                        {
                            ssf = new SSFBinaryReader(potentialBase.FullName).Load();
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"[DEBUG] Could not load base SSF '{potentialBase.FullName}': {e.GetType().Name}: {e.Message}");
                        }
                    }
                }

                if (ssf == null)
                {
                    ssf = new SSF();
                }

                // For replace operations, apply modifications
                // For patch operations, just copy the base file as-is
                if (replaceFile)
                {
                    // Apply modifications to generate the replacement file
                    // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:131-231
                    // Original: for modifier in mod_ssf.modifiers: modifier.apply(ssf, memory)
                    // Note: During generation, we create an empty PatcherMemory since tokens may not be resolved yet.
                    // The actual token resolution happens during patch application, not during generation.
                    PatcherMemory memory = new PatcherMemory();
                    foreach (var modifier in modSsf.Modifiers)
                    {
                        if (modifier is KOTORModSync.Mods.SSF.ModifySSF modifySsf)
                        {
                            try
                            {
                                // Resolve StrRef token value using the same pattern as ModifySSF.Apply
                                string strRefValue = modifySsf.Stringref.Value(memory);
                                int strRefInt = int.Parse(strRefValue);
                                ssf.SetData(modifySsf.Sound, strRefInt);
                            }
                            catch (KeyNotFoundException ex)
                            {
                                // Token not yet resolved - this is expected during generation
                                Console.WriteLine($"[DEBUG] StrRef token not resolved during generation: {ex.Message}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[DEBUG] Failed to apply SSF modification: {ex.GetType().Name}: {ex.Message}");
                            }
                        }
                    }
                }

                // Write SSF file
                var outputPath = new FileInfo(Path.Combine(_tslpatchdataPath.FullName, modSsf.SourceFile));
                SSFAuto.WriteSsf(ssf, outputPath.FullName, ResourceType.SSF);
                generated[modSsf.SourceFile] = outputPath;
            }

            return generated;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:131-231
        // Original: def _copy_install_files(...): ...
        private Dictionary<string, FileInfo> CopyInstallFiles(
            List<InstallFile> installFiles,
            DirectoryInfo baseDataPath)
        {
            var copiedFiles = new Dictionary<string, FileInfo>();

            // Group files by folder for efficient processing
            var filesByFolder = new Dictionary<string, List<string>>();
            foreach (var installFile in installFiles)
            {
                string folder = !string.IsNullOrEmpty(installFile.Destination) && installFile.Destination != "."
                    ? installFile.Destination
                    : "Override";
                string filename = !string.IsNullOrEmpty(installFile.SaveAs)
                    ? installFile.SaveAs
                    : installFile.SourceFile;

                if (!filesByFolder.ContainsKey(folder))
                {
                    filesByFolder[folder] = new List<string>();
                }
                filesByFolder[folder].Add(filename);
            }

            foreach (var kvp in filesByFolder)
            {
                string folder = kvp.Key;
                var filenames = kvp.Value;
                var sourceFolder = new DirectoryInfo(Path.Combine(baseDataPath.FullName, folder));

                foreach (string filename in filenames)
                {
                    var sourceFile = new FileInfo(Path.Combine(sourceFolder.FullName, filename));
                    var destFile = new FileInfo(Path.Combine(_tslpatchdataPath.FullName, filename));

                    // Handle module capsules specially
                    if (folder == "modules")
                    {
                        if (sourceFile.Exists)
                        {
                            try
                            {
                                byte[] sourceData = File.ReadAllBytes(sourceFile.FullName);
                                WriteResourceWithIo(sourceData, destFile.FullName, Path.GetExtension(filename).TrimStart('.').ToLowerInvariant());
                                copiedFiles[filename] = destFile;
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"[ERROR] Failed to copy module capsule {filename}: {e.Message}");
                            }
                        }
                        continue;
                    }

                    // For module-specific resources, extract from capsule
                    if (folder.StartsWith("modules\\") || folder.StartsWith("modules/"))
                    {
                        // Extract from module capsule
                        string[] folderParts = folder.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                        if (folderParts.Length >= 2)
                        {
                            string moduleName = folderParts[1];
                            var modulePath = new FileInfo(Path.Combine(baseDataPath.FullName, "modules", moduleName));

                            if (modulePath.Exists)
                            {
                                try
                                {
                                    // TODO: Extract from capsule - requires Capsule implementation
                                    // For now, skip module resource extraction
                                    Console.WriteLine($"[DEBUG] Module resource extraction not yet implemented: {filename} from {moduleName}");
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine($"[ERROR] Failed to extract {filename} from {moduleName}: {e.Message}");
                                }
                            }
                        }
                        continue;
                    }

                    // For regular files, copy using appropriate method
                    if (sourceFile.Exists)
                    {
                        try
                        {
                            string fileExt = Path.GetExtension(filename).TrimStart('.').ToLowerInvariant();
                            byte[] sourceData = File.ReadAllBytes(sourceFile.FullName);
                            WriteResourceWithIo(sourceData, destFile.FullName, fileExt);
                            copiedFiles[filename] = destFile;
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"[ERROR] Failed to copy {filename}: {e.Message}");
                        }
                    }
                }
            }

            return copiedFiles;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:233-287
        // Original: def _write_resource_with_io(...): ...
        private void WriteResourceWithIo(byte[] data, string destPath, string fileExt)
        {
            try
            {
                // Use appropriate io function based on file type
                var gffExtensions = GetGffExtensions();
                if (gffExtensions.Contains(fileExt.ToUpperInvariant()))
                {
                    // GFF-based format - use io_gff
                    var gffObj = new GFFBinaryReader(data).Load();
                    ResourceType resourceType = ResourceType.FromExtension(fileExt);
                    GFFAuto.WriteGff(gffObj, destPath, resourceType);
                }
                else if (fileExt == "2da")
                {
                    // 2DA file - use io_2da
                    var twodaObj = new TwoDABinaryReader(data).Load();
                    TwoDAAuto.WriteTwoDA(twodaObj, destPath, ResourceType.TwoDA);
                }
                else if (fileExt == "tlk")
                {
                    // TLK file - use io_tlk
                    var tlkObj = new TLKBinaryReader(data).Load();
                    TLKAuto.WriteTlk(tlkObj, destPath, ResourceType.TLK);
                }
                else if (fileExt == "ssf")
                {
                    // SSF file - use io_ssf
                    var ssfObj = new SSFBinaryReader(data).Load();
                    SSFAuto.WriteSsf(ssfObj, destPath, ResourceType.SSF);
                }
                else if (fileExt == "lip")
                {
                    // LIP file - use io_lip
                    var lipObj = new LIPBinaryReader(data).Load();
                    LIPAuto.WriteLip(lipObj, destPath, ResourceType.LIP);
                }
                else
                {
                    // For other formats (NCS, MDL, MDX, WAV, TGA, BIK, etc.), write as binary
                    File.WriteAllBytes(destPath, data);
                }
            }
            catch (Exception e)
            {
                // If parsing fails, fall back to binary write
                Console.WriteLine($"[ERROR] Failed to use io function for {fileExt}, falling back to binary write: {e.Message}");
                File.WriteAllBytes(destPath, data);
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:490-508
        // Original: def _apply_gff_modifications(...): ...
        private void ApplyGffModifications(GFF gff, List<ModifyGFF> modifiers)
        {
            foreach (var modifier in modifiers)
            {
                if (modifier is ModifyFieldGFF modifyField)
                {
                    ApplyModifyField(gff.Root, modifyField);
                }
                else if (modifier is AddFieldGFF addField)
                {
                    ApplyAddField(gff.Root, addField);
                }
                else if (modifier is AddStructToListGFF addStructToList)
                {
                    ApplyAddStructToList(gff.Root, addStructToList);
                }
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:509-551
        // Original: def _navigate_to_parent_struct(...): ...
        private GFFStruct NavigateToParentStruct(GFFStruct rootStruct, List<string> pathParts, string context = "modification")
        {
            GFFStruct currentStruct = rootStruct;

            for (int i = 0; i < pathParts.Count - 1; i++)
            {
                string part = pathParts[i];
                if (currentStruct == null)
                {
                    Console.WriteLine($"[ERROR] Cannot navigate {context}: current_struct is None at '{part}'");
                    return null;
                }

                if (int.TryParse(part, out int listIndex))
                {
                    // List index navigation - this shouldn't happen in parent navigation
                    Console.WriteLine($"[ERROR] Path part '{part}' expects list but got GFFStruct during {context}");
                    return null;
                }
                else
                {
                    // Struct field navigation
                    GFFStruct nestedStruct = currentStruct.GetStruct(part);
                    if (nestedStruct == null)
                    {
                        Console.WriteLine($"[ERROR] Cannot navigate to struct field '{part}' during {context} - field missing or wrong type");
                        return null;
                    }
                    currentStruct = nestedStruct;
                }
            }

            return currentStruct;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:553-574
        // Original: def _apply_modify_field(...): ...
        private void ApplyModifyField(GFFStruct rootStruct, ModifyFieldGFF modifier)
        {
            // Navigate to the correct struct
            var pathParts = modifier.Path.Replace("\\", "/").Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            GFFStruct currentStruct = NavigateToParentStruct(rootStruct, pathParts, $"ModifyField: {modifier.Path}");

            if (currentStruct == null)
            {
                return;
            }

            // Set the field value
            string fieldLabel = pathParts[pathParts.Count - 1];
            object value = ExtractFieldValue(modifier.Value);
            SetFieldValue(currentStruct, fieldLabel, value);
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:576-600
        // Original: def _set_field_value(...): ...
        private void SetFieldValue(GFFStruct gffStruct, string fieldLabel, object value)
        {
            if (value is int intValue)
            {
                gffStruct.SetUInt32(fieldLabel, (uint)intValue);
            }
            else if (value is float floatValue)
            {
                gffStruct.SetSingle(fieldLabel, floatValue);
            }
            else if (value is string stringValue)
            {
                gffStruct.SetString(fieldLabel, stringValue);
            }
            else if (value is ResRef resRefValue)
            {
                gffStruct.SetResRef(fieldLabel, resRefValue);
            }
            else if (value is LocalizedString locStringValue)
            {
                gffStruct.SetLocString(fieldLabel, locStringValue);
            }
            else if (value is Vector3 vector3Value)
            {
                gffStruct.SetVector3(fieldLabel, new System.Numerics.Vector3(vector3Value.X, vector3Value.Y, vector3Value.Z));
            }
            else if (value is Vector4 vector4Value)
            {
                gffStruct.SetVector4(fieldLabel, new System.Numerics.Vector4(vector4Value.X, vector4Value.Y, vector4Value.Z, vector4Value.W));
            }
            else
            {
                Console.WriteLine($"[ERROR] Unknown value type for field '{fieldLabel}': {value?.GetType().Name ?? "null"}");
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:602-644
        // Original: def _navigate_to_struct_creating_if_needed(...): ...
        private GFFStruct NavigateToStructCreatingIfNeeded(GFFStruct rootStruct, List<string> pathParts, string context = "AddField")
        {
            GFFStruct currentStruct = rootStruct;

            foreach (string part in pathParts)
            {
                if (string.IsNullOrWhiteSpace(part))
                {
                    continue;
                }

                if (int.TryParse(part, out int listIndex))
                {
                    // List index navigation - not supported in this context
                    Console.WriteLine($"[ERROR] Expected struct at '{part}' but got list index in {context}");
                    return null;
                }
                else
                {
                    // Struct field - check if exists, create if not
                    if (currentStruct.Exists(part))
                    {
                        GFFStruct nestedStruct = currentStruct.GetStruct(part);
                        if (nestedStruct == null)
                        {
                            Console.WriteLine($"[ERROR] Field '{part}' exists but is not a struct in {context}");
                            return null;
                        }
                        currentStruct = nestedStruct;
                    }
                    else
                    {
                        // Create new struct if field doesn't exist
                        var newStruct = new GFFStruct();
                        currentStruct.SetStruct(part, newStruct);
                        currentStruct = newStruct;
                    }
                }
            }

            return currentStruct;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:646-673
        // Original: def _apply_nested_modifiers(...): ...
        private void ApplyNestedModifiers(GFFStruct currentStruct, AddFieldGFF modifier)
        {
            if (modifier.Modifiers == null || modifier.Modifiers.Count == 0)
            {
                return;
            }

            if (modifier.FieldType == GFFFieldType.Struct)
            {
                GFFStruct nestedStruct = currentStruct.GetStruct(modifier.Label);
                if (nestedStruct == null)
                {
                    Console.WriteLine($"[ERROR] Cannot apply nested modifiers: struct field '{modifier.Label}' not found after creation");
                    return;
                }
                foreach (var nestedMod in modifier.Modifiers)
                {
                    if (nestedMod is AddFieldGFF addField)
                    {
                        ApplyAddField(nestedStruct, addField);
                    }
                    else if (nestedMod is AddStructToListGFF)
                    {
                        Console.WriteLine($"[ERROR] Unexpected AddStructToListGFF in Struct context for '{modifier.Label}'");
                    }
                }
            }
            else if (modifier.FieldType == GFFFieldType.List)
            {
                // Lists handle their own nested modifiers via AddStructToListGFF
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:675-699
        // Original: def _apply_add_field(...): ...
        private void ApplyAddField(GFFStruct rootStruct, AddFieldGFF modifier)
        {
            // Navigate to parent struct, creating intermediate structs as needed
            var pathParts = !string.IsNullOrEmpty(modifier.Path)
                ? modifier.Path.Replace("\\", "/").Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).ToList()
                : new List<string>();
            GFFStruct currentStruct = NavigateToStructCreatingIfNeeded(rootStruct, pathParts, $"AddField: {modifier.Label}");

            if (currentStruct == null)
            {
                return;
            }

            // Add the field to the struct
            object value = ExtractFieldValue(modifier.Value);
            SetFieldByType(currentStruct, modifier.Label, modifier.FieldType, value);

            // Recursively apply nested modifiers if present
            if (modifier.FieldType == GFFFieldType.Struct || modifier.FieldType == GFFFieldType.List)
            {
                ApplyNestedModifiers(currentStruct, modifier);
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:849-891
        // Original: def _set_field_by_type(...): ...
        private void SetFieldByType(GFFStruct gffStruct, string label, GFFFieldType fieldType, object value)
        {
            try
            {
                switch (fieldType)
                {
                    case GFFFieldType.UInt8:
                        gffStruct.SetUInt8(label, Convert.ToByte(value));
                        break;
                    case GFFFieldType.Int8:
                        gffStruct.SetInt8(label, Convert.ToSByte(value));
                        break;
                    case GFFFieldType.UInt16:
                        gffStruct.SetUInt16(label, Convert.ToUInt16(value));
                        break;
                    case GFFFieldType.Int16:
                        gffStruct.SetInt16(label, Convert.ToInt16(value));
                        break;
                    case GFFFieldType.UInt32:
                        gffStruct.SetUInt32(label, Convert.ToUInt32(value));
                        break;
                    case GFFFieldType.Int32:
                        gffStruct.SetInt32(label, Convert.ToInt32(value));
                        break;
                    case GFFFieldType.UInt64:
                        gffStruct.SetUInt64(label, Convert.ToUInt64(value));
                        break;
                    case GFFFieldType.Int64:
                        gffStruct.SetInt64(label, Convert.ToInt64(value));
                        break;
                    case GFFFieldType.Single:
                        gffStruct.SetSingle(label, Convert.ToSingle(value));
                        break;
                    case GFFFieldType.Double:
                        gffStruct.SetDouble(label, Convert.ToDouble(value));
                        break;
                    case GFFFieldType.String:
                        gffStruct.SetString(label, value?.ToString() ?? "");
                        break;
                    case GFFFieldType.ResRef:
                        if (value is ResRef resRef)
                        {
                            gffStruct.SetResRef(label, resRef);
                        }
                        else
                        {
                            string resRefStr = value?.ToString() ?? "";
                            gffStruct.SetResRef(label, string.IsNullOrEmpty(resRefStr) ? ResRef.FromBlank() : new ResRef(resRefStr));
                        }
                        break;
                    case GFFFieldType.LocalizedString:
                        if (value is LocalizedString locString)
                        {
                            gffStruct.SetLocString(label, locString);
                        }
                        break;
                    case GFFFieldType.Vector3:
                        if (value is Vector3 v3)
                        {
                            gffStruct.SetVector3(label, new System.Numerics.Vector3(v3.X, v3.Y, v3.Z));
                        }
                        break;
                    case GFFFieldType.Vector4:
                        if (value is Vector4 v4)
                        {
                            gffStruct.SetVector4(label, v4);
                        }
                        break;
                    case GFFFieldType.Struct:
                        if (value is GFFStruct structValue)
                        {
                            gffStruct.SetStruct(label, structValue);
                        }
                        break;
                    case GFFFieldType.List:
                        if (value is GFFList listValue)
                        {
                            gffStruct.SetList(label, listValue);
                        }
                        else
                        {
                            gffStruct.SetList(label, new GFFList());
                        }
                        break;
                    default:
                        Console.WriteLine($"[DEBUG] No setter for field type: {fieldType}");
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[DEBUG] Error setting field {label} with type {fieldType}: {e.Message}");
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:893-909
        // Original: def _extract_field_value(...): ...
        private object ExtractFieldValue(FieldValue fieldValue)
        {
            if (fieldValue is FieldValueConstant constant)
            {
                // FieldValueConstant has a Stored property
                var storedProperty = constant.GetType().GetProperty("Stored");
                if (storedProperty != null)
                {
                    return storedProperty.GetValue(constant);
                }
            }

            // If it's a FieldValue with a Value method, call it with null memory
            var valueMethod = fieldValue.GetType().GetMethod("Value");
            if (valueMethod != null)
            {
                try
                {
                    return valueMethod.Invoke(fieldValue, new object[] { null, null });
                }
                catch
                {
                    // Fall through
                }
            }

            return fieldValue;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:701-750
        // Original: def _navigate_to_list_creating_if_needed(...): ...
        private GFFList NavigateToListCreatingIfNeeded(GFFStruct rootStruct, List<string> pathParts, string context = "AddStructToList")
        {
            object currentObj = rootStruct;

            foreach (string part in pathParts)
            {
                if (string.IsNullOrWhiteSpace(part))
                {
                    continue;
                }

                if (int.TryParse(part, out int listIndex))
                {
                    // List index navigation
                    if (!(currentObj is GFFList currentList))
                    {
                        Console.WriteLine($"[ERROR] Expected list at index '{part}' but got {currentObj.GetType().Name} in {context}");
                        return null;
                    }
                    GFFStruct item = currentList.At(listIndex);
                    if (item == null)
                    {
                        Console.WriteLine($"[ERROR] List index {part} out of bounds in {context}");
                        return null;
                    }
                    currentObj = item;
                }
                else if (currentObj is GFFStruct currentStruct)
                {
                    object navigatedObj = NavigateStructFieldToList(currentStruct, part, context);
                    if (navigatedObj == null)
                    {
                        return null;
                    }
                    currentObj = navigatedObj;
                }
                else
                {
                    Console.WriteLine($"[ERROR] Cannot navigate from {currentObj.GetType().Name} at '{part}' in {context}");
                    return null;
                }
            }

            // Verify final object is a list
            if (!(currentObj is GFFList resultList))
            {
                Console.WriteLine($"[ERROR] Path '{string.Join("/", pathParts)}' did not resolve to GFFList in {context}, got {currentObj.GetType().Name}");
                return null;
            }

            return resultList;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:752-789
        // Original: def _navigate_struct_field_to_list(...): ...
        private object NavigateStructFieldToList(GFFStruct currentStruct, string fieldName, string context)
        {
            if (!currentStruct.Exists(fieldName))
            {
                // Field doesn't exist - create new list
                var newList = new GFFList();
                currentStruct.SetList(fieldName, newList);
                return newList;
            }

            // Field exists - navigate to it
            GFFFieldType? fieldType = currentStruct.GetFieldType(fieldName);
            if (!fieldType.HasValue)
            {
                return null;
            }

            // Navigate based on field type
            if (fieldType.Value == GFFFieldType.List)
            {
                GFFList resultList = currentStruct.GetList(fieldName);
                if (resultList == null)
                {
                    Console.WriteLine($"[ERROR] get_list('{fieldName}') returned None in {context}");
                }
                return resultList;
            }

            if (fieldType.Value == GFFFieldType.Struct)
            {
                GFFStruct resultStruct = currentStruct.GetStruct(fieldName);
                if (resultStruct == null)
                {
                    Console.WriteLine($"[ERROR] get_struct('{fieldName}') returned None in {context}");
                }
                return resultStruct;
            }

            Console.WriteLine($"[ERROR] Field '{fieldName}' has type {fieldType.Value}, expected List or Struct in {context}");
            return null;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:791-811
        // Original: def _extract_struct_from_modifier(...): ...
        private GFFStruct ExtractStructFromModifier(AddStructToListGFF modifier)
        {
            if (modifier.Value is FieldValueConstant constant)
            {
                var storedProperty = constant.GetType().GetProperty("Stored");
                if (storedProperty != null)
                {
                    object stored = storedProperty.GetValue(constant);
                    if (stored is GFFStruct gffStruct)
                    {
                        return gffStruct;
                    }
                }
            }

            // Create new struct
            return new GFFStruct();
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:813-848
        // Original: def _apply_add_struct_to_list(...): ...
        private void ApplyAddStructToList(GFFStruct rootStruct, AddStructToListGFF modifier)
        {
            // Navigate to the target list
            var pathParts = !string.IsNullOrEmpty(modifier.Path)
                ? modifier.Path.Replace("\\", "/").Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).ToList()
                : new List<string>();
            GFFList targetList = NavigateToListCreatingIfNeeded(rootStruct, pathParts, $"AddStructToList: {modifier.Identifier}");

            if (targetList == null)
            {
                return;
            }

            // Extract struct from modifier
            GFFStruct newStruct = ExtractStructFromModifier(modifier);

            // Add the struct to the list
            GFFStruct addedStruct = targetList.Add(newStruct.StructId);

            // Copy fields from newStruct to addedStruct
            foreach ((string fieldLabel, GFFFieldType fieldType, object fieldValue) in newStruct)
            {
                SetFieldByType(addedStruct, fieldLabel, fieldType, fieldValue);
            }

            // Apply nested modifiers to the added struct
            if (modifier.Modifiers != null)
            {
                foreach (var nestedMod in modifier.Modifiers)
                {
                    if (nestedMod is AddFieldGFF addField)
                    {
                        ApplyAddField(addedStruct, addField);
                    }
                    else if (nestedMod is AddStructToListGFF addStructToList)
                    {
                        ApplyAddStructToList(addedStruct, addStructToList);
                    }
                }
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/gff/gff_data.py
        // Original: GFFContent.get_extensions()
        private static HashSet<string> GetGffExtensions()
        {
            // All GFF content types map to their lowercase extension
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "gff", "bic", "btc", "btd", "bte", "bti", "btp", "btm", "btt",
                "utc", "utd", "ute", "uti", "utp", "uts", "utm", "utt", "utw",
                "are", "dlg", "fac", "git", "gui", "ifo", "itp", "jrl", "pth",
                "nfo", "pt", "gvt", "inv"
            };
        }
    }
}
