using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KOTORModSync;
using KOTORModSync.Installation;
using KOTORModSync.Resource;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Diff
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:998-1027
    // Original: @dataclass class DiffContext:
    /// <summary>
    /// Context for diff operations, grouping related file paths.
    /// </summary>
    public class DiffContext
    {
        public string File1Rel { get; set; }
        public string File2Rel { get; set; }
        public string Ext { get; set; }
        public string Resname { get; set; }

        // Resolution order location types (for resolution-aware diffing)
        public string File1LocationType { get; set; } // Location type in vanilla/older install (Override, Modules (.mod), etc.)
        public string File2LocationType { get; set; } // Location type in modded/newer install
        public string File1Filepath { get; set; } // Full filepath in base installation (for StrRef reference finding)
        public string File2Filepath { get; set; } // Full filepath in target installation (for module name extraction)
        public Installation.Installation File1Installation { get; set; } // Base installation object (for StrRef reference finding)
        public Installation.Installation File2Installation { get; set; } // Target installation object (for StrRef/2DA reference finding)

        public DiffContext(string file1Rel, string file2Rel, string ext, string resname = null)
        {
            File1Rel = file1Rel;
            File2Rel = file2Rel;
            Ext = ext;
            Resname = resname;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:1014-1027
        // Original: @property def where(self) -> str:
        /// <summary>
        /// Get the display name for the resource being compared.
        /// Returns full path context: install_name/location/container/resource.ext
        /// Uses file2_rel (modded/target) as it's more relevant for patch generation.
        /// </summary>
        public string Where
        {
            get
            {
                if (!string.IsNullOrEmpty(Resname))
                {
                    // For resources inside containers (capsules/BIFs)
                    // file2_rel contains full path like: swkotor/data/2da.bif
                    // Build: swkotor/data/2da.bif/appearance.2da
                    return $"{File2Rel}/{Resname}.{Ext}";
                }
                // For loose files, just return the full path from modded/target
                return File2Rel;
            }
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:1035-1057
    // Original: def is_text_content(data: bytes) -> bool:
    /// <summary>
    /// Heuristically determine if data is text content.
    /// </summary>
    public static class DiffEngine
    {
        public static bool IsTextContent(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return true;
            }

            // Try to decode as UTF-8 first
            try
            {
                System.Text.Encoding.UTF8.GetString(data);
                return true;
            }
            catch
            {
                // Ignore
            }

            // Try Windows-1252 (common for KOTOR text files)
            try
            {
                System.Text.Encoding.GetEncoding(1252).GetString(data);
                return true;
            }
            catch
            {
                // Ignore
            }

            // Check for high ratio of printable ASCII characters
            // ASCII printable range: 32-126, plus tab(9), LF(10), CR(13)
            const int PRINTABLE_ASCII_MIN = 32;
            const int PRINTABLE_ASCII_MAX = 126;
            const double TEXT_THRESHOLD = 0.7;

            int printableCount = data.Count(b => (PRINTABLE_ASCII_MIN <= b && b <= PRINTABLE_ASCII_MAX) || b == 9 || b == 10 || b == 13);
            return (double)printableCount / data.Length > TEXT_THRESHOLD;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:1190-1196
        // Original: def walk_files(root: Path) -> set[str]:
        /// <summary>
        /// Walk all files in a directory tree.
        /// </summary>
        public static HashSet<string> WalkFiles(string root)
        {
            if (string.IsNullOrEmpty(root))
            {
                return new HashSet<string>();
            }

            if (!Directory.Exists(root) && !File.Exists(root))
            {
                return new HashSet<string>();
            }

            if (File.Exists(root))
            {
                return new HashSet<string> { Path.GetFileName(root).ToLowerInvariant() };
            }

            HashSet<string> files = new HashSet<string>();
            foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                string relPath = Path.GetRelativePath(root, file).Replace('\\', '/');
                files.Add(relPath.ToLowerInvariant());
            }
            return files;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:1199-1202
        // Original: def ext_of(path: Path) -> str:
        /// <summary>
        /// Extract extension from path.
        /// </summary>
        public static string ExtOf(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext.StartsWith(".") ? ext.Substring(1) : ext;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:1205-1210
        // Original: def should_skip_rel(_rel: str) -> bool:
        /// <summary>
        /// Check if a relative path should be skipped.
        /// Note: Currently unused but kept for future filtering capabilities.
        /// </summary>
        public static bool ShouldSkipRel(string rel)
        {
            return false;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:2010-2012
        // Original: def is_modules_directory(dir_path: Path) -> bool:
        /// <summary>
        /// Check if a directory is a modules directory.
        /// </summary>
        public static bool IsModulesDirectory(string dirPath)
        {
            if (string.IsNullOrEmpty(dirPath))
            {
                return false;
            }
            string dirName = Path.GetFileName(dirPath).ToLowerInvariant();
            return dirName == "modules" || dirName == "module" || dirName == "mods";
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:1159-1172
        // Original: def relative_path_from_to(src: PurePath, dst: PurePath) -> Path:
        /// <summary>
        /// Calculate relative path from src to dst.
        /// </summary>
        public static string RelativePathFromTo(string src, string dst)
        {
            try
            {
                return Path.GetRelativePath(src, dst).Replace('\\', '/');
            }
            catch
            {
                // Fallback if paths are on different drives or cannot be made relative
                return dst;
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:2252-2288
        // Original: def should_include_in_filtered_diff(file_path: str, filters: list[str] | None) -> bool:
        /// <summary>
        /// Check if a file should be included based on filter criteria.
        /// </summary>
        public static bool ShouldIncludeInFilteredDiff(string filePath, List<string> filters)
        {
            if (filters == null || filters.Count == 0)
            {
                return true; // No filters means include everything
            }

            string fileName = Path.GetFileName(filePath).ToLowerInvariant();
            string[] pathParts = filePath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string filterPattern in filters)
            {
                string filterName = Path.GetFileName(filterPattern).ToLowerInvariant();

                // Direct filename match
                if (filterName == fileName)
                {
                    return true;
                }

                // Check if filter name appears in parent directories
                if (pathParts.Any(p => p.Equals(filterName, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }

                // Module name match (for .rim/.mod/.erf files)
                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                if (ext == ".rim" || ext == ".mod" || ext == ".erf")
                {
                    try
                    {
                        string root = Module.NameToRoot(filePath);
                        if (!string.IsNullOrEmpty(filterName) && filterName.Equals(root, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                    catch
                    {
                        // Ignore errors in module root extraction
                    }
                }
            }

            return false;
        }
    }
}