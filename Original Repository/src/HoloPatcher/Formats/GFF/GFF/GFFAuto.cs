using System;
using System.IO;
using KOTORModSync.Resource;

namespace KOTORModSync.Formats.GFF
{

    /// <summary>
    /// Auto-detection and convenience functions for GFF files.
    /// 1:1 port of Python gff_auto.py from pykotor/resource/formats/gff/gff_auto.py
    /// </summary>
    public static class GFFAuto
    {
        /// <summary>
        /// Compatibility overload for helpers that call <c>ReadGff(source, offset, size)</c>.
        /// </summary>
        public static GFF ReadGff(object source, int offset = 0, int? size = null)
        {
            return ReadGff(source, offset, size, null);
        }

        /// <summary>
        /// Reads the GFF data from the source location with the specified format (GFF or GFF_XML).
        /// 1:1 port of Python read_gff function.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/gff/gff_auto.py:66-107
        /// </summary>
        public static GFF ReadGff(object source, int offset = 0, int? size = null, ResourceType fileFormat = null)
        {
            ResourceType format = fileFormat ?? ResourceType.GFF;
            
            if (format.IsGff())
            {
                byte[] data;
                if (source is string filePath)
                {
                    data = File.ReadAllBytes(filePath);
                }
                else if (source is byte[] bytes)
                {
                    data = bytes;
                }
                else if (source is Stream stream)
                {
                    using (var ms = new MemoryStream())
                    {
                        stream.CopyTo(ms);
                        data = ms.ToArray();
                    }
                }
                else
                {
                    throw new ArgumentException("Source must be a file path, byte array, or stream.", nameof(source));
                }
                
                var reader = new GFFBinaryReader(data, offset, size ?? 0);
                return reader.Load();
            }
            else
            {
                throw new ArgumentException("Unsupported format specified; use GFF, GFF_XML, or GFF_JSON.");
            }
        }


        /// <summary>
        /// Writes the GFF data to the target location with the specified format (GFF or GFF_XML).
        /// 1:1 port of Python write_gff function.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/gff/gff_auto.py:109-143
        /// </summary>
        public static void WriteGff(GFF gff, object target, ResourceType fileFormat = null)
        {
            ResourceType format = fileFormat ?? ResourceType.GFF;
            
            if (format.IsGff())
            {
                // Set content type from filename if not already set
                if (gff.Content == GFFContent.GFF && target is string targetPath && !string.IsNullOrEmpty(targetPath))
                {
                    gff.Content = GFFContentExtensions.FromResName(targetPath);
                }

                var writer = new GFFBinaryWriter(gff);
                byte[] data = writer.Write();
                
                if (target is string filePath)
                {
                    File.WriteAllBytes(filePath, data);
                }
                else if (target is Stream stream)
                {
                    stream.Write(data, 0, data.Length);
                }
                else
                {
                    throw new ArgumentException("Target must be a file path or stream.", nameof(target));
                }
            }
            else
            {
                throw new ArgumentException("Unsupported format specified; use GFF, GFF_XML, or GFF_JSON.");
            }
        }

        /// <summary>
        /// Returns the GFF data as a byte array.
        /// 1:1 port of Python bytes_gff function.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/gff/gff_auto.py:145-169
        /// </summary>
        public static byte[] BytesGff(GFF gff, ResourceType fileFormat = null)
        {
            ResourceType format = fileFormat ?? ResourceType.GFF;
            var writer = new GFFBinaryWriter(gff);
            return writer.Write();
        }
    }
}

