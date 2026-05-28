using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using KOTORModSync;
using KOTORModSync.Common;

namespace KOTORModSync.Formats.ERF
{

    public class ERFBinaryWriter
    {
        private readonly ERF _erf;

        public ERFBinaryWriter(ERF erf)
        {
            _erf = erf ?? throw new ArgumentNullException(nameof(erf));
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

                string fourCC = _erf.ErfType == ERFType.MOD ? "MOD " : "ERF ";
                writer.Write(Encoding.ASCII.GetBytes(fourCC));
                writer.Write(Encoding.ASCII.GetBytes("V1.0"));

                var resources = _erf.ToList();
                uint entryCount = (uint)resources.Count;

                // Determine offsets
                uint headerSize = 160;
                uint localizedStringSize = 0; // Not fully supported yet in ERF class
                uint offsetToLocalizedStrings = headerSize; // Immediately after header
                uint offsetToKeys = offsetToLocalizedStrings + localizedStringSize;
                uint keySize = 24; // 16 + 4 + 2 + 2
                uint keysTotalSize = entryCount * keySize;
                uint offsetToResourcesInfo = offsetToKeys + keysTotalSize;
                uint resourceInfoSize = 8; // 4 + 4
                uint resourceInfoTotalSize = entryCount * resourceInfoSize;
                uint offsetToResourceData = offsetToResourcesInfo + resourceInfoTotalSize;

                writer.Write(0); // LanguageCount
                writer.Write(localizedStringSize);
                writer.Write(entryCount);
                writer.Write(offsetToLocalizedStrings);
                writer.Write(offsetToKeys);
                writer.Write(offsetToResourcesInfo);
                writer.Write((uint)DateTime.Now.Year); // BuildYear
                writer.Write((uint)DateTime.Now.DayOfYear); // BuildDay
                writer.Write(0xFFFFFFFF); // DescriptionStrRef - not supported in ERF class yet

                // Padding to 160 bytes
                // Current position: 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4 = 44 bytes?
                // Header fields:
                // Type (4), Version (4), LangCount(4), LocStrSize(4), EntryCount(4), OffLocStr(4), OffKeys(4), OffRes(4), Year(4), Day(4), DescStrRef(4) = 11 * 4 = 44 bytes.
                // 160 - 44 = 116 bytes padding.
                writer.Write(new byte[116]);

                // Localized Strings (empty)

                // Keys
                uint currentId = 0;
                foreach (ERFResource res in resources)
                {
                    byte[] resRefBytes = Encoding.ASCII.GetBytes(res.ResRef.ToString());
                    byte[] paddedResRef = new byte[16];
                    Array.Copy(resRefBytes, paddedResRef, Math.Min(resRefBytes.Length, 16));

                    writer.Write(paddedResRef);
                    writer.Write(currentId++); // Resource ID
                    writer.Write((ushort)res.ResType.TypeId); // ResType
                    writer.Write((ushort)0); // Unused
                }

                // Resources Info
                uint currentOffset = offsetToResourceData;
                foreach (ERFResource res in resources)
                {
                    writer.Write(currentOffset);
                    writer.Write((uint)res.Data.Length);
                    currentOffset += (uint)res.Data.Length;
                }

                // Resource Data
                foreach (ERFResource res in resources)
                {
                    writer.Write(res.Data);
                }
            }
        }
    }
}
