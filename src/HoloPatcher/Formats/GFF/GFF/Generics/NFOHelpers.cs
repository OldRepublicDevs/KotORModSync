using System;
using KOTORModSync.Common;
using KOTORModSync.Formats.GFF;

namespace KOTORModSync.Resource.Generics
{
    internal static class NFOHelpers
    {
        public static NFOData ConstructNfo(GFF gff)
        {
            if (gff == null) throw new ArgumentNullException(nameof(gff));

            GFFStruct root = gff.Root ?? new GFFStruct();
            var nfo = new NFOData();

            nfo.AreaName = root.Acquire("AREANAME", string.Empty);
            nfo.LastModule = root.Acquire("LASTMODULE", string.Empty);
            nfo.SavegameName = root.Acquire("SAVEGAMENAME", string.Empty);
            nfo.TimePlayedSeconds = root.Acquire("TIMEPLAYED", 0);

            if (root.Exists("TIMESTAMP"))
            {
                // TIMESTAMP is commonly FILETIME in a 64-bit integer; tolerate both signed/unsigned.
                GFFFieldType? type = root.GetFieldType("TIMESTAMP");
                if (type == GFFFieldType.UInt64)
                {
                    nfo.TimestampFileTime = root.GetUInt64("TIMESTAMP");
                }
                else
                {
                    long v = root.GetInt64("TIMESTAMP");
                    nfo.TimestampFileTime = v < 0 ? (ulong?)null : (ulong)v;
                }
            }

            nfo.CheatUsed = root.Acquire("CHEATUSED", (byte)0) != 0;
            nfo.GameplayHint = root.Acquire("GAMEPLAYHINT", (byte)0);

            // STORYHINT variants:
            // - Legacy single byte
            nfo.StoryHintLegacy = root.Acquire("STORYHINT", (byte)0);
            // - Per-index flags 0..9
            bool anyIndexed = false;
            for (int i = 0; i < 10; i++)
            {
                string field = "STORYHINT" + i;
                if (!root.Exists(field))
                {
                    continue;
                }

                anyIndexed = true;
                bool hint = root.Acquire(field, (byte)0) != 0;
                nfo.StoryHints[i] = hint;
            }

            // If only legacy story hint exists, keep StoryHints defaulted.
            if (!anyIndexed)
            {
                // Leave list as-is; consumers can choose legacy or indexed.
            }

            nfo.Portrait0 = root.Acquire("PORTRAIT0", ResRef.FromBlank());
            nfo.Portrait1 = root.Acquire("PORTRAIT1", ResRef.FromBlank());
            nfo.Portrait2 = root.Acquire("PORTRAIT2", ResRef.FromBlank());

            nfo.LiveContentBitmask = root.Acquire("LIVECONTENT", (byte)0);

            // Live entries: tolerate 1..9.
            for (int i = 1; i <= 9; i++)
            {
                string field = "LIVE" + i;
                if (root.Exists(field))
                {
                    nfo.LiveEntries[i - 1] = root.Acquire(field, string.Empty);
                }
            }

            nfo.PcName = root.Acquire("PCNAME", string.Empty);

            return nfo;
        }

        public static GFF DismantleNfo(NFOData nfo)
        {
            if (nfo == null) throw new ArgumentNullException(nameof(nfo));

            var gff = new GFF(GFFContent.NFO);
            GFFStruct root = gff.Root;

            root.SetString("AREANAME", nfo.AreaName ?? string.Empty);
            root.SetString("LASTMODULE", nfo.LastModule ?? string.Empty);
            root.SetString("SAVEGAMENAME", nfo.SavegameName ?? string.Empty);
            root.SetUInt32("TIMEPLAYED", (uint)Math.Max(0, nfo.TimePlayedSeconds));

            if (nfo.TimestampFileTime.HasValue)
            {
                root.SetUInt64("TIMESTAMP", nfo.TimestampFileTime.Value);
            }

            root.SetUInt8("CHEATUSED", (byte)(nfo.CheatUsed ? 1 : 0));
            root.SetUInt8("GAMEPLAYHINT", nfo.GameplayHint);

            // Preserve legacy field for tools expecting it.
            root.SetUInt8("STORYHINT", nfo.StoryHintLegacy);

            // Also write indexed story hints if provided.
            if (nfo.StoryHints != null)
            {
                for (int i = 0; i < 10 && i < nfo.StoryHints.Count; i++)
                {
                    root.SetUInt8("STORYHINT" + i, (byte)(nfo.StoryHints[i] ? 1 : 0));
                }
            }

            root.SetResRef("PORTRAIT0", nfo.Portrait0 ?? ResRef.FromBlank());
            root.SetResRef("PORTRAIT1", nfo.Portrait1 ?? ResRef.FromBlank());
            root.SetResRef("PORTRAIT2", nfo.Portrait2 ?? ResRef.FromBlank());

            root.SetUInt8("LIVECONTENT", nfo.LiveContentBitmask);

            if (nfo.LiveEntries != null)
            {
                for (int i = 1; i <= 9 && i - 1 < nfo.LiveEntries.Count; i++)
                {
                    root.SetString("LIVE" + i, nfo.LiveEntries[i - 1] ?? string.Empty);
                }
            }

            if (!string.IsNullOrEmpty(nfo.PcName))
            {
                root.SetString("PCNAME", nfo.PcName);
            }

            return gff;
        }
    }
}


