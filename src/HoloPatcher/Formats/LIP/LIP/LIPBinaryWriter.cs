using System;
using System.IO;
using KOTORModSync;
using KOTORModSync.Formats.LIP;
using KOTORModSync.Common;

namespace KOTORModSync.Formats.LIP
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lip/io_lip.py:59-81
    // Original: class LIPBinaryWriter(ResourceWriter)
    public class LIPBinaryWriter : IDisposable
    {
        public const int HeaderSize = 16;
        public const int LipEntrySize = 5;

        private readonly LIP _lip;
        private readonly RawBinaryWriter _writer;

        public LIPBinaryWriter(LIP lip, string filepath)
        {
            _lip = lip ?? throw new ArgumentNullException(nameof(lip));
            _writer = RawBinaryWriter.ToFile(filepath);
        }

        public LIPBinaryWriter(LIP lip, Stream target)
        {
            _lip = lip ?? throw new ArgumentNullException(nameof(lip));
            _writer = RawBinaryWriter.ToStream(target);
        }

        public LIPBinaryWriter(LIP lip)
        {
            _lip = lip ?? throw new ArgumentNullException(nameof(lip));
            _writer = RawBinaryWriter.ToByteArray();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lip/io_lip.py:71-81
        // Original: @autoclose def write(self, *, auto_close: bool = True)
        public void Write(bool autoClose = true)
        {
            try
            {
                _writer.WriteString("LIP ", stringLength: 4);
                _writer.WriteString("V1.0", stringLength: 4);
                _writer.WriteSingle(_lip.Length);
                _writer.WriteUInt32((uint)_lip.Count);

                foreach (var keyframe in _lip.Frames)
                {
                    _writer.WriteSingle(keyframe.Time);
                    _writer.WriteUInt8((byte)keyframe.Shape);
                }
            }
            finally
            {
                if (autoClose)
                {
                    Dispose();
                }
            }
        }

        public void Dispose()
        {
            _writer?.Dispose();
        }

        // Matching BWMBinaryWriter pattern for BytesBwm
        // Get the data from the underlying RawBinaryWriter
        public byte[] Data()
        {
            return _writer?.Data() ?? new byte[0];
        }
    }
}
