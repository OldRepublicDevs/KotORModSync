using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using KOTORModSync.Formats;
using KOTORModSync.Resource;
using JetBrains.Annotations;

namespace KOTORModSync.Formats.RIM
{

    /// <summary>
    /// Reads RIM (Resource Information Manager) files.
    /// 1:1 port of Python RIMBinaryReader from pykotor/resource/formats/rim/io_rim.py
    /// </summary>
    public class RIMBinaryReader : BinaryFormatReaderBase
    {
        [CanBeNull]
        private RIM _rim;

        public RIMBinaryReader(byte[] data) : base(data)
        {
        }

        public RIMBinaryReader(string filepath) : base(filepath)
        {
        }

        public RIMBinaryReader(Stream source) : base(source)
        {
        }

        public RIM Load()
        {
            try
            {
                _rim = new RIM();

                Reader.Seek(0);

                string fileType = Encoding.ASCII.GetString(Reader.ReadBytes(4));
                string fileVersion = Encoding.ASCII.GetString(Reader.ReadBytes(4));

                if (fileType != "RIM ")
                {
                    throw new InvalidDataException("The RIM file type that was loaded was unrecognized.");
                }

                if (fileVersion != "V1.0")
                {
                    throw new InvalidDataException("The RIM version that was loaded is not supported.");
                }

                Reader.SeekRelative(4); // Skip 4 bytes
                uint entryCount = Reader.ReadUInt32();
                uint offsetToKeys = Reader.ReadUInt32();

                var resrefs = new List<string>();
                var resids = new List<uint>();
                var restypes = new List<uint>();
                var resoffsets = new List<uint>();
                var ressizes = new List<uint>();

                Reader.Seek((int)offsetToKeys);
                for (uint i = 0; i < entryCount; i++)
                {
                    string resrefStr = Encoding.ASCII.GetString(Reader.ReadBytes(16)).TrimEnd('\0');
                    resrefs.Add(resrefStr.ToLowerInvariant());
                    restypes.Add(Reader.ReadUInt32());
                    resids.Add(Reader.ReadUInt32());
                    resoffsets.Add(Reader.ReadUInt32());
                    ressizes.Add(Reader.ReadUInt32());
                }

                for (int i = 0; i < entryCount; i++)
                {
                    Reader.Seek((int)resoffsets[i]);
                    byte[] resdata = Reader.ReadBytes((int)ressizes[i]);
                    ResourceType resType = ResourceType.FromId((int)restypes[i]);
                    _rim.SetData(resrefs[i], resType, resdata);
                }

                return _rim;
            }
            catch (EndOfStreamException ex)
            {
                throw new InvalidDataException("Corrupted or truncated RIM file.", ex);
            }
        }
    }
}

