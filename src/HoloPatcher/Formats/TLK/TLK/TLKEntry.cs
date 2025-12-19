using System;
using KOTORModSync;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Formats.TLK
{

    /// <summary>
    /// Represents a single entry in a TLK file.
    /// </summary>
    public class TLKEntry
    {
        public string Text { get; set; }
        public ResRef Voiceover { get; set; }

        public TLKEntry(string text, ResRef voiceover)
        {
            Text = text;
            Voiceover = voiceover;
        }

        // The following fields exist in TLK format, but do not perform any function in KOTOR. The game ignores these.
        public bool TextPresent { get; set; } = true;
        public bool SoundPresent { get; set; } = true;
        public bool SoundLengthPresent { get; set; } = true;
        public float SoundLength { get; set; } = 0f;

        public int TextLength => Text.Length;

        public override bool Equals([CanBeNull] object obj)
        {
            if (obj is TLKEntry other)
            {
                return Text == other.Text && Voiceover.Equals(other.Voiceover);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Text, Voiceover);
        }

        public override string ToString()
        {
            return $"TLKEntry(text=\"{Text}\", voiceover={Voiceover})";
        }
    }
}
