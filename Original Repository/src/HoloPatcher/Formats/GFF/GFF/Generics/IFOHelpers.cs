using KOTORModSync;
using KOTORModSync.Formats.GFF;
using KOTORModSync.Resource;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/ifo.py
    // Original: construct_ifo and dismantle_ifo functions
    public static class IFOHelpers
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/ifo.py:127-182
        // Original: def construct_ifo(gff: GFF) -> IFO:
        public static IFO ConstructIfo(GFF gff)
        {
            var ifo = new IFO();
            var root = gff.Root;

            // Extract basic fields (matching Python field names)
            ifo.ModId = root.Acquire<byte[]>("Mod_ID", new byte[16]);
            ifo.ModName = root.Acquire<LocalizedString>("Mod_Name", LocalizedString.FromInvalid());
            ifo.Name = ifo.ModName; // Alias
            ifo.Tag = root.Acquire<string>("Mod_Tag", "");
            ifo.VoId = root.Acquire<string>("Mod_VO_ID", "");
            ifo.ResRef = root.Acquire<ResRef>("Mod_Entry_Area", ResRef.FromBlank());
            ifo.EntryArea = ifo.ResRef; // Alias
            ifo.EntryX = root.Acquire<float>("Mod_Entry_X", 0.0f);
            ifo.EntryY = root.Acquire<float>("Mod_Entry_Y", 0.0f);
            ifo.EntryZ = root.Acquire<float>("Mod_Entry_Z", 0.0f);
            float dirX = root.Acquire<float>("Mod_Entry_Dir_X", 0.0f);
            float dirY = root.Acquire<float>("Mod_Entry_Dir_Y", 0.0f);
            // Store direction components (Python calculates angle, but we store X/Y/Z separately)
            ifo.EntryDirectionX = dirX;
            ifo.EntryDirectionY = dirY;
            ifo.EntryDirectionZ = 0.0f;
            // Calculate entry direction angle from X/Y components
            ifo.EntryDirection = (float)System.Math.Atan2(dirY, dirX);

            // Extract script hooks (using Python field names)
            ifo.OnClientEnter = root.Acquire<ResRef>("Mod_OnClientEntr", ResRef.FromBlank());
            ifo.OnClientLeave = root.Acquire<ResRef>("Mod_OnClientLeav", ResRef.FromBlank());
            ifo.OnHeartbeat = root.Acquire<ResRef>("Mod_OnHeartbeat", ResRef.FromBlank());
            ifo.OnUserDefined = root.Acquire<ResRef>("Mod_OnUsrDefined", ResRef.FromBlank());
            ifo.OnActivateItem = root.Acquire<ResRef>("Mod_OnActvtItem", ResRef.FromBlank());
            ifo.OnAcquireItem = root.Acquire<ResRef>("Mod_OnAcquirItem", ResRef.FromBlank());
            ifo.OnUnacquireItem = root.Acquire<ResRef>("Mod_OnUnAqreItem", ResRef.FromBlank());
            ifo.OnPlayerDeath = root.Acquire<ResRef>("Mod_OnPlrDeath", ResRef.FromBlank());
            ifo.OnPlayerDying = root.Acquire<ResRef>("Mod_OnPlrDying", ResRef.FromBlank());
            ifo.OnPlayerRespawn = root.Acquire<ResRef>("Mod_OnSpawnBtnDn", ResRef.FromBlank());
            ifo.OnPlayerRest = root.Acquire<ResRef>("Mod_OnPlrRest", ResRef.FromBlank());
            ifo.OnPlayerLevelUp = root.Acquire<ResRef>("Mod_OnPlrLvlUp", ResRef.FromBlank());
            ifo.OnPlayerCancelCutscene = root.Acquire<ResRef>("Mod_OnPlrCancelCutscene", ResRef.FromBlank());
            ifo.OnLoad = root.Acquire<ResRef>("Mod_OnModLoad", ResRef.FromBlank());
            ifo.OnStart = root.Acquire<ResRef>("Mod_OnModStart", ResRef.FromBlank());
            ifo.StartMovie = root.Acquire<ResRef>("Mod_StartMovie", ResRef.FromBlank());

            // Extract area list
            var areaList = root.Acquire<GFFList>("Mod_Area_list", new GFFList());
            if (areaList != null && areaList.Count > 0)
            {
                var firstArea = areaList.At(0);
                if (firstArea != null)
                {
                    var areaName = firstArea.Acquire<ResRef>("Area_Name", ResRef.FromBlank());
                    // Store first area name (Python stores in ifo.area_name)
                    // ifo.AreaName would need to be added to IFO class
                }
            }
            foreach (var areaStruct in areaList)
            {
                var areaName = areaStruct.Acquire<ResRef>("Area_Name", ResRef.FromBlank());
                ifo.AreaList.Add(areaName);
            }

            // Extract other fields
            ifo.ExpansionPack = root.Acquire<int>("Expansion_Pack", 0);
            ifo.Description = root.Acquire<LocalizedString>("Mod_Description", LocalizedString.FromInvalid());
            ifo.ModVersion = root.Acquire<int>("Mod_Version", 0);
            ifo.Hak = root.Acquire<string>("Mod_Hak", "");
            ifo.DawnHour = root.Acquire<int>("Mod_DawnHour", 0);
            ifo.DuskHour = root.Acquire<int>("Mod_DuskHour", 0);
            ifo.TimeScale = root.Acquire<int>("Mod_MinPerHour", 0);
            ifo.StartMonth = root.Acquire<int>("Mod_StartMonth", 0);
            ifo.StartDay = root.Acquire<int>("Mod_StartDay", 0);
            ifo.StartHour = root.Acquire<int>("Mod_StartHour", 0);
            ifo.StartYear = root.Acquire<int>("Mod_StartYear", 0);
            ifo.XpScale = root.Acquire<int>("Mod_XPScale", 0);
            // VaultId may not exist in Python version

            return ifo;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/ifo.py:183-277
        // Original: def dismantle_ifo(ifo: IFO, game: Game = Game.K2) -> GFF:
        public static GFF DismantleIfo(IFO ifo, Game game = Game.K2)
        {
            var gff = new GFF(GFFContent.IFO);
            var root = gff.Root;

            // Set basic fields (matching Python field names)
            root.SetBinary("Mod_ID", ifo.ModId);
            root.SetLocString("Mod_Name", ifo.ModName);
            root.SetString("Mod_Tag", ifo.Tag);
            root.SetString("Mod_VO_ID", ifo.VoId);
            root.SetUInt8("Mod_IsSaveGame", 0);
            root.SetResRef("Mod_Entry_Area", ifo.ResRef);
            root.SetSingle("Mod_Entry_X", ifo.EntryX);
            root.SetSingle("Mod_Entry_Y", ifo.EntryY);
            root.SetSingle("Mod_Entry_Z", ifo.EntryZ);
            // Calculate entry direction vector from angle or use stored components
            if (ifo.EntryDirectionX != 0 || ifo.EntryDirectionY != 0)
            {
                root.SetSingle("Mod_Entry_Dir_X", ifo.EntryDirectionX);
                root.SetSingle("Mod_Entry_Dir_Y", ifo.EntryDirectionY);
            }
            else
            {
                // Calculate from angle
                root.SetSingle("Mod_Entry_Dir_X", (float)System.Math.Cos(ifo.EntryDirection));
                root.SetSingle("Mod_Entry_Dir_Y", (float)System.Math.Sin(ifo.EntryDirection));
            }

            // Set script hooks (using Python field names)
            root.SetResRef("Mod_OnHeartbeat", ifo.OnHeartbeat);
            root.SetResRef("Mod_OnClientEntr", ifo.OnClientEnter);
            root.SetResRef("Mod_OnClientLeav", ifo.OnClientLeave);
            root.SetResRef("Mod_OnActvtItem", ifo.OnActivateItem);
            root.SetResRef("Mod_OnAcquirItem", ifo.OnAcquireItem);
            root.SetResRef("Mod_OnUsrDefined", ifo.OnUserDefined);
            root.SetResRef("Mod_OnUnAqreItem", ifo.OnUnacquireItem);
            root.SetResRef("Mod_OnPlrDeath", ifo.OnPlayerDeath);
            root.SetResRef("Mod_OnPlrDying", ifo.OnPlayerDying);
            root.SetResRef("Mod_OnPlrLvlUp", ifo.OnPlayerLevelUp);
            root.SetResRef("Mod_OnSpawnBtnDn", ifo.OnPlayerRespawn);
            root.SetResRef("Mod_OnPlrRest", ifo.OnPlayerRest);
            root.SetResRef("Mod_OnModLoad", ifo.OnLoad);
            root.SetResRef("Mod_OnModStart", ifo.OnStart);
            root.SetResRef("Mod_StartMovie", ifo.StartMovie);

            // Set other fields
            root.SetString("Mod_Hak", ifo.Hak);
            root.SetInt32("Mod_DawnHour", ifo.DawnHour);
            root.SetInt32("Mod_DuskHour", ifo.DuskHour);
            root.SetInt32("Mod_MinPerHour", ifo.TimeScale);
            root.SetInt32("Mod_StartMonth", ifo.StartMonth);
            root.SetInt32("Mod_StartDay", ifo.StartDay);
            root.SetInt32("Mod_StartHour", ifo.StartHour);
            root.SetInt32("Mod_StartYear", ifo.StartYear);
            root.SetInt32("Mod_XPScale", ifo.XpScale);

            // Set area list
            var areaList = new GFFList();
            root.SetList("Mod_Area_list", areaList);
            if (ifo.AreaList != null && ifo.AreaList.Count > 0)
            {
                var areaStruct = areaList.Add(6);
                areaStruct.SetResRef("Area_Name", ifo.AreaList[0]);
            }

            // Set other fields
            root.SetUInt16("Expansion_Pack", (ushort)ifo.ExpansionPack);
            root.SetLocString("Mod_Description", ifo.Description);
            root.SetUInt32("Mod_Version", (uint)ifo.ModVersion);

            return gff;
        }
    }
}