// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using JetBrains.Annotations;

namespace KOTORModSync.Core.Utility
{
    /// <summary>
    /// Compatibility helpers for .NET Framework 4.8 that don't have some .NET 8.0 APIs.
    /// </summary>
    public static class NetFrameworkCompatibility
    {
        /// <summary>
        /// Polyfill for Path.GetRelativePath (available in .NET Standard 2.1+ but not .NET Framework 4.8).
        /// </summary>
        [NotNull]
        public static string GetRelativePath([NotNull] string relativeTo, [NotNull] string path)
        {
            if (relativeTo is null)
                throw new ArgumentNullException(nameof(relativeTo));
            if (path is null)
                throw new ArgumentNullException(nameof(path));

            // Normalize paths
            relativeTo = Path.GetFullPath(relativeTo);
            path = Path.GetFullPath(path);

            // Check if paths are on different drives
            if (Path.GetPathRoot(relativeTo) != Path.GetPathRoot(path))
                return path;

            // Split paths into segments
            string[] relativeToParts = relativeTo.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string[] pathParts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Find common prefix
            int commonLength = 0;
            int minLength = Math.Min(relativeToParts.Length, pathParts.Length);
            for (int i = 0; i < minLength; i++)
            {
                if (string.Equals(relativeToParts[i], pathParts[i], StringComparison.OrdinalIgnoreCase))
                    commonLength++;
                else
                    break;
            }

            // Build relative path
            var result = new StringBuilder();
            for (int i = commonLength; i < relativeToParts.Length; i++)
            {
                result.Append("..");
                result.Append(Path.DirectorySeparatorChar);
            }

            for (int i = commonLength; i < pathParts.Length; i++)
            {
                result.Append(pathParts[i]);
                if (i < pathParts.Length - 1)
                    result.Append(Path.DirectorySeparatorChar);
            }

            return result.Length == 0 ? "." : result.ToString();
        }

        /// <summary>
        /// Polyfill for Math.Clamp (available in .NET Core 2.0+ but not .NET Framework 4.8).
        /// </summary>
        public static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        /// <summary>
        /// Polyfill for Math.Clamp (double overload).
        /// </summary>
        public static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        /// <summary>
        /// Polyfill for Convert.FromHexString (available in .NET 5+ but not .NET Framework 4.8).
        /// </summary>
        [NotNull]
        public static byte[] FromHexString([NotNull] string hex)
        {
            if (hex is null)
                throw new ArgumentNullException(nameof(hex));
            if (hex.Length % 2 != 0)
                throw new ArgumentException("Hex string must have even length", nameof(hex));

            byte[] result = new byte[hex.Length / 2];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return result;
        }

        /// <summary>
        /// Polyfill for string.Replace with StringComparison (available in .NET Core 2.1+ but not .NET Framework 4.8).
        /// </summary>
        [NotNull]
        public static string Replace([NotNull] string str, [NotNull] string oldValue, [NotNull] string newValue, StringComparison comparisonType)
        {
            if (str is null)
                throw new ArgumentNullException(nameof(str));
            if (oldValue is null)
                throw new ArgumentNullException(nameof(oldValue));
            if (newValue is null)
                throw new ArgumentNullException(nameof(newValue));

            if (oldValue.Length == 0)
                return str;

            var result = new StringBuilder(str.Length);
            int startIndex = 0;
            int index;

            while ((index = str.IndexOf(oldValue, startIndex, comparisonType)) >= 0)
            {
                result.Append(str, startIndex, index - startIndex);
                result.Append(newValue);
                startIndex = index + oldValue.Length;
            }

            result.Append(str, startIndex, str.Length - startIndex);
            return result.ToString();
        }

        /// <summary>
        /// Polyfill for SHA256.HashData (available in .NET 5+ but not .NET Framework 4.8).
        /// </summary>
        [NotNull]
        public static byte[] HashDataSHA256([NotNull] byte[] data)
        {
            if (data is null)
                throw new ArgumentNullException(nameof(data));

            using (var sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(data);
            }
        }

        /// <summary>
        /// Polyfill for SHA1.HashData (available in .NET 5+ but not .NET Framework 4.8).
        /// </summary>
        [NotNull]
        public static byte[] HashDataSHA1([NotNull] byte[] data)
        {
            if (data is null)
                throw new ArgumentNullException(nameof(data));

            using (var sha1 = SHA1.Create())
            {
                return sha1.ComputeHash(data);
            }
        }

        /// <summary>
        /// Polyfill for SHA256.HashDataAsync (available in .NET 5+ but not .NET Framework 4.8).
        /// </summary>
        public static async Task<byte[]> HashDataSHA256Async([NotNull] Stream stream, CancellationToken cancellationToken = default)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            long originalPosition = stream.Position;
            using (var sha256 = SHA256.Create())
            {
                return await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    byte[] result = sha256.ComputeHash(stream);
                    stream.Position = originalPosition;
                    return result;
                }, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Polyfill for File.ReadAllTextAsync (available in .NET Standard 2.1+ but not .NET Framework 4.8).
        /// </summary>
        public static async Task<string> ReadAllTextAsync([NotNull] string path, Encoding encoding = null, CancellationToken cancellationToken = default)
        {
            if (path is null)
                throw new ArgumentNullException(nameof(path));

            encoding = encoding ?? Encoding.UTF8;
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return File.ReadAllText(path, encoding);
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Polyfill for File.WriteAllTextAsync (available in .NET Standard 2.1+ but not .NET Framework 4.8).
        /// </summary>
        public static async Task WriteAllTextAsync([NotNull] string path, [NotNull] string contents, Encoding encoding = null, CancellationToken cancellationToken = default)
        {
            if (path is null)
                throw new ArgumentNullException(nameof(path));
            if (contents is null)
                throw new ArgumentNullException(nameof(contents));

            encoding = encoding ?? Encoding.UTF8;
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.WriteAllText(path, contents, encoding);
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Polyfill for File.ReadAllBytesAsync (available in .NET Standard 2.1+ but not .NET Framework 4.8).
        /// </summary>
        public static async Task<byte[]> ReadAllBytesAsync([NotNull] string path, CancellationToken cancellationToken = default)
        {
            if (path is null)
                throw new ArgumentNullException(nameof(path));

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return File.ReadAllBytes(path);
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Polyfill for File.WriteAllBytesAsync (available in .NET Standard 2.1+ but not .NET Framework 4.8).
        /// </summary>
        public static async Task WriteAllBytesAsync([NotNull] string path, [NotNull] byte[] bytes, CancellationToken cancellationToken = default)
        {
            if (path is null)
                throw new ArgumentNullException(nameof(path));
            if (bytes is null)
                throw new ArgumentNullException(nameof(bytes));

            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.WriteAllBytes(path, bytes);
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Helper to check if string contains a character with StringComparison (for .NET Framework 4.8 compatibility).
        /// </summary>
        public static bool Contains([NotNull] string str, char value, StringComparison comparisonType)
        {
            if (str is null)
                throw new ArgumentNullException(nameof(str));

            // For char, we can only do case-sensitive or case-insensitive
            if (comparisonType == StringComparison.Ordinal || comparisonType == StringComparison.CurrentCulture)
            {
                return str.IndexOf(value) >= 0;
            }
            else
            {
                // Case-insensitive: check both upper and lower case
                char upper = char.ToUpperInvariant(value);
                char lower = char.ToLowerInvariant(value);
                return str.IndexOf(upper) >= 0 || str.IndexOf(lower) >= 0;
            }
        }

        /// <summary>
        /// Helper to check if string contains a substring with StringComparison (for .NET Framework 4.8 compatibility).
        /// </summary>
        public static bool Contains([NotNull] string str, [NotNull] string value, StringComparison comparisonType)
        {
            if (str is null)
                throw new ArgumentNullException(nameof(str));
            if (value is null)
                throw new ArgumentNullException(nameof(value));

            return str.IndexOf(value, comparisonType) >= 0;
        }
    }
}

