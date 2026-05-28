using System;
using System.Collections.Generic;
using System.Linq;
using KOTORModSync.Common.Script;
using KOTORModSync.Formats.NCS;

namespace KOTORModSync.Formats.NCS.Compiler
{

    /// <summary>
    /// Represents a binary operation expression (e.g., a + b, x == y).
    /// </summary>
    public class BinaryOperatorExpression : Expression
    {
        public Expression Left { get; set; }
        public Expression Right { get; set; }
        public Operator Operator { get; set; }
        public OperatorMapping OperatorMapping { get; set; }

        public BinaryOperatorExpression(
            Expression left,
            Expression right,
            Operator op,
            OperatorMapping operatorMapping)
        {
            Left = left ?? throw new ArgumentNullException(nameof(left));
            Right = right ?? throw new ArgumentNullException(nameof(right));
            Operator = op;
            OperatorMapping = operatorMapping ?? throw new ArgumentNullException(nameof(operatorMapping));
        }

        public override DynamicDataType Compile(NCS ncs, CodeRoot root, CodeBlock block)
        {
            // Python: Only add to temp_stack if the expression didn't already add it
            int tempStackBeforeExpr1 = block.TempStack;
            DynamicDataType leftType = Left.Compile(ncs, root, block);
            int type1Size = leftType.Size(root);
            if (block.TempStack == tempStackBeforeExpr1)
            {
                block.TempStack += type1Size;
            }

            int tempStackBeforeExpr2 = block.TempStack;
            DynamicDataType rightType = Right.Compile(ncs, root, block);
            int type2Size = rightType.Size(root);
            if (block.TempStack == tempStackBeforeExpr2)
            {
                block.TempStack += type2Size;
            }

            // [CanBeNull] Find matching operator mapping
            BinaryOperatorMapping mapping = OperatorMapping.Binary.FirstOrDefault(m =>
                m.Lhs == leftType.Builtin && m.Rhs == rightType.Builtin);

            if (mapping == null)
            {
                throw new CompileError(
                    $"No operator '{GetOperatorSymbol(Operator)}' defined for types " +
                    $"{leftType.Builtin.ToScriptString()} and {rightType.Builtin.ToScriptString()}\n" +
                    $"  Available combinations: {GetAvailableCombinations()}");
            }

            ncs.Add(mapping.Instruction, new List<object>());

            // Python: block.temp_stack -= (type1_size + type2_size - result_size)
            DynamicDataType resultType = new DynamicDataType(mapping.Result);
            int resultSize = resultType.Size(root);
            // Binary operation consumed both operands and left result on stack
            block.TempStack -= (type1Size + type2Size - resultSize);

            return resultType;
        }

        private static string GetOperatorSymbol(Operator op)
        {
            switch (op)
            {
                case Operator.ADDITION:
                    return "+";
                case Operator.SUBTRACT:
                    return "-";
                case Operator.MULTIPLY:
                    return "*";
                case Operator.DIVIDE:
                    return "/";
                case Operator.MODULUS:
                    return "%";
                case Operator.EQUAL:
                    return "==";
                case Operator.NOT_EQUAL:
                    return "!=";
                case Operator.GREATER_THAN:
                    return ">";
                case Operator.LESS_THAN:
                    return "<";
                case Operator.GREATER_THAN_OR_EQUAL:
                    return ">=";
                case Operator.LESS_THAN_OR_EQUAL:
                    return "<=";
                case Operator.AND:
                    return "&&";
                case Operator.OR:
                    return "||";
                case Operator.BITWISE_AND:
                    return "&";
                case Operator.BITWISE_OR:
                    return "|";
                case Operator.BITWISE_XOR:
                    return "^";
                case Operator.BITWISE_LEFT:
                    return "<<";
                case Operator.BITWISE_RIGHT:
                    return ">>";
                default:
                    return op.ToString();
            }
        }

        private string GetAvailableCombinations()
        {
            IEnumerable<string> combinations = OperatorMapping.Binary
                .Select(m => $"{m.Lhs.ToScriptString()} {GetOperatorSymbol(Operator)} {m.Rhs.ToScriptString()}")
                .Take(5);
            return string.Join(", ", combinations);
        }

        public override string ToString()
        {
            return $"({Left} {GetOperatorSymbol(Operator)} {Right})";
        }
    }
}

