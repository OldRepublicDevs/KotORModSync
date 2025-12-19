using System.Collections.Generic;
using KOTORModSync;
using KOTORModSync.Formats.GFF;
using KOTORModSync.Resource;
using static KOTORModSync.Common.GameExtensions;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/uts.py
    // Original: construct_uts and dismantle_uts functions
    public static class UTSHelpers
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/uts.py:199-234
        // Original: def construct_uts(gff: GFF) -> UTS:
        public static UTS ConstructUts(GFF gff)
        {
            var uts = new UTS();
            var root = gff.Root;

            // Extract basic fields
            uts.Tag = root.Acquire<string>("Tag", "");
            uts.ResRef = root.Acquire<ResRef>("TemplateResRef", ResRef.FromBlank());
            // Boolean fields stored as UInt8 - use GetUInt8() != 0 (matching UTW fix)
            uts.Active = root.GetUInt8("Active") != 0;
            uts.Continuous = root.GetUInt8("Continuous") != 0;
            uts.Looping = root.GetUInt8("Looping") != 0;
            uts.Positional = root.GetUInt8("Positional") != 0;
            uts.RandomPosition = root.GetUInt8("RandomPosition") != 0;
            uts.Random = root.GetUInt8("Random") != 0;
            uts.Name = root.Acquire<LocalizedString>("LocName", LocalizedString.FromInvalid());
            uts.Volume = root.Acquire<int>("Volume", 0);
            uts.VolumeVariance = root.Acquire<int>("VolumeVrtn", 0);
            uts.PitchVariance = root.Acquire<float>("PitchVariation", 0.0f);
            uts.Elevation = root.Acquire<float>("Elevation", 0.0f);
            uts.MinDistance = root.Acquire<float>("MinDistance", 0.0f);
            uts.MaxDistance = root.Acquire<float>("MaxDistance", 0.0f);
            uts.DistanceCutoff = root.Acquire<float>("DistanceCutoff", 0.0f);
            uts.Priority = root.Acquire<int>("Priority", 0);
            uts.Hours = root.Acquire<int>("Hours", 0);
            uts.Times = root.Acquire<int>("Times", 0);
            uts.Interval = (int)root.GetUInt32("Interval");
            uts.IntervalVariance = (int)root.GetUInt32("IntervalVrtn");
            uts.Sound = root.Acquire<ResRef>("Sound", ResRef.FromBlank());
            uts.Comment = root.Acquire<string>("Comment", "");
            uts.RandomRangeX = root.Acquire<float>("RandomRangeX", 0.0f);
            uts.RandomRangeY = root.Acquire<float>("RandomRangeY", 0.0f);

            // Extract sounds list
            var soundsList = root.Acquire<GFFList>("Sounds", new GFFList());
            uts.Sounds.Clear();
            foreach (var soundStruct in soundsList)
            {
                var sound = soundStruct.Acquire<ResRef>("Sound", ResRef.FromBlank());
                if (sound != null && !string.IsNullOrEmpty(sound.ToString()))
                {
                    uts.Sounds.Add(sound);
                }
            }

            return uts;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/uts.py:237-311
        // Original: def dismantle_uts(uts: UTS, game: Game = Game.K2, *, use_deprecated: bool = True) -> GFF:
        public static GFF DismantleUts(UTS uts, Game game = Game.K2, bool useDeprecated = true)
        {
            var gff = new GFF(GFFContent.UTS);
            var root = gff.Root;

            // Set basic fields
            root.SetString("Tag", uts.Tag);
            root.SetResRef("TemplateResRef", uts.ResRef);
            root.SetUInt8("Active", uts.Active ? (byte)1 : (byte)0);
            root.SetUInt8("Continuous", uts.Continuous ? (byte)1 : (byte)0);
            root.SetUInt8("Looping", uts.Looping ? (byte)1 : (byte)0);
            root.SetUInt8("Positional", uts.Positional ? (byte)1 : (byte)0);
            root.SetUInt8("RandomPosition", uts.RandomPosition ? (byte)1 : (byte)0);
            root.SetUInt8("Random", uts.Random ? (byte)1 : (byte)0);
            root.SetUInt8("Volume", (byte)uts.Volume);
            root.SetUInt8("VolumeVrtn", (byte)uts.VolumeVariance);
            root.SetSingle("PitchVariation", uts.PitchVariance);
            root.SetSingle("Elevation", uts.Elevation);
            root.SetSingle("MinDistance", uts.MinDistance);
            root.SetSingle("MaxDistance", uts.MaxDistance);
            root.SetSingle("DistanceCutoff", uts.DistanceCutoff);
            root.SetUInt8("Priority", (byte)uts.Priority);
            root.SetUInt32("Interval", (uint)uts.Interval);
            root.SetUInt32("IntervalVrtn", (uint)uts.IntervalVariance);
            root.SetResRef("Sound", uts.Sound);
            root.SetString("Comment", uts.Comment);
            root.SetSingle("RandomRangeX", uts.RandomRangeX);
            root.SetSingle("RandomRangeY", uts.RandomRangeY);
            root.SetUInt8("PaletteID", 0);
            if (useDeprecated)
            {
                root.SetLocString("LocName", uts.Name);
                root.SetUInt32("Hours", (uint)uts.Hours);
                root.SetUInt8("Times", (byte)uts.Times);
            }
            else
            {
                // Always set LocName even if not deprecated
                root.SetLocString("LocName", uts.Name);
            }

            // Set sounds list
            var soundsList = new GFFList();
            root.SetList("Sounds", soundsList);
            if (uts.Sounds != null)
            {
                foreach (var sound in uts.Sounds)
                {
                    var soundStruct = soundsList.Add(2);
                    soundStruct.SetResRef("Sound", sound);
                }
            }

            return gff;
        }
    }
}