using System.Collections.Generic;
using KOTORModSync;
using KOTORModSync.Resource;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics
{
    /// <summary>
    /// Stores game state information for Aurora and s.
    ///
    /// GAM files are GFF-based format files that store game state including party information,
    /// global variables, game time, and time played. Used by Aurora (Neverwinter Nights) and
    ///  (, , ).
    ///
    /// NOTE: Odyssey (KOTOR) does NOT use GAM format - it uses NFO format for save games.
    /// </summary>
    /// <remarks>
    /// GAM File Format:
    /// - GFF format with "GAM " signature
    /// - Used by Aurora (nwmain.exe, nwn2main.exe) and  (.exe, .exe, .exe)
    /// - NOT used by Odyssey (swkotor.exe, swkotor2.exe) - Odyssey uses NFO format
    /// - Contains game state: party members, global variables, game time, time played
    /// - Structure differs between Aurora and s (different field names and organization)
    /// </remarks>
    [PublicAPI]
    public sealed class GAM
    {
        // Matching pattern from other GFF-based formats (IFO, ARE, etc.)
        // Original: GAM files are GFF format with "GAM " signature
        public static readonly ResourceType BinaryType = ResourceType.GAM;

        // Game time fields (common across Aurora and Infinity)
        /// <summary>
        /// Current game time hour (0-23).
        /// </summary>
        public int GameTimeHour { get; set; }

        /// <summary>
        /// Current game time minute (0-59).
        /// </summary>
        public int GameTimeMinute { get; set; }

        /// <summary>
        /// Current game time second (0-59).
        /// </summary>
        public int GameTimeSecond { get; set; }

        /// <summary>
        /// Current game time millisecond (0-999).
        /// </summary>
        public int GameTimeMillisecond { get; set; }

        /// <summary>
        /// Total time played in seconds.
        /// </summary>
        public int TimePlayed { get; set; }

        // Party information (Aurora and Infinity both have party systems)
        /// <summary>
        /// List of party member ResRefs.
        /// </summary>
        public List<ResRef> PartyMembers { get; set; } = new List<ResRef>();

        // Global variables (Aurora and Infinity both use global variables)
        /// <summary>
        /// Boolean global variables (name -> value).
        /// </summary>
        public Dictionary<string, bool> GlobalBooleans { get; set; } = new Dictionary<string, bool>();

        /// <summary>
        /// Numeric global variables (name -> value).
        /// </summary>
        public Dictionary<string, int> GlobalNumbers { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// String global variables (name -> value).
        /// </summary>
        public Dictionary<string, string> GlobalStrings { get; set; } = new Dictionary<string, string>();

        // Aurora-specific fields (nwmain.exe, nwn2main.exe)
        /// <summary>
        /// Module name (Aurora-specific).
        /// </summary>
        public string ModuleName { get; set; } = string.Empty;

        /// <summary>
        /// Current area ResRef (Aurora-specific).
        /// </summary>
        public ResRef CurrentArea { get; set; } = ResRef.FromBlank();

        /// <summary>
        /// Player character ResRef (Aurora-specific).
        /// </summary>
        public ResRef PlayerCharacter { get; set; } = ResRef.FromBlank();

        // Infinity-specific fields (.exe, .exe, .exe)
        /// <summary>
        /// Game name/identifier (Infinity-specific).
        /// </summary>
        public string GameName { get; set; } = string.Empty;

        /// <summary>
        /// Current chapter (Infinity-specific).
        /// </summary>
        public int Chapter { get; set; }

        /// <summary>
        /// Journal entries (Infinity-specific).
        /// </summary>
        public List<GAMJournalEntry> JournalEntries { get; set; } = new List<GAMJournalEntry>();

        /// <summary>
        /// Represents a journal entry in  GAM files.
        /// </summary>
        public class GAMJournalEntry
        {
            /// <summary>
            /// Journal entry text (localized string reference).
            /// </summary>
            public int TextStrRef { get; set; }

            /// <summary>
            /// Whether the journal entry is completed.
            /// </summary>
            public bool Completed { get; set; }

            /// <summary>
            /// Journal entry category/type.
            /// </summary>
            public int Category { get; set; }
        }
    }
}

