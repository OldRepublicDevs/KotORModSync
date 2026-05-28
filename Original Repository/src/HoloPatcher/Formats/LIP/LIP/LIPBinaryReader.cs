using System;
using System.IO;
using KOTORModSync;
using KOTORModSync.Formats.LIP;

namespace KOTORModSync.Formats.LIP
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lip/io_lip.py:12-56
    // Original: class LIPBinaryReader(ResourceReader)
    public class LIPBinaryReader : IDisposable
    {
        private readonly KOTORModSync.Common.RawBinaryReader _reader;
        private LIP _lip;

        public LIPBinaryReader(byte[] data, int offset = 0, int size = 0)
        {
            int? sizeNullable = size > 0 ? (int?)size : null;
            _reader = KOTORModSync.Common.RawBinaryReader.FromBytes(data, offset, sizeNullable);
        }

        public LIPBinaryReader(string filepath, int offset = 0, int size = 0)
        {
            int? sizeNullable = size > 0 ? (int?)size : null;
            _reader = KOTORModSync.Common.RawBinaryReader.FromFile(filepath, offset, sizeNullable);
        }

        public LIPBinaryReader(Stream source, int offset = 0, int size = 0)
        {
            int? sizeNullable = size > 0 ? (int?)size : null;
            _reader = KOTORModSync.Common.RawBinaryReader.FromStream(source, offset, sizeNullable);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lip/io_lip.py:32-56
        // Original: @autoclose def load(self, *, auto_close: bool = True) -> LIP
        public LIP Load(bool autoClose = true)
        {
            try
            {
                _lip = new LIP();

                string fileType = _reader.ReadString(4);
                string fileVersion = _reader.ReadString(4);

                if (fileType != "LIP ")
                {
                    throw new ArgumentException("The file type that was loaded is invalid.");
                }

                if (fileVersion != "V1.0")
                {
                    throw new ArgumentException("The LIP version that was loaded is not supported.");
                }

                _lip.Length = _reader.ReadSingle();
                uint entryCount = _reader.ReadUInt32();

                // vendor/reone/src/libs/graphics/format/lipreader.cpp:35-45
                for (int i = 0; i < entryCount; i++)
                {
                    float time = _reader.ReadSingle();
                    LIPShape shape = (LIPShape)_reader.ReadUInt8();
                    _lip.Add(time, shape);
                }

                return _lip;
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
            _reader?.Dispose();
        }
    }
}

