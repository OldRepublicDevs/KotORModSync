using System;
using System.Collections.Generic;
using System.Linq;
using KOTORModSync.Common.Script;
using KOTORModSync.Formats.NCS;

namespace KOTORModSync.Formats.NCS.Compiler
{

    /// <summary>
    /// Represents a multiplication assignment expression (x *= y).
    /// </summary>
    public class MultiplicationAssignmentExpression : Expression
    {
        public FieldAccess FieldAccess { get; set; }
        public Expression Value { get; set; }

        public MultiplicationAssignmentExpression(FieldAccess fieldAccess, Expression value)
        {
            FieldAccess = fieldAccess ?? throw new ArgumentNullException(nameof(fieldAccess));
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public override DynamicDataType Compile(NCS ncs, CodeRoot root, CodeBlock block)
        {
            // Copy the variable to the top of the stack
            (bool isGlobal, DynamicDataType variableType, int stackIndex) = FieldAccess.GetScoped(block, root);
            NCSInstructionType instructionType = isGlobal ? NCSInstructionType.CPTOPBP : NCSInstructionType.CPTOPSP;
            ncs.Add(instructionType, new List<object> { stackIndex, variableType.Size(root) });
            block.TempStack += variableType.Size(root);

            // Add the result of the expression to the stack
            int tempStackBeforeExpr = block.TempStack;
            DynamicDataType expressionType = Value.Compile(ncs, root, block);
            int expressionResultSize = expressionType.Size(root);
            int tempStackAfterExpr = block.TempStack;
            int expressionStackDelta = tempStackAfterExpr - tempStackBeforeExpr;
            if (expressionStackDelta == 0)
            {
                block.TempStack += expressionResultSize;
                expressionStackDelta = expressionResultSize;
            }

            // Determine what instruction to apply to the two values
            NCSInstructionType arithmeticInstruction;
            if (variableType.Builtin == DataType.Int && expressionType.Builtin == DataType.Int)
            {
                arithmeticInstruction = NCSInstructionType.MULII;
            }
            else if (variableType.Builtin == DataType.Int && expressionType.Builtin == DataType.Float)
            {
                arithmeticInstruction = NCSInstructionType.MULIF;
            }
            else if (variableType.Builtin == DataType.Float && expressionType.Builtin == DataType.Float)
            {
                arithmeticInstruction = NCSInstructionType.MULFF;
            }
            else if (variableType.Builtin == DataType.Float && expressionType.Builtin == DataType.Int)
            {
                arithmeticInstruction = NCSInstructionType.MULFI;
            }
            else if (variableType.Builtin == DataType.Vector && expressionType.Builtin == DataType.Float)
            {
                arithmeticInstruction = NCSInstructionType.MULVF;
            }
            else if (variableType.Builtin == DataType.Float && expressionType.Builtin == DataType.Vector)
            {
                arithmeticInstruction = NCSInstructionType.MULFV;
            }
            else
            {
                string varName = string.Join(".", FieldAccess.Identifiers.Select(i => i.Label));
                throw new CompileError(
                    $"Type mismatch in *= operation on '{varName}'\n" +
                    $"  Variable type: {variableType.Builtin.ToScriptString()}\n" +
                    $"  Expression type: {expressionType.Builtin.ToScriptString()}\n" +
                    "  Supported: int*=int, float*=float/int, vector*=float, float*=vector");
            }

            // Multiply the expression and our temp variable copy
            ncs.Add(arithmeticInstruction, new List<object>());

            // Copy the result to the original variable in the stack
            NCSInstructionType insCpDown = isGlobal ? NCSInstructionType.CPDOWNBP : NCSInstructionType.CPDOWNSP;
            int offsetCpDown = isGlobal ? stackIndex : stackIndex - variableType.Size(root);
            ncs.Add(insCpDown, new List<object> { offsetCpDown, variableType.Size(root) });

            // Arithmetic operation consumed variable copy and expression (2 values), left result (1 value)
            block.TempStack = block.TempStack - variableType.Size(root) - expressionStackDelta + variableType.Size(root);

            return variableType;
        }

        public override string ToString()
        {
            return $"{FieldAccess} *= {Value}";
        }
    }
}

