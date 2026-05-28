// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/ini_manager.py
// Original: class INIManager: ...
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JetBrains.Annotations;

namespace KOTORModSync.TSLPatcher
{
    /// <summary>
    /// Manages INI file operations with easy section merging and updating.
    /// 1:1 port of INIManager from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/ini_manager.py
    /// </summary>
    public class INIManager
    {
        private readonly string _iniPath;
        private Dictionary<string, Dictionary<string, object>> _config;

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/ini_manager.py:25-32
        // Original: def __init__(self, ini_path: Path) -> None: ...
        public INIManager(string iniPath)
        {
            _iniPath = iniPath;
            _config = null;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/ini_manager.py:34-44
        // Original: def load(self) -> None: ...
        public void Load()
        {
            if (File.Exists(_iniPath))
            {
                _config = ParseIniFile(_iniPath);
            }
            else
            {
                _config = new Dictionary<string, Dictionary<string, object>>();
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/ini_manager.py:46-59
        // Original: def initialize_sections(self, section_headers: list[str]) -> None: ...
        public void InitializeSections(List<string> sectionHeaders)
        {
            if (_config == null)
            {
                _config = new Dictionary<string, Dictionary<string, object>>();
            }

            // ConfigObj doesn't use brackets in section names
            foreach (string header in sectionHeaders)
            {
                string sectionName = header.Trim('[', ']');
                if (!_config.ContainsKey(sectionName))
                {
                    _config[sectionName] = new Dictionary<string, object>();
                }
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/ini_manager.py:61-114
        // Original: def merge_section_lines(self, section_name: str, lines: list[str]) -> None: ...
        public void MergeSectionLines(string sectionName, List<string> lines)
        {
            if (_config == null)
            {
                Load();
            }

            // Ensure section exists
            if (!_config.ContainsKey(sectionName))
            {
                _config[sectionName] = new Dictionary<string, object>();
            }

            Dictionary<string, object> section = _config[sectionName];

            // Parse and merge lines
            foreach (string line in lines)
            {
                string parsedLine = line.Trim();
                if (string.IsNullOrEmpty(parsedLine) || parsedLine.StartsWith(";") || parsedLine.StartsWith("#"))
                {
                    continue; // Skip empty lines and comments
                }

                if (!parsedLine.Contains("="))
                {
                    continue;
                }

                int eqIndex = parsedLine.IndexOf('=');
                string key = parsedLine.Substring(0, eqIndex).Trim();
                string value = parsedLine.Substring(eqIndex + 1).Trim();

                // Remove quotes if present
                if (value.Length >= 2 && value.StartsWith("\"") && value.EndsWith("\""))
                {
                    value = value.Substring(1, value.Length - 2);
                }
                else if (value.Length >= 2 && value.StartsWith("'") && value.EndsWith("'"))
                {
                    value = value.Substring(1, value.Length - 2);
                }

                // ConfigObj handles lists automatically
                // For now, treat as single value
                if (!section.ContainsKey(key))
                {
                    section[key] = value;
                }
                else
                {
                    // Key exists - check if it's a list or merge strategy
                    object existing = section[key];
                    if (existing is List<string> existingList)
                    {
                        if (!existingList.Contains(value))
                        {
                            existingList.Add(value);
                        }
                    }
                    // Convert to list if different values
                    else if (!existing.Equals(value))
                    {
                        section[key] = new List<string> { existing.ToString(), value };
                    }
                }
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/ini_manager.py:116-164
        // Original: def merge_sections_from_serializer(self, serialized_lines: list[str]) -> None: ...
        public void MergeSectionsFromSerializer(List<string> serializedLines)
        {
            if (_config == null)
            {
                Load();
            }

            string currentSection = null;
            var currentLines = new List<string>();

            foreach (string line in serializedLines)
            {
                string lineStripped = line.Trim();

                // Skip header comments (handled separately)
                if (lineStripped.StartsWith(";") || lineStripped.StartsWith("#"))
                {
                    continue;
                }

                // Detect section header
                if (lineStripped.StartsWith("[") && lineStripped.EndsWith("]"))
                {
                    // Process previous section
                    if (currentSection != null && currentLines.Count > 0)
                    {
                        MergeSectionLines(currentSection, currentLines);
                    }

                    // Start new section
                    string sectionName = lineStripped.Substring(1, lineStripped.Length - 2);
                    currentSection = sectionName;
                    currentLines = new List<string>();

                    // Ensure section exists
                    if (!_config.ContainsKey(sectionName))
                    {
                        _config[sectionName] = new Dictionary<string, object>();
                    }
                }
                // Accumulate lines for current section
                else if (currentSection != null)
                {
                    currentLines.Add(line);
                }
            }

            // Process final section
            if (currentSection != null && currentLines.Count > 0)
            {
                MergeSectionLines(currentSection, currentLines);
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/ini_manager.py:166-171
        // Original: def write(self) -> None: ...
        public void Write()
        {
            if (_config == null)
            {
                return;
            }

            WriteIniFile(_iniPath, _config);
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/ini_manager.py:173-187
        // Original: def get_section(self, section_name: str) -> ConfigObj | None: ...
        [CanBeNull]
        public Dictionary<string, object> GetSection(string sectionName)
        {
            if (_config == null)
            {
                return null;
            }
            return _config.ContainsKey(sectionName) ? _config[sectionName] : null;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/ini_manager.py:189-200
        // Original: def section_exists(self, section_name: str) -> bool: ...
        public bool SectionExists(string sectionName)
        {
            if (_config == null)
            {
                return false;
            }
            return _config.ContainsKey(sectionName);
        }

        // Helper method to parse INI file
        private static Dictionary<string, Dictionary<string, object>> ParseIniFile(string filePath)
        {
            var config = new Dictionary<string, Dictionary<string, object>>();
            string currentSection = null;

            if (!File.Exists(filePath))
            {
                return config;
            }

            string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";") || trimmed.StartsWith("#"))
                {
                    continue;
                }

                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    currentSection = trimmed.Substring(1, trimmed.Length - 2);
                    if (!config.ContainsKey(currentSection))
                    {
                        config[currentSection] = new Dictionary<string, object>();
                    }
                }
                else if (currentSection != null && trimmed.Contains("="))
                {
                    int eqIndex = trimmed.IndexOf('=');
                    string key = trimmed.Substring(0, eqIndex).Trim();
                    string value = trimmed.Substring(eqIndex + 1).Trim();
                    config[currentSection][key] = value;
                }
            }

            return config;
        }

        // Helper method to write INI file
        private static void WriteIniFile(string filePath, Dictionary<string, Dictionary<string, object>> config)
        {
            var sb = new StringBuilder();
            foreach (KeyValuePair<string, Dictionary<string, object>> section in config)
            {
                sb.AppendLine($"[{section.Key}]");
                foreach (KeyValuePair<string, object> kvp in section.Value)
                {
                    if (kvp.Value is List<string> listValue)
                    {
                        foreach (string item in listValue)
                        {
                            sb.AppendLine($"{kvp.Key}={item}");
                        }
                    }
                    else
                    {
                        string valueStr = kvp.Value?.ToString() ?? "";
                        if (valueStr.Contains(" "))
                        {
                            valueStr = $"\"{valueStr}\"";
                        }
                        sb.AppendLine($"{kvp.Key}={valueStr}");
                    }
                }
                sb.AppendLine();
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }
    }
}

