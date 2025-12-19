using System;
using System.IO;
using KOTORModSync.Resource;
using JetBrains.Annotations;

namespace KOTORModSync.Formats.TwoDA
{

    /// <summary>
    /// Auto-detection and convenience functions for TwoDA files.
    /// 1:1 port of Python twoda_auto.py from pykotor/resource/formats/twoda/twoda_auto.py
    /// </summary>
    public static class TwoDAAuto
    {
        /// <summary>
        /// Writes the TwoDA data to the target location with the specified format.
        /// 1:1 port of Python write_2da function.
        /// </summary>
        public static void WriteTwoDA(TwoDA twoda, string target, ResourceType fileFormat)
        {
            if (fileFormat == ResourceType.TwoDA)
            {
                var writer = new TwoDABinaryWriter(twoda);
                byte[] data = writer.Write();
                File.WriteAllBytes(target, data);
            }
            else
            {
                throw new ArgumentException("Unsupported format specified; use TwoDA, TwoDA_CSV or TwoDA_JSON.");
            }
        }

        /// <summary>
        /// Returns the TwoDA data as a byte array.
        /// 1:1 port of Python bytes_2da function.
        /// </summary>
        public static byte[] BytesTwoDA(TwoDA twoda, [CanBeNull] ResourceType fileFormat = null)
        {
            var writer = new TwoDABinaryWriter(twoda);
            return writer.Write();
        }
    }
}

