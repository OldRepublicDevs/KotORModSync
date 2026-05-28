using System.Collections.Generic;
using KOTORModSync.Common.Script;
using KOTORModSync.Formats.NCS;

namespace KOTORModSync.Formats.NCS.Compiler
{

    /// <summary>
    /// Represents a pre-increment expression (++x).
    /// </summary>
    public class PreIncrementExpression : Expression
    {
        public FieldAccess FieldAccess { get; set; }

        public PreIncrementExpression(FieldAccess fieldAccess)
        {
            FieldAccess = fieldAccess ?? throw new System.ArgumentNullException(nameof(fieldAccess));
        }

        public override DynamicDataType Compile(NCS ncs, CodeRoot root, CodeBlock block)
        {
            (bool isGlobal, DynamicDataType variableType, int stackIndex) = FieldAccess.GetScoped(block, root);

            if (variableType.Builtin != DataType.Int && variableType.Builtin != DataType.Float)
            {
                throw new CompileError(
                    $"Increment operator requires int or float type, got {variableType.Builtin.ToScriptString()}");
            }

            NCSInstructionType instructionType = isGlobal ? NCSInstructionType.INCxBP : NCSInstructionType.INCxSP;
            ncs.Add(instructionType, new List<object> { stackIndex });

            // Push incremented value onto stack
            NCSInstructionType copyInst = isGlobal ? NCSInstructionType.CPTOPBP : NCSInstructionType.CPTOPSP;
            ncs.Add(copyInst, new List<object> { stackIndex, variableType.Size(root) });
            block.TempStack += variableType.Size(root);

            return variableType;
        }

        public override string ToString()
        {
            return $"++{FieldAccess}";
        }
    }
}

