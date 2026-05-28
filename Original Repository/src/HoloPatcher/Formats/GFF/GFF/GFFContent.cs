using System.Collections.Generic;
using KOTORModSync.Common;

namespace KOTORModSync.Formats.GFF
{

    /// <summary>
    /// The different resources that the GFF can represent.
    /// </summary>
    public enum GFFContent
    {
        GFF,
        BIC,
        BTC,
        BTD,
        BTE,
        BTI,
        BTP,
        BTM,
        BTT,
        UTC,
        UTD,
        UTE,
        UTI,
        UTP,
        UTS,
        UTM,
        UTT,
        UTW,
        ARE,
        DLG,
        FAC,
        GIT,
        GUI,
        IFO,
        ITP,
        JRL,
        PTH,
        NFO,
        PT,
        GVT,
        INV,
        GAM,
        CNV
    }

    public static class GFFContentExtensions
    {
        public static string ToFourCC(this GFFContent content)
        {
            return content.ToString().PadRight(4);
        }

        public static GFFContent FromResName(string resname)
        {
            if (string.IsNullOrEmpty(resname))
            {
                return GFFContent.GFF;
            }

            string lower = resname.ToLowerInvariant();
            if (lower.EndsWith(".are")) return GFFContent.ARE;
            if (lower.EndsWith(".dlg")) return GFFContent.DLG;
            if (lower.EndsWith(".git")) return GFFContent.GIT;
            if (lower.EndsWith(".ifo")) return GFFContent.IFO;
            if (lower.EndsWith(".jrl")) return GFFContent.JRL;
            if (lower.EndsWith(".pth")) return GFFContent.PTH;
            if (lower.EndsWith(".utc")) return GFFContent.UTC;
            if (lower.EndsWith(".utd")) return GFFContent.UTD;
            if (lower.EndsWith(".ute")) return GFFContent.UTE;
            if (lower.EndsWith(".uti")) return GFFContent.UTI;
            if (lower.EndsWith(".utm")) return GFFContent.UTM;
            if (lower.EndsWith(".utp")) return GFFContent.UTP;
            if (lower.EndsWith(".uts")) return GFFContent.UTS;
            if (lower.EndsWith(".utt")) return GFFContent.UTT;
            if (lower.EndsWith(".utw")) return GFFContent.UTW;
            if (lower.EndsWith(".gam")) return GFFContent.GAM;
            if (lower.EndsWith(".cnv")) return GFFContent.CNV;
            return GFFContent.GFF;
        }

        public static GFFContent FromFourCC(string fourCC)
        {
            switch (fourCC.Trim())
            {
                case "GFF": return GFFContent.GFF;
                case "BIC": return GFFContent.BIC;
                case "BTC": return GFFContent.BTC;
                case "BTD": return GFFContent.BTD;
                case "BTE": return GFFContent.BTE;
                case "BTI": return GFFContent.BTI;
                case "BTP": return GFFContent.BTP;
                case "BTM": return GFFContent.BTM;
                case "BTT": return GFFContent.BTT;
                case "UTC": return GFFContent.UTC;
                case "UTD": return GFFContent.UTD;
                case "UTE": return GFFContent.UTE;
                case "UTI": return GFFContent.UTI;
                case "UTP": return GFFContent.UTP;
                case "UTS": return GFFContent.UTS;
                case "UTM": return GFFContent.UTM;
                case "UTT": return GFFContent.UTT;
                case "UTW": return GFFContent.UTW;
                case "ARE": return GFFContent.ARE;
                case "DLG": return GFFContent.DLG;
                case "FAC": return GFFContent.FAC;
                case "GIT": return GFFContent.GIT;
                case "GUI": return GFFContent.GUI;
                case "IFO": return GFFContent.IFO;
                case "ITP": return GFFContent.ITP;
                case "JRL": return GFFContent.JRL;
                case "PTH": return GFFContent.PTH;
                case "NFO": return GFFContent.NFO;
                case "PT": return GFFContent.PT;
                case "GVT": return GFFContent.GVT;
                case "INV": return GFFContent.INV;
                case "GAM": return GFFContent.GAM;
                case "CNV": return GFFContent.CNV;
                default: return GFFContent.GFF;
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/gff/gff_data.py
        // Original: @classmethod def get_extensions(cls) -> list[str]:
        /// <summary>
        /// Get all file extensions that represent GFF-based formats.
        /// </summary>
        public static HashSet<string> GetExtensions()
        {
            return new HashSet<string>
            {
                "gff", "utc", "uti", "utp", "ute", "utm", "utd", "utw",
                "dlg", "cnv", "are", "git", "ifo", "gui", "jrl", "fac", "pth",
                "bic", "btc", "btd", "bte", "bti", "btp", "btm", "btt",
                "itp", "nfo", "pt", "gvt", "inv", "gam"
            };
        }

        /// <summary>
        /// Check if a fourCC string represents a valid GFF content type.
        /// </summary>
        public static bool IsValidGFFContent(string fourCC)
        {
            if (string.IsNullOrEmpty(fourCC))
            {
                return false;
            }
            string trimmedFourCC = fourCC.Trim();
            return System.Enum.TryParse<GFFContent>(trimmedFourCC, ignoreCase: true, out _);
        }
    }
}
