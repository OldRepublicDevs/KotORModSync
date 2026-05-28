using System;
using System.Numerics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KOTORModSync;
using KOTORModSync.Formats.GFF;
using KOTORModSync.Logger;
using KOTORModSync.Memory;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Mods.GFF
{

    /// <summary>
    /// GFF modification algorithms for TSLPatcher/HoloPatcher.
    ///
    /// This module implements GFF field modification logic for applying patches from changes.ini files.
    /// Handles field additions, modifications, list operations, and struct manipulations.
    ///
    /// References:
    /// ----------
    ///     vendor/TSLPatcher/TSLPatcher.pl - Perl GFF modification logic (broken and unfinished)
    ///     vendor/Kotor.NET/Kotor.NET.Patcher/ - Incomplete C# patcher
    /// </summary>

    /// <summary>
    /// Helper function to set localized string field.
    /// Python: def set_locstring(struct: GFFStruct, label: str, value: LocalizedStringDelta, memory: PatcherMemory)
    /// </summary>
    internal static class GFFHelpers
    {
        internal static void SetLocString(GFFStruct struct_, string label, LocalizedStringDelta value, PatcherMemory memory)
        {
            LocalizedString original = new LocalizedString(0);
            value.Apply(original, memory);
            struct_.SetLocString(label, original);
        }
    }

    /// <summary>
    /// Abstract base for GFF modifications.
    /// 1:1 port from Python ModifyGFF in pykotor/tslpatcher/mods/gff.py
    /// </summary>
    public abstract class ModifyGFF
    {
        public abstract void Apply(object rootContainer, PatcherMemory memory, PatchLogger logger, Game game = Game.K1);

        protected static void SetFieldValue(GFFStruct gffStruct, string label, object value, GFFFieldType fieldType, PatcherMemory memory)
        {
            // Python: FIELD_TYPE_TO_SETTER[field_type](struct_container, label, value, memory)
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
                    gffStruct.SetString(label, value.ToString() ?? "");
                    break;
                case GFFFieldType.ResRef:
                    gffStruct.SetResRef(label, value as ResRef ?? ResRef.FromBlank());
                    break;
                case GFFFieldType.LocalizedString:
                    if (value is LocalizedStringDelta delta)
                    {
                        GFFHelpers.SetLocString(gffStruct, label, delta, memory);
                    }
                    else if (value is LocalizedString locString)
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
                        gffStruct.SetVector4(label, new System.Numerics.Vector4(v4.X, v4.Y, v4.Z, v4.W));
                    }
                    break;
                case GFFFieldType.List:
                    if (value is GFFList list)
                    {
                        gffStruct.SetList(label, list);
                    }
                    break;
                case GFFFieldType.Struct:
                    if (value is GFFStruct @struct)
                    {
                        gffStruct.SetStruct(label, @struct);
                    }
                    break;
            }
        }

        /// <summary>
        /// Navigates through gff lists/structs to find the specified path.
        ///
        /// Args:
        /// ----
        ///     root_container (GFFStruct): The root container to start navigation
        ///
        /// Returns:
        /// -------
        ///     container (GFFList | GFFStruct | None): The container at the end of the path or None if not found
        ///
        /// Processing Logic:
        /// ----------------
        ///     - It checks if the path is valid PureWindowsPath
        ///     - Loops through each part of the path
        ///     - Acquires the container at each step from the parent container
        ///     - Returns the container at the end or None if not found along the path
        /// </summary>
        [CanBeNull]
        protected static object NavigateContainers(GFFStruct rootContainer, string path)
        {
            // Python: path = PureWindowsPath(path)
            // Python: if not path.name: return root_container
            if (string.IsNullOrEmpty(path))
            {
                return rootContainer;
            }

            string[] parts = path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            // Python: container: ComparableMixin | GFFStruct | GFFList | None = root_container
            // Can be null if not found
            object container = rootContainer;

            foreach (string step in parts)
            {
                // Python: Skip >>##INDEXINLIST##<< sentinel - it's not a real path component
                if (step == ">>##INDEXINLIST##<<")
                {
                    continue;
                }

                if (container is GFFStruct gffStruct)
                {
                    // Python: container = container.acquire(step, None, (GFFStruct, GFFList))
                    // Try struct first, then list
                    // Can be null if struct not found
                    if (gffStruct.TryGetStruct(step, out GFFStruct childStruct))
                    {
                        container = childStruct;
                    }
                    // Can be null if list not found
                    else if (gffStruct.TryGetList(step, out GFFList childList))
                    {
                        container = childList;
                    }
                    else
                    {
                        return null;
                    }
                }
                else if (container is GFFList gffList)
                {
                    // Python: container = container.at(int(step))
                    if (int.TryParse(step, out int index))
                    {
                        container = gffList.At(index);
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }

            // Python: assert isinstance(container, (GFFStruct, GFFList, type(None)))
            return container;
        }

        /// <summary>
        /// Helper method to split a path into parent path and label (filename).
        /// Handles both backslashes and forward slashes correctly on all platforms.
        /// </summary>
        protected static (string parentPath, string label) SplitPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return ("", "");
            }

            string[] parts = path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return ("", "");
            }

            if (parts.Length == 1)
            {
                return ("", parts[0]);
            }

            string label = parts[parts.Length - 1];
            string parentPath = string.Join("\\", parts, 0, parts.Length - 1);
            return (parentPath, label);
        }

        protected static string CombinePath(string basePath, string childPath)
        {
            string trimmedBase = string.IsNullOrEmpty(basePath) ? "" : basePath.TrimEnd('\\', '/');
            string trimmedChild = string.IsNullOrEmpty(childPath) ? "" : childPath.Trim('\\', '/');
            if (string.IsNullOrEmpty(trimmedBase))
            {
                return trimmedChild;
            }
            if (string.IsNullOrEmpty(trimmedChild))
            {
                return trimmedBase;
            }
            return $"{trimmedBase}/{trimmedChild}";
        }

        /// <summary>
        /// Navigates to a field from the root gff struct from a path.
        /// Python: def _navigate_to_field(self, root_container: GFFStruct, path: PureWindowsPath | os.PathLike | str) -> _GFFField | None
        /// Returns a tuple of (fieldType, value) or null if not found
        /// </summary>
        [CanBeNull]
        protected (GFFFieldType fieldType, object value)? NavigateToField(GFFStruct rootContainer, string path)
        {
            // Python: path = PureWindowsPath(path)
            // Python: container: GFFList | GFFStruct | None = self._navigate_containers(root_container, path.parent)
            (string parentPath, string label) = SplitPath(path);
            // Can be null if not found
            object container = NavigateContainers(rootContainer, parentPath);

            // Python: return container._fields[label] if isinstance(container, GFFStruct) else None
            if (container is GFFStruct gffStruct)
            {
                // Access field type and value - in C# we use TryGetFieldType and GetValue
                if (gffStruct.TryGetFieldType(label, out GFFFieldType fieldType))
                {
                    // Can be null if not found
                    object value = gffStruct.GetValue(label);
                    if (value != null)
                    {
                        return (fieldType, value);
                    }
                }
            }
            return null;
        }

        protected static string GetIdentifierForLogging(ModifyGFF modifier)
        {
            if (modifier is AddStructToListGFF addStruct)
            {
                return addStruct.Identifier;
            }
            if (modifier is AddFieldGFF addField)
            {
                return addField.Identifier;
            }
            if (modifier is ModifyFieldGFF modifyField)
            {
                return modifyField.Identifier;
            }
            if (modifier is Memory2DAModifierGFF mem2DA)
            {
                return mem2DA.Identifier;
            }
            return modifier.GetType().Name;
        }

        protected static string GetPathForLogging(ModifyGFF modifier)
        {
            if (modifier is AddStructToListGFF addStruct)
            {
                return addStruct.Path;
            }
            if (modifier is AddFieldGFF addField)
            {
                return addField.Path;
            }
            if (modifier is ModifyFieldGFF modifyField)
            {
                return modifyField.Path;
            }
            if (modifier is Memory2DAModifierGFF mem2DA)
            {
                return mem2DA.Path;
            }
            return "";
        }
    }

    /// <summary>
    /// Adds a new struct to a GFF list.
    /// 1:1 port from Python AddStructToListGFF
    /// </summary>
    public class AddStructToListGFF : ModifyGFF
    {
        public string Identifier { get; }
        public FieldValue Value { get; }
        public string Path { get; set; }
        public int? IndexToToken { get; }
        public List<ModifyGFF> Modifiers { get; } = new List<ModifyGFF>();

        public AddStructToListGFF(string identifier, [CanBeNull] FieldValue value, string path, int? indexToToken = null)
        {
            Identifier = identifier;
            Value = value;
            Path = path;
            IndexToToken = indexToToken;
        }

        /// <summary>
        /// Adds a new struct to a list.
        ///
        /// Args:
        /// ----
        ///     root_struct: The root struct to navigate and modify.
        ///     memory: The memory object to read/write values from.
        ///     logger: The logger to log errors or warnings.
        ///
        /// Processing Logic:
        /// ----------------
        ///     1. Navigates to the target list container using the provided path.
        ///     2. Checks if the navigated container is a list, otherwise logs an error.
        ///     3. Creates a new struct and adds it to the list.
        ///     4. Applies any additional field modifications specified in the modifiers.
        /// </summary>
        public override void Apply(object rootContainer, PatcherMemory memory, PatchLogger logger, Game game = Game.K1)
        {
            if (!(rootContainer is GFFStruct rootStruct))
            {
                logger.AddError($"Expected GFFStruct but got {rootContainer.GetType().Name}");
                return;
            }

            // Python: list_container: GFFList | None = [CanBeNull] None
            GFFList listContainer = null;

            // Python: if self.path.name == ">>##INDEXINLIST##<<":
            (_, string pathName) = SplitPath(Path);
            string workingPath = Path;
            if (pathName == ">>##INDEXINLIST##<<")
            {
                logger.AddVerbose($"Removing unique sentinel from AddStructToListGFF instance (ini section [{Identifier}]). Path: '{Path}'");
                // Python: self.path = self.path.parent  # HACK(th3w1zard1): idk why conditional parenting is necessary but it works
                (workingPath, _) = SplitPath(Path);
                Path = workingPath;
            }

            // Python: navigated_container: GFFList | GFFStruct | None = self._navigate_containers(root_container, self.path) if self.path.name else root_container
            (_, string workingPathName) = SplitPath(workingPath);
            // Can be null if not found
            object navigatedContainer = string.IsNullOrEmpty(workingPathName)
                ? rootStruct
                : NavigateContainers(rootStruct, workingPath);

            // Python: if navigated_container is root_container:
            if (navigatedContainer == rootStruct)
            {
                logger.AddNote($"GFF path '{workingPath}' not found, defaulting to the gff root struct.");
            }

            // Python: if isinstance(navigated_container, GFFList):
            if (navigatedContainer is GFFList gffList)
            {
                listContainer = gffList;
            }
            else
            {
                // Python: reason: str = "Does not exist" if navigated_container is None else f"Path points to a '{navigated_container.__class__.__name__}', expected a GFFList."
                string reason = navigatedContainer is null
                    ? "Does not exist"
                    : $"Path points to a '{navigatedContainer.GetType().Name}', expected a GFFList.";
                string pathDisplay = string.IsNullOrEmpty(workingPath) ? $"[{Identifier}]" : workingPath;
                logger.AddError($"Unable to add struct to list '{pathDisplay}': {reason}");
                return;
            }

            // Python: new_struct: GFFStruct | None = [CanBeNull] None
            GFFStruct newStruct = null;
            try
            {
                // Python: lookup: Any = self.value.value(memory, GFFFieldType.Struct)
                object lookup = Value.Value(memory, GFFFieldType.Struct);

                // Python: if lookup == "listindex":
                if (lookup is string listIndexStr && listIndexStr == "listindex")
                {
                    // Python: new_struct = GFFStruct(len(list_container._structs)-1)
                    newStruct = new GFFStruct(listContainer.Count - 1);
                }
                // Python: elif isinstance(lookup, GFFStruct):
                else if (lookup is GFFStruct gffStruct)
                {
                    newStruct = gffStruct;
                }
                else
                {
                    // Python: raise ValueError(f"bad lookup: {lookup} ({lookup!r}) expected 'listindex' or GFFStruct")
                    throw new ArgumentException($"bad lookup: {lookup} ({lookup}) expected 'listindex' or GFFStruct");
                }
            }
            catch (KeyNotFoundException e)
            {
                // Python: except KeyError as e:
                logger.AddError($"INI section [{Identifier}] threw an exception: {e}");
            }

            // Python: if not isinstance(new_struct, GFFStruct):
            if (newStruct is null)
            {
                logger.AddError($"Failed to add a new struct to list '{workingPath}' in [{Identifier}]. Reason: Expected GFFStruct but got '{newStruct}' ({newStruct}) of type {newStruct?.GetType().Name ?? "null"} Skipping...");
                return;
            }

            // Python: list_container._structs.append(new_struct)
            // In C#, Add creates a new struct, so we need to add with the structId and then copy fields if it's an existing struct
            GFFStruct addedStruct = listContainer.Add(newStruct.StructId);
            // If newStruct is not the same as what Add created (i.e., it's an existing struct with fields), copy the fields
            if (newStruct.Count > 0)
            {
                // Copy all fields from newStruct to addedStruct
                foreach ((string label, GFFFieldType fieldType, object value) in newStruct)
                {
                    SetFieldValue(addedStruct, label, value, fieldType, memory);
                }
            }

            // Python: if self.index_to_token is not None:
            if (IndexToToken.HasValue)
            {
                // Python: length = str(len(list_container) - 1)
                string length = (listContainer.Count - 1).ToString();
                // Python: logger.add_verbose(f"Set 2DAMEMORY{self.index_to_token}={length}")
                logger.AddVerbose($"Set 2DAMEMORY{IndexToToken.Value}={length}");
                // Python: memory.memory_2da[self.index_to_token] = length
                memory.Memory2DA[IndexToToken.Value] = length;
            }

            // Python: for add_field in self.modifiers:
            foreach (ModifyGFF addField in Modifiers)
            {
                // Python: assert isinstance(add_field, (AddFieldGFF, AddStructToListGFF, Memory2DAModifierGFF, ModifyFieldGFF)), f"{type(add_field).__name__}: {add_field}"
                if (
                    !(addField is AddFieldGFF)
                    && !(addField is AddStructToListGFF)
                    && !(addField is Memory2DAModifierGFF)
                    && !(addField is ModifyFieldGFF))
                {
                    logger.AddError($"Unexpected modifier type: {addField.GetType().Name}: {addField}");
                    continue;
                }

                // Python: list_index = len(list_container) - 1
                int listIndex = listContainer.Count - 1;

                // Python: newpath = self.path / str(list_index)
                string newpath = string.IsNullOrEmpty(workingPath)
                    ? listIndex.ToString()
                    : $"{workingPath}/{listIndex}";

                string addFieldIdentifier = GetIdentifierForLogging(addField);
                string addFieldPath = GetPathForLogging(addField);
                logger.AddVerbose($"Resolved GFFList path of [{addFieldIdentifier}] from '{addFieldPath}' --> '{newpath}'");
                // Python: add_field.path = newpath
                if (addField is AddFieldGFF addFieldGFF)
                {
                    addFieldGFF.Path = newpath;
                }
                else if (addField is AddStructToListGFF addStructToListGFF)
                {
                    // Update the path (Path property is mutable with { get; set; })
                    addStructToListGFF.Path = newpath;
                }

                // Python: add_field.apply(root_container, memory, logger)
                addField.Apply(rootStruct, memory, logger, game);
            }
        }

        public static GFFFieldType FieldType => GFFFieldType.Struct;
    }

    /// <summary>
    /// Adds a new field to a GFF structure.
    /// 1:1 port from Python AddFieldGFF
    /// </summary>
    public class AddFieldGFF : ModifyGFF
    {
        public string Identifier { get; }
        public string Label { get; }
        public GFFFieldType FieldType { get; }
        public FieldValue Value { get; }
        public string Path { get; set; }
        public List<ModifyGFF> Modifiers { get; } = new List<ModifyGFF>();

        public AddFieldGFF(string identifier, string label, GFFFieldType fieldType, FieldValue value, string path)
        {
            Identifier = identifier;
            Label = label;
            FieldType = fieldType;
            Value = value;
            Path = path;
        }

        /// <summary>
        /// Adds a new field to a GFF struct.
        ///
        /// Args:
        /// ----
        ///     root_struct: GFFStruct - The root GFF struct to navigate and modify.
        ///     memory: PatcherMemory - The memory state to read values from.
        ///     logger: PatchLogger - The logger to record errors to.
        ///
        /// Processing Logic:
        /// ----------------
        ///     - Navigates to the specified container path and gets the GFFStruct instance
        ///     - Resolves the field value using the provided value expression
        ///     - Resolves the value path if part of !FieldPath memory
        ///     - Sets the field on the struct instance using the appropriate setter based on field type
        ///     - Applies any modifier patches recursively
        /// </summary>
        public override void Apply(object rootContainer, PatcherMemory memory, PatchLogger logger, Game game = Game.K1)
        {
            if (!(rootContainer is GFFStruct rootStruct))
            {
                logger.AddError($"Expected GFFStruct but got {rootContainer.GetType().Name}");
                return;
            }

            logger.AddVerbose($"Apply patch from INI section [{Identifier}] FieldType: {FieldType} GFF Path: '{Path}'");
            // Python: container_path = self.path.parent if self.path.name == ">>##INDEXINLIST##<<" else self.path
            (string parentPath, string pathName) = SplitPath(Path);
            string containerPath = pathName == ">>##INDEXINLIST##<<" ? parentPath : Path ?? string.Empty;

            // Python: navigated_container: GFFList | GFFStruct | None = self._navigate_containers(root_container, container_path)
            // Can be null if not found
            object navigatedContainer = NavigateContainers(rootStruct, containerPath);
            if (!(navigatedContainer is GFFStruct structContainer))
            {
                string reason = navigatedContainer is null ? "does not exist!" : "is not an instance of a GFFStruct.";
                logger.AddError($"Unable to add new GFF Field '{Label}' at GFF Path '{containerPath}'! This {reason}");
                return;
            }

            // Python: value: Any = self.value.value(memory, self.field_type)
            object value = Value.Value(memory, FieldType);

            // Python: if isinstance(value, PureWindowsPath): - Handle !FieldPath
            if (value is string strValue && (strValue.Contains('/') || strValue.Contains('\\')))
            {
                string storedFieldpath = strValue;
                if (Value is FieldValue2DAMemory fieldValue2DA)
                {
                    logger.AddVerbose($"Looking up field pointer of stored !FieldPath ({storedFieldpath}) in 2DAMEMORY{fieldValue2DA.TokenId}");
                }
                else
                {
                    logger.AddVerbose($"Found PureWindowsPath object in value() lookup from non-FieldValue2DAMemory object? Path: \"{storedFieldpath}\" INI section: [{Identifier}]");
                }
                (string fromParentPath, string fromLabel) = SplitPath(storedFieldpath);
                // Can be null if not found
                object fromContainer = NavigateContainers(rootStruct, fromParentPath);
                if (!(fromContainer is GFFStruct fromStruct))
                {
                    string reason = fromContainer is null ? "does not exist!" : "is not an instance of a GFFStruct.";
                    logger.AddError($"Unable to use !FieldPath from 2DAMEMORY. Parent field at '{fromParentPath}' {reason}");
                    return;
                }
                value = fromStruct.GetValue(fromLabel) ?? value;
                logger.AddVerbose($"Acquired value '{value}' from 2DAMEMORY !FieldPath({storedFieldpath})");
            }

            logger.AddVerbose($"AddField: Creating field of type '{FieldType}' value: '{value}' at GFF path '{Path}'. INI section: [{Identifier}]");

            // Python: FIELD_TYPE_TO_SETTER[self.field_type](struct_container, self.label, value, memory)
            SetFieldValue(structContainer, Label, value, FieldType, memory);

            // Python: for add_field in self.modifiers:
            foreach (ModifyGFF addField in Modifiers)
            {
                // Python: assert isinstance(add_field, (AddFieldGFF, AddStructToListGFF, ModifyFieldGFF, Memory2DAModifierGFF))
                if (
                    !(addField is AddFieldGFF)
                    && !(addField is AddStructToListGFF)
                    && !(addField is ModifyFieldGFF)
                    && !(addField is Memory2DAModifierGFF))
                {
                    logger.AddError($"Unexpected modifier type: {addField.GetType().Name}");
                    continue;
                }

                // Python: # HACK(th3w1zard1): resolves any >>##INDEXINLIST##<<, not sure why lengths aren't the same though (hence use of zip_longest)? Whatever, it works.
                // Python: newpath = PureWindowsPath("")
                // Python: for part, resolvedpart in zip_longest(add_field.path.parts, self.path.parts):
                // Python:     newpath /= resolvedpart or part
                string childPath = addField is AddFieldGFF af ? af.Path : (addField is AddStructToListGFF asl ? asl.Path : string.Empty);
                // Python zip_longest logic: newpath parts are resolvedpart or part
                List<string> newpathParts = new List<string>();
                string[] childParts = string.IsNullOrEmpty(childPath) ? Array.Empty<string>() : childPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                string[] selfParts = string.IsNullOrEmpty(Path) ? Array.Empty<string>() : Path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                int maxParts = Math.Max(childParts.Length, selfParts.Length);
                for (int i = 0; i < maxParts; i++)
                {
                    string part = i < childParts.Length ? childParts[i] : null;
                    string resolved = i < selfParts.Length ? selfParts[i] : null;
                    newpathParts.Add(!string.IsNullOrEmpty(resolved) ? resolved : part);
                }
                string newpath = string.Join("/", newpathParts.Where(p => !string.IsNullOrEmpty(p)));

                string childIdentifier = GetIdentifierForLogging(addField);
                logger.AddVerbose($"Resolved gff path of INI section [{childIdentifier}] from relative '{childPath}' --> absolute '{newpath}'");
                if (addField is AddFieldGFF addFieldGFF)
                {
                    addFieldGFF.Path = newpath;
                }
                else if (addField is AddStructToListGFF addStructToListGFF)
                {
                    addStructToListGFF.Path = newpath;
                }

                addField.Apply(rootStruct, memory, logger, game);
            }
        }
    }

    /// <summary>
    /// A modifier class used for !FieldPath support.
    /// </summary>
    public class Memory2DAModifierGFF : ModifyGFF
    {
        public string Identifier { get; }
        public string Path { get; }
        public int DestTokenId { get; }
        public int? SrcTokenId { get; }

        public Memory2DAModifierGFF(string identifier, string path, int destTokenId, int? srcTokenId = null)
        {
            Identifier = identifier;
            Path = path;
            DestTokenId = destTokenId;
            SrcTokenId = srcTokenId;
        }

        public override void Apply(object rootContainer, PatcherMemory memory, PatchLogger logger, Game game = Game.K1)
        {
            if (!(rootContainer is GFFStruct rootStruct))
            {
                return;
            }

            // Python: dest_field: _GFFField | None = None
            // Python: source_field: _GFFField | None = None
            (GFFFieldType fieldType, object value)? destFieldInfo = null;
            (GFFFieldType fieldType, object value)? sourceFieldInfo = null;

            // Python: display_dest_name = f"2DAMEMORY{self.dest_token_id}"
            string displayDestName = $"2DAMEMORY{DestTokenId}";

            // Python: display_src_name = f"2DAMEMORY{self.src_token_id}"
            string displaySrcName;

            // Python: if self.src_token_id is None:  # assign the path and leave.
            if (SrcTokenId is null)
            {
                // Python: display_src_name = f"!FieldPath({self.path})"
                displaySrcName = $"!FieldPath({Path})";
                // Python: logger.add_verbose(f"Assign {display_dest_name}={display_src_name}")
                logger.AddVerbose($"Assign {displayDestName}={displaySrcName}");

                // Python: memory.memory_2da[self.dest_token_id] = self.path
                // Python stores PureWindowsPath object, which when converted to string uses backslashes on Windows
                // Match Python's behavior: PureWindowsPath uses backslashes, so store as-is (Path already has backslashes from ConfigReader)
                // Python's PureWindowsPath("Nested\Field") converts to string as "Nested\Field" (single backslash)
                string windowsPath = Path ?? "";
                memory.Memory2DA[DestTokenId] = windowsPath;
                return;
            }

            // Python: display_src_name = f"2DAMEMORY{self.src_token_id}"
            displaySrcName = $"2DAMEMORY{SrcTokenId.Value}";
            // Python: logger.add_verbose(f"GFFList ptr !fieldpath: Assign {display_dest_name}={display_src_name} initiated. iniPath: {self.path}, section: [{self.identifier}]")
            logger.AddVerbose($"GFFList ptr !fieldpath: Assign {displayDestName}={displaySrcName} initiated. iniPath: {Path}, section: [{Identifier}]");

            // Python: ptr_to_dest: PureWindowsPath | Any = memory.memory_2da.get(self.dest_token_id, None) if self.dest_token_id is not None else self.path
            // Can be null if not found
            object ptrToDest = memory.Memory2DA.TryGetValue(DestTokenId, out string destPath) ? destPath : Path;

            // Python: if isinstance(ptr_to_dest, PureWindowsPath):
            // In Python, PureWindowsPath("AppearanceType") is still a path (path.name="AppearanceType", path.parent="")
            // So we need to check if ptrToDest is a string (path) and try to navigate to it
            if (ptrToDest is string destPathStr)
            {
                // Python: dest_field = self._navigate_to_field(root_container, ptr_to_dest)
                destFieldInfo = NavigateToField(rootStruct, destPathStr);

                // Python: if dest_field is None:
                if (destFieldInfo is null)
                {
                    // Python: raise ValueError(f"Cannot assign 2DAMEMORY{self.dest_token_id}=2DAMEMORY{self.src_token_id}: LEFT side of assignment's path '{ptr_to_dest}' does not point to a valid GFF Field!")
                    throw new ArgumentException($"Cannot assign 2DAMEMORY{DestTokenId}=2DAMEMORY{SrcTokenId.Value}: LEFT side of assignment's path '{ptrToDest}' does not point to a valid GFF Field!");
                }

                // Python: assert isinstance(dest_field, _GFFField)
                // Python: logger.add_verbose(f"LEFT SIDE 2DAMEMORY{self.src_token_id} lookup at '{ptr_to_dest}' returned '{dest_field.value()}'")
                logger.AddVerbose($"LEFT SIDE 2DAMEMORY{SrcTokenId.Value} lookup at '{ptrToDest}' returned '{destFieldInfo.Value.value}'");
            }
            // Python: elif ptr_to_dest is None:
            else if (ptrToDest is null)
            {
                // Python: logger.add_verbose(f"Left side {display_dest_name} is not defined yet.")
                logger.AddVerbose($"Left side {displayDestName} is not defined yet.");
            }
            else
            {
                // Python: logger.add_verbose(f"Left side {display_dest_name} value of {ptr_to_dest} will be overwritten.")
                logger.AddVerbose($"Left side {displayDestName} value of {ptrToDest} will be overwritten.");
            }

            // Python: # Lookup assigning value
            // Python: ptr_to_src: PureWindowsPath | Any = memory.memory_2da.get(self.src_token_id, None)
            if (!memory.Memory2DA.TryGetValue(SrcTokenId.Value, out string ptrToSrc))
            {
                ptrToSrc = null;
            }

            // Python: if ptr_to_src is None:
            if (ptrToSrc is null)
            {
                // Python: raise ValueError(f"Cannot assign {display_dest_name}={display_src_name} because RIGHT side of assignment is undefined.")
                throw new ArgumentException($"Cannot assign {displayDestName}={displaySrcName} because RIGHT side of assignment is undefined.");
            }

            // Python: if isinstance(ptr_to_src, PureWindowsPath):
            if (ptrToSrc is string srcPathStr && (srcPathStr.Contains('/') || srcPathStr.Contains('\\')))
            {
                // Python: logger.add_verbose(f"Assigner {display_src_name} is a pointer !FieldPath to another field located at '{ptr_to_src}'")
                logger.AddVerbose($"Assigner {displaySrcName} is a pointer !FieldPath to another field located at '{ptrToSrc}'");

                // Python: source_field = self._navigate_to_field(root_container, ptr_to_src)
                sourceFieldInfo = NavigateToField(rootStruct, ptrToSrc);

                // Python: assert not isinstance(source_field, PureWindowsPath)
                // Python: assert isinstance(source_field, _GFFField)
                if (sourceFieldInfo is null)
                {
                    throw new InvalidOperationException($"Source field at '{ptrToSrc}' is not a valid GFF Field");
                }
            }
            else
            {
                // Python: logger.add_verbose(f"Assigner {display_src_name} holds literal value '{ptr_to_src}'. other stored info debug: Path: '{self.path}' INI section: [{self.identifier}]")
                logger.AddVerbose($"Assigner {displaySrcName} holds literal value '{ptrToSrc}'. other stored info debug: Path: '{Path}' INI section: [{Identifier}]");
            }

            // Python: if isinstance(dest_field, _GFFField):
            if (destFieldInfo.HasValue)
            {
                // Python: logger.add_verbose("assign dest ptr field.")
                logger.AddVerbose("assign dest ptr field.");

                // Python: assert source_field is None or dest_field.field_type() is source_field.field_type(), f"Not a _GFFField: {ptr_to_src} ({display_src_name}) OR {dest_field.field_type()} != {source_field.field_type()}"
                if (sourceFieldInfo.HasValue && destFieldInfo.Value.fieldType != sourceFieldInfo.Value.fieldType)
                {
                    throw new InvalidOperationException($"Not a _GFFField: {ptrToSrc} ({displaySrcName}) OR {destFieldInfo.Value.fieldType} != {sourceFieldInfo.Value.fieldType}");
                }

                // Python: dest_field._value = FieldValueConstant(ptr_to_src).value(memory, dest_field.field_type())
                // Get the destination field path to set it
                string destFieldPath = ptrToDest is string destPathForSetting && (destPathForSetting.Contains('/') || destPathForSetting.Contains('\\'))
                    ? destPathForSetting
                    : Path;
                (string destParentPath, string destLabel) = SplitPath(destFieldPath);
                // Can be null if not found
                object destContainer = NavigateContainers(rootStruct, destParentPath);
                if (destContainer is GFFStruct destStruct)
                {
                    // Python: dest_field._value = FieldValueConstant(ptr_to_src).value(memory, dest_field.field_type())
                    // Note: ptr_to_src can be either a path string or a literal value, FieldValueConstant handles both
                    object convertedValue = new FieldValueConstant(ptrToSrc).Value(memory, destFieldInfo.Value.fieldType);
                    SetFieldValue(destStruct, destLabel, convertedValue, destFieldInfo.Value.fieldType, memory);
                }
            }
            else
            {
                // Python: memory.memory_2da[self.dest_token_id] = ptr_to_dest
                memory.Memory2DA[DestTokenId] = ptrToDest?.ToString() ?? "";
            }
        }
    }

    /// <summary>
    /// Modifies an existing field in a GFF structure.
    /// 1:1 port from Python ModifyFieldGFF
    /// </summary>
    public class ModifyFieldGFF : ModifyGFF
    {
        public string Path { get; }
        public FieldValue Value { get; }
        public string Identifier { get; }

        // Python line 520-528: def __init__(self, path, value, identifier: str = "")
        public ModifyFieldGFF(string path, FieldValue value, string identifier = "")
        {
            Path = path;
            Value = value;
            Identifier = identifier;
        }

        /// <summary>
        /// Applies a patch to an existing field in a GFF structure.
        ///
        /// Args:
        /// ----
        ///     root_struct: {GFF structure}: Root GFF structure to navigate and modify
        ///     memory: {PatcherMemory}: Memory context to retrieve values
        ///     logger: {PatchLogger}: Logger to record errors
        ///
        /// Processing Logic:
        /// ----------------
        ///     - Navigates container hierarchy to the parent of the field using the patch path
        ///     - Checks if parent container exists and is a GFFStruct
        ///     - Gets the field type from the parent struct
        ///     - Converts the patch value to the correct type
        ///     - Calls the corresponding setter method on the parent struct
        /// </summary>
        public override void Apply(object rootContainer, PatcherMemory memory, PatchLogger logger, Game game = Game.K1)
        {
            if (!(rootContainer is GFFStruct rootStruct))
            {
                logger.AddError($"Expected GFFStruct but got {rootContainer.GetType().Name}");
                return;
            }

            // Python: label: str = self.path.name
            // Python: navigated_container: GFFList | GFFStruct | None = self._navigate_containers(root_container, self.path.parent)
            (string parentPath, string label) = SplitPath(Path);
            // Can be null if not found
            object navigatedContainer = NavigateContainers(rootStruct, parentPath);

            if (!(navigatedContainer is GFFStruct navigatedStruct))
            {
                string reason = navigatedContainer is null ? "does not exist!" : "is not an instance of a GFFStruct.";
                logger.AddError($"Unable to modify GFF field '{label}'. Path '{Path}' {reason}");
                return;
            }

            // Python: field_type: GFFFieldType = navigated_struct._fields[label].field_type()
            if (!navigatedStruct.TryGetFieldType(label, out GFFFieldType fieldType))
            {
                // Field does not exist; align with TSLPatcher behavior by creating it on-the-fly.
                fieldType = GFFFieldType.Int32;
            }

            // Python: value: Any = self.value.value(memory, field_type)
            object value = Value.Value(memory, fieldType);

            // Python: if isinstance(value, PureWindowsPath): - Handle !FieldPath
            if (value is string strValue && (strValue.Contains('/') || strValue.Contains('\\')))
            {
                string storedFieldpath = strValue;
                if (Value is FieldValue2DAMemory fieldValue2DA)
                {
                    logger.AddVerbose($"Looking up field pointer of stored !FieldPath ({storedFieldpath}) in 2DAMEMORY{fieldValue2DA.TokenId}");
                }
                else
                {
                    logger.AddVerbose($"Found PureWindowsPath object in value() lookup from non-FieldValue2DAMemory object? Path: \"{storedFieldpath}\" INI section: [{Identifier}]");
                }
                (string fromParentPath, string fromLabel) = SplitPath(storedFieldpath);
                // Can be null if not found
                object fromContainer = NavigateContainers(rootStruct, fromParentPath);
                if (!(fromContainer is GFFStruct fromStruct))
                {
                    string reason = fromContainer is null ? "does not exist!" : "is not an instance of a GFFStruct.";
                    logger.AddError($"Unable use !FieldPath from 2DAMEMORY. Parent field at '{fromParentPath}' {reason}");
                    return;
                }
                value = fromStruct.GetValue(fromLabel) ?? value;
                logger.AddVerbose($"Acquired value '{value}' from field at !FieldPath '{storedFieldpath}'");
            }

            // Python: try: orig_value = FIELD_TYPE_TO_GETTER[field_type](navigated_struct, label)
            try
            {
                // Can be null if not found
                object origValue = GetFieldValue(navigatedStruct, label, fieldType);
                logger.AddVerbose($"Found original value of '{origValue}' ({origValue}) at GFF Path {Path}: Patch section: [{Identifier}]");
            }
            catch (KeyNotFoundException)
            {
                string msg = $"The field {fieldType} did not exist at {Path} in INI section [{Identifier}]. Use AddField if you need to create fields/structs.\nDue to the above error, no value will be set here.";
                logger.AddError(msg);
                return;
            }

            logger.AddVerbose($"Direct set value of determined field type '{fieldType}' at GFF path '{Path}' to new value '{value}'. INI section: [{Identifier}]");

            // Python: if field_type is not GFFFieldType.LocalizedString:
            if (fieldType != GFFFieldType.LocalizedString)
            {
                SetFieldValue(navigatedStruct, label, value, fieldType, memory);
                return;
            }

            // Python: assert isinstance(value, LocalizedString)
            if (!(value is LocalizedString locString))
            {
                logger.AddError($"Expected LocalizedString but got {value?.GetType().Name ?? "null"}");
                return;
            }

            // Python: if not navigated_struct.exists(label):
            if (!navigatedStruct.Exists(label))
            {
                navigatedStruct.SetLocString(label, locString);
            }
            else
            {
                // Python: assert isinstance(value, LocalizedStringDelta)
                if (!(value is LocalizedStringDelta delta))
                {
                    logger.AddError($"Expected LocalizedStringDelta for existing field but got {value.GetType().Name}");
                    return;
                }
                LocalizedString original = navigatedStruct.GetLocString(label);
                delta.Apply(original, memory);
                navigatedStruct.SetLocString(label, original);
            }
        }

        private static object GetFieldValue(GFFStruct gffStruct, string label, GFFFieldType fieldType)
        {
            switch (fieldType)
            {
                case GFFFieldType.Int8: return gffStruct.GetInt8(label);
                case GFFFieldType.UInt8: return gffStruct.GetUInt8(label);
                case GFFFieldType.Int16: return gffStruct.GetInt16(label);
                case GFFFieldType.UInt16: return gffStruct.GetUInt16(label);
                case GFFFieldType.Int32: return gffStruct.GetInt32(label);
                case GFFFieldType.UInt32: return gffStruct.GetUInt32(label);
                case GFFFieldType.Int64: return gffStruct.GetInt64(label);
                case GFFFieldType.UInt64: return gffStruct.GetUInt64(label);
                case GFFFieldType.Single: return gffStruct.GetSingle(label);
                case GFFFieldType.Double: return gffStruct.GetDouble(label);
                case GFFFieldType.String: return gffStruct.GetString(label);
                case GFFFieldType.ResRef: return gffStruct.GetResRef(label);
                case GFFFieldType.LocalizedString: return gffStruct.GetLocString(label);
                case GFFFieldType.Vector3: return gffStruct.GetVector3(label);
                case GFFFieldType.Vector4: return gffStruct.GetVector4(label);
                case GFFFieldType.List: return gffStruct.GetList(label);
                case GFFFieldType.Struct: return gffStruct.GetStruct(label);
                default:
                    throw new KeyNotFoundException($"Unknown field type: {fieldType}");
            }
        }
    }
}
