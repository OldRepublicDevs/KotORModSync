using System.Collections.Generic;
using KOTORModSync.Formats.TLK;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Diff
{

    public class TlkCompareResult
    {
        public Dictionary<int, TlkChangedEntry> ChangedEntries { get; } = new Dictionary<int, TlkChangedEntry>();
        public Dictionary<int, (string Text, string Sound)> AddedEntries { get; } = new Dictionary<int, (string Text, string Sound)>();
    }

    public class TlkChangedEntry
    {
        [CanBeNull]
        public string Text { get; set; }
        [CanBeNull]
        public string Sound { get; set; }
    }

    public static class TlkDiff
    {
        public static TlkCompareResult Compare(TLK original, TLK modified)
        {
            var result = new TlkCompareResult();
            int origCount = original.Count;
            int modCount = modified.Count;

            for (int i = 0; i < modCount; i++)
            {
                if (i >= origCount)
                {
                    // Added entry
                    TLKEntry entry = modified[i];
                    result.AddedEntries[i] = (entry.Text, entry.Voiceover.ToString());
                }
                else
                {
                    // Compare existing
                    TLKEntry origEntry = original[i];
                    TLKEntry modEntry = modified[i];

                    string changedText = null;
                    string changedSound = null;

                    if (origEntry.Text != modEntry.Text)
                    {
                        changedText = modEntry.Text;
                    }

                    // Assuming ResRef implements Equals correctly or use ToString()
                    if (origEntry.Voiceover.ToString() != modEntry.Voiceover.ToString())
                    {
                        changedSound = modEntry.Voiceover.ToString();
                    }

                    if (changedText != null || changedSound != null)
                    {
                        result.ChangedEntries[i] = new TlkChangedEntry { Text = changedText, Sound = changedSound };
                    }
                }
            }

            return result;
        }
    }
}
