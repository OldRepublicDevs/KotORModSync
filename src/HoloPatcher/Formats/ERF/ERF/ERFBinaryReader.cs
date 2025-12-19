using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using KOTORModSync.Formats;
using KOTORModSync.Resource;
using JetBrains.Annotations;

namespace KOTORModSync.Formats.ERF
{

    /// <summary>
    /// Reads ERF (Encapsulated Resource File) files.
    /// 1:1 port of Python ERFBinaryReader from pykotor/resource/formats/erf/io_erf.py
    /// </summary>
    public class ERFBinaryReader : BinaryFormatReaderBase
    {
        [CanBeNull]
        private ERF _erf;

        public ERFBinaryReader(byte[] data) : base(data)
        {
        }

        public ERFBinaryReader(string filepath) : base(filepath)
        {
        }

        public ERFBinaryReader(Stream source) : base(source)
        {
        }

        public ERF Load()
        {
            try
            {
                Reader.Seek(0);

                string fileType = Encoding.ASCII.GetString(Reader.ReadBytes(4));
                string fileVersion = Encoding.ASCII.GetString(Reader.ReadBytes(4));

                if (fileVersion != "V1.0")
                {
                    throw new InvalidDataException($"ERF version '{fileVersion}' is unsupported.");
                }

                ERFType? erfType = null;
                foreach (ERFType type in Enum.GetValues<ERFType>())
                {
                    if (ERFTypeExtensions.ToFourCC(type) == fileType)
                    {
                        erfType = type;
                        break;
                    }
                }

                if (erfType is null)
                {
                    throw new InvalidDataException($"Not a valid ERF file: '{fileType}'");
                }

                _erf = new ERF(erfType.Value);

                Reader.SeekRelative(8); // Skip 8 bytes
                uint entryCount = Reader.ReadUInt32();
                Reader.SeekRelative(4); // Skip 4 bytes
                uint offsetToKeys = Reader.ReadUInt32();
                uint offsetToResources = Reader.ReadUInt32();
                Reader.SeekRelative(8); // Skip 8 bytes
                uint descriptionStrref = Reader.ReadUInt32();

                if (descriptionStrref == 0 && fileType == ERFTypeExtensions.ToFourCC(ERFType.MOD))
                {
                    _erf.IsSaveErf = true;
                }

                var resrefs = new List<string>();
                var resids = new List<uint>();
                var restypes = new List<ushort>();

                Reader.Seek((int)offsetToKeys);
                for (uint i = 0; i < entryCount; i++)
                {
                    string resrefStr = Encoding.ASCII.GetString(Reader.ReadBytes(16)).TrimEnd('\0');
                    resrefs.Add(resrefStr.ToLowerInvariant());
                    resids.Add(Reader.ReadUInt32());
                    restypes.Add(Reader.ReadUInt16());
                    Reader.SeekRelative(2); // Skip 2 bytes
                }

                var resoffsets = new List<uint>();
                var ressizes = new List<uint>();

                Reader.Seek((int)offsetToResources);
                for (uint i = 0; i < entryCount; i++)
                {
                    resoffsets.Add(Reader.ReadUInt32());
                    ressizes.Add(Reader.ReadUInt32());
                }

                for (int i = 0; i < entryCount; i++)
                {
                    Reader.Seek((int)resoffsets[i]);
                    byte[] resdata = Reader.ReadBytes((int)ressizes[i]);
                    ResourceType resType = ResourceType.FromId(restypes[i]);
                    _erf.SetData(resrefs[i], resType, resdata);
                }

                return _erf;
            }
            catch (EndOfStreamException ex)
            {
                throw new InvalidDataException("Corrupted or truncated ERF file.", ex);
            }
        }
    }
}

