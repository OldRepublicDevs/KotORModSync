using KOTORModSync;
using KOTORModSync.Formats.GFF;
using KOTORModSync.Resource;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utw.py:110-152
    // Original: construct_utw and dismantle_utw functions
    public static class UTWHelpers
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utw.py:110-128
        // Original: def construct_utw(gff: GFF) -> UTW:
        public static UTW ConstructUtw(GFF gff)
        {
            var utw = new UTW();
            var root = gff.Root;

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utw.py:116-126
            // Original: Extract all UTW fields from GFF root
            utw.AppearanceId = root.Acquire<int>("Appearance", 0);
            utw.LinkedTo = root.Acquire<string>("LinkedTo", "");
            utw.ResRef = root.Acquire<ResRef>("TemplateResRef", ResRef.FromBlank());
            utw.Tag = root.Acquire<string>("Tag", "");
            utw.Name = root.Acquire<LocalizedString>("LocalizedName", LocalizedString.FromInvalid());
            utw.Description = root.Acquire<LocalizedString>("Description", LocalizedString.FromInvalid());
            // HasMapNote and MapNoteEnabled are stored as UInt8 (0 or 1), need to read as byte and convert
            utw.HasMapNote = root.GetUInt8("HasMapNote") != 0;
            utw.MapNote = root.Acquire<LocalizedString>("MapNote", LocalizedString.FromInvalid());
            utw.MapNoteEnabled = root.GetUInt8("MapNoteEnabled") != 0;
            utw.PaletteId = root.Acquire<int>("PaletteID", 0);
            utw.Comment = root.Acquire<string>("Comment", "");

            return utw;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utw.py:131-152
        // Original: def dismantle_utw(utw: UTW, game: Game = Game.K2, *, use_deprecated: bool = True) -> GFF:
        public static GFF DismantleUtw(UTW utw, Game game = Game.K2, bool useDeprecated = true)
        {
            var gff = new GFF(GFFContent.UTW);
            var root = gff.Root;

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utw.py:140-150
            // Original: Set all UTW fields in GFF root
            root.SetUInt8("Appearance", (byte)utw.AppearanceId);
            root.SetString("LinkedTo", utw.LinkedTo);
            root.SetResRef("TemplateResRef", utw.ResRef);
            root.SetString("Tag", utw.Tag);
            root.SetLocString("LocalizedName", utw.Name);
            root.SetLocString("Description", utw.Description);
            root.SetUInt8("HasMapNote", utw.HasMapNote ? (byte)1 : (byte)0);
            root.SetLocString("MapNote", utw.MapNote);
            root.SetUInt8("MapNoteEnabled", utw.MapNoteEnabled ? (byte)1 : (byte)0);
            root.SetUInt8("PaletteID", (byte)utw.PaletteId);
            root.SetString("Comment", utw.Comment);

            return gff;
        }
    }
}
