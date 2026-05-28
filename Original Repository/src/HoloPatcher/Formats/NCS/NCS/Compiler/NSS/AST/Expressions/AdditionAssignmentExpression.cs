using System;
using System.Collections.Generic;
using System.Linq;
using KOTORModSync.Common.Script;
using KOTORModSync.Formats.NCS;
using KOTORModSync.Formats.NCS.Compiler.NSS;

namespace KOTORModSync.Formats.NCS.Compiler
{

    /// <summary>
    /// Represents an addition assignment expression (x += y).
    /// </summary>
    public class AdditionAssignmentExpression : Expression
    {
        public FieldAccess FieldAccess { get; set; }
        public Expression Value { get; set; }

        public AdditionAssignmentExpression(FieldAccess fieldAccess, Expression value)
        {
            FieldAccess = fieldAccess ?? throw new ArgumentNullException(nameof(fieldAccess));
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/classes.py:1643-1711
        public override DynamicDataType Compile(NCS ncs, CodeRoot root, CodeBlock block)
        {
            // Copy the variable to the top of the stack
            // Matching PyKotor classes.py lines 1649-1660
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

            // Add the result of the expression to the stack
            // Matching PyKotor classes.py lines 1662-1668
            int tempStackBeforeExpr = block.TempStack;
            DynamicDataType expressionType = Value.Compile(ncs, root, block);
            // Only add to temp_stack if the expression didn't already add it
            // (FunctionCallExpression and EngineCallExpression already add their return values)
            if (block.TempStack == tempStackBeforeExpr)
            {
                block.TempStack += expressionType.Size(root);
            }

            // Determine what instruction to apply to the two values
            // Matching PyKotor classes.py lines 1670-1691
            NCSInstructionType arithmeticInstruction;
            if (variableType == DynamicDataType.INT && expressionType == DynamicDataType.INT)
            {
                arithmeticInstruction = NCSInstructionType.ADDII;
            }
            else if (variableType == DynamicDataType.INT && expressionType == DynamicDataType.FLOAT)
            {
                arithmeticInstruction = NCSInstructionType.ADDIF;
            }
            else if (variableType == DynamicDataType.FLOAT && expressionType == DynamicDataType.FLOAT)
            {
                arithmeticInstruction = NCSInstructionType.ADDFF;
            }
            else if (variableType == DynamicDataType.FLOAT && expressionType == DynamicDataType.INT)
            {
                arithmeticInstruction = NCSInstructionType.ADDFI;
            }
            else if (variableType == DynamicDataType.STRING && expressionType == DynamicDataType.STRING)
            {
                arithmeticInstruction = NCSInstructionType.ADDSS;
            }
            else if (variableType == DynamicDataType.VECTOR && expressionType == DynamicDataType.VECTOR)
            {
                arithmeticInstruction = NCSInstructionType.ADDVV;
            }
            else
            {
                string varName = string.Join(".", FieldAccess.Identifiers.Select(i => i.Label));
                throw new CompileError(
                    $"Type mismatch in += operation on '{varName}'\n" +
                    $"  Variable type: {variableType.Builtin.ToScriptString()}\n" +
                    $"  Expression type: {expressionType.Builtin.ToScriptString()}\n" +
                    "  Supported: int+=int, float+=float/int, string+=string, vector+=vector");
            }

            // Add the expression and our temp variable copy together
            ncs.Add(arithmeticInstruction, new List<object>());

            // Copy the result to the original variable in the stack
            // The arithmetic operation consumed both operands and left the result on stack
            // After CPDOWNSP, the result is still on stack (for ExpressionStatement to clean up)
            // Matching PyKotor classes.py lines 1696-1702
            NCSInstructionType insCpDown = isGlobal ? NCSInstructionType.CPDOWNBP : NCSInstructionType.CPDOWNSP;
            // Result (variable_type size) is on stack; offset to original variable accounts for this
            int offsetCpDown = isGlobal ? stackIndex : stackIndex - variableType.Size(root);
            ncs.Add(insCpDown, new List<object> { offsetCpDown, variableType.Size(root) });

            // Arithmetic operation consumed variable copy and expression (2 values), left result (1 value)
            // Result is still on stack (copied to variable location but also remains on top for ExpressionStatement)
            // temp_stack currently = variable_size + expression_size
            // After operation: stack has 1 result of variable_type size
            // Net change: both operands consumed, result pushed
            // Matching PyKotor classes.py line 1709
            block.TempStack = block.TempStack - variableType.Size(root) - expressionType.Size(root) + variableType.Size(root);
            // Return variable_type (the result type) so ExpressionStatement knows what size to clean up
            return variableType;
        }

        public override string ToString()
        {
            return $"{FieldAccess} += {Value}";
        }
    }
}

