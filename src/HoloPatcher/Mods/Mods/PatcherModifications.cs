using System;
using System.Collections.Generic;
using System.IO;
using KOTORModSync;
using KOTORModSync.Logger;
using KOTORModSync.Memory;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Mods
{

    /// <summary>
    /// Possible actions for how the patcher should behave when patching a file to a ERF/MOD/RIM while that filename already exists in the Override folder.
    /// </summary>
    public static class OverrideType
    {
        /// <summary>Do nothing: don't even check (TSLPatcher default)</summary>
        public const string IGNORE = "ignore";

        /// <summary>Log a warning (HoloPatcher default)</summary>
        public const string WARN = "warn";

        /// <summary>Rename the file in the Override folder with the 'old_' prefix. Also logs a warning.</summary>
        public const string RENAME = "rename";
    }

    /// <summary>
    /// Abstract base class for TSLPatcher modifications.
    /// 
    /// Args:
    /// ----
    ///     sourcefile (str): The source file for the modifications.
    ///     replace (bool | None, optional): Whether to replace the file. Defaults to None.
    ///     modifiers (list | None, optional): List of modifiers. Defaults to None.
    /// 
    /// Attributes:
    /// ----------
    ///     sourcefile (str): The source file for the modifications.
    ///     sourcefolder (str): The source folder.
    ///     saveas (str): The final name of the file this patch will save as (!SaveAs/!Filename)
    ///     replace_file (bool): Whether to replace the file.
    ///         This bool is True when using syntax Replace#=file_to_replace.ext, and therefore False when File#=file_to_replace.ext syntax is used.
    ///         It is currently unknown whether this takes priority over !ReplaceFile, current PyKotor implementation will prioritize !ReplaceFile
    ///     destination (str): The destination for the patch file.
    ///     action (str): The action for this patch, purely used for logging purposes.
    ///     override_type (str): The override type, see `class OverrideType` above.
    ///     skip_if_not_replace (bool): Determines how !ReplaceFile will be handled.
    ///         TSLPatcher's InstallList and CompileList are the only patchlists that handle replace behavior differently.
    ///         in InstallList/CompileList, if this is True and !ReplaceFile is False or File#=file_to_install.ext, the resource will be skipped if the resource already exists.
    /// 
    /// Methods:
    /// -------
    ///     patch_resource(source, memory, logger, game): Patch the resource defined by the 'source' arg. Returns the bytes data of the result.
    ///     apply(mutable_data, memory, logger, game): Apply this patch's modifications to the mutable_data object argument passed.
    ///     pop_tslpatcher_vars(file_section_dict, default_destination): Parse optional TSLPatcher exclamation point variables.
    /// 
    /// Exclamation-point variables:
    /// ---------------------------
    ///     NOTE: All exclamation-point variables that define a path in TSLPatcher must be backslashed instead of forward-slashed. PyKotor will normalize both slashes though.
    ///     - Top-level variables (e.g. [CompileList] [InstallList] [GFFList])
    ///         !DefaultDestination=relative/path/to/destination/folder - Determines where the destination folder is for top-level patch objects.
    ///             Note: !DefaultDestination is highly undocumented in TSLPatcher so it's unclear whether this matches what TSLPatcher does. I believe it takes priority over InstallList's destinations (excluding !Destination)
    ///     - File-level variables ( e.g. [my_file.nss] )
    ///         !SourceFile=this_file.extension - the name of the file to load. Defaults to 'this_file.ext' when using the `File#=this_file` or `Replace#=this_file` syntax.
    ///         !ReplaceFile=&lt;1 or 0&gt; - Whether to replace the file. Takes priority over `Replace#=this_file.ext` syntax
    ///         !SaveAs=&lt;some_file.tpc&gt; - Determines the final filename of the patch. Defaults to whatever !SourceFile is defined as.
    ///         !Filename=&lt;asdf_file.qwer&gt; - Literally the same as !SaveAs
    ///         !Destination=relative/path/to/destination/folder - The relative path to the folder to save this patched file.
    ///         !OverrideType=&lt;warn or ignore or rename&gt; - How to handle conflict resolution. See `class OverrideType` above.
    ///         !SourceFolder=relative/path/to/tslpatchdata/subfolder - **NEW HOLOPATCHER** support for pathing within the mod's tslpatchdata itself. Currently only used in InstallList.
    ///     NOTE: Some patch lists, albeit rare, have different exclamation-point variables. See tslpatcher/mods/ncs.py and tslpatcher/mods/tlk.py for outliers.
    /// </summary>
    public abstract class PatcherModifications
    {
        public const string DEFAULT_DESTINATION = "Override";

        /// <summary>
        /// The source file for the modifications.
        /// </summary>
        public virtual string SourceFile { get; set; }

        /// <summary>
        /// The source folder.
        /// </summary>
        public virtual string SourceFolder { get; set; } = ".";

        /// <summary>
        /// The final name of the file this patch will save as (!SaveAs/!Filename)
        /// </summary>
        public virtual string SaveAs { get; set; }

        /// <summary>
        /// Whether to replace the file.
        /// This bool is True when using syntax Replace#=file_to_replace.ext, and therefore False when File#=file_to_replace.ext syntax is used.
        /// It is currently unknown whether this takes priority over !ReplaceFile, current PyKotor implementation will prioritize !ReplaceFile
        /// </summary>
        public virtual bool ReplaceFile { get; set; }

        /// <summary>
        /// The destination for the patch file.
        /// </summary>
        public virtual string Destination { get; set; } = DEFAULT_DESTINATION;

        /// <summary>
        /// The action for this patch, purely used for logging purposes.
        /// </summary>
        public virtual string Action { get; set; } = "Patch ";

        /// <summary>
        /// The override type, see `class OverrideType` above.
        /// </summary>
        public virtual string OverrideTypeValue { get; set; } = OverrideType.WARN;

        /// <summary>
        /// Determines how !ReplaceFile will be handled.
        /// TSLPatcher's InstallList and CompileList are the only patchlists that handle replace behavior differently.
        /// in InstallList/CompileList, if this is True and !ReplaceFile is False or File#=file_to_install.ext, the resource will be skipped if the resource already exists.
        /// </summary>
        public virtual bool SkipIfNotReplace { get; set; }

        /// <summary>
        /// Full path to source file for copying to tslpatchdata (set by diff engine)
        /// </summary>
        [CanBeNull]
        protected string SourceFilePath { get; set; }

        protected PatcherModifications(
            string sourcefile,
            bool? replace = null,
            [CanBeNull] string destination = null)
        {
            SourceFile = sourcefile;
            SourceFolder = ".";
            SaveAs = sourcefile;
            ReplaceFile = replace ?? false;
            Destination = destination ?? DEFAULT_DESTINATION;

            Action = "Patch" + " ";
            OverrideTypeValue = OverrideType.WARN;
            SkipIfNotReplace = false; // [InstallList] and [CompileList] only
        }

        /// <summary>
        /// If bytes is returned, patch the resource. If True is returned, skip this resource.
        /// </summary>
        public abstract object PatchResource(
            byte[] source,
            PatcherMemory memory,
            PatchLogger logger,
            Game game);

        /// <summary>
        /// Apply this patch's modifications to the mutable_data object argument passed.
        /// </summary>
        public abstract void Apply(
            object mutableData,
            PatcherMemory memory,
            PatchLogger logger,
            Game game);

        /// <summary>
        /// All optional TSLPatcher vars that can be parsed for a given patch list.
        /// </summary>
        public virtual void PopTslPatcherVars(
            [CanBeNull] Dictionary<string, string> fileSectionDict,
            [CanBeNull] string defaultDestination = null,
            string defaultSourceFolder = ".")
        {
            // Note: The second argument passed to the 'pop' function is the default.
            SourceFile = fileSectionDict.TryGetValue("!SourceFile", out string sourceFile) ? sourceFile : SourceFile;
            fileSectionDict.Remove("!SourceFile");
            // !SaveAs and !Filename are the same.
            SaveAs = fileSectionDict.TryGetValue("!Filename", out string filename) ? filename : fileSectionDict.TryGetValue("!SaveAs", out string saveAs) ? saveAs : SaveAs;
            fileSectionDict.Remove("!Filename");
            fileSectionDict.Remove("!SaveAs");

            string destinationFallback = defaultDestination ?? DEFAULT_DESTINATION;
            Destination = fileSectionDict.TryGetValue("!Destination", out string destination) ? destination : destinationFallback;
            fileSectionDict.Remove("!Destination");

            // !ReplaceFile=1 is prioritized, see Stoffe's HLFP mod v2.1 for reference.
            object replaceFile = fileSectionDict.TryGetValue("!ReplaceFile", out string rf) ? rf : (object)ReplaceFile;
            ReplaceFile = ConvertToBool(replaceFile);
            fileSectionDict.Remove("!ReplaceFile");

            // TSLPatcher defaults to "ignore". However realistically, Override file shadowing is
            // a major problem, so HoloPatcher defaults to "warn"
            OverrideTypeValue = fileSectionDict.TryGetValue("!OverrideType", out string overrideType) ? overrideType.ToLowerInvariant() : OverrideType.WARN;
            fileSectionDict.Remove("!OverrideType");
            // !SourceFolder: Relative path from mod_path (which is typically the tslpatchdata folder) to source files.
            // Default value "." refers to mod_path itself (the tslpatchdata folder), not its parent.
            // Path resolution: mod_path / sourcefolder / sourcefile
            // For example: if mod_path = "C:/Mod/tslpatchdata" and sourcefolder = ".", then:
            //   - Final path = "C:/Mod/tslpatchdata" / "." / "file.ext" = "C:/Mod/tslpatchdata/file.ext"
            //   - If sourcefolder = "subfolder", then: "C:/Mod/tslpatchdata" / "subfolder" / "file.ext" = "C:/Mod/tslpatchdata/subfolder/file.ext"
            SourceFolder = fileSectionDict.TryGetValue("!SourceFolder", out string sourceFolder) ? sourceFolder : defaultSourceFolder;
            fileSectionDict.Remove("!SourceFolder");
        }

        /// <summary>
        /// Convert a value to boolean.
        /// 
        /// The value can be:
        /// - A boolean (True or False)
        /// - A string "1" (which should be converted to True)
        /// - A string "0" (which should be converted to False)
        /// 
        /// This function is redundant, but provided for users that may not understand Python.
        /// </summary>
        protected static bool ConvertToBool(object value)
        {
            // Convert a value to boolean.
            // 
            // The value can be:
            // - A boolean (True or False)
            // - A string "1" (which should be converted to True)
            // - A string "0" (which should be converted to False)
            // 
            // This function is redundant, but provided for users that may not understand Python.
            return value is bool b && b || (value is string str && str == "1");
        }
    }
}