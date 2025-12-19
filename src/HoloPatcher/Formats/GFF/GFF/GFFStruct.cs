using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using KOTORModSync;
using KOTORModSync.Common;
using JetBrains.Annotations;

namespace KOTORModSync.Formats.GFF
{

    /// <summary>
    /// Stores a collection of GFF fields.
    /// </summary>
    public class GFFStruct : IEnumerable<(string label, GFFFieldType fieldType, object value)>
    {
        public int StructId { get; set; }

        public GFFStruct(int structId = 0)
        {
            StructId = structId;
        }

        private readonly Dictionary<string, GFFField> _fields = new Dictionary<string, GFFField>();

        public int Count => _fields.Count;

        public bool Exists(string label) => _fields.ContainsKey(label);

        public void Remove(string label)
        {
            _fields.Remove(label);
        }

        public GFFFieldType? GetFieldType(string label)
        {
            // Can be null if field not found
            if (_fields.TryGetValue(label, out GFFField field))
            {
                return field.FieldType;
            }
            return null;
        }

        public bool TryGetFieldType(string label, out GFFFieldType fieldType)
        {
            // Can be null if field not found
            if (_fields.TryGetValue(label, out GFFField field))
            {
                fieldType = field.FieldType;
                return true;
            }
            fieldType = default;
            return false;
        }

        [CanBeNull]
        public object GetValue(string label, [CanBeNull] object defaultValue = null)
        {
            // Can be null if field not found
            return _fields.TryGetValue(label, out GFFField field) ? field.Value : defaultValue;
        }

        [CanBeNull]
        public T Acquire<T>(string label, [CanBeNull] T defaultValue = default)
        {
            // Can be null if field not found
            if (!_fields.TryGetValue(label, out GFFField field))
            {
                return defaultValue;
            }

            return field.Value is T value ? value : defaultValue;
        }

        // Getters for specific types
        public byte GetUInt8(string label) => Convert.ToByte(GetValue(label, (byte)0));
        public sbyte GetInt8(string label) => Convert.ToSByte(GetValue(label, (sbyte)0));
        public ushort GetUInt16(string label) => Convert.ToUInt16(GetValue(label, (ushort)0));
        public short GetInt16(string label) => Convert.ToInt16(GetValue(label, (short)0));
        public uint GetUInt32(string label) => Convert.ToUInt32(GetValue(label, 0u));
        public int GetInt32(string label) => Convert.ToInt32(GetValue(label, 0));
        public ulong GetUInt64(string label) => Convert.ToUInt64(GetValue(label, 0ul));
        public long GetInt64(string label) => Convert.ToInt64(GetValue(label, 0L));
        public float GetSingle(string label) => Convert.ToSingle(GetValue(label, 0f));
        public double GetDouble(string label) => Convert.ToDouble(GetValue(label, 0.0));
        public string GetString(string label) => GetValue(label)?.ToString() ?? string.Empty;
        public ResRef GetResRef(string label) => GetValue(label) as ResRef ?? ResRef.FromBlank();
        public LocalizedString GetLocString(string label) => GetValue(label) as LocalizedString ?? LocalizedString.FromInvalid();
        public byte[] GetBinary(string label) => GetValue(label) as byte[] ?? Array.Empty<byte>();
        public System.Numerics.Vector3 GetVector3(string label) => (System.Numerics.Vector3)(GetValue(label) ?? System.Numerics.Vector3.Zero);
        public System.Numerics.Vector4 GetVector4(string label) => (System.Numerics.Vector4)(GetValue(label) ?? System.Numerics.Vector4.Zero);
        public GFFStruct GetStruct(string label) => GetValue(label) as GFFStruct ?? new GFFStruct();
        public GFFList GetList(string label) => GetValue(label) as GFFList ?? new GFFList();

        public bool TryGetLocString(string label, out LocalizedString value)
        {
            // Can be null if field not found
            if (_fields.TryGetValue(label, out GFFField field) && field.Value is LocalizedString locString)
            {
                value = locString;
                return true;
            }
            value = null;
            return false;
        }

        public bool TryGetStruct(string label, out GFFStruct value)
        {
            // Can be null if field not found
            if (_fields.TryGetValue(label, out GFFField field) && field.Value is GFFStruct gffStruct)
            {
                value = gffStruct;
                return true;
            }
            value = null;
            return false;
        }

        public bool TryGetList(string label, out GFFList value)
        {
            // Can be null if field not found
            if (_fields.TryGetValue(label, out GFFField field) && field.Value is GFFList gffList)
            {
                value = gffList;
                return true;
            }
            value = null;
            return false;
        }

        // Setters for specific types
        public void SetUInt8(string label, byte value) => SetField(label, GFFFieldType.UInt8, value);
        public void SetInt8(string label, sbyte value) => SetField(label, GFFFieldType.Int8, value);
        public void SetUInt16(string label, ushort value) => SetField(label, GFFFieldType.UInt16, value);
        public void SetInt16(string label, short value) => SetField(label, GFFFieldType.Int16, value);
        public void SetUInt32(string label, uint value) => SetField(label, GFFFieldType.UInt32, value);
        public void SetInt32(string label, int value) => SetField(label, GFFFieldType.Int32, value);
        public void SetUInt64(string label, ulong value) => SetField(label, GFFFieldType.UInt64, value);
        public void SetInt64(string label, long value) => SetField(label, GFFFieldType.Int64, value);
        public void SetSingle(string label, float value) => SetField(label, GFFFieldType.Single, value);
        public void SetDouble(string label, double value) => SetField(label, GFFFieldType.Double, value);
        public void SetString(string label, string value) => SetField(label, GFFFieldType.String, value);
        public void SetResRef(string label, ResRef value) => SetField(label, GFFFieldType.ResRef, value);
        public void SetLocString(string label, LocalizedString value) => SetField(label, GFFFieldType.LocalizedString, value);
        public void SetBinary(string label, byte[] value) => SetField(label, GFFFieldType.Binary, value);
        public void SetVector3(string label, System.Numerics.Vector3 value) => SetField(label, GFFFieldType.Vector3, value);
        public void SetVector4(string label, System.Numerics.Vector4 value) => SetField(label, GFFFieldType.Vector4, value);
        public void SetStruct(string label, GFFStruct value) => SetField(label, GFFFieldType.Struct, value);
        public void SetList(string label, GFFList value) => SetField(label, GFFFieldType.List, value);

        public void SetField(string label, GFFFieldType fieldType, object value)
        {
            _fields[label] = new GFFField(fieldType, value);
        }

        public IEnumerator<(string label, GFFFieldType fieldType, object value)> GetEnumerator()
        {
            foreach ((string label, GFFField field) in _fields)
            {
                yield return (label, field.FieldType, field.Value);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public object this[string label]
        {
            get => GetValue(label) ?? throw new KeyError(label, "Field does not exist in this GFFStruct");
            set
            {
                // Can be null if field not found
                if (_fields.TryGetValue(label, out GFFField field))
                {
                    _fields[label] = new GFFField(field.FieldType, value);
                }
                else
                {
                    throw new KeyError(label, "Cannot set field that doesn't exist. Use Set* methods to create fields.");
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/gff/gff_data.py:609-739
        // Original: def compare(self, other: object, log_func: Callable = print, current_path: PureWindowsPath | os.PathLike | str | None = None, ignore_default_changes: bool = False, ignore_values: dict[str, set[Any]] | None = None, comparison_result: GFFComparisonResult | None = None) -> bool:
        public bool Compare(GFFStruct other, Action<string> logFunc, string currentPath = null, bool ignoreDefaultChanges = false, Dictionary<string, HashSet<object>> ignoreValues = null, GFFComparisonResult comparisonResult = null)
        {
            HashSet<string> ignoreLabels = new HashSet<string> { "KTInfoDate", "KTGameVerIndex", "KTInfoVersion", "EditorInfo" };
            ignoreValues = ignoreValues ?? new Dictionary<string, HashSet<object>>();
            comparisonResult = comparisonResult ?? new GFFComparisonResult();
            currentPath = currentPath ?? "GFFRoot";

            bool IsIgnorableValue(string label, object v)
            {
                // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/gff/gff_data.py:644-646
                // Original: return not v or str(v) in {"0", "-1"} or (label in ignore_values and v in ignore_values[label])
                if (v == null) return true;

                // Check for empty strings, empty collections, zero values
                if (v is string str && string.IsNullOrEmpty(str)) return true;
                if (v is System.Collections.ICollection coll && coll.Count == 0) return true;

                string strVal = v.ToString();
                if (strVal == "0" || strVal == "-1" || string.IsNullOrEmpty(strVal)) return true;

                if (ignoreValues != null && ignoreValues.ContainsKey(label) && ignoreValues[label].Contains(v)) return true;
                return false;
            }

            bool IsIgnorableComparison(string label, object oldValue, object newValue)
            {
                return IsIgnorableValue(label, oldValue) && IsIgnorableValue(label, newValue);
            }

            if (other == null)
            {
                logFunc($"GFFStruct counts have changed at '{currentPath}': '{Count}' --> '<unknown>'");
                logFunc("");
                return false;
            }

            bool isSame = true;

            if (Count != other.Count && !ignoreDefaultChanges)
            {
                logFunc("");
                logFunc($"GFFStruct: number of fields have changed at '{currentPath}': '{Count}' --> '{other.Count}'");
                isSame = false;
            }

            if (StructId != other.StructId)
            {
                logFunc($"Struct ID is different at '{currentPath}': '{StructId}' --> '{other.StructId}'");
                isSame = false;
            }

            Dictionary<string, (GFFFieldType fieldType, object value)> oldDict = new Dictionary<string, (GFFFieldType, object)>();
            Dictionary<string, (GFFFieldType fieldType, object value)> newDict = new Dictionary<string, (GFFFieldType, object)>();

            int idx = 0;
            foreach ((string label, GFFFieldType ftype, object value) in this)
            {
                if (!ignoreLabels.Contains(label))
                {
                    string dictKey = label ?? $"gffstruct({idx})";
                    oldDict[dictKey] = (ftype, value);
                }
                idx++;
            }

            idx = 0;
            foreach ((string label, GFFFieldType ftype, object value) in other)
            {
                if (!ignoreLabels.Contains(label))
                {
                    string dictKey = label ?? $"gffstruct({idx})";
                    newDict[dictKey] = (ftype, value);
                }
                idx++;
            }

            HashSet<string> allLabels = new HashSet<string>(oldDict.Keys);
            foreach (string key in newDict.Keys)
            {
                allLabels.Add(key);
            }

            foreach (string label in allLabels)
            {
                string childPath = string.IsNullOrEmpty(currentPath) ? label : $"{currentPath}/{label}";
                oldDict.TryGetValue(label, out var oldEntry);
                newDict.TryGetValue(label, out var newEntry);

                GFFFieldType? oldFtype = oldEntry.fieldType;
                object oldValue = oldEntry.value;
                GFFFieldType? newFtype = newEntry.fieldType;
                object newValue = newEntry.value;

                if (ignoreDefaultChanges && IsIgnorableComparison(label, oldValue, newValue))
                {
                    continue;
                }

                if (oldFtype == null || oldValue == null)
                {
                    if (newFtype == null)
                    {
                        throw new InvalidOperationException($"newFtype shouldn't be None here. Relevance: oldFtype={oldFtype}, oldValue={oldValue}, newValue={newValue}");
                    }
                    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/gff/gff_data.py:689-695
                    // Original: if old_ftype is None or old_value is None: ... log_func(...) ... continue
                    // If ignore_default_changes is true and the new value is ignorable, skip it
                    if (ignoreDefaultChanges && IsIgnorableValue(label, newValue))
                    {
                        continue;
                    }
                    logFunc($"Extra '{newFtype.Value}' field found at '{childPath}': {FormatText(SafeRepr(newValue))}");
                    comparisonResult.AddFieldStat("extra", label);
                    isSame = false;
                    continue;
                }

                if (newValue == null || newFtype == null)
                {
                    logFunc($"Missing '{oldFtype.Value}' field at '{childPath}': {FormatText(SafeRepr(oldValue))}");
                    comparisonResult.AddFieldStat("missing", label);
                    isSame = false;
                    continue;
                }

                if (oldFtype != newFtype)
                {
                    logFunc($"Field type is different at '{childPath}': '{oldFtype.Value}'-->'{newFtype.Value}'");
                    comparisonResult.AddFieldStat("mismatched", label);
                    comparisonResult.AddValueMismatch(childPath, "field_type", oldFtype.Value.ToString(), newFtype.Value.ToString());
                    isSame = false;
                    continue;
                }

                if (oldFtype == GFFFieldType.Struct)
                {
                    GFFStruct oldStruct = oldValue as GFFStruct;
                    GFFStruct newStruct = newValue as GFFStruct;
                    if (oldStruct == null || newStruct == null)
                    {
                        logFunc($"Struct type mismatch at '{childPath}'");
                        isSame = false;
                        continue;
                    }

                    if (oldStruct.StructId != newStruct.StructId)
                    {
                        logFunc($"Struct ID is different at '{childPath}': '{oldStruct.StructId}'-->'{newStruct.StructId}'");
                        comparisonResult.AddStructIdMismatch(childPath, oldStruct.StructId, newStruct.StructId);
                        isSame = false;
                    }

                    if (!oldStruct.Compare(newStruct, logFunc, childPath, ignoreDefaultChanges, ignoreValues, comparisonResult))
                    {
                        isSame = false;
                    }
                }
                else if (oldFtype == GFFFieldType.List)
                {
                    GFFList oldList = oldValue as GFFList;
                    GFFList newList = newValue as GFFList;
                    if (oldList == null || newList == null)
                    {
                        logFunc($"List type mismatch at '{childPath}'");
                        isSame = false;
                        continue;
                    }

                    if (!oldList.Compare(newList, logFunc, childPath, ignoreDefaultChanges, ignoreValues, comparisonResult))
                    {
                        isSame = false;
                    }
                }
                else if (!ValuesAreEqual(oldValue, newValue))
                {
                    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/gff/gff_data.py:723-735
                    // Original: elif old_value != new_value: ... (with float comparison and string comparison checks)
                    if (oldValue is float oldFloat && newValue is float newFloat && Math.Abs(oldFloat - newFloat) < 0.0001f)
                    {
                        comparisonResult.AddFieldStat("used", label);
                        continue;
                    }

                    // Check if both values are 0 or default (for numeric types)
                    if (ignoreDefaultChanges)
                    {
                        bool oldIsZero = IsZeroOrDefault(oldValue);
                        bool newIsZero = IsZeroOrDefault(newValue);
                        if (oldIsZero && newIsZero)
                        {
                            continue; // Both are zero/default, ignore the difference
                        }
                    }

                    if (oldValue.ToString() == newValue.ToString())
                    {
                        logFunc($"Field '{oldFtype.Value}' is different at '{childPath}': String representations match, but have other properties that don't (such as a lang id difference).");
                        continue;
                    }

                    logFunc($"Field '{oldFtype.Value}' is different at '{childPath}':");
                    logFunc(FormatDiff(oldValue, newValue, label));
                    comparisonResult.AddFieldStat("mismatched", label);
                    comparisonResult.AddValueMismatch(childPath, oldFtype.Value.ToString(), oldValue, newValue);
                    isSame = false;
                }
                else
                {
                    comparisonResult.AddFieldStat("used", label);
                }
            }

            return isSame;
        }

        private static bool ValuesAreEqual(object v1, object v2)
        {
            if (v1 == null && v2 == null) return true;
            if (v1 == null || v2 == null) return false;
            if (v1.Equals(v2)) return true;
            return false;
        }

        private static bool IsZeroOrDefault(object v)
        {
            if (v == null) return true;
            if (v is byte b && b == 0) return true;
            if (v is sbyte sb && sb == 0) return true;
            if (v is ushort us && us == 0) return true;
            if (v is short s && s == 0) return true;
            if (v is uint ui && ui == 0) return true;
            if (v is int i && i == 0) return true;
            if (v is ulong ul && ul == 0) return true;
            if (v is long l && l == 0) return true;
            if (v is float f && Math.Abs(f) < 0.0001f) return true;
            if (v is double d && Math.Abs(d) < 0.0001) return true;
            if (v is string str && string.IsNullOrEmpty(str)) return true;
            if (v is ResRef resRef && string.IsNullOrEmpty(resRef.ToString())) return true;
            return false;
        }

        private static string FormatText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text.Length > 80) return text.Substring(0, 77) + "...";
            return text;
        }

        private static string SafeRepr(object obj)
        {
            if (obj == null) return "null";
            try
            {
                return obj.ToString();
            }
            catch
            {
                return "<repr failed>";
            }
        }

        private static string FormatDiff(object oldValue, object newValue, string name)
        {
            string oldStr = oldValue?.ToString() ?? "null";
            string newStr = newValue?.ToString() ?? "null";
            return $"--- (old){name}\n+++ (new){name}\n@@ -1 +1 @@\n-{oldStr}\n+{newStr}";
        }

        /// <summary>
        /// Internal field storage class
        /// </summary>
        private class GFFField
        {
            public GFFFieldType FieldType { get; }
            public object Value { get; }

            public GFFField(GFFFieldType fieldType, object value)
            {
                FieldType = fieldType;
                Value = value;
            }
        }
    }

    public class KeyError : Exception
    {
        public KeyError(string key, string message) : base($"Key '{key}': {message}") { }
    }
}

