using System;
using System.Collections.Generic;
using KOTORModSync;
using KOTORModSync.Formats.GFF;
using KOTORModSync.Resource;
using static KOTORModSync.Common.GameExtensions;
using GFFAuto = KOTORModSync.Formats.GFF.GFFAuto;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/uti.py
    // Original: construct_uti and dismantle_uti functions
    public static class UTIHelpers
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/uti.py:203-280
        // Original: def construct_uti(gff: GFF) -> UTI:
        public static UTI ConstructUti(GFF gff)
        {
            var uti = new UTI();
            var root = gff.Root;

            // Extract basic fields
            uti.ResRef = root.Acquire<ResRef>("TemplateResRef", ResRef.FromBlank());
            uti.BaseItem = root.Acquire<int>("BaseItem", 0);
            uti.Name = root.Acquire<LocalizedString>("LocalizedName", LocalizedString.FromInvalid());
            uti.Description = root.Acquire<LocalizedString>("DescIdentified", LocalizedString.FromInvalid());
            uti.DescriptionUnidentified = root.Acquire<LocalizedString>("Description", LocalizedString.FromInvalid());
            uti.Tag = root.Acquire<string>("Tag", "");
            // Charges is stored as UInt8, so we need to read it as byte, not int
            byte? chargesNullable = root.GetUInt8("Charges");
            uti.Charges = chargesNullable ?? 0;
            // Cost is stored as UInt32, so we need to read it as uint, not int
            uint? costNullable = root.GetUInt32("Cost");
            uti.Cost = costNullable.HasValue ? (int)costNullable.Value : 0;
            // StackSize is stored as UInt16, so we need to read it as ushort, not int
            ushort? stackSizeNullable = root.GetUInt16("StackSize");
            uti.StackSize = stackSizeNullable ?? 0;
            // Plot is stored as UInt8, so we need to read it as byte, not int
            byte? plotNullable = root.GetUInt8("Plot");
            uti.Plot = plotNullable ?? 0;
            // AddCost is stored as UInt32, so we need to read it as uint, not int
            uint? addCostNullable = root.GetUInt32("AddCost");
            uti.AddCost = addCostNullable.HasValue ? (int)addCostNullable.Value : 0;
            // PaletteID is stored as UInt8, so we need to read it as byte, not int
            byte? paletteIdNullable = root.GetUInt8("PaletteID");
            uti.PaletteId = paletteIdNullable ?? 0;
            uti.Comment = root.Acquire<string>("Comment", "");
            // ModelVariation, BodyVariation, and TextureVar are stored as UInt8, so we need to read them as byte, not int
            byte? modelVariationNullable = root.GetUInt8("ModelVariation");
            uti.ModelVariation = modelVariationNullable ?? 0;
            byte? bodyVariationNullable = root.GetUInt8("BodyVariation");
            uti.BodyVariation = bodyVariationNullable ?? 0;
            byte? textureVarNullable = root.GetUInt8("TextureVar");
            uti.TextureVariation = textureVarNullable ?? 0;
            // UpgradeLevel is stored as UInt8 in K2, so we need to read it as byte, not int
            byte? upgradeLevelNullable = root.GetUInt8("UpgradeLevel");
            uti.UpgradeLevel = upgradeLevelNullable ?? 0;
            uti.Stolen = root.Acquire<int>("Stolen", 0);
            uti.Identified = root.Acquire<int>("Identified", 0);

            // Extract properties list
            var propertiesList = root.Acquire<GFFList>("PropertiesList", new GFFList());
            uti.Properties.Clear();
            foreach (var propStruct in propertiesList)
            {
                var prop = new UTIProperty();
                // Matching DismantleUti: CostTable is UInt8
                byte? costTableNullable = propStruct.GetUInt8("CostTable");
                prop.CostTable = costTableNullable.HasValue ? (int)costTableNullable.Value : 0;
                // Matching DismantleUti: CostValue is UInt16
                ushort? costValueNullable = propStruct.GetUInt16("CostValue");
                prop.CostValue = costValueNullable.HasValue ? (int)costValueNullable.Value : 0;
                // Matching DismantleUti: Param1 is UInt8
                byte? param1Nullable = propStruct.GetUInt8("Param1");
                prop.Param1 = param1Nullable.HasValue ? (int)param1Nullable.Value : 0;
                // Matching DismantleUti: Param1Value is UInt8
                byte? param1ValueNullable = propStruct.GetUInt8("Param1Value");
                prop.Param1Value = param1ValueNullable.HasValue ? (int)param1ValueNullable.Value : 0;
                // Matching DismantleUti: PropertyName is UInt16
                ushort? propertyNameNullable = propStruct.GetUInt16("PropertyName");
                prop.PropertyName = propertyNameNullable.HasValue ? (int)propertyNameNullable.Value : 0;
                // Matching DismantleUti: Subtype is UInt16
                ushort? subtypeNullable = propStruct.GetUInt16("Subtype");
                prop.Subtype = subtypeNullable.HasValue ? (int)subtypeNullable.Value : 0;
                // Matching DismantleUti: ChanceAppear is UInt8
                byte? chanceAppearNullable = propStruct.GetUInt8("ChanceAppear");
                prop.ChanceAppear = chanceAppearNullable.HasValue ? (int)chanceAppearNullable.Value : 100;
                if (propStruct.Exists("UpgradeType"))
                {
                    // Matching DismantleUti: UpgradeType is UInt8
                    byte? upgradeTypeNullable = propStruct.GetUInt8("UpgradeType");
                    prop.UpgradeType = upgradeTypeNullable.HasValue ? (int)upgradeTypeNullable.Value : 0;
                }
                uti.Properties.Add(prop);
            }

            return uti;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/uti.py:283-400
        // Original: def dismantle_uti(uti: UTI, game: Game = Game.K2, *, use_deprecated: bool = True) -> GFF:
        public static GFF DismantleUti(UTI uti, Game game = Game.K2, bool useDeprecated = true)
        {
            var gff = new GFF(GFFContent.UTI);
            var root = gff.Root;

            // Set basic fields
            root.SetResRef("TemplateResRef", uti.ResRef);
            root.SetInt32("BaseItem", uti.BaseItem);
            root.SetLocString("LocalizedName", uti.Name);
            root.SetLocString("Description", uti.DescriptionUnidentified);
            root.SetLocString("DescIdentified", uti.Description);
            root.SetString("Tag", uti.Tag);
            root.SetUInt8("Charges", (byte)uti.Charges);
            root.SetUInt32("Cost", (uint)uti.Cost);
            root.SetUInt16("StackSize", (ushort)uti.StackSize);
            root.SetUInt8("Plot", (byte)uti.Plot);
            root.SetUInt32("AddCost", (uint)uti.AddCost);
            root.SetUInt8("PaletteID", (byte)uti.PaletteId);
            root.SetString("Comment", uti.Comment);
            root.SetUInt8("ModelVariation", (byte)uti.ModelVariation);
            root.SetUInt8("BodyVariation", (byte)uti.BodyVariation);
            root.SetUInt8("TextureVar", (byte)uti.TextureVariation);

            // KotOR 2 only fields
            if (game.IsK2())
            {
                root.SetUInt8("UpgradeLevel", (byte)uti.UpgradeLevel);
            }

            if (useDeprecated)
            {
                root.SetUInt8("Stolen", (byte)uti.Stolen);
                root.SetUInt8("Identified", (byte)uti.Identified);
            }

            // Set properties list
            var propertiesList = new GFFList();
            root.SetList("PropertiesList", propertiesList);
            if (uti.Properties != null)
            {
                foreach (var prop in uti.Properties)
                {
                    var propStruct = propertiesList.Add(0);
                    propStruct.SetUInt8("CostTable", (byte)prop.CostTable);
                    propStruct.SetUInt16("CostValue", (ushort)prop.CostValue);
                    propStruct.SetUInt8("Param1", (byte)prop.Param1);
                    propStruct.SetUInt8("Param1Value", (byte)prop.Param1Value);
                    propStruct.SetUInt16("PropertyName", (ushort)prop.PropertyName);
                    propStruct.SetUInt16("Subtype", (ushort)prop.Subtype);
                    propStruct.SetUInt8("ChanceAppear", (byte)prop.ChanceAppear);
                    if (prop.UpgradeType.HasValue)
                    {
                        propStruct.SetUInt8("UpgradeType", (byte)prop.UpgradeType.Value);
                    }
                }
            }

            return gff;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/uti.py:371-390
        // Original: def read_uti(source: SOURCE_TYPES, offset: int = 0, size: int | None = None) -> UTI:
        public static UTI ReadUti(byte[] data, int offset = 0, int size = -1)
        {
            byte[] dataToRead = data;
            if (size > 0 && offset + size <= data.Length)
            {
                dataToRead = new byte[size];
                System.Array.Copy(data, offset, dataToRead, 0, size);
            }
            GFF gff = GFF.FromBytes(dataToRead);
            return ConstructUti(gff);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/uti.py:392-407
        // Original: def bytes_uti(uti: UTI, game: Game = Game.K2, file_format: ResourceType = ResourceType.GFF) -> bytes:
        public static byte[] BytesUti(UTI uti, Game game = Game.K2, ResourceType fileFormat = null)
        {
            if (fileFormat == null)
            {
                fileFormat = ResourceType.UTI;
            }
            GFF gff = DismantleUti(uti, game);
            return GFFAuto.BytesGff(gff, fileFormat);
        }
    }
}