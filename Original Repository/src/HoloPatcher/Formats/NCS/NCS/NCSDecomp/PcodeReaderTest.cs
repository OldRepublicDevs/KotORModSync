using System;
using static KOTORModSync.Formats.NCS.NCSDecomp.DecompilerLogger;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    public class PcodeReaderTest
    {
        public static void Main(String[] args)
        {

            // Test parseFixedSizeArgs directly
            string testLine = "FFFFFFF8 0004      CPDOWNSP FFFFFFF8, 0004";
            int[] argSizes = new[]
            {
                4,
                2
            }; // sint32, sint16
            Debug("Testing parseFixedSizeArgs:");
            Debug("Input: " + testLine);
            Debug("Expected arg sizes: [4, 2]");

            // Simulate the parsing
            String[] parts = testLine.Split("\\s+");
            IList<string> hexParts = new List<string>();
            foreach (string part in parts)
            {

                // Stop at "str" marker
                if (part.Equals("str"))
                {
                    break;
                }


                // Stop at function references (fn_XXXXXXXX, off_XXXXXXXX, loc_XXXXXXXX)
                if (Regex.IsMatch(part, "^(fn|off|loc|sta|sub)_[0-9A-Fa-f]+$"))
                {
                    break;
                }


                // Stop at instruction name - must contain at least one letter G-Z (not a hex digit)
                // This distinguishes "CPDOWNSP" from "FFFFFFF8"
                if (Regex.IsMatch(part, "^[A-Za-z_][A-Za-z0-9_]*$") && Regex.IsMatch(part, ".*[G-Zg-z].*"))
                {
                    Debug("  Stopping at instruction name: " + part);
                    break;
                }


                // Collect hex values (must be at least 2 chars)
                if (Regex.IsMatch(part, "^[0-9A-Fa-f]{2,}$"))
                {
                    hexParts.Add(part);
                    Debug("  Found hex value: " + part);
                }
                else
                {
                    Debug("  Skipping: " + part);
                }
            }

            Debug("hexParts: " + hexParts);

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
                int hexDigits = argSize * 2;
                if (hexPartIndex < hexParts.Count)
                {
                    string hexValue = hexParts[hexPartIndex];
                    Debug("  Processing arg " + argIndex + ": hexValue=" + hexValue + " expected " + hexDigits + " hex digits");

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

                    Debug("    After padding/truncation: " + hexValue);

                    // Parse as unsigned long to handle large hex values
                    long value = Long.ParseLong(hexValue, 16);
                    Debug("    Parsed value: " + value + " (0x" + Long.ToHexString(value) + ")");

                    // Write bytes (big-endian)
                    for (int i = 0; i < argSize; i++)
                    {
                        int shift = (argSize - 1 - i) * 8;
                        result[resultPos + i] = (byte)((value >> shift) & 0xFF);
                    }

                    Debug("    Written bytes at " + resultPos + ": ");
                    for (int i = 0; i < argSize; i++)
                    {
                        Debug(String.Format("%02X ", result[resultPos + i] & 0xFF));
                    }

                    Debug("");
                }
                else
                {
                    Debug("  No hex value for arg " + argIndex);
                }

                resultPos += argSize;
                hexPartIndex++;
            }

            Debug("\nFinal result:");
            foreach (byte b in result)
            {
                JavaSystem.@out.Print(String.Format("%02X ", b & 0xFF));
            }

            Debug();
        }
    }
}




