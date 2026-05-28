using System;
using System.Collections.Generic;
using System.Linq;
using KOTORModSync;
using KOTORModSync.Formats.GFF;
using KOTORModSync.Logger;
using KOTORModSync.Resource;
using static KOTORModSync.Common.GameExtensions;
using GFFAuto = KOTORModSync.Formats.GFF.GFFAuto;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utc.py
    // Original: construct_utc and dismantle_utc functions
    public static class UTCHelpers
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utc.py:500-794
        // Original: def construct_utc(gff: GFF) -> UTC:
        public static UTC ConstructUtc(GFF gff)
        {
            var utc = new UTC();
            var root = gff.Root;

            // Extract basic fields
            utc.ResRef = root.Acquire<ResRef>("TemplateResRef", ResRef.FromBlank());
            utc.Tag = root.Acquire<string>("Tag", "");
            utc.Comment = root.Acquire<string>("Comment", "");
            utc.Conversation = root.Acquire<ResRef>("Conversation", ResRef.FromBlank());
            utc.FirstName = root.Acquire<LocalizedString>("FirstName", LocalizedString.FromInvalid());
            utc.LastName = root.Acquire<LocalizedString>("LastName", LocalizedString.FromInvalid());

            // Extract appearance and identity fields
            utc.SubraceId = root.Acquire<int>("SubraceIndex", 0);
            utc.PerceptionId = root.Acquire<int>("PerceptionRange", 0);
            utc.RaceId = root.Acquire<int>("Race", 0);
            utc.AppearanceId = root.Acquire<int>("Appearance_Type", 0);
            utc.GenderId = root.Acquire<int>("Gender", 0);
            utc.FactionId = root.Acquire<int>("FactionID", 0);
            utc.WalkrateId = root.Acquire<int>("WalkRate", 0);
            utc.SoundsetId = root.Acquire<int>("SoundSetFile", 0);
            utc.PortraitId = root.Acquire<int>("PortraitId", 0);
            utc.PaletteId = root.Acquire<int>("PaletteID", 0);
            utc.BodybagId = root.Acquire<int>("BodyBag", 0);
            utc.PortraitResRef = root.Acquire<ResRef>("Portrait", ResRef.FromBlank());
            utc.SaveWill = root.Acquire<int>("SaveWill", 0);
            utc.SaveFortitude = root.Acquire<int>("SaveFortitude", 0);
            utc.Morale = root.Acquire<int>("Morale", 0);
            utc.MoraleRecovery = root.Acquire<int>("MoraleRecovery", 0);
            utc.MoraleBreakpoint = root.Acquire<int>("MoraleBreakpoint", 0);
            utc.BodyVariation = root.Acquire<int>("BodyVariation", 0);
            utc.TextureVariation = root.Acquire<int>("TextureVar", 0);

            // Extract boolean flags - stored as UInt8, use GetUInt8() != 0 (matching UTW fix)
            utc.NotReorienting = root.GetUInt8("NotReorienting") != 0;
            utc.PartyInteract = root.GetUInt8("PartyInteract") != 0;
            utc.NoPermDeath = root.GetUInt8("NoPermDeath") != 0;
            utc.Min1Hp = root.GetUInt8("Min1HP") != 0;
            utc.Plot = root.GetUInt8("Plot") != 0;
            utc.Interruptable = root.GetUInt8("Interruptable") != 0;
            utc.IsPc = root.GetUInt8("IsPC") != 0;
            utc.Disarmable = root.GetUInt8("Disarmable") != 0;
            utc.IgnoreCrePath = root.GetUInt8("IgnoreCrePath") != 0;
            utc.Hologram = root.GetUInt8("Hologram") != 0;
            utc.WillNotRender = root.GetUInt8("WillNotRender") != 0;

            // Extract stats
            utc.Alignment = root.Acquire<int>("GoodEvil", 0);
            utc.ChallengeRating = root.Acquire<float>("ChallengeRating", 0.0f);
            utc.Blindspot = root.Acquire<float>("BlindSpot", 0.0f);
            utc.MultiplierSet = root.Acquire<int>("MultiplierSet", 0);
            utc.NaturalAc = root.Acquire<int>("NaturalAC", 0);
            utc.ReflexBonus = root.Acquire<int>("refbonus", 0);
            utc.WillpowerBonus = root.Acquire<int>("willbonus", 0);
            utc.FortitudeBonus = root.Acquire<int>("fortbonus", 0);

            // Extract ability scores
            utc.Strength = root.Acquire<int>("Str", 0);
            utc.Dexterity = root.Acquire<int>("Dex", 0);
            utc.Constitution = root.Acquire<int>("Con", 0);
            utc.Intelligence = root.Acquire<int>("Int", 0);
            utc.Wisdom = root.Acquire<int>("Wis", 0);
            utc.Charisma = root.Acquire<int>("Cha", 0);

            // Extract hit points and force points
            utc.CurrentHp = root.Acquire<int>("CurrentHitPoints", 0);
            utc.MaxHp = root.Acquire<int>("MaxHitPoints", 0);
            utc.Hp = root.Acquire<int>("HitPoints", 0);
            utc.MaxFp = root.Acquire<int>("ForcePoints", 0);
            utc.Fp = root.Acquire<int>("CurrentForce", 0);

            // Extract script hooks
            utc.OnEndDialog = root.Acquire<ResRef>("ScriptEndDialogu", ResRef.FromBlank());
            utc.OnBlocked = root.Acquire<ResRef>("ScriptOnBlocked", ResRef.FromBlank());
            utc.OnHeartbeat = root.Acquire<ResRef>("ScriptHeartbeat", ResRef.FromBlank());
            utc.OnNotice = root.Acquire<ResRef>("ScriptOnNotice", ResRef.FromBlank());
            utc.OnSpell = root.Acquire<ResRef>("ScriptSpellAt", ResRef.FromBlank());
            utc.OnAttacked = root.Acquire<ResRef>("ScriptAttacked", ResRef.FromBlank());
            utc.OnDamaged = root.Acquire<ResRef>("ScriptDamaged", ResRef.FromBlank());
            utc.OnDisturbed = root.Acquire<ResRef>("ScriptDisturbed", ResRef.FromBlank());
            utc.OnEndRound = root.Acquire<ResRef>("ScriptEndRound", ResRef.FromBlank());
            utc.OnDialog = root.Acquire<ResRef>("ScriptDialogue", ResRef.FromBlank());
            utc.OnSpawn = root.Acquire<ResRef>("ScriptSpawn", ResRef.FromBlank());
            utc.OnRested = root.Acquire<ResRef>("ScriptRested", ResRef.FromBlank());
            utc.OnDeath = root.Acquire<ResRef>("ScriptDeath", ResRef.FromBlank());
            utc.OnUserDefined = root.Acquire<ResRef>("ScriptUserDefine", ResRef.FromBlank());

            // Extract skills from SkillList
            var skillList = root.Acquire<GFFList>("SkillList", new GFFList());
            if (skillList != null)
            {
                var skill0 = skillList.At(0);
                if (skill0 != null)
                {
                    utc.ComputerUse = skill0.Acquire<int>("Rank", 0);
                }
                var skill1 = skillList.At(1);
                if (skill1 != null)
                {
                    utc.Demolitions = skill1.Acquire<int>("Rank", 0);
                }
                var skill2 = skillList.At(2);
                if (skill2 != null)
                {
                    utc.Stealth = skill2.Acquire<int>("Rank", 0);
                }
                var skill3 = skillList.At(3);
                if (skill3 != null)
                {
                    utc.Awareness = skill3.Acquire<int>("Rank", 0);
                }
                var skill4 = skillList.At(4);
                if (skill4 != null)
                {
                    utc.Persuade = skill4.Acquire<int>("Rank", 0);
                }
                var skill5 = skillList.At(5);
                if (skill5 != null)
                {
                    utc.Repair = skill5.Acquire<int>("Rank", 0);
                }
                var skill6 = skillList.At(6);
                if (skill6 != null)
                {
                    utc.Security = skill6.Acquire<int>("Rank", 0);
                }
                var skill7 = skillList.At(7);
                if (skill7 != null)
                {
                    utc.TreatInjury = skill7.Acquire<int>("Rank", 0);
                }
            }

            // Extract classes from ClassList
            var classList = root.Acquire<GFFList>("ClassList", new GFFList());
            foreach (var classStruct in classList)
            {
                int classId = classStruct.Acquire<int>("Class", 0);
                int classLevel = classStruct.Acquire<int>("ClassLevel", 0);
                var utcClass = new UTCClass(classId, classLevel);

                // Extract powers from KnownList0
                var powerList = classStruct.Acquire<GFFList>("KnownList0", new GFFList());
                int index = 0;
                foreach (var powerStruct in powerList)
                {
                    int spell = powerStruct.Acquire<int>("Spell", 0);
                    utcClass.Powers.Add(spell);
                    index++;
                }

                utc.Classes.Add(utcClass);
            }

            // Extract feats from FeatList
            var featList = root.Acquire<GFFList>("FeatList", new GFFList());
            int featIndex = 0;
            foreach (var featStruct in featList)
            {
                int featId = featStruct.Acquire<int>("Feat", 0);
                utc.Feats.Add(featId);
                featIndex++;
            }

            // Extract equipment from Equip_ItemList
            var equipmentList = root.Acquire<GFFList>("Equip_ItemList", new GFFList());
            foreach (var equipmentStruct in equipmentList)
            {
                EquipmentSlot slot = (EquipmentSlot)equipmentStruct.StructId;
                ResRef resref = equipmentStruct.Acquire<ResRef>("EquippedRes", ResRef.FromBlank());
                bool droppable = equipmentStruct.Acquire<int>("Dropable", 0) != 0;
                utc.Equipment[slot] = new InventoryItem(resref, droppable);
            }

            // Extract inventory from ItemList
            var itemList = root.Acquire<GFFList>("ItemList", new GFFList());
            foreach (var itemStruct in itemList)
            {
                ResRef resref = itemStruct.Acquire<ResRef>("InventoryRes", ResRef.FromBlank());
                bool droppable = itemStruct.Acquire<int>("Dropable", 0) != 0;
                utc.Inventory.Add(new InventoryItem(resref, droppable));
            }

            return utc;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utc.py:797-954
        // Original: def dismantle_utc(utc: UTC, game: Game = Game.K2, *, use_deprecated: bool = True) -> GFF:
        public static GFF DismantleUtc(UTC utc, Game game = Game.K2, bool useDeprecated = true)
        {
            var gff = new GFF(GFFContent.UTC);
            var root = gff.Root;

            // Set basic fields
            root.SetResRef("TemplateResRef", utc.ResRef);
            root.SetString("Tag", utc.Tag);
            root.SetString("Comment", utc.Comment);
            root.SetResRef("Conversation", utc.Conversation);
            root.SetLocString("FirstName", utc.FirstName);
            root.SetLocString("LastName", utc.LastName);

            // Set appearance and identity fields
            root.SetUInt8("SubraceIndex", (byte)utc.SubraceId);
            root.SetUInt8("PerceptionRange", (byte)utc.PerceptionId);
            root.SetUInt8("Race", (byte)utc.RaceId);
            root.SetUInt16("Appearance_Type", (ushort)utc.AppearanceId);
            root.SetUInt8("Gender", (byte)utc.GenderId);
            root.SetUInt16("FactionID", (ushort)utc.FactionId);
            root.SetInt32("WalkRate", utc.WalkrateId);
            root.SetUInt16("SoundSetFile", (ushort)utc.SoundsetId);
            root.SetUInt16("PortraitId", (ushort)utc.PortraitId);
            root.SetResRef("Portrait", utc.PortraitResRef);
            root.SetUInt8("SaveWill", (byte)utc.SaveWill);
            root.SetUInt8("SaveFortitude", (byte)utc.SaveFortitude);
            root.SetUInt8("Morale", (byte)utc.Morale);
            root.SetUInt8("MoraleRecovery", (byte)utc.MoraleRecovery);
            root.SetUInt8("MoraleBreakpoint", (byte)utc.MoraleBreakpoint);
            root.SetUInt8("BodyVariation", (byte)utc.BodyVariation);
            root.SetUInt8("TextureVar", (byte)utc.TextureVariation);

            // Set boolean flags
            root.SetUInt8("NotReorienting", utc.NotReorienting ? (byte)1 : (byte)0);
            root.SetUInt8("PartyInteract", utc.PartyInteract ? (byte)1 : (byte)0);
            root.SetUInt8("NoPermDeath", utc.NoPermDeath ? (byte)1 : (byte)0);
            root.SetUInt8("Min1HP", utc.Min1Hp ? (byte)1 : (byte)0);
            root.SetUInt8("Plot", utc.Plot ? (byte)1 : (byte)0);
            root.SetUInt8("Interruptable", utc.Interruptable ? (byte)1 : (byte)0);
            root.SetUInt8("IsPC", utc.IsPc ? (byte)1 : (byte)0);
            root.SetUInt8("Disarmable", utc.Disarmable ? (byte)1 : (byte)0);

            // Set stats
            root.SetUInt8("GoodEvil", (byte)utc.Alignment);
            root.SetSingle("ChallengeRating", utc.ChallengeRating);
            root.SetUInt8("NaturalAC", (byte)utc.NaturalAc);
            root.SetInt16("refbonus", (short)utc.ReflexBonus);
            root.SetInt16("willbonus", (short)utc.WillpowerBonus);
            root.SetInt16("fortbonus", (short)utc.FortitudeBonus);

            // Set ability scores
            root.SetUInt8("Str", (byte)utc.Strength);
            root.SetUInt8("Dex", (byte)utc.Dexterity);
            root.SetUInt8("Con", (byte)utc.Constitution);
            root.SetUInt8("Int", (byte)utc.Intelligence);
            root.SetUInt8("Wis", (byte)utc.Wisdom);
            root.SetUInt8("Cha", (byte)utc.Charisma);

            // Set hit points and force points
            root.SetInt16("CurrentHitPoints", (short)utc.CurrentHp);
            root.SetInt16("MaxHitPoints", (short)utc.MaxHp);
            root.SetInt16("HitPoints", (short)utc.Hp);
            root.SetInt16("CurrentForce", (short)utc.Fp);
            root.SetInt16("ForcePoints", (short)utc.MaxFp);

            // Set script hooks
            root.SetResRef("ScriptEndDialogu", utc.OnEndDialog);
            root.SetResRef("ScriptOnBlocked", utc.OnBlocked);
            root.SetResRef("ScriptHeartbeat", utc.OnHeartbeat);
            root.SetResRef("ScriptOnNotice", utc.OnNotice);
            root.SetResRef("ScriptSpellAt", utc.OnSpell);
            root.SetResRef("ScriptAttacked", utc.OnAttacked);
            root.SetResRef("ScriptDamaged", utc.OnDamaged);
            root.SetResRef("ScriptDisturbed", utc.OnDisturbed);
            root.SetResRef("ScriptEndRound", utc.OnEndRound);
            root.SetResRef("ScriptDialogue", utc.OnDialog);
            root.SetResRef("ScriptSpawn", utc.OnSpawn);
            root.SetResRef("ScriptDeath", utc.OnDeath);
            root.SetResRef("ScriptUserDefine", utc.OnUserDefined);

            root.SetUInt8("PaletteID", (byte)utc.PaletteId);

            // Set skills in SkillList
            var skillList = new GFFList();
            root.SetList("SkillList", skillList);
            var skill0 = skillList.Add(0);
            skill0.SetUInt8("Rank", (byte)utc.ComputerUse);
            var skill1 = skillList.Add(0);
            skill1.SetUInt8("Rank", (byte)utc.Demolitions);
            var skill2 = skillList.Add(0);
            skill2.SetUInt8("Rank", (byte)utc.Stealth);
            var skill3 = skillList.Add(0);
            skill3.SetUInt8("Rank", (byte)utc.Awareness);
            var skill4 = skillList.Add(0);
            skill4.SetUInt8("Rank", (byte)utc.Persuade);
            var skill5 = skillList.Add(0);
            skill5.SetUInt8("Rank", (byte)utc.Repair);
            var skill6 = skillList.Add(0);
            skill6.SetUInt8("Rank", (byte)utc.Security);
            var skill7 = skillList.Add(0);
            skill7.SetUInt8("Rank", (byte)utc.TreatInjury);

            // Set classes in ClassList
            var classList = new GFFList();
            root.SetList("ClassList", classList);
            foreach (var utcClass in utc.Classes)
            {
                var classStruct = classList.Add(2);
                classStruct.SetInt32("Class", utcClass.ClassId);
                classStruct.SetInt16("ClassLevel", (short)utcClass.ClassLevel);
                var powerList = new GFFList();
                classStruct.SetList("KnownList0", powerList);
                foreach (var power in utcClass.Powers)
                {
                    var powerStruct = powerList.Add(3);
                    powerStruct.SetUInt16("Spell", (ushort)power);
                    powerStruct.SetUInt8("SpellFlags", 1);
                    powerStruct.SetUInt8("SpellMetaMagic", 0);
                }
            }

            // Set feats in FeatList
            var featList = new GFFList();
            root.SetList("FeatList", featList);
            foreach (var feat in utc.Feats)
            {
                featList.Add(1).SetUInt16("Feat", (ushort)feat);
            }

            // Set equipment in Equip_ItemList
            var equipmentList = new GFFList();
            root.SetList("Equip_ItemList", equipmentList);
            foreach (var kvp in utc.Equipment)
            {
                var equipmentStruct = equipmentList.Add((int)kvp.Key);
                equipmentStruct.SetResRef("EquippedRes", kvp.Value.ResRef);
                if (kvp.Value.Droppable)
                {
                    equipmentStruct.SetUInt8("Dropable", 1);
                }
            }

            // Set inventory in ItemList
            var itemList = new GFFList();
            root.SetList("ItemList", itemList);
            for (int i = 0; i < utc.Inventory.Count; i++)
            {
                var item = utc.Inventory[i];
                var itemStruct = itemList.Add(i);
                itemStruct.SetResRef("InventoryRes", item.ResRef);
                itemStruct.SetUInt16("Repos_PosX", (ushort)i);
                itemStruct.SetUInt16("Repos_Posy", 0);
                if (item.Droppable)
                {
                    itemStruct.SetUInt8("Dropable", 1);
                }
            }

            // KotOR 2 only fields
            if (game.IsK2())
            {
                root.SetSingle("BlindSpot", utc.Blindspot);
                root.SetUInt8("MultiplierSet", (byte)utc.MultiplierSet);
                root.SetUInt8("IgnoreCrePath", utc.IgnoreCrePath ? (byte)1 : (byte)0);
                root.SetUInt8("Hologram", utc.Hologram ? (byte)1 : (byte)0);
                root.SetUInt8("WillNotRender", utc.WillNotRender ? (byte)1 : (byte)0);
            }

            // Deprecated fields
            if (useDeprecated)
            {
                root.SetUInt8("BodyBag", (byte)utc.BodybagId);
                root.SetString("Deity", utc.Deity);
                root.SetLocString("Description", utc.Description);
                root.SetUInt8("LawfulChaotic", (byte)utc.Lawfulness);
                root.SetInt32("Phenotype", utc.PhenotypeId);
                root.SetResRef("ScriptRested", utc.OnRested);
                root.SetString("Subrace", utc.SubraceName);
                root.SetList("SpecAbilityList", new GFFList());
                root.SetList("TemplateList", new GFFList());
            }

            return gff;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utc.py:957-976
        // Original: def read_utc(source: SOURCE_TYPES, offset: int = 0, size: int | None = None) -> UTC:
        public static UTC ReadUtc(byte[] data, int offset = 0, int size = -1)
        {
            byte[] dataToRead = data;
            if (size > 0 && offset + size <= data.Length)
            {
                dataToRead = new byte[size];
                System.Array.Copy(data, offset, dataToRead, 0, size);
            }
            GFF gff = GFF.FromBytes(dataToRead);
            return ConstructUtc(gff);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utc.py:978-993
        // Original: def bytes_utc(utc: UTC, game: Game = Game.K2, file_format: ResourceType = ResourceType.GFF) -> bytes:
        public static byte[] BytesUtc(UTC utc, Game game = Game.K2, ResourceType fileFormat = null)
        {
            if (fileFormat == null)
            {
                fileFormat = ResourceType.UTC;
            }
            GFF gff = DismantleUtc(utc, game);
            return GFFAuto.BytesGff(gff, fileFormat);
        }
    }
}