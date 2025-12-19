using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using KOTORModSync.Common;

namespace KOTORModSync.Formats.RIM
{
    public class RIMBinaryWriter
    {
        private readonly RIM _rim;

        public RIMBinaryWriter(RIM rim)
        {
            _rim = rim ?? throw new ArgumentNullException(nameof(rim));
        }

        public byte[] Write()
        {
            using (var ms = new MemoryStream())
            {
                Write(ms);
                return ms.ToArray();
            }
        }

        public void Write(Stream stream)
        {
            using (var writer = new System.IO.BinaryWriter(stream, Encoding.ASCII, true))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIM "));
                writer.Write(Encoding.ASCII.GetBytes("V1.0"));

                List<RIMResource> resources = _rim.GetResources();
                uint entryCount = (uint)resources.Count;
                uint offsetToKeys = 120;

                writer.Write(0); // Reserved
                writer.Write(entryCount);
                writer.Write(offsetToKeys);

                // Padding to 120 bytes
                // Current pos: 4+4+4+4+4 = 20 bytes.
                // 120 - 20 = 100 bytes padding.
                writer.Write(new byte[100]);

                // Keys
                // ResRef(16), ResType(4), ResID(4), Offset(4), Size(4) = 32 bytes per entry.
                uint keySize = 32;
                uint keysTotalSize = entryCount * keySize;
                uint offsetToData = offsetToKeys + keysTotalSize;

                uint currentOffset = offsetToData;
                uint currentId = 0;

                foreach (RIMResource res in resources)
                {
                    byte[] resRefBytes = Encoding.ASCII.GetBytes(res.ResRef.ToString());
                    byte[] paddedResRef = new byte[16];
                    Array.Copy(resRefBytes, paddedResRef, Math.Min(resRefBytes.Length, 16));

                    writer.Write(paddedResRef);
                    writer.Write((uint)res.ResType.TypeId);
                    writer.Write(currentId++);
                    writer.Write(currentOffset);
                    writer.Write((uint)res.Data.Length);

                    currentOffset += (uint)res.Data.Length;
                }

                // Resource Data
                foreach (RIMResource res in resources)
                {
                    writer.Write(res.Data);
                }
            }
        }
    }
}
