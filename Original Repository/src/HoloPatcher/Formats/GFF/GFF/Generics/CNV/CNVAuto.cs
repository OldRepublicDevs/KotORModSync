using System;
using System.IO;
using KOTORModSync;
using KOTORModSync.Formats.GFF;
using KOTORModSync.Resource;
using KOTORModSync.Common;
using GFFAuto = KOTORModSync.Formats.GFF.GFFAuto;

namespace KOTORModSync.Resource.Generics.CNV
{
    // Matching pattern from GAMAuto and other GFF-based format auto classes
    // Original: read_cnv, write_cnv, bytes_cnv functions for CNV conversation format
    public static class CNVAuto
    {
        /// <summary>
        /// Reads a CNV file from various source types.
        /// </summary>
        /// <param name="source">Source to read from (string filepath, byte[] data, or Stream).</param>
        /// <param name="offset">Byte offset to start reading from (default: 0).</param>
        /// <param name="size">Number of bytes to read (null = read all).</param>
        /// <returns>A CNV object with parsed conversation data.</returns>
        /// <remarks>
        /// CNV Reading:
        /// - Reads GFF format file with "CNV " signature
        /// - Supports file paths, byte arrays, and streams
        /// - Constructs CNV object from GFF structure
        /// - Used by Eclipse Engine games (Dragon Age, )
        /// </remarks>
        public static CNV ReadCnv(object source, int offset = 0, int? size = null)
        {
            // Read GFF first (CNV is a GFF format)
            GFF gff = GFFAuto.ReadGff(source, offset, size);

            // Construct CNV from GFF
            return CNVHelper.ConstructCnv(gff);
        }

        /// <summary>
        /// Writes a CNV file to various target types.
        /// </summary>
        /// <param name="cnv">The CNV object to write.</param>
        /// <param name="target">Target to write to (string filepath or Stream).</param>
        /// <param name="game">The game type (must be Eclipse engine).</param>
        /// <param name="fileFormat">File format (default: CNV, must be CNV or GFF).</param>
        /// <remarks>
        /// CNV Writing:
        /// - Validates game type (Eclipse only)
        /// - Dismantles CNV object to GFF structure
        /// - Writes GFF format file with "CNV " signature
        /// - Supports file paths and streams
        /// </remarks>
        public static void WriteCnv(CNV cnv, object target, Game game, ResourceType fileFormat = null)
        {
            ResourceType format = fileFormat ?? ResourceType.CNV;
            if (format != ResourceType.CNV && format != ResourceType.GFF)
            {
                throw new ArgumentException("Unsupported format specified; use CNV or GFF.", nameof(fileFormat));
            }

            // Validate game type - CNV format is only used by Eclipse Engine
            if (!game.IsEclipse())
            {
                throw new ArgumentException(
                    $"CNV format is only supported for Eclipse Engine games (Dragon Age, ). " +
                    $"Provided game: {game}",
                    nameof(game));
            }

            // Dismantle CNV to GFF
            GFF gff = CNVHelper.DismantleCnv(cnv, game);

            // Write GFF
            if (target is string filepath)
            {
                GFFAuto.WriteGff(gff, filepath, format);
            }
            else if (target is Stream stream)
            {
                GFFAuto.WriteGff(gff, stream, format);
            }
            else
            {
                throw new ArgumentException("Target must be string or Stream for CNV", nameof(target));
            }
        }

        /// <summary>
        /// Converts a CNV object to bytes.
        /// </summary>
        /// <param name="cnv">The CNV object to convert.</param>
        /// <param name="game">The game type (must be Eclipse engine).</param>
        /// <param name="fileFormat">File format (default: CNV, must be CNV or GFF).</param>
        /// <returns>Byte array containing CNV file data.</returns>
        /// <remarks>
        /// CNV Bytes Conversion:
        /// - Validates game type (Eclipse only)
        /// - Dismantles CNV object to GFF structure
        /// - Converts GFF to bytes with "CNV " signature
        /// </remarks>
        public static byte[] BytesCnv(CNV cnv, Game game, ResourceType fileFormat = null)
        {
            ResourceType format = fileFormat ?? ResourceType.CNV;
            if (format != ResourceType.CNV && format != ResourceType.GFF)
            {
                throw new ArgumentException("Unsupported format specified; use CNV or GFF.", nameof(fileFormat));
            }

            // Validate game type - CNV format is only used by Eclipse Engine
            if (!game.IsEclipse())
            {
                throw new ArgumentException(
                    $"CNV format is only supported for Eclipse Engine games (Dragon Age, ). " +
                    $"Provided game: {game}",
                    nameof(game));
            }

            // Dismantle CNV to GFF
            GFF gff = CNVHelper.DismantleCnv(cnv, game);

            // Convert GFF to bytes
            return GFFAuto.BytesGff(gff, format);
        }
    }
}

