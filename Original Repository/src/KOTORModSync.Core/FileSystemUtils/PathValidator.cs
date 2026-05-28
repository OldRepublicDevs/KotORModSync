// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

using JetBrains.Annotations;

namespace KOTORModSync.Core.FileSystemUtils
{
    public static class PathValidator
    {

        private static readonly char[] s_invalidPathCharsWindows =
        {
            '\0', '\a', '\b', '\t', '\n', '\v', '\f', '\r', '"', '*', '<', '>', '?',
        };

        private static readonly char[] s_invalidPathCharsUnix = { '\0' };

        private static readonly string[] s_reservedFileNamesWindows =
        {
            "CON", "PRN", "AUX", "NUL", "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8",
            "COM9", "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        };

        public static bool IsValidPath(
            [CanBeNull] string path,
            bool enforceAllPlatforms = true,
            bool ignoreWildcards = false
        )
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return false;
                }

                if (HasMixedSlashes(path))
                {
                    return false;
                }

                if (HasRepeatedSlashes(path))
                {
                    return false;
                }

                char[] invalidChars = enforceAllPlatforms
                    ? s_invalidPathCharsWindows
                    : GetInvalidCharsForPlatform();

                invalidChars = ignoreWildcards
                    ? invalidChars.Where(c => c != '*' && c != '?').ToArray()
                    : invalidChars;

                if (path.IndexOfAny(invalidChars) >= 0)
                {
                    return false;
                }

                if (ContainsNonPrintableChars(path))
                {
                    return false;
                }

                if (enforceAllPlatforms || Utility.UtilityHelper.GetOperatingSystem() == OSPlatform.Windows)
                {
                    if (HasColonOutsideOfPathRoot(path))
                    {
                        return false;
                    }

                    if (IsReservedFileNameWindows(path))
                    {
                        return false;
                    }

                    if (HasInvalidWindowsFileNameParts(path))
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                Logger.LogException(e);
                return false;
            }
        }

        public static bool HasColonOutsideOfPathRoot([CanBeNull] string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string[] parts = path.Split('/', '\\');
            for (int i = 1; i < parts.Length; i++)
            {
                if (!parts[i].Contains(":"))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        public static bool HasRepeatedSlashes([CanBeNull] string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            for (int i = 0; i < input.Length - 1; i++)
            {
                if ((input[i] == '\\' || input[i] == '/') && (input[i + 1] == '\\' || input[i + 1] == '/'))
                {
                    return true;
                }
            }

            return false;
        }

        public static char[] GetInvalidCharsForPlatform() =>
            Utility.UtilityHelper.GetOperatingSystem() == OSPlatform.Linux
                ? s_invalidPathCharsUnix
                : s_invalidPathCharsWindows;

        public static bool HasMixedSlashes([CanBeNull] string input) =>
            (input?.Contains('/') ?? false) && input.Contains('\\');

        public static bool ContainsNonPrintableChars([CanBeNull] string path) => path?.Any(c => c < ' ') ?? false;

        public static bool IsReservedFileNameWindows([CanBeNull] string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string[] pathParts = path.Split(
                new[] { '\\', '/', Path.DirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries
            );

            return pathParts.Select(Path.GetFileNameWithoutExtension).Any(
                fileName => s_reservedFileNamesWindows.Any(
                    reservedName => string.Equals(reservedName, fileName, StringComparison.OrdinalIgnoreCase)
                )
            );
        }

        public static bool HasInvalidWindowsFileNameParts([CanBeNull] string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            string[] pathParts = path.Split(
                new[] { '\\', '/', Path.DirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries
            );
            foreach (string part in pathParts)
            {

                if (part.EndsWith(" ", StringComparison.Ordinal) || part.EndsWith(".", StringComparison.Ordinal))
                {
                    return true;
                }

                for (int i = 0; i < part.Length - 1; i++)
                {
                    if (part[i] == '.' && part[i + 1] == '.')
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
