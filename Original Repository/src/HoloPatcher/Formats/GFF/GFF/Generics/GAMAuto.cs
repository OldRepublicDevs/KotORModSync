using System;
using System.IO;
using KOTORModSync;
using KOTORModSync.Formats.GFF;
using KOTORModSync.Resource;
using KOTORModSync.Common;
using GFFAuto = KOTORModSync.Formats.GFF.GFFAuto;

namespace KOTORModSync.Resource.Generics
{
    // Matching pattern from other GFF-based format auto classes (IFO, ARE, etc.)
    // Original: read_gam, write_gam, bytes_gam functions for GAM file format
    public static class GAMAuto
    {
        /// <summary>
        /// Reads a GAM file from various source types.
        /// </summary>
        /// <param name="source">Source to read from (string filepath, byte[] data, or Stream).</param>
        /// <param name="offset">Byte offset to start reading from (default: 0).</param>
        /// <param name="size">Number of bytes to read (null = read all).</param>
        /// <returns>A GAM object with parsed data.</returns>
        /// <remarks>
        /// GAM Reading:
        /// - Reads GFF format file with "GAM " signature
        /// - Supports file paths, byte arrays, and streams
        /// - Constructs GAM object from GFF structure
        /// </remarks>
        public static GAM ReadGam(object source, int offset = 0, int? size = null)
        {
            // Read GFF first (GAM is a GFF format)
            GFF gff = GFFAuto.ReadGff(source, offset, size);

            // Construct GAM from GFF
            return GAMHelpers.ConstructGam(gff);
        }

        /// <summary>
        /// Writes a GAM file to various target types.
        /// </summary>
        /// <param name="gam">The GAM object to write.</param>
        /// <param name="target">Target to write to (string filepath or Stream).</param>
        /// <param name="game">The game type (must be Aurora or , not Odyssey).</param>
        /// <param name="fileFormat">File format (default: GAM, must be GAM).</param>
        /// <remarks>
        /// GAM Writing:
        /// - Validates game type (Aurora or Infinity only, not Odyssey)
        /// - Dismantles GAM object to GFF structure
        /// - Writes GFF format file with "GAM " signature
        /// - Supports file paths and streams
        /// </remarks>
        public static void WriteGam(GAM gam, object target, Game game, ResourceType fileFormat = null)
        {
            ResourceType format = fileFormat ?? ResourceType.GAM;
            if (format != ResourceType.GAM)
            {
                throw new ArgumentException("Unsupported format specified; use GAM.", nameof(fileFormat));
            }

            // Validate game type - GAM format is only used by Aurora and Infinity, NOT Odyssey
            if (game.IsOdyssey())
            {
                throw new ArgumentException(
                    $"GAM format is not supported for Odyssey engine (KOTOR). Odyssey uses NFO format for save games. " +
                    $"GAM format is only supported for Aurora (NWN) and  (, , ). " +
                    $"Provided game: {game}",
                    nameof(game));
            }

            // For now, only Aurora is supported ( games not in Game enum yet)
            // When  games are added to Game enum, update this validation
            if (!game.IsAurora())
            {
                throw new ArgumentException(
                    $"GAM format is only supported for Aurora (NWN) and  games. " +
                    $"Currently only Aurora (NWN, NWN2) is supported as  games are not yet in the Game enum. " +
                    $"Provided game: {game}",
                    nameof(game));
            }

            // Dismantle GAM to GFF
            GFF gff = GAMHelpers.DismantleGam(gam, game);

            // Write GFF
            GFFAuto.WriteGff(gff, target, format);
        }

        /// <summary>
        /// Converts a GAM object to bytes.
        /// </summary>
        /// <param name="gam">The GAM object to convert.</param>
        /// <param name="game">The game type (must be Aurora or , not Odyssey).</param>
        /// <param name="fileFormat">File format (default: GAM, must be GAM).</param>
        /// <returns>Byte array containing GAM file data.</returns>
        /// <remarks>
        /// GAM Bytes Conversion:
        /// - Validates game type (Aurora or Infinity only, not Odyssey)
        /// - Dismantles GAM object to GFF structure
        /// - Converts GFF to bytes with "GAM " signature
        /// </remarks>
        public static byte[] BytesGam(GAM gam, Game game, ResourceType fileFormat = null)
        {
            ResourceType format = fileFormat ?? ResourceType.GAM;
            if (format != ResourceType.GAM)
            {
                throw new ArgumentException("Unsupported format specified; use GAM.", nameof(fileFormat));
            }

            // Validate game type - GAM format is only used by Aurora and Infinity, NOT Odyssey
            if (game.IsOdyssey())
            {
                throw new ArgumentException(
                    $"GAM format is not supported for Odyssey engine (KOTOR). Odyssey uses NFO format for save games. " +
                    $"GAM format is only supported for Aurora (NWN) and  (, , ). " +
                    $"Provided game: {game}",
                    nameof(game));
            }

            // For now, only Aurora is supported ( games not in Game enum yet)
            // When  games are added to Game enum, update this validation
            if (!game.IsAurora())
            {
                throw new ArgumentException(
                    $"GAM format is only supported for Aurora (NWN) and  games. " +
                    $"Currently only Aurora (NWN, NWN2) is supported as  games are not yet in the Game enum. " +
                    $"Provided game: {game}",
                    nameof(game));
            }

            // Dismantle GAM to GFF
            GFF gff = GAMHelpers.DismantleGam(gam, game);

            // Convert GFF to bytes
            return GFFAuto.BytesGff(gff, format);
        }
    }
}

