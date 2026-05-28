// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/KnownExternalCompilers.java
// Copyright 2021-2025 NCSDecomp
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/KnownExternalCompilers.java:26-216
    // Original: public enum KnownExternalCompilers
    /// <summary>
    /// Enumeration of known external NSS compilers and their command-line schemas.
    /// Each entry encapsulates the SHA256 fingerprint for a particular
    /// nwnnsscomp.exe (or compatible) build plus the exact argument templates
    /// required to compile or decompile.
    /// </summary>
    public static class KnownExternalCompilers
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/KnownExternalCompilers.java:30-37
        // Original: TSLPATCHER(...)
        public static readonly CompilerInfo TSLPATCHER = new CompilerInfo(
            "539EB689D2E0D3751AEED273385865278BEF6696C46BC0CAB116B40C3B2FE820",
            "TSLPatcher",
            new DateTime(2009, 1, 1),
            "todo",
            new string[] { "-c", "{source}", "-o", "{output}" },
            new string[] { "-d", "{source}", "-o", "{output}" }
        );

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/KnownExternalCompilers.java:42-49
        // Original: KOTOR_TOOL(...)
        public static readonly CompilerInfo KOTOR_TOOL = new CompilerInfo(
            "E36AA3172173B654AE20379888EDDC9CF45C62FBEB7AB05061C57B52961C824D",
            "KOTOR Tool",
            new DateTime(2005, 1, 1),
            "Fred Tetra",
            new string[] { "-c", "--outputdir", "{output_dir}", "-o", "{output_name}", "-g", "{game_value}", "{includes}", "{source}" },
            new string[] { "-d", "--outputdir", "{output_dir}", "-o", "{output_name}", "-g", "{game_value}", "{source}" }
        );

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/KnownExternalCompilers.java:54-61
        // Original: V1(...)
        public static readonly CompilerInfo V1 = new CompilerInfo(
            "EC3E657C18A32AD13D28DA0AA3A77911B32D9661EA83CF0D9BCE02E1C4D8499D",
            "v1.3 first public release",
            new DateTime(2003, 12, 31),
            "todo",
            new string[] { "-c", "{source}", "{output}" },
            new string[] { "-d", "{source}", "{output}" }
        );

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/KnownExternalCompilers.java:66-73
        // Original: KOTOR_SCRIPTING_TOOL(...)
        public static readonly CompilerInfo KOTOR_SCRIPTING_TOOL = new CompilerInfo(
            "B7344408A47BE8780816CF68F5A171A09640AB47AD1A905B7F87DE30A50A0A92",
            "KOTOR Scripting Tool",
            new DateTime(2016, 5, 18),
            "James Goad",
            new string[] { "-c", "--outputdir", "{output_dir}", "-o", "{output_name}", "-g", "{game_value}", "{includes}", "{source}" },
            new string[] { "-d", "--outputdir", "{output_dir}", "-o", "{output_name}", "-g", "{game_value}", "{source}" }
        );

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/KnownExternalCompilers.java:78-85
        // Original: XOREOS(...)
        public static readonly CompilerInfo XOREOS = new CompilerInfo(
            "",
            "Xoreos Tools",
            new DateTime(2016, 1, 1),
            "Xoreos Team",
            new string[] { },
            new string[] { }
        );

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/KnownExternalCompilers.java:90-97
        // Original: KNSSCOMP(...)
        public static readonly CompilerInfo KNSSCOMP = new CompilerInfo(
            "",
            "knsscomp",
            new DateTime(2022, 1, 1),
            "Nick Hugi",
            new string[] { "-c", "{source}", "-o", "{output}" },
            new string[] { }
        );

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/KnownExternalCompilers.java:101-109
        // Original:
        //   NCSDIS(
        //      "B1F398C2F64F4ACF2F39C417E7C7EB6F5483369BB95853C63A009F925A2E257C",
        //      "ncsdis",
        //      LocalDate.of(2020, 8, 3),
        //      "Unknown", // TODO: Identify original author
        //      new String[]{}, // ncsdis doesn't support compilation
        //      new String[]{"{source}", "{output}"} // ncsdis.exe <input.ncs> <output.pcode>
        //   );
        public static readonly CompilerInfo NCSDIS = new CompilerInfo(
            "B1F398C2F64F4ACF2F39C417E7C7EB6F5483369BB95853C63A009F925A2E257C",
            "ncsdis",
            new DateTime(2020, 8, 3),
            "Unknown", // Author not identified in original DeNCS source - ncsdis is a community tool
            new string[] { }, // ncsdis doesn't support compilation
            new string[] { "{source}", "{output}" } // ncsdis.exe <input.ncs> <output.pcode>
        );

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/KnownExternalCompilers.java:111-119
        // Original: private static final Map<String, KnownExternalCompilers> BY_HASH
        private static readonly Dictionary<string, CompilerInfo> BY_HASH = new Dictionary<string, CompilerInfo>();

        static KnownExternalCompilers()
        {
            CompilerInfo[] allCompilers = new CompilerInfo[]
            {
                TSLPATCHER,
                KOTOR_TOOL,
                V1,
                KOTOR_SCRIPTING_TOOL,
                XOREOS,
                KNSSCOMP
            };

            foreach (CompilerInfo compiler in allCompilers)
            {
                if (!string.IsNullOrEmpty(compiler.Sha256))
                {
                    BY_HASH[compiler.Sha256.ToUpperInvariant()] = compiler;
                }
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/KnownExternalCompilers.java:201-206
        // Original: public static KnownExternalCompilers fromSha256(String sha256)
        /// <summary>
        /// Looks up a compiler by its SHA256 hash.
        /// </summary>
        /// <param name="sha256">The SHA256 hash (case-insensitive)</param>
        /// <returns>The matching compiler, or null if not found</returns>
        public static CompilerInfo FromSha256(string sha256)
        {
            if (string.IsNullOrEmpty(sha256))
            {
                return null;
            }
            BY_HASH.TryGetValue(sha256.ToUpperInvariant(), out CompilerInfo result);
            return result;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/KnownExternalCompilers.java:114-188
        // Original: Immutable record of compiler metadata
        /// <summary>
        /// Immutable record of compiler metadata.
        /// </summary>
        public class CompilerInfo
        {
            public readonly string Sha256;
            public readonly string Name;
            public readonly DateTime ReleaseDate;
            public readonly string Author;
            public readonly string[] CompileArgs;
            public readonly string[] DecompileArgs;

            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/KnownExternalCompilers.java:131-139
            // Original: KnownExternalCompilers(...)
            public CompilerInfo(string sha256, string name, DateTime releaseDate, string author,
                string[] compileArgs, string[] decompileArgs)
            {
                this.Sha256 = sha256;
                this.Name = name;
                this.ReleaseDate = releaseDate;
                this.Author = author;
                this.CompileArgs = compileArgs ?? new string[0];
                this.DecompileArgs = decompileArgs ?? new string[0];
            }

            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/KnownExternalCompilers.java:182-184
            // Original: public String[] getCompileArgs()
            public string[] GetCompileArgs()
            {
                return (string[])this.CompileArgs.Clone();
            }

            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/KnownExternalCompilers.java:191-193
            // Original: public String[] getDecompileArgs()
            public string[] GetDecompileArgs()
            {
                return (string[])this.DecompileArgs.Clone();
            }

            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/KnownExternalCompilers.java:213-215
            // Original: public boolean supportsDecompilation()
            public bool SupportsDecompilation()
            {
                return this.DecompileArgs != null && this.DecompileArgs.Length > 0;
            }
        }
    }
}

