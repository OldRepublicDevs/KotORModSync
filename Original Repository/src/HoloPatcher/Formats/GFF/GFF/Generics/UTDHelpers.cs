using KOTORModSync;
using KOTORModSync.Formats.GFF;
using KOTORModSync.Resource;
using static KOTORModSync.Common.GameExtensions;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utd.py
    // Original: construct_utd and dismantle_utd functions
    public static class UTDHelpers
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utd.py:498-560
        // Original: def construct_utd(gff: GFF) -> UTD:
        public static UTD ConstructUtd(GFF gff)
        {
            var utd = new UTD();
            var root = gff.Root;

            // Extract basic fields
            utd.Tag = root.Acquire<string>("Tag", "");
            utd.Name = root.Acquire<LocalizedString>("LocName", LocalizedString.FromInvalid());
            utd.ResRef = root.Acquire<ResRef>("TemplateResRef", ResRef.FromBlank());
            // AutoRemoveKey, Plot, Min1HP, KeyRequired, Lockable, Locked, Static, NotBlastable are stored as UInt8 (boolean flags)
            byte? autoRemoveKeyNullable = root.GetUInt8("AutoRemoveKey");
            utd.AutoRemoveKey = (autoRemoveKeyNullable ?? 0) != 0;
            utd.Conversation = root.Acquire<ResRef>("Conversation", ResRef.FromBlank());
            // Faction is stored as UInt32, so we need to read it as uint, not int
            uint? factionNullable = root.GetUInt32("Faction");
            utd.FactionId = factionNullable.HasValue ? (int)factionNullable.Value : 0;
            byte? plotNullable = root.GetUInt8("Plot");
            utd.Plot = (plotNullable ?? 0) != 0;
            byte? min1HpNullable = root.GetUInt8("Min1HP");
            utd.Min1Hp = (min1HpNullable ?? 0) != 0;
            byte? keyRequiredNullable = root.GetUInt8("KeyRequired");
            utd.KeyRequired = (keyRequiredNullable ?? 0) != 0;
            byte? lockableNullable = root.GetUInt8("Lockable");
            utd.Lockable = (lockableNullable ?? 0) != 0;
            byte? lockedNullable = root.GetUInt8("Locked");
            utd.Locked = (lockedNullable ?? 0) != 0;
            // OpenLockDC is stored as UInt8, so we need to read it as byte, not int
            byte? unlockDcNullable = root.GetUInt8("OpenLockDC");
            utd.UnlockDc = unlockDcNullable ?? 0;
            utd.KeyName = root.Acquire<string>("KeyName", "");
            // AnimationState is stored as UInt8, so we need to read it as byte, not int
            byte? animationStateNullable = root.GetUInt8("AnimationState");
            utd.AnimationState = animationStateNullable ?? 0;
            // HP and CurrentHP are stored as Int16, so we need to read them as short, not int
            short? maximumHpNullable = root.GetInt16("HP");
            utd.MaximumHp = maximumHpNullable ?? 0;
            short? currentHpNullable = root.GetInt16("CurrentHP");
            utd.CurrentHp = currentHpNullable ?? 0;
            // Hardness, Fort, GenericType are stored as UInt8, so we need to read them as byte, not int
            byte? hardnessNullable = root.GetUInt8("Hardness");
            utd.Hardness = hardnessNullable ?? 0;
            byte? fortNullable = root.GetUInt8("Fort");
            utd.Fortitude = fortNullable ?? 0;
            byte? genericTypeNullable = root.GetUInt8("GenericType");
            utd.AppearanceId = genericTypeNullable ?? 0;
            byte? staticNullable = root.GetUInt8("Static");
            utd.Static = (staticNullable ?? 0) != 0;
            utd.OnClick = root.Acquire<ResRef>("OnClick", ResRef.FromBlank());
            utd.OnOpenFailed = root.Acquire<ResRef>("OnFailToOpen", ResRef.FromBlank());
            utd.Comment = root.Acquire<string>("Comment", "");
            // OpenLockDiff and OpenState are stored as UInt8 (K2 only), so we need to read them as byte, not int
            byte? unlockDiffNullable = root.GetUInt8("OpenLockDiff");
            utd.UnlockDiff = unlockDiffNullable ?? 0;
            // OpenLockDiffMod is stored as Int8 (K2 only), so we need to read it as sbyte, not int
            sbyte? unlockDiffModNullable = root.GetInt8("OpenLockDiffMod");
            utd.UnlockDiffMod = unlockDiffModNullable ?? 0;
            utd.Description = root.Acquire<LocalizedString>("Description", LocalizedString.FromInvalid());
            // Ref and Will are stored as UInt8 (deprecated), so we need to read them as byte, not int
            byte? reflexNullable = root.GetUInt8("Ref");
            utd.Reflex = reflexNullable ?? 0;
            byte? willpowerNullable = root.GetUInt8("Will");
            utd.Willpower = willpowerNullable ?? 0;
            byte? openStateNullable = root.GetUInt8("OpenState");
            utd.OpenState = openStateNullable ?? 0;
            byte? notBlastableNullable = root.GetUInt8("NotBlastable");
            utd.NotBlastable = (notBlastableNullable ?? 0) != 0;

            // Extract trap properties (deprecated, toolset only)
            byte? trapDetectableNullable = root.GetUInt8("TrapDetectable");
            utd.TrapDetectable = (trapDetectableNullable ?? 0) != 0;
            byte? trapDisarmableNullable = root.GetUInt8("TrapDisarmable");
            utd.TrapDisarmable = (trapDisarmableNullable ?? 0) != 0;
            byte? disarmDcNullable = root.GetUInt8("DisarmDC");
            utd.DisarmDc = disarmDcNullable ?? 0;
            byte? trapOneShotNullable = root.GetUInt8("TrapOneShot");
            utd.TrapOneShot = (trapOneShotNullable ?? 0) != 0;
            byte? trapTypeNullable = root.GetUInt8("TrapType");
            utd.TrapType = trapTypeNullable ?? 0;
            byte? paletteIdNullable = root.GetUInt8("PaletteID");
            utd.PaletteId = paletteIdNullable ?? 0;

            // Extract script hooks
            utd.OnClosed = root.Acquire<ResRef>("OnClosed", ResRef.FromBlank());
            utd.OnDamaged = root.Acquire<ResRef>("OnDamaged", ResRef.FromBlank());
            utd.OnDeath = root.Acquire<ResRef>("OnDeath", ResRef.FromBlank());
            utd.OnHeartbeat = root.Acquire<ResRef>("OnHeartbeat", ResRef.FromBlank());
            utd.OnLock = root.Acquire<ResRef>("OnLock", ResRef.FromBlank());
            utd.OnMelee = root.Acquire<ResRef>("OnMeleeAttacked", ResRef.FromBlank());
            utd.OnOpen = root.Acquire<ResRef>("OnOpen", ResRef.FromBlank());
            utd.OnUnlock = root.Acquire<ResRef>("OnUnlock", ResRef.FromBlank());
            utd.OnUserDefined = root.Acquire<ResRef>("OnUserDefined", ResRef.FromBlank());
            utd.OnPower = root.Acquire<ResRef>("OnSpellCastAt", ResRef.FromBlank());

            return utd;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utd.py:563-665
        // Original: def dismantle_utd(utd: UTD, game: Game = Game.K2, *, use_deprecated: bool = True) -> GFF:
        public static GFF DismantleUtd(UTD utd, Game game = Game.K2, bool useDeprecated = true)
        {
            var gff = new GFF(GFFContent.UTD);
            var root = gff.Root;

            // Set basic fields
            root.SetString("Tag", utd.Tag);
            root.SetLocString("LocName", utd.Name);
            root.SetResRef("TemplateResRef", utd.ResRef);
            root.SetUInt8("AutoRemoveKey", utd.AutoRemoveKey ? (byte)1 : (byte)0);
            root.SetResRef("Conversation", utd.Conversation);
            root.SetUInt32("Faction", (uint)utd.FactionId);
            root.SetUInt8("Plot", utd.Plot ? (byte)1 : (byte)0);
            root.SetUInt8("Min1HP", utd.Min1Hp ? (byte)1 : (byte)0);
            root.SetUInt8("KeyRequired", utd.KeyRequired ? (byte)1 : (byte)0);
            root.SetUInt8("Lockable", utd.Lockable ? (byte)1 : (byte)0);
            root.SetUInt8("Locked", utd.Locked ? (byte)1 : (byte)0);
            root.SetUInt8("OpenLockDC", (byte)utd.UnlockDc);
            root.SetString("KeyName", utd.KeyName);
            root.SetUInt8("AnimationState", (byte)utd.AnimationState);
            root.SetInt16("HP", (short)utd.MaximumHp);
            root.SetInt16("CurrentHP", (short)utd.CurrentHp);
            root.SetUInt8("Hardness", (byte)utd.Hardness);
            root.SetUInt8("Fort", (byte)utd.Fortitude);
            root.SetUInt8("GenericType", (byte)utd.AppearanceId);
            root.SetUInt8("Static", utd.Static ? (byte)1 : (byte)0);
            root.SetResRef("OnClick", utd.OnClick);
            root.SetResRef("OnFailToOpen", utd.OnOpenFailed);
            root.SetString("Comment", utd.Comment);

            // KotOR 2 only fields
            // Write OpenLockDiff if it has a non-zero value (for roundtrip compatibility)
            // or if game is K2 (matching Python behavior)
            if (game.IsK2() || utd.UnlockDiff != 0)
            {
                root.SetUInt8("OpenLockDiff", (byte)utd.UnlockDiff);
            }
            if (game.IsK2() || utd.UnlockDiffMod != 0)
            {
                root.SetInt8("OpenLockDiffMod", (sbyte)utd.UnlockDiffMod);
            }
            if (game.IsK2())
            {
                root.SetUInt8("OpenState", (byte)utd.OpenState);
                root.SetUInt8("NotBlastable", utd.NotBlastable ? (byte)1 : (byte)0);
            }

            if (useDeprecated)
            {
                root.SetLocString("Description", utd.Description);
                root.SetUInt8("Ref", (byte)utd.Reflex);
                root.SetUInt8("Will", (byte)utd.Willpower);
                root.SetUInt8("TrapDetectable", utd.TrapDetectable ? (byte)1 : (byte)0);
                root.SetUInt8("TrapDisarmable", utd.TrapDisarmable ? (byte)1 : (byte)0);
                root.SetUInt8("DisarmDC", (byte)utd.DisarmDc);
                root.SetUInt8("TrapOneShot", utd.TrapOneShot ? (byte)1 : (byte)0);
                root.SetUInt8("TrapType", (byte)utd.TrapType);
                root.SetUInt8("PaletteID", (byte)utd.PaletteId);
            }

            // Set script hooks
            root.SetResRef("OnClosed", utd.OnClosed);
            root.SetResRef("OnDamaged", utd.OnDamaged);
            root.SetResRef("OnDeath", utd.OnDeath);
            root.SetResRef("OnHeartbeat", utd.OnHeartbeat);
            root.SetResRef("OnLock", utd.OnLock);
            root.SetResRef("OnMeleeAttacked", utd.OnMelee);
            root.SetResRef("OnOpen", utd.OnOpen);
            root.SetResRef("OnUnlock", utd.OnUnlock);
            root.SetResRef("OnUserDefined", utd.OnUserDefined);
            root.SetResRef("OnSpellCastAt", utd.OnPower);

            return gff;
        }
    }
}