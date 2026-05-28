using System;
using System.Collections.Generic;
using KOTORModSync.Common.Script;
using KOTORModSync.Formats.NCS;

namespace KOTORModSync.Formats.NCS.Compiler
{
    /// <summary>
    /// Represents a scoped block statement (e.g., { int x; }).
    /// </summary>
    public class ScopedBlockStatement : Statement
    {
        public CodeBlock Block { get; set; }

        public ScopedBlockStatement(CodeBlock block)
        {
            Block = block ?? throw new ArgumentNullException(nameof(block));
        }

        public override object Compile(
            NCS ncs,
            CodeRoot root,
            CodeBlock block,
            NCSInstruction returnInstruction,
            NCSInstruction breakInstruction,
            NCSInstruction continueInstruction)
        {
            // Python: self._parent = block (set parent to the outer block, not the inner block)
            Block.Parent = block;
            // Pass the outer block as the parent parameter, not the inner Block
            Block.Compile(ncs, root, block, returnInstruction, breakInstruction, continueInstruction);
            return DynamicDataType.VOID;
        }
    }
}


