using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace KOTORModSync.Resource
{

    /// <summary>
    /// Represents a resource type that is used within either games.
    /// </summary>
    public class ResourceType
    {
        private static readonly Dictionary<int, ResourceType> _byTypeId = new Dictionary<int, ResourceType>();
        private static readonly Dictionary<string, ResourceType> _byExtension = new Dictionary<string, ResourceType>();

        public int TypeId { get; }
        public string Extension { get; }
        public string Category { get; }
        public string Contents { get; }
        public bool IsInvalid { get; }
        [CanBeNull]
        public string TargetMember { get; }
        public string Name { get; private set; } = string.Empty;

        private ResourceType(
            int typeId,
            string extension,
            string category,
            string contents,
            bool isInvalid = false,
            [CanBeNull] string targetMember = null,
            [CanBeNull] string name = null)
        {
            TypeId = typeId;
            Extension = extension.Trim().ToLower();
            Category = category;
            Contents = contents;
            IsInvalid = isInvalid;
            TargetMember = targetMember;
            Name = name ?? string.Empty;

            if (!isInvalid)
            {
                _byTypeId[typeId] = this;
                _byExtension[Extension] = this;
            }
        }

        /// <summary>
        /// Returns True if this resourcetype is a gff, excluding the xml/json abstractions, False otherwise.
        /// </summary>
        public bool IsGff() => Contents == "gff";

        /// <summary>
        /// Returns the target type for this resource type.
        /// </summary>
        public ResourceType TargetType()
        {
            if (TargetMember is null)
            {
                return this;
            }

            // Can be null if not found
            System.Reflection.FieldInfo field = typeof(ResourceType).GetField(
                TargetMember,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
            );
            if (!(field is null) && field.FieldType == typeof(ResourceType))
            {
                return (ResourceType)field.GetValue(null);
            }

            return this;
        }

        /// <summary>
        /// Validates the resource type and throws if invalid.
        /// </summary>
        public ResourceType Validate()
        {
            if (IsInvalid)
            {
                throw new ArgumentException($"Invalid ResourceType: '{this}'");
            }
            return this;
        }

        /// <summary>
        /// Returns whether this resource type is valid.
        /// </summary>
        public bool IsValid() => !IsInvalid;

        public override string ToString() => Extension.ToUpper();

        public override int GetHashCode() => Extension.GetHashCode(StringComparison.OrdinalIgnoreCase);

        public override bool Equals([CanBeNull] object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj is ResourceType other)
            {
                if (IsInvalid || other.IsInvalid)
                {
                    return IsInvalid && other.IsInvalid;
                }
                return Name == other.Name;
            }

            if (obj is string str)
            {
                return Extension.Equals(str, StringComparison.OrdinalIgnoreCase);
            }

            if (obj is int intValue)
            {
                return TypeId == intValue;
            }

            return false;
        }

        public static bool operator ==([CanBeNull] ResourceType left, [CanBeNull] ResourceType right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left is null || right is null)
            {
                return false;
            }

            return left.Equals(right);
        }

        public static bool operator !=([CanBeNull] ResourceType left, [CanBeNull] ResourceType right) => !(left == right);

        public static implicit operator int(ResourceType type) => type.TypeId;

        public static implicit operator bool(ResourceType type) => !type.IsInvalid;

        /// <summary>
        /// Returns the ResourceType for the specified id.
        /// </summary>
        public static ResourceType FromId(int typeId)
        {
            // Search through all static fields to match Python's behavior
            // Can be null if not found
            ResourceType fields = typeof(ResourceType).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(f => f.FieldType == typeof(ResourceType))
                .Select(f => (ResourceType)f.GetValue(null))
                .Where(rt => rt.TypeId == typeId)
                .FirstOrDefault();

            return fields ?? FromInvalid(typeId: typeId);
        }

        /// <summary>
        /// Returns the ResourceType for the specified id (accepts string or int).
        /// </summary>
        public static ResourceType FromId(string typeId)
        {
            if (int.TryParse(typeId, out int id))
            {
                return FromId(id);
            }

            return FromInvalid();
        }

        /// <summary>
        /// Alias for FromId to match Python API.
        /// </summary>
        public static ResourceType FromTypeId(int typeId) => FromId(typeId);

        /// <summary>
        /// Returns the ResourceType for the specified extension.
        /// This will slice off the leading dot in the extension, if it exists.
        /// </summary>
        public static ResourceType FromExtension(string extension)
        {
            string ext = extension.TrimStart('.').ToLower();

            // Search through all static fields to match Python's behavior
            // Can be null if not found
            ResourceType fields = typeof(ResourceType).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(f => f.FieldType == typeof(ResourceType))
                .Select(f => (ResourceType)f.GetValue(null))
                .Where(rt => rt.Extension == ext)
                .FirstOrDefault();

            return fields ?? FromInvalid(extension: ext);
        }

        /// <summary>
        /// Creates an invalid ResourceType instance.
        /// </summary>
        public static ResourceType FromInvalid(
            [CanBeNull] int? typeId = null,
            [CanBeNull] string extension = null,
            [CanBeNull] string category = null,
            [CanBeNull] string contents = null,
            bool? isInvalid = null,
            [CanBeNull] string targetMember = null)
        {
            if (typeId is null && extension is null && category is null && contents is null && isInvalid is null && targetMember is null)
            {
                return INVALID;
            }

            return new ResourceType(
                typeId ?? INVALID.TypeId,
                extension ?? INVALID.Extension,
                category ?? INVALID.Category,
                contents ?? INVALID.Contents,
                isInvalid ?? true,
                targetMember ?? INVALID.TargetMember,
                $"INVALID_{extension ?? typeId?.ToString() ?? Guid.NewGuid().ToString("N")}"
            );
        }

        // Resource types used in KotOR games
        public static readonly ResourceType INVALID = new ResourceType(-1, "", "Undefined", "binary", isInvalid: true, name: "INVALID");
        public static readonly ResourceType RES = new ResourceType(0, "res", "Save Data", "gff", name: "RES");
        public static readonly ResourceType BMP = new ResourceType(1, "bmp", "Images", "binary", name: "BMP");
        public static readonly ResourceType MVE = new ResourceType(2, "mve", "Video", "video", name: "MVE"); // Video, 
        public static readonly ResourceType TGA = new ResourceType(3, "tga", "Textures", "binary", name: "TGA");
        public static readonly ResourceType WAV = new ResourceType(4, "wav", "Audio", "binary", name: "WAV");
        public static readonly ResourceType PLT = new ResourceType(6, "plt", "Other", "binary", name: "PLT");
        public static readonly ResourceType INI = new ResourceType(7, "ini", "Text Files", "plaintext", name: "INI"); // swkotor.ini
        public static readonly ResourceType BMU = new ResourceType(8, "bmu", "Audio", "binary", name: "BMU"); // mp3 with obfuscated extra header
        public static readonly ResourceType MPG = new ResourceType(9, "mpg", "Video", "binary", name: "MPG");
        public static readonly ResourceType TXT = new ResourceType(10, "txt", "Text Files", "plaintext", name: "TXT");
        public static readonly ResourceType WMA = new ResourceType(11, "wma", "Audio", "binary", name: "WMA");
        public static readonly ResourceType WMV = new ResourceType(12, "wmv", "Audio", "binary", name: "WMV");
        public static readonly ResourceType XMV = new ResourceType(13, "xmv", "Audio", "binary", name: "XMV"); // Xbox video
        public static readonly ResourceType PLH = new ResourceType(2000, "plh", "Models", "binary", name: "PLH");
        public static readonly ResourceType TEX = new ResourceType(2001, "tex", "Textures", "binary", name: "TEX");
        public static readonly ResourceType MDL = new ResourceType(2002, "mdl", "Models", "binary", name: "MDL");
        public static readonly ResourceType THG = new ResourceType(2003, "thg", "Unused", "binary", name: "THG");
        public static readonly ResourceType FNT = new ResourceType(2005, "fnt", "Font", "binary", name: "FNT");
        public static readonly ResourceType LUA = new ResourceType(2007, "lua", "Scripts", "plaintext", name: "LUA");
        public static readonly ResourceType SLT = new ResourceType(2008, "slt", "Unused", "binary", name: "SLT");
        public static readonly ResourceType NSS = new ResourceType(2009, "nss", "Scripts", "plaintext", name: "NSS");
        public static readonly ResourceType NCS = new ResourceType(2010, "ncs", "Scripts", "binary", name: "NCS");
        public static readonly ResourceType MOD = new ResourceType(2011, "mod", "Modules", "binary", name: "MOD");
        public static readonly ResourceType ARE = new ResourceType(2012, "are", "Module Data", "gff", name: "ARE");
        public static readonly ResourceType SET = new ResourceType(2013, "set", "Unused", "binary", name: "SET");
        public static readonly ResourceType IFO = new ResourceType(2014, "ifo", "Module Data", "gff", name: "IFO");
        public static readonly ResourceType BIC = new ResourceType(2015, "bic", "Creatures", "gff", name: "BIC");
        public static readonly ResourceType WOK = new ResourceType(2016, "wok", "Walkmeshes", "binary", name: "WOK");
        public static readonly ResourceType TwoDA = new ResourceType(2017, "2da", "2D Arrays", "binary", name: "TwoDA");
        public static readonly ResourceType TLK = new ResourceType(2018, "tlk", "Talk Tables", "binary", name: "TLK");
        public static readonly ResourceType TXI = new ResourceType(2022, "txi", "Textures", "plaintext", name: "TXI");
        public static readonly ResourceType GIT = new ResourceType(2023, "git", "Module Data", "gff", name: "GIT");
        public static readonly ResourceType BTI = new ResourceType(2024, "bti", "Items", "gff", name: "BTI");
        public static readonly ResourceType UTI = new ResourceType(2025, "uti", "Items", "gff", name: "UTI");
        public static readonly ResourceType BTC = new ResourceType(2026, "btc", "Creatures", "gff", name: "BTC");
        public static readonly ResourceType UTC = new ResourceType(2027, "utc", "Creatures", "gff", name: "UTC");
        public static readonly ResourceType DLG = new ResourceType(2029, "dlg", "Dialogs", "gff", name: "DLG");
        public static readonly ResourceType CNV = new ResourceType(2034, "cnv", "Conversations", "gff", name: "CNV"); // Eclipse Engine conversation format
        public static readonly ResourceType ITP = new ResourceType(2030, "itp", "Palettes", "binary", name: "ITP");
        public static readonly ResourceType BTT = new ResourceType(2031, "btt", "Triggers", "gff", name: "BTT");
        public static readonly ResourceType UTT = new ResourceType(2032, "utt", "Triggers", "gff", name: "UTT");
        public static readonly ResourceType DDS = new ResourceType(2033, "dds", "Textures", "binary", name: "DDS");
        public static readonly ResourceType UTS = new ResourceType(2035, "uts", "Sounds", "gff", name: "UTS");
        public static readonly ResourceType LTR = new ResourceType(2036, "ltr", "Other", "binary", name: "LTR");
        public static readonly ResourceType GFF = new ResourceType(2037, "gff", "Other", "gff", name: "GFF");
        public static readonly ResourceType FAC = new ResourceType(2038, "fac", "Factions", "gff", name: "FAC");
        public static readonly ResourceType BTE = new ResourceType(2039, "bte", "Encounters", "gff", name: "BTE");
        public static readonly ResourceType UTE = new ResourceType(2040, "ute", "Encounters", "gff", name: "UTE");
        public static readonly ResourceType BTD = new ResourceType(2041, "btd", "Doors", "gff", name: "BTD");
        public static readonly ResourceType UTD = new ResourceType(2042, "utd", "Doors", "gff", name: "UTD");
        public static readonly ResourceType BTP = new ResourceType(2043, "btp", "Placeables", "gff", name: "BTP");
        public static readonly ResourceType UTP = new ResourceType(2044, "utp", "Placeables", "gff", name: "UTP");
        public static readonly ResourceType DFT = new ResourceType(2045, "dft", "Defaults", "binary", name: "DFT");
        public static readonly ResourceType DTF = new ResourceType(2045, "dft", "Defaults", "plaintext", name: "DTF");
        public static readonly ResourceType GIC = new ResourceType(2046, "gic", "Module Data", "gff", name: "GIC");
        public static readonly ResourceType GUI = new ResourceType(2047, "gui", "GUIs", "gff", name: "GUI");
        public static readonly ResourceType BTM = new ResourceType(2050, "btm", "Merchants", "gff", name: "BTM");
        public static readonly ResourceType UTM = new ResourceType(2051, "utm", "Merchants", "gff", name: "UTM");
        public static readonly ResourceType DWK = new ResourceType(2052, "dwk", "Walkmeshes", "binary", name: "DWK");
        public static readonly ResourceType PWK = new ResourceType(2053, "pwk", "Walkmeshes", "binary", name: "PWK");
        public static readonly ResourceType JRL = new ResourceType(2056, "jrl", "Journals", "gff", name: "JRL");
        public static readonly ResourceType SAV = new ResourceType(2057, "sav", "Save Data", "erf", name: "SAV");
        public static readonly ResourceType UTW = new ResourceType(2058, "utw", "Waypoints", "gff", name: "UTW");
        public static readonly ResourceType FourPC = new ResourceType(2059, "4pc", "Textures", "binary", name: "FourPC"); // RGBA 16-bit
        public static readonly ResourceType SSF = new ResourceType(2060, "ssf", "Soundsets", "binary", name: "SSF");
        public static readonly ResourceType HAK = new ResourceType(2061, "hak", "Modules", "erf", name: "HAK");
        public static readonly ResourceType NWM = new ResourceType(2062, "nwm", "Modules", "erf", name: "NWM");
        public static readonly ResourceType BIK = new ResourceType(2063, "bik", "Videos", "binary", name: "BIK");
        public static readonly ResourceType NDB = new ResourceType(2064, "ndb", "Other", "binary", name: "NDB");
        public static readonly ResourceType PTM = new ResourceType(2065, "ptm", "Other", "binary", name: "PTM");
        public static readonly ResourceType PTT = new ResourceType(2066, "ptt", "Other", "binary", name: "PTT");
        public static readonly ResourceType NCM = new ResourceType(2067, "ncm", "Unused", "binary", name: "NCM");
        public static readonly ResourceType MFX = new ResourceType(2068, "mfx", "Unused", "binary", name: "MFX");
        public static readonly ResourceType MAT = new ResourceType(2069, "mat", "Materials", "binary", name: "MAT");
        public static readonly ResourceType MDB = new ResourceType(2070, "mdb", "Models", "binary", name: "MDB");
        public static readonly ResourceType SAY = new ResourceType(2071, "say", "Unused", "binary", name: "SAY");
        public static readonly ResourceType TTF = new ResourceType(2072, "ttf", "Fonts", "binary", name: "TTF");
        public static readonly ResourceType TTC = new ResourceType(2073, "ttc", "Unused", "binary", name: "TTC");
        public static readonly ResourceType CUT = new ResourceType(2074, "cut", "Cutscenes", "gff", name: "CUT");
        public static readonly ResourceType KA = new ResourceType(2075, "ka", "Unused", "xml", name: "KA");
        public static readonly ResourceType JPG = new ResourceType(2076, "jpg", "Images", "binary", name: "JPG");
        public static readonly ResourceType ICO = new ResourceType(2077, "ico", "Images", "binary", name: "ICO");
        public static readonly ResourceType OGG = new ResourceType(2078, "ogg", "Audio", "binary", name: "OGG");
        public static readonly ResourceType SPT = new ResourceType(2079, "spt", "Unused", "binary", name: "SPT");
        public static readonly ResourceType SPW = new ResourceType(2080, "spw", "Unused", "binary", name: "SPW");
        public static readonly ResourceType WFX = new ResourceType(2081, "wfx", "Unused", "xml", name: "WFX");
        public static readonly ResourceType UGM = new ResourceType(2082, "ugm", "Unused", "binary", name: "UGM");
        public static readonly ResourceType QDB = new ResourceType(2083, "qdb", "Unused", "gff", name: "QDB");
        public static readonly ResourceType QST = new ResourceType(2084, "qst", "Unused", "gff", name: "QST");
        public static readonly ResourceType NPC = new ResourceType(2085, "npc", "Unused", "binary", name: "NPC");
        public static readonly ResourceType SPN = new ResourceType(2086, "spn", "Unused", "binary", name: "SPN");
        public static readonly ResourceType UTX = new ResourceType(2087, "utx", "Unused", "binary", name: "UTX");
        public static readonly ResourceType MMD = new ResourceType(2088, "mmd", "Unused", "binary", name: "MMD");
        public static readonly ResourceType SMM = new ResourceType(2089, "smm", "Unused", "binary", name: "SMM");
        public static readonly ResourceType UTA = new ResourceType(2090, "uta", "Unused", "binary", name: "UTA");
        public static readonly ResourceType MDE = new ResourceType(2091, "mde", "Unused", "binary", name: "MDE");
        public static readonly ResourceType MDV = new ResourceType(2092, "mdv", "Unused", "binary", name: "MDV");
        public static readonly ResourceType MDA = new ResourceType(2093, "mda", "Unused", "binary", name: "MDA");
        public static readonly ResourceType MBA = new ResourceType(2094, "mba", "Unused", "binary", name: "MBA");
        public static readonly ResourceType OCT = new ResourceType(2095, "oct", "Unused", "binary", name: "OCT");
        public static readonly ResourceType BFX = new ResourceType(2096, "bfx", "Unused", "binary", name: "BFX");
        public static readonly ResourceType PDB = new ResourceType(2097, "pdb", "Unused", "binary", name: "PDB");
        public static readonly ResourceType PVS = new ResourceType(2099, "pvs", "Unused", "binary", name: "PVS");
        public static readonly ResourceType CFX = new ResourceType(2100, "cfx", "Unused", "binary", name: "CFX");
        public static readonly ResourceType LUC = new ResourceType(2101, "luc", "Scripts", "binary", name: "LUC");
        public static readonly ResourceType PNG = new ResourceType(2110, "png", "Images", "binary", name: "PNG");
        public static readonly ResourceType LYT = new ResourceType(3000, "lyt", "Module Data", "plaintext", name: "LYT");
        public static readonly ResourceType VIS = new ResourceType(3001, "vis", "Module Data", "plaintext", name: "VIS");
        public static readonly ResourceType RIM = new ResourceType(3002, "rim", "Modules", "binary", name: "RIM");
        public static readonly ResourceType PTH = new ResourceType(3003, "pth", "Paths", "gff", name: "PTH");
        public static readonly ResourceType LIP = new ResourceType(3004, "lip", "Lips", "lips", name: "LIP");
        public static readonly ResourceType GAM = new ResourceType(3005, "gam", "Save Data", "gff", name: "GAM");
        public static readonly ResourceType TPC = new ResourceType(3007, "tpc", "Textures", "binary", name: "TPC");
        public static readonly ResourceType MDX = new ResourceType(3008, "mdx", "Models", "binary", name: "MDX");
        public static readonly ResourceType CWA = new ResourceType(3027, "cwa", "Crowd Attributes", "gff", name: "CWA");
        public static readonly ResourceType BIP = new ResourceType(3028, "bip", "Lips", "lips", name: "BIP");
        public static readonly ResourceType ERF = new ResourceType(9997, "erf", "Modules", "binary", name: "ERF");
        public static readonly ResourceType BIF = new ResourceType(9998, "bif", "Archives", "binary", name: "BIF");
        public static readonly ResourceType KEY = new ResourceType(9999, "key", "Chitin", "binary", name: "KEY");
        
        // Unreal Engine 3 Package formats (Eclipse Engine - Dragon Age, )
        public static readonly ResourceType PCC = new ResourceType(10000, "pcc", "Packages", "binary", name: "PCC");
        public static readonly ResourceType UPK = new ResourceType(10001, "upk", "Packages", "binary", name: "UPK");

        // For Toolset Use:
        public static readonly ResourceType MP3 = new ResourceType(25014, "mp3", "Audio", "binary", name: "MP3");
        public static readonly ResourceType TLK_XML = new ResourceType(50001, "tlk.xml", "Talk Tables", "plaintext", name: "TLK_XML");
        public static readonly ResourceType MDL_ASCII = new ResourceType(50002, "mdl.ascii", "Models", "plaintext", name: "MDL_ASCII");
        public static readonly ResourceType TwoDA_CSV = new ResourceType(50003, "2da.csv", "2D Arrays", "plaintext", name: "TwoDA_CSV");
        public static readonly ResourceType GFF_XML = new ResourceType(50004, "gff.xml", "Other", "plaintext", targetMember: "GFF", name: "GFF_XML");
        public static readonly ResourceType GFF_JSON = new ResourceType(50005, "gff.json", "Other", "plaintext", targetMember: "GFF", name: "GFF_JSON");
        public static readonly ResourceType IFO_XML = new ResourceType(50006, "ifo.xml", "Module Data", "plaintext", targetMember: "IFO", name: "IFO_XML");
        public static readonly ResourceType GIT_XML = new ResourceType(50007, "git.xml", "Module Data", "plaintext", targetMember: "GIT", name: "GIT_XML");
        public static readonly ResourceType UTI_XML = new ResourceType(50008, "uti.xml", "Items", "plaintext", targetMember: "UTI", name: "UTI_XML");
        public static readonly ResourceType UTC_XML = new ResourceType(50009, "utc.xml", "Creatures", "plaintext", targetMember: "UTC", name: "UTC_XML");
        public static readonly ResourceType DLG_XML = new ResourceType(50010, "dlg.xml", "Dialogs", "plaintext", targetMember: "DLG", name: "DLG_XML");
        public static readonly ResourceType ITP_XML = new ResourceType(50011, "itp.xml", "Palettes", "plaintext", name: "ITP_XML");
        public static readonly ResourceType UTT_XML = new ResourceType(50012, "utt.xml", "Triggers", "plaintext", targetMember: "UTT", name: "UTT_XML");
        public static readonly ResourceType UTS_XML = new ResourceType(50013, "uts.xml", "Sounds", "plaintext", targetMember: "UTS", name: "UTS_XML");
        public static readonly ResourceType FAC_XML = new ResourceType(50014, "fac.xml", "Factions", "plaintext", targetMember: "FAC", name: "FAC_XML");
        public static readonly ResourceType UTE_XML = new ResourceType(50015, "ute.xml", "Encounters", "plaintext", targetMember: "UTE", name: "UTE_XML");
        public static readonly ResourceType UTD_XML = new ResourceType(50016, "utd.xml", "Doors", "plaintext", targetMember: "UTD", name: "UTD_XML");

        public static readonly ResourceType LIP_XML = new ResourceType(50023, "lip.xml", "Lips", "plaintext", targetMember: "LIP", name: "LIP_XML");
        public static readonly ResourceType SSF_XML = new ResourceType(50024, "ssf.xml", "Soundsets", "plaintext", targetMember: "SSF", name: "SSF_XML");
        public static readonly ResourceType ARE_XML = new ResourceType(50025, "are.xml", "Module Data", "plaintext", targetMember: "ARE", name: "ARE_XML");
        public static readonly ResourceType TwoDA_JSON = new ResourceType(50026, "2da.json", "2D Arrays", "plaintext", targetMember: "TwoDA", name: "TwoDA_JSON");
        public static readonly ResourceType TLK_JSON = new ResourceType(50027, "tlk.json", "Talk Tables", "plaintext", targetMember: "TLK", name: "TLK_JSON");
        public static readonly ResourceType LIP_JSON = new ResourceType(50028, "lip.json", "Lips", "plaintext", targetMember: "LIP", name: "LIP_JSON");
        public static readonly ResourceType RES_XML = new ResourceType(50029, "res.xml", "Save Data", "plaintext", targetMember: "RES", name: "RES_XML");
    }
}

