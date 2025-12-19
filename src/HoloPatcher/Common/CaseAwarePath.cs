using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Common
{

    /// <summary>
    /// A path class capable of resolving case-sensitivity differences across platforms.
    /// Essential for working with KOTOR files on Unix filesystems where case-sensitivity matters.
    /// </summary>
    public class CaseAwarePath
    {
        private readonly string _path;

        public CaseAwarePath(string path)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
        }

        public CaseAwarePath(object path)
        {
            if (path is string strPath)
            {
                _path = strPath;
            }
            else if (path is CaseAwarePath casePath)
            {
                _path = casePath._path;
            }
            else
            {
                throw new ArgumentException($"Invalid type for path: {path?.GetType().Name ?? "null"}");
            }
        }

        public CaseAwarePath(params string[] paths)
        {
            if (paths == null || paths.Length == 0)
            {
                throw new ArgumentException("At least one path must be provided");
            }

            _path = Path.Combine(paths);
        }

        public static CaseAwarePath From(string path) => new CaseAwarePath(path);

        public string FullPath => Path.GetFullPath(_path);
        public string FileName => Path.GetFileName(_path);
        public string Name => Path.GetFileName(_path);  // Alias for FileName
        public string DirectoryName => Path.GetDirectoryName(_path) ?? "";
        public string Extension => Path.GetExtension(_path);

        public bool Exists()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return File.Exists(_path) || Directory.Exists(_path);
            }

            // On Unix, try case-sensitive resolution
            string resolved = GetCaseSensitivePath(_path);
            return File.Exists(resolved) || Directory.Exists(resolved);
        }

        public bool IsFile() => File.Exists(GetResolvedPath());
        public bool IsDirectory() => Directory.Exists(GetResolvedPath());

        public string GetResolvedPath()
        {
            string resolvedPath;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                resolvedPath = _path;
            }
            else
            {
                resolvedPath = GetCaseSensitivePath(_path);
            }

            // Normalize the path: convert to forward slashes for consistency
            // but preserve UNC paths on Windows (\\server\share)
            bool isUNC = resolvedPath.StartsWith(@"\\") || resolvedPath.StartsWith("//");

            resolvedPath = resolvedPath.Replace('\\', '/');

            // Remove duplicate slashes (but preserve UNC prefix)
            if (isUNC)
            {
                // Preserve the double slash at the start for UNC paths
                resolvedPath = "//" + resolvedPath.Substring(2);
                while (resolvedPath.IndexOf("//", 2) >= 0)
                {
                    int idx = resolvedPath.IndexOf("//", 2);
                    resolvedPath = resolvedPath.Substring(0, idx) + resolvedPath.Substring(idx + 1);
                }
            }
            else
            {
                while (resolvedPath.Contains("//"))
                {
                    resolvedPath = resolvedPath.Replace("//", "/");
                }
            }

            // Remove trailing slash unless it's a root (e.g., "C:/")
            if (resolvedPath.Length > 2 && resolvedPath.EndsWith("/"))
            {
                resolvedPath = resolvedPath.TrimEnd('/');
            }

            // On Windows, convert back to backslashes to preserve UNC paths
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && isUNC)
            {
                resolvedPath = resolvedPath.Replace('/', '\\');
            }

            return resolvedPath;
        }

        /// <summary>
        /// Resolves a path to match the actual filesystem case on case-sensitive systems.
        /// </summary>
        public static string GetCaseSensitivePath(string path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return path;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            // Get absolute path
            string absolutePath = Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
            string[] parts = absolutePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Start from root
            string currentPath = parts[0] + Path.DirectorySeparatorChar;

            for (int i = 1; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i]))
                {
                    continue;
                }

                string nextPath = Path.Combine(currentPath, parts[i]);

                // If path exists exactly as is, continue
                if (File.Exists(nextPath) || Directory.Exists(nextPath))
                {
                    currentPath = nextPath;
                    continue;
                }

                // Try to find case-insensitive match
                if (Directory.Exists(currentPath))
                {
                    // Can be null if no closest match found
                    string closestMatch = FindClosestMatch(parts[i], currentPath, i == parts.Length - 1);
                    if (closestMatch != null)
                    {
                        parts[i] = closestMatch;
                        currentPath = Path.Combine(currentPath, closestMatch);
                        continue;
                    }
                }

                // Path doesn't exist, return what we have so far + remaining parts
                currentPath = nextPath;
            }

            return currentPath;
        }

        /// <summary>
        /// Finds the closest case-insensitive match for a filename in a directory.
        /// </summary>
        [CanBeNull]
        private static string FindClosestMatch(string target, string directoryPath, bool isLastPart)
        {
            if (!Directory.Exists(directoryPath))
            {
                return null;
            }

            IEnumerable<string> candidates;
            try
            {
                if (isLastPart)
                {
                    // For the last part, check both files and directories
                    candidates = Directory.GetFileSystemEntries(directoryPath);
                }
                else
                {
                    // For intermediate parts, only check directories
                    candidates = Directory.GetDirectories(directoryPath);
                }
            }
            catch
            {
                return null;
            }

            int maxMatching = -1;
            // Can be null if no closest match found
            string closestMatch = null;

            foreach (string candidatePath in candidates)
            {
                string candidateName = Path.GetFileName(candidatePath);
                int matchingChars = GetMatchingCharactersCount(candidateName, target);

                if (matchingChars > maxMatching)
                {
                    maxMatching = matchingChars;
                    closestMatch = candidateName;

                    // Early exit if exact match
                    if (matchingChars == target.Length)
                    {
                        break;
                    }
                }
            }

            return closestMatch ?? target;
        }

        /// <summary>
        /// Returns the number of case-sensitive characters that match at each position.
        /// Returns -1 if the strings are not case-insensitive matches.
        /// </summary>
        public static int GetMatchingCharactersCount(string str1, string str2)
        {
            if (!string.Equals(str1, str2, StringComparison.OrdinalIgnoreCase))
            {
                return -1;
            }

            int count = 0;
            int minLength = Math.Min(str1.Length, str2.Length);

            for (int i = 0; i < minLength; i++)
            {
                if (str1[i] == str2[i])
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Finds the closest case match from a list of CaseAwarePath items.
        /// </summary>
        public static CaseAwarePath FindClosestMatch(string target, IEnumerable<CaseAwarePath> items)
        {
            int maxMatching = -1;
            // Can be null if no closest match found
            CaseAwarePath closestMatch = null;

            foreach (CaseAwarePath item in items)
            {
                string itemStr = item.ToString();
                int matchingChars = GetMatchingCharactersCount(itemStr, target);

                if (matchingChars > maxMatching)
                {
                    maxMatching = matchingChars;
                    closestMatch = item;

                    if (matchingChars == target.Length)
                    {
                        break;
                    }
                }
            }

            return closestMatch ?? new CaseAwarePath(target);
        }

        public CaseAwarePath Combine(string other)
        {
            return new CaseAwarePath(Path.Combine(_path, other));
        }

        public CaseAwarePath Combine(params string[] paths)
        {
            string[] allPaths = new[] { _path }.Concat(paths).ToArray();
            return new CaseAwarePath(Path.Combine(allPaths));
        }

        public CaseAwarePath JoinPath(string other) => Combine(other);
        public CaseAwarePath JoinPath(params string[] paths) => Combine(paths);

        public bool EndsWith(string suffix)
        {
            return _path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
        }

        public bool IsRelativeTo(string other)
        {
            string thisPath = GetResolvedPath().Replace('\\', '/');
            string otherPath = other.Replace('\\', '/');

            // Ensure paths end consistently for comparison
            if (!otherPath.EndsWith('/'))
            {
                otherPath += '/';
            }

            return thisPath.StartsWith(otherPath, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(thisPath, otherPath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
        }

        public bool IsRelativeTo(CaseAwarePath other)
        {
            return IsRelativeTo(other.GetResolvedPath());
        }

        public string RelativeTo(CaseAwarePath basePath)
        {
            string thisPath = GetResolvedPath();
            string basePathStr = basePath.GetResolvedPath();

            if (!Path.IsPathRooted(thisPath) || !Path.IsPathRooted(basePathStr))
            {
                // For relative paths, just do string manipulation
                return Path.GetRelativePath(basePathStr, thisPath);
            }

            return Path.GetRelativePath(basePathStr, thisPath);
        }

        public static string StrNorm(string path, string slash)
        {
            // Normalize path separators
            string normalized = path.Replace('/', Path.DirectorySeparatorChar)
                                   .Replace('\\', Path.DirectorySeparatorChar);

            // Remove duplicate separators
            while (normalized.Contains(Path.DirectorySeparatorChar.ToString() + Path.DirectorySeparatorChar))
            {
                normalized = normalized.Replace(
                    Path.DirectorySeparatorChar.ToString() + Path.DirectorySeparatorChar,
                    Path.DirectorySeparatorChar.ToString());
            }

            // Remove trailing separator unless it's a root
            normalized = normalized.TrimEnd(Path.DirectorySeparatorChar);

            // Convert to requested slash type
            if (slash == "\\")
            {
                normalized = normalized.Replace('/', '\\');
            }
            else if (slash == "/")
            {
                normalized = normalized.Replace('\\', '/');
            }

            return normalized;
        }

        public (string stem, string ext) SplitFilename(int dots = 1)
        {
            if (dots == 0)
            {
                throw new ArgumentException("dots parameter cannot be 0");
            }

            string filename = Path.GetFileName(_path);
            if (string.IsNullOrEmpty(filename))
            {
                return ("", "");
            }

            string[] parts = filename.Split('.');
            if (parts.Length == 1)
            {
                return (filename, "");
            }

            if (dots > 0)
            {
                // Split from the right, taking 'dots' number of extensions
                // If dots >= parts.Length - 1, take all extensions (leaving just the first part as stem)
                int numExtensions = Math.Min(dots, parts.Length - 1);
                int splitIndex = parts.Length - numExtensions;

                string stem = string.Join(".", parts.Take(splitIndex));
                string ext = string.Join(".", parts.Skip(splitIndex));
                return (stem, ext);
            }
            else
            {
                // Split from the left (negative dots)
                // If abs(dots) >= parts.Length - 1, return normal split (from the right)
                int absDots = Math.Abs(dots);
                if (absDots >= parts.Length - 1)
                {
                    // Fall back to normal split from right
                    return (string.Join(".", parts.Take(parts.Length - 1)),
                            string.Join(".", parts.Skip(parts.Length - 1)));
                }

                int takeParts = absDots;
                return (string.Join(".", parts.Skip(takeParts)),
                        string.Join(".", parts.Take(takeParts)));
            }
        }

        public static CaseAwarePath operator /(CaseAwarePath left, string right)
        {
            return left.Combine(right);
        }

        public static CaseAwarePath operator /(CaseAwarePath left, CaseAwarePath right)
        {
            return left.Combine(right._path);
        }

        public override string ToString() => GetResolvedPath();

        public override bool Equals([CanBeNull] object obj)
        {
            if (obj is CaseAwarePath other)
            {
                return string.Equals(
                    GetResolvedPath().Replace('\\', '/'),
                    other.GetResolvedPath().Replace('\\', '/'),
                    StringComparison.OrdinalIgnoreCase
                );
            }
            if (obj is string strPath)
            {
                return string.Equals(
                    GetResolvedPath().Replace('\\', '/'),
                    strPath.Replace('\\', '/'),
                    StringComparison.OrdinalIgnoreCase
                );
            }
            return false;
        }

        public override int GetHashCode()
        {
            return GetResolvedPath().Replace('\\', '/').ToLowerInvariant().GetHashCode();
        }

        public static implicit operator string(CaseAwarePath path) => path.ToString();
    }
}
