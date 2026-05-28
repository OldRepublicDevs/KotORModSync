// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/NwnnsscompConfig.java
// Copyright 2021-2025 NCSDecomp
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/NwnnsscompConfig.java:18-186
    // Original: public class NwnnsscompConfig
    /// <summary>
    /// Resolves and formats command-line arguments for nwnnsscomp across versions.
    /// Different nwnnsscomp releases are not backwards compatible, so we detect the
    /// executable by SHA256 (HashUtil) and then hydrate the correct
    /// argument template from KnownExternalCompilers.
    /// </summary>
    public class NwnnsscompConfig
    {
        private readonly string sha256Hash;
        private readonly NcsFile sourceFile;
        private readonly NcsFile outputFile;
        private readonly NcsFile outputDir;
        private readonly string outputName;
        private readonly bool isK2;
        private readonly KnownExternalCompilers.CompilerInfo chosenCompiler;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/NwnnsscompConfig.java:44-64
        // Original: public NwnnsscompConfig(File compilerPath, File sourceFile, File outputFile, boolean isK2) throws IOException
        /// <summary>
        /// Creates a new configuration for nwnnsscomp execution.
        /// </summary>
        /// <param name="compilerPath">Path to the nwnnsscomp.exe file</param>
        /// <param name="sourceFile">The source file to compile/decompile</param>
        /// <param name="outputFile">The output file path</param>
        /// <param name="isK2">true for KotOR 2 (TSL), false for KotOR 1</param>
        /// <exception cref="IOException">If the compiler file cannot be read or hashed</exception>
        /// <exception cref="ArgumentException">If the compiler version is not recognized</exception>
        public NwnnsscompConfig(NcsFile compilerPath, NcsFile sourceFile, NcsFile outputFile, bool isK2)
        {
            this.sourceFile = sourceFile;
            // Convert to absolute path to ensure parent directory is always available
            NcsFile absoluteOutputFile = new NcsFile(outputFile.GetAbsolutePath());
            this.outputFile = absoluteOutputFile;
            this.outputDir = absoluteOutputFile.Directory != null ? new NcsFile(absoluteOutputFile.Directory) : null;
            this.outputName = absoluteOutputFile.Name;
            this.isK2 = isK2;

            // Calculate hash of the compiler executable
            this.sha256Hash = HashUtil.CalculateSHA256(compilerPath);

            // Look up the compiler version
            this.chosenCompiler = KnownExternalCompilers.FromSha256(this.sha256Hash);
            if (this.chosenCompiler == null)
            {
                throw new ArgumentException(
                    "Unknown compiler version with SHA256 hash: " + this.sha256Hash +
                    ". This compiler may not be supported. Please use a known version of nwnnsscomp.exe.");
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/NwnnsscompConfig.java:72-74
        // Original: public String[] getCompileArgs(String executable)
        /// <summary>
        /// Gets the formatted compile command-line arguments.
        /// </summary>
        /// <param name="executable">Path to the nwnnsscomp executable</param>
        /// <returns>Array of command-line arguments</returns>
        public string[] GetCompileArgs(string executable)
        {
            return FormatArgs(this.chosenCompiler.GetCompileArgs(), executable);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/NwnnsscompConfig.java:83-121
        // Original: public String[] getCompileArgs(String executable, java.util.List<File> includeDirs)
        /// <summary>
        /// Gets the formatted compile command-line arguments and appends include paths.
        /// </summary>
        /// <param name="executable">Path to the nwnnsscomp executable</param>
        /// <param name="includeDirs">Optional include directories to append via -i</param>
        /// <returns>Array of command-line arguments</returns>
        public string[] GetCompileArgs(string executable, List<NcsFile> includeDirs)
        {
            // Build include arguments array for {includes} placeholder
            List<string> includeArgs = new List<string>();
            if (includeDirs != null && includeDirs.Count > 0)
            {
                foreach (NcsFile dir in includeDirs)
                {
                    if (dir != null && dir.Exists())
                    {
                        includeArgs.Add("-i");
                        includeArgs.Add(dir.GetAbsolutePath());
                    }
                }
            }

            // Get base template args
            string[] template = this.chosenCompiler.GetCompileArgs();
            List<string> args = new List<string>();

            // Process template and expand {includes} placeholder
            foreach (string arg in template)
            {
                if (arg.Equals("{includes}"))
                {
                    // Insert include arguments at this position
                    args.AddRange(includeArgs);
                }
                else
                {
                    // Format the argument (replacing other placeholders)
                    string formatted = arg
                        .Replace("{source}", this.sourceFile.GetAbsolutePath())
                        .Replace("{output}", this.outputFile.GetAbsolutePath())
                        .Replace("{output_dir}", this.outputDir != null ? this.outputDir.GetAbsolutePath() : "")
                        .Replace("{output_name}", this.outputName)
                        .Replace("{game_value}", this.isK2 ? "2" : "1");
                    args.Add(formatted);
                }
            }

            // Prepend the executable path
            string[] result = new string[args.Count + 1];
            result[0] = executable;
            Array.Copy(args.ToArray(), 0, result, 1, args.Count);
            return result;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/NwnnsscompConfig.java:130-136
        // Original: public String[] getDecompileArgs(String executable)
        /// <summary>
        /// Gets the formatted decompile command-line arguments.
        /// </summary>
        /// <param name="executable">Path to the nwnnsscomp executable</param>
        /// <returns>Array of command-line arguments</returns>
        /// <exception cref="NotSupportedException">If decompilation is not supported</exception>
        public string[] GetDecompileArgs(string executable)
        {
            if (!this.chosenCompiler.SupportsDecompilation())
            {
                throw new NotSupportedException(
                    "Compiler '" + this.chosenCompiler.Name + "' does not support decompilation");
            }
            return FormatArgs(this.chosenCompiler.GetDecompileArgs(), executable);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/NwnnsscompConfig.java:146-167
        // Original: private String[] formatArgs(String[] argsList, String executable)
        /// <summary>
        /// Formats the argument template with actual values.
        /// </summary>
        /// <param name="argsList">The argument template array</param>
        /// <param name="executable">The executable path</param>
        /// <returns>Formatted argument array where placeholders are replaced</returns>
        private string[] FormatArgs(string[] argsList, string executable)
        {
            List<string> formatted = new List<string>();
            foreach (string arg in argsList)
            {
                string replaced = arg
                    .Replace("{source}", this.sourceFile.GetAbsolutePath())
                    .Replace("{output}", this.outputFile.GetAbsolutePath())
                    .Replace("{output_dir}", this.outputDir != null ? this.outputDir.GetAbsolutePath() : "")
                    .Replace("{output_name}", this.outputName)
                    .Replace("{game_value}", this.isK2 ? "2" : "1")
                    .Replace("{includes}", ""); // Remove {includes} placeholder when no includes provided
                // Only add non-empty arguments
                if (!string.IsNullOrEmpty(replaced))
                {
                    formatted.Add(replaced);
                }
            }

            // Prepend the executable path
            string[] result = new string[formatted.Count + 1];
            result[0] = executable;
            Array.Copy(formatted.ToArray(), 0, result, 1, formatted.Count);
            return result;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/NwnnsscompConfig.java:174-176
        // Original: public KnownExternalCompilers getChosenCompiler()
        public KnownExternalCompilers.CompilerInfo GetChosenCompiler()
        {
            return this.chosenCompiler;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/NwnnsscompConfig.java:183-185
        // Original: public String getSha256Hash()
        public string GetSha256Hash()
        {
            return this.sha256Hash;
        }
    }
}

