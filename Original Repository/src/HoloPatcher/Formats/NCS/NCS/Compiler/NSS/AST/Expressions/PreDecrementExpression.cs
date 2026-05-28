using System.Collections.Generic;
using System.Linq;
using KOTORModSync.Common.Script;
using KOTORModSync.Formats.NCS;
using KOTORModSync.Formats.NCS.Compiler.NSS;

namespace KOTORModSync.Formats.NCS.Compiler
{

    /// <summary>
    /// Represents a pre-decrement expression (--x).
    /// </summary>
    public class PreDecrementExpression : Expression
    {
        public FieldAccess FieldAccess { get; set; }

        public PreDecrementExpression(FieldAccess fieldAccess)
        {
            FieldAccess = fieldAccess ?? throw new System.ArgumentNullException(nameof(fieldAccess));
        }

        public override DynamicDataType Compile(NCS ncs, CodeRoot root, CodeBlock block)
        {
            // Matching PyKotor classes.py lines 2906-2935 exactly
            // First compile the field access to push value to stack
            // Note: FieldAccess.Compile does NOT add to temp_stack, so we don't either
            DynamicDataType variableType = FieldAccess.Compile(ncs, root, block);

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/classes.py:2909-2915
            // Original: if variable_type != DynamicDataType.INT:
            if (variableType.Builtin != DataType.Int)
            {
                string varName = string.Join(".", FieldAccess.Identifiers.Select(i => i.Label));
                throw new NSS.CompileError(
                    $"Decrement operator (--) requires integer variable, got {variableType.Builtin.ToScriptString().ToLower()}\n" +
                    $"  Variable: {varName}");
            }

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/classes.py:2917
            // Original: isglobal, variable_type, stack_index, is_const = self.field_access.get_scoped(block, root)
            GetScopedResult scoped = FieldAccess.GetScoped(block, root);
            bool isGlobal = scoped.IsGlobal;
            int stackIndex = scoped.Offset;
            if (scoped.IsConst)
            {
                string varName = string.Join(".", FieldAccess.Identifiers.Select(i => i.Label));
                throw new NSS.CompileError($"Cannot decrement const variable '{varName}'");
            }

            // Decrement the value on the stack (the value that was just pushed by FieldAccess.Compile)
            // Matching PyKotor line 2922: ncs.add(NCSInstructionType.DECxSP, args=[-4])
            // DECxSP with negative offset decrements the value at the top of the stack
            ncs.Add(NCSInstructionType.DECxSP, new List<object> { -variableType.Size(root) });

            // Copy the decremented value back to the variable location
            // Matching PyKotor lines 2924-2933
            if (isGlobal)
            {
                ncs.Add(NCSInstructionType.CPDOWNBP, new List<object> { stackIndex, variableType.Size(root) });
            }
            else
            {
                ncs.Add(NCSInstructionType.CPDOWNSP, new List<object> { stackIndex - variableType.Size(root), variableType.Size(root) });
            }

            // Matching PyKotor line 2935: return variable_type
            // The decremented value is still on the stack (for assignment), so temp_stack is correct
            return variableType;
        }

        public override string ToString()
        {
            return $"--{FieldAccess}";
        }
    }
}

