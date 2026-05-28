using System;
using System.Collections.Generic;
using KOTORModSync.Common;

namespace KOTORModSync.Formats.NCS.Compiler
{
    // Token base class and language definitions adapted from nss2csharp

    public class NssTokenBase
    {
        public object UserData { get; set; }
    }

    public enum NssKeywords
    {
        If,
        Else,
        For,
        While,
        Do,
        Switch,
        Break,
        Return,
        Case,
        Const,
        Void,
        Int,
        Float,
        String,
        Struct,
        Object,
        Location,
        Vector,
        ItemProperty,
        Effect,
        Talent,
        Action,
        Event,
        ObjectInvalid,
        ObjectSelf,
        Default,
        Continue,
        Include
    }

    public class NssKeyword : NssTokenBase
    {
        public NssKeywords Keyword { get; set; }

        public override string ToString()
        {
            foreach (KeyValuePair<string, NssKeywords> kvp in KeywordMap)
            {
                if (kvp.Value == Keyword)
                {
                    return kvp.Key;
                }
            }
            return null;
        }

        public static Dictionary<string, NssKeywords> KeywordMap = new Dictionary<string, NssKeywords>
        {
            { "if", NssKeywords.If },
            { "else", NssKeywords.Else },
            { "for", NssKeywords.For },
            { "while", NssKeywords.While },
            { "do", NssKeywords.Do },
            { "switch", NssKeywords.Switch },
            { "break", NssKeywords.Break },
            { "return", NssKeywords.Return },
            { "case", NssKeywords.Case },
            { "const", NssKeywords.Const },
            { "void", NssKeywords.Void },
            { "int", NssKeywords.Int },
            { "float", NssKeywords.Float },
            { "string", NssKeywords.String },
            { "struct", NssKeywords.Struct },
            { "object", NssKeywords.Object },
            { "location", NssKeywords.Location },
            { "vector", NssKeywords.Vector },
            { "itemproperty", NssKeywords.ItemProperty },
            { "effect", NssKeywords.Effect },
            { "talent", NssKeywords.Talent },
            { "action", NssKeywords.Action },
            { "event", NssKeywords.Event },
            { "OBJECT_INVALID", NssKeywords.ObjectInvalid },
            { "OBJECT_SELF", NssKeywords.ObjectSelf },
            { "default", NssKeywords.Default },
            { "continue", NssKeywords.Continue },
            { "include", NssKeywords.Include }
        };
    }

    public class NssIdentifier : NssTokenBase
    {
        public string Identifier { get; set; }

        public override string ToString()
        {
            return Identifier;
        }
    }

    public enum NssSeparators
    {
        Space,
        NewLine,
        OpenParen,
        CloseParen,
        OpenCurlyBrace,
        CloseCurlyBrace,
        Semicolon,
        Tab,
        Comma,
        OpenSquareBracket,
        CloseSquareBracket,
        Dot
    }

    public class NssSeparator : NssTokenBase
    {
        public NssSeparators Separator { get; set; }

        public override string ToString()
        {
            foreach (KeyValuePair<char, NssSeparators> kvp in SeparatorMap)
            {
                if (kvp.Value == Separator)
                {
                    return kvp.Key.ToString();
                }
            }
            return null;
        }

        public static Dictionary<char, NssSeparators> SeparatorMap = new Dictionary<char, NssSeparators>
        {
            { ' ', NssSeparators.Space },
            { '\n', NssSeparators.NewLine },
            { '\r', NssSeparators.NewLine },
            { '(', NssSeparators.OpenParen },
            { ')', NssSeparators.CloseParen },
            { '{', NssSeparators.OpenCurlyBrace },
            { '}', NssSeparators.CloseCurlyBrace },
            { ';', NssSeparators.Semicolon },
            { '\t', NssSeparators.Tab },
            { ',', NssSeparators.Comma },
            { '[', NssSeparators.OpenSquareBracket },
            { ']', NssSeparators.CloseSquareBracket },
            { '.', NssSeparators.Dot }
        };
    }

    public enum NssOperators
    {
        Addition,
        Subtraction,
        Division,
        Multiplication,
        Modulo,
        And,
        Or,
        Not,
        Inversion,
        GreaterThan,
        LessThan,
        Assignment, // single '='
        Equals,     // '=='
        TernaryQuestionMark,
        TernaryColon,
        NotEqual,
        LessThanOrEqual,
        GreaterThanOrEqual,
        Xor,
        AdditionAssignment,
        SubtractionAssignment,
        MultiplicationAssignment,
        DivisionAssignment,
        ModuloAssignment,
        BitwiseAndAssignment,
        BitwiseOrAssignment,
        BitwiseXorAssignment,
        BitwiseLeftAssignment,
        BitwiseRightAssignment,
        BitwiseUnsignedRightAssignment
    }

    public class NssOperator : NssTokenBase
    {
        public NssOperators Operator { get; set; }

        public override string ToString()
        {
            foreach (KeyValuePair<char, NssOperators> kvp in OperatorMap)
            {
                if (kvp.Value == Operator)
                {
                    return kvp.Key.ToString();
                }
            }
            return null;
        }

        public static Dictionary<char, NssOperators> OperatorMap = new Dictionary<char, NssOperators>
        {
            { '+', NssOperators.Addition },
            { '-', NssOperators.Subtraction },
            { '/', NssOperators.Division },
            { '*', NssOperators.Multiplication },
            { '%', NssOperators.Modulo },
            { '&', NssOperators.And },
            { '|', NssOperators.Or },
            { '!', NssOperators.Not },
            { '~', NssOperators.Inversion },
            { '>', NssOperators.GreaterThan },
            { '<', NssOperators.LessThan },
            { '=', NssOperators.Assignment },
            { '?', NssOperators.TernaryQuestionMark },
            { ':', NssOperators.TernaryColon },
            { '^', NssOperators.Xor }
        };
    }

    public enum NssLiteralType
    {
        Int,
        Float,
        String
    }

    public class NssLiteral : NssTokenBase
    {
        public NssLiteralType LiteralType { get; set; }
        public string Literal { get; set; }

        public override string ToString()
        {
            return Literal;
        }
    }

    public enum NssCommentType
    {
        LineComment,
        BlockComment
    }

    public class NssComment : NssTokenBase
    {
        public NssCommentType CommentType { get; set; }
        public string Comment { get; set; }
        public bool Terminated { get; set; }

        public override string ToString()
        {
            if (CommentType == NssCommentType.LineComment)
            {
                return "//" + Comment;
            }
            else if (CommentType == NssCommentType.BlockComment)
            {
                return "/*" + Comment + (Terminated ? "*/" : "");
            }
            return null;
        }
    }

    public enum NssPreprocessorType
    {
        Unknown,
        Include
    }

    public class NssPreprocessor : NssTokenBase
    {
        public NssPreprocessorType PreprocessorType { get; set; }
        public string Data { get; set; }

        public override string ToString()
        {
            return Data;
        }
    }
}