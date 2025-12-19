// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/Decoder.java:20-496
// Original: public class Decoder
using System;
using static KOTORModSync.Formats.NCS.NCSDecomp.DecompilerLogger;
using System.IO;
using System.Numerics;
using System.Text;

using NumBigInteger = System.Numerics.BigInteger;
namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    public class Decoder
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/Decoder.java:21-56
        // Original: private static final int DECOCT_CPDOWNSP = 1; ... private static final int DECOCT_NOP = 45; private static final int DECOCT_T = 66;
        private const byte DECOCT_CPDOWNSP = 1;
        private const byte DECOCT_RSADD = 2;
        private const byte DECOCT_CPTOPSP = 3;
        private const byte DECOCT_CONST = 4;
        private const byte DECOCT_ACTION = 5;
        private const byte DECOCT_LOGANDII = 6;
        private const byte DECOCT_LOGORII = 7;
        private const byte DECOCT_INCORII = 8;
        private const byte DECOCT_EXCORII = 9;
        private const byte DECOCT_BOOLANDII = 10;
        private const byte DECOCT_EQUAL = 11;
        private const byte DECOCT_NEQUAL = 12;
        private const byte DECOCT_GEQ = 13;
        private const byte DECOCT_GT = 14;
        private const byte DECOCT_LT = 15;
        private const byte DECOCT_LEQ = 16;
        private const byte DECOCT_SHLEFTII = 17;
        private const byte DECOCT_SHRIGHTII = 18;
        private const byte DECOCT_USHRIGHTII = 19;
        private const byte DECOCT_ADD = 20;
        private const byte DECOCT_SUB = 21;
        private const byte DECOCT_MUL = 22;
        private const byte DECOCT_DIV = 23;
        private const byte DECOCT_MOD = 24;
        private const byte DECOCT_NEG = 25;
        private const byte DECOCT_COMP = 26;
        private const byte DECOCT_MOVSP = 27;
        private const byte DECOCT_STATEALL = 28;
        private const byte DECOCT_JMP = 29;
        private const byte DECOCT_JSR = 30;
        private const byte DECOCT_JZ = 31;
        private const byte DECOCT_RETN = 32;
        private const byte DECOCT_DESTRUCT = 33;
        private const byte DECOCT_NOT = 34;
        private const byte DECOCT_DECISP = 35;
        private const byte DECOCT_INCISP = 36;
        private const byte DECOCT_JNZ = 37;
        private const byte DECOCT_CPDOWNBP = 38;
        private const byte DECOCT_CPTOPBP = 39;
        private const byte DECOCT_DECIBP = 40;
        private const byte DECOCT_INCIBP = 41;
        private const byte DECOCT_SAVEBP = 42;
        private const byte DECOCT_RESTOREBP = 43;
        private const byte DECOCT_STORE_STATE = 44;
        private const byte DECOCT_NOP = 45;
        private const byte DECOCT_T = 66;
        private System.IO.BinaryReader @in;
        private ActionsData actions;
        private int pos;

        public Decoder(System.IO.BinaryReader @in, ActionsData actions)
        {
            this.@in = @in;
            this.actions = actions;
            this.pos = 0;
        }

        public virtual string Decode()
        {
            this.ReadHeader();
            return this.ReadCommands();
        }

        private string ReadCommands()
        {
            StringBuilder strbuffer = new StringBuilder();
            while (this.ReadCommand(strbuffer) != -1)
            {
            }

            return strbuffer.ToString();
        }

        private int ReadCommand(StringBuilder strbuffer)
        {
            byte[] buffer = new byte[1];
            int commandpos = this.pos;
            int status = this.@in.Read(buffer, 0, 1);
            this.pos++;
            if (status <= 0)
            {
                return -1;
            }

            strbuffer.Append(this.GetCommand(buffer[0]));
            strbuffer.Append(" " + commandpos);

            try
            {
                switch (buffer[0])
                {
                    case DECOCT_CPDOWNSP:
                    case DECOCT_CPTOPSP:
                    case DECOCT_CPDOWNBP:
                    case DECOCT_CPTOPBP:
                        strbuffer.Append(" " + this.ReadByteAsString());
                        strbuffer.Append(" " + this.ReadSignedInt());
                        strbuffer.Append(" " + this.ReadUnsignedShort());
                        break;
                    case DECOCT_RSADD:
                    case DECOCT_LOGANDII:
                    case DECOCT_LOGORII:
                    case DECOCT_INCORII:
                    case DECOCT_EXCORII:
                    case DECOCT_BOOLANDII:
                    case DECOCT_GEQ:
                    case DECOCT_GT:
                    case DECOCT_LT:
                    case DECOCT_LEQ:
                    case DECOCT_SHLEFTII:
                    case DECOCT_SHRIGHTII:
                    case DECOCT_USHRIGHTII:
                    case DECOCT_ADD:
                    case DECOCT_SUB:
                    case DECOCT_MUL:
                    case DECOCT_DIV:
                    case DECOCT_MOD:
                    case DECOCT_NEG:
                    case DECOCT_COMP:
                    case DECOCT_RETN:
                    case DECOCT_NOT:
                    case DECOCT_SAVEBP:
                    case DECOCT_RESTOREBP:
                    case DECOCT_NOP:
                        strbuffer.Append(" " + this.ReadByteAsString());
                        break;
                    case DECOCT_CONST:
                        {
                            byte bx = this.ReadByte();
                            strbuffer.Append(" " + bx.ToString());
                            switch (bx)
                            {
                                case 3:
                                    strbuffer.Append(" " + this.ReadUnsignedInt());
                                    break;
                                case 4:
                                    strbuffer.Append(" " + this.ReadFloat());
                                    break;
                                case 5:
                                    strbuffer.Append(" " + this.ReadString());
                                    break;
                                case 6:
                                    strbuffer.Append(" " + this.ReadSignedInt());
                                    break;
                                default:
                                    throw new Exception("Unknown or unexpected constant type: " + bx.ToString());
                            }

                            break;
                        }
                    case DECOCT_ACTION:
                        strbuffer.Append(" " + this.ReadByteAsString());
                        strbuffer.Append(" " + this.ReadUnsignedShort());
                        strbuffer.Append(" " + this.ReadByteAsString());
                        break;
                    case DECOCT_EQUAL:
                    case DECOCT_NEQUAL:
                        {
                            byte b = this.ReadByte();
                            strbuffer.Append(" " + b.ToString());
                            if (b == 36)
                            {
                                strbuffer.Append(" " + this.ReadUnsignedShort());
                            }

                            break;
                        }
                    case DECOCT_MOVSP:
                    case DECOCT_JMP:
                    case DECOCT_JSR:
                    case DECOCT_JZ:
                    case DECOCT_DECISP:
                    case DECOCT_INCISP:
                    case DECOCT_JNZ:
                    case DECOCT_DECIBP:
                    case DECOCT_INCIBP:
                        strbuffer.Append(" " + this.ReadByteAsString());
                        strbuffer.Append(" " + this.ReadSignedInt());
                        break;
                    case DECOCT_STATEALL:
                        strbuffer.Append(" " + this.ReadByteAsString());
                        break;
                    case DECOCT_DESTRUCT:
                        strbuffer.Append(" " + this.ReadByteAsString());
                        strbuffer.Append(" " + this.ReadUnsignedShort());
                        strbuffer.Append(" " + this.ReadUnsignedShort());
                        strbuffer.Append(" " + this.ReadUnsignedShort());
                        break;
                    case DECOCT_STORE_STATE:
                        strbuffer.Append(" " + this.ReadByteAsString());
                        strbuffer.Append(" " + this.ReadSignedInt());
                        strbuffer.Append(" " + this.ReadSignedInt());
                        break;
                    case DECOCT_T:
                        strbuffer.Append(" " + this.ReadSignedInt());
                        break;
                    default:
                        throw new Exception("Unknown command type: " + buffer[0].ToString());
                }
            }
            catch (Exception)
            {
                Debug("error in .ncs file at pos " + this.pos.ToString());
                throw;
            }

            strbuffer.Append("; ");
            return 1;
        }

        private byte ReadByte()
        {
            byte[] buffer = new byte[1];
            int status = this.@in.Read(buffer, 0, 1);
            this.pos++;
            if (status <= 0)
            {
                throw new Exception("Unexpected EOF");
            }

            return buffer[0];
        }

        private string ReadByteAsString()
        {
            return this.ReadByte().ToString();
        }

        private string ReadUnsignedInt()
        {
            byte[] buffer = new byte[4];
            int status = this.@in.Read(buffer, 0, 4);
            if (status <= 0)
            {
                throw new Exception("Unexpected EOF");
            }

            this.pos += 4;
            long l = 0;
            for (int i = 0; i < 4; i++)
            {
                l |= (long)(buffer[i] & 0xFF);
                if (i < 3)
                {
                    l <<= 8;
                }
            }

            return l.ToString();
        }

        private string ReadSignedInt()
        {
            byte[] buffer = new byte[4];
            int status = this.@in.Read(buffer, 0, 4);
            if (status <= 0)
            {
                throw new Exception("Unexpected EOF");
            }

            this.pos += 4;
            NumBigInteger i = this.ToBigIntegerSigned(buffer);
            return i.ToString();
        }

        private string ReadUnsignedShort()
        {
            byte[] buffer = new byte[2];
            int status = this.@in.Read(buffer, 0, 2);
            if (status <= 0)
            {
                throw new Exception("Unexpected EOF");
            }

            this.pos += 2;
            NumBigInteger i = this.ToBigIntegerSigned(buffer);
            return i.ToString();
        }

        private string ReadFloat()
        {
            byte[] buffer = new byte[4];
            int status = this.@in.Read(buffer, 0, 4);
            if (status <= 0)
            {
                throw new Exception("Unexpected EOF");
            }

            this.pos += 4;
            NumBigInteger i = this.ToBigIntegerSigned(buffer);
            int bits = (int)i;
            float value = BitConverter.Int32BitsToSingle(bits);
            return value.ToString();
        }

        private string ReadString()
        {
            byte[] buffer = new byte[2];
            int status = this.@in.Read(buffer, 0, 2);
            if (status <= 0)
            {
                throw new Exception("Unexpected EOF");
            }

            this.pos += 2;
            NumBigInteger sizeInt = this.ToBigIntegerSigned(buffer);
            int size = (int)sizeInt;
            buffer = new byte[size];
            status = this.@in.Read(buffer, 0, size);
            if (status <= 0)
            {
                throw new Exception("Unexpected EOF");
            }

            this.pos += size;
            return "\"" + Encoding.UTF8.GetString(buffer) + "\"";
        }

        private void ReadHeader()
        {
            byte[] buffer = new byte[8];
            byte[] header = new byte[]
            {
                78,
                67,
                83,
                32,
                86,
                49,
                46,
                48
            };
            int status = this.@in.Read(buffer, 0, 8);
            this.pos += 8;
            if (status <= 0 || !this.SequenceEqual(buffer, header))
            {
                throw new Exception("The data file is not an NCS V1.0 file.");
            }
        }

        private string GetCommand(byte command)
        {
            switch (command)
            {
                case DECOCT_CPDOWNSP:
                    return "CPDOWNSP";
                case DECOCT_RSADD:
                    return "RSADD";
                case DECOCT_CPTOPSP:
                    return "CPTOPSP";
                case DECOCT_CONST:
                    return "CONST";
                case DECOCT_ACTION:
                    return "ACTION";
                case DECOCT_LOGANDII:
                    return "LOGANDII";
                case DECOCT_LOGORII:
                    return "LOGORII";
                case DECOCT_INCORII:
                    return "INCORII";
                case DECOCT_EXCORII:
                    return "EXCORII";
                case DECOCT_BOOLANDII:
                    return "BOOLANDII";
                case DECOCT_EQUAL:
                    return "EQUAL";
                case DECOCT_NEQUAL:
                    return "NEQUAL";
                case DECOCT_GEQ:
                    return "GEQ";
                case DECOCT_GT:
                    return "GT";
                case DECOCT_LT:
                    return "LT";
                case DECOCT_LEQ:
                    return "LEQ";
                case DECOCT_SHLEFTII:
                    return "SHLEFTII";
                case DECOCT_SHRIGHTII:
                    return "SHRIGHTII";
                case DECOCT_USHRIGHTII:
                    return "USHRIGHTII";
                case DECOCT_ADD:
                    return "ADD";
                case DECOCT_SUB:
                    return "SUB";
                case DECOCT_MUL:
                    return "MUL";
                case DECOCT_DIV:
                    return "DIV";
                case DECOCT_MOD:
                    return "MOD";
                case DECOCT_NEG:
                    return "NEG";
                case DECOCT_COMP:
                    return "COMP";
                case DECOCT_MOVSP:
                    return "MOVSP";
                case DECOCT_STATEALL:
                    return "STATEALL";
                case DECOCT_JMP:
                    return "JMP";
                case DECOCT_JSR:
                    return "JSR";
                case DECOCT_JZ:
                    return "JZ";
                case DECOCT_RETN:
                    return "RETN";
                case DECOCT_DESTRUCT:
                    return "DESTRUCT";
                case DECOCT_NOT:
                    return "NOT";
                case DECOCT_DECISP:
                    return "DECISP";
                case DECOCT_INCISP:
                    return "INCISP";
                case DECOCT_JNZ:
                    return "JNZ";
                case DECOCT_CPDOWNBP:
                    return "CPDOWNBP";
                case DECOCT_CPTOPBP:
                    return "CPTOPBP";
                case DECOCT_DECIBP:
                    return "DECIBP";
                case DECOCT_INCIBP:
                    return "INCIBP";
                case DECOCT_SAVEBP:
                    return "SAVEBP";
                case DECOCT_RESTOREBP:
                    return "RESTOREBP";
                case DECOCT_STORE_STATE:
                    return "STORE_STATE";
                case DECOCT_NOP:
                    return "NOP";
                case DECOCT_T:
                    return "T";
                default:
                    throw new Exception("Unknown command code: " + command.ToString());
            }
        }

        private NumBigInteger ToBigIntegerSigned(byte[] bigEndian)
        {
            int len = bigEndian.Length;
            byte[] little = new byte[len];
            for (int i = 0; i < len; i++)
            {
                little[i] = bigEndian[len - 1 - i];
            }

            bool negative = (bigEndian[0] & 0x80) != 0;
            if (negative)
            {
                byte[] extended = new byte[len + 1];
                for (int i = 0; i < little.Length; i++)
                {
                    extended[i] = little[i];
                }

                extended[extended.Length - 1] = 0xFF;
                return new NumBigInteger(extended);
            }

            return new NumBigInteger(little);
        }

        private bool SequenceEqual(byte[] left, byte[] right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
