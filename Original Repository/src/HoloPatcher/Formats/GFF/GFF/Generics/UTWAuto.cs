using System;
using System.IO;
using KOTORModSync;
using KOTORModSync.Formats.GFF;
using KOTORModSync.Resource;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utw.py:155-184
    // Original: def read_utw, def write_utw, def bytes_utw
    public static class UTWAuto
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utw.py:155-161
        // Original: def read_utw(source: SOURCE_TYPES, offset: int = 0, size: int | None = None) -> UTW:
        public static UTW ReadUtw(object source, int offset = 0, int? size = null)
        {
            int sizeValue = size ?? 0;
            GFF gff;
            if (source is string filepath)
            {
                gff = new GFFBinaryReader(filepath).Load();
            }
            else if (source is byte[] data)
            {
                using (var ms = new MemoryStream(data, offset, sizeValue > 0 ? sizeValue : data.Length - offset))
                {
                    gff = new GFFBinaryReader(ms).Load();
                }
            }
            else if (source is Stream stream)
            {
                gff = new GFFBinaryReader(stream).Load();
            }
            else
            {
                throw new ArgumentException("Source must be string, byte[], or Stream for UTW");
            }
            return UTWHelpers.ConstructUtw(gff);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utw.py:164-173
        // Original: def write_utw(utw: UTW, target: TARGET_TYPES, game: Game = Game.K2, file_format: ResourceType = ResourceType.GFF, *, use_deprecated: bool = True):
        public static void WriteUtw(UTW utw, object target, Game game = Game.K2, ResourceType fileFormat = null, bool useDeprecated = true)
        {
            ResourceType format = fileFormat ?? ResourceType.GFF;
            GFF gff = UTWHelpers.DismantleUtw(utw, game, useDeprecated);
            if (target is string filepath)
            {
                GFFAuto.WriteGff(gff, filepath, format);
            }
            else if (target is Stream stream)
            {
                byte[] data = GFFAuto.BytesGff(gff, format);
                stream.Write(data, 0, data.Length);
            }
            else
            {
                throw new ArgumentException("Target must be string or Stream for UTW");
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utw.py:176-184
        // Original: def bytes_utw(utw: UTW, game: Game = Game.K2, file_format: ResourceType = ResourceType.GFF, *, use_deprecated: bool = True) -> bytes:
        public static byte[] BytesUtw(UTW utw, Game game = Game.K2, ResourceType fileFormat = null, bool useDeprecated = true)
        {
            ResourceType format = fileFormat ?? ResourceType.GFF;
            GFF gff = UTWHelpers.DismantleUtw(utw, game, useDeprecated);
            return GFFAuto.BytesGff(gff, format);
        }
    }
}