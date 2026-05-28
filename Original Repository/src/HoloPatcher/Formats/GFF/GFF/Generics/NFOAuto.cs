using System;
using KOTORModSync.Formats.GFF;
using KOTORModSync.Resource;
using GFFAuto = KOTORModSync.Formats.GFF.GFFAuto;

namespace KOTORModSync.Resource.Generics
{
    /// <summary>
    /// Auto-reader/writer for Odyssey save metadata (<c>NFO</c>) stored as a GFF.
    /// </summary>
    public static class NFOAuto
    {
        public static NFOData ReadNfo(object source, int offset = 0, int? size = null)
        {
            GFF gff = GFFAuto.ReadGff(source, offset, size);
            return NFOHelpers.ConstructNfo(gff);
        }

        public static void WriteNfo(NFOData nfo, object target, ResourceType fileFormat = null)
        {
            ResourceType format = fileFormat ?? ResourceType.GFF;
            if (nfo == null) throw new ArgumentNullException(nameof(nfo));

            // NFO is stored as a GFF file on disk (savenfo.res) but the ResourceType is still GFF.
            if (format != ResourceType.GFF)
            {
                throw new ArgumentException("Unsupported format specified; use GFF (NFO content).", nameof(fileFormat));
            }

            GFF gff = NFOHelpers.DismantleNfo(nfo);
            GFFAuto.WriteGff(gff, target, format);
        }

        public static byte[] BytesNfo(NFOData nfo, ResourceType fileFormat = null)
        {
            ResourceType format = fileFormat ?? ResourceType.GFF;
            if (nfo == null) throw new ArgumentNullException(nameof(nfo));

            if (format != ResourceType.GFF)
            {
                throw new ArgumentException("Unsupported format specified; use GFF (NFO content).", nameof(fileFormat));
            }

            GFF gff = NFOHelpers.DismantleNfo(nfo);
            return GFFAuto.BytesGff(gff, format);
        }
    }
}


