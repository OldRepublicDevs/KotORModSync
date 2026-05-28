using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using KOTORModSync;
using KOTORModSync.Common;
using KOTORModSync.Formats.NCS.Compiler;
using JetBrains.Annotations;

namespace KOTORModSync.Formats.NCS
{
    /// <summary>
    /// Shared utility methods for NCS compilers.
    /// </summary>
    internal static class CompilerUtils
    {
        internal static string DecodeBytesWithFallbacks(byte[] data)
        {
            // Try UTF-8 first
            try
            {
                string result = Encoding.UTF8.GetString(data);
                // Check if it's valid UTF-8 by trying to re-encode
                byte[] reencoded = Encoding.UTF8.GetBytes(result);
                if (reencoded.SequenceEqual(data))
                {
                    return result;
                }
            }
            catch
            {
                // Fall through to next encoding
            }

            // Fallback to Windows-1252
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                return Encoding.GetEncoding("windows-1252").GetString(data);
            }
            catch
            {
                // Final fallback to ASCII
                return Encoding.ASCII.GetString(data);
            }
        }
    }

    /// <summary>
    /// Known external NSS compilers and their configurations.
    /// </summary>
    public enum KnownExternalCompilers
    {
        TSLPATCHER,
        KOTOR_TOOL,
        V1,
        KOTOR_SCRIPTING_TOOL,
        NCSDecomp,
        XOREOS,
        KNSSCOMP
    }

    /// <summary>
    /// Configuration for an external compiler.
    /// </summary>
    public class ExternalCompilerConfig
    {
        public string Sha256 { get; set; }
        public string Name { get; set; }
        public DateTime ReleaseDate { get; set; }
        public string Author { get; set; }
        public Dictionary<string, List<string>> CommandLine { get; set; }

        public ExternalCompilerConfig(string sha256, string name, DateTime releaseDate, string author, Dictionary<string, List<string>> commandLine)
        {
            Sha256 = sha256;
            Name = name;
            ReleaseDate = releaseDate;
            Author = author;
            CommandLine = commandLine;
        }

        public static Dictionary<KnownExternalCompilers, ExternalCompilerConfig> GetAll()
        {
            return new Dictionary<KnownExternalCompilers, ExternalCompilerConfig>
            {
                [KnownExternalCompilers.TSLPATCHER] = new ExternalCompilerConfig(
                    "539EB689D2E0D3751AEED273385865278BEF6696C46BC0CAB116B40C3B2FE820",
                    "TSLPatcher",
                    new DateTime(2009, 1, 1),
                    "todo",
                    new Dictionary<string, List<string>>
                    {
                        ["compile"] = new List<string> { "-c", "{source}", "-o", "{output}" },
                        ["decompile"] = new List<string> { "-d", "{source}", "-o", "{output}" }
                    }
                ),
                [KnownExternalCompilers.KOTOR_TOOL] = new ExternalCompilerConfig(
                    "E36AA3172173B654AE20379888EDDC9CF45C62FBEB7AB05061C57B52961C824D",
                    "KOTOR Tool",
                    new DateTime(2005, 1, 1),
                    "Fred Tetra",
                    new Dictionary<string, List<string>>
                    {
                        ["compile"] = new List<string> { "-c", "--outputdir", "{output_dir}", "-o", "{output_name}", "-g", "{game_value}", "{source}" },
                        ["decompile"] = new List<string> { "-d", "--outputdir", "{output_dir}", "-o", "{output_name}", "-g", "{game_value}", "{source}" }
                    }
                ),
                [KnownExternalCompilers.V1] = new ExternalCompilerConfig(
                    "EC3E657C18A32AD13D28DA0AA3A77911B32D9661EA83CF0D9BCE02E1C4D8499D",
                    "v1.3 first public release",
                    new DateTime(2003, 12, 31),
                    "todo",
                    new Dictionary<string, List<string>>
                    {
                        ["compile"] = new List<string> { "-c", "{source}", "{output}" },
                        ["decompile"] = new List<string> { "-d", "{source}", "{output}" }
                    }
                ),
                [KnownExternalCompilers.KOTOR_SCRIPTING_TOOL] = new ExternalCompilerConfig(
                    "B7344408A47BE8780816CF68F5A171A09640AB47AD1A905B7F87DE30A50A0A92",
                    "KOTOR Scripting Tool",
                    new DateTime(2016, 5, 18),
                    "James Goad",
                    new Dictionary<string, List<string>>
                    {
                        ["compile"] = new List<string> { "-c", "--outputdir", "{output_dir}", "-o", "{output_name}", "-g", "{game_value}", "{source}" },
                        ["decompile"] = new List<string> { "-d", "--outputdir", "{output_dir}", "-o", "{output_name}", "-g", "{game_value}", "{source}" }
                    }
                ),
                [KnownExternalCompilers.NCSDecomp] = new ExternalCompilerConfig(
                    "539EB689D2E0D3751AEED273385865278BEF6696C46BC0CAB116B40C3B2FE820",
                    "NCSDecomp",
                    new DateTime(2006, 5, 30),
                    "todo",
                    new Dictionary<string, List<string>>()
                ),
                [KnownExternalCompilers.XOREOS] = new ExternalCompilerConfig(
                    "",
                    "Xoreos Tools",
                    new DateTime(2016, 1, 1),
                    "Xoreos Team",
                    new Dictionary<string, List<string>>()
                ),
                [KnownExternalCompilers.KNSSCOMP] = new ExternalCompilerConfig(
                    "",
                    "knsscomp",
                    new DateTime(2022, 1, 1),
                    "Nick Hugi",
                    new Dictionary<string, List<string>>
                    {
                        ["compile"] = new List<string> { "-c", "{source}", "-o", "{output}" },
                        ["decompile"] = new List<string>()
                    }
                )
            };
        }

        public static KnownExternalCompilers FromSha256(string sha256)
        {
            string upperSha256 = sha256.ToUpperInvariant();
            Dictionary<KnownExternalCompilers, ExternalCompilerConfig> allConfigs = GetAll();

            foreach (KeyValuePair<KnownExternalCompilers, ExternalCompilerConfig> kvp in allConfigs)
            {
                if (kvp.Value.Sha256 == upperSha256)
                {
                    return kvp.Key;
                }
            }

            throw new ArgumentException($"No compilers found with sha256 hash '{upperSha256}'");
        }
    }

    /// <summary>
    /// Configuration for nwnnsscomp execution.
    /// Unifies arguments passed to different nwnnsscomp.exe versions.
    /// </summary>
    public class NwnnsscompConfig
    {
        public string Sha256Hash { get; set; }
        public string SourceFile { get; set; }
        public string OutputFile { get; set; }
        public string OutputDir { get; set; }
        public string OutputName { get; set; }
        public Game Game { get; set; }
        public KnownExternalCompilers ChosenCompiler { get; set; }

        public NwnnsscompConfig(string sha256Hash, string sourceFile, string outputFile, Game game)
        {
            Sha256Hash = sha256Hash;
            SourceFile = sourceFile;
            OutputFile = outputFile;
            OutputDir = Path.GetDirectoryName(outputFile) ?? "";
            OutputName = Path.GetFileName(outputFile);
            Game = game;
            ChosenCompiler = ExternalCompilerConfig.FromSha256(sha256Hash);
        }

        public List<string> GetCompileArgs(string executable)
        {
            ExternalCompilerConfig config = ExternalCompilerConfig.GetAll()[ChosenCompiler];
            return FormatArgs(config.CommandLine["compile"], executable);
        }

        public List<string> GetDecompileArgs(string executable)
        {
            ExternalCompilerConfig config = ExternalCompilerConfig.GetAll()[ChosenCompiler];
            return FormatArgs(config.CommandLine["decompile"], executable);
        }

        private List<string> FormatArgs(List<string> argsList, string executable)
        {
            var formattedArgs = argsList.Select(arg => arg
                .Replace("{source}", SourceFile)
                .Replace("{output}", OutputFile)
                .Replace("{output_dir}", OutputDir)
                .Replace("{output_name}", OutputName)
                .Replace("{game_value}", Game.IsK1() ? "1" : "2"))
                .ToList();

            formattedArgs.Insert(0, executable);
            return formattedArgs;
        }
    }

    /// <summary>
    /// Built-in NSS to NCS compiler using PyKotor's native implementation.
    ///
    /// This compiler provides full NSS compilation without external dependencies,
    /// using the internal parser and code generator.
    /// </summary>
    public class InbuiltNCSCompiler : NCSCompiler
    {
        public override void CompileScript(
            string sourcePath,
            string outputPath,
            Game game,
            [CanBeNull] List<NCSOptimizer> optimizers = null,
            bool debug = false)
        {
            byte[] nssData = File.ReadAllBytes(sourcePath);
            string nssContents = CompilerUtils.DecodeBytesWithFallbacks(nssData);
            string parentDir = Path.GetDirectoryName(sourcePath);
            List<string> libraryLookup = parentDir != null ? new List<string> { parentDir } : new List<string>();
            NCS ncs = NCSAuto.CompileNss(nssContents, game, null, optimizers, libraryLookup, null, debug);
            NCSAuto.WriteNcs(ncs, outputPath);
        }

        /// <summary>
        /// Compiles and returns the NCS object (convenience method).
        /// </summary>
        public NCS CompileScriptToNcs(
            string sourcePath,
            string outputPath,
            [CanBeNull] Game game,
            List<NCSOptimizer> optimizers = null,
            bool debug = false)
        {
            byte[] nssData = File.ReadAllBytes(sourcePath);
            string nssContents = CompilerUtils.DecodeBytesWithFallbacks(nssData);
            string parentDir = Path.GetDirectoryName(sourcePath);
            List<string> libraryLookup = parentDir != null ? new List<string> { parentDir } : new List<string>();
            NCS ncs = NCSAuto.CompileNss(nssContents, game, null, optimizers, libraryLookup, null, debug);
            NCSAuto.WriteNcs(ncs, outputPath);
            return ncs;
        }
    }

    /// <summary>
    /// External NSS compiler wrapper for nwnnsscomp.exe.
    /// </summary>
    public class ExternalNCSCompiler : NCSCompiler
    {
        private string _nwnnsscompPath;
        private string _fileHash;

        public ExternalNCSCompiler(string nwnnsscompPath)
        {
            ChangeNwnnsscompPath(nwnnsscompPath);
        }

        public KnownExternalCompilers GetInfo()
        {
            return ExternalCompilerConfig.FromSha256(_fileHash);
        }

        public void ChangeNwnnsscompPath(string nwnnsscompPath)
        {
            _nwnnsscompPath = nwnnsscompPath;
            _fileHash = GenerateHash(nwnnsscompPath);
        }

        public NwnnsscompConfig Config(string sourceFile, string outputFile, Game game)
        {
            return new NwnnsscompConfig(_fileHash, sourceFile, outputFile, game);
        }

        public override void CompileScript(
            string sourcePath,
            string outputPath,
            Game game,
            [CanBeNull] List<NCSOptimizer> optimizers = null,
            bool debug = false)
        {
            // External compilers don't use optimizers (they're applied post-compilation)
            // Just ignore the optimizers parameter to match the abstract interface
            CompileScriptWithOutput(sourcePath, outputPath, game, 5);
        }

        /// <summary>
        /// Compiles an NSS script file to NCS bytecode and returns stdout/stderr.
        /// This is the original method signature from the Python version.
        /// </summary>
        public (string stdout, string stderr) CompileScriptWithOutput(
            string sourceFile,
            string outputFile,
            Game game,
            int timeout = 5)
        {
            if (!File.Exists(sourceFile))
            {
                throw new FileNotFoundException($"Source file not found: {sourceFile}");
            }

            if (!File.Exists(_nwnnsscompPath))
            {
                throw new InvalidOperationException($"Compiler executable not found: {_nwnnsscompPath}");
            }

            NwnnsscompConfig config = Config(sourceFile, outputFile, game);

            var startInfo = new ProcessStartInfo
            {
                FileName = _nwnnsscompPath,
                Arguments = string.Join(" ", config.GetCompileArgs(_nwnnsscompPath).Skip(1)),
                WorkingDirectory = Path.GetDirectoryName(sourceFile) ?? "",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using (var process = new Process { StartInfo = startInfo })
                {
                    var output = new StringBuilder();
                    var error = new StringBuilder();

                    process.OutputDataReceived += (sender, args) =>
                    {
                        if (!string.IsNullOrEmpty(args?.Data))
                        {
                            output.AppendLine(args.Data);
                        }
                    };

                    process.ErrorDataReceived += (sender, args) =>
                    {
                        if (!string.IsNullOrEmpty(args?.Data))
                        {
                            error.AppendLine(args.Data);
                        }
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    bool finished = process.WaitForExit(timeout * 1000);

                    if (!finished)
                    {
                        process.Kill();
                        throw new InvalidOperationException($"Compilation timed out after {timeout} seconds");
                    }

                    (string stdout, string stderr) = GetOutput(process.ExitCode, output.ToString(), error.ToString());

                    if (stdout.Contains("File is an include file, ignored"))
                    {
                        throw new EntryPointException("This file has no entry point and cannot be compiled (Most likely an include file).");
                    }

                    if (process.ExitCode != 0 && !string.IsNullOrEmpty(stderr))
                    {
                        throw new InvalidOperationException($"Compilation failed with return code {process.ExitCode}: {stderr}");
                    }

                    return (stdout, stderr);
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"Failed to run compiler: {e.Message}", e);
            }
        }

        public (string stdout, string stderr) DecompileScript(
            string sourceFile,
            string outputFile,
            Game game,
            int timeout = 5)
        {
            if (!File.Exists(sourceFile))
            {
                throw new FileNotFoundException($"Source file not found: {sourceFile}");
            }

            if (!File.Exists(_nwnnsscompPath))
            {
                throw new InvalidOperationException($"Compiler executable not found: {_nwnnsscompPath}");
            }

            NwnnsscompConfig config = Config(sourceFile, outputFile, game);

            ExternalCompilerConfig compilerConfig = ExternalCompilerConfig.GetAll()[config.ChosenCompiler];
            if (compilerConfig.CommandLine.GetValueOrDefault("decompile") == null || compilerConfig.CommandLine["decompile"].Count == 0)
            {
                throw new InvalidOperationException($"Compiler '{compilerConfig.Name}' does not support decompilation");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = _nwnnsscompPath,
                Arguments = string.Join(" ", config.GetDecompileArgs(_nwnnsscompPath).Skip(1)),
                WorkingDirectory = Path.GetDirectoryName(sourceFile) ?? "",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using (var process = new Process { StartInfo = startInfo })
                {
                    var output = new StringBuilder();
                    var error = new StringBuilder();

                    process.OutputDataReceived += (sender, args) =>
                    {
                        if (!string.IsNullOrEmpty(args?.Data))
                        {
                            output.AppendLine(args.Data);
                        }
                    };

                    process.ErrorDataReceived += (sender, args) =>
                    {
                        if (!string.IsNullOrEmpty(args?.Data))
                        {
                            error.AppendLine(args.Data);
                        }
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    bool finished = process.WaitForExit(timeout * 1000);

                    if (!finished)
                    {
                        process.Kill();
                        throw new InvalidOperationException($"Decompilation timed out after {timeout} seconds");
                    }

                    (string stdout, string stderr) = GetOutput(process.ExitCode, output.ToString(), error.ToString());

                    if (process.ExitCode != 0 && !string.IsNullOrEmpty(stderr))
                    {
                        throw new InvalidOperationException($"Decompilation failed with return code {process.ExitCode}: {stderr}");
                    }

                    return (stdout, stderr);
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"Failed to run decompiler: {e.Message}", e);
            }
        }

        private (string stdout, string stderr) GetOutput(int returnCode, string stdout, string stderr)
        {
            if (returnCode != 0 && string.IsNullOrWhiteSpace(stderr))
            {
                stderr = $"No error provided, but return code is nonzero: ({returnCode})";
            }

            if (stdout.Contains("Error:"))
            {
                List<string> stdoutLines = stdout.Split('\n').ToList();
                string errorLine = "";
                var filteredStdoutLines = new List<string>();

                foreach (string line in stdoutLines)
                {
                    if (line.Contains("Error:"))
                    {
                        errorLine += "\n" + line;
                    }
                    else
                    {
                        filteredStdoutLines.Add(line);
                    }
                }

                stdout = string.Join("\n", filteredStdoutLines);

                if (!string.IsNullOrEmpty(errorLine))
                {
                    stderr = string.IsNullOrEmpty(stderr) ? errorLine : stderr + "\n" + errorLine;
                }
            }

            return (stdout, stderr);
        }

        private static string GenerateHash(string filePath)
        {
            using (var sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(filePath))
            {
                byte[] hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
            }
        }

        /// <summary>
        /// Exception thrown when a script has no entry point.
        /// </summary>
        public class EntryPointException : Exception
        {
            public EntryPointException(string message) : base(message) { }
        }
    }
}
