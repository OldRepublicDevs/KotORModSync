using System;
using System.Collections.Generic;
using System.Linq;

namespace KOTORModSync.Formats.NCS.Compiler
{
    /// <summary>
    /// NSS (NWScript Source) lexer/tokenizer.
    /// Adapted from nss2csharp Lexer_Nss.cs
    /// </summary>
    public class NssLexer
    {
        public List<NssTokenBase> Tokens { get; private set; }
        public List<NssLexDebugRange> DebugRanges { get; set; }

        public int Analyse(string data)
        {
            Tokens = new List<NssTokenBase>();
            DebugRanges = new List<NssLexDebugRange>();

            // Set up the debug data per line
            {
                int lineNum = 0;
                int cumulativeLen = 0;
                foreach (string line in data.Split('\n'))
                {
                    NssLexDebugRange range = new NssLexDebugRange();
                    range.Line = lineNum;
                    range.IndexStart = cumulativeLen;
                    range.IndexEnd = cumulativeLen + line.Length;
                    DebugRanges.Add(range);

                    lineNum = range.Line + 1;
                    cumulativeLen = range.IndexEnd + 1;
                }
            }

            int chBaseIndex = 0;
            while (chBaseIndex < data.Length)
            {
                int chBaseIndexLast = chBaseIndex;

                // PREPROCESSOR
                {
                    chBaseIndex = Preprocessor(chBaseIndex, data);
                    if (chBaseIndex != chBaseIndexLast) continue;
                }

                // COMMENTS
                {
                    chBaseIndex = Comment(chBaseIndex, data);
                    if (chBaseIndex != chBaseIndexLast) continue;
                }

                // SEPARATORS
                {
                    chBaseIndex = Separator(chBaseIndex, data);
                    if (chBaseIndex != chBaseIndexLast) continue;
                }

                // OPERATORS
                {
                    chBaseIndex = Operator(chBaseIndex, data);
                    if (chBaseIndex != chBaseIndexLast) continue;
                }

                // LITERALS
                {
                    chBaseIndex = Literal(chBaseIndex, data);
                    if (chBaseIndex != chBaseIndexLast) continue;
                }

                // KEYWORDS
                {
                    chBaseIndex = Keyword(chBaseIndex, data);
                    if (chBaseIndex != chBaseIndexLast) continue;
                }

                // IDENTIFIERS
                {
                    chBaseIndex = Identifier(chBaseIndex, data);
                    if (chBaseIndex != chBaseIndexLast) continue;
                }

                return 1; // Error: couldn't tokenize
            }

            // Remove any empty identifiers that may have slipped through
            Tokens.RemoveAll(t => t is NssIdentifier ident && string.IsNullOrEmpty(ident.Identifier));

            return 0; // Success
        }

        private int Preprocessor(int chBaseIndex, string data)
        {
            char ch = data[chBaseIndex];
            if (ch == '#')
            {
                // Just scan for a new line or eof, then add this in.
                int chScanningIndex = chBaseIndex;

                while (++chScanningIndex <= data.Length)
                {
                    bool eof = chScanningIndex >= data.Length - 1;

                    bool proceed = eof;
                    if (!proceed)
                    {
                        char chScanning = data[chScanningIndex];
                        proceed = NssSeparator.SeparatorMap.ContainsKey(chScanning) &&
                            NssSeparator.SeparatorMap[chScanning] == NssSeparators.NewLine;
                    }

                    if (proceed)
                    {
                        NssPreprocessor preprocessor = new NssPreprocessor();
                        preprocessor.PreprocessorType = NssPreprocessorType.Unknown;

                        int chStartIndex = chBaseIndex;
                        int chEndIndex = eof ? data.Length : chScanningIndex;

                        if (chStartIndex == chEndIndex)
                        {
                            preprocessor.Data = "";
                        }
                        else
                        {
                            preprocessor.Data = data.Substring(chStartIndex, chEndIndex - chStartIndex);
                        }

                        // Check for #include
                        string trimmed = preprocessor.Data.TrimStart('#').TrimStart();
                        if (trimmed.StartsWith("include", StringComparison.OrdinalIgnoreCase))
                        {
                            preprocessor.PreprocessorType = NssPreprocessorType.Include;
                        }

                        int chNewBaseIndex = chEndIndex;
                        AttachDebugData(preprocessor, DebugRanges, chBaseIndex, chNewBaseIndex - 1);

                        Tokens.Add(preprocessor);
                        chBaseIndex = chNewBaseIndex;
                        break;
                    }
                }
            }

            return chBaseIndex;
        }

        private int Comment(int chBaseIndex, string data)
        {
            char ch = data[chBaseIndex];
            if (ch == '/')
            {
                int chNextIndex = chBaseIndex + 1;
                if (chNextIndex < data.Length)
                {
                    char nextCh = data[chNextIndex];
                    if (nextCh == '/')
                    {
                        // Line comment
                        int chScanningIndex = chNextIndex;

                        while (++chScanningIndex <= data.Length)
                        {
                            bool eof = chScanningIndex >= data.Length - 1;

                            bool proceed = eof;
                            if (!proceed)
                            {
                                char chScanning = data[chScanningIndex];
                                proceed = NssSeparator.SeparatorMap.ContainsKey(chScanning) &&
                                    NssSeparator.SeparatorMap[chScanning] == NssSeparators.NewLine;
                            }

                            if (proceed)
                            {
                                NssComment comment = new NssComment();
                                comment.CommentType = NssCommentType.LineComment;

                                int chStartIndex = chNextIndex + 1;
                                int chEndIndex = eof ? data.Length : chScanningIndex;

                                if (chStartIndex == chEndIndex)
                                {
                                    comment.Comment = "";
                                }
                                else
                                {
                                    comment.Comment = data.Substring(chStartIndex, chEndIndex - chStartIndex);
                                }

                                int chNewBaseIndex = chEndIndex;
                                AttachDebugData(comment, DebugRanges, chBaseIndex, chNewBaseIndex - 1);

                                Tokens.Add(comment);
                                chBaseIndex = chNewBaseIndex;
                                break;
                            }
                        }
                    }
                    else if (nextCh == '*')
                    {
                        // Block comment
                        bool terminated = false;
                        int chScanningIndex = chNextIndex + 1;
                        while (++chScanningIndex < data.Length)
                        {
                            char chScanning = data[chScanningIndex];
                            if (chScanning == '/')
                            {
                                char chScanningLast = data[chScanningIndex - 1];
                                if (chScanningLast == '*')
                                {
                                    terminated = true;
                                    break;
                                }
                            }
                        }

                        bool eof = chScanningIndex >= data.Length - 1;

                        NssComment comment = new NssComment();
                        comment.CommentType = NssCommentType.BlockComment;
                        comment.Terminated = terminated;

                        int chStartIndex = chBaseIndex + 2;
                        int chEndIndex = !terminated && eof ? data.Length : chScanningIndex + (terminated ? -1 : 0);
                        comment.Comment = data.Substring(chStartIndex, chEndIndex - chStartIndex);

                        int chNewBaseIndex = eof ? data.Length : chScanningIndex + 1;
                        AttachDebugData(comment, DebugRanges, chBaseIndex, chNewBaseIndex - 1);

                        Tokens.Add(comment);
                        chBaseIndex = chNewBaseIndex;
                    }
                }
            }

            return chBaseIndex;
        }

        private int Separator(int chBaseIndex, string data)
        {
            char ch = data[chBaseIndex];

            if (NssSeparator.SeparatorMap.ContainsKey(ch))
            {
                NssSeparator separator = new NssSeparator();
                separator.Separator = NssSeparator.SeparatorMap[ch];

                int chNewBaseIndex = chBaseIndex + 1;
                AttachDebugData(separator, DebugRanges, chBaseIndex, chNewBaseIndex - 1);

                Tokens.Add(separator);
                chBaseIndex = chNewBaseIndex;
            }

            return chBaseIndex;
        }

        private int Operator(int chBaseIndex, string data)
        {
            char ch = data[chBaseIndex];
            char nextCh = (chBaseIndex + 1 < data.Length) ? data[chBaseIndex + 1] : '\0';
            char nextNextCh = (chBaseIndex + 2 < data.Length) ? data[chBaseIndex + 2] : '\0';
            char nextNextNextCh = (chBaseIndex + 3 < data.Length) ? data[chBaseIndex + 3] : '\0';

            // Check for two-character operators first
            // Note: check longest matches first
            if (ch == '>' && nextCh == '>' && nextNextCh == '>' && nextNextNextCh == '=')
            {
                NssOperator op = new NssOperator();
                op.Operator = NssOperators.BitwiseUnsignedRightAssignment;
                int chNewBaseIndex = chBaseIndex + 4;
                AttachDebugData(op, DebugRanges, chBaseIndex, chNewBaseIndex - 1);
                Tokens.Add(op);
                return chNewBaseIndex;
            }
            if (ch == '>' && nextCh == '>' && nextNextCh == '=')
            {
                NssOperator op = new NssOperator();
                op.Operator = NssOperators.BitwiseRightAssignment;
                int chNewBaseIndex = chBaseIndex + 3;
                AttachDebugData(op, DebugRanges, chBaseIndex, chNewBaseIndex - 1);
                Tokens.Add(op);
                return chNewBaseIndex;
            }
            if (ch == '<' && nextCh == '<' && nextNextCh == '=')
            {
                NssOperator op = new NssOperator();
                op.Operator = NssOperators.BitwiseLeftAssignment;
                int chNewBaseIndex = chBaseIndex + 3;
                AttachDebugData(op, DebugRanges, chBaseIndex, chNewBaseIndex - 1);
                Tokens.Add(op);
                return chNewBaseIndex;
            }
            if (ch == '&' && nextCh == '=')
            {
                NssOperator op = new NssOperator();
                op.Operator = NssOperators.BitwiseAndAssignment;
                int chNewBaseIndex = chBaseIndex + 2;
                AttachDebugData(op, DebugRanges, chBaseIndex, chNewBaseIndex - 1);
                Tokens.Add(op);
                return chNewBaseIndex;
            }
            if (ch == '|' && nextCh == '=')
            {
                NssOperator op = new NssOperator();
                op.Operator = NssOperators.BitwiseOrAssignment;
                int chNewBaseIndex = chBaseIndex + 2;
                AttachDebugData(op, DebugRanges, chBaseIndex, chNewBaseIndex - 1);
                Tokens.Add(op);
                return chNewBaseIndex;
            }
            if (ch == '^' && nextCh == '=')
            {
                NssOperator op = new NssOperator();
                op.Operator = NssOperators.BitwiseXorAssignment;
                int chNewBaseIndex = chBaseIndex + 2;
                AttachDebugData(op, DebugRanges, chBaseIndex, chNewBaseIndex - 1);
                Tokens.Add(op);
                return chNewBaseIndex;
            }
            if (ch == '%' && nextCh == '=')
            {
                NssOperator op = new NssOperator();
                op.Operator = NssOperators.ModuloAssignment;
                int chNewBaseIndex = chBaseIndex + 2;
                AttachDebugData(op, DebugRanges, chBaseIndex, chNewBaseIndex - 1);
                Tokens.Add(op);
                return chNewBaseIndex;
            }
            if (ch == '+' && nextCh == '=')
            {
                NssOperator op = new NssOperator();
                op.Operator = NssOperators.AdditionAssignment;
                int chNewBaseIndex = chBaseIndex + 2;
                AttachDebugData(op, DebugRanges, chBaseIndex, chNewBaseIndex - 1);
                Tokens.Add(op);
                return chNewBaseIndex;
            }
            if (ch == '-' && nextCh == '=')
            {
                NssOperator op = new NssOperator();
                op.Operator = NssOperators.SubtractionAssignment;
                int chNewBaseIndex = chBaseIndex + 2;
                AttachDebugData(op, DebugRanges, chBaseIndex, chNewBaseIndex - 1);
                Tokens.Add(op);
                return chNewBaseIndex;
            }
            if (ch == '*' && nextCh == '=')
            {
                NssOperator op = new NssOperator();
                op.Operator = NssOperators.MultiplicationAssignment;
                int chNewBaseIndex = chBaseIndex + 2;
                AttachDebugData(op, DebugRanges, chBaseIndex, chNewBaseIndex - 1);
                Tokens.Add(op);
                return chNewBaseIndex;
            }
            if (ch == '/' && nextCh == '=')
            {
                NssOperator op = new NssOperator();
                op.Operator = NssOperators.DivisionAssignment;
                int chNewBaseIndex = chBaseIndex + 2;
                AttachDebugData(op, DebugRanges, chBaseIndex, chNewBaseIndex - 1);
                Tokens.Add(op);
                return chNewBaseIndex;
            }
            if (ch == '%' && nextCh == '=')
            {
                NssOperator op = new NssOperator();
                op.Operator = NssOperators.ModuloAssignment;
                int chNewBaseIndex = chBaseIndex + 2;
                AttachDebugData(op, DebugRanges, chBaseIndex, chNewBaseIndex - 1);
                Tokens.Add(op);
                return chNewBaseIndex;
            }
            if (ch == '&' && nextCh == '=')
            {
                NssOperator op = new NssOperator();
                op.Operator = NssOperators.BitwiseAndAssignment;
                int chNewBaseIndex = chBaseIndex + 2;
                AttachDebugData(op, DebugRanges, chBaseIndex, chNewBaseIndex - 1);
                Tokens.Add(op);
                return chNewBaseIndex;
            }
            if (ch == '|' && nextCh == '=')
            {
                NssOperator op = new NssOperator();
                op.Operator = NssOperators.BitwiseOrAssignment;
                int chNewBaseIndex = chBaseIndex + 2;
                AttachDebugData(op, DebugRanges, chBaseIndex, chNewBaseIndex - 1);
                Tokens.Add(op);
                return chNewBaseIndex;
            }
            if (ch == '^' && nextCh == '=')
            {
                NssOperator op = new NssOperator();
                op.Operator = NssOperators.BitwiseXorAssignment;
                int chNewBaseIndex = chBaseIndex + 2;
                AttachDebugData(op, DebugRanges, chBaseIndex, chNewBaseIndex - 1);
                Tokens.Add(op);
                return chNewBaseIndex;
            }
            if (ch == '<' && nextCh == '<')
            {
                char nextNext = chBaseIndex + 2 < data.Length ? data[chBaseIndex + 2] : '\0';
                if (nextNext == '=')
                {
                    NssOperator op = new NssOperator();
                    op.Operator = NssOperators.BitwiseLeftAssignment;
                    int chNewBaseIndex = chBaseIndex + 3;
                    AttachDebugData(op, DebugRanges, chBaseIndex, chNewBaseIndex - 1);
                    Tokens.Add(op);
                    return chNewBaseIndex;
                }
            }
            if (ch == '>' && nextCh == '>')
            {
                char nextNext = chBaseIndex + 2 < data.Length ? data[chBaseIndex + 2] : '\0';
                if (nextNext == '=')
                {
                    NssOperator op = new NssOperator();
                    op.Operator = NssOperators.BitwiseRightAssignment;
                    int chNewBaseIndex = chBaseIndex + 3;
                    AttachDebugData(op, DebugRanges, chBaseIndex, chNewBaseIndex - 1);
                    Tokens.Add(op);
                    return chNewBaseIndex;
                }
                if (nextNext == '>')
                {
                    char nextNextNext = chBaseIndex + 3 < data.Length ? data[chBaseIndex + 3] : '\0';
                    if (nextNextNext == '=')
                    {
                        NssOperator op = new NssOperator();
                        op.Operator = NssOperators.BitwiseUnsignedRightAssignment;
                        int chNewBaseIndex = chBaseIndex + 4;
                        AttachDebugData(op, DebugRanges, chBaseIndex, chNewBaseIndex - 1);
                        Tokens.Add(op);
                        return chNewBaseIndex;
                    }
                }
            }
            // Comparison and logical multi-character operators
            if (ch == '<' && nextCh == '=')
            {
                NssOperator op = new NssOperator();
                op.Operator = NssOperators.LessThanOrEqual;
                int chNewBaseIndex = chBaseIndex + 2;
                AttachDebugData(op, DebugRanges, chBaseIndex, chNewBaseIndex - 1);
                Tokens.Add(op);
                return chNewBaseIndex;
            }
            if (ch == '>' && nextCh == '=')
            {
                NssOperator op = new NssOperator();
                op.Operator = NssOperators.GreaterThanOrEqual;
                int chNewBaseIndex = chBaseIndex + 2;
                AttachDebugData(op, DebugRanges, chBaseIndex, chNewBaseIndex - 1);
                Tokens.Add(op);
                return chNewBaseIndex;
            }
            if (ch == '=' && nextCh == '=')
            {
                NssOperator op = new NssOperator();
                op.Operator = NssOperators.Equals;
                int chNewBaseIndex = chBaseIndex + 2;
                AttachDebugData(op, DebugRanges, chBaseIndex, chNewBaseIndex - 1);
                Tokens.Add(op);
                return chNewBaseIndex;
            }
            if (ch == '!' && nextCh == '=')
            {
                NssOperator op = new NssOperator();
                op.Operator = NssOperators.NotEqual;
                int chNewBaseIndex = chBaseIndex + 2;
                AttachDebugData(op, DebugRanges, chBaseIndex, chNewBaseIndex - 1);
                Tokens.Add(op);
                return chNewBaseIndex;
            }
            if (ch == '&' && nextCh == '&')
            {
                NssOperator op = new NssOperator();
                op.Operator = NssOperators.And;
                int chNewBaseIndex = chBaseIndex + 2;
                AttachDebugData(op, DebugRanges, chBaseIndex, chNewBaseIndex - 1);
                Tokens.Add(op);
                return chNewBaseIndex;
            }
            if (ch == '|' && nextCh == '|')
            {
                NssOperator op = new NssOperator();
                op.Operator = NssOperators.Or;
                int chNewBaseIndex = chBaseIndex + 2;
                AttachDebugData(op, DebugRanges, chBaseIndex, chNewBaseIndex - 1);
                Tokens.Add(op);
                return chNewBaseIndex;
            }
            // Do not special-case '<<' or '>>' without '='; let single '<'/'>' be tokenized separately.

            // Single-character operators
            if (NssOperator.OperatorMap.ContainsKey(ch))
            {
                NssOperator op = new NssOperator();
                op.Operator = NssOperator.OperatorMap[ch];

                int chNewBaseIndex = chBaseIndex + 1;
                AttachDebugData(op, DebugRanges, chBaseIndex, chNewBaseIndex - 1);

                Tokens.Add(op);
                chBaseIndex = chNewBaseIndex;
            }

            return chBaseIndex;
        }

        private int Literal(int chBaseIndex, string data)
        {
            char ch = data[chBaseIndex];
            bool isString = ch == '"';
            bool isNumber = char.IsNumber(ch);
            if (isString || isNumber)
            {
                NssLiteral literal = null;

                int chScanningIndex = chBaseIndex;

                if (isString)
                {
                    while (++chScanningIndex < data.Length)
                    {
                        char chScanning = data[chScanningIndex];
                        char chScanningLast = data[chScanningIndex - 1];
                        if (chScanning == '"' && chScanningLast != '\\')
                        {
                            literal = new NssLiteral();
                            literal.LiteralType = NssLiteralType.String;

                            int chStartIndex = chBaseIndex;
                            int chEndIndex = chScanningIndex + 1;

                            if (chStartIndex == chEndIndex)
                            {
                                literal.Literal = "";
                            }
                            else
                            {
                                literal.Literal = data.Substring(chStartIndex, chEndIndex - chStartIndex);
                            }

                            Tokens.Add(literal);
                            int chNewBaseIndex = chEndIndex;
                            AttachDebugData(literal, DebugRanges, chBaseIndex, chNewBaseIndex - 1);

                            chBaseIndex = chNewBaseIndex;
                            break;
                        }
                    }
                }
                else
                {
                    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/lexer.py:322-335
                    // Original: def t_FLOAT_VALUE(self, t): r"[0-9]+\.[0-9]+f?|[0-9]f" / def t_INT_HEX_VALUE(self, t): "0x[0-9a-fA-F]+" / def t_INT_VALUE(self, t): "[0-9]+"
                    if (ch == '0' && chBaseIndex + 1 < data.Length && (data[chBaseIndex + 1] == 'x' || data[chBaseIndex + 1] == 'X'))
                    {
                        int hexStart = chBaseIndex + 2;
                        int hexIndex = hexStart;
                        while (hexIndex < data.Length)
                        {
                            char hexChar = data[hexIndex];
                            bool isHexDigit = char.IsDigit(hexChar) ||
                                              (hexChar >= 'a' && hexChar <= 'f') ||
                                              (hexChar >= 'A' && hexChar <= 'F');
                            if (!isHexDigit)
                            {
                                break;
                            }
                            hexIndex++;
                        }

                        if (hexIndex > hexStart)
                        {
                            literal = new NssLiteral();
                            literal.LiteralType = NssLiteralType.Int;
                            literal.Literal = data.Substring(chBaseIndex, hexIndex - chBaseIndex);
                            Tokens.Add(literal);
                            AttachDebugData(literal, DebugRanges, chBaseIndex, hexIndex - 1);
                            chBaseIndex = hexIndex;
                        }
                    }
                    else
                    {
                        bool seenDecimalPlace = false;
                        bool seenFloatSuffix = false;
                        int chEndIndex = chBaseIndex;

                        while (chEndIndex < data.Length)
                        {
                            char chScanning = data[chEndIndex];

                            if (char.IsNumber(chScanning))
                            {
                                chEndIndex++;
                                continue;
                            }

                            if (chScanning == '.' && !seenDecimalPlace)
                            {
                                seenDecimalPlace = true;
                                chEndIndex++;
                                continue;
                            }

                            if ((chScanning == 'f' || chScanning == 'F') && !seenFloatSuffix)
                            {
                                seenFloatSuffix = true;
                                chEndIndex++;
                                break;
                            }

                            break;
                        }

                        if (chEndIndex > chBaseIndex)
                        {
                            literal = new NssLiteral();
                            literal.LiteralType = (seenDecimalPlace || seenFloatSuffix) ? NssLiteralType.Float : NssLiteralType.Int;
                            literal.Literal = data.Substring(chBaseIndex, chEndIndex - chBaseIndex);

                            AttachDebugData(literal, DebugRanges, chBaseIndex, chEndIndex - 1);
                            Tokens.Add(literal);
                            chBaseIndex = chEndIndex;
                        }
                    }
                }
            }

            return chBaseIndex;
        }

        private int Keyword(int chBaseIndex, string data)
        {
            char ch = data[chBaseIndex];

            // Matching PyKotor lexer.py: PLY matches keywords via regex with word boundaries (\b)
            // No restriction on previous token type - word boundary check is sufficient
            foreach (KeyValuePair<string, NssKeywords> kvp in NssKeyword.KeywordMap)
            {
                if (chBaseIndex + kvp.Key.Length >= data.Length)
                {
                    continue; // This would overrun us.
                }

                string strFromData = data.Substring(chBaseIndex, kvp.Key.Length);
                if (strFromData.Equals(kvp.Key, StringComparison.Ordinal))
                {
                    // Matching PyKotor lexer.py line 277: r"location\b" - word boundary matches whitespace, end of string, or non-word characters
                    int chNextAlongIndex = chBaseIndex + kvp.Key.Length;
                    bool accept = false;

                    if (chNextAlongIndex >= data.Length)
                    {
                        accept = true;
                    }
                    else
                    {
                        char chNextAlong = data[chNextAlongIndex];
                        // Word boundary: separator, operator, or whitespace (matching PyKotor's \b regex)
                        accept = NssSeparator.SeparatorMap.ContainsKey(chNextAlong) ||
                                 NssOperator.OperatorMap.ContainsKey(chNextAlong) ||
                                 char.IsWhiteSpace(chNextAlong);
                    }

                    if (accept)
                    {
                        NssKeyword keyword = new NssKeyword();
                        keyword.Keyword = kvp.Value;

                        int chNewBaseIndex = chNextAlongIndex;
                        AttachDebugData(keyword, DebugRanges, chBaseIndex, chNewBaseIndex - 1);

                        Tokens.Add(keyword);
                        chBaseIndex = chNewBaseIndex;
                        break;
                    }
                }
            }

            return chBaseIndex;
        }

        private int Identifier(int chBaseIndex, string data)
        {
            char ch = data[chBaseIndex];

            // Identifiers must start with a letter or underscore
            if (!char.IsLetter(ch) && ch != '_')
            {
                return chBaseIndex;
            }

            int chScanningIndex = chBaseIndex;
            bool eof;

            do
            {
                eof = chScanningIndex >= data.Length;
                if (eof) break;

                char chScanning = data[chScanningIndex];

                bool hasOperator = NssOperator.OperatorMap.ContainsKey(chScanning);
                bool hasSeparator = NssSeparator.SeparatorMap.ContainsKey(chScanning);
                if (eof || hasSeparator || hasOperator)
                {
                    NssIdentifier identifier = new NssIdentifier();

                    int chStartIndex = chBaseIndex;
                    int chEndIndex = chScanningIndex + (eof ? 1 : 0);
                    identifier.Identifier = data.Substring(chStartIndex, chEndIndex - chStartIndex);

                    // Don't add empty identifiers
                    if (identifier.Identifier.Length == 0)
                    {
                        return chBaseIndex;
                    }

                    int chNewBaseIndex = chScanningIndex + (eof ? 1 : 0);
                    AttachDebugData(identifier, DebugRanges, chBaseIndex, chNewBaseIndex - 1);

                    Tokens.Add(identifier);
                    chBaseIndex = chNewBaseIndex;
                    break;
                }

                ++chScanningIndex;
            } while (chScanningIndex < data.Length);

            return chBaseIndex;
        }

        public struct NssLexDebugRange
        {
            public int Line { get; set; }
            public int IndexStart { get; set; }
            public int IndexEnd { get; set; }
        }

        public struct NssLexDebugInfo
        {
            public int LineStart { get; set; }
            public int LineEnd { get; set; }
            public int ColumnStart { get; set; }
            public int ColumnEnd { get; set; }
        }

        private int m_LastDebugDataIndex = 0;

        private void AttachDebugData(NssTokenBase token, List<NssLexDebugRange> debugRanges, int indexStart, int indexEnd)
        {
            NssLexDebugInfo debugInfo = new NssLexDebugInfo();

            bool foundStart = false;
            bool foundEnd = false;

            for (int i = m_LastDebugDataIndex; i < debugRanges.Count; ++i)
            {
                int startIndex = i;
                int endIndex = i;

                if (indexStart >= debugRanges[startIndex].IndexStart && indexStart <= debugRanges[startIndex].IndexEnd)
                {
                    foundStart = true;

                    for (int j = i; j < debugRanges.Count; ++j)
                    {
                        if (indexStart >= debugRanges[endIndex].IndexStart && indexStart <= debugRanges[endIndex].IndexEnd)
                        {
                            foundEnd = true;
                            endIndex = j;
                            break;
                        }
                    }

                    if (!foundEnd)
                    {
                        break;
                    }

                    debugInfo.LineStart = i;
                    debugInfo.LineEnd = endIndex;
                    debugInfo.ColumnStart = indexStart - debugRanges[startIndex].IndexStart;
                    debugInfo.ColumnEnd = indexEnd - debugRanges[endIndex].IndexStart;
                    m_LastDebugDataIndex = i;
                    break;
                }
            }

            if (!foundStart || !foundEnd)
            {
                // Silent fail for debug info
            }

            token.UserData = debugInfo;
        }
    }
}

