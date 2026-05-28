using System;
using System.Collections.Generic;
using System.Linq;
using KOTORModSync.Common.Script;
using KOTORModSync.Formats.NCS;

namespace KOTORModSync.Formats.NCS.Compiler
{

    /// <summary>
    /// Represents a unary operation expression (e.g., -x, !b, ~i).
    /// </summary>
    public class UnaryOperatorExpression : Expression
    {
        public Expression Operand { get; set; }
        public Operator Operator { get; set; }
        public OperatorMapping OperatorMapping { get; set; }

        public UnaryOperatorExpression(
            Expression operand,
            Operator op,
            OperatorMapping operatorMapping)
        {
            Operand = operand ?? throw new ArgumentNullException(nameof(operand));
            Operator = op;
            OperatorMapping = operatorMapping ?? throw new ArgumentNullException(nameof(operatorMapping));
        }

        public override DynamicDataType Compile(NCS ncs, CodeRoot root, CodeBlock block)
        {
            DynamicDataType operandType = Operand.Compile(ncs, root, block);
            block.TempStack += operandType.Size(root);

            // [CanBeNull] Find matching operator mapping
            UnaryOperatorMapping mapping = OperatorMapping.Unary.FirstOrDefault(m => m.Rhs == operandType.Builtin);

            if (mapping == null)
            {
                throw new CompileError(
                    $"No unary operator '{GetOperatorSymbol(Operator)}' defined for type {operandType.Builtin.ToScriptString()}\n" +
                    $"  Available types: {GetAvailableTypes()}");
            }

            ncs.Add(mapping.Instruction, new List<object>());

            block.TempStack -= operandType.Size(root);

            // Most unary operators return same type as operand
            return operandType;
        }

        private static string GetOperatorSymbol(Operator op)
        {
            switch (op)
            {
                case Operator.SUBTRACT:
                    return "-";
                case Operator.NOT:
                    return "!";
                case Operator.ONES_COMPLEMENT:
                    return "~";
                default:
                    return op.ToString();
            }
        }

        private string GetAvailableTypes()
        {
            IEnumerable<string> types = OperatorMapping.Unary
                .Select(m => m.Rhs.ToScriptString())
                .Distinct();
            return string.Join(", ", types);
        }

        public override string ToString()
        {
            return $"{GetOperatorSymbol(Operator)}{Operand}";
        }
    }
}

