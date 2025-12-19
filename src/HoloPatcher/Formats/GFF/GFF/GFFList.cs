using System;
using System.Numerics;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KOTORModSync;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Formats.GFF
{

    /// <summary>
    /// A collection of GFFStructs.
    /// </summary>
    public class GFFList : IEnumerable<GFFStruct>
    {
        private readonly List<GFFStruct> _structs = new List<GFFStruct>();

        public int Count => _structs.Count;

        public GFFStruct Add(int structId = 0)
        {
            var newStruct = new GFFStruct(structId);
            _structs.Add(newStruct);
            return newStruct;
        }

        [CanBeNull]
        public GFFStruct At(int index)
        {
            return index < _structs.Count ? _structs[index] : null;
        }

        public void Remove(int index)
        {
            if (index >= 0 && index < _structs.Count)
            {
                _structs.RemoveAt(index);
            }
        }

        public GFFStruct this[int index]
        {
            get => _structs[index];
            set => _structs[index] = value;
        }

        public IEnumerator<GFFStruct> GetEnumerator() => _structs.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/gff/gff_data.py:1863-2111
        // Original: def compare(self, other: object, log_func: Callable[..., Any] = print, current_path: PureWindowsPath | None = None, *, ignore_default_changes: bool = False, ignore_values: dict[str, set[Any]] | None = None, comparison_result: GFFComparisonResult | None = None) -> bool:
        public bool Compare(GFFList other, Action<string> logFunc, string currentPath = null, bool ignoreDefaultChanges = false, Dictionary<string, HashSet<object>> ignoreValues = null, GFFComparisonResult comparisonResult = null)
        {
            currentPath = currentPath ?? "GFFList";
            ignoreValues = ignoreValues ?? new Dictionary<string, HashSet<object>>();
            comparisonResult = comparisonResult ?? new GFFComparisonResult();
            bool isSameResult = true;

            if (other == null)
            {
                logFunc($"GFFList counts have changed at '{currentPath}': '{Count}' --> '<unknown>'");
                logFunc("");
                return false;
            }

            // Build content-based lookup to detect moved/reordered structs
            Dictionary<(int structId, string fieldsKey), List<int>> oldStructsMap = new Dictionary<(int, string), List<int>>();
            Dictionary<(int structId, string fieldsKey), List<int>> newStructsMap = new Dictionary<(int, string), List<int>>();

            for (int idx = 0; idx < _structs.Count; idx++)
            {
                GFFStruct s = _structs[idx];
                var key = StructKey(s);
                if (!oldStructsMap.ContainsKey(key))
                {
                    oldStructsMap[key] = new List<int>();
                }
                oldStructsMap[key].Add(idx);
            }

            for (int idx = 0; idx < other._structs.Count; idx++)
            {
                GFFStruct s = other._structs[idx];
                var key = StructKey(s);
                if (!newStructsMap.ContainsKey(key))
                {
                    newStructsMap[key] = new List<int>();
                }
                newStructsMap[key].Add(idx);
            }

            HashSet<(int, string)> addedKeys = new HashSet<(int, string)>(newStructsMap.Keys);
            addedKeys.ExceptWith(oldStructsMap.Keys);

            HashSet<(int, string)> removedKeys = new HashSet<(int, string)>(oldStructsMap.Keys);
            removedKeys.ExceptWith(newStructsMap.Keys);

            HashSet<(int, string)> commonKeys = new HashSet<(int, string)>(oldStructsMap.Keys);
            commonKeys.IntersectWith(newStructsMap.Keys);

            HashSet<int> reportedIndicesOld = new HashSet<int>();
            HashSet<int> reportedIndicesNew = new HashSet<int>();

            int len1 = Count;
            int len2 = other.Count;

            if (len1 != len2)
            {
                logFunc($"GFFList size mismatch at '{currentPath}': Old has {len1} structs, New has {len2} structs (diff: {len2 - len1:+d})");
                comparisonResult.AddFieldCountMismatch(currentPath, len1, len2);
            }

            // Report added structs
            if (addedKeys.Count > 0)
            {
                logFunc($"\n{addedKeys.Count} struct(s) added in new GFFList at '{currentPath}':");
                foreach (var key in addedKeys.OrderBy(k => newStructsMap[k][0]))
                {
                    List<int> indices = newStructsMap[key];
                    foreach (int idx in indices)
                    {
                        GFFStruct s = other._structs[idx];
                        logFunc($"  [New:{idx}] Struct#{s.StructId} (struct_id={s.StructId})");
                        logFunc("  Contents of new struct:");
                        foreach ((string label, GFFFieldType fieldType, object fieldValue) in s)
                        {
                            logFunc($"    {fieldType}: {label}: {FormatText(fieldValue?.ToString() ?? "null")}");
                        }
                        logFunc("");
                        reportedIndicesNew.Add(idx);
                    }
                }
                isSameResult = false;
                comparisonResult.AddFieldStat("extra", currentPath);
            }

            // Report removed structs
            if (removedKeys.Count > 0)
            {
                logFunc($"\n{removedKeys.Count} struct(s) removed from old GFFList at '{currentPath}':");
                foreach (var key in removedKeys.OrderBy(k => oldStructsMap[k][0]))
                {
                    List<int> indices = oldStructsMap[key];
                    foreach (int idx in indices)
                    {
                        GFFStruct s = _structs[idx];
                        logFunc($"  [Old:{idx}] Struct#{s.StructId} (struct_id={s.StructId})");
                        logFunc("  Contents of old struct:");
                        foreach ((string label, GFFFieldType fieldType, object fieldValue) in s)
                        {
                            logFunc($"    {fieldType}: {label}: {FormatText(fieldValue?.ToString() ?? "null")}");
                        }
                        logFunc("");
                        reportedIndicesOld.Add(idx);
                    }
                }
                isSameResult = false;
                comparisonResult.AddFieldStat("missing", currentPath);
            }

            // Detect moved/reordered structs
            int movedCount = 0;
            foreach (var key in commonKeys)
            {
                List<int> oldIndices = oldStructsMap[key];
                List<int> newIndices = newStructsMap[key];

                if (!oldIndices.OrderBy(x => x).SequenceEqual(newIndices.OrderBy(x => x)))
                {
                    if (movedCount == 0)
                    {
                        logFunc($"\nStructs moved/reordered in GFFList at '{currentPath}':");
                    }
                    movedCount++;
                    int structId = key.Item1;
                    string oldIndicesStr = string.Join(", ", oldIndices.OrderBy(x => x));
                    string newIndicesStr = string.Join(", ", newIndices.OrderBy(x => x));
                    logFunc($"  Struct#{structId}: moved from index [{oldIndicesStr}] to [{newIndicesStr}]");
                    foreach (int i in oldIndices) reportedIndicesOld.Add(i);
                    foreach (int i in newIndices) reportedIndicesNew.Add(i);
                }
            }

            if (movedCount > 0)
            {
                logFunc("");
                isSameResult = false;
                comparisonResult.AddFieldStat("mismatched", currentPath);
            }

            // Check for structs at same index that have different content
            int modifiedCount = 0;
            int maxIndex = Math.Min(len1, len2);
            for (int idx = 0; idx < maxIndex; idx++)
            {
                if (reportedIndicesOld.Contains(idx) || reportedIndicesNew.Contains(idx))
                {
                    continue;
                }

                GFFStruct oldStruct = _structs[idx];
                GFFStruct newStruct = other._structs[idx];

                var oldKey = StructKey(oldStruct);
                var newKey = StructKey(newStruct);

                if (!oldKey.Equals(newKey))
                {
                    if (modifiedCount == 0)
                    {
                        logFunc($"\nStructs modified at same index in GFFList at '{currentPath}':");
                    }
                    modifiedCount++;
                    logFunc($"  [{idx}] Old: Struct#{oldStruct.StructId}");
                    logFunc($"  [{idx}] New: Struct#{newStruct.StructId}");
                    if (!oldStruct.Compare(newStruct, logFunc, $"{currentPath}/{idx}", ignoreDefaultChanges, ignoreValues, comparisonResult))
                    {
                        isSameResult = false;
                    }
                    reportedIndicesOld.Add(idx);
                    reportedIndicesNew.Add(idx);
                }
            }

            // For structs at same index with same content, still do comparison to catch nested differences
            for (int idx = 0; idx < maxIndex; idx++)
            {
                if (reportedIndicesOld.Contains(idx) || reportedIndicesNew.Contains(idx))
                {
                    continue;
                }

                GFFStruct oldStruct = _structs[idx];
                GFFStruct newStruct = other._structs[idx];

                if (!oldStruct.Compare(newStruct, logFunc, $"{currentPath}/{idx}", ignoreDefaultChanges, ignoreValues, comparisonResult))
                {
                    isSameResult = false;
                }
            }

            if (modifiedCount > 0)
            {
                comparisonResult.AddFieldStat("mismatched", currentPath);
            }

            bool hasDifferences = addedKeys.Count > 0 || removedKeys.Count > 0 || movedCount > 0 || modifiedCount > 0;
            if (hasDifferences)
            {
                logFunc($"\nGFFList Summary at '{currentPath}': {addedKeys.Count} added, {removedKeys.Count} removed, {movedCount} moved/reordered, {modifiedCount} modified");
            }

            return !hasDifferences;
        }

        private static (int structId, string fieldsKey) StructKey(GFFStruct s)
        {
            var fields = new List<string>();
            foreach ((string label, GFFFieldType fieldType, object value) in s)
            {
                string hashable = HashableValue(value);
                fields.Add($"{label}:{fieldType}:{hashable}");
            }
            fields.Sort();
            return (s.StructId, string.Join("|", fields));
        }

        private static string HashableValue(object value)
        {
            if (value == null) return "null";
            if (value is int || value is float || value is double || value is bool || value is string || value is byte[])
            {
                return value.ToString();
            }
            if (value is ResRef resRef)
            {
                return $"ResRef:{resRef}";
            }
            if (value is Vector3 v3)
            {
                return $"Vector3:{v3.X},{v3.Y},{v3.Z}";
            }
            if (value is Vector4 v4)
            {
                return $"Vector4:{v4.X},{v4.Y},{v4.Z},{v4.W}";
            }
            if (value is LocalizedString locStr)
            {
                return $"LocalizedString:{locStr.StringRef}";
            }
            if (value is GFFStruct gffStruct)
            {
                return StructKey(gffStruct).ToString();
            }
            if (value is GFFList gffList)
            {
                var keys = new List<string>();
                foreach (GFFStruct item in gffList)
                {
                    keys.Add(StructKey(item).ToString());
                }
                return $"List:[{string.Join(",", keys)}]";
            }
            return value.ToString();
        }

        private static string FormatText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text.Length > 80) return text.Substring(0, 77) + "...";
            return text;
        }
    }
}
