using System;
using System.Collections.Generic;
using System.Linq;
using KOTORModSync.Common.Script;
using KOTORModSync.Formats.NCS;
using KOTORModSync.Formats.NCS.Compiler.NSS;

namespace KOTORModSync.Formats.NCS.Compiler
{

    /// <summary>
    /// Represents an assignment expression (field = value).
    /// </summary>
    public class AssignmentExpression : Expression
    {
        public FieldAccess FieldAccess { get; set; }
        public Expression Value { get; set; }

        public AssignmentExpression(FieldAccess fieldAccess, Expression value)
        {
            FieldAccess = fieldAccess ?? throw new ArgumentNullException(nameof(fieldAccess));
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/classes.py:1587-1634
        public override DynamicDataType Compile(NCS ncs, CodeRoot root, CodeBlock block)
        {
            // Save temp_stack before compiling expression to check if expression already added to it
            // Matching PyKotor classes.py line 1589
            int tempStackBefore = block.TempStack;
            // Compile expression - expressions may or may not add to temp_stack themselves
            // Matching PyKotor classes.py line 1591
            DynamicDataType variableType = Value.Compile(ncs, root, block);
            int tempStackAfter = block.TempStack;

            // Only add to temp_stack if the expression didn't already add it
            // (FunctionCallExpression and EngineCallExpression already add their return values)
            // Matching PyKotor classes.py lines 1594-1598
            if (tempStackAfter == tempStackBefore)
            {
                // Expression didn't add to temp_stack, so we need to add it
                block.TempStack += variableType.Size(root);
            }

            // Get variable location - get_scoped uses temp_stack (including expression result) in its calculation
            // Matching PyKotor classes.py lines 1601-1604
            GetScopedResult scoped = FieldAccess.GetScoped(block, root);
            bool isGlobal = scoped.IsGlobal;
            DynamicDataType expressionType = scoped.Datatype;
            int stackIndex = scoped.Offset;

            if (scoped.IsConst)
            {
                string varName = string.Join(".", FieldAccess.Identifiers.Select(i => i.Label));
                throw new NSS.CompileError($"Cannot assign to const variable '{varName}'");
            }

            // Matching PyKotor classes.py line 1611
            NCSInstructionType instructionType = isGlobal ? NCSInstructionType.CPDOWNBP : NCSInstructionType.CPDOWNSP;
            // get_scoped() already accounts for temp_stack (which includes the expression result),
            // so stack_index points to the correct variable location
            // Matching PyKotor classes.py lines 1612-1613

            if (variableType != expressionType)
            {
                string varName = string.Join(".", FieldAccess.Identifiers.Select(i => i.Label));
                throw new NSS.CompileError(
                    $"Type mismatch in assignment to '{varName}'\n" +
                    $"  Variable type: {expressionType.Builtin.ToScriptString()}\n" +
                    $"  Expression type: {variableType.Builtin.ToScriptString()}");
            }

            // Copy the value that the expression has already been placed on the stack to where the identifiers position is
            // Matching PyKotor classes.py lines 1624-1627
            ncs.Add(instructionType, new List<object> { stackIndex, expressionType.Size(root) });

            // Don't remove the expression result from the stack - leave it for ExpressionStatement to clean up
            // This matches the behavior of other assignment operations (+=, -=, etc.)
            // The result is copied to the variable location but remains on top of stack
            // ExpressionStatement will remove it based on temp_stack tracking
            // Matching PyKotor classes.py lines 1629-1632

            return variableType;
        }

        public override string ToString()
        {
            return $"{FieldAccess} = {Value}";
        }
    }
}

