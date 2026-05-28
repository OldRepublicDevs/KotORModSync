using KOTORModSync;
using KOTORModSync.Formats.GFF;
using KOTORModSync.Resource;
using GFFAuto = KOTORModSync.Formats.GFF.GFFAuto;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py
    // Original: construct_are and dismantle_are functions
    public static class AREHelpers
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:394-535
        // Original: def construct_are(gff: GFF) -> ARE:
        // Note: This function loads ALL fields from GFF regardless of game type.
        // Game-specific field handling is done in DismantleAre when writing.
        // All engines (Odyssey/KOTOR, Aurora/NWN, Eclipse/DA/ME) use the same ARE file format structure.
        // K2-specific fields (Grass_Emissive, DirtyARGB, ChanceRain/Snow/Lightning, etc.) are optional
        // and will be 0/default if not present (which is correct for K1, Aurora, and Eclipse).
        public static ARE ConstructAre(GFF gff)
        {
            var are = new ARE();

            var root = gff.Root;
            var mapStruct = root.Acquire<GFFStruct>("Map", new GFFStruct());
            // map_original_struct_id would need to be stored in ARE class
            // are.map_original_struct_id = mapStruct.StructId;

            // Matching Python: are.north_axis = ARENorthAxis(map_struct.acquire("NorthAxis", 0))
            are.NorthAxis = (ARENorthAxis)mapStruct.Acquire<int>("NorthAxis", 0);
            // Matching Python: are.map_zoom = map_struct.acquire("MapZoom", 0)
            are.MapZoom = mapStruct.Acquire<int>("MapZoom", 0);
            // Matching Python: are.map_res_x = map_struct.acquire("MapResX", 0)
            are.MapResX = mapStruct.Acquire<int>("MapResX", 0);
            // Matching Python: are.map_point_1 = Vector2(map_struct.acquire("MapPt1X", 0.0), map_struct.acquire("MapPt1Y", 0.0))
            are.MapPoint1 = new System.Numerics.Vector2(
                mapStruct.Acquire<float>("MapPt1X", 0.0f),
                mapStruct.Acquire<float>("MapPt1Y", 0.0f));
            // Matching Python: are.map_point_2 = Vector2(map_struct.acquire("MapPt2X", 0.0), map_struct.acquire("MapPt2Y", 0.0))
            are.MapPoint2 = new System.Numerics.Vector2(
                mapStruct.Acquire<float>("MapPt2X", 0.0f),
                mapStruct.Acquire<float>("MapPt2Y", 0.0f));
            // Matching Python: are.world_point_1 = Vector2(map_struct.acquire("WorldPt1X", 0.0), map_struct.acquire("WorldPt1Y", 0.0))
            are.WorldPoint1 = new System.Numerics.Vector2(
                mapStruct.Acquire<float>("WorldPt1X", 0.0f),
                mapStruct.Acquire<float>("WorldPt1Y", 0.0f));
            // Matching Python: are.world_point_2 = Vector2(map_struct.acquire("WorldPt2X", 0.0), map_struct.acquire("WorldPt2Y", 0.0))
            are.WorldPoint2 = new System.Numerics.Vector2(
                mapStruct.Acquire<float>("WorldPt2X", 0.0f),
                mapStruct.Acquire<float>("WorldPt2Y", 0.0f));
            are.MapList = new System.Collections.Generic.List<ResRef>(); // Placeholder

            // Extract basic fields
            are.Tag = root.Acquire<string>("Tag", "");
            are.Name = root.Acquire<LocalizedString>("Name", LocalizedString.FromInvalid());
            are.AlphaTest = root.Acquire<int>("AlphaTest", 0);
            are.CameraStyle = root.Acquire<int>("CameraStyle", 0);
            are.DefaultEnvMap = root.Acquire<ResRef>("DefaultEnvMap", ResRef.FromBlank());
            // Matching Python: are.unescapable = bool(root.acquire("Unescapable", 0))
            are.Unescapable = root.GetUInt8("Unescapable") == 1;
            // Matching Python: are.disable_transit = bool(root.acquire("DisableTransit", 0))
            are.DisableTransit = root.GetUInt8("DisableTransit") == 1;
            are.GrassTexture = root.Acquire<ResRef>("Grass_TexName", ResRef.FromBlank());
            are.GrassDensity = root.Acquire<float>("Grass_Density", 0.0f);
            are.GrassSize = root.Acquire<float>("Grass_QuadSize", 0.0f);
            are.GrassProbLL = root.Acquire<float>("Grass_Prob_LL", 0.0f);
            are.GrassProbLR = root.Acquire<float>("Grass_Prob_LR", 0.0f);
            are.GrassProbUL = root.Acquire<float>("Grass_Prob_UL", 0.0f);
            are.GrassProbUR = root.Acquire<float>("Grass_Prob_UR", 0.0f);
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:506-509
            // Original: are.grass_ambient = Color.from_rgb_integer(root.acquire("Grass_Ambient", 0))
            are.GrassAmbient = Color.FromRgbInteger(root.Acquire<int>("Grass_Ambient", 0));
            // Original: are.grass_diffuse = Color.from_rgb_integer(root.acquire("Grass_Diffuse", 0))
            are.GrassDiffuse = Color.FromRgbInteger(root.Acquire<int>("Grass_Diffuse", 0));
            // Original: are.grass_emissive = Color.from_rgb_integer(root.acquire("Grass_Emissive", 0))
            are.GrassEmissive = Color.FromRgbInteger(root.Acquire<int>("Grass_Emissive", 0));
            are.FogEnabled = root.Acquire<int>("SunFogOn", 0) != 0;
            are.FogNear = root.Acquire<float>("SunFogNear", 0.0f);
            are.FogFar = root.Acquire<float>("SunFogFar", 0.0f);
            are.WindPower = root.Acquire<int>("WindPower", 0);
            are.ShadowOpacity = root.Acquire<ResRef>("ShadowOpacity", ResRef.FromBlank());
            are.OnEnter = root.Acquire<ResRef>("OnEnter", ResRef.FromBlank());
            are.OnExit = root.Acquire<ResRef>("OnExit", ResRef.FromBlank());
            are.OnHeartbeat = root.Acquire<ResRef>("OnHeartbeat", ResRef.FromBlank());
            are.OnUserDefined = root.Acquire<ResRef>("OnUserDefined", ResRef.FromBlank());
            // Matching Python: are.stealth_xp = bool(root.acquire("StealthXPEnabled", 0))
            are.StealthXp = root.GetUInt8("StealthXPEnabled") == 1;
            // Matching Python: are.stealth_xp_loss = root.acquire("StealthXPLoss", 0)
            are.StealthXpLoss = root.Acquire<int>("StealthXPLoss", 0);
            // Matching Python: are.stealth_xp_max = root.acquire("StealthXPMax", 0)
            are.StealthXpMax = root.Acquire<int>("StealthXPMax", 0);
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:496
            // Original: are.loadscreen_id = root.acquire("LoadScreenID", 0)
            are.LoadScreenID = root.Acquire<int>("LoadScreenID", 0);

            // Extract color fields (as RGB integers)
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:502-505
            // Original: are.sun_ambient = Color.from_rgb_integer(root.acquire("SunAmbientColor", 0))
            are.SunAmbient = Color.FromRgbInteger(root.Acquire<int>("SunAmbientColor", 0));
            // Original: are.sun_diffuse = Color.from_rgb_integer(root.acquire("SunDiffuseColor", 0))
            are.SunDiffuse = Color.FromRgbInteger(root.Acquire<int>("SunDiffuseColor", 0));
            // Original: are.dynamic_light = Color.from_rgb_integer(root.acquire("DynAmbientColor", 0))
            are.DynamicLight = Color.FromRgbInteger(root.Acquire<int>("DynAmbientColor", 0));
            // Original: are.fog_color = Color.from_rgb_integer(root.acquire("SunFogColor", 0))
            are.FogColor = Color.FromRgbInteger(root.Acquire<int>("SunFogColor", 0));
            
            // Extract K2-specific dirty formula fields (KotOR 2 Only)
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:183,191,199
            // Original: are.dirty_formula_1 = root.acquire("DirtyFormulaOne", 0)
            are.DirtyFormula1 = root.Acquire<int>("DirtyFormulaOne", 0);
            // Original: are.dirty_formula_2 = root.acquire("DirtyFormulaTwo", 0)
            are.DirtyFormula2 = root.Acquire<int>("DirtyFormulaTwo", 0);
            // Original: are.dirty_formula_3 = root.acquire("DirtyFormulaThre", 0)
            are.DirtyFormula3 = root.Acquire<int>("DirtyFormulaThre", 0);
            
            // Extract Comments field (toolset-only, not used by game engine)
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:356
            // Original: are.comment = root.acquire("Comments", "")
            are.Comment = root.Acquire<string>("Comments", "");

            // Extract rooms list
            var roomsList = root.Acquire<GFFList>("Rooms", new GFFList());
            // are.Rooms would need to be a List<ARERoom> in ARE class
            // foreach (GFFStruct roomStruct in roomsList) { ... }

            return are;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:538-682
        // Original: def dismantle_are(are: ARE, game: Game = Game.K2, *, use_deprecated: bool = True) -> GFF:
        public static GFF DismantleAre(ARE are, Game game = Game.K2, bool useDeprecated = true)
        {
            var gff = new GFF(GFFContent.ARE);
            var root = gff.Root;

            // Create Map struct
            var mapStruct = new GFFStruct();
            root.SetStruct("Map", mapStruct);
            // Matching Python: map_struct.set_int32("NorthAxis", are.north_axis.value)
            mapStruct.SetInt32("NorthAxis", (int)are.NorthAxis);
            // Matching Python: map_struct.set_int32("MapZoom", are.map_zoom)
            mapStruct.SetInt32("MapZoom", are.MapZoom);
            // Matching Python: map_struct.set_int32("MapResX", are.map_res_x)
            mapStruct.SetInt32("MapResX", are.MapResX);
            // Matching Python: map_struct.set_single("MapPt1X", map_pt1.x) and map_struct.set_single("MapPt1Y", map_pt1.y)
            mapStruct.SetSingle("MapPt1X", are.MapPoint1.X);
            mapStruct.SetSingle("MapPt1Y", are.MapPoint1.Y);
            // Matching Python: map_struct.set_single("MapPt2X", map_pt2.x) and map_struct.set_single("MapPt2Y", map_pt2.y)
            mapStruct.SetSingle("MapPt2X", are.MapPoint2.X);
            mapStruct.SetSingle("MapPt2Y", are.MapPoint2.Y);
            // Matching Python: map_struct.set_single("WorldPt1X", are.world_point_1.x) and map_struct.set_single("WorldPt1Y", are.world_point_1.y)
            mapStruct.SetSingle("WorldPt1X", are.WorldPoint1.X);
            mapStruct.SetSingle("WorldPt1Y", are.WorldPoint1.Y);
            // Matching Python: map_struct.set_single("WorldPt2X", are.world_point_2.x) and map_struct.set_single("WorldPt2Y", are.world_point_2.y)
            mapStruct.SetSingle("WorldPt2X", are.WorldPoint2.X);
            mapStruct.SetSingle("WorldPt2Y", are.WorldPoint2.Y);

            // Set basic fields - written for ALL game types (Odyssey, Aurora, Eclipse)
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:596-601
            root.SetString("Tag", are.Tag);
            root.SetLocString("Name", are.Name);
            root.SetString("Comments", are.Comment);
            root.SetSingle("AlphaTest", are.AlphaTest);
            root.SetInt32("CameraStyle", are.CameraStyle);
            root.SetResRef("DefaultEnvMap", are.DefaultEnvMap);
            // Matching Python: root.set_uint8("Unescapable", are.unescapable)
            root.SetUInt8("Unescapable", are.Unescapable ? (byte)1 : (byte)0);
            // Matching Python: root.set_uint8("DisableTransit", are.disable_transit)
            root.SetUInt8("DisableTransit", are.DisableTransit ? (byte)1 : (byte)0);
            // Matching Python: root.set_uint8("StealthXPEnabled", are.stealth_xp)
            root.SetUInt8("StealthXPEnabled", are.StealthXp ? (byte)1 : (byte)0);
            // Matching Python: root.set_uint32("StealthXPLoss", are.stealth_xp_loss)
            root.SetUInt32("StealthXPLoss", (uint)are.StealthXpLoss);
            // Matching Python: root.set_uint32("StealthXPMax", are.stealth_xp_max)
            root.SetUInt32("StealthXPMax", (uint)are.StealthXpMax);
            root.SetResRef("Grass_TexName", are.GrassTexture);
            root.SetSingle("Grass_Density", are.GrassDensity);
            root.SetSingle("Grass_QuadSize", are.GrassSize);
            root.SetSingle("Grass_Prob_LL", are.GrassProbLL);
            root.SetSingle("Grass_Prob_LR", are.GrassProbLR);
            root.SetSingle("Grass_Prob_UL", are.GrassProbUL);
            root.SetSingle("Grass_Prob_UR", are.GrassProbUR);
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:593-594
            // Original: root.set_uint32("Grass_Ambient", are.grass_ambient.rgb_integer())
            root.SetUInt32("Grass_Ambient", (uint)are.GrassAmbient.ToRgbInteger());
            // Original: root.set_uint32("Grass_Diffuse", are.grass_diffuse.rgb_integer())
            root.SetUInt32("Grass_Diffuse", (uint)are.GrassDiffuse.ToRgbInteger());
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:639
            // Original: root.set_uint32("Grass_Emissive", are.grass_emissive.rgb_integer()) (KotOR 2 only)
            // K2-specific fields should only be written for K2 games (K2, K2_XBOX, K2_IOS, K2_ANDROID)
            // Aurora (NWN) and Eclipse engines use ARE files but don't have K2-specific fields
            // Using IsK2() extension method to properly detect all K2 variants
            if (game.IsK2())
            {
                root.SetUInt32("Grass_Emissive", (uint)are.GrassEmissive.ToRgbInteger());
                // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:635-652
                // Original: K2-specific fields (DirtyARGB, ChanceRain/Snow/Lightning, DirtySize/Formula/Func)
                // Note: These fields are only in K2, not in K1, Aurora, or Eclipse
                // TODO: Add remaining K2-specific properties to ARE class (DirtyArgb1-3, ChanceRain/Snow/Lightning, DirtySize/Func1-3)
                // When ARE class has these properties, uncomment:
                // root.SetInt32("DirtyARGBOne", (int)are.DirtyArgb1.ToRgbInteger());
                // root.SetInt32("DirtyARGBTwo", (int)are.DirtyArgb2.ToRgbInteger());
                // root.SetInt32("DirtyARGBThree", (int)are.DirtyArgb3.ToRgbInteger());
                // root.SetInt32("ChanceRain", are.ChanceRain);
                // root.SetInt32("ChanceSnow", are.ChanceSnow);
                // root.SetInt32("ChanceLightning", are.ChanceLightning);
                // root.SetInt32("DirtySizeOne", are.DirtySize1);
                // root.SetInt32("DirtyFuncOne", are.DirtyFunc1);
                // root.SetInt32("DirtySizeTwo", are.DirtySize2);
                // root.SetInt32("DirtyFuncTwo", are.DirtyFunc2);
                // root.SetInt32("DirtySizeThree", are.DirtySize3);
                // root.SetInt32("DirtyFuncThree", are.DirtyFunc3);
                // Dirty formula fields are now implemented:
                root.SetInt32("DirtyFormulaOne", are.DirtyFormula1);
                root.SetInt32("DirtyFormulaTwo", are.DirtyFormula2);
                root.SetInt32("DirtyFormulaThre", are.DirtyFormula3);
            }
            // Set fog and lighting fields - written for ALL game types
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:609-614
            root.SetUInt8("SunFogOn", are.FogEnabled ? (byte)1 : (byte)0);
            root.SetSingle("SunFogNear", are.FogNear);
            root.SetSingle("SunFogFar", are.FogFar);
            root.SetInt32("WindPower", are.WindPower);
            // Note: ShadowOpacity in Python is int (shadow_opacity), but C# ARE class uses ResRef
            // This may need to be fixed if ShadowOpacity should be int instead of ResRef
            // For now, writing as ResRef to match current ARE class definition
            root.SetResRef("ShadowOpacity", are.ShadowOpacity);
            
            // Set script hooks - written for ALL game types
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:620-623
            root.SetResRef("OnEnter", are.OnEnter);
            root.SetResRef("OnExit", are.OnExit);
            root.SetResRef("OnHeartbeat", are.OnHeartbeat);
            root.SetResRef("OnUserDefined", are.OnUserDefined);
            
            // Set rooms list - written for ALL game types, but with K2-specific fields conditionally
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:625-633
            var roomsList = new GFFList();
            root.SetList("Rooms", roomsList);
            // TODO: When ARE class has Rooms property (List<ARERoom>), implement room serialization:
            // foreach (var room in are.Rooms)
            // {
            //     var roomStruct = roomsList.Add(0);
            //     roomStruct.SetSingle("AmbientScale", room.AmbientScale);
            //     roomStruct.SetInt32("EnvAudio", room.EnvAudio);
            //     roomStruct.SetString("RoomName", room.Name);
            //     if (game.IsK2())
            //     {
            //         roomStruct.SetUInt8("DisableWeather", room.Weather ? (byte)1 : (byte)0);
            //         roomStruct.SetInt32("ForceRating", room.ForceRating);
            //     }
            // }
            
            // Set load screen ID - written for ALL game types
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:673
            // Original: root.set_uint16("LoadScreenID", are.loadscreen_id)
            root.SetUInt16("LoadScreenID", (ushort)are.LoadScreenID);

            // Set color fields (as RGB integers) - written for ALL game types
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:589-592
            // Original: root.set_uint32("SunAmbientColor", are.sun_ambient.rgb_integer())
            root.SetUInt32("SunAmbientColor", (uint)are.SunAmbient.ToRgbInteger());
            // Original: root.set_uint32("SunDiffuseColor", are.sun_diffuse.rgb_integer())
            root.SetUInt32("SunDiffuseColor", (uint)are.SunDiffuse.ToRgbInteger());
            // Original: root.set_uint32("DynAmbientColor", are.dynamic_light.rgb_integer())
            root.SetUInt32("DynAmbientColor", (uint)are.DynamicLight.ToRgbInteger());
            // Original: root.set_uint32("SunFogColor", are.fog_color.rgb_integer())
            root.SetUInt32("SunFogColor", (uint)are.FogColor.ToRgbInteger());

            // Set deprecated fields - only written when useDeprecated is true
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:654-680
            // Original: if use_deprecated:
            // Note: These fields are toolset-only and not used by game engines
            // They are preserved for compatibility with existing ARE files
            if (useDeprecated)
            {
                // TODO: When ARE class has these deprecated properties, implement:
                // root.SetInt32("ID", are.UnusedId);
                // root.SetInt32("Creator_ID", are.CreatorId);
                // root.SetUInt32("Flags", are.Flags);
                // root.SetInt32("ModSpotCheck", are.ModSpotCheck);
                // root.SetInt32("ModListenCheck", are.ModListenCheck);
                // root.SetUInt32("MoonAmbientColor", (uint)are.MoonAmbient.ToRgbInteger());
                // root.SetUInt32("MoonDiffuseColor", (uint)are.MoonDiffuse.ToRgbInteger());
                // root.SetUInt8("MoonFogOn", are.MoonFog ? (byte)1 : (byte)0);
                // root.SetSingle("MoonFogNear", are.MoonFogNear);
                // root.SetSingle("MoonFogFar", are.MoonFogFar);
                // root.SetUInt32("MoonFogColor", (uint)are.MoonFogColor.ToRgbInteger());
                // root.SetUInt8("MoonShadows", are.MoonShadows ? (byte)1 : (byte)0);
                // root.SetUInt8("IsNight", are.IsNight ? (byte)1 : (byte)0);
                // root.SetUInt8("LightingScheme", (byte)are.LightingScheme);
                // root.SetUInt8("DayNightCycle", are.DayNightCycle ? (byte)1 : (byte)0);
                // root.SetUInt8("NoRest", are.NoRest ? (byte)1 : (byte)0);
                // root.SetUInt8("NoHangBack", are.NoHangBack ? (byte)1 : (byte)0);
                // root.SetUInt8("PlayerOnly", are.PlayerOnly ? (byte)1 : (byte)0);
                // root.SetUInt8("PlayerVsPlayer", are.PlayerVsPlayer ? (byte)1 : (byte)0);
                // var expansionList = new GFFList();
                // root.SetList("Expansion_List", expansionList);
            }

            return gff;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:685-700
        // Original: def read_are(source: SOURCE_TYPES, offset: int = 0, size: int | None = None) -> ARE:
        public static ARE ReadAre(byte[] data, int offset = 0, int size = -1)
        {
            byte[] dataToRead = data;
            if (size > 0 && offset + size <= data.Length)
            {
                dataToRead = new byte[size];
                System.Array.Copy(data, offset, dataToRead, 0, size);
            }
            GFF gff = GFF.FromBytes(dataToRead);
            return ConstructAre(gff);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:742-757
        // Original: def bytes_are(are: ARE, game: Game = Game.K2, file_format: ResourceType = ResourceType.GFF) -> bytes:
        public static byte[] BytesAre(ARE are, Game game = Game.K2, ResourceType fileFormat = null)
        {
            if (fileFormat == null)
            {
                fileFormat = ResourceType.ARE;
            }
            GFF gff = DismantleAre(are, game);
            return GFFAuto.BytesGff(gff, fileFormat);
        }
    }
}
