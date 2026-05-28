using System;
using System.Collections.Generic;
using KOTORModSync.Mods.GFF;
using KOTORModSync.Mods.SSF;
using KOTORModSync.Mods.TLK;
using KOTORModSync.Mods.TwoDA;

namespace KOTORModSync.Diff
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/analyzers.py:89-104
    // Original: class DiffAnalyzer(ABC):
    /// <summary>
    /// Abstract base interface for diff analyzers.
    /// </summary>
    public interface IDiffAnalyzer
    {
        /// <summary>
        /// Analyze differences and return a PatcherModifications object.
        /// TLK analyzers may return a tuple of (ModificationsTLK, strref_mappings).
        /// All other analyzers return PatcherModifications | None.
        /// </summary>
        object Analyze(byte[] leftData, byte[] rightData, string identifier);
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/analyzers.py:845-869
    // Original: class DiffAnalyzerFactory:
    /// <summary>
    /// Factory for creating appropriate diff analyzers.
    /// </summary>
    public static class DiffAnalyzerFactory
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/analyzers.py:848-869
        // Original: @staticmethod def get_analyzer(resource_type: str) -> DiffAnalyzer | None:
        /// <summary>
        /// Get the appropriate analyzer for a resource type.
        /// </summary>
        public static IDiffAnalyzer GetAnalyzer(string resourceType)
        {
            if (string.IsNullOrEmpty(resourceType))
            {
                return null;
            }

            string resourceTypeLower = resourceType.ToLowerInvariant();

            // 2DA analyzer
            if (resourceTypeLower == "2da" || resourceTypeLower == "twoda")
            {
                return new TwoDaDiffAnalyzerWrapper();
            }

            // GFF analyzer (handles all GFF-based formats)
            HashSet<string> gffTypes = new HashSet<string>
            {
                "gff", "utc", "uti", "utp", "ute", "utm", "utd", "utw",
                "dlg", "are", "git", "ifo", "gui", "jrl", "fac"
            };
            if (gffTypes.Contains(resourceTypeLower))
            {
                return new GffDiffAnalyzerWrapper();
            }

            // TLK analyzer
            if (resourceTypeLower == "tlk")
            {
                return new TlkDiffAnalyzerWrapper();
            }

            // SSF analyzer
            if (resourceTypeLower == "ssf")
            {
                return new SsfDiffAnalyzerWrapper();
            }

            return null;
        }

        // Wrapper classes to adapt existing analyzers to IDiffAnalyzer interface
        private class TwoDaDiffAnalyzerWrapper : IDiffAnalyzer
        {
            private readonly TwoDaDiffAnalyzer _analyzer = new TwoDaDiffAnalyzer();

            public object Analyze(byte[] leftData, byte[] rightData, string identifier)
            {
                return _analyzer.Analyze(leftData, rightData, identifier);
            }
        }

        private class GffDiffAnalyzerWrapper : IDiffAnalyzer
        {
            private readonly GffDiffAnalyzer _analyzer = new GffDiffAnalyzer();

            public object Analyze(byte[] leftData, byte[] rightData, string identifier)
            {
                return _analyzer.Analyze(leftData, rightData, identifier);
            }
        }

        private class TlkDiffAnalyzerWrapper : IDiffAnalyzer
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/analyzers.py:690-800
            // Original: class TLKDiffAnalyzer(DiffAnalyzer):
            public object Analyze(byte[] leftData, byte[] rightData, string identifier)
            {
                try
                {
                    var leftReader = new Formats.TLK.TLKBinaryReader(leftData);
                    var rightReader = new Formats.TLK.TLKBinaryReader(rightData);
                    Formats.TLK.TLK leftTlk = leftReader.Load();
                    Formats.TLK.TLK rightTlk = rightReader.Load();

                    int leftSize = leftTlk.Count;
                    int rightSize = rightTlk.Count;

                    // Extract the actual TLK filename from the identifier
                    string tlkFilename = System.IO.Path.GetFileName(identifier ?? "dialog.tlk");

                    // Use "append.tlk" as the sourcefile per TSLPatcher convention
                    var modifications = new ModificationsTLK("append.tlk", replace: false, modifiers: new List<ModifyTLK>());
                    modifications.SaveAs = tlkFilename; // This is the actual TLK file being patched

                    // StrRef mappings will be returned separately
                    Dictionary<int, int> strrefMappings = new Dictionary<int, int>(); // old_strref -> token_id

                    int tokenId = 0;

                    // Process modified entries - these get appended and old refs must be updated
                    int minSize = Math.Min(leftSize, rightSize);
                    for (int idx = 0; idx < minSize; idx++)
                    {
                        Formats.TLK.TLKEntry leftEntry = leftTlk.Get(idx);
                        Formats.TLK.TLKEntry rightEntry = rightTlk.Get(idx);

                        if (leftEntry == null || rightEntry == null)
                        {
                            continue;
                        }

                        bool textDiffers = leftEntry.Text != rightEntry.Text;
                        bool voiceoverDiffers = !leftEntry.Voiceover.Equals(rightEntry.Voiceover);
                        bool entryModified = textDiffers || voiceoverDiffers;

                        if (entryModified)
                        {
                            // Append the modified entry as a new entry
                            var modify = new ModifyTLK(tokenId, isReplacement: false);
                            modify.ModIndex = idx; // Store the original TLK index for reference tracking
                            modify.Text = rightEntry.Text;
                            modify.Sound = rightEntry.Voiceover.ToString();
                            modifications.Modifiers.Add(modify);

                            // Track that old StrRef idx should map to token_id
                            strrefMappings[idx] = tokenId;
                            tokenId++;
                        }
                    }

                    // Process new entries (appends)
                    if (rightSize > leftSize)
                    {
                        for (int idx = leftSize; idx < rightSize; idx++)
                        {
                            Formats.TLK.TLKEntry rightEntry = rightTlk.Get(idx);
                            if (rightEntry == null)
                            {
                                continue;
                            }

                            // Append: token_id is the memory token
                            var modify = new ModifyTLK(tokenId, isReplacement: false);
                            modify.ModIndex = idx; // Store the original TLK index for reference
                            modify.Text = rightEntry.Text;
                            modify.Sound = rightEntry.Voiceover.ToString();
                            modifications.Modifiers.Add(modify);

                            // Track mapping for new entries too
                            strrefMappings[idx] = tokenId;
                            tokenId++;
                        }
                    }

                    if (modifications.Modifiers.Count > 0)
                    {
                        Console.WriteLine($"TLK modifications: identifier={identifier}, modifier_count={modifications.Modifiers.Count}, strref_count={strrefMappings.Count}");
                        // Return tuple: (modifications, strref_mappings)
                        return Tuple.Create(modifications, strrefMappings);
                    }
                    return null;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to parse TLK files: identifier={identifier}, error={e}");
                    Console.WriteLine(e.StackTrace);
                    return null;
                }
            }
        }

        private class SsfDiffAnalyzerWrapper : IDiffAnalyzer
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/analyzers.py:803-842
            // Original: class SSFDiffAnalyzer(DiffAnalyzer):
            public object Analyze(byte[] leftData, byte[] rightData, string identifier)
            {
                try
                {
                    var leftReader = new Formats.SSF.SSFBinaryReader(leftData);
                    var rightReader = new Formats.SSF.SSFBinaryReader(rightData);
                    Formats.SSF.SSF leftSsf = leftReader.Load();
                    Formats.SSF.SSF rightSsf = rightReader.Load();

                    // Extract just the filename from the identifier
                    string filename = System.IO.Path.GetFileName(identifier ?? "file.ssf");
                    var modifications = new ModificationsSSF(filename, replace: false, modifiers: new List<ModifySSF>());

                    // Check all SSF sounds
                    foreach (Formats.SSF.SSFSound sound in Enum.GetValues(typeof(Formats.SSF.SSFSound)))
                    {
                        int? leftValue = leftSsf.Get(sound);
                        int? rightValue = rightSsf.Get(sound);
                        bool valuesDiffer = leftValue != rightValue;

                        if (valuesDiffer && rightValue.HasValue)
                        {
                            var modify = new ModifySSF(sound, new Memory.NoTokenUsage(rightValue.Value));
                            modifications.Modifiers.Add(modify);
                        }
                    }

                    if (modifications.Modifiers.Count > 0)
                    {
                        Console.WriteLine($"SSF modifications: identifier={identifier}, modifier_count={modifications.Modifiers.Count}");
                    }

                    return modifications.Modifiers.Count > 0 ? modifications : null;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to parse SSF files: identifier={identifier}, error={e.GetType().Name}: {e}");
                    Console.WriteLine(e.StackTrace);
                    return null;
                }
            }
        }
    }
}
