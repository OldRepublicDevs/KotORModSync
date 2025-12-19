using System.Collections.Generic;
using KOTORModSync;
using KOTORModSync.Formats.GFF;
using KOTORModSync.Resource;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/jrl.py:83-88
    // Original: class JRLQuestPriority(IntEnum):
    public enum JRLQuestPriority
    {
        Highest = 0,
        High = 1,
        Medium = 2,
        Low = 3,
        Lowest = 4
    }

    /// <summary>
    /// Stores journal (quest) data.
    ///
    /// JRL files are GFF-based format files that store journal/quest data including
    /// quest entries, priorities, and planet associations.
    /// </summary>
    [PublicAPI]
    public sealed class JRL
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/jrl.py:29
        // Original: BINARY_TYPE = ResourceType.JRL
        public static readonly ResourceType BinaryType = ResourceType.JRL;

        public List<JRLQuest> Quests { get; set; } = new List<JRLQuest>();

        public JRL()
        {
        }
    }

    /// <summary>
    /// Stores data of an individual quest.
    /// </summary>
    [PublicAPI]
    public sealed class JRLQuest
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/jrl.py:37-60
        // Original: class JRLQuest:
        public LocalizedString Name { get; set; } = LocalizedString.FromInvalid();
        public int PlanetId { get; set; }
        public int PlotIndex { get; set; }
        public JRLQuestPriority Priority { get; set; } = JRLQuestPriority.Lowest;
        public string Tag { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public List<JRLQuestEntry> Entries { get; set; } = new List<JRLQuestEntry>();

        public JRLQuest()
        {
        }
    }

    /// <summary>
    /// Stores a quest entry (journal entry).
    /// </summary>
    [PublicAPI]
    public sealed class JRLQuestEntry
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/jrl.py:63-80
        // Original: class JRLEntry:
        public LocalizedString Text { get; set; } = LocalizedString.FromInvalid();
        public bool End { get; set; }
        public int EntryId { get; set; }
        public float XpPercentage { get; set; }

        public JRLQuestEntry()
        {
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/jrl.py:91-112
    // Original: def construct_jrl(gff: GFF) -> JRL:
    public static class JRLHelper
    {
        public static JRL ConstructJrl(GFF gff)
        {
            var jrl = new JRL();

            GFFList categories = gff.Root.Acquire("Categories", new GFFList());
            foreach (GFFStruct categoryStruct in categories)
            {
                var quest = new JRLQuest();
                jrl.Quests.Add(quest);
                quest.Comment = categoryStruct.Acquire("Comment", string.Empty);
                quest.Name = categoryStruct.Acquire("Name", LocalizedString.FromInvalid());
                quest.PlanetId = categoryStruct.Acquire("PlanetID", 0);
                quest.PlotIndex = categoryStruct.Acquire("PlotIndex", 0);
                int priorityValue = categoryStruct.Acquire("Priority", 0);
                quest.Priority = (JRLQuestPriority)priorityValue;
                quest.Tag = categoryStruct.Acquire("Tag", string.Empty);

                GFFList entryList = categoryStruct.Acquire("EntryList", new GFFList());
                foreach (GFFStruct entryStruct in entryList)
                {
                    var entry = new JRLQuestEntry();
                    quest.Entries.Add(entry);
                    entry.End = entryStruct.Acquire("End", (ushort)0) != 0;
                    entry.EntryId = (int)entryStruct.Acquire("ID", (uint)0);
                    entry.Text = entryStruct.Acquire("Text", LocalizedString.FromInvalid());
                    entry.XpPercentage = entryStruct.Acquire("XP_Percentage", 0.0f);
                }
            }

            return jrl;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/jrl.py:115-141
        // Original: def dismantle_jrl(jrl: JRL, game: Game = Game.K2, *, use_deprecated: bool = True) -> GFF:
        public static GFF DismantleJrl(JRL jrl)
        {
            var gff = new GFF(GFFContent.JRL);

            var categoryList = new GFFList();
            gff.Root.SetList("Categories", categoryList);
            for (int i = 0; i < jrl.Quests.Count; i++)
            {
                JRLQuest quest = jrl.Quests[i];
                GFFStruct categoryStruct = categoryList.Add(i);
                categoryStruct.SetString("Comment", quest.Comment);
                categoryStruct.SetLocString("Name", quest.Name);
                categoryStruct.SetInt32("PlanetID", quest.PlanetId);
                categoryStruct.SetInt32("PlotIndex", quest.PlotIndex);
                categoryStruct.SetUInt32("Priority", (uint)quest.Priority);
                categoryStruct.SetString("Tag", quest.Tag);

                var entryList = new GFFList();
                categoryStruct.SetList("EntryList", entryList);
                for (int j = 0; j < quest.Entries.Count; j++)
                {
                    JRLQuestEntry entry = quest.Entries[j];
                    GFFStruct entryStruct = entryList.Add(j);
                    entryStruct.SetUInt16("End", (ushort)(entry.End ? 1 : 0));
                    entryStruct.SetUInt32("ID", (uint)entry.EntryId);
                    entryStruct.SetLocString("Text", entry.Text);
                    entryStruct.SetSingle("XP_Percentage", entry.XpPercentage);
                }
            }

            return gff;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/jrl.py:144-150
        // Original: def read_jrl(source: SOURCE_TYPES, offset: int = 0, size: int | None = None) -> JRL:
        public static JRL ReadJrl(byte[] data, int offset = 0, int size = -1)
        {
            byte[] dataToRead = data;
            if (size > 0 && offset + size <= data.Length)
            {
                dataToRead = new byte[size];
                System.Array.Copy(data, offset, dataToRead, 0, size);
            }
            GFF gff = GFF.FromBytes(dataToRead);
            return ConstructJrl(gff);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/jrl.py:163-168
        // Original: def bytes_jrl(jrl: JRL, file_format: ResourceType = ResourceType.GFF) -> bytes:
        public static byte[] BytesJrl(JRL jrl, ResourceType fileFormat = null)
        {
            if (fileFormat == null)
            {
                fileFormat = ResourceType.JRL;
            }
            GFF gff = DismantleJrl(jrl);
            return GFFAuto.BytesGff(gff, fileFormat);
        }
    }
}