using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace KOTORModSync.Formats.TwoDA
{

    /// <summary>
    /// Writes TwoDA binary data.
    /// 1:1 port of Python TwoDABinaryWriter from pykotor/resource/formats/twoda/io_twoda.py
    /// </summary>
    public class TwoDABinaryWriter
    {
        private readonly TwoDA _twoda;

        public TwoDABinaryWriter(TwoDA twoda)
        {
            _twoda = twoda;
        }

        public byte[] Write()
        {
            using (var ms = new MemoryStream())
            using (var writer = new System.IO.BinaryWriter(ms))
            {
                List<string> headers = _twoda.GetHeaders();

                // Write header
                writer.Write(Encoding.ASCII.GetBytes("2DA "));
                writer.Write(Encoding.ASCII.GetBytes("V2.b"));
                writer.Write(Encoding.ASCII.GetBytes("\n"));

                // Write column headers
                foreach (string header in headers)
                {
                    writer.Write(Encoding.ASCII.GetBytes(header + "\t"));
                }
                writer.Write((byte)0); // \0

                // Write row count
                writer.Write((uint)_twoda.GetHeight());

                // Write row labels
                foreach (string rowLabel in _twoda.GetLabels())
                {
                    writer.Write(Encoding.ASCII.GetBytes(rowLabel + "\t"));
                }

                // Build cell data and offsets
                var values = new List<string>();
                var valueOffsets = new List<int>();
                var cellOffsets = new List<int>();
                int dataSize = 0;

                foreach (TwoDARow row in _twoda)
                {
                    foreach (string header in _twoda.GetHeaders())
                    {
                        string value = row.GetString(header) + "\0";

                        if (!values.Contains(value))
                        {
                            int valueOffset = valueOffsets.Count > 0
                                ? values[values.Count - 1].Length + valueOffsets[valueOffsets.Count - 1]
                                : 0;
                            values.Add(value);
                            valueOffsets.Add(valueOffset);
                            dataSize += value.Length;
                        }

                        int cellOffset = valueOffsets[values.IndexOf(value)];
                        cellOffsets.Add(cellOffset);
                    }
                }

                // Write cell offsets
                foreach (int cellOffset in cellOffsets)
                {
                    writer.Write((ushort)cellOffset);
                }

                // Write data size
                writer.Write((ushort)dataSize);

                // Write cell values
                foreach (string value in values)
                {
                    writer.Write(Encoding.ASCII.GetBytes(value));
                }

                return ms.ToArray();
            }
        }
    }
}

