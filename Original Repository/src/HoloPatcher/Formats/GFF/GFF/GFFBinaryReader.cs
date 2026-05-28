using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using KOTORModSync;
using KOTORModSync.Formats;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Formats.GFF
{

    /// <summary>
    /// Reads GFF (General File Format) binary data.
    /// 1:1 port of Python GFFBinaryReader from pykotor/resource/formats/gff/io_gff.py
    /// </summary>
    public class GFFBinaryReader : BinaryFormatReaderBase
    {
        [CanBeNull] private GFF _gff;
        private List<string> _labels = new List<string>();
        private int _fieldDataOffset;
        private int _fieldIndicesOffset;
        private int _listIndicesOffset;
        private int _structOffset;
        private int _fieldOffset;

        // Complex fields that are stored in the field data section
        private static readonly HashSet<GFFFieldType> _complexFields = new HashSet<GFFFieldType>()
    {
        GFFFieldType.UInt64,
        GFFFieldType.Int64,
        GFFFieldType.Double,
        GFFFieldType.String,
        GFFFieldType.ResRef,
        GFFFieldType.LocalizedString,
        GFFFieldType.Binary,
        GFFFieldType.Vector3,
        GFFFieldType.Vector4
    };

        public GFFBinaryReader(byte[] data) : base(data)
        {
        }

        public GFFBinaryReader(byte[] data, int offset, int size) : base(SliceData(data, offset, size))
        {
        }

        private static byte[] SliceData(byte[] data, int offset, int size)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            if (offset < 0 || offset > data.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            // size <= 0 means "read to end" to match existing call sites that pass 0 for null.
            int length = size > 0 ? Math.Min(size, data.Length - offset) : (data.Length - offset);
            if (offset == 0 && length == data.Length)
            {
                return data;
            }

            byte[] slice = new byte[length];
            Array.Copy(data, offset, slice, 0, length);
            return slice;
        }

        public GFFBinaryReader(string filepath) : base(filepath)
        {
        }

        public GFFBinaryReader(Stream source) : base(source)
        {
        }

        public GFF Load()
        {
            try
            {
                _gff = new GFF();

                Reader.Seek(0);

                // Read header
                string fileType = Encoding.ASCII.GetString(Reader.ReadBytes(4));
                string fileVersion = Encoding.ASCII.GetString(Reader.ReadBytes(4));

                // Validate content type
                if (!IsValidGFFContent(fileType))
                {
                    throw new InvalidDataException("Not a valid binary GFF file.");
                }

                if (fileVersion != "V3.2")
                {
                    throw new InvalidDataException("The GFF version of the file is unsupported.");
                }

                _gff.Content = GFFContentExtensions.FromFourCC(fileType);

                _structOffset = (int)Reader.ReadUInt32();
                Reader.ReadUInt32(); // struct count (unused during reading)
                _fieldOffset = (int)Reader.ReadUInt32();
                Reader.ReadUInt32(); // field count (unused)
                int labelOffset = (int)Reader.ReadUInt32();
                int labelCount = (int)Reader.ReadUInt32();
                _fieldDataOffset = (int)Reader.ReadUInt32();
                Reader.ReadUInt32(); // field data count (unused)
                _fieldIndicesOffset = (int)Reader.ReadUInt32();
                Reader.ReadUInt32(); // field indices count (unused)
                _listIndicesOffset = (int)Reader.ReadUInt32();
                Reader.ReadUInt32(); // list indices count (unused)

                // Read labels
                _labels = new List<string>();
                Reader.Seek(labelOffset);
                for (int i = 0; i < labelCount; i++)
                {
                    string label = Encoding.ASCII.GetString(Reader.ReadBytes(16)).TrimEnd('\0');
                    _labels.Add(label);
                }

                // Load root struct
                LoadStruct(_gff.Root, 0);

                return _gff;
            }
            catch (EndOfStreamException)
            {
                throw new InvalidDataException("Invalid GFF file format - unexpected end of file.");
            }
        }

        private static bool IsValidGFFContent(string fourCC)
        {
            // Check if fourCC matches any GFFContent enum value
            string trimmedFourCC = fourCC.Trim();
            return Enum.TryParse<GFFContent>(trimmedFourCC, ignoreCase: true, out _);
        }

        private void LoadStruct(GFFStruct gffStruct, int structIndex)
        {
            int structPosition = _structOffset + structIndex * 12;
            
            // Validate struct position is within bounds
            if (structPosition + 12 > Reader.Size)
            {
                throw new InvalidDataException($"GFF struct at index {structIndex} would exceed file boundaries (position {structPosition}, file size {Reader.Size})");
            }
            
            Reader.Seek(structPosition);

            int structId = Reader.ReadInt32();
            uint data = Reader.ReadUInt32();
            uint fieldCount = Reader.ReadUInt32();

            gffStruct.StructId = structId;

            if (fieldCount == 1)
            {
                // Debug: Log what we're about to load
                // System.Diagnostics.Debug.WriteLine($"LoadStruct: fieldCount=1, data={data}, fieldOffset={_fieldOffset}, seeking to {_fieldOffset + (int)data * 12}");
                LoadField(gffStruct, (int)data);
            }
            else if (fieldCount > 1)
            {
                int indicesPosition = _fieldIndicesOffset + (int)data;
                
                // Validate indices position is within bounds
                if (indicesPosition + (fieldCount * 4) > Reader.Size)
                {
                    throw new InvalidDataException($"GFF field indices would exceed file boundaries (position {indicesPosition}, count {fieldCount}, file size {Reader.Size})");
                }
                
                Reader.Seek(indicesPosition);
                var indices = new List<int>();
                for (int i = 0; i < fieldCount; i++)
                {
                    indices.Add((int)Reader.ReadUInt32());
                }

                foreach (int index in indices)
                {
                    LoadField(gffStruct, index);
                }
            }
        }

        private void LoadField(GFFStruct gffStruct, int fieldIndex)
        {
            int fieldPosition = _fieldOffset + fieldIndex * 12;
            // Validate field position is within bounds
            if (fieldPosition + 12 > Reader.Size)
            {
                throw new InvalidDataException($"GFF field at index {fieldIndex} would exceed file boundaries (position {fieldPosition}, file size {Reader.Size})");
            }
            
            Reader.Seek(fieldPosition);

            uint fieldTypeId = Reader.ReadUInt32();
            uint labelId = Reader.ReadUInt32();
            
            // Validate labelId is within bounds
            if (labelId >= _labels.Count)
            {
                throw new InvalidDataException($"GFF field at index {fieldIndex} has invalid label ID {labelId} (max {_labels.Count - 1})");
            }

            var fieldType = (GFFFieldType)fieldTypeId;
            string label = _labels[(int)labelId];

            if (_complexFields.Contains(fieldType))
            {
                uint offset = Reader.ReadUInt32(); // Relative to field data
                Reader.Seek(_fieldDataOffset + (int)offset);

                switch (fieldType)
                {
                    case GFFFieldType.UInt64:
                        gffStruct.SetUInt64(label, Reader.ReadUInt64());
                        break;
                    case GFFFieldType.Int64:
                        gffStruct.SetInt64(label, Reader.ReadInt64());
                        break;
                    case GFFFieldType.Double:
                        gffStruct.SetDouble(label, Reader.ReadDouble());
                        break;
                    case GFFFieldType.String:
                        uint stringLength = Reader.ReadUInt32();
                        string str = Encoding.ASCII.GetString(Reader.ReadBytes((int)stringLength)).TrimEnd('\0');
                        gffStruct.SetString(label, str);
                        break;
                    case GFFFieldType.ResRef:
                        byte resrefLength = Reader.ReadUInt8();
                        string resrefStr = Encoding.ASCII.GetString(Reader.ReadBytes(resrefLength)).Trim();
                        gffStruct.SetResRef(label, new ResRef(resrefStr));
                        break;
                    case GFFFieldType.LocalizedString:
                        gffStruct.SetLocString(label, Reader.ReadLocalizedString());
                        break;
                    case GFFFieldType.Binary:
                        uint binaryLength = Reader.ReadUInt32();
                        gffStruct.SetBinary(label, Reader.ReadBytes((int)binaryLength));
                        break;
                    case GFFFieldType.Vector3:
                        var v3 = Reader.ReadVector3();
                        gffStruct.SetVector3(label, new System.Numerics.Vector3(v3.X, v3.Y, v3.Z));
                        break;
                    case GFFFieldType.Vector4:
                        var v4 = Reader.ReadVector4();
                        gffStruct.SetVector4(label, new System.Numerics.Vector4(v4.X, v4.Y, v4.Z, v4.W));
                        break;
                }
            }
            else if (fieldType == GFFFieldType.Struct)
            {
                uint structIndex = Reader.ReadUInt32();
                var newStruct = new GFFStruct();
                LoadStruct(newStruct, (int)structIndex);
                gffStruct.SetStruct(label, newStruct);
            }
            else if (fieldType == GFFFieldType.List)
            {
                LoadList(gffStruct, label);
            }
            else
            {
                // Simple types (stored inline as 4-byte values in the field entry)
                // The writer writes all simple types as 4-byte values, so we read 4 bytes and extract
                // Matching PyKotor implementation: writer writes 4 bytes, reader should read 4 bytes
                // However, Python reader reads 1-2 bytes which may be a bug - we'll read 4 bytes to match writer
                switch (fieldType)
                {
                    case GFFFieldType.UInt8:
                        // Read 4 bytes, extract first byte
                        uint uint8Val = Reader.ReadUInt32();
                        gffStruct.SetUInt8(label, (byte)(uint8Val == 0xFFFFFFFFu ? 0xFF : (uint8Val & 0xFF)));
                        break;
                    case GFFFieldType.Int8:
                        int int8Val = Reader.ReadInt32();
                        gffStruct.SetInt8(label, (sbyte)(int8Val == -1 ? -1 : (int8Val & 0xFF)));
                        break;
                    case GFFFieldType.UInt16:
                        uint uint16Val = Reader.ReadUInt32();
                        gffStruct.SetUInt16(label, (ushort)(uint16Val == 0xFFFFFFFFu ? 0xFFFF : (uint16Val & 0xFFFF)));
                        break;
                    case GFFFieldType.Int16:
                        int int16Val = Reader.ReadInt32();
                        gffStruct.SetInt16(label, (short)(int16Val == -1 ? -1 : (int16Val & 0xFFFF)));
                        break;
                    case GFFFieldType.UInt32:
                        gffStruct.SetUInt32(label, Reader.ReadUInt32());
                        break;
                    case GFFFieldType.Int32:
                        gffStruct.SetInt32(label, Reader.ReadInt32());
                        break;
                    case GFFFieldType.Single:
                        gffStruct.SetSingle(label, Reader.ReadSingle());
                        break;
                }
            }
        }

        private void LoadList(GFFStruct gffStruct, string label)
        {
            uint offset = Reader.ReadUInt32(); // Relative to list indices
            Reader.Seek(_listIndicesOffset + (int)offset);

            var value = new GFFList();
            uint count = Reader.ReadUInt32();
            var listIndices = new List<int>();

            for (int i = 0; i < count; i++)
            {
                listIndices.Add((int)Reader.ReadUInt32());
            }

            foreach (int structIndex in listIndices)
            {
                value.Add(0);
                // Can be null if not found
                GFFStruct child = value.At(value.Count - 1);
                if (child != null)
                {
                    LoadStruct(child, structIndex);
                }
            }

            gffStruct.SetList(label, value);
        }
    }
}
