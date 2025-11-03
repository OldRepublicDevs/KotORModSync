// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;

namespace KOTORModSync
{
    /// <summary>
    /// Holds command-line arguments passed to the application.
    ///
    /// Usage examples:
    /// --kotorPath="C:\Path\To\Game"
    /// --modDirectory="C:\Path\To\Mods"
    /// --instructionFile="C:\Path\To\Instructions.toml"
    /// </summary>
    public static class CLIArguments
    {
        /// <summary>
        /// Path to the KOTOR game directory (destination path).
        /// </summary>
        public static string KotorPath { get; set; }

        /// <summary>
        /// Path to the mod directory (source path).
        /// </summary>
        public static string ModDirectory { get; set; }

        /// <summary>
        /// Path to the instruction file to load automatically.
        /// </summary>
        public static string InstructionFile { get; set; }

        /// <summary>
        /// Parses command-line arguments.
        /// Supports formats: --kotorPath=value, --modDirectory=value, --instructionFile=value
        /// </summary>
        public static void Parse(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return;
            }

            foreach (string arg in args)
            {
                if (string.IsNullOrWhiteSpace(arg))
                {
                    continue;
                }

                // Skip argument names
                if (arg.StartsWith("--", StringComparison.OrdinalIgnoreCase) || arg.StartsWith("-", StringComparison.OrdinalIgnoreCase))
                {
                    // Support --key=value format
                    int equalsIndex = arg.IndexOf('=');
                    if (equalsIndex > 0)
                    {
                        string key = arg.Substring(0, equalsIndex).TrimStart('-', ' ');
                        string value = arg.Substring(equalsIndex + 1).Trim('"', '\'');

                        switch (key.ToLowerInvariant())
                        {
                            case "kotorpath":
                                KotorPath = value;
                                Core.Logger.Log($"CLI: Set KotorPath to '{value}'");
                                break;
                            case "moddirectory":
                                ModDirectory = value;
                                Core.Logger.Log($"CLI: Set ModDirectory to '{value}'");
                                break;
                            case "instructionfile":
                                InstructionFile = value;
                                Core.Logger.Log($"CLI: Set InstructionFile to '{value}'");
                                break;
                        }
                    }
                }
            }
        }
    }
}

