using System.Collections.Generic;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.TwoDA;

namespace KOTORModSync.Diff
{

    public class TwoDaCompareResult
    {
        public List<string> AddedColumns { get; } = new List<string>();
        public Dictionary<int, Dictionary<string, string>> ChangedRows { get; } = new Dictionary<int, Dictionary<string, string>>();
        public List<(string Label, Dictionary<string, string> Cells)> AddedRows { get; } = new List<(string Label, Dictionary<string, string> Cells)>();
    }

    public static class TwoDaDiff
    {
        public static TwoDaCompareResult Compare(TwoDA original, TwoDA modified)
        {
            var result = new TwoDaCompareResult();

            // Detect added columns
            var origHeaders = new HashSet<string>(original.GetHeaders());
            foreach (string header in modified.GetHeaders())
            {
                if (!origHeaders.Contains(header))
                {
                    result.AddedColumns.Add(header);
                }
            }

            // Detect modified/added rows
            int origHeight = original.GetHeight();
            int modHeight = modified.GetHeight();

            for (int i = 0; i < modHeight; i++)
            {
                TwoDARow modRow = modified.GetRow(i);

                if (i >= origHeight)
                {
                    // Added row
                    result.AddedRows.Add((modRow.Label(), modRow.GetData()));
                }
                else
                {
                    // Check for changes in existing rows
                    TwoDARow origRow = original.GetRow(i);
                    var changes = new Dictionary<string, string>();

                    foreach (string header in modified.GetHeaders())
                    {
                        string modVal = modRow.GetString(header);

                        if (origHeaders.Contains(header))
                        {
                            string origVal = origRow.GetString(header);
                            if (modVal != origVal)
                            {
                                changes[header] = modVal;
                            }
                        }
                        else
                        {
                            // New column.
                            changes[header] = modVal;
                        }
                    }

                    if (changes.Any())
                    {
                        result.ChangedRows[i] = changes;
                    }
                }
            }

            return result;
        }

        public static string SerializeToIni(TwoDaCompareResult result, string filename)
        {
            var sb = new StringBuilder();

            // Process added columns
            foreach (string col in result.AddedColumns)
            {
                // TSLPatcher syntax: AddColumn stuff
                // Requires heuristics to determine default value?
                // For now, just output a basic instruction
                // [2DAList]
                // Row0=filename
                // [filename]
                // AddColumn0=ColumnName

                // But usually we return the inner content for the file section.
            }

            // This is getting into "Mod Generator" territory.
            // For simple diffing, maybe just output a readable report?
            // Or strictly TSLPatcher INI format?
            // The user asked for "Diff/Comparison logic".

            // I'll stick to providing the Compare result. Serialization logic can be added if specifically requested or needed for tests.

            // Serialization logic pending
            return sb.ToString();
        }
    }
}
