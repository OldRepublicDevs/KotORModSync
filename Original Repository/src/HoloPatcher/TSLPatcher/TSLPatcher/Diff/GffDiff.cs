using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.GFF;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Diff
{

    public class GffCompareResult
    {
        public List<(string Path, object OldValue, object NewValue)> Differences { get; } = new List<(string Path, object OldValue, object NewValue)>();

        public void AddDifference(string path, [CanBeNull] object oldValue, [CanBeNull] object newValue)
        {
            Differences.Add((path, oldValue, newValue));
        }
    }

    public static class GffDiff
    {
        public static GffCompareResult Compare(GFFStruct original, GFFStruct modified)
        {
            var result = new GffCompareResult();
            CompareStructs(original, modified, "", result);
            return result;
        }

        private static void CompareStructs(GFFStruct original, GFFStruct modified, string currentPath, GffCompareResult result)
        {
            // Check all fields in modified
            // Can be null if field not found
            foreach (string fieldLabel in modified.Select(f => f.label))
            {
                object modValue = modified.GetValue(fieldLabel);
                object origValue = original.GetValue(fieldLabel);
                string fieldPath = string.IsNullOrEmpty(currentPath) ? fieldLabel : $"{currentPath}/{fieldLabel}";

                if (origValue == null)
                {
                    // New field
                    result.AddDifference(fieldPath, null, modValue);
                }
                else
                {
                    CompareValues(origValue, modValue, fieldPath, result);
                }
            }

            // Check for removed fields (in original but not in modified)
            var modifiedFieldLabels = modified.Select(f => f.label).ToHashSet();
            // Can be null if field not found
            foreach (string fieldLabel in original.Select(f => f.label))
            {
                if (!modifiedFieldLabels.Contains(fieldLabel))
                {
                    object origValue = original.GetValue(fieldLabel);
                    string fieldPath = string.IsNullOrEmpty(currentPath) ? fieldLabel : $"{currentPath}/{fieldLabel}";
                    result.AddDifference(fieldPath, origValue, null);
                }
            }
        }

        private static void CompareValues([CanBeNull] object origValue, [CanBeNull] object modValue, string currentPath, GffCompareResult result)
        {
            if (origValue == null && modValue == null)
            {
                return;
            }

            if (origValue == null || modValue == null)
            {
                result.AddDifference(currentPath, origValue, modValue);
                return;
            }

            // Types must match (mostly)
            if (origValue.GetType() != modValue.GetType())
            {
                result.AddDifference(currentPath, origValue, modValue);
                return;
            }

            if (origValue is GFFStruct origStruct && modValue is GFFStruct modStruct)
            {
                CompareStructs(origStruct, modStruct, currentPath, result);
            }
            else if (origValue is GFFList origList && modValue is GFFList modList)
            {
                CompareLists(origList, modList, currentPath, result);
            }
            else if (!ValuesAreEqual(origValue, modValue))
            {
                result.AddDifference(currentPath, origValue, modValue);
            }
        }

        private static void CompareLists(GFFList original, GFFList modified, string currentPath, GffCompareResult result)
        {
            // Compare by index
            int count = Math.Max(original.Count, modified.Count);
            for (int i = 0; i < count; i++)
            {
                // Can be null if item not found
                GFFStruct origItem = i < original.Count ? original.At(i) : null;
                // Can be null if item not found
                GFFStruct modItem = i < modified.Count ? modified.At(i) : null;
                string itemPath = $"{currentPath}/{i}";

                if (origItem == null)
                {
                    // New item
                    result.AddDifference(itemPath, null, modItem);
                }
                else if (modItem == null)
                {
                    // Removed item (usually not handled well by TSLPatcher GFFList patching, but detected here)
                    result.AddDifference(itemPath, origItem, null);
                }
                else
                {
                    CompareStructs(origItem, modItem, itemPath, result);
                }
            }
        }

        private static bool ValuesAreEqual(object v1, object v2)
        {
            if (v1.Equals(v2))
            {
                return true;
            }

            // Handle specific types like Vector3, Vector4, ResRef, etc. if they don't implement Equals correctly
            // Assuming Andastra.Parsing types implement Equals or are value types.
            // ResRef, LocalizedString, Vector3, Vector4 in KOTORModSync.Common should implement Equals.

            return false;
        }

        public static Dictionary<string, object> FlattenDifferences(GffCompareResult result)
        {
            var flat = new Dictionary<string, object>();
            foreach ((string path, object _, object newValue) in result.Differences)
            {
                // Normalize path separator
                string normalizedPath = path.Replace("\\", "/");
                flat[normalizedPath] = newValue;
            }
            return flat;
        }

        public static Dictionary<string, object> BuildHierarchy(Dictionary<string, object> flatChanges)
        {
            var root = new Dictionary<string, object>();

            foreach (KeyValuePair<string, object> kvp in flatChanges)
            {
                string[] parts = kvp.Key.Split('/');
                Dictionary<string, object> currentDict = root;

                for (int i = 0; i < parts.Length - 1; i++)
                {
                    string part = parts[i];
                    if (!currentDict.ContainsKey(part))
                    {
                        currentDict[part] = new Dictionary<string, object>();
                    }

                    if (currentDict[part] is Dictionary<string, object> nextDict)
                    {
                        currentDict = nextDict;
                    }
                    else
                    {
                        // Conflict: path implies directory but existing value is a leaf
                        // TODO: FIXME - For now, overwrite or ignore?
                        // Python logic probably overwrites.
                        var newDict = new Dictionary<string, object>();
                        currentDict[part] = newDict;
                        currentDict = newDict;
                    }
                }

                currentDict[parts.Last()] = kvp.Value;
            }

            return root;
        }

        public static string SerializeToIni(Dictionary<string, object> hierarchy)
        {
            var sb = new StringBuilder();
            SerializeDict(sb, hierarchy, "");
            return sb.ToString();
        }

        private static void SerializeDict(StringBuilder sb, Dictionary<string, object> dict, string sectionPrefix)
        {
            // Split into values and nested sections
            var values = new Dictionary<string, object>();
            var sections = new Dictionary<string, Dictionary<string, object>>();

            foreach (KeyValuePair<string, object> kvp in dict)
            {
                if (kvp.Value is Dictionary<string, object> nestedDict)
                {
                    sections[kvp.Key] = nestedDict;
                }
                else
                {
                    values[kvp.Key] = kvp.Value;
                }
            }

            // Write values
            if (values.Count > 0)
            {
                if (!string.IsNullOrEmpty(sectionPrefix))
                {
                    sb.AppendLine($"[{sectionPrefix}]");
                }

                foreach (KeyValuePair<string, object> kvp in values)
                {
                    string key = kvp.Key;
                    string valStr = FormatValue(kvp.Value);
                    sb.AppendLine($"{key}={valStr}");
                }
                sb.AppendLine();
            }

            // Recurse for sections
            foreach (KeyValuePair<string, Dictionary<string, object>> kvp in sections)
            {
                string nextSection = string.IsNullOrEmpty(sectionPrefix) ? kvp.Key : $"{sectionPrefix}.{kvp.Key}";
                // If sectionPrefix is empty, we might be at root. Root usually doesn't have [Root] section in TSLPatcher unless specified.
                // TSLPatcher INI structure usually starts with [GFFList] -> File -> [File] -> ModifyField -> Value

                // The tests expect:
                // [Section1]
                // Field1=value1

                SerializeDict(sb, kvp.Value, nextSection);
            }
        }

        private static string FormatValue([CanBeNull] object value)
        {
            if (value == null)
            {
                return "null";
            }

            if (value is string s)
            {
                if (s.Contains(" "))
                {
                    return $"\"{s}\"";
                }

                return s;
            }
            // Handle other types
            return value.ToString() ?? "";
        }
    }
}
