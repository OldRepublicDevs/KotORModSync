using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace KOTORModSync.Formats.TwoDA
{

    /// <summary>
    /// Represents a 2DA file.
    /// </summary>
    public class TwoDA : IEnumerable<TwoDARow>
    {
        private readonly List<Dictionary<string, string>> _rows = new List<Dictionary<string, string>>();
        private readonly List<string> _headers = new List<string>();
        private readonly List<string> _labels = new List<string>();
        private readonly Dictionary<string, string> _columnDefaults = new Dictionary<string, string>();

        public TwoDA([CanBeNull] List<string> headers = null)
        {
            if (headers != null)
            {
                _headers.AddRange(headers);
            }
        }

        public int GetHeight() => _rows.Count;
        public int GetWidth() => _headers.Count;

        public List<string> GetHeaders() => new List<string>(_headers);
        public List<string> GetLabels() => new List<string>(_labels);
        
        // Properties for compatibility with Utilities.cs
        public List<string> Headers => GetHeaders();
        public List<TwoDARow> Rows
        {
            get
            {
                var result = new List<TwoDARow>();
                for (int i = 0; i < _rows.Count; i++)
                {
                    result.Add(new TwoDARow(GetLabel(i), _rows[i]));
                }
                return result;
            }
        }

        public string GetLabel(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _labels.Count)
            {
                throw new IndexOutOfRangeException($"Row index {rowIndex} is out of range");
            }

            return _labels[rowIndex];
        }

        public int GetRowIndex(string rowLabel)
        {
            for (int i = 0; i < _labels.Count; i++)
            {
                if (_labels[i].Equals(rowLabel, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            throw new KeyNotFoundException($"Row label '{rowLabel}' not found");
        }

        public void SetLabel(int rowIndex, string value)
        {
            if (rowIndex < 0 || rowIndex >= _labels.Count)
            {
                throw new IndexOutOfRangeException($"Row index {rowIndex} is out of range");
            }

            _labels[rowIndex] = value;
        }

        public List<string> GetColumn(string header)
        {
            if (!_headers.Contains(header))
            {
                throw new KeyNotFoundException($"The header '{header}' does not exist.");
            }

            var result = new List<string>();
            for (int i = 0; i < GetHeight(); i++)
            {
                result.Add(_rows[i][header]);
            }
            return result;
        }

        public void AddColumn(string header)
        {
            if (_headers.Contains(header))
            {
                throw new InvalidOperationException($"The header '{header}' already exists.");
            }

            _headers.Add(header);
            if (!_columnDefaults.ContainsKey(header))
            {
                _columnDefaults[header] = "";
            }
            foreach (Dictionary<string, string> row in _rows)
            {
                row[header] = "";
            }
        }

        public void RemoveColumn(string header)
        {
            if (_headers.Contains(header))
            {
                foreach (Dictionary<string, string> row in _rows)
                {
                    row.Remove(header);
                }
                _headers.Remove(header);
            }
        }

        public TwoDARow GetRow(int rowIndex, [CanBeNull] string context = null)
        {
            try
            {
                string label = GetLabel(rowIndex);
                return new TwoDARow(label, _rows[rowIndex]);
            }
            catch (IndexOutOfRangeException e)
            {
                throw new IndexOutOfRangeException(
                    $"Row index {rowIndex} not found in the 2DA." +
                    (context != null ? $" Context: {context}" : ""), e);
            }
        }

        [CanBeNull]
        public TwoDARow FindRow(string rowLabel)
        {
            return this.FirstOrDefault(row => row.Label() == rowLabel);
        }

        [CanBeNull]
        public int? RowIndex([NotNull] TwoDARow row)
        {
            int index = 0;
            foreach (TwoDARow searching in this)
            {
                if (searching.Equals(row))
                {
                    return index;
                }

                index++;
            }
            return null;
        }

        /// <summary>
        /// Finds the maximum numeric label and returns the next integer.
        /// </summary>
        public int LabelMax()
        {
            int maxFound = -1;
            foreach (string label in _labels)
            {
                if (int.TryParse(label, out int labelValue))
                {
                    maxFound = Math.Max(labelValue, maxFound);
                }
            }
            return maxFound + 1;
        }

        public int AddRow([CanBeNull] string rowLabel = null, [CanBeNull] Dictionary<string, object> cells = null)
        {
            var newRow = new Dictionary<string, string>();
            _rows.Add(newRow);
            _labels.Add(rowLabel ?? LabelMax().ToString());

            if (cells != null)
            {
                var convertedCells = new Dictionary<string, string>();
                foreach ((string key, object value) in cells)
                {
                    convertedCells[key] = value?.ToString() ?? "";
                }
                cells = convertedCells.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value);
            }

            foreach (string header in _headers)
            {
                string defaultValue = _columnDefaults.TryGetValue(header, out string def) ? def : "";
                newRow[header] = cells?.ContainsKey(header) == true
                    ? cells[header]?.ToString() ?? ""
                    : defaultValue;
            }

            return _rows.Count - 1;
        }

        public void RemoveRow(int rowIndex)
        {
            if (rowIndex >= 0 && rowIndex < _rows.Count)
            {
                _rows.RemoveAt(rowIndex);
                _labels.RemoveAt(rowIndex);
            }
        }

        public int CopyRow(int rowIndex, [CanBeNull] string newLabel = null)
        {
            TwoDARow row = GetRow(rowIndex);
            var cellsCopy = new Dictionary<string, object>();
            foreach ((string header, string value) in row.GetData())
            {
                cellsCopy[header] = value;
            }
            return AddRow(newLabel ?? row.Label(), cellsCopy);
        }

        public int CopyRow(TwoDARow sourceRow, [CanBeNull] string rowLabel = null, [CanBeNull] Dictionary<string, object> overrideCells = null)
        {
            int? sourceIndex = RowIndex(sourceRow);

            var newRow = new Dictionary<string, string>();
            _rows.Add(newRow);
            _labels.Add(rowLabel ?? _rows.Count.ToString());

            overrideCells = overrideCells ?? new Dictionary<string, object>();
            var convertedCells = new Dictionary<string, string>();
            foreach ((string key, object value) in overrideCells)
            {
                convertedCells[key] = value?.ToString() ?? "";
            }

            foreach (string header in _headers)
            {
                newRow[header] = convertedCells.ContainsKey(header)
                    ? convertedCells[header]
                    : (sourceIndex.HasValue ? GetCellString(sourceIndex.Value, header) : "");
            }

            return _rows.Count - 1;
        }

        public void SetColumnDefault(string header, string defaultValue)
        {
            _columnDefaults[header] = defaultValue ?? "";
        }

        public string GetColumnDefault(string header)
        {
            return _columnDefaults.TryGetValue(header, out string value) ? value : "";
        }

        public string GetCellString(int rowIndex, string header, [CanBeNull] string context = null)
        {
            try
            {
                return _rows[rowIndex][header];
            }
            catch (KeyNotFoundException)
            {
                throw new KeyNotFoundException(
                    $"The header '{header}' does not exist in row {rowIndex}." +
                    (context != null ? $" Context: {context}" : ""));
            }
        }

        public string GetCellString(string rowLabel, string header, [CanBeNull] string context = null)
        {
            int rowIndex = GetRowIndex(rowLabel);
            return GetCellString(rowIndex, header, context);
        }

        public int? GetCellInt(int rowIndex, string header, int? defaultValue = null)
        {
            string cellValue = GetCellString(rowIndex, header);
            if (string.IsNullOrWhiteSpace(cellValue) || cellValue == "****")
            {
                return defaultValue;
            }

            if (int.TryParse(cellValue, out int result))
            {
                return result;
            }

            return defaultValue;
        }

        public float? GetCellFloat(int rowIndex, string header, float? defaultValue = null)
        {
            string cellValue = GetCellString(rowIndex, header);
            if (string.IsNullOrWhiteSpace(cellValue) || cellValue == "****")
            {
                return defaultValue;
            }

            if (float.TryParse(cellValue, out float result))
            {
                return result;
            }

            return defaultValue;
        }

        public void SetCellString(int rowIndex, string header, string value)
        {
            if (!_headers.Contains(header))
            {
                throw new KeyNotFoundException($"The header '{header}' does not exist.");
            }

            _rows[rowIndex][header] = value;
        }

        public void SetCellInt(int rowIndex, string header, int value)
        {
            SetCellString(rowIndex, header, value.ToString());
        }

        public void SetCellFloat(int rowIndex, string header, float value)
        {
            SetCellString(rowIndex, header, value.ToString());
        }

        /// <summary>
        /// Compares this TwoDA with another TwoDA instance.
        /// Ported from vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/twoda/twoda_data.py:854.
        /// </summary>
        public bool Compare(TwoDA other, Action<string> logFunc = null)
        {
            Action<string> logger = logFunc ?? (message => Console.WriteLine(message));

            var oldHeaders = new HashSet<string>(_headers);
            var newHeaders = new HashSet<string>(other.GetHeaders());
            bool ret = true;

            var missingHeaders = new HashSet<string>(oldHeaders);
            missingHeaders.ExceptWith(newHeaders);
            if (missingHeaders.Count > 0)
            {
                logger($"Missing headers in new TwoDA: {string.Join(", ", missingHeaders)}");
                ret = false;
            }

            var extraHeaders = new HashSet<string>(newHeaders);
            extraHeaders.ExceptWith(oldHeaders);
            if (extraHeaders.Count > 0)
            {
                logger($"Extra headers in new TwoDA: {string.Join(", ", extraHeaders)}");
                ret = false;
            }

            if (!ret)
            {
                return false;
            }

            var commonHeaders = new HashSet<string>(oldHeaders);
            commonHeaders.IntersectWith(newHeaders);

            var oldIndices = new HashSet<int?>();
            foreach (TwoDARow row in this)
            {
                oldIndices.Add(RowIndex(row));
            }

            var newIndices = new HashSet<int?>();
            foreach (TwoDARow row in other)
            {
                newIndices.Add(other.RowIndex(row));
            }

            var missingRows = new HashSet<int?>(oldIndices);
            missingRows.ExceptWith(newIndices);
            if (missingRows.Count > 0)
            {
                logger($"Missing rows in new TwoDA: {string.Join(", ", missingRows)}");
                ret = false;
            }

            var extraRows = new HashSet<int?>(newIndices);
            extraRows.ExceptWith(oldIndices);
            if (extraRows.Count > 0)
            {
                logger($"Extra rows in new TwoDA: {string.Join(", ", extraRows)}");
                ret = false;
            }

            foreach (int? index in oldIndices.Intersect(newIndices))
            {
                if (!index.HasValue)
                {
                    logger("Row mismatch");
                    return false;
                }

                TwoDARow oldRow = GetRow(index.Value);
                TwoDARow newRow = other.GetRow(index.Value);
                foreach (string header in commonHeaders)
                {
                    string oldValue = oldRow.GetString(header);
                    string newValue = newRow.GetString(header);
                    if (oldValue != newValue)
                    {
                        logger($"Cell mismatch at RowIndex '{index.Value}' Header '{header}': '{oldValue}' --> '{newValue}'");
                        ret = false;
                    }
                }
            }

            return ret;
        }

        public IEnumerator<TwoDARow> GetEnumerator()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                yield return new TwoDARow(GetLabel(i), _rows[i]);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Serializes the 2DA to a byte array.
        /// </summary>
        public byte[] ToBytes()
        {
            var writer = new TwoDABinaryWriter(this);
            return writer.Write();
        }

        /// <summary>
        /// Deserializes a 2DA from a byte array.
        /// </summary>
        public static TwoDA FromBytes(byte[] data)
        {
            var reader = new TwoDABinaryReader(data);
            return reader.Load();
        }

        /// <summary>
        /// Saves the 2DA to a file.
        /// </summary>
        public void Save(string path)
        {
            var writer = new TwoDABinaryWriter(this);
            byte[] data = writer.Write();
            System.IO.File.WriteAllBytes(path, data);
        }
    }
}

