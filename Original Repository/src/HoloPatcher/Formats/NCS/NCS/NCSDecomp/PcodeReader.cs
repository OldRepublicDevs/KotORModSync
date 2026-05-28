// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
// Pcode Reader for NCSDecomp
// Reads text-based pcode disassembly format and converts to binary NCS
// Based on xoreos-tools implementation for accurate argument parsing
// 
// Pcode format (from nwnnsscomp -d, matching xoreos-tools format):
//   offset opcode qualifier [args...]  INSTRUCTION_NAME [human_readable_args]
// Example:
//   00000008 42 00001D83              T 00001D83          (size marker - special format)
//   0000000D 02 03                    RSADDI              (no args)
//   0000000F 1E 00 00000008           JSR fn_00000017     (sint32 arg)
//   00000019 04 03 00000000           CONSTI 0            (int const)
//   00000321 04 05 str                CONSTS "MIN_RACE_GEAR"  (string const)
//   00000EEC 2C 01 10 0000006C 00000090 STORESTATE sta_00000EFC 108 144
// 
namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    public class PcodeReader
    {
        // Argument type sizes (matching xoreos-tools)
        private static readonly int ARG_UINT8 = 1; // 1 byte (2 hex digits)
        private static readonly int ARG_UINT16 = 2; // 2 bytes (4 hex digits)
        private static readonly int ARG_SINT16 = 2; // 2 bytes (4 hex digits)
        private static readonly int ARG_SINT32 = 4; // 4 bytes (8 hex digits)
        private static readonly int ARG_UINT32 = 4; // 4 bytes (8 hex digits)
        // Opcode argument definitions (matching xoreos-tools kOpcodeArguments table)
        // Each entry is an array of argument sizes in bytes
        private static readonly int[][] OPCODE_ARGS = new int[0x43][];
        static PcodeReader()
        {

            // Initialize all to empty
            for (int i = 0; i < OPCODE_ARGS.Length; i++)
            {
                OPCODE_ARGS[i] = new int[0];
            }


            // 0x01 CPDOWNSP: sint32, sint16
            OPCODE_ARGS[0x01] = new int[]
            {
                ARG_SINT32,
                ARG_SINT16
            };

            // 0x02 RSADD: no args (type is in qualifier)
            OPCODE_ARGS[0x02] = new int[0];

            // 0x03 CPTOPSP: sint32, sint16
            OPCODE_ARGS[0x03] = new int[]
            {
                ARG_SINT32,
                ARG_SINT16
            };

            // 0x04 CONST: variable type (handled specially)
            OPCODE_ARGS[0x04] = new int[]
            {
                -1
            }; // -1 means variable type

            // 0x05 ACTION: uint16, uint8
            OPCODE_ARGS[0x05] = new int[]
            {
                ARG_UINT16,
                ARG_UINT8
            };

            // 0x06-0x0B: no args
            // 0x0C EQ: no args (except struct/struct which has sint16 - handled specially)
            OPCODE_ARGS[0x0C] = new int[0];

            // 0x0D NEQ: no args (except struct/struct which has sint16 - handled specially)
            OPCODE_ARGS[0x0D] = new int[0];

            // 0x0E-0x1B: no args
            // 0x1B MOVSP: sint32
            OPCODE_ARGS[0x1B] = new int[]
            {
                ARG_SINT32
            };

            // 0x1C STORESTATEALL: no args
            // 0x1D JMP: sint32
            OPCODE_ARGS[0x1D] = new int[]
            {
                ARG_SINT32
            };

            // 0x1E JSR: sint32
            OPCODE_ARGS[0x1E] = new int[]
            {
                ARG_SINT32
            };

            // 0x1F JZ: sint32
            OPCODE_ARGS[0x1F] = new int[]
            {
                ARG_SINT32
            };

            // 0x20 RETN: no args
            // 0x21 DESTRUCT: sint16, sint16, sint16
            OPCODE_ARGS[0x21] = new int[]
            {
                ARG_SINT16,
                ARG_SINT16,
                ARG_SINT16
            };

            // 0x22 NOT: no args
            // 0x23 DECISP: sint32
            OPCODE_ARGS[0x23] = new int[]
            {
                ARG_SINT32
            };

            // 0x24 INCISP: sint32
            OPCODE_ARGS[0x24] = new int[]
            {
                ARG_SINT32
            };

            // 0x25 JNZ: sint32
            OPCODE_ARGS[0x25] = new int[]
            {
                ARG_SINT32
            };

            // 0x26 CPDOWNBP: sint32, sint16
            OPCODE_ARGS[0x26] = new int[]
            {
                ARG_SINT32,
                ARG_SINT16
            };

            // 0x27 CPTOPBP: sint32, sint16
            OPCODE_ARGS[0x27] = new int[]
            {
                ARG_SINT32,
                ARG_SINT16
            };

            // 0x28 DECBP: sint32
            OPCODE_ARGS[0x28] = new int[]
            {
                ARG_SINT32
            };

            // 0x29 INCBP: sint32
            OPCODE_ARGS[0x29] = new int[]
            {
                ARG_SINT32
            };

            // 0x2A SAVEBP: no args
            // 0x2B RESTOREBP: no args
            // 0x2C STORESTATE: special - uses qualifier as first arg (uint8), then uint32, uint32
            OPCODE_ARGS[0x2C] = new int[]
            {
                ARG_UINT8,
                ARG_UINT32,
                ARG_UINT32
            };

            // 0x2D NOP: no args
            // 0x30 WRITEARRAY: sint32, sint16
            OPCODE_ARGS[0x30] = new int[]
            {
                ARG_SINT32,
                ARG_SINT16
            };

            // 0x32 READARRAY: sint32, sint16
            OPCODE_ARGS[0x32] = new int[]
            {
                ARG_SINT32,
                ARG_SINT16
            };

            // 0x37 GETREF: sint32, sint16
            OPCODE_ARGS[0x37] = new int[]
            {
                ARG_SINT32,
                ARG_SINT16
            };

            // 0x39 GETREFARRAY: sint32, sint16
            OPCODE_ARGS[0x39] = new int[]
            {
                ARG_SINT32,
                ARG_SINT16
            }; // 0x42 SCRIPTSIZE: no args (handled specially)
        }

        // Main pattern: offset opcode rest_of_line
        private static readonly Regex LINE_START = new Regex("^\\s*([0-9A-Fa-f]{8})\\s+([0-9A-Fa-f]{2})\\s+(.*)$");
        public static byte[] ConvertPcodeToBinary(Stream pcodeStream)
        {
            StreamReader reader = new StreamReader(pcodeStream, System.Text.Encoding.GetEncoding("ISO-8859-1"));
            List<Instruction> instructions = new List<Instruction>();
            string line;
            int maxOffset = 13; // Minimum: header + size marker
            int fileSize = 0; // From size marker if present
            while ((line = reader.ReadLine()) != null)
            {

                // Skip comment lines, label lines, and empty lines
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("//") || trimmed.StartsWith(";") || trimmed.StartsWith("_") || System.Text.RegularExpressions.Regex.IsMatch(trimmed, "^[a-zA-Z_][a-zA-Z0-9_]*:.*$") || trimmed.StartsWith("----"))
                {
                    continue;
                }

                Match m = LINE_START.Match(line);
                if (!m.Success)
                {
                    continue; // Not a valid instruction line
                }

                int offset = Convert.ToInt32(m.Groups[1].Value, 16);
                int opcode = Convert.ToInt32(m.Groups[2].Value, 16);
                string rest = m.Groups[3].Value.Trim();

                // Handle size marker line specially (opcode 0x42 at offset 8)
                // Format: "00001D83              T 00001D83"
                // The "00001D83" after opcode is the 4-byte file size, not qualifier+args
                if (offset == 8 && opcode == 0x42)
                {

                    // Parse file size from the rest (first 8 hex chars)
                    string fileSizeHex = System.Text.RegularExpressions.Regex.Replace(rest, "[^0-9A-Fa-f].*$", "");
                    if (fileSizeHex.Length >= 8)
                    {
                        fileSize = Convert.ToInt32(fileSizeHex.Substring(0, 8), 16);
                    }


                    // Don't add size marker as an instruction - we handle it separately
                    continue;
                }


                // Parse qualifier (2 hex chars)
                if (rest.Length < 2)
                {
                    continue; // Malformed line
                }

                int qualifier;
                string argsAndRest;

                // Check if rest starts with 2 hex chars
                if (rest.Length >= 2 && System.Text.RegularExpressions.Regex.IsMatch(rest.Substring(0, 2), "[0-9A-Fa-f]{2}"))
                {
                    qualifier = Convert.ToInt32(rest.Substring(0, 2), 16);
                    argsAndRest = rest.Length > 2 ? rest.Substring(2).Trim() : "";
                }
                else
                {
                    continue; // Can't parse qualifier
                }


                // Parse args based on opcode definition (matching xoreos-tools)
                byte[] args = ParseArgsFromRest(argsAndRest, opcode, qualifier);
                instructions.Add(new Instruction(offset, opcode, qualifier, args));
                int instructionEnd = offset + 2 + (args != null ? args.Length : 0);
                maxOffset = Math.Max(maxOffset, instructionEnd);
            }


            // Build binary NCS file
            MemoryStream @out = new MemoryStream();

            // Write header: "NCS V1.0" (8 bytes)
            byte[] header = new byte[] { 0x4E, 0x43, 0x53, 0x20, 0x56, 0x31, 0x2E, 0x30 };
            @out.Write(header, 0, header.Length);

            // Calculate instruction data size
            int instructionStartOffset = 8; // Decoder starts reading from offset 8
            int dataSize = maxOffset - instructionStartOffset;
            if (dataSize < 5)
            {
                dataSize = 5; // Minimum: size marker (1 byte) + file size (4 bytes)
            }

            byte[] instructionData = new byte[dataSize];

            // Write size marker (0x42) at position 0 (file offset 8)
            instructionData[0] = 0x42;

            // Positions 1-4 will be updated with actual file size after we build the full array
            // Write each instruction at its correct offset
            foreach (Instruction inst in instructions)
            {
                if (inst.offset >= instructionStartOffset)
                {
                    int pos = inst.offset - instructionStartOffset;
                    if (pos >= 0 && pos < instructionData.Length)
                    {
                        instructionData[pos] = (byte)inst.opcode;
                        if (pos + 1 < instructionData.Length)
                        {
                            instructionData[pos + 1] = (byte)inst.qualifier;
                        }

                        if (inst.args != null && inst.args.Length > 0)
                        {
                            for (int i = 0; i < inst.args.Length && pos + 2 + i < instructionData.Length; i++)
                            {
                                instructionData[pos + 2 + i] = inst.args[i];
                            }
                        }
                    }
                }
            }

            @out.Write(instructionData, 0, instructionData.Length);

            // Build result and update file size in header
            byte[] result = @out.ToArray();
            int actualFileSize = result.Length; // This is now: 8 (header) + dataSize

            // Update file size at positions 9-12 (after "NCS V1.0" + size marker byte)
            result[9] = (byte)((actualFileSize >> 24) & 0xFF);
            result[10] = (byte)((actualFileSize >> 16) & 0xFF);
            result[11] = (byte)((actualFileSize >> 8) & 0xFF);
            result[12] = (byte)(actualFileSize & 0xFF);
            return result;
        }

        /// <summary>
        /// Parse instruction arguments based on opcode definition (matching xoreos-tools).
        /// Uses the OPCODE_ARGS table to determine argument sizes.
        /// </summary>
        private static byte[] ParseArgsFromRest(string rest, int opcode, int qualifier)
        {
            if (rest == null || rest.Length == 0)
            {

                // Check if opcode expects args
                if (opcode < OPCODE_ARGS.Length && OPCODE_ARGS[opcode].Length > 0)
                {

                    // Special case: STORESTATE uses qualifier as first arg
                    if (opcode == 0x2C)
                    {
                        return new byte[]
                        {
                            (byte)qualifier,
                            0,
                            0,
                            0,
                            0,
                            0,
                            0,
                            0,
                            0
                        }; // uint8 + 2x uint32
                    }

                    return new byte[0]; // Missing args, return empty
                }

                return new byte[0];
            }


            // Special case: CONST instruction (opcode 0x04)
            if (opcode == 0x04)
            {
                return ParseConstArgs(rest, qualifier);
            }


            // Special case: STORESTATE (opcode 0x2C)
            // Format: "01 10 0000006C 00000090 STORESTATE sta_00000EFC 108 144"
            // Args: qualifier (uint8), uint32, uint32
            if (opcode == 0x2C)
            {
                return ParseStoreStateArgs(rest, qualifier);
            }


            // Get argument sizes for this opcode
            if (opcode >= OPCODE_ARGS.Length || OPCODE_ARGS[opcode].Length == 0)
            {
                return new byte[0]; // No args expected
            }

            int[] argSizes = OPCODE_ARGS[opcode];
            return ParseFixedSizeArgs(rest, argSizes);
        }

        /// <summary>
        /// Parse CONST instruction arguments (variable type based on qualifier).
        /// </summary>
        private static byte[] ParseConstArgs(string rest, int qualifier)
        {

            // Qualifier determines the type:
            // 0x03 = int (4 bytes)
            // 0x04 = float (4 bytes)
            // 0x05 = string (2-byte length + string bytes)
            // 0x06 = object (4 bytes)
            if (qualifier == 0x05)
            {

                // String constant: extract from quoted string
                return ParseStringConstant(rest);
            }


            // For int, float, object: parse 4-byte hex value
            // Format: "00000000" or "42C80000" or "FFFFFFFF"
            string hexValue = System.Text.RegularExpressions.Regex.Replace(rest, "[^0-9A-Fa-f].*$", "");
            if (hexValue.Length >= 8)
            {
                hexValue = hexValue.Substring(0, 8);
                byte[] result = new byte[4];

                // Use Long.parseLong to handle values like FFFFFFFF
                long value = Long.ParseLong(hexValue, 16);
                result[0] = (byte)((value >> 24) & 0xFF);
                result[1] = (byte)((value >> 16) & 0xFF);
                result[2] = (byte)((value >> 8) & 0xFF);
                result[3] = (byte)(value & 0xFF);
                return result;
            }

            return new byte[4]; // Default: 4 zero bytes
        }

        /// <summary>
        /// Parse STORESTATE arguments.
        /// Format: "01 10 0000006C 00000090 STORESTATE sta_00000EFC 108 144"
        /// According to xoreos-tools parseOpcodeStore:
        ///   args[0] = type byte (uint8) = qualifier
        ///   args[1] = uint32
        ///   args[2] = uint32
        /// In pcode, we see: qualifier "01", then hex values for the args
        /// The "10" might be a formatting artifact or the first arg value
        /// </summary>
        private static byte[] ParseStoreStateArgs(string rest, int qualifier)
        {

            // Extract hex values from rest
            String[] parts = rest.Split("\\s+");
            IList<string> hexParts = new List<string>();
            foreach (string part in parts)
            {

                // Stop at instruction name
                if (System.Text.RegularExpressions.Regex.IsMatch(part, "^[A-Z_][A-Z0-9_]*$"))
                {
                    break;
                }


                // Stop at references
                if (System.Text.RegularExpressions.Regex.IsMatch(part, "^(fn|off|loc|sta|sub)_[0-9A-Fa-f]+$"))
                {
                    break;
                }


                // Collect hex values
                if (System.Text.RegularExpressions.Regex.IsMatch(part, "^[0-9A-Fa-f]+$"))
                {
                    hexParts.Add(part);
                }
            }


            // STORESTATE: args[0] = type (uint8), args[1] = uint32, args[2] = uint32
            // args[0] is the qualifier byte itself
            byte[] result = new byte[1 + 4 + 4]; // uint8 + uint32 + uint32

            // First arg: type byte (qualifier)
            result[0] = (byte)qualifier;

            // Second arg: uint32 (first 8-hex-digit value)
            int hexIndex = 0;
            if (hexParts.Count > hexIndex)
            {

                // Skip 2-digit values, look for 8-digit (uint32)
                while (hexIndex < hexParts.Count && hexParts[hexIndex].Length < 8)
                {
                    hexIndex++;
                }

                if (hexIndex < hexParts.Count)
                {
                    string secondArg = hexParts[hexIndex];
                    if (secondArg.Length >= 8)
                    {
                        long value = Long.ParseLong(secondArg.Substring(0, 8), 16);
                        result[1] = (byte)((value >> 24) & 0xFF);
                        result[2] = (byte)((value >> 16) & 0xFF);
                        result[3] = (byte)((value >> 8) & 0xFF);
                        result[4] = (byte)(value & 0xFF);
                    }

                    hexIndex++;
                }
            }


            // Third arg: uint32 (second 8-hex-digit value)
            if (hexIndex < hexParts.Count)
            {
                string thirdArg = hexParts[hexIndex];
                if (thirdArg.Length >= 8)
                {
                    long value = Long.ParseLong(thirdArg.Substring(0, 8), 16);
                    result[5] = (byte)((value >> 24) & 0xFF);
                    result[6] = (byte)((value >> 16) & 0xFF);
                    result[7] = (byte)((value >> 8) & 0xFF);
                    result[8] = (byte)(value & 0xFF);
                }
            }

            return result;
        }

        /// <summary>
        /// Parse fixed-size arguments based on opcode definition.
        /// Handles signed values correctly by using Long.parseLong for large hex values.
        /// </summary>
        private static byte[] ParseFixedSizeArgs(string rest, int[] argSizes)
        {

            // Extract hex values from rest
            String[] parts = rest.Split("\\s+");
            IList<string> hexParts = new List<string>();
            foreach (string part in parts)
            {

                // Stop at instruction name (uppercase words)
                if (System.Text.RegularExpressions.Regex.IsMatch(part, "^[A-Z_][A-Z0-9_]*$"))
                {
                    break;
                }


                // Stop at "str" marker
                if (part.Equals("str"))
                {
                    break;
                }


                // Stop at function references (fn_XXXXXXXX, off_XXXXXXXX, loc_XXXXXXXX)
                if (System.Text.RegularExpressions.Regex.IsMatch(part, "^(fn|off|loc|sta|sub)_[0-9A-Fa-f]+$"))
                {
                    break;
                }


                // Collect hex values (must be at least 2 chars to avoid matching single-char noise)
                if (System.Text.RegularExpressions.Regex.IsMatch(part, "^[0-9A-Fa-f]{2,}$"))
                {
                    hexParts.Add(part);
                }
            }


            // Calculate total size
            int totalSize = 0;
            foreach (int size in argSizes)
            {
                totalSize += size;
            }

            byte[] result = new byte[totalSize];
            int resultPos = 0;
            int hexPartIndex = 0;
            for (int argIndex = 0; argIndex < argSizes.Length; argIndex++)
            {
                int argSize = argSizes[argIndex];
                int hexDigits = argSize * 2; // 2 hex digits per byte
                if (hexPartIndex < hexParts.Count)
                {
                    string hexValue = hexParts[hexPartIndex];

                    // Pad with leading zeros if needed
                    while (hexValue.Length < hexDigits)
                    {
                        hexValue = "0" + hexValue;
                    }


                    // Truncate if too long
                    if (hexValue.Length > hexDigits)
                    {
                        hexValue = hexValue.Substring(hexValue.Length - hexDigits);
                    }


                    // Parse as unsigned long to handle large hex values like FFFFFFF4
                    long value = Long.ParseLong(hexValue, 16);

                    // Write bytes (little-endian)
                    for (int i = 0; i < argSize; i++)
                    {
                        int shift = i * 8;
                        result[resultPos + i] = (byte)((value >> shift) & 0xFF);
                    }
                }

                resultPos += argSize;
                hexPartIndex++;
            }

            return result;
        }

        /// <summary>
        /// Parse a string constant from pcode format.
        /// Input format: "000D str           CONSTS \"MIN_RACE_GEAR\""
        /// Output: 2-byte length (big-endian) + string bytes
        /// </summary>
        private static byte[] ParseStringConstant(string rest)
        {

            // Extract the quoted string from the line
            int quoteStart = rest.IndexOf('"');
            int quoteEnd = rest.LastIndexOf('"');
            string stringValue;
            if (quoteStart >= 0 && quoteEnd > quoteStart)
            {
                stringValue = rest.Substring(quoteStart + 1, quoteEnd);

                // Handle escape sequences
                stringValue = stringValue.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t").Replace("\\\"", "\"").Replace("\\\\", "\\");
            }
            else
            {

                // No quoted string found - might be empty string ""
                stringValue = "";
            }


            // Build the binary representation:
            // 2-byte length (big-endian) + string bytes
            byte[] stringBytes;
            try
            {
                stringBytes = System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(stringValue);
            }
            catch (Exception)
            {
                stringBytes = System.Text.Encoding.UTF8.GetBytes(stringValue);
            }

            int length = stringBytes.Length;
            byte[] result = new byte[2 + length];

            // 2-byte length (big-endian)
            result[0] = (byte)((length >> 8) & 0xFF);
            result[1] = (byte)(length & 0xFF);

            // String bytes
            System.Array.Copy(stringBytes, 0, result, 2, stringBytes.Length);
            return result;
        }

        internal class Instruction
        {
            internal int offset;
            internal int opcode;
            internal int qualifier;
            internal byte[] args;
            internal Instruction(int offset, int opcode, int qualifier, byte[] args)
            {
                this.offset = offset;
                this.opcode = opcode;
                this.qualifier = qualifier;
                this.args = args;
            }
        }
    }
}




