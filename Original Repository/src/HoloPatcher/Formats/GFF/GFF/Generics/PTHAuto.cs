using System;
using System.IO;
using KOTORModSync;
using KOTORModSync.Formats.GFF;
using KOTORModSync.Resource;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/pth.py:212-241
    // Original: def read_pth, def write_pth, def bytes_pth
    public static class PTHAuto
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/pth.py:212-218
        // Original: def read_pth(source: SOURCE_TYPES, offset: int = 0, size: int | None = None) -> PTH:
        public static PTH ReadPth(object source, int offset = 0, int? size = null)
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
                throw new ArgumentException("Source must be string, byte[], or Stream for PTH");
            }
            return PTHHelpers.ConstructPth(gff);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/pth.py:221-230
        // Original: def write_pth(pth: PTH, target: TARGET_TYPES, game: Game = Game.K2, file_format: ResourceType = ResourceType.GFF, *, use_deprecated: bool = True):
        public static void WritePth(PTH pth, object target, Game game = Game.K2, ResourceType fileFormat = null, bool useDeprecated = true)
        {
            ResourceType format = fileFormat ?? ResourceType.GFF;
            GFF gff = PTHHelpers.DismantlePth(pth, game, useDeprecated);
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
                throw new ArgumentException("Target must be string or Stream for PTH");
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/pth.py:233-241
        // Original: def bytes_pth(pth: PTH, game: Game = Game.K2, file_format: ResourceType = ResourceType.GFF, *, use_deprecated: bool = True) -> bytes:
        public static byte[] BytesPth(PTH pth, Game game = Game.K2, ResourceType fileFormat = null, bool useDeprecated = true)
        {
            ResourceType format = fileFormat ?? ResourceType.GFF;
            GFF gff = PTHHelpers.DismantlePth(pth, game, useDeprecated);
            return GFFAuto.BytesGff(gff, format);
        }
    }
}
