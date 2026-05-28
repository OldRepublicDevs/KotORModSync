using System.Collections.Generic;
using JetBrains.Annotations;

namespace KOTORModSync.Formats.NCS.Compiler
{

    /// <summary>
    /// Represents a return statement.
    /// </summary>
    public class ReturnStatement : Statement
    {
        [CanBeNull]
        public Expression Expression { get; set; }

        public ReturnStatement([CanBeNull] Expression expression = null)
        {
            Expression = expression;
        }

        public override object Compile(
            NCS ncs,
            CodeRoot root,
            CodeBlock block,
            NCSInstruction returnInstruction,
            [CanBeNull] NCSInstruction breakInstruction,
            [CanBeNull] NCSInstruction continueInstruction)
        {
            int tempStackBefore = block.TempStack;
            DynamicDataType returnType = DynamicDataType.VOID;

            if (Expression != null)
            {
                returnType = Expression.Compile(ncs, root, block);
                int scopeSize = block.FullScopeSize(root);
                ncs.Add(NCSInstructionType.CPDOWNSP, new List<object> { -scopeSize - returnType.Size(root) * 2, returnType.Size(root) });
                ncs.Add(NCSInstructionType.MOVSP, new List<object> { -returnType.Size(root) });
                block.TempStack = tempStackBefore;
            }

            ncs.Add(NCSInstructionType.MOVSP, new List<object> { -block.FullScopeSize(root) });
            ncs.Add(NCSInstructionType.JMP, jump: returnInstruction);
            block.TempStack = tempStackBefore;
            return returnType;
        }

    }
}
