using System;
using System.IO;
using KOTORModSync;
using KOTORModSync.Formats.GFF;
using KOTORModSync.Resource;

namespace KOTORModSync.Resource.Generics.CNV
{
    // Matching pattern from GFFBinaryReader and other GFF-based format readers
    // Original: CNVBinaryReader for reading CNV conversation files
    /// <summary>
    /// Binary reader for CNV (conversation) files.
    /// </summary>
    /// <remarks>
    /// CNV Binary Reader:
    /// - Reads GFF format files with "CNV " signature
    /// - Used by Eclipse Engine games (Dragon Age, )
    /// - Constructs CNV objects from GFF structures
    /// </remarks>
    public class CNVBinaryReader : IDisposable
    {
        private readonly object _source;
        private readonly int _offset;
        private readonly int? _size;
        private CNV _cnv;

        /// <summary>
        /// Creates a CNVBinaryReader from byte data.
        /// </summary>
        /// <param name="data">The CNV file data.</param>
        /// <param name="offset">Byte offset to start reading from (default: 0).</param>
        /// <param name="size">Number of bytes to read (0 = read all).</param>
        public CNVBinaryReader(byte[] data, int offset = 0, int size = 0)
        {
            _source = data ?? throw new ArgumentNullException(nameof(data));
            _offset = offset;
            _size = size > 0 ? (int?)size : null;
        }

        /// <summary>
        /// Creates a CNVBinaryReader from a file path.
        /// </summary>
        /// <param name="filepath">Path to the CNV file.</param>
        /// <param name="offset">Byte offset to start reading from (default: 0).</param>
        /// <param name="size">Number of bytes to read (0 = read all).</param>
        public CNVBinaryReader(string filepath, int offset = 0, int size = 0)
        {
            _source = filepath ?? throw new ArgumentNullException(nameof(filepath));
            _offset = offset;
            _size = size > 0 ? (int?)size : null;
        }

        /// <summary>
        /// Creates a CNVBinaryReader from a stream.
        /// </summary>
        /// <param name="source">Stream to read from.</param>
        /// <param name="offset">Byte offset to start reading from (default: 0).</param>
        /// <param name="size">Number of bytes to read (0 = read all).</param>
        public CNVBinaryReader(Stream source, int offset = 0, int size = 0)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _offset = offset;
            _size = size > 0 ? (int?)size : null;
        }

        /// <summary>
        /// Loads the CNV file and returns the CNV object.
        /// </summary>
        /// <returns>A CNV object with parsed conversation data.</returns>
        public CNV Load()
        {
            // Read GFF first (CNV is a GFF format)
            GFF gff = GFFAuto.ReadGff(_source, _offset, _size, ResourceType.CNV);

            // Construct CNV from GFF
            _cnv = CNVHelper.ConstructCnv(gff);

            return _cnv;
        }

        public void Dispose()
        {
            // GFFBinaryReader doesn't implement IDisposable, so nothing to dispose
        }
    }
}

