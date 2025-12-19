using System;
using System.IO;
using System.Text;
using KOTORModSync.Formats;

namespace KOTORModSync.Formats.SSF
{

    /// <summary>
    /// Reads SSF (Sound Set File) binary data.
    /// Matches Python SSFBinaryReader class.
    /// </summary>
    public class SSFBinaryReader : BinaryFormatReaderBase
    {
        public SSFBinaryReader(byte[] data) : base(data, Encoding.ASCII)
        {
        }

        public SSFBinaryReader(string filepath) : base(filepath, Encoding.ASCII)
        {
        }

        public SSFBinaryReader(Stream source) : base(source, Encoding.ASCII)
        {
        }

        public SSF Load()
        {
            try
            {
                var ssf = new SSF();

                // Read header
                string fileType = Encoding.ASCII.GetString(Reader.ReadBytes(4));
                string fileVersion = Encoding.ASCII.GetString(Reader.ReadBytes(4));

                if (fileType != "SSF ")
                {
                    throw new InvalidDataException("Attempted to load an invalid SSF file.");
                }

                if (fileVersion != "V1.1")
                {
                    throw new InvalidDataException("The supplied SSF file version is not supported.");
                }

                uint soundsOffset = Reader.ReadUInt32();
                Reader.Seek((int)soundsOffset);

                // Read all 28 sound references in order
                ssf.SetData(SSFSound.BATTLE_CRY_1, ReadInt32MaxNeg1());
                ssf.SetData(SSFSound.BATTLE_CRY_2, ReadInt32MaxNeg1());
                ssf.SetData(SSFSound.BATTLE_CRY_3, ReadInt32MaxNeg1());
                ssf.SetData(SSFSound.BATTLE_CRY_4, ReadInt32MaxNeg1());
                ssf.SetData(SSFSound.BATTLE_CRY_5, ReadInt32MaxNeg1());
                ssf.SetData(SSFSound.BATTLE_CRY_6, ReadInt32MaxNeg1());
                ssf.SetData(SSFSound.SELECT_1, ReadInt32MaxNeg1());
                ssf.SetData(SSFSound.SELECT_2, ReadInt32MaxNeg1());
                ssf.SetData(SSFSound.SELECT_3, ReadInt32MaxNeg1());
                ssf.SetData(SSFSound.ATTACK_GRUNT_1, ReadInt32MaxNeg1());
                ssf.SetData(SSFSound.ATTACK_GRUNT_2, ReadInt32MaxNeg1());
                ssf.SetData(SSFSound.ATTACK_GRUNT_3, ReadInt32MaxNeg1());
                ssf.SetData(SSFSound.PAIN_GRUNT_1, ReadInt32MaxNeg1());
                ssf.SetData(SSFSound.PAIN_GRUNT_2, ReadInt32MaxNeg1());
                ssf.SetData(SSFSound.LOW_HEALTH, ReadInt32MaxNeg1());
                ssf.SetData(SSFSound.DEAD, ReadInt32MaxNeg1());
                ssf.SetData(SSFSound.CRITICAL_HIT, ReadInt32MaxNeg1());
                ssf.SetData(SSFSound.TARGET_IMMUNE, ReadInt32MaxNeg1());
                ssf.SetData(SSFSound.LAY_MINE, ReadInt32MaxNeg1());
                ssf.SetData(SSFSound.DISARM_MINE, ReadInt32MaxNeg1());
                ssf.SetData(SSFSound.BEGIN_STEALTH, ReadInt32MaxNeg1());
                ssf.SetData(SSFSound.BEGIN_SEARCH, ReadInt32MaxNeg1());
                ssf.SetData(SSFSound.BEGIN_UNLOCK, ReadInt32MaxNeg1());
                ssf.SetData(SSFSound.UNLOCK_FAILED, ReadInt32MaxNeg1());
                ssf.SetData(SSFSound.UNLOCK_SUCCESS, ReadInt32MaxNeg1());
                ssf.SetData(SSFSound.SEPARATED_FROM_PARTY, ReadInt32MaxNeg1());
                ssf.SetData(SSFSound.REJOINED_PARTY, ReadInt32MaxNeg1());
                ssf.SetData(SSFSound.POISONED, ReadInt32MaxNeg1());

                return ssf;
            }
            catch (EndOfStreamException)
            {
                throw new InvalidDataException("The supplied SSF file version is not supported.");
            }
        }

        /// <summary>
        /// Reads a UInt32 and converts 0xFFFFFFFF to -1 (matches Python max_neg1 behavior).
        /// </summary>
        private int ReadInt32MaxNeg1()
        {
            uint value = Reader.ReadUInt32();
            return value == 0xFFFFFFFF ? -1 : (int)value;
        }
    }
}

