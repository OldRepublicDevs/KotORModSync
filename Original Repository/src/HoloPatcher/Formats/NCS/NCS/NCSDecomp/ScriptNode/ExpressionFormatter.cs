// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ExpressionFormatter.java:1-235
// Copyright 2021-2025 NCSDecomp
// Licensed under the Business Source License 1.1 (BSL 1.1).
// Visit https://bolabaden.org for more information and other ventures
// See LICENSE.txt file in the project root for full license information.

using System;
using KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode;

namespace KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode
{
    /// <summary>
    /// Centralized expression pretty-printer that minimizes redundant parentheses
    /// while keeping operator precedence and associativity intact. Expressions render
    /// through this formatter instead of hand-built toString implementations in
    /// individual nodes so nested expressions share consistent formatting rules.
    /// </summary>
    internal static class ExpressionFormatter
    {
        private enum Position
        {
            NONE,
            LEFT,
            RIGHT
        }

        private const int PREC_ASSIGNMENT = 1;
        private const int PREC_LOGICAL_OR = 2;
        private const int PREC_LOGICAL_AND = 3;
        private const int PREC_BIT_OR = 4;
        private const int PREC_BIT_XOR = 5;
        private const int PREC_BIT_AND = 6;
        private const int PREC_EQUALITY = 7;
        private const int PREC_RELATIONAL = 8;
        private const int PREC_SHIFT = 9;
        private const int PREC_ADDITIVE = 10;
        private const int PREC_MULTIPLICATIVE = 11;
        private const int PREC_UNARY = 12;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ExpressionFormatter.java:36-38
        // Original: static String format(AExpression expr)
        public static string Format(AExpression expr)
        {
            return Format(expr, int.MaxValue, Position.NONE, null);
        }

        /// <summary>
        /// Formats an expression for value contexts (variable initializers, returns)
        /// and preserves explicit grouping for simple comparison operations to match
        /// the original source style used by most shipped scripts.
        /// </summary>
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ExpressionFormatter.java:45-51
        // Original: static String formatValue(AExpression expr)
        public static string FormatValue(AExpression expr)
        {
            string rendered = Format(expr);
            if (expr is ABinaryExp binaryExp && NeedsValueParens(binaryExp))
            {
                return EnsureWrapped(rendered);
            }
            return rendered;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ExpressionFormatter.java:53-76
        // Original: private static String format(AExpression expr, int parentPrec, Position side, String parentOp)
        private static string Format(AExpression expr, int parentPrec, Position side, string parentOp)
        {
            if (expr == null)
            {
                return "";
            }

            if (expr is ABinaryExp binaryExp)
            {
                return FormatBinary(binaryExp, parentPrec, side, parentOp);
            }
            if (expr is AConditionalExp conditionalExp)
            {
                return FormatConditional(conditionalExp, parentPrec, side, parentOp);
            }
            if (expr is AUnaryExp unaryExp)
            {
                return FormatUnary(unaryExp, parentPrec, side, parentOp);
            }
            if (expr is AUnaryModExp unaryModExp)
            {
                return FormatUnaryMod(unaryModExp, parentPrec, side, parentOp);
            }
            if (expr is AModifyExp modifyExp)
            {
                return FormatAssignment(modifyExp, parentPrec, side, parentOp);
            }

            // Leaf-ish nodes keep their own rendering (constants, function calls, etc.)
            return expr.ToString();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ExpressionFormatter.java:78-85
        // Original: private static String formatBinary(ABinaryExp exp, int parentPrec, Position side, String parentOp)
        private static string FormatBinary(ABinaryExp exp, int parentPrec, Position side, string parentOp)
        {
            string op = exp.GetOp();
            int prec = Precedence(op);
            string left = Format(exp.GetLeft(), prec, Position.LEFT, op);
            string right = Format(exp.GetRight(), prec, Position.RIGHT, op);
            string rendered = left + " " + op + " " + right;
            return WrapIfNeeded(rendered, prec, parentPrec, side, parentOp, op);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ExpressionFormatter.java:87-94
        // Original: private static String formatConditional(AConditionalExp exp, int parentPrec, Position side, String parentOp)
        private static string FormatConditional(AConditionalExp exp, int parentPrec, Position side, string parentOp)
        {
            string op = exp.GetOp();
            int prec = Precedence(op);
            string left = Format(exp.GetLeft(), prec, Position.LEFT, op);
            string right = Format(exp.GetRight(), prec, Position.RIGHT, op);
            string rendered = left + " " + op + " " + right;
            return WrapIfNeeded(rendered, prec, parentPrec, side, parentOp, op);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ExpressionFormatter.java:96-102
        // Original: private static String formatUnary(AUnaryExp exp, int parentPrec, Position side, String parentOp)
        private static string FormatUnary(AUnaryExp exp, int parentPrec, Position side, string parentOp)
        {
            string op = exp.GetOp();
            int prec = PREC_UNARY;
            string inner = Format(exp.GetExp(), prec, Position.RIGHT, op);
            string rendered = op + inner;
            return WrapIfNeeded(rendered, prec, parentPrec, side, parentOp, op);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ExpressionFormatter.java:104-110
        // Original: private static String formatUnaryMod(AUnaryModExp exp, int parentPrec, Position side, String parentOp)
        private static string FormatUnaryMod(AUnaryModExp exp, int parentPrec, Position side, string parentOp)
        {
            string op = exp.GetOp();
            int prec = PREC_UNARY;
            string target = Format(exp.GetVarRef(), prec, Position.RIGHT, op);
            string rendered = exp.GetPrefix() ? op + target : target + op;
            return WrapIfNeeded(rendered, prec, parentPrec, side, parentOp, op);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ExpressionFormatter.java:112-118
        // Original: private static String formatAssignment(AModifyExp exp, int parentPrec, Position side, String parentOp)
        private static string FormatAssignment(AModifyExp exp, int parentPrec, Position side, string parentOp)
        {
            int prec = PREC_ASSIGNMENT;
            string left = Format(exp.GetVarRef(), prec, Position.LEFT, "=");
            string right = Format(exp.GetExpression(), prec, Position.RIGHT, "=");
            string rendered = left + " = " + right;
            return WrapIfNeeded(rendered, prec, parentPrec, side, parentOp, "=");
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ExpressionFormatter.java:120-123
        // Original: private static String wrapIfNeeded(String rendered, int selfPrec, int parentPrec, Position side, String parentOp, String selfOp)
        private static string WrapIfNeeded(string rendered, int selfPrec, int parentPrec, Position side, string parentOp, string selfOp)
        {
            return ShouldParenthesize(selfPrec, parentPrec, side, parentOp, selfOp) ? "(" + rendered + ")" : rendered;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ExpressionFormatter.java:125-146
        // Original: private static boolean shouldParenthesize(int selfPrec, int parentPrec, Position side, String parentOp, String selfOp)
        private static bool ShouldParenthesize(int selfPrec, int parentPrec, Position side, string parentOp, string selfOp)
        {
            if (parentPrec == int.MaxValue)
            {
                return false; // top-level expressions never need wrapping
            }
            if (selfPrec < parentPrec)
            {
                return true; // child binds looser than parent
            }
            if (selfPrec > parentPrec || parentOp == null)
            {
                return false;
            }

            // Equal precedence: parenthesize right-hand children when associativity differs
            if (side == Position.RIGHT)
            {
                if (IsNonAssociative(parentOp))
                {
                    return true;
                }
                return !parentOp.Equals(selfOp);
            }

            return false;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ExpressionFormatter.java:148-184
        // Original: private static int precedence(String op)
        private static int Precedence(string op)
        {
            if (op == null)
            {
                return PREC_UNARY; // safest default for unknown operators
            }
            switch (op)
            {
                case "||":
                    return PREC_LOGICAL_OR;
                case "&&":
                    return PREC_LOGICAL_AND;
                case "|":
                    return PREC_BIT_OR;
                case "^":
                    return PREC_BIT_XOR;
                case "&":
                    return PREC_BIT_AND;
                case "==":
                case "!=":
                    return PREC_EQUALITY;
                case "<":
                case "<=":
                case ">":
                case ">=":
                    return PREC_RELATIONAL;
                case "<<":
                case ">>":
                    return PREC_SHIFT;
                case "+":
                case "-":
                    return PREC_ADDITIVE;
                case "*":
                case "/":
                case "%":
                    return PREC_MULTIPLICATIVE;
                default:
                    return PREC_UNARY;
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ExpressionFormatter.java:186-207
        // Original: private static boolean isNonAssociative(String op)
        private static bool IsNonAssociative(string op)
        {
            if (op == null)
            {
                return false;
            }
            switch (op)
            {
                case "=":
                case "-":
                case "/":
                case "%":
                case "<<":
                case ">>":
                case "==":
                case "!=":
                case "<":
                case "<=":
                case ">":
                case ">=":
                    return true;
                default:
                    return false;
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ExpressionFormatter.java:209-212
        // Original: private static boolean needsValueParens(ABinaryExp exp)
        private static bool NeedsValueParens(ABinaryExp exp)
        {
            string op = exp.GetOp();
            return IsComparisonOp(op);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ExpressionFormatter.java:214-229
        // Original: private static boolean isComparisonOp(String op)
        private static bool IsComparisonOp(string op)
        {
            if (op == null)
            {
                return false;
            }
            switch (op)
            {
                case "==":
                case "!=":
                case "<":
                case "<=":
                case ">":
                case ">=":
                    return true;
                default:
                    return false;
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ExpressionFormatter.java:231-234
        // Original: private static String ensureWrapped(String rendered)
        private static string EnsureWrapped(string rendered)
        {
            string trimmed = rendered.Trim();
            return trimmed.StartsWith("(") && trimmed.EndsWith(")") ? rendered : "(" + rendered + ")";
        }
    }
}

