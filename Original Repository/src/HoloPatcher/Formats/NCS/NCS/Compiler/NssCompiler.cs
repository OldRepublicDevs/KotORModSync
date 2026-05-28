using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using KOTORModSync;
using KOTORModSync.Common.Script;
using KOTORModSync.Formats.NCS;
using KOTORModSync.Formats.NCS.Compiler.NSS;
using KOTORModSync.Formats.NCS.Optimizers;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Formats.NCS.Compiler
{

    /// <summary>
    /// NSS to NCS compiler.
    /// </summary>
    public class NssCompiler
    {
        private readonly Game _game;
        [CanBeNull]
        private readonly List<string> _libraryLookup;
        private readonly bool _debug;
        [CanBeNull]
        private readonly List<ScriptFunction> _functions;
        [CanBeNull]
        private readonly List<ScriptConstant> _constants;

        public NssCompiler(Game game, [CanBeNull] List<string> libraryLookup = null, bool debug = false,
            [CanBeNull] List<ScriptFunction> functions = null, [CanBeNull] List<ScriptConstant> constants = null)
        {
            _game = game;
            _libraryLookup = libraryLookup;
            _debug = debug;
            _functions = functions;
            _constants = constants;
        }

        /// <summary>
        /// Compile NSS source code to NCS bytecode.
        /// Implements selective symbol loading to match nwnnsscomp.exe behavior.
        /// </summary>
        public NCS Compile(string source, Dictionary<string, byte[]> library = null)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                throw new ArgumentException("Source cannot be null or empty", nameof(source));
            }

            // MATCHES nwnnsscomp.exe: Two-pass selective symbol loading
            // Pass 1: Analyze symbol usage to determine what needs to be included
            var symbolUsage = AnalyzeSymbolUsage(source);

            // Pass 2: Filter functions/constants to only include referenced symbols
            List<ScriptFunction> functions = FilterUsedFunctions(
                _functions ?? (_game.IsK1() ? ScriptDefs.KOTOR_FUNCTIONS : ScriptDefs.TSL_FUNCTIONS),
                symbolUsage.usedFunctions);

            List<ScriptConstant> constants = FilterUsedConstants(
                _constants ?? (_game.IsK1() ? ScriptDefs.KOTOR_CONSTANTS : ScriptDefs.TSL_CONSTANTS),
                symbolUsage.usedConstants);

            // Filter library to only include referenced include files
            var filteredLibrary = FilterLibrary(library, symbolUsage.includeFiles);

            var parser = new NssParser(functions, constants, filteredLibrary, _libraryLookup);
            CodeRoot root = parser.Parse(source);

            var ncs = new NCS();
            root.Compile(ncs);

            return ncs;
        }

        /// <summary>
        /// Analyze NSS source to determine which symbols are actually used.
        /// Matches nwnnsscomp.exe's selective loading behavior.
        /// </summary>
        private static SymbolUsage AnalyzeSymbolUsage(string source)
        {
            var usage = new SymbolUsage();
            var lines = source.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                // Parse #include directives
                if (trimmed.StartsWith("#include"))
                {
                    var includeFile = ExtractIncludeFileName(trimmed);
                    if (!string.IsNullOrEmpty(includeFile) && !usage.includeFiles.Contains(includeFile))
                    {
                        usage.includeFiles.Add(includeFile);
                    }
                }
                else
                {
                    // Look for function calls and constant usage
                    // This is a simplified analysis - in practice, nwnnsscomp.exe does full AST analysis
                    ExtractSymbolUsage(trimmed, usage);
                }
            }

            return usage;
        }

        private static string ExtractIncludeFileName(string includeLine)
        {
            var startQuote = includeLine.IndexOf('"');
            if (startQuote == -1) startQuote = includeLine.IndexOf('<');
            if (startQuote == -1) return null;

            var endChar = includeLine[startQuote] == '"' ? '"' : '>';
            var endQuote = includeLine.IndexOf(endChar, startQuote + 1);
            if (endQuote == -1) return null;

            var filename = includeLine.Substring(startQuote + 1, endQuote - startQuote - 1);
            return filename.EndsWith(".nss") ? filename.Substring(0, filename.Length - 4) : filename;
        }

        private static void ExtractSymbolUsage(string line, SymbolUsage usage)
        {
            // Simplified symbol extraction - looks for function calls and constants
            // nwnnsscomp.exe does full semantic analysis, but this approximates the behavior
            var words = line.Split(new[] { ' ', '(', ')', ',', ';', '=', '!', '+', '-', '*', '/', '%', '&', '|', '^', '<', '>', '?' },
                                 StringSplitOptions.RemoveEmptyEntries);

            foreach (var word in words)
            {
                if (!string.IsNullOrEmpty(word) && char.IsLetter(word[0]))
                {
                    // Check if it's a function call (followed by parentheses)
                    if (line.Contains(word + "("))
                    {
                        if (!usage.usedFunctions.Contains(word))
                            usage.usedFunctions.Add(word);
                    }
                    else
                    {
                        // Assume it's a constant if it's all uppercase or starts with specific prefixes
                        if (word.All(c => char.IsUpper(c) || char.IsDigit(c) || c == '_'))
                        {
                            if (!usage.usedConstants.Contains(word))
                                usage.usedConstants.Add(word);
                        }
                    }
                }
            }
        }

        private static List<ScriptFunction> FilterUsedFunctions(List<ScriptFunction> allFunctions, List<string> usedNames)
        {
            // Always include essential functions that nwnnsscomp.exe includes by default
            var essentialFunctions = new HashSet<string> {
                "main", "StartingConditional", "GetLastPerceived", "GetEnteringObject",
                "GetExitingObject", "GetIsDead", "GetHitDice", "GetTag", "GetName",
                "GetStringLength", "GetStringLeft", "GetStringRight", "GetStringMid",
                "IntToString", "FloatToString", "GetLocalInt", "SetLocalInt"
            };

            var filtered = new List<ScriptFunction>();
            foreach (var func in allFunctions)
            {
                if (essentialFunctions.Contains(func.Name) || usedNames.Contains(func.Name))
                {
                    filtered.Add(func);
                }
            }

            return filtered;
        }

        private static List<ScriptConstant> FilterUsedConstants(List<ScriptConstant> allConstants, List<string> usedNames)
        {
            // Always include essential constants
            var essentialConstants = new HashSet<string> {
                "TRUE", "FALSE", "OBJECT_INVALID", "OBJECT_SELF"
            };

            var filtered = new List<ScriptConstant>();
            foreach (var constant in allConstants)
            {
                if (essentialConstants.Contains(constant.Name) || usedNames.Contains(constant.Name))
                {
                    filtered.Add(constant);
                }
            }

            return filtered;
        }

        private static Dictionary<string, byte[]> FilterLibrary(Dictionary<string, byte[]> library, List<string> includeFiles)
        {
            if (library == null) return null;

            var filtered = new Dictionary<string, byte[]>();
            foreach (var includeFile in includeFiles)
            {
                if (library.ContainsKey(includeFile))
                {
                    filtered[includeFile] = library[includeFile];
                }
            }

            // Always include nwscript if available
            if (library.ContainsKey("nwscript"))
            {
                filtered["nwscript"] = library["nwscript"];
            }

            return filtered;
        }

        private class SymbolUsage
        {
            public List<string> usedFunctions = new List<string>();
            public List<string> usedConstants = new List<string>();
            public List<string> includeFiles = new List<string>();
        }
    }

    // NssParser is now in NSS/NssParser.cs
}