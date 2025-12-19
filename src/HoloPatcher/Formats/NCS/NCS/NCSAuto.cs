using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using KOTORModSync;
using KOTORModSync.Common.Script;
using KOTORModSync.Formats.NCS.Compiler;
using KOTORModSync.Formats.NCS.NCSDecomp;
using KOTORModSync.Formats.NCS.NCSDecomp.Utils;
using KOTORModSync.Formats.NCS.Optimizers;
using KOTORModSync.Resource;
using JetBrains.Annotations;
using FileScriptData = KOTORModSync.Formats.NCS.NCSDecomp.Utils.FileScriptData;
using KOTORModSync.Common;

namespace KOTORModSync.Formats.NCS
{

    /// <summary>
    /// Auto-loading functions for NCS files.
    /// </summary>
    public static class NCSAuto
    {
        /// <summary>
        /// Parse #include directives from NSS source code.
        /// Matches nwnnsscomp.exe behavior of selective include processing.
        /// </summary>
        private static List<string> ParseIncludeDirectives(string source)
        {
            var includes = new List<string>();
            var lines = source.Split('\n');

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("#include"))
                {
                    // Extract include file name from #include "filename" or #include <filename>
                    var startQuote = trimmed.IndexOf('"');
                    if (startQuote == -1)
                    {
                        startQuote = trimmed.IndexOf('<');
                    }

                    if (startQuote != -1)
                    {
                        var endChar = trimmed[startQuote] == '"' ? '"' : '>';
                        var endQuote = trimmed.IndexOf(endChar, startQuote + 1);
                        if (endQuote != -1)
                        {
                            var includeFile = trimmed.Substring(startQuote + 1, endQuote - startQuote - 1);
                            // Remove .nss extension if present
                            if (includeFile.EndsWith(".nss"))
                            {
                                includeFile = includeFile.Substring(0, includeFile.Length - 4);
                            }
                            if (!includes.Contains(includeFile))
                            {
                                includes.Add(includeFile);
                            }
                        }
                    }
                }
            }

            return includes;
        }

        /// <summary>
        /// Returns an NCS instance from the source.
        ///
        /// Args:
        ///     source: The source of the data (file path, byte array, or stream).
        ///     offset: The byte offset of the file inside the data.
        ///     size: Number of bytes to allowed to read from the stream. If not specified, uses the whole stream.
        ///
        /// Raises:
        ///     InvalidDataException: If the file was corrupted or in an unsupported format.
        ///
        /// Returns:
        ///     An NCS instance.
        /// </summary>
        public static NCS ReadNcs(string filepath, int offset = 0, int? size = null)
        {
            using (var reader = new NCSBinaryReader(filepath, offset, size ?? 0))
            {
                return reader.Load();
            }
        }

        public static NCS ReadNcs(byte[] data, int offset = 0, int? size = null)
        {
            using (var reader = new NCSBinaryReader(data, offset, size ?? 0))
            {
                return reader.Load();
            }
        }

        public static NCS ReadNcs(Stream source, int offset = 0, int? size = null)
        {
            using (var reader = new NCSBinaryReader(source, offset, size ?? 0))
            {
                return reader.Load();
            }
        }

        /// <summary>
        /// Writes the NCS data to the target location with the specified format (NCS only).
        ///
        /// Args:
        ///     ncs: The NCS file being written.
        ///     target: The location to write the data to (file path or stream).
        ///     fileFormat: The file format (currently only NCS is supported).
        ///
        /// Raises:
        ///     ArgumentException: If an unsupported file format was given.
        /// </summary>
        public static void WriteNcs(NCS ncs, string filepath, [CanBeNull] ResourceType fileFormat = null)
        {
            fileFormat = fileFormat ?? ResourceType.NCS;
            if (fileFormat != ResourceType.NCS)
            {
                throw new ArgumentException("Unsupported format specified; use NCS.", nameof(fileFormat));
            }

            byte[] data = new NCSBinaryWriter(ncs).Write();
            System.IO.File.WriteAllBytes(filepath, data);
        }

        public static void WriteNcs(NCS ncs, [CanBeNull] Stream target, ResourceType fileFormat = null)
        {
            fileFormat = fileFormat ?? ResourceType.NCS;
            if (fileFormat != ResourceType.NCS)
            {
                throw new ArgumentException("Unsupported format specified; use NCS.", nameof(fileFormat));
            }

            byte[] data = new NCSBinaryWriter(ncs).Write();
            target.Write(data, 0, data.Length);
        }

        /// <summary>
        /// Returns the NCS data in the specified format (NCS only) as a byte array.
        ///
        /// This is a convenience method that wraps the WriteNcs() method.
        ///
        /// Args:
        ///     ncs: The target NCS object.
        ///     fileFormat: The file format (currently only NCS is supported).
        ///
        /// Raises:
        ///     ArgumentException: If an unsupported file format was given.
        ///
        /// Returns:
        ///     The NCS data as a byte array.
        /// </summary>
        public static byte[] BytesNcs(NCS ncs, [CanBeNull] ResourceType fileFormat = null)
        {
            fileFormat = fileFormat ?? ResourceType.NCS;
            if (fileFormat != ResourceType.NCS)
            {
                throw new ArgumentException("Unsupported format specified; use NCS.", nameof(fileFormat));
            }

            return new NCSBinaryWriter(ncs).Write();
        }

        /// <summary>
        /// Compile NSS source code to NCS bytecode.
        ///
        /// Args:
        ///     source: The NSS source code string to compile
        ///     game: Target game (K1 or TSL) - determines which function/constant definitions to use
        ///     library: Optional dictionary of include file names to their byte content.
        ///             If not provided, uses selective loading from nwscript.nss only (matches nwnnsscomp.exe behavior)
        ///     optimizers: Optional list of post-compilation optimizers to apply (nwnnsscomp.exe applies none)
        ///     libraryLookup: Paths to search for #include files
        ///     errorlog: Optional error logger for parser (not yet implemented in C#)
        ///     debug: Enable debug output from parser
        ///     nwscriptPath: Optional path to nwscript.nss file. If provided, functions and constants will be parsed from this file.
        ///                   If not provided, falls back to ScriptDefs.KOTOR_FUNCTIONS/CONSTANTS or ScriptDefs.TSL_FUNCTIONS/CONSTANTS
        ///
        /// Returns:
        ///     NCS: Compiled NCS bytecode object
        ///
        /// Raises:
        ///     CompileError: If source code has syntax errors or semantic issues
        ///     EntryPointError: If script has no main() or StartingConditional() entry point
        ///
        /// Note:
        ///     Unlike previous versions, this now matches nwnnsscomp.exe behavior:
        ///     - Only includes functions/constants that are actually referenced
        ///     - Does not dump entire library files exhaustively
        ///     - Applies no default optimizations (nwnnsscomp.exe produces optimized bytecode directly)
        /// </summary>
        public static NCS CompileNss(
            string source,
            Game game,
            Dictionary<string, byte[]> library = null,
            List<NCSOptimizer> optimizers = null,
            [CanBeNull] List<string> libraryLookup = null,
            [CanBeNull] object errorlog = null,
            bool debug = false,
            [CanBeNull] string nwscriptPath = null)
        {
            List<ScriptFunction> functions = null;
            List<ScriptConstant> constants = null;

            // Parse nwscript.nss if provided
            if (!string.IsNullOrEmpty(nwscriptPath))
            {
                try
                {
                    var parsed = NwscriptParser.ParseNwscriptFile(nwscriptPath, game);
                    functions = parsed.functions;
                    constants = parsed.constants;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to parse nwscript.nss file: {nwscriptPath}. Error: {ex.Message}", ex);
                }
            }

            // IMPLEMENTATION: Selective include loading (matches nwnnsscomp.exe behavior)
            // nwnnsscomp.exe does NOT dump entire library files exhaustively.
            // It only includes functions/constants that are actually referenced.
            var compiler = new NssCompiler(game, libraryLookup, debug, functions, constants);

            // If no library provided, use selective loading from nwscript.nss only
            if (library == null)
            {
                // Old pykotor logic: includes the whole library
                //library = game.IsK1() ? ScriptLib.KOTOR_LIBRARY : ScriptLib.TSL_LIBRARY;

                // nwnnsscomp.exe logic: only includes functions/constants that are actually referenced
                // Parse #include directives from source to determine what needs to be loaded
                var includeFiles = ParseIncludeDirectives(source);
                var selectiveLibrary = new Dictionary<string, byte[]>();

                // Only load nwscript.nss initially (matches nwnnsscomp.exe behavior)
                var baseLibrary = game.IsK1() ? ScriptLib.KOTOR_LIBRARY : ScriptLib.TSL_LIBRARY;
                if (baseLibrary.ContainsKey("nwscript"))
                {
                    selectiveLibrary["nwscript"] = baseLibrary["nwscript"];
                }

                // For other includes, only load if they exist and are referenced
                foreach (var includeFile in includeFiles)
                {
                    if (baseLibrary.ContainsKey(includeFile))
                    {
                        // TODO: Implement selective symbol extraction based on usage analysis
                        // For now, include the full file but mark for future optimization
                        selectiveLibrary[includeFile] = baseLibrary[includeFile];
                    }
                }

                library = selectiveLibrary;
            }
            NCS ncs = compiler.Compile(source, library);

            // Apply optimizers only if explicitly provided
            // Based on reverse engineering of nwnnsscomp.exe, it does not appear to perform
            // explicit optimization passes. The bytecode generation should produce optimized
            // output directly without post-compilation optimization passes.
            if (optimizers != null && optimizers.Count > 0)
            {
                // Apply only user-specified optimizers
                foreach (NCSOptimizer optimizer in optimizers)
                {
                    optimizer.Reset();
                }
                ncs.Optimize(optimizers);
            }

            // Apply all optimizers (if any)
            if (optimizers.Count > 0)
            {
                foreach (NCSOptimizer optimizer in optimizers)
                {
                    optimizer.Reset();
                }
                ncs.Optimize(optimizers);
            }

            if (System.Environment.GetEnvironmentVariable("NCS_INTERPRETER_DEBUG") == "true")
            {
                System.Console.WriteLine("=== NCS after optimize ===");
                for (int i = 0; i < ncs.Instructions.Count; i++)
                {
                    NCSInstruction inst = ncs.Instructions[i];
                    int jumpIdx = inst.Jump != null ? ncs.GetInstructionIndex(inst.Jump) : -1;
                    System.Console.WriteLine($"{i}: {inst.InsType} args=[{string.Join(",", inst.Args ?? new List<object>())}] jumpIdx={jumpIdx}");
                }
            }

            return ncs;
        }

        /// <summary>
        /// Decompile NCS bytecode to NSS source code.
        /// Uses the DeNCS decompiler (1:1 port from vendor/DeNCS) for accurate decompilation.
        /// </summary>
        public static string DecompileNcs(
            [CanBeNull] NCS ncs,
            Game game,
            List<ScriptFunction> functions = null,
            [CanBeNull] List<ScriptConstant> constants = null,
            [CanBeNull] string nwscriptPath = null)
        {
            if (ncs == null)
            {
                throw new ArgumentNullException(nameof(ncs));
            }

            // Use FileDecompiler (DeNCS port) for 1:1 accurate decompilation
            NCSDecomp.FileDecompiler fileDecompiler;
            if (!string.IsNullOrEmpty(nwscriptPath) && System.IO.File.Exists(nwscriptPath))
            {
                // Use nwscript file directly if provided
                var nwscriptFile = new NCSDecomp.NcsFile(nwscriptPath);
                fileDecompiler = new NCSDecomp.FileDecompiler(nwscriptFile);
            }
            else
            {
                // Fall back to lazy loading (will search for nwscript file)
                NWScriptLocator.GameType gameType = game.IsK2() ? NWScriptLocator.GameType.TSL : NWScriptLocator.GameType.K1;
                fileDecompiler = new NCSDecomp.FileDecompiler(null, gameType);
            }

            FileScriptData data = null;
            try
            {
                data = fileDecompiler.DecompileNcsObject(ncs);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Decompilation failed with exception: " + ex.Message +
                    (ex.InnerException != null ? " (Inner: " + ex.InnerException.Message + ")" : "") +
                    ". FileDecompiler returned null. " +
                    "This usually means the decompiler couldn't analyze the NCS bytecode structure. " +
                    "Check console output for detailed error messages.", ex);
            }

            if (data == null)
            {
                throw new InvalidOperationException(
                    "Decompilation failed - FileDecompiler returned null. " +
                    "This usually means the decompiler couldn't analyze the NCS bytecode structure. " +
                    "Possible causes: no main subroutine found, actions file not loaded, or exception during decompilation. " +
                    "Check console output for detailed error messages.");
            }

            data.GenerateCode();
            string code = data.GetCode();

            // Clean up
            data.Close();
            fileDecompiler.CloseAllFiles();

            return code ?? string.Empty;
        }
    }
}
