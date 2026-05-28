using KOTORModSync;
using KOTORModSync.Formats.GFF;
using KOTORModSync.Resource;
using static KOTORModSync.Common.GameExtensions;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/ute.py
    // Original: construct_ute and dismantle_ute functions
    public static class UTEHelpers
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/ute.py:235-274
        // Original: def construct_ute(gff: GFF) -> UTE:
        public static UTE ConstructUte(GFF gff)
        {
            var ute = new UTE();
            var root = gff.Root;

            // Extract basic fields
            ute.Tag = root.Acquire<string>("Tag", "");
            ute.ResRef = root.Acquire<ResRef>("TemplateResRef", ResRef.FromBlank());
            ute.Active = root.Acquire<int>("Active", 0) != 0;
            ute.DifficultyId = root.Acquire<int>("DifficultyIndex", 0);
            ute.DifficultyIndex = root.Acquire<int>("Difficulty", 0);
            ute.Faction = root.Acquire<int>("Faction", 0);
            ute.MaxCreatures = root.Acquire<int>("MaxCreatures", 0);
            ute.PlayerOnly = root.Acquire<int>("PlayerOnly", 0) != 0 ? 1 : 0;
            ute.RecCreatures = root.Acquire<int>("RecCreatures", 0);
            ute.Reset = root.Acquire<int>("Reset", 0) != 0 ? 1 : 0;
            ute.ResetTime = root.Acquire<int>("ResetTime", 0);
            ute.Respawn = root.Acquire<int>("Respawns", 0);
            ute.SingleSpawn = root.Acquire<int>("SpawnOption", 0) != 0 ? 1 : 0;
            ute.OnEnteredScript = root.Acquire<ResRef>("OnEntered", ResRef.FromBlank());
            ute.OnExitScript = root.Acquire<ResRef>("OnExit", ResRef.FromBlank());
            ute.OnExhaustedScript = root.Acquire<ResRef>("OnExhausted", ResRef.FromBlank());
            ute.OnHeartbeatScript = root.Acquire<ResRef>("OnHeartbeat", ResRef.FromBlank());
            ute.OnUserDefinedScript = root.Acquire<ResRef>("OnUserDefined", ResRef.FromBlank());
            ute.Comment = root.Acquire<string>("Comment", "");
            ute.Name = root.Acquire<LocalizedString>("LocalizedName", LocalizedString.FromInvalid());
            ute.PaletteId = root.Acquire<int>("PaletteID", 0);

            // Extract creature list
            var creatureList = root.Acquire<GFFList>("CreatureList", new GFFList());
            ute.Creatures.Clear();
            foreach (var creatureStruct in creatureList)
            {
                var creature = new UTECreature();
                // Matching PyKotor implementation: creature.appearance_id = creature_struct.acquire("Appearance", 0)
                creature.Appearance = creatureStruct.Acquire<int>("Appearance", 0);
                // Matching PyKotor implementation: creature.challenge_rating = creature_struct.acquire("CR", 0.0)
                creature.CR = (int)creatureStruct.Acquire<float>("CR", 0.0f);
                // Matching PyKotor implementation: creature.single_spawn = bool(creature_struct.acquire("SingleSpawn", 0))
                creature.SingleSpawn = creatureStruct.Acquire<int>("SingleSpawn", 0) != 0 ? 1 : 0;
                // Matching PyKotor implementation: creature.resref = creature_struct.acquire("ResRef", ResRef.from_blank())
                creature.ResRef = creatureStruct.Acquire<ResRef>("ResRef", ResRef.FromBlank());
                // Matching PyKotor implementation: creature.guaranteed_count = creature_struct.acquire("GuaranteedCount", 0)
                creature.GuaranteedCount = creatureStruct.Acquire<int>("GuaranteedCount", 0);
                ute.Creatures.Add(creature);
            }

            return ute;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/ute.py:277-322
        // Original: def dismantle_ute(ute: UTE, game: Game = Game.K2, *, use_deprecated: bool = True) -> GFF:
        public static GFF DismantleUte(UTE ute, Game game = Game.K2, bool useDeprecated = true)
        {
            var gff = new GFF(GFFContent.UTE);
            var root = gff.Root;

            // Set basic fields
            root.SetString("Tag", ute.Tag);
            root.SetResRef("TemplateResRef", ute.ResRef);
            root.SetUInt8("Active", (byte)(ute.Active ? 1 : 0));
            root.SetInt32("DifficultyIndex", ute.DifficultyId);
            root.SetUInt32("Faction", (uint)ute.Faction);
            root.SetInt32("MaxCreatures", ute.MaxCreatures);
            root.SetUInt8("PlayerOnly", (byte)(ute.PlayerOnly != 0 ? 1 : 0));
            root.SetInt32("RecCreatures", ute.RecCreatures);
            root.SetUInt8("Reset", (byte)(ute.Reset != 0 ? 1 : 0));
            root.SetInt32("ResetTime", ute.ResetTime);
            root.SetInt32("Respawns", ute.Respawn);
            root.SetInt32("SpawnOption", ute.SingleSpawn);
            root.SetResRef("OnEntered", ute.OnEnteredScript);
            root.SetResRef("OnExit", ute.OnExitScript);
            root.SetResRef("OnExhausted", ute.OnExhaustedScript);
            root.SetResRef("OnHeartbeat", ute.OnHeartbeatScript);
            root.SetResRef("OnUserDefined", ute.OnUserDefinedScript);
            root.SetString("Comment", ute.Comment);

            // Set creature list
            if (ute.Creatures.Count > 0)
            {
                var creatureList = new GFFList();
                foreach (var creature in ute.Creatures)
                {
                    var creatureStruct = creatureList.Add();
                    // Matching PyKotor implementation: creature_struct.set_int32("Appearance", creature.appearance_id)
                    creatureStruct.SetInt32("Appearance", creature.Appearance);
                    // Matching PyKotor implementation: creature_struct.set_single("CR", creature.challenge_rating)
                    creatureStruct.SetSingle("CR", creature.CR);
                    creatureStruct.SetUInt8("SingleSpawn", (byte)(creature.SingleSpawn != 0 ? 1 : 0));
                    creatureStruct.SetResRef("ResRef", creature.ResRef);
                    if (game.IsK2())
                    {
                        creatureStruct.SetInt32("GuaranteedCount", creature.GuaranteedCount);
                    }
                }
                root.SetList("CreatureList", creatureList);
            }

            if (useDeprecated)
            {
                // Matching PyKotor implementation: root.set_locstring("LocalizedName", ute.name)
                root.SetLocString("LocalizedName", ute.Name);
                // Matching PyKotor implementation: root.set_int32("Difficulty", ute.unused_difficulty)
                root.SetInt32("Difficulty", ute.DifficultyIndex);
                // Matching PyKotor implementation: root.set_uint8("PaletteID", ute.palette_id)
                root.SetUInt8("PaletteID", (byte)ute.PaletteId);
            }

            return gff;
        }
    }
}