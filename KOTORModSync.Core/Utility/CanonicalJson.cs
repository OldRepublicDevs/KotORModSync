// Copyright (C) 2025
// Licensed under the GPL version 3 license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace KOTORModSync.Core.Utility
{
    /// <summary>
    /// Provides deterministic JSON serialization for content-addressable hashing.
    /// Implements canonical rules to ensure identical output across all platforms and runtimes.
    /// </summary>
    public static class CanonicalJson
    {
        /// <summary>
        /// Serializes a dictionary to canonical JSON format.
        /// Rules:
        /// - Unicode NFC normalization for all strings
        /// - Lexicographic key ordering by UTF-8 codepoints
        /// - Minimal decimal notation (no exponents, strip trailing zeros)
        /// - Lowercase booleans
        /// - Arrays preserve order
        /// - No whitespace
        /// </summary>
        public static string Serialize(Dictionary<string, object> obj)
        {
            if (obj is null)
            {
                return "null";
            }

            var sb = new StringBuilder();
            SerializeValue(obj, sb);
            return sb.ToString();
        }

        /// <summary>
        /// Computes a SHA-256 hash of the canonical JSON representation.
        /// </summary>
        public static string ComputeHash(Dictionary<string, object> obj)
        {
            string canonical = Serialize(obj);
            byte[] bytes = Encoding.UTF8.GetBytes(canonical);
#if NET48
byte[] hash;
using (var sha = SHA256.Create())
{
hash = sha.ComputeHash(bytes);
}
#else
            byte[] hash = SHA256.HashData(bytes);
#endif
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private static void SerializeValue(object value, StringBuilder sb)
        {
            if (value is null)
            {
                sb.Append("null");
            }
            else if (value is bool boolVal)
            {
                sb.Append(boolVal ? "true" : "false");
            }
            else if (value is string strVal)
            {
                SerializeString(strVal, sb);
            }
            else if (value is int || value is long || value is short || value is byte)
            {
                sb.Append(Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture));
            }
            else if (value is double || value is float || value is decimal)
            {
                SerializeNumber(Convert.ToDouble(value), sb);
            }
            else if (value is Dictionary<string, object> dictVal)
            {
                SerializeDictionary(dictVal, sb);
            }
            else if (value is IDictionary<string, object> iDictVal)
            {
                var dict = new Dictionary<string, object>(iDictVal, StringComparer.Ordinal);
                SerializeDictionary(dict, sb);
            }
            else if (value is System.Collections.IList listVal)
            {
                SerializeArray(listVal, sb);
            }
            else
            {
                // Fallback to string representation
                SerializeString(value.ToString(), sb);
            }
        }

        private static void SerializeDictionary(Dictionary<string, object> dict, StringBuilder sb)
        {
            sb.Append('{');

            // Sort keys lexicographically by UTF-8 bytes
            var sortedKeys = dict.Keys
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToList();

            bool first = true;
            foreach (string key in sortedKeys)
            {
                if (!first)
                {
                    sb.Append(',');
                }

                first = false;

                SerializeString(key, sb);
                sb.Append(':');
                SerializeValue(dict[key], sb);
            }

            sb.Append('}');
        }

        private static void SerializeArray(System.Collections.IList array, StringBuilder sb)
        {
            sb.Append('[');

            bool first = true;
            foreach (object item in array)
            {
                if (!first)
                {
                    sb.Append(',');
                }

                first = false;

                SerializeValue(item, sb);
            }

            sb.Append(']');
        }

        private static void SerializeString(string str, StringBuilder sb)
        {
            // Normalize to Unicode NFC
            string normalized = str.Normalize(NormalizationForm.FormC);

            sb.Append('"');
            foreach (char c in normalized)
            {
                switch (c)
                {
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    default:
                        if (char.IsControl(c))
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
        }

        private static void SerializeNumber(double number, StringBuilder sb)
        {
            if (double.IsNaN(number) || double.IsInfinity(number))
            {
                sb.Append("null");
                return;
            }

            // Use InvariantCulture with minimal representation
            // Check if it's an integer value
            if (Math.Abs(number % 1) < double.Epsilon)
            {
                sb.Append(((long)number).ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                // Use G format with sufficient precision, then strip trailing zeros
                string numStr = number.ToString("G17", CultureInfo.InvariantCulture);

                // Remove scientific notation if present by converting back
                if (numStr.Contains('E') || numStr.Contains('e'))
                {
                    numStr = number.ToString("F99", CultureInfo.InvariantCulture).TrimEnd('0');
                    if (numStr.EndsWith(".", StringComparison.Ordinal))
                    {
                        numStr += "0";
                    }
                }
                else
                {
                    // Strip trailing zeros after decimal point
                    if (numStr.Contains('.'))
                    {
                        numStr = numStr.TrimEnd('0');
                        if (numStr.EndsWith(".", StringComparison.Ordinal))
                        {
                            numStr += "0";
                        }
                    }
                }

                sb.Append(numStr);
            }
        }
    }
}
