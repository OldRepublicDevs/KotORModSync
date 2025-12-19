using System;
using System.Collections.Generic;
using KOTORModSync;
using KOTORModSync.Formats.GFF;
using KOTORModSync.Resource;
using KOTORModSync.Common;
using static KOTORModSync.Common.GameExtensions;

namespace KOTORModSync.Resource.Generics
{
    // Matching pattern from other GFF-based format helpers (IFOHelpers, AREHelpers, etc.)
    // Original: construct_gam and dismantle_gam functions for GAM file format
    public static class GAMHelpers
    {
        /// <summary>
        /// Constructs a GAM object from a GFF structure.
        /// </summary>
        /// <param name="gff">The GFF structure containing GAM data.</param>
        /// <returns>A GAM object with extracted data.</returns>
        /// <remarks>
        /// GAM Construction:
        /// - Extracts game state from GFF root struct
        /// - Handles both Aurora and  formats
        /// - Game time, time played, party members, global variables
        /// - Engine-specific fields are extracted based on presence in GFF
        /// </remarks>
        public static GAM ConstructGam(GFF gff)
        {
            var gam = new GAM();
            var root = gff.Root;

            // Extract game time fields (common across Aurora and Infinity)
            gam.GameTimeHour = root.Acquire<int>("GameTimeHour", 0);
            gam.GameTimeMinute = root.Acquire<int>("GameTimeMinute", 0);
            gam.GameTimeSecond = root.Acquire<int>("GameTimeSecond", 0);
            gam.GameTimeMillisecond = root.Acquire<int>("GameTimeMillisecond", 0);

            // Extract time played (common across Aurora and Infinity)
            gam.TimePlayed = root.Acquire<int>("TimePlayed", 0);

            // Extract party members (common across Aurora and Infinity)
            var partyList = root.Acquire<GFFList>("PartyList", new GFFList());
            if (partyList != null)
            {
                foreach (var partyStruct in partyList)
                {
                    var memberResRef = partyStruct.Acquire<ResRef>("PartyMember", ResRef.FromBlank());
                    if (!memberResRef.IsBlank())
                    {
                        gam.PartyMembers.Add(memberResRef);
                    }
                }
            }

            // Extract global variables (common across Aurora and Infinity)
            // Boolean globals
            var boolList = root.Acquire<GFFList>("GlobalBooleans", new GFFList());
            if (boolList != null)
            {
                foreach (var boolStruct in boolList)
                {
                    string name = boolStruct.Acquire<string>("Name", "");
                    bool value = boolStruct.GetUInt8("Value") != 0;
                    if (!string.IsNullOrEmpty(name))
                    {
                        gam.GlobalBooleans[name] = value;
                    }
                }
            }

            // Numeric globals
            var numList = root.Acquire<GFFList>("GlobalNumbers", new GFFList());
            if (numList != null)
            {
                foreach (var numStruct in numList)
                {
                    string name = numStruct.Acquire<string>("Name", "");
                    int value = numStruct.Acquire<int>("Value", 0);
                    if (!string.IsNullOrEmpty(name))
                    {
                        gam.GlobalNumbers[name] = value;
                    }
                }
            }

            // String globals
            var strList = root.Acquire<GFFList>("GlobalStrings", new GFFList());
            if (strList != null)
            {
                foreach (var strStruct in strList)
                {
                    string name = strStruct.Acquire<string>("Name", "");
                    string value = strStruct.Acquire<string>("Value", "");
                    if (!string.IsNullOrEmpty(name))
                    {
                        gam.GlobalStrings[name] = value;
                    }
                }
            }

            // Extract Aurora-specific fields
            gam.ModuleName = root.Acquire<string>("ModuleName", "");
            gam.CurrentArea = root.Acquire<ResRef>("CurrentArea", ResRef.FromBlank());
            gam.PlayerCharacter = root.Acquire<ResRef>("PlayerCharacter", ResRef.FromBlank());

            // Extract Infinity-specific fields
            gam.GameName = root.Acquire<string>("GameName", "");
            gam.Chapter = root.Acquire<int>("Chapter", 0);

            // Extract Infinity journal entries
            var journalList = root.Acquire<GFFList>("JournalEntries", new GFFList());
            if (journalList != null)
            {
                foreach (var journalStruct in journalList)
                {
                    var entry = new GAM.GAMJournalEntry
                    {
                        TextStrRef = journalStruct.Acquire<int>("TextStrRef", 0),
                        Completed = journalStruct.GetUInt8("Completed") != 0,
                        Category = journalStruct.Acquire<int>("Category", 0)
                    };
                    gam.JournalEntries.Add(entry);
                }
            }

            return gam;
        }

        /// <summary>
        /// Dismantles a GAM object into a GFF structure.
        /// </summary>
        /// <param name="gam">The GAM object to dismantle.</param>
        /// <param name="game">The game type (must be Aurora or , not Odyssey).</param>
        /// <returns>A GFF structure containing GAM data.</returns>
        /// <remarks>
        /// GAM Dismantling:
        /// - Validates that game is Aurora or Infinity (not Odyssey)
        /// - Creates GFF with "GAM " signature
        /// - Writes game state to GFF root struct
        /// - Handles both Aurora and  formats
        /// - Engine-specific fields are written based on game type
        /// </remarks>
        public static GFF DismantleGam(GAM gam, Game game)
        {
            // Validate game type - GAM format is only used by Aurora and Infinity, NOT Odyssey
            if (game.IsOdyssey())
            {
                throw new ArgumentException(
                    $"GAM format is not supported for Odyssey engine (KOTOR). Odyssey uses NFO format for save games. " +
                    $"GAM format is only supported for Aurora (NWN) and  (, , ). " +
                    $"Provided game: {game}",
                    nameof(game));
            }

            // For now, only Aurora is supported ( games not in Game enum yet)
            // When  games are added to Game enum, remove this check
            if (!game.IsAurora())
            {
                throw new ArgumentException(
                    $"GAM format is only supported for Aurora (NWN) and  games. " +
                    $"Currently only Aurora (NWN, NWN2) is supported as  games are not yet in the Game enum. " +
                    $"Provided game: {game}",
                    nameof(game));
            }

            var gff = new GFF(GFFContent.GAM);
            var root = gff.Root;

            // Set game time fields (common across Aurora and Infinity)
            root.SetInt32("GameTimeHour", gam.GameTimeHour);
            root.SetInt32("GameTimeMinute", gam.GameTimeMinute);
            root.SetInt32("GameTimeSecond", gam.GameTimeSecond);
            root.SetInt32("GameTimeMillisecond", gam.GameTimeMillisecond);

            // Set time played (common across Aurora and Infinity)
            root.SetInt32("TimePlayed", gam.TimePlayed);

            // Set party members (common across Aurora and Infinity)
            var partyList = new GFFList();
            root.SetList("PartyList", partyList);
            if (gam.PartyMembers != null)
            {
                foreach (var member in gam.PartyMembers)
                {
                    var partyStruct = partyList.Add(6); // Struct type
                    partyStruct.SetResRef("PartyMember", member);
                }
            }

            // Set global variables (common across Aurora and Infinity)
            // Boolean globals
            var boolList = new GFFList();
            root.SetList("GlobalBooleans", boolList);
            if (gam.GlobalBooleans != null)
            {
                foreach (var kvp in gam.GlobalBooleans)
                {
                    var boolStruct = boolList.Add(6); // Struct type
                    boolStruct.SetString("Name", kvp.Key);
                    boolStruct.SetUInt8("Value", kvp.Value ? (byte)1 : (byte)0);
                }
            }

            // Numeric globals
            var numList = new GFFList();
            root.SetList("GlobalNumbers", numList);
            if (gam.GlobalNumbers != null)
            {
                foreach (var kvp in gam.GlobalNumbers)
                {
                    var numStruct = numList.Add(6); // Struct type
                    numStruct.SetString("Name", kvp.Key);
                    numStruct.SetInt32("Value", kvp.Value);
                }
            }

            // String globals
            var strList = new GFFList();
            root.SetList("GlobalStrings", strList);
            if (gam.GlobalStrings != null)
            {
                foreach (var kvp in gam.GlobalStrings)
                {
                    var strStruct = strList.Add(6); // Struct type
                    strStruct.SetString("Name", kvp.Key);
                    strStruct.SetString("Value", kvp.Value);
                }
            }

            // Set Aurora-specific fields (only for Aurora games)
            if (game.IsAurora())
            {
                root.SetString("ModuleName", gam.ModuleName);
                root.SetResRef("CurrentArea", gam.CurrentArea);
                root.SetResRef("PlayerCharacter", gam.PlayerCharacter);
            }

            // Set Infinity-specific fields (only for  games)
            if (Is(game))
            {
                root.SetString("GameName", gam.GameName);
                root.SetInt32("Chapter", gam.Chapter);

                // Set Infinity journal entries
                var journalList = new GFFList();
                root.SetList("JournalEntries", journalList);
                if (gam.JournalEntries != null)
                {
                    foreach (var entry in gam.JournalEntries)
                    {
                        var journalStruct = journalList.Add(6); // Struct type
                        journalStruct.SetInt32("TextStrRef", entry.TextStrRef);
                        journalStruct.SetUInt8("Completed", entry.Completed ? (byte)1 : (byte)0);
                        journalStruct.SetInt32("Category", entry.Category);
                    }
                }
            }

            return gff;
        }

        /// <summary>
        /// Checks if the game is an  game.
        /// </summary>
        /// <param name="game">The game enum value.</param>
        /// <returns>True if the game is an  game.</returns>
        /// <remarks>
        ///  games:
        /// -  (.exe)
        /// -  (.exe)
        /// -  (.exe)
        /// 
        /// NOTE:  games are not currently in the Game enum, but this method
        /// is prepared for when they are added. For now, this returns false.
        /// </remarks>
        private static bool Is(Game game)
        {
            //  games would be added to Game enum in the future
            // For now, return false as  games are not in the enum yet
            // This method is prepared for future  support
            return false;
        }
    }
}

