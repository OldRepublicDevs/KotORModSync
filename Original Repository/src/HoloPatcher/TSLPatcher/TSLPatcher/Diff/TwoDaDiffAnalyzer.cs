using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KOTORModSync.Formats.TwoDA;
using KOTORModSync.Mods.TwoDA;

namespace KOTORModSync.Diff
{

    /// <summary>
    /// 1:1 port of TwoDADiffAnalyzer from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/analyzers.py.
    /// </summary>
    public class TwoDaDiffAnalyzer
    {
        public Modifications2DA Analyze(byte[] leftData, byte[] rightData, string identifier)
        {
            TwoDA left2da;
            TwoDA right2da;
            try
            {
                left2da = new TwoDABinaryReader(leftData).Load();
                right2da = new TwoDABinaryReader(rightData).Load();
            }
            catch (Exception)
            {
                return null;
            }

            string filename = Path.GetFileName(identifier ?? string.Empty);
            var modifications = new Modifications2DA(filename);

            AnalyzeColumns(left2da, right2da, modifications);
            AnalyzeRows(left2da, right2da, modifications);

            return modifications.Modifiers.Count > 0 ? modifications : null;
        }

        private static void AnalyzeColumns(
            TwoDA left2da,
            TwoDA right2da,
            Modifications2DA modifications)
        {
            var leftHeaders = new HashSet<string>(left2da.GetHeaders());
            var rightHeaders = new HashSet<string>(right2da.GetHeaders());

            var addedColumns = new List<string>(rightHeaders);
            addedColumns.RemoveAll(leftHeaders.Contains);
            addedColumns.Sort(StringComparer.Ordinal);

            int colIdx = 0;
            foreach (string columnName in addedColumns)
            {
                List<string> columnData = right2da.GetColumn(columnName);
                string defaultValue = DetermineDefaultValue(columnData);

                string addColumnId = string.Format(
                    "{0}_{1}_addcol_{2}",
                    modifications.SourceFile,
                    columnName,
                    colIdx);

                var addColumn = new AddColumn2DA(
                    addColumnId,
                    columnName,
                    defaultValue,
                    new Dictionary<int, RowValue>(),
                    new Dictionary<string, RowValue>(),
                    new Dictionary<int, string>());

                int rowHeight = right2da.GetHeight();
                for (int rowIndex = 0; rowIndex < rowHeight; rowIndex++)
                {
                    string cellValue = right2da.GetCellString(rowIndex, columnName);
                    if (cellValue != defaultValue)
                    {
                        addColumn.IndexInsert[rowIndex] = new RowValueConstant(cellValue);
                    }
                }

                modifications.Modifiers.Add(addColumn);
                colIdx++;
            }
        }

        private static void AnalyzeRows(
            TwoDA left2da,
            TwoDA right2da,
            Modifications2DA modifications)
        {
            int leftHeight = left2da.GetHeight();
            int rightHeight = right2da.GetHeight();
            List<string> commonHeaders = left2da.GetHeaders()
                .Where(header => right2da.GetHeaders().Contains(header))
                .ToList();

            int minHeight = leftHeight < rightHeight ? leftHeight : rightHeight;
            int changeRowCounter = 0;

            for (int rowIndex = 0; rowIndex < minHeight; rowIndex++)
            {
                var changedCells = new Dictionary<string, RowValue>();
                foreach (string header in commonHeaders)
                {
                    string leftValue = left2da.GetCellString(rowIndex, header);
                    string rightValue = right2da.GetCellString(rowIndex, header);
                    if (leftValue != rightValue)
                    {
                        changedCells[header] = new RowValueConstant(rightValue);
                    }
                }

                if (changedCells.Count > 0)
                {
                    string changeRowId = string.Format(
                        "{0}_changerow_{1}",
                        modifications.SourceFile,
                        changeRowCounter);
                    changeRowCounter++;

                    string leftLabel = SafeGetLabel(left2da, rowIndex);
                    string rightLabel = SafeGetLabel(right2da, rowIndex);
                    int targetRowIndex = ResolveRowIndexValue(rowIndex, rightLabel, leftLabel);

                    var changeRow = new ChangeRow2DA(
                        changeRowId,
                        new Target(TargetType.ROW_INDEX, targetRowIndex),
                        changedCells,
                        new Dictionary<int, RowValue>(),
                        new Dictionary<int, RowValue>());

                    modifications.Modifiers.Add(changeRow);
                }
            }

            if (rightHeight > leftHeight)
            {
                int addRowCounter = 0;
                for (int rowIndex = leftHeight; rowIndex < rightHeight; rowIndex++, addRowCounter++)
                {
                    var cells = new Dictionary<string, RowValue>();
                    string rowLabel = SafeGetLabel(right2da, rowIndex);

                    List<string> headers = right2da.GetHeaders();
                    foreach (string header in headers)
                    {
                        string cellValue = right2da.GetCellString(rowIndex, header);
                        if (!string.IsNullOrEmpty(cellValue))
                        {
                            cells[header] = new RowValueConstant(cellValue);
                        }
                    }

                    string addRowId = string.Format(
                        "{0}_addrow_{1}",
                        modifications.SourceFile,
                        addRowCounter);

                    var addRow = new AddRow2DA(
                        addRowId,
                        null,
                        rowLabel,
                        cells,
                        new Dictionary<int, RowValue>(),
                        new Dictionary<int, RowValue>());

                    modifications.Modifiers.Add(addRow);
                }
            }
        }

        private static string SafeGetLabel(TwoDA twoda, int rowIndex)
        {
            try
            {
                return twoda.GetLabel(rowIndex);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static int ResolveRowIndexValue(int fallbackIndex, params string[] labels)
        {
            foreach (string label in labels)
            {
                int? numeric = ParseNumericRowLabel(label);
                if (numeric.HasValue)
                {
                    return numeric.Value;
                }
            }

            return fallbackIndex;
        }

        private static int? ParseNumericRowLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return null;
            }

            string stripped = label.Trim();
            if (stripped.Length == 0)
            {
                return null;
            }

            char first = stripped[0];
            if ((first == '+' || first == '-') && stripped.Length > 1)
            {
                string digits = stripped.Substring(1);
                if (IsDigits(digits) && int.TryParse(stripped, out int parsedSigned))
                {
                    return parsedSigned;
                }
                return null;
            }

            if (IsDigits(stripped) && int.TryParse(stripped, out int parsed))
            {
                return parsed;
            }

            return null;
        }

        private static bool IsDigits(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                if (!char.IsDigit(value[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static string DetermineDefaultValue(List<string> columnData)
        {
            if (columnData == null || columnData.Count == 0)
            {
                return "****";
            }

            var valueCounts = new Dictionary<string, int>();
            var firstIndex = new Dictionary<string, int>();
            var keysInOrder = new List<string>();

            for (int i = 0; i < columnData.Count; i++)
            {
                string value = columnData[i];
                if (!valueCounts.ContainsKey(value))
                {
                    valueCounts[value] = 0;
                    firstIndex[value] = i;
                    keysInOrder.Add(value);
                }

                valueCounts[value] = valueCounts[value] + 1;
            }

            int starCount = valueCounts.ContainsKey("****") ? valueCounts["****"] : 0;
            int quarterThreshold = columnData.Count / 4;
            bool hasStars = valueCounts.ContainsKey("****");
            bool isCommonStar = hasStars && starCount > quarterThreshold;
            if (isCommonStar)
            {
                return "****";
            }

            if (valueCounts.Count > 0)
            {
                string mostCommonValue = null;
                int mostCommonCount = -1;

                foreach (string key in keysInOrder)
                {
                    int count = valueCounts[key];
                    bool chooseValue = count > mostCommonCount;
                    if (!chooseValue && mostCommonValue != null && count == mostCommonCount && firstIndex[key] < firstIndex[mostCommonValue])
                    {
                        chooseValue = true;
                    }

                    if (mostCommonValue == null)
                    {
                        chooseValue = true;
                    }

                    if (chooseValue)
                    {
                        mostCommonValue = key;
                        mostCommonCount = count;
                    }
                }

                double halfThreshold = columnData.Count / 2.0;
                if (mostCommonCount > halfThreshold)
                {
                    return mostCommonValue;
                }

                if (mostCommonValue == string.Empty && mostCommonCount > quarterThreshold)
                {
                    return string.Empty;
                }

                return "****";
            }

            return "****";
        }
    }
}

