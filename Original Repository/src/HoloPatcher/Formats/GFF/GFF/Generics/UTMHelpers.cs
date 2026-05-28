using System.Collections.Generic;
using KOTORModSync;
using KOTORModSync.Formats.GFF;
using KOTORModSync.Resource;
using static KOTORModSync.Common.GameExtensions;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utm.py
    // Original: construct_utm and dismantle_utm functions
    public static class UTMHelpers
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utm.py:130-160
        // Original: def construct_utm(gff: GFF) -> UTM:
        public static UTM ConstructUtm(GFF gff)
        {
            var utm = new UTM();
            var root = gff.Root;

            // Extract basic fields
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utm.py:136-145
            utm.ResRef = root.Acquire<ResRef>("ResRef", ResRef.FromBlank());
            utm.Name = root.Acquire<LocalizedString>("LocName", LocalizedString.FromInvalid());
            utm.Tag = root.Acquire<string>("Tag", "");
            utm.MarkUp = root.Acquire<int>("MarkUp", 0);
            utm.MarkDown = root.Acquire<int>("MarkDown", 0);
            utm.OnOpenScript = root.Acquire<ResRef>("OnOpenStore", ResRef.FromBlank());
            utm.Comment = root.Acquire<string>("Comment", "");
            // ID is stored as UInt8, so we need to read it as byte, not int
            byte? idNullable = root.GetUInt8("ID");
            utm.Id = idNullable ?? 0;

            // Extract BuySellFlag
            // Matching PyKotor implementation: utm.can_buy = root.acquire("BuySellFlag", 0) & 1 != 0
            // Matching PyKotor implementation: utm.can_sell = root.acquire("BuySellFlag", 0) & 2 != 0
            // BuySellFlag is stored as UInt8, so we need to read it as byte, not int
            byte? buySellFlagNullable = root.GetUInt8("BuySellFlag");
            byte buySellFlag = buySellFlagNullable ?? (byte)0;
            utm.CanBuy = (buySellFlag & 1) != 0;
            utm.CanSell = (buySellFlag & 2) != 0;

            // Extract inventory
            // Matching PyKotor implementation: item_list: GFFList = root.acquire("ItemList", GFFList())
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utm.py:147-154
            var itemList = root.Acquire<GFFList>("ItemList", new GFFList());
            utm.Items.Clear();
            foreach (var itemStruct in itemList)
            {
                var item = new UTMItem();
                // Matching PyKotor implementation: item.resref = item_struct.acquire("InventoryRes", ResRef.from_blank())
                item.ResRef = itemStruct.Acquire<ResRef>("InventoryRes", ResRef.FromBlank());
                // Matching PyKotor implementation: item.infinite = bool(item_struct.acquire("Infinite", 0))
                item.Infinite = itemStruct.Acquire<int>("Infinite", 0) != 0 ? 1 : 0;
                // Matching PyKotor implementation: item.droppable = bool(item_struct.acquire("Dropable", 0))
                item.Droppable = itemStruct.Acquire<int>("Dropable", 0) != 0 ? 1 : 0;
                utm.Items.Add(item);
            }

            return utm;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utm.py:163-223
        // Original: def dismantle_utm(utm: UTM, game: Game = Game.K2, *, use_deprecated: bool = True) -> GFF:
        public static GFF DismantleUtm(UTM utm, Game game = Game.K2, bool useDeprecated = true)
        {
            var gff = new GFF(GFFContent.UTM);
            var root = gff.Root;

            // Set basic fields
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utm.py:167-174
            root.SetResRef("ResRef", utm.ResRef);
            root.SetLocString("LocName", utm.Name);
            root.SetString("Tag", utm.Tag);
            root.SetInt32("MarkUp", utm.MarkUp);
            root.SetInt32("MarkDown", utm.MarkDown);
            root.SetResRef("OnOpenStore", utm.OnOpenScript);
            root.SetString("Comment", utm.Comment);

            // Set BuySellFlag (can_buy = bit 0, can_sell = bit 1)
            // Matching PyKotor implementation: root.set_uint8("BuySellFlag", utm.can_buy + utm.can_sell * 2)
            int buySellFlag = (utm.CanBuy ? 1 : 0) + (utm.CanSell ? 2 : 0);
            root.SetUInt8("BuySellFlag", (byte)buySellFlag);

            // Set deprecated ID field if useDeprecated is true
            // Matching PyKotor implementation: if use_deprecated: root.set_uint8("ID", utm.id)
            if (useDeprecated)
            {
                root.SetUInt8("ID", (byte)utm.Id);
            }

            // Set inventory
            // Matching PyKotor implementation: item_list: GFFList = root.set_list("ItemList", GFFList())
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utm.py:176-185
            var itemList = new GFFList();
            for (int i = 0; i < utm.Items.Count; i++)
            {
                var item = utm.Items[i];
                var itemStruct = itemList.Add(i);
                itemStruct.SetResRef("InventoryRes", item.ResRef);
                // Matching PyKotor implementation: item_struct.set_uint16("Repos_PosX", i)
                itemStruct.SetUInt16("Repos_PosX", (ushort)i);
                itemStruct.SetUInt16("Repos_PosY", 0);
                if (item.Droppable != 0)
                {
                    itemStruct.SetUInt8("Dropable", (byte)(item.Droppable != 0 ? 1 : 0));
                }
                if (item.Infinite != 0)
                {
                    itemStruct.SetUInt8("Infinite", (byte)(item.Infinite != 0 ? 1 : 0));
                }
            }
            root.SetList("ItemList", itemList);

            return gff;
        }
    }
}