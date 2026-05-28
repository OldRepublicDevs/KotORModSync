using System;
using System.IO;
using KOTORModSync;
using KOTORModSync.Formats.GFF;
using KOTORModSync.Resource;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics.CNV
{
    // Matching pattern from GFFBinaryWriter and other GFF-based format writers
    // Original: CNVBinaryWriter for writing CNV conversation files
    /// <summary>
    /// Binary writer for CNV (conversation) files.
    /// </summary>
    /// <remarks>
    /// CNV Binary Writer:
    /// - Writes GFF format files with "CNV " signature
    /// - Used by Eclipse Engine games (Dragon Age, )
    /// - Dismantles CNV objects to GFF structures
    /// </remarks>
    public class CNVBinaryWriter
    {
        private readonly CNV _cnv;
        private readonly Game _game;
        private readonly string _filepath;
        private readonly Stream _stream;
        private byte[] _writtenBytes;

        /// <summary>
        /// Creates a CNVBinaryWriter for writing to a file.
        /// </summary>
        /// <param name="cnv">The CNV object to write.</param>
        /// <param name="filepath">Path to write the CNV file to.</param>
        /// <param name="game">The game type (must be Eclipse engine).</param>
        public CNVBinaryWriter(CNV cnv, string filepath, Game game)
        {
            _cnv = cnv ?? throw new ArgumentNullException(nameof(cnv));
            _filepath = filepath ?? throw new ArgumentNullException(nameof(filepath));
            _game = game;
            
            // Validate game type - CNV format is only used by Eclipse Engine
            if (!game.IsEclipse())
            {
                throw new ArgumentException(
                    $"CNV format is only supported for Eclipse Engine games (Dragon Age, ). " +
                    $"Provided game: {game}",
                    nameof(game));
            }
        }

        /// <summary>
        /// Creates a CNVBinaryWriter for writing to a stream.
        /// </summary>
        /// <param name="cnv">The CNV object to write.</param>
        /// <param name="target">Stream to write to.</param>
        /// <param name="game">The game type (must be Eclipse engine).</param>
        public CNVBinaryWriter(CNV cnv, Stream target, Game game)
        {
            _cnv = cnv ?? throw new ArgumentNullException(nameof(cnv));
            _stream = target ?? throw new ArgumentNullException(nameof(target));
            _game = game;
            
            // Validate game type - CNV format is only used by Eclipse Engine
            if (!game.IsEclipse())
            {
                throw new ArgumentException(
                    $"CNV format is only supported for Eclipse Engine games (Dragon Age, ). " +
                    $"Provided game: {game}",
                    nameof(game));
            }
        }

        /// <summary>
        /// Creates a CNVBinaryWriter for writing to a byte array.
        /// </summary>
        /// <param name="cnv">The CNV object to write.</param>
        /// <param name="game">The game type (must be Eclipse engine).</param>
        public CNVBinaryWriter(CNV cnv, Game game)
        {
            _cnv = cnv ?? throw new ArgumentNullException(nameof(cnv));
            _game = game;
            
            // Validate game type - CNV format is only used by Eclipse Engine
            if (!game.IsEclipse())
            {
                throw new ArgumentException(
                    $"CNV format is only supported for Eclipse Engine games (Dragon Age, ). " +
                    $"Provided game: {game}",
                    nameof(game));
            }
        }

        /// <summary>
        /// Writes the CNV file.
        /// </summary>
        public void Write()
        {
            // Dismantle CNV to GFF
            GFF gff = CNVHelper.DismantleCnv(_cnv, _game);

            // Write GFF using GFFBinaryWriter (always writes to byte array)
            var gffWriter = new GFFBinaryWriter(gff);
            byte[] data = gffWriter.Write();

            // Write to appropriate destination
            if (_filepath != null)
            {
                File.WriteAllBytes(_filepath, data);
            }
            else if (_stream != null)
            {
                _stream.Write(data, 0, data.Length);
            }
            else
            {
                _writtenBytes = data;
            }
        }

        /// <summary>
        /// Gets the written bytes (only available when writing to byte array).
        /// </summary>
        public byte[] ToByteArray()
        {
            return _writtenBytes;
        }
    }
}
