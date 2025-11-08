// Copyright (C) 2025
// Licensed under the GPL version 3 license.

using System;
using System.Collections.Generic;
using System.Text;

namespace KOTORModSync.Core.Utility
{
    /// <summary>
    /// Provides canonical bencoding for network cache content identification.
    /// Implements deterministic encoding to ensure identical output across all platforms.
    /// </summary>
    public static class CanonicalBencoding
    {
        /// <summary>
        /// Encodes a sorted dictionary to canonical bencode format.
        /// Rules:
        /// - Dictionary keys in lexicographic byte order (SortedDictionary handles this)
        /// - Integers: minimal representation (no leading zeros except "0")
        /// - Strings: length-prefixed UTF-8 bytes
        /// - Byte arrays: length-prefixed raw bytes
        /// </summary>
        public static byte[] BencodeCanonical(SortedDictionary<string, object> dict)
        {
            var output = new List<byte>();
            EncodeDictionary(dict, output);
            return output.ToArray();
        }

        private static void EncodeDictionary(SortedDictionary<string, object> dict, List<byte> output)
        {
            output.Add((byte)'d');

            foreach (KeyValuePair<string, object> kvp in dict)
            {
                // Encode key (always a string)
                EncodeString(kvp.Key, output);

                // Encode value
                EncodeValue(kvp.Value, output);
            }

            output.Add((byte)'e');
        }

        private static void EncodeValue(object value, List<byte> output)
        {
            if (value is null)
            {
                throw new ArgumentException("Null values are not allowed in bencoding", nameof(value));
            }

            if (value is long longVal)
            {
                EncodeInteger(longVal, output);
            }
            else if (value is int intVal)
            {
                EncodeInteger(intVal, output);
            }
            else if (value is string strVal)
            {
                EncodeString(strVal, output);
            }
            else if (value is byte[] byteVal)
            {
                EncodeBytes(byteVal, output);
            }
            else if (value is SortedDictionary<string, object> dictVal)
            {
                EncodeDictionary(dictVal, output);
            }
            else if (value is List<object> listVal)
            {
                EncodeList(listVal, output);
            }
            else
            {
                throw new ArgumentException($"Unsupported type for bencoding: {value.GetType()}", nameof(value));
            }
        }

        private static void EncodeInteger(long value, List<byte> output)
        {
            output.Add((byte)'i');
            string intStr = value.ToString();
            output.AddRange(Encoding.ASCII.GetBytes(intStr));
            output.Add((byte)'e');
        }

        private static void EncodeString(string value, List<byte> output)
        {
            byte[] strBytes = Encoding.UTF8.GetBytes(value);
            string lengthStr = strBytes.Length.ToString();
            output.AddRange(Encoding.ASCII.GetBytes(lengthStr));
            output.Add((byte)':');
            output.AddRange(strBytes);
        }

        private static void EncodeBytes(byte[] value, List<byte> output)
        {
            string lengthStr = value.Length.ToString();
            output.AddRange(Encoding.ASCII.GetBytes(lengthStr));
            output.Add((byte)':');
            output.AddRange(value);
        }

        private static void EncodeList(List<object> list, List<byte> output)
        {
            output.Add((byte)'l');

            foreach (object item in list)
            {
                EncodeValue(item, output);
            }

            output.Add((byte)'e');
        }
    }
}
