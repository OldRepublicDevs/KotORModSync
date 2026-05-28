namespace KOTORModSync.Formats.ERF
{

    /// <summary>
    /// The type of ERF/capsule file.
    /// </summary>
    public enum ERFType
    {
        /// <summary>
        /// Standard ERF archive (ERF)
        /// </summary>
        ERF,

        /// <summary>
        /// Module file (MOD) or Save file (SAV) - same format
        /// </summary>
        MOD
    }

    public static class ERFTypeExtensions
    {
        public static string ToFourCC(this ERFType type)
        {
            switch (type)
            {
                case ERFType.ERF:
                    return "ERF ";
                case ERFType.MOD:
                    return "MOD ";
                default:
                    return "ERF ";
            }
            ;
        }

        public static ERFType FromFourCC(string fourCC)
        {
            switch (fourCC?.Trim())
            {
                case "ERF":
                    return ERFType.ERF;
                case "MOD":
                    return ERFType.MOD;
                default:
                    return ERFType.ERF;
            }
            ;
        }

        public static ERFType FromExtension(string extension)
        {
            string ext = extension.TrimStart('.').ToLowerInvariant();
            switch (ext)
            {
                case "erf":
                    return ERFType.ERF;
                case "mod":
                    return ERFType.MOD;
                case "sav":
                    return ERFType.MOD; // SAV files use MOD format
                default:
                    throw new System.ArgumentException($"Invalid ERF extension: {extension}");
            }
        }
    }
}

