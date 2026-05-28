using System.Collections.Generic;
using System.Numerics;
using KOTORModSync;
using KOTORModSync.Resource;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics
{
    /// <summary>
    /// Stores static area data.
    ///
    /// ARE files are GFF-based format files that store static area information including
    /// lighting, fog, grass, weather, script hooks, and map data.
    /// </summary>
    [PublicAPI]
    public sealed class ARE
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:19
        // Original: BINARY_TYPE = ResourceType.ARE
        public static readonly ResourceType BinaryType = ResourceType.ARE;

        // Basic ARE properties
        public string Tag { get; set; } = string.Empty;
        public LocalizedString Name { get; set; } = LocalizedString.FromInvalid();
        public int AlphaTest { get; set; }
        public int CameraStyle { get; set; }
        public ResRef DefaultEnvMap { get; set; } = ResRef.FromBlank();
        public bool DisableTransit { get; set; }
        public bool Unescapable { get; set; }
        public bool StealthXp { get; set; }
        public int StealthXpMax { get; set; }
        public int StealthXpLoss { get; set; }
        public ResRef GrassTexture { get; set; } = ResRef.FromBlank();
        public float GrassDensity { get; set; }
        public float GrassSize { get; set; }
        public float GrassProbLL { get; set; }
        public float GrassProbLR { get; set; }
        public float GrassProbUL { get; set; }
        public float GrassProbUR { get; set; }
        /// <summary>
        /// Grass diffuse color (RGB).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:68
        /// Original: grass_diffuse: "Grass_Diffuse" field.
        /// </remarks>
        public Color GrassDiffuse { get; set; } = new Color(0, 0, 0);
        /// <summary>
        /// Grass ambient color (RGB).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:67
        /// Original: grass_ambient: "Grass_Ambient" field.
        /// </remarks>
        public Color GrassAmbient { get; set; } = new Color(0, 0, 0);
        /// <summary>
        /// Grass emissive color (RGB). KotOR 2 Only.
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:73
        /// Original: grass_emissive: "Grass_Emissive" field. KotOR 2 Only.
        /// </remarks>
        public Color GrassEmissive { get; set; } = new Color(0, 0, 0);
        public bool FogEnabled { get; set; }
        public float FogNear { get; set; }
        public float FogFar { get; set; }
        public Color FogColor { get; set; } = new Color(0, 0, 0);
        public bool SunFogEnabled { get; set; }
        public float SunFogNear { get; set; }
        public float SunFogFar { get; set; }
        public Color SunFogColor { get; set; } = new Color(0, 0, 0);
        public Color SunAmbient { get; set; } = new Color(0, 0, 0);
        public Color SunDiffuse { get; set; } = new Color(0, 0, 0);
        public Color DynamicLight { get; set; } = new Color(0, 0, 0);
        public int WindPower { get; set; }
        public ResRef ShadowOpacity { get; set; } = ResRef.FromBlank();
        public ResRef ChancesOfRain { get; set; } = ResRef.FromBlank();
        public ResRef ChancesOfSnow { get; set; } = ResRef.FromBlank();
        public ResRef ChancesOfLightning { get; set; } = ResRef.FromBlank();
        public ResRef ChancesOfFog { get; set; } = ResRef.FromBlank();
        public int Weather { get; set; }
        public int SkyBox { get; set; }
        public int MoonAmbient { get; set; }
        public int DawnAmbient { get; set; }
        public int DayAmbient { get; set; }
        public int DuskAmbient { get; set; }
        public int NightAmbient { get; set; }
        
        // Aurora (NWN) specific fields
        // Based on nwmain.exe: CNWSArea::LoadProperties @ 0x140361dd0
        // Reads from AreaProperties struct: EnvAudio, SkyBox, MoonFogColor, SunFogColor, MoonFogAmount, SunFogAmount, MoonAmbientColor, MoonDiffuseColor, SunAmbientColor, SunDiffuseColor, DisplayName
        /// <summary>
        /// Environment audio ID (Aurora/NWN only).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea::LoadProperties @ 0x140361dd0
        /// Original: CResGFF::ReadFieldINT(param_1, local_res20, "EnvAudio", ...)
        /// </remarks>
        public int EnvAudio { get; set; }
        
        /// <summary>
        /// Display name (Aurora/NWN only).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea::LoadProperties @ 0x140361dd0
        /// Original: CResGFF::ReadFieldCExoString(param_1, local_60, local_res20, "DisplayName", ...)
        /// </remarks>
        public string DisplayName { get; set; } = string.Empty;
        
        /// <summary>
        /// Moon fog color (Aurora/NWN only).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea::LoadProperties @ 0x140361dd0
        /// Original: CResGFF::ReadFieldDWORD(param_1, local_res20, "MoonFogColor", ...)
        /// </remarks>
        public Color MoonFogColor { get; set; } = new Color(0, 0, 0);
        
        /// <summary>
        /// Moon fog amount (Aurora/NWN only).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea::LoadProperties @ 0x140361dd0
        /// Original: CResGFF::ReadFieldBYTE(param_1, local_res20, "MoonFogAmount", ...)
        /// </remarks>
        public byte MoonFogAmount { get; set; }
        
        /// <summary>
        /// Moon ambient color (Aurora/NWN only).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea::LoadProperties @ 0x140361dd0
        /// Original: CResGFF::ReadFieldDWORD(param_1, local_res20, "MoonAmbientColor", ...)
        /// </remarks>
        public Color MoonAmbientColor { get; set; } = new Color(0, 0, 0);
        
        /// <summary>
        /// Moon diffuse color (Aurora/NWN only).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea::LoadProperties @ 0x140361dd0
        /// Original: CResGFF::ReadFieldDWORD(param_1, local_res20, "MoonDiffuseColor", ...)
        /// </remarks>
        public Color MoonDiffuseColor { get; set; } = new Color(0, 0, 0);
        public int DawnDir1 { get; set; }
        public int DawnDir2 { get; set; }
        public int DawnDir3 { get; set; }
        public int DayDir1 { get; set; }
        public int DayDir2 { get; set; }
        public int DayDir3 { get; set; }
        public int DuskDir1 { get; set; }
        public int DuskDir2 { get; set; }
        public int DuskDir3 { get; set; }
        public int NightDir1 { get; set; }
        public int NightDir2 { get; set; }
        public int NightDir3 { get; set; }
        public Color DawnColor1 { get; set; } = new Color(0, 0, 0);
        public Color DawnColor2 { get; set; } = new Color(0, 0, 0);
        public Color DawnColor3 { get; set; } = new Color(0, 0, 0);
        public Color DayColor1 { get; set; } = new Color(0, 0, 0);
        public Color DayColor2 { get; set; } = new Color(0, 0, 0);
        public Color DayColor3 { get; set; } = new Color(0, 0, 0);
        public Color DuskColor1 { get; set; } = new Color(0, 0, 0);
        public Color DuskColor2 { get; set; } = new Color(0, 0, 0);
        public Color DuskColor3 { get; set; } = new Color(0, 0, 0);
        public Color NightColor1 { get; set; } = new Color(0, 0, 0);
        public Color NightColor2 { get; set; } = new Color(0, 0, 0);
        public Color NightColor3 { get; set; } = new Color(0, 0, 0);
        public ResRef OnEnter { get; set; } = ResRef.FromBlank();
        public ResRef OnExit { get; set; } = ResRef.FromBlank();
        public ResRef OnHeartbeat { get; set; } = ResRef.FromBlank();
        public ResRef OnUserDefined { get; set; } = ResRef.FromBlank();
        public ResRef OnEnter2 { get; set; } = ResRef.FromBlank();
        public ResRef OnExit2 { get; set; } = ResRef.FromBlank();
        public ResRef OnHeartbeat2 { get; set; } = ResRef.FromBlank();
        public ResRef OnUserDefined2 { get; set; } = ResRef.FromBlank();
        public List<string> AreaList { get; set; } = new List<string>();
        public List<ResRef> MapList { get; set; } = new List<ResRef>();

        /// <summary>
        /// Dirty formula 1 (KotOR 2 Only).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:78
        /// Original: dirty_formula_1: "DirtyFormulaOne" field. KotOR 2 Only.
        /// </remarks>
        public int DirtyFormula1 { get; set; }

        /// <summary>
        /// Dirty formula 2 (KotOR 2 Only).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:81
        /// Original: dirty_formula_2: "DirtyFormulaTwo" field. KotOR 2 Only.
        /// </remarks>
        public int DirtyFormula2 { get; set; }

        /// <summary>
        /// Dirty formula 3 (KotOR 2 Only).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:84
        /// Original: dirty_formula_3: "DirtyFormulaThre" field. KotOR 2 Only.
        /// </remarks>
        public int DirtyFormula3 { get; set; }

        /// <summary>
        /// Developer comments/notes (toolset only, not used by game engine).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:140
        /// Original: self.comment: str = ""
        /// </remarks>
        public string Comment { get; set; } = string.Empty;

        /// <summary>
        /// Map north axis orientation.
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:260
        /// Original: self.north_axis: ARENorthAxis = ARENorthAxis.PositiveX
        /// </remarks>
        public ARENorthAxis NorthAxis { get; set; } = ARENorthAxis.PositiveX;

        /// <summary>
        /// Map zoom level.
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:259
        /// Original: self.map_zoom: int = 0
        /// </remarks>
        public int MapZoom { get; set; }

        /// <summary>
        /// Map resolution X.
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:258
        /// Original: self.map_res_x: int = 0
        /// </remarks>
        public int MapResX { get; set; }

        /// <summary>
        /// Map point 1 (image coordinates, normalized 0.0-1.0).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:254
        /// Original: self.map_point_1: Vector2 = Vector2.from_null()
        /// </remarks>
        public Vector2 MapPoint1 { get; set; }

        /// <summary>
        /// Map point 2 (image coordinates, normalized 0.0-1.0).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:255
        /// Original: self.map_point_2: Vector2 = Vector2.from_null()
        /// </remarks>
        public Vector2 MapPoint2 { get; set; }

        /// <summary>
        /// World point 1 (world coordinates).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:256
        /// Original: self.world_point_1: Vector2 = Vector2.from_null()
        /// </remarks>
        public Vector2 WorldPoint1 { get; set; }

        /// <summary>
        /// World point 2 (world coordinates).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:257
        /// Original: self.world_point_2: Vector2 = Vector2.from_null()
        /// </remarks>
        public Vector2 WorldPoint2 { get; set; }

        /// <summary>
        /// Load screen ID (index into loadscreens.2da).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:104,293
        /// Original: loadscreen_id: "LoadScreenID" field. Not used by the game engine.
        /// </remarks>
        public int LoadScreenID { get; set; }

        public ARE()
        {
        }
    }
}
