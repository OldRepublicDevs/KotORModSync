using System.Collections.Generic;
using KOTORModSync.Formats.NCS;
using JetBrains.Annotations;

namespace KOTORModSync.Formats.NCS.Compiler
{

    /// <summary>
    /// Represents a continue statement in a loop.
    /// </summary>
    public class ContinueStatement : Statement
    {
        [CanBeNull]
        public NCSInstruction JumpTarget { get; set; }

        public override object Compile(
            NCS ncs,
            CodeRoot root,
            CodeBlock block,
            NCSInstruction returnInstruction,
            [CanBeNull] NCSInstruction breakInstruction,
            [CanBeNull] NCSInstruction continueInstruction)
        {
            if (continueInstruction == null)
            {
                throw new CompileError("continue statement not inside loop");
            }

            ncs.Add(NCSInstructionType.MOVSP, new List<object> { -block.BreakScopeSize(root) });
            ncs.Add(NCSInstructionType.JMP, jump: continueInstruction);
            return DynamicDataType.VOID;
        }
    }
}

