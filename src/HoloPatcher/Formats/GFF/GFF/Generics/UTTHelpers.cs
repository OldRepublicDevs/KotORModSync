using KOTORModSync;
using KOTORModSync.Formats.GFF;
using KOTORModSync.Resource;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utt.py:212-264
    // Original: construct_utt and dismantle_utt functions
    public static class UTTHelpers
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utt.py:212-264
        // Original: def construct_utt(gff: GFF) -> UTT:
        public static UTT ConstructUtt(GFF gff)
        {
            var utt = new UTT();
            var root = gff.Root;

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utt.py:236-262
            // Original: Extract all UTT fields from GFF root
            utt.Tag = root.Acquire<string>("Tag", "");
            utt.ResRef = root.Acquire<ResRef>("TemplateResRef", ResRef.FromBlank());
            utt.AutoRemoveKey = root.Acquire<int>("AutoRemoveKey", 0) != 0;
            utt.FactionId = root.Acquire<int>("Faction", 0);
            utt.Cursor = root.Acquire<int>("Cursor", 0);
            utt.HighlightHeight = root.Acquire<float>("HighlightHeight", 0.0f);
            utt.KeyName = root.Acquire<string>("KeyName", "");
            utt.TypeId = root.Acquire<int>("Type", 0);
            utt.TrapDetectable = root.Acquire<int>("TrapDetectable", 0) != 0;
            utt.TrapDetectDc = root.Acquire<int>("TrapDetectDC", 0);
            utt.TrapDisarmable = root.Acquire<int>("TrapDisarmable", 0) != 0;
            utt.TrapDisarmDc = root.Acquire<int>("DisarmDC", 0);
            utt.IsTrap = root.Acquire<int>("TrapFlag", 0) != 0;
            utt.TrapOnce = root.Acquire<int>("TrapOneShot", 0) != 0;
            utt.TrapType = root.Acquire<int>("TrapType", 0);
            utt.OnDisarmScript = root.Acquire<ResRef>("OnDisarm", ResRef.FromBlank());
            utt.OnTrapTriggeredScript = root.Acquire<ResRef>("OnTrapTriggered", ResRef.FromBlank());
            utt.OnClickScript = root.Acquire<ResRef>("OnClick", ResRef.FromBlank());
            utt.OnHeartbeatScript = root.Acquire<ResRef>("ScriptHeartbeat", ResRef.FromBlank());
            utt.OnEnterScript = root.Acquire<ResRef>("ScriptOnEnter", ResRef.FromBlank());
            utt.OnExitScript = root.Acquire<ResRef>("ScriptOnExit", ResRef.FromBlank());
            utt.OnUserDefinedScript = root.Acquire<ResRef>("ScriptUserDefine", ResRef.FromBlank());
            utt.Comment = root.Acquire<string>("Comment", "");
            utt.Name = root.Acquire<LocalizedString>("LocalizedName", LocalizedString.FromInvalid());
            utt.LoadscreenId = root.Acquire<int>("LoadScreenID", 0);
            utt.PortraitId = root.Acquire<int>("PortraitId", 0);
            utt.PaletteId = root.Acquire<int>("PaletteID", 0);

            return utt;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utt.py:267-324
        // Original: def dismantle_utt(utt: UTT, game: Game = Game.K2, *, use_deprecated: bool = True) -> GFF:
        public static GFF DismantleUtt(UTT utt, Game game = Game.K2, bool useDeprecated = true)
        {
            var gff = new GFF(GFFContent.UTT);
            var root = gff.Root;

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utt.py:293-323
            // Original: Set all UTT fields in GFF root
            root.SetString("Tag", utt.Tag);
            root.SetResRef("TemplateResRef", utt.ResRef);
            root.SetUInt8("AutoRemoveKey", (byte)(utt.AutoRemoveKey ? 1 : 0));
            root.SetUInt32("Faction", (uint)utt.FactionId);
            root.SetUInt8("Cursor", (byte)utt.Cursor);
            root.SetSingle("HighlightHeight", utt.HighlightHeight);
            root.SetString("KeyName", utt.KeyName);
            root.SetInt32("Type", utt.TypeId);
            root.SetUInt8("TrapDetectable", (byte)(utt.TrapDetectable ? 1 : 0));
            root.SetUInt8("TrapDetectDC", (byte)utt.TrapDetectDc);
            root.SetUInt8("TrapDisarmable", (byte)(utt.TrapDisarmable ? 1 : 0));
            root.SetUInt8("DisarmDC", (byte)utt.TrapDisarmDc);
            root.SetUInt8("TrapFlag", (byte)(utt.IsTrap ? 1 : 0));
            root.SetUInt8("TrapOneShot", (byte)(utt.TrapOnce ? 1 : 0));
            root.SetUInt8("TrapType", (byte)utt.TrapType);
            root.SetResRef("OnDisarm", utt.OnDisarmScript);
            root.SetResRef("OnTrapTriggered", utt.OnTrapTriggeredScript);
            root.SetResRef("OnClick", utt.OnClickScript);
            root.SetResRef("ScriptHeartbeat", utt.OnHeartbeatScript);
            root.SetResRef("ScriptOnEnter", utt.OnEnterScript);
            root.SetResRef("ScriptOnExit", utt.OnExitScript);
            root.SetResRef("ScriptUserDefine", utt.OnUserDefinedScript);
            root.SetString("Comment", utt.Comment);

            root.SetUInt8("PaletteID", (byte)utt.PaletteId);

            if (useDeprecated)
            {
                root.SetLocString("LocalizedName", utt.Name);
                root.SetUInt16("LoadScreenID", (ushort)utt.LoadscreenId);
                root.SetUInt16("PortraitId", (ushort)utt.PortraitId);
            }

            return gff;
        }
    }
}