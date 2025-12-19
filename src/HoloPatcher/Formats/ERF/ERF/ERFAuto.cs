using System;
using System.IO;
using KOTORModSync.Resource;
using JetBrains.Annotations;

namespace KOTORModSync.Formats.ERF
{

    /// <summary>
    /// Auto-detection and convenience functions for ERF files.
    /// 1:1 port of Python erf_auto.py from pykotor/resource/formats/erf/erf_auto.py
    /// </summary>
    public static class ERFAuto
    {
        /// <summary>
        /// Returns an ERF instance from the source.
        /// 1:1 port of Python read_erf function.
        /// </summary>
        public static ERF ReadErf(string source, int offset = 0, int size = 0)
        {
            return new ERFBinaryReader(source).Load();
        }

        /// <summary>
        /// Returns an ERF instance from byte data.
        /// </summary>
        public static ERF ReadErf(byte[] data, int offset = 0, int size = 0)
        {
            return new ERFBinaryReader(data).Load();
        }

        /// <summary>
        /// Writes the ERF data to the target location with the specified format (ERF, MOD, SAV, or HAK).
        /// 1:1 port of Python write_erf function.
        /// </summary>
        public static void WriteErf(ERF erf, string target, ResourceType fileFormat)
        {
            if (fileFormat == ResourceType.ERF || fileFormat == ResourceType.MOD || fileFormat == ResourceType.SAV || fileFormat == ResourceType.HAK)
            {
                var writer = new ERFBinaryWriter(erf);
                byte[] data = writer.Write();
                File.WriteAllBytes(target, data);
            }
            else
            {
                throw new ArgumentException($"Unsupported format specified: '{fileFormat}'; expected one of ResourceType.ERF, ResourceType.MOD, ResourceType.SAV, ResourceType.HAK.");
            }
        }

        /// <summary>
        /// Returns the ERF data as a byte array.
        /// 1:1 port of Python bytes_erf function.
        /// </summary>
        public static byte[] BytesErf(ERF erf, [CanBeNull] ResourceType fileFormat = null)
        {
            var writer = new ERFBinaryWriter(erf);
            return writer.Write();
        }
    }
}

