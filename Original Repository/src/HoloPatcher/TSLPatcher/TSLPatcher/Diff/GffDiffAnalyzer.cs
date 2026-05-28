// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/analyzers.py:296-688
// Original: class GFFDiffAnalyzer(DiffAnalyzer): ...
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KOTORModSync;
using KOTORModSync.Formats.GFF;
using KOTORModSync.Mods.GFF;
using KOTORModSync.Common;

namespace KOTORModSync.Diff
{
    /// <summary>
    /// Analyzer for GFF file differences.
    /// 1:1 port of GFFDiffAnalyzer from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/analyzers.py:296-688
    /// </summary>
    public class GffDiffAnalyzer
    {
        private const double GFF_FLOAT_TOLERANCE = 1e-6;

        public ModificationsGFF Analyze(byte[] leftData, byte[] rightData, string identifier)
        {
            GFF leftGff;
            GFF rightGff;
            try
            {
                var leftReader = new GFFBinaryReader(leftData);
                var rightReader = new GFFBinaryReader(rightData);
                leftGff = leftReader.Load();
                rightGff = rightReader.Load();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to parse GFF files: identifier={identifier}, error={e}");
                Console.WriteLine(e.StackTrace);
                return null;
            }

            // Extract just the filename from the identifier (may contain full path like "install/modules/file.utc")
            string filename = Path.GetFileName(identifier ?? string.Empty);
            var modifications = new ModificationsGFF(filename, replace: false, modifiers: new List<ModifyGFF>());

            // Analyze struct differences recursively
            var rootPath = new PurePath();
            AnalyzeStruct(leftGff.Root, rightGff.Root, rootPath, modifications);

            if (modifications.Modifiers.Count > 0)
            {
                Console.WriteLine($"GFF modifications: identifier={identifier}, modifier_count={modifications.Modifiers.Count}");
            }

            return modifications.Modifiers.Count > 0 ? modifications : null;
        }

        private void AnalyzeStruct(
            GFFStruct leftStruct,
            GFFStruct rightStruct,
            PurePath path,
            ModificationsGFF modifications)
        {
            // Get all fields
            var leftFields = new HashSet<string>(leftStruct.Select(f => f.label));
            var rightFields = new HashSet<string>(rightStruct.Select(f => f.label));

            // Common fields - check for modifications
            var commonFields = new HashSet<string>(leftFields);
            commonFields.IntersectWith(rightFields);
            foreach (string fieldLabel in commonFields)
            {
                var fieldPath = path / fieldLabel;
                AnalyzeField(leftStruct, rightStruct, fieldLabel, fieldPath, modifications);
            }

            // Added fields
            var addedFields = new HashSet<string>(rightFields);
            addedFields.ExceptWith(leftFields);
            foreach (string fieldLabel in addedFields)
            {
                var fieldPath = path / fieldLabel;
                CreateAddField(rightStruct, fieldLabel, fieldPath, modifications);
            }
        }

        private void AnalyzeField(
            GFFStruct leftStruct,
            GFFStruct rightStruct,
            string fieldLabel,
            PurePath fieldPath,
            ModificationsGFF modifications)
        {
            // Use exists() to check field presence, then get field info properly
            bool leftExists = leftStruct.Exists(fieldLabel);
            if (!leftExists)
            {
                return;
            }
            bool rightExists = rightStruct.Exists(fieldLabel);
            if (!rightExists)
            {
                return;
            }

            // Get field types
            GFFFieldType? leftFieldTypeNullable = leftStruct.GetFieldType(fieldLabel);
            GFFFieldType? rightFieldTypeNullable = rightStruct.GetFieldType(fieldLabel);
            if (!leftFieldTypeNullable.HasValue || !rightFieldTypeNullable.HasValue)
            {
                return;
            }
            GFFFieldType leftFieldType = leftFieldTypeNullable.Value;
            GFFFieldType rightFieldType = rightFieldTypeNullable.Value;

            bool typesDiffer = leftFieldType != rightFieldType;
            if (typesDiffer)
            {
                // Type changed - treat as modification
                CreateModifyField(rightStruct, fieldLabel, fieldPath, modifications);
                return;
            }

            // Compare values based on type
            bool isStructType = leftFieldType == GFFFieldType.Struct;
            if (isStructType)
            {
                // Recursively analyze nested struct
                GFFStruct leftNested = leftStruct.GetStruct(fieldLabel);
                GFFStruct rightNested = rightStruct.GetStruct(fieldLabel);
                AnalyzeStruct(leftNested, rightNested, fieldPath, modifications);
            }
            else if (leftFieldType == GFFFieldType.List)
            {
                // Analyze list differences
                GFFList leftList = leftStruct.GetList(fieldLabel);
                GFFList rightList = rightStruct.GetList(fieldLabel);
                AnalyzeList(leftList, rightList, fieldPath, modifications);
            }
            else
            {
                // Scalar value comparison
                bool valuesEqual = ValuesEqual(leftFieldType, rightFieldType, leftStruct, rightStruct, fieldLabel);
                if (!valuesEqual)
                {
                    CreateModifyField(rightStruct, fieldLabel, fieldPath, modifications);
                }
            }
        }

        private void AnalyzeList(
            GFFList leftList,
            GFFList rightList,
            PurePath path,
            ModificationsGFF modifications)
        {
            int leftSize = leftList.Count;
            int rightSize = rightList.Count;

            // Check common elements for modifications
            int minSize = Math.Min(leftSize, rightSize);
            for (int idx = 0; idx < minSize; idx++)
            {
                var itemPath = path / idx.ToString();
                GFFStruct leftItem = leftList.At(idx);
                GFFStruct rightItem = rightList.At(idx);
                bool leftItemExists = leftItem != null;
                if (!leftItemExists)
                {
                    continue;
                }
                bool rightItemExists = rightItem != null;
                if (!rightItemExists)
                {
                    continue;
                }
                if (leftItem != null && rightItem != null)
                {
                    AnalyzeStruct(leftItem, rightItem, itemPath, modifications);
                }
            }

            // Handle added list elements
            bool hasNewItems = rightSize > leftSize;
            if (hasNewItems)
            {
                for (int idx = leftSize; idx < rightSize; idx++)
                {
                    GFFStruct rightItem = rightList.At(idx);
                    bool rightItemExists = rightItem != null;
                    if (!rightItemExists)
                    {
                        continue;
                    }

                    // Create AddStructToListGFF for each new list entry
                    // Generate unique identifier for this list addition
                    string sourcefileNormalized = modifications.SourceFile.Replace(".", "_");
                    string pathName = path.Name;
                    string sectionName = $"gff_{sourcefileNormalized}_{pathName}_{idx}_0";

                    // Create a FieldValueConstant that wraps the GFFStruct
                    var value = new FieldValueConstant(rightItem);

                    // Create the AddStructToListGFF modifier
                    var addStruct = new AddStructToListGFF(
                        identifier: sectionName,
                        value: value,
                        path: path.ToString(),
                        indexToToken: null);

                    // Recursively add all fields from the new struct
                    if (rightItem != null)
                    {
                        AddAllStructFields(rightItem, addStruct, new PurePath());
                    }

                    modifications.Modifiers.Add(addStruct);
                }
            }
        }

        private void AddAllStructFields(
            GFFStruct gffStruct,
            ModifyGFF parentModifier,
            PurePath basePath)
        {
            // Iterate over struct fields (returns tuples of (label, field_type, value))
            string parentId = null;
            if (parentModifier is AddStructToListGFF addStructToList)
            {
                parentId = addStructToList.Identifier;
            }
            else if (parentModifier is AddFieldGFF addField)
            {
                parentId = addField.Identifier;
            }
            if (parentId == null)
            {
                return;
            }
            foreach ((string fieldLabel, GFFFieldType fieldType, object fieldValue) in gffStruct)
            {
                PurePath fieldPath = basePath.Name != null ? basePath / fieldLabel : new PurePath(fieldLabel);

                // Generate unique section name for this field
                string sectionName = $"{parentId}_{fieldLabel}_0".Replace("\\", "_").Replace("/", "_");

                // Create AddFieldGFF for this field
                bool isStructType = fieldType == GFFFieldType.Struct;
                if (isStructType)
                {
                    // For structs, create AddFieldGFF with nested modifiers
                    var valueConstant = new FieldValueConstant(fieldValue);
                    var addField = new AddFieldGFF(
                        identifier: sectionName,
                        label: fieldLabel,
                        fieldType: fieldType,
                        value: valueConstant,
                        path: fieldPath.ToString());

                    // Recursively add nested fields
                    bool isGffStruct = fieldValue is GFFStruct;
                    if (isGffStruct)
                    {
                        AddAllStructFields((GFFStruct)fieldValue, addField, new PurePath());
                    }

                    if (parentModifier is AddStructToListGFF addStructToList1)
                    {
                        addStructToList1.Modifiers.Add(addField);
                    }
                    else if (parentModifier is AddFieldGFF addFieldParent1)
                    {
                        addFieldParent1.Modifiers.Add(addField);
                    }
                }
                else if (fieldType == GFFFieldType.List)
                {
                    // For lists, create AddFieldGFF
                    var valueConstant = new FieldValueConstant(fieldValue);
                    var addField = new AddFieldGFF(
                        identifier: sectionName,
                        label: fieldLabel,
                        fieldType: fieldType,
                        value: valueConstant,
                        path: fieldPath.ToString());

                    // Add nested structs from the list
                    bool isGffList = fieldValue is GFFList;
                    if (isGffList)
                    {
                        var gffList = (GFFList)fieldValue;
                        for (int listIdx = 0; listIdx < gffList.Count; listIdx++)
                        {
                            GFFStruct listItem = gffList.At(listIdx);
                            bool isListItemStruct = listItem != null;
                            if (isListItemStruct)
                            {
                                string listSectionName = $"{sectionName}_{listIdx}_0";
                                var listValue = new FieldValueConstant(listItem);

                                var addListStruct = new AddStructToListGFF(
                                    identifier: listSectionName,
                                    value: listValue,
                                    path: "",
                                    indexToToken: null);

                                // Recursively add all fields from the list item
                                AddAllStructFields(listItem, addListStruct, new PurePath());
                                addField.Modifiers.Add(addListStruct);
                            }
                        }
                    }

                    if (parentModifier is AddStructToListGFF addStructToList2)
                    {
                        addStructToList2.Modifiers.Add(addField);
                    }
                    else if (parentModifier is AddFieldGFF addFieldParent2)
                    {
                        addFieldParent2.Modifiers.Add(addField);
                    }
                }
                else
                {
                    // For simple fields, just create AddFieldGFF with the value
                    var valueConstant = new FieldValueConstant(fieldValue);
                    var addField = new AddFieldGFF(
                        identifier: sectionName,
                        label: fieldLabel,
                        fieldType: fieldType,
                        value: valueConstant,
                        path: fieldPath.ToString());

                    if (parentModifier is AddStructToListGFF addStructToList3)
                    {
                        addStructToList3.Modifiers.Add(addField);
                    }
                    else if (parentModifier is AddFieldGFF addFieldParent3)
                    {
                        addFieldParent3.Modifiers.Add(addField);
                    }
                }
            }
        }

        private void CreateModifyField(
            GFFStruct gffStruct,
            string fieldLabel,
            PurePath fieldPath,
            ModificationsGFF modifications)
        {
            bool fieldExists = gffStruct.Exists(fieldLabel);
            if (!fieldExists)
            {
                return;
            }

            // Get field type
            GFFFieldType? fieldTypeNullable = gffStruct.GetFieldType(fieldLabel);
            if (!fieldTypeNullable.HasValue)
            {
                return;
            }
            GFFFieldType fieldType = fieldTypeNullable.Value;
            object value = GetFieldValue(gffStruct, fieldLabel, fieldType);

            string pathStr = fieldPath.ToString().Replace("/", "\\");
            var modifyField = new ModifyFieldGFF(
                path: pathStr,
                value: new FieldValueConstant(value));
            modifications.Modifiers.Add(modifyField);
        }

        private void CreateAddField(
            GFFStruct gffStruct,
            string fieldLabel,
            PurePath fieldPath,
            ModificationsGFF modifications)
        {
            bool fieldExists = gffStruct.Exists(fieldLabel);
            if (!fieldExists)
            {
                return;
            }

            // Get field type
            GFFFieldType? fieldTypeNullable = gffStruct.GetFieldType(fieldLabel);
            if (!fieldTypeNullable.HasValue)
            {
                return;
            }
            GFFFieldType fieldType = fieldTypeNullable.Value;
            object value = GetFieldValue(gffStruct, fieldLabel, fieldType);

            // Determine parent path
            bool hasParent = fieldPath.Parent != null && fieldPath.Parent.Parts.Count > 0;
            string parentPath = hasParent ? fieldPath.Parent.ToString().Replace("/", "\\") : "";

            string sourcefileNormalized = modifications.SourceFile.Replace(".", "_");
            string addFieldId = $"{sourcefileNormalized}_add_{fieldLabel}";
            var addField = new AddFieldGFF(
                identifier: addFieldId,
                label: fieldLabel,
                fieldType: fieldType,
                value: new FieldValueConstant(value),
                path: parentPath);
            modifications.Modifiers.Add(addField);
        }

        private object GetFieldValue(
            GFFStruct gffStruct,
            string fieldLabel,
            GFFFieldType fieldType)
        {
            switch (fieldType)
            {
                case GFFFieldType.UInt8:
                    return gffStruct.GetUInt8(fieldLabel);
                case GFFFieldType.Int8:
                    return gffStruct.GetInt8(fieldLabel);
                case GFFFieldType.UInt16:
                    return gffStruct.GetUInt16(fieldLabel);
                case GFFFieldType.Int16:
                    return gffStruct.GetInt16(fieldLabel);
                case GFFFieldType.UInt32:
                    return gffStruct.GetUInt32(fieldLabel);
                case GFFFieldType.Int32:
                    return gffStruct.GetInt32(fieldLabel);
                case GFFFieldType.UInt64:
                    return gffStruct.GetUInt64(fieldLabel);
                case GFFFieldType.Int64:
                    return gffStruct.GetInt64(fieldLabel);
                case GFFFieldType.Single:
                    return gffStruct.GetSingle(fieldLabel);
                case GFFFieldType.Double:
                    return gffStruct.GetDouble(fieldLabel);
                case GFFFieldType.String:
                    return gffStruct.GetString(fieldLabel);
                case GFFFieldType.ResRef:
                    return gffStruct.GetResRef(fieldLabel);
                case GFFFieldType.LocalizedString:
                    return gffStruct.GetLocString(fieldLabel);
                case GFFFieldType.Vector3:
                    return gffStruct.GetVector3(fieldLabel);
                case GFFFieldType.Vector4:
                    return gffStruct.GetVector4(fieldLabel);
                default:
                    return null;
            }
        }

        private bool ValuesEqual(
            GFFFieldType leftFieldType,
            GFFFieldType rightFieldType,
            GFFStruct leftStruct,
            GFFStruct rightStruct,
            string fieldLabel)
        {
            object leftValue = GetFieldValue(leftStruct, fieldLabel, leftFieldType);
            object rightValue = GetFieldValue(rightStruct, fieldLabel, rightFieldType);

            // Special handling for floats
            bool isFloatComparison = leftValue is float && rightValue is float;
            if (isFloatComparison)
            {
                float leftFloat = (float)leftValue;
                float rightFloat = (float)rightValue;
                double diff = Math.Abs(leftFloat - rightFloat);
                return diff < GFF_FLOAT_TOLERANCE;
            }

            return Equals(leftValue, rightValue);
        }
    }

    /// <summary>
    /// Simple path class to match Python's PurePath behavior for path manipulation.
    /// </summary>
    public class PurePath
    {
        public List<string> Parts { get; } = new List<string>();

        public PurePath()
        {
        }

        public PurePath(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                Parts.AddRange(path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries));
            }
        }

        public string Name => Parts.Count > 0 ? Parts[Parts.Count - 1] : null;

        public PurePath Parent
        {
            get
            {
                if (Parts.Count <= 1)
                {
                    return new PurePath();
                }
                var parent = new PurePath();
                for (int i = 0; i < Parts.Count - 1; i++)
                {
                    parent.Parts.Add(Parts[i]);
                }
                return parent;
            }
        }

        public static PurePath operator /(PurePath left, string right)
        {
            var result = new PurePath();
            if (left != null)
            {
                result.Parts.AddRange(left.Parts);
            }
            if (!string.IsNullOrEmpty(right))
            {
                result.Parts.AddRange(right.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries));
            }
            return result;
        }

        public static PurePath operator /(PurePath left, int right)
        {
            return left / right.ToString();
        }

        public override string ToString()
        {
            return string.Join("/", Parts);
        }
    }
}
