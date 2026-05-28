using System.Collections.Generic;
using KOTORModSync;
using KOTORModSync.Formats.GFF;
using KOTORModSync.Resource;
using static KOTORModSync.Common.GameExtensions;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utp.py
    // Original: construct_utp and dismantle_utp functions
    public static class UTPHelpers
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utp.py:185-258
        // Original: def construct_utp(gff: GFF) -> UTP:
        public static UTP ConstructUtp(GFF gff)
        {
            var utp = new UTP();
            var root = gff.Root;

            // Extract basic fields
            utp.Tag = root.Acquire<string>("Tag", "");
            utp.Name = root.Acquire<LocalizedString>("LocName", LocalizedString.FromInvalid());
            utp.ResRef = root.Acquire<ResRef>("TemplateResRef", ResRef.FromBlank());
            // Boolean fields stored as UInt8 - use GetUInt8() != 0 (matching UTW fix)
            utp.AutoRemoveKey = root.GetUInt8("AutoRemoveKey") != 0;
            utp.Conversation = root.Acquire<ResRef>("Conversation", ResRef.FromBlank());
            utp.FactionId = root.Acquire<int>("Faction", 0);
            utp.Plot = root.GetUInt8("Plot") != 0;
            utp.NotBlastable = root.GetUInt8("NotBlastable") != 0;
            utp.Min1Hp = root.GetUInt8("Min1HP") != 0;
            utp.KeyRequired = root.GetUInt8("KeyRequired") != 0;
            utp.Lockable = root.GetUInt8("Lockable") != 0;
            utp.Locked = root.GetUInt8("Locked") != 0;
            utp.UnlockDc = root.GetUInt8("OpenLockDC");
            utp.KeyName = root.Acquire<string>("KeyName", "");
            utp.AnimationState = root.GetUInt8("AnimationState");
            utp.AppearanceId = root.Acquire<int>("Appearance", 0);
            utp.MaximumHp = root.Acquire<int>("HP", 0);
            utp.CurrentHp = root.Acquire<int>("CurrentHP", 0);
            utp.Hardness = root.GetUInt8("Hardness");
            utp.Fortitude = root.GetUInt8("Fort");
            utp.HasInventory = root.GetUInt8("HasInventory") != 0;
            utp.PartyInteract = root.GetUInt8("PartyInteract") != 0;
            utp.Static = root.GetUInt8("Static") != 0;
            utp.Useable = root.GetUInt8("Useable") != 0;
            utp.Comment = root.Acquire<string>("Comment", "");
            utp.UnlockDiff = root.Acquire<int>("OpenLockDiff", 0);
            utp.UnlockDiffMod = root.Acquire<int>("OpenLockDiffMod", 0);
            utp.Description = root.Acquire<LocalizedString>("Description", LocalizedString.FromInvalid());
            utp.Reflex = root.Acquire<int>("Ref", 0);
            utp.Will = root.Acquire<int>("Will", 0);

            // Extract script hooks
            utp.OnClosed = root.Acquire<ResRef>("OnClosed", ResRef.FromBlank());
            utp.OnDamaged = root.Acquire<ResRef>("OnDamaged", ResRef.FromBlank());
            utp.OnDeath = root.Acquire<ResRef>("OnDeath", ResRef.FromBlank());
            utp.OnHeartbeat = root.Acquire<ResRef>("OnHeartbeat", ResRef.FromBlank());
            utp.OnLock = root.Acquire<ResRef>("OnLock", ResRef.FromBlank());
            utp.OnMelee = root.Acquire<ResRef>("OnMeleeAttacked", ResRef.FromBlank());
            utp.OnOpen = root.Acquire<ResRef>("OnOpen", ResRef.FromBlank());
            utp.OnPower = root.Acquire<ResRef>("OnSpellCastAt", ResRef.FromBlank());
            utp.OnUnlock = root.Acquire<ResRef>("OnUnlock", ResRef.FromBlank());
            utp.OnUserDefined = root.Acquire<ResRef>("OnUserDefined", ResRef.FromBlank());
            utp.OnEndDialog = root.Acquire<ResRef>("OnEndDialogue", ResRef.FromBlank());
            utp.OnInventory = root.Acquire<ResRef>("OnInvDisturbed", ResRef.FromBlank());
            utp.OnUsed = root.Acquire<ResRef>("OnUsed", ResRef.FromBlank());
            utp.OnOpenFailed = root.Acquire<ResRef>("OnFailToOpen", ResRef.FromBlank());

            // Extract inventory
            var itemList = root.Acquire<GFFList>("ItemList", new GFFList());
            utp.Inventory.Clear();
            foreach (var itemStruct in itemList)
            {
                var resref = itemStruct.Acquire<ResRef>("InventoryRes", ResRef.FromBlank());
                bool droppable = itemStruct.GetUInt8("Dropable") != 0;
                if (resref != null && !string.IsNullOrEmpty(resref.ToString()))
                {
                    utp.Inventory.Add(new InventoryItem(resref, droppable));
                }
            }

            return utp;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utp.py:261-377
        // Original: def dismantle_utp(utp: UTP, game: Game = Game.K2, *, use_deprecated: bool = True) -> GFF:
        public static GFF DismantleUtp(UTP utp, Game game = Game.K2, bool useDeprecated = true)
        {
            var gff = new GFF(GFFContent.UTP);
            var root = gff.Root;

            // Set basic fields
            root.SetString("Tag", utp.Tag);
            root.SetLocString("LocName", utp.Name);
            root.SetResRef("TemplateResRef", utp.ResRef);
            root.SetUInt8("AutoRemoveKey", utp.AutoRemoveKey ? (byte)1 : (byte)0);
            root.SetResRef("Conversation", utp.Conversation);
            root.SetUInt32("Faction", (uint)utp.FactionId);
            root.SetUInt8("Plot", utp.Plot ? (byte)1 : (byte)0);
            root.SetUInt8("Min1HP", utp.Min1Hp ? (byte)1 : (byte)0);
            root.SetUInt8("KeyRequired", utp.KeyRequired ? (byte)1 : (byte)0);
            root.SetUInt8("Lockable", utp.Lockable ? (byte)1 : (byte)0);
            root.SetUInt8("Locked", utp.Locked ? (byte)1 : (byte)0);
            root.SetUInt8("OpenLockDC", (byte)utp.UnlockDc);
            root.SetString("KeyName", utp.KeyName);
            root.SetUInt8("AnimationState", (byte)utp.AnimationState);
            root.SetUInt32("Appearance", (uint)utp.AppearanceId);
            root.SetInt16("HP", (short)utp.MaximumHp);
            root.SetInt16("CurrentHP", (short)utp.CurrentHp);
            root.SetUInt8("Hardness", (byte)utp.Hardness);
            root.SetUInt8("Fort", (byte)utp.Fortitude);
            root.SetUInt8("HasInventory", utp.HasInventory ? (byte)1 : (byte)0);
            root.SetUInt8("PartyInteract", utp.PartyInteract ? (byte)1 : (byte)0);
            root.SetUInt8("Static", utp.Static ? (byte)1 : (byte)0);
            root.SetUInt8("Useable", utp.Useable ? (byte)1 : (byte)0);
            root.SetString("Comment", utp.Comment);

            // Set script hooks
            root.SetResRef("OnClosed", utp.OnClosed);
            root.SetResRef("OnDamaged", utp.OnDamaged);
            root.SetResRef("OnDeath", utp.OnDeath);
            root.SetResRef("OnHeartbeat", utp.OnHeartbeat);
            root.SetResRef("OnLock", utp.OnLock);
            root.SetResRef("OnMeleeAttacked", utp.OnMelee);
            root.SetResRef("OnOpen", utp.OnOpen);
            root.SetResRef("OnSpellCastAt", utp.OnPower);
            root.SetResRef("OnUnlock", utp.OnUnlock);
            root.SetResRef("OnUserDefined", utp.OnUserDefined);
            root.SetResRef("OnEndDialogue", utp.OnEndDialog);
            root.SetResRef("OnInvDisturbed", utp.OnInventory);
            root.SetResRef("OnUsed", utp.OnUsed);

            // Set inventory
            var itemList = new GFFList();
            root.SetList("ItemList", itemList);
            if (utp.Inventory != null)
            {
                for (int i = 0; i < utp.Inventory.Count; i++)
                {
                    var item = utp.Inventory[i];
                    var itemStruct = itemList.Add(i);
                    itemStruct.SetResRef("InventoryRes", item.ResRef);
                    itemStruct.SetUInt16("Repos_PosX", (ushort)i);
                    itemStruct.SetUInt16("Repos_PosY", 0);
                    if (item.Droppable)
                    {
                        itemStruct.SetUInt8("Dropable", 1);
                    }
                }
            }

            root.SetUInt8("PaletteID", 0);

            // KotOR 2 only fields
            if (game.IsK2())
            {
                root.SetUInt8("NotBlastable", utp.NotBlastable ? (byte)1 : (byte)0);
                root.SetUInt8("OpenLockDiff", (byte)utp.UnlockDiff);
                root.SetInt8("OpenLockDiffMod", (sbyte)utp.UnlockDiffMod);
                root.SetResRef("OnFailToOpen", utp.OnOpenFailed);
            }

            if (useDeprecated)
            {
                root.SetLocString("Description", utp.Description);
                root.SetUInt8("Will", (byte)utp.Will);
                root.SetUInt8("Ref", (byte)utp.Reflex);
            }

            return gff;
        }
    }
}