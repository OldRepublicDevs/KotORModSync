// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/NWScriptSyntaxHighlighter.java:28-365
// Original: public class NWScriptSyntaxHighlighter
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
    /// Syntax highlighter patterns and utilities for NWScript code.
    /// Provides comprehensive syntax highlighting patterns for NWScript language features including:
    /// - Keywords (int, void, float, string, object, etc.)
    /// - Control flow statements (if, else, while, for, switch, case, break, return, etc.)
    /// - Comments (single-line and multi-line)
    /// - Strings (single and double quoted)
    /// - Numbers (integers and floats)
    /// - Operators
    /// - Function calls
    /// 
    /// This class provides the core patterns and color schemes. UI implementations
    /// should use these patterns to apply syntax highlighting in their text editors.
    /// </summary>
    public static class NWScriptSyntaxHighlighter
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/NWScriptSyntaxHighlighter.java:30-36
        // Original: Color scheme
        // Color scheme (RGB values)
        public static readonly (int R, int G, int B) KeywordColor = (0, 0, 255); // Blue
        public static readonly (int R, int G, int B) TypeColor = (128, 0, 128); // Purple
        public static readonly (int R, int G, int B) StringColor = (0, 128, 0); // Green
        public static readonly (int R, int G, int B) CommentColor = (128, 128, 128); // Gray
        public static readonly (int R, int G, int B) NumberColor = (255, 0, 0); // Red
        public static readonly (int R, int G, int B) FunctionColor = (0, 128, 128); // Teal

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/NWScriptSyntaxHighlighter.java:39-43
        // Original: NWScript keywords
        public static readonly string[] Keywords = new string[]
        {
            "if", "else", "while", "for", "do", "switch", "case", "default", "break", "continue",
            "return", "void", "int", "float", "string", "object", "vector", "location", "effect",
            "event", "talent", "action", "const", "struct"
        };

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/NWScriptSyntaxHighlighter.java:45-49
        // Original: NWScript types
        public static readonly string[] Types = new string[]
        {
            "int", "float", "string", "object", "vector", "location", "effect", "event", "talent",
            "action", "void"
        };

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/NWScriptSyntaxHighlighter.java:52-68
        // Original: Patterns for different syntax elements
        // Fixed patterns to prevent catastrophic backtracking:
        // - Multi-line comments: use possessive quantifiers and negated character classes
        // - Strings: use possessive quantifiers to prevent excessive backtracking
        private static readonly string KeywordPattern = "\\b(" + string.Join("|", Keywords) + ")\\b";
        private static readonly string TypePattern = "\\b(" + string.Join("|", Types) + ")\\b";

        public static readonly Regex PatternCommentSingle = new Regex("//.*$", RegexOptions.Multiline | RegexOptions.Compiled);
        public static readonly Regex PatternCommentMulti = new Regex("/\\*(?:[^*]|\\*(?!/))*+\\*/", RegexOptions.Multiline | RegexOptions.Compiled);
        public static readonly Regex PatternStringDouble = new Regex("\"(?:[^\"\\\\]|\\\\.)*+\"", RegexOptions.Compiled);
        public static readonly Regex PatternStringSingle = new Regex("'(?:[^'\\\\]|\\\\.)*+'", RegexOptions.Compiled);
        public static readonly Regex PatternNumber = new Regex("\\b\\d+\\.?\\d*[fF]?\\b", RegexOptions.Compiled);
        public static readonly Regex PatternKeyword = new Regex(KeywordPattern, RegexOptions.Compiled);
        public static readonly Regex PatternType = new Regex(TypePattern, RegexOptions.Compiled);
        public static readonly Regex PatternFunction = new Regex("\\b([a-zA-Z_][a-zA-Z0-9_]*)\\s*+\\(", RegexOptions.Compiled);

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/NWScriptSyntaxHighlighter.java:70-71
        // Original: Maximum text size for highlighting (500KB - prevents regex catastrophic backtracking on huge files)
        public const int MaxHighlightSize = 500000;

        /// <summary>
        /// Gets all highlighting patterns in priority order (highest priority first).
        /// UI implementations should apply these patterns in this order.
        /// </summary>
        public static IEnumerable<(Regex Pattern, (int R, int G, int B) Color, bool Bold, bool Italic)> GetHighlightingPatterns()
        {
            // 1. Multi-line comments (highest priority)
            yield return (PatternCommentMulti, CommentColor, false, true);
            // 2. Single-line comments
            yield return (PatternCommentSingle, CommentColor, false, true);
            // 3. Double-quoted strings
            yield return (PatternStringDouble, StringColor, false, false);
            // 4. Single-quoted strings
            yield return (PatternStringSingle, StringColor, false, false);
            // 5. Numbers (only if not already styled)
            yield return (PatternNumber, NumberColor, false, false);
            // 6. Keywords (only if not already styled)
            yield return (PatternKeyword, KeywordColor, true, false);
            // 7. Types (only if not already styled)
            yield return (PatternType, TypeColor, true, false);
            // 8. Function calls (only if not already styled)
            // Note: Function pattern uses group 1 for the function name
            yield return (PatternFunction, FunctionColor, false, false);
        }

        /// <summary>
        /// Checks if a function name is actually a keyword (should not be highlighted as a function).
        /// </summary>
        public static bool IsKeyword(string functionName)
        {
            if (string.IsNullOrEmpty(functionName))
            {
                return false;
            }
            foreach (string keyword in Keywords)
            {
                if (keyword.Equals(functionName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
