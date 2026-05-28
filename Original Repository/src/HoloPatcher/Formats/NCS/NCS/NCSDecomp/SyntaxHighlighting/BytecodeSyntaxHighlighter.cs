// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/BytecodeSyntaxHighlighter.java:17-282
// Original: public class BytecodeSyntaxHighlighter
// Copyright 2021-2025 NCSDecomp
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using KOTORModSync.Common;

namespace KOTORModSync.Formats.NCS.NCSDecomp.SyntaxHighlighting
{
    /// <summary>
    /// Syntax highlighter patterns and utilities for NCS bytecode output.
    /// Provides syntax highlighting patterns for bytecode format including:
    /// - Instruction opcodes (JSR, RETN, CONSTI, CPDOWNSP, MOVSP, etc.)
    /// - Hexadecimal addresses and values
    /// - Function names (fn_*)
    /// - Type indicators (T, I, F, S, O, etc.)
    /// 
    /// This class provides the core patterns and color schemes. UI implementations
    /// should use these patterns to apply syntax highlighting in their text editors.
    /// </summary>
    public static class BytecodeSyntaxHighlighter
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/BytecodeSyntaxHighlighter.java:27-32
        // Original: Color scheme constants
        // Color scheme (RGB values)
        public static readonly (int R, int G, int B) InstructionColor = (0, 0, 255); // Blue
        public static readonly (int R, int G, int B) AddressColor = (128, 128, 128); // Gray
        public static readonly (int R, int G, int B) HexValueColor = (0, 128, 0); // Green
        public static readonly (int R, int G, int B) FunctionColor = (128, 0, 128); // Purple
        public static readonly (int R, int G, int B) TypeColor = (255, 140, 0); // Orange

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/BytecodeSyntaxHighlighter.java:35-44
        // Original: Bytecode instruction patterns
        public static readonly string[] Instructions = new string[]
        {
            "CPDOWNSP", "RSADD", "RSADDI", "CPTOPSP", "CONST", "CONSTI", "CONSTF", "CONSTS", "ACTION",
            "LOGANDII", "LOGORII", "INCORII", "EXCORII", "BOOLANDII",
            "EQUAL", "NEQUAL", "GEQ", "GT", "LT", "LEQ",
            "SHLEFTII", "SHRIGHTII", "USHRIGHTII",
            "ADD", "SUB", "MUL", "DIV", "MOD", "NEG", "COMP",
            "MOVSP", "STATEALL", "JMP", "JSR", "JZ", "JNZ", "RETN", "DESTRUCT",
            "NOT", "DECISP", "INCISP", "CPDOWNBP", "CPTOPBP", "DECIBP", "INCIBP",
            "SAVEBP", "RESTOREBP", "STORE_STATE", "NOP", "T"
        };

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/BytecodeSyntaxHighlighter.java:47-54
        // Original: Patterns for different bytecode elements
        private static readonly string InstructionPattern = "\\b(" + string.Join("|", Instructions) + ")\\b";
        public static readonly Regex PatternInstruction = new Regex(InstructionPattern, RegexOptions.Compiled);
        public static readonly Regex PatternAddress = new Regex("\\b[0-9A-Fa-f]{8}\\b", RegexOptions.Compiled);
        public static readonly Regex PatternHexValue = new Regex("\\b[0-9A-Fa-f]{4,}\\b", RegexOptions.Compiled);
        public static readonly Regex PatternFunction = new Regex("\\bfn_[0-9A-Fa-f]+\\b", RegexOptions.Compiled);
        public static readonly Regex PatternType = new Regex("\\bT\\s+[0-9A-Fa-f]+\\b", RegexOptions.Compiled);

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/BytecodeSyntaxHighlighter.java:56-57
        // Original: Maximum text size for highlighting (500KB - prevents regex catastrophic backtracking on huge files)
        public const int MaxHighlightSize = 500000;

        /// <summary>
        /// Gets all highlighting patterns in priority order (highest priority first).
        /// UI implementations should apply these patterns in this order.
        /// </summary>
        public static IEnumerable<(Regex Pattern, (int R, int G, int B) Color, bool Bold)> GetHighlightingPatterns()
        {
            // 1. Functions (highest priority - most specific)
            yield return (PatternFunction, FunctionColor, true);
            // 2. Type indicators (T followed by hex)
            yield return (PatternType, TypeColor, true);
            // 3. Instructions
            yield return (PatternInstruction, InstructionColor, true);
            // 4. Addresses (8 hex digits)
            yield return (PatternAddress, AddressColor, false);
            // 5. Other hex values (4+ hex digits, but not already styled as addresses)
            yield return (PatternHexValue, HexValueColor, false);
        }
    }
}
