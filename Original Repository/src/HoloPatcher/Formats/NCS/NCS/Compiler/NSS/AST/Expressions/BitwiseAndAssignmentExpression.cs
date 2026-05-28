using System;
using System.Collections.Generic;
using System.Linq;
using KOTORModSync.Common.Script;
using KOTORModSync.Formats.NCS;
using KOTORModSync.Formats.NCS.Compiler.NSS;

namespace KOTORModSync.Formats.NCS.Compiler
{
    public class BitwiseAndAssignmentExpression : Expression
    {
        public FieldAccess FieldAccess { get; set; }
        public Expression Value { get; set; }

        public BitwiseAndAssignmentExpression(FieldAccess fieldAccess, Expression value)
        {
            FieldAccess = fieldAccess ?? throw new ArgumentNullException(nameof(fieldAccess));
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public override DynamicDataType Compile(NCS ncs, CodeRoot root, CodeBlock block)
        {
            GetScopedResult scoped = FieldAccess.GetScoped(block, root);
            bool isGlobal = scoped.IsGlobal;
            DynamicDataType variableType = scoped.Datatype;
            int stackIndex = scoped.Offset;
            if (scoped.IsConst)
            {
                string varName = string.Join(".", FieldAccess.Identifiers.Select(i => i.Label));
                throw new NSS.CompileError($"Cannot assign to const variable '{varName}'");
            }
            NCSInstructionType instructionType = isGlobal ? NCSInstructionType.CPTOPBP : NCSInstructionType.CPTOPSP;
            ncs.Add(instructionType, new List<object> { stackIndex, variableType.Size(root) });
            block.TempStack += variableType.Size(root);

            int tempStackBeforeExpr = block.TempStack;
            DynamicDataType expressionType = Value.Compile(ncs, root, block);
            int expressionSize = expressionType.Size(root);
            int tempStackAfterExpr = block.TempStack;
            int expressionStackDelta = tempStackAfterExpr - tempStackBeforeExpr;
            if (expressionStackDelta == 0)
            {
                block.TempStack += expressionSize;
                expressionStackDelta = expressionSize;
            }

            if (variableType.Builtin != DataType.Int || expressionType.Builtin != DataType.Int)
            {
                string varName = string.Join(".", FieldAccess.Identifiers.Select(i => i.Label));
                throw new NSS.CompileError(
                    $"Type mismatch in &= operation on '{varName}'\n" +
                    $"  Variable type: {variableType.Builtin.ToScriptString()}\n" +
                    $"  Expression type: {expressionType.Builtin.ToScriptString()}\n" +
                    "  Supported: int&=int");
            }

            ncs.Add(NCSInstructionType.BOOLANDII, new List<object>());

            NCSInstructionType insCpDown = isGlobal ? NCSInstructionType.CPDOWNBP : NCSInstructionType.CPDOWNSP;
            int offsetCpDown = isGlobal ? stackIndex : stackIndex - variableType.Size(root);
            ncs.Add(insCpDown, new List<object> { offsetCpDown, variableType.Size(root) });

            block.TempStack = block.TempStack - variableType.Size(root) - expressionStackDelta + variableType.Size(root);

            return variableType;
        }

        public override string ToString()
        {
            return $"{FieldAccess} &= {Value}";
        }
    }
}

