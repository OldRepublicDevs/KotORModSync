using System;
using System.Numerics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using KOTORModSync;
using KOTORModSync.Common;

namespace KOTORModSync.Formats.GFF
{

    /// <summary>
    /// Writes GFF (General File Format) binary data.
    /// 1:1 port of Python GFFBinaryWriter from pykotor/resource/formats/gff/io_gff.py
    /// </summary>
    public class GFFBinaryWriter
    {
        private readonly GFF _gff;
        private readonly KOTORModSync.Common.RawBinaryWriter _structWriter;
        private readonly KOTORModSync.Common.RawBinaryWriter _fieldWriter;
        private readonly KOTORModSync.Common.RawBinaryWriter _fieldDataWriter;
        private readonly KOTORModSync.Common.RawBinaryWriter _fieldIndicesWriter;
        private readonly KOTORModSync.Common.RawBinaryWriter _listIndicesWriter;
        private readonly List<string> _labels = new List<string>();
        private int _structCount = 0;
        private int _fieldCount = 0;

        // Complex fields that are stored in the field data section
        private static readonly HashSet<GFFFieldType> _complexFields = new HashSet<GFFFieldType>
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

        public GFFBinaryWriter(GFF gff)
        {
            _gff = gff;
            _structWriter = KOTORModSync.Common.RawBinaryWriter.ToByteArray();
            _fieldWriter = KOTORModSync.Common.RawBinaryWriter.ToByteArray();
            _fieldDataWriter = KOTORModSync.Common.RawBinaryWriter.ToByteArray();
            _fieldIndicesWriter = KOTORModSync.Common.RawBinaryWriter.ToByteArray();
            _listIndicesWriter = KOTORModSync.Common.RawBinaryWriter.ToByteArray();
        }

        public byte[] Write()
        {
            // Build all sections
            BuildStruct(_gff.Root);

            // Calculate offsets
            int structOffset = 56; // Header size
            int structCount = _structWriter.Size() / 12;
            int fieldOffset = structOffset + _structWriter.Size();
            int fieldCount = _fieldWriter.Size() / 12;
            int labelOffset = fieldOffset + _fieldWriter.Size();
            int labelCount = _labels.Count;
            int fieldDataOffset = labelOffset + _labels.Count * 16;
            int fieldDataCount = _fieldDataWriter.Size();
            int fieldIndicesOffset = fieldDataOffset + _fieldDataWriter.Size();
            int fieldIndicesCount = _fieldIndicesWriter.Size();
            int listIndicesOffset = fieldIndicesOffset + _fieldIndicesWriter.Size();
            int listIndicesCount = _listIndicesWriter.Size();

            // Write the file using RawBinaryWriter for consistency
            using (var fileWriter = KOTORModSync.Common.RawBinaryWriter.ToByteArray())
            {
                // Write header
                fileWriter.WriteBytes(Encoding.ASCII.GetBytes(_gff.Content.ToFourCC()));
                fileWriter.WriteBytes(Encoding.ASCII.GetBytes("V3.2"));
                fileWriter.WriteUInt32((uint)structOffset);
                fileWriter.WriteUInt32((uint)structCount);
                fileWriter.WriteUInt32((uint)fieldOffset);
                fileWriter.WriteUInt32((uint)fieldCount);
                fileWriter.WriteUInt32((uint)labelOffset);
                fileWriter.WriteUInt32((uint)labelCount);
                fileWriter.WriteUInt32((uint)fieldDataOffset);
                fileWriter.WriteUInt32((uint)fieldDataCount);
                fileWriter.WriteUInt32((uint)fieldIndicesOffset);
                fileWriter.WriteUInt32((uint)fieldIndicesCount);
                fileWriter.WriteUInt32((uint)listIndicesOffset);
                fileWriter.WriteUInt32((uint)listIndicesCount);

                // Write all sections
                byte[] structData = _structWriter.Data();
                fileWriter.WriteBytes(structData);

                byte[] fieldData = _fieldWriter.Data();
                fileWriter.WriteBytes(fieldData);

                // Write labels (16 bytes each)
                foreach (string label in _labels)
                {
                    byte[] labelBytes = Encoding.ASCII.GetBytes(label.PadRight(16, '\0'));
                    fileWriter.WriteBytes(labelBytes);
                }

                fileWriter.WriteBytes(_fieldDataWriter.Data());
                fileWriter.WriteBytes(_fieldIndicesWriter.Data());
                fileWriter.WriteBytes(_listIndicesWriter.Data());

                return fileWriter.Data();
            }
        }

        private void BuildStruct(GFFStruct gffStruct)
        {
            _structCount++;
            int structId = gffStruct.StructId;
            int fieldCount = gffStruct.Count;

            // Write struct ID (handle -1 as 0xFFFFFFFF)
            if (structId == -1)
            {
                _structWriter.WriteUInt32(0xFFFFFFFFu);
            }
            else
            {
                _structWriter.WriteUInt32((uint)structId);
            }

            if (fieldCount == 0)
            {
                _structWriter.WriteUInt32(0xFFFFFFFFu);
                _structWriter.WriteUInt32(0u);
            }
            else if (fieldCount == 1)
            {
                // When fieldCount == 1, store the field index directly (before incrementing)
                // The field index is the current _fieldCount value (0-based)
                uint fieldIndex = (uint)_fieldCount;
                _structWriter.WriteUInt32(fieldIndex);
                _structWriter.WriteUInt32((uint)fieldCount);

                foreach ((string label, GFFFieldType fieldType, object value) in gffStruct)
                {
                    BuildField(label, value, fieldType);
                }
            }
            else if (fieldCount > 1)
            {
                WriteLargeStruct(fieldCount, gffStruct);
            }
        }

        private void WriteLargeStruct(int fieldCount, GFFStruct gffStruct)
        {
            _structWriter.WriteUInt32((uint)_fieldIndicesWriter.Size());
            _structWriter.WriteUInt32((uint)fieldCount);

            int pos = _fieldIndicesWriter.Position();
            _fieldIndicesWriter.Seek(_fieldIndicesWriter.Size());

            // Reserve space for field indices
            for (int i = 0; i < fieldCount; i++)
            {
                _fieldIndicesWriter.WriteUInt32(0u);
            }

            int index = 0;
            foreach ((string label, GFFFieldType fieldType, object value) in gffStruct)
            {
                int currentPos = _fieldIndicesWriter.Position();
                _fieldIndicesWriter.Seek(pos + index * 4);
                _fieldIndicesWriter.WriteUInt32((uint)_fieldCount);
                _fieldIndicesWriter.Seek(currentPos);
                BuildField(label, value, fieldType);
                index++;
            }
        }

        private void BuildList(GFFList gffList)
        {
            int pos = _listIndicesWriter.Position();
            _listIndicesWriter.Seek(_listIndicesWriter.Size());

            _listIndicesWriter.WriteUInt32((uint)gffList.Count);
            int indexStartPos = _listIndicesWriter.Position();

            // Reserve space for struct indices
            for (int i = 0; i < gffList.Count; i++)
            {
                _listIndicesWriter.WriteUInt32(0u);
            }

            int index = 0;
            foreach (GFFStruct gffStruct in gffList)
            {
                int currentPos = _listIndicesWriter.Position();
                _listIndicesWriter.Seek(indexStartPos + index * 4);
                _listIndicesWriter.WriteUInt32((uint)_structCount);
                _listIndicesWriter.Seek(currentPos);
                BuildStruct(gffStruct);
                index++;
            }
        }

        private void BuildField(string label, object value, GFFFieldType fieldType)
        {
            _fieldCount++;
            uint fieldTypeId = (uint)fieldType;
            uint labelIndex = (uint)GetLabelIndex(label);

            _fieldWriter.WriteUInt32(fieldTypeId);
            _fieldWriter.WriteUInt32(labelIndex);

            if (_complexFields.Contains(fieldType))
            {
                _fieldWriter.WriteUInt32((uint)_fieldDataWriter.Size());
                _fieldDataWriter.Seek(_fieldDataWriter.Size());

                switch (fieldType)
                {
                    case GFFFieldType.UInt64:
                        _fieldDataWriter.WriteUInt64(Convert.ToUInt64(value));
                        break;
                    case GFFFieldType.Int64:
                        _fieldDataWriter.WriteInt64(Convert.ToInt64(value));
                        break;
                    case GFFFieldType.Double:
                        _fieldDataWriter.WriteDouble(Convert.ToDouble(value));
                        break;
                    case GFFFieldType.String:
                        string str = value.ToString() ?? "";
                        byte[] strBytes = Encoding.ASCII.GetBytes(str);
                        _fieldDataWriter.WriteUInt32((uint)strBytes.Length);
                        _fieldDataWriter.WriteBytes(strBytes);
                        break;
                    case GFFFieldType.ResRef:
                        string resrefStr = value.ToString() ?? "";
                        byte[] resrefBytes = Encoding.ASCII.GetBytes(resrefStr);
                        _fieldDataWriter.WriteUInt8((byte)resrefBytes.Length);
                        _fieldDataWriter.WriteBytes(resrefBytes);
                        break;
                    case GFFFieldType.LocalizedString:
                        _fieldDataWriter.WriteLocalizedString((LocalizedString)value);
                        break;
                    case GFFFieldType.Binary:
                        byte[] binaryData = (byte[])value;
                        _fieldDataWriter.WriteUInt32((uint)binaryData.Length);
                        _fieldDataWriter.WriteBytes(binaryData);
                        break;
                    case GFFFieldType.Vector4:
                        _fieldDataWriter.WriteVector4((Vector4)value);
                        break;
                    case GFFFieldType.Vector3:
                        _fieldDataWriter.WriteVector3((Vector3)value);
                        break;
                }
            }
            else if (fieldType == GFFFieldType.Struct)
            {
                _fieldWriter.WriteUInt32((uint)_structCount);
                BuildStruct((GFFStruct)value);
            }
            else if (fieldType == GFFFieldType.List)
            {
                _fieldWriter.WriteUInt32((uint)_listIndicesWriter.Size());
                BuildList((GFFList)value);
            }
            else
            {
                // Simple types (stored inline as 4-byte values in the field entry)
                // Matching PyKotor implementation: writer writes 4-byte values for all simple types
                switch (fieldType)
                {
                    case GFFFieldType.UInt8:
                        uint uint8Val = Convert.ToUInt32(value);
                        _fieldWriter.WriteUInt32(uint8Val == 0xFFFFFFFFu ? 0xFFFFFFFFu : uint8Val);
                        break;
                    case GFFFieldType.Int8:
                        _fieldWriter.WriteInt32(Convert.ToInt32(value));
                        break;
                    case GFFFieldType.UInt16:
                        uint uint16Val = Convert.ToUInt32(value);
                        _fieldWriter.WriteUInt32(uint16Val == 0xFFFFFFFFu ? 0xFFFFFFFFu : uint16Val);
                        break;
                    case GFFFieldType.Int16:
                        _fieldWriter.WriteInt32(Convert.ToInt32(value));
                        break;
                    case GFFFieldType.UInt32:
                        uint uint32Val = Convert.ToUInt32(value);
                        _fieldWriter.WriteUInt32(uint32Val == 0xFFFFFFFFu ? 0xFFFFFFFFu : uint32Val);
                        break;
                    case GFFFieldType.Int32:
                        _fieldWriter.WriteInt32(Convert.ToInt32(value));
                        break;
                    case GFFFieldType.Single:
                        _fieldWriter.WriteSingle(Convert.ToSingle(value));
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown field type '{fieldType}'");
                }
            }
        }

        private int GetLabelIndex(string label)
        {
            int index = _labels.IndexOf(label);
            if (index >= 0)
            {
                return index;
            }

            _labels.Add(label);
            return _labels.Count - 1;
        }
    }
}
