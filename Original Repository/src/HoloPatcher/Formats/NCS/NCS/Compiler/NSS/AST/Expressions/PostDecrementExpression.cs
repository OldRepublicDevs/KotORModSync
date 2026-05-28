using System.Collections.Generic;
using System.Linq;
using KOTORModSync.Common.Script;
using KOTORModSync.Formats.NCS;
using KOTORModSync.Formats.NCS.Compiler.NSS;

namespace KOTORModSync.Formats.NCS.Compiler
{

    /// <summary>
    /// Represents a post-decrement expression (x--).
    /// </summary>
    public class PostDecrementExpression : Expression
    {
        public FieldAccess FieldAccess { get; set; }

        public PostDecrementExpression(FieldAccess fieldAccess)
        {
            FieldAccess = fieldAccess ?? throw new System.ArgumentNullException(nameof(fieldAccess));
        }

        public override DynamicDataType Compile(NCS ncs, CodeRoot root, CodeBlock block)
        {
            // Matching PyKotor classes.py lines 2942-2965
            // First compile the field access to push value to stack
            DynamicDataType variableType = FieldAccess.Compile(ncs, root, block);
            block.TempStack += variableType.Size(root); // Matching PyKotor line 2944

            if (variableType.Builtin != DataType.Int && variableType.Builtin != DataType.Float)
            {
                string varName = string.Join(".", FieldAccess.Identifiers.Select(i => i.Label));
                throw new CompileError(
                    $"Decrement operator (--) requires integer variable, got {variableType.Builtin.ToScriptString().ToLower()}\n" +
                    $"  Variable: {varName}");
            }

            GetScopedResult scoped = FieldAccess.GetScoped(block, root);
            bool isGlobal = scoped.IsGlobal;
            int stackIndex = scoped.Offset;
            if (scoped.IsConst)
            {
                string varName = string.Join(".", FieldAccess.Identifiers.Select(i => i.Label));
                throw new NSS.CompileError($"Cannot decrement const variable '{varName}'");
            }

            // Decrement the variable (value is already on stack from FieldAccess.Compile)
            // Matching PyKotor lines 2959-2962
            if (isGlobal)
            {
                ncs.Add(NCSInstructionType.DECxBP, new List<object> { stackIndex });
            }
            else
            {
                ncs.Add(NCSInstructionType.DECxSP, new List<object> { stackIndex });
            }

            // Matching PyKotor line 2964
            block.TempStack -= variableType.Size(root);

            return variableType;
        }

        public override string ToString()
        {
            return $"{FieldAccess}--";
        }
    }
}

