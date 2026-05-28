using System;
using System.Collections.Generic;
using KOTORModSync.Common;
using JetBrains.Annotations;

namespace KOTORModSync.Resource.Generics
{
    /// <summary>
    /// High-level representation of Odyssey (KotOR/KotOR2) save metadata stored in <c>savenfo.res</c>.
    /// </summary>
    /// <remarks>
    /// This is intentionally tolerant to field variants observed in the wild:
    /// - Some saves store <c>STORYHINT</c> as a single byte
    /// - Some store <c>STORYHINT0..9</c> as separate bytes
    /// - Live content may appear as <c>LIVE1..9</c> strings plus <c>LIVECONTENT</c> bitmask
    /// </remarks>
    [PublicAPI]
    public sealed class NFOData
    {
        public string AreaName { get; set; } = string.Empty;
        public string LastModule { get; set; } = string.Empty;
        public string SavegameName { get; set; } = string.Empty;
        public int TimePlayedSeconds { get; set; }

        /// <summary>
        /// Windows FILETIME, if present.
        /// </summary>
        public ulong? TimestampFileTime { get; set; }

        public bool CheatUsed { get; set; }
        public byte GameplayHint { get; set; }

        /// <summary>
        /// Legacy single-byte story hint field (<c>STORYHINT</c>) when present.
        /// </summary>
        public byte StoryHintLegacy { get; set; }

        /// <summary>
        /// Per-index story hints (<c>STORYHINT0..9</c>) when present.
        /// </summary>
        public List<bool> StoryHints { get; } = new List<bool>(capacity: 10);

        public ResRef Portrait0 { get; set; } = ResRef.FromBlank();
        public ResRef Portrait1 { get; set; } = ResRef.FromBlank();
        public ResRef Portrait2 { get; set; } = ResRef.FromBlank();

        public byte LiveContentBitmask { get; set; }
        public List<string> LiveEntries { get; } = new List<string>(capacity: 9);

        public string PcName { get; set; } = string.Empty;

        public NFOData()
        {
            // Normalize list lengths for consumers.
            for (int i = 0; i < 10; i++) StoryHints.Add(false);
            for (int i = 0; i < 9; i++) LiveEntries.Add(string.Empty);
        }
    }
}


