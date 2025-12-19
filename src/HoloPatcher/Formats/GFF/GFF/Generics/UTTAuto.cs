using System;
using System.IO;
using KOTORModSync;
using KOTORModSync.Formats.GFF;
using KOTORModSync.Resource;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utt.py:327-356
    // Original: def read_utt, def write_utt, def bytes_utt
    public static class UTTAuto
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utt.py:327-333
        // Original: def read_utt(source: SOURCE_TYPES, offset: int = 0, size: int | None = None) -> UTT:
        public static UTT ReadUtt(object source, int offset = 0, int? size = null)
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
                throw new ArgumentException("Source must be string, byte[], or Stream for UTT");
            }
            return UTTHelpers.ConstructUtt(gff);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utt.py:336-345
        // Original: def write_utt(utt: UTT, target: TARGET_TYPES, game: Game = Game.K2, file_format: ResourceType = ResourceType.GFF, *, use_deprecated: bool = True):
        public static void WriteUtt(UTT utt, object target, Game game = Game.K2, ResourceType fileFormat = null, bool useDeprecated = true)
        {
            ResourceType format = fileFormat ?? ResourceType.GFF;
            GFF gff = UTTHelpers.DismantleUtt(utt, game, useDeprecated);
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
                throw new ArgumentException("Target must be string or Stream for UTT");
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utt.py:348-356
        // Original: def bytes_utt(utt: UTT, game: Game = Game.K2, file_format: ResourceType = ResourceType.GFF, *, use_deprecated: bool = True) -> bytes:
        public static byte[] BytesUtt(UTT utt, Game game = Game.K2, ResourceType fileFormat = null, bool useDeprecated = true)
        {
            ResourceType format = fileFormat ?? ResourceType.GFF;
            GFF gff = UTTHelpers.DismantleUtt(utt, game, useDeprecated);
            return GFFAuto.BytesGff(gff, format);
        }
    }
}