using System;
using System.IO;
using KOTORModSync.Resource;
using JetBrains.Annotations;

namespace KOTORModSync.Formats.RIM
{

    /// <summary>
    /// Auto-detection and convenience functions for RIM files.
    /// 1:1 port of Python rim_auto.py from pykotor/resource/formats/rim/rim_auto.py
    /// </summary>
    public static class RIMAuto
    {
        /// <summary>
        /// Returns an RIM instance from the source.
        /// 1:1 port of Python read_rim function.
        /// </summary>
        public static RIM ReadRim(string source, int offset = 0, int size = 0)
        {
            return new RIMBinaryReader(source).Load();
        }

        /// <summary>
        /// Returns an RIM instance from byte data.
        /// </summary>
        public static RIM ReadRim(byte[] data, int offset = 0, int size = 0)
        {
            return new RIMBinaryReader(data).Load();
        }

        /// <summary>
        /// Writes the RIM data to the target location with the specified format (RIM only).
        /// 1:1 port of Python write_rim function.
        /// </summary>
        public static void WriteRim(RIM rim, string target, ResourceType fileFormat)
        {
            if (fileFormat == ResourceType.RIM)
            {
                var writer = new RIMBinaryWriter(rim);
                byte[] data = writer.Write();
                File.WriteAllBytes(target, data);
            }
            else
            {
                throw new ArgumentException("Unsupported format specified; use RIM.");
            }
        }

        /// <summary>
        /// Returns the RIM data as a byte array.
        /// 1:1 port of Python bytes_rim function.
        /// </summary>
        public static byte[] BytesRim(RIM rim, [CanBeNull] ResourceType fileFormat = null)
        {
            var writer = new RIMBinaryWriter(rim);
            return writer.Write();
        }
    }
}

