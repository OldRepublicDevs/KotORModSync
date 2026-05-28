using System.Collections.Generic;
using KOTORModSync.Common.Script;
using KOTORModSync.Formats.NCS;
using JetBrains.Annotations;

namespace KOTORModSync.Formats.NCS.Compiler
{

    /// <summary>
    /// Represents an if/else statement.
    /// </summary>
    public class IfStatement : Statement
    {
        public Expression Condition { get; set; }
        public CodeBlock ThenBlock { get; set; }
        [CanBeNull]
        public CodeBlock ElseBlock { get; set; }

        public IfStatement(Expression condition, [CanBeNull] CodeBlock thenBlock, [CanBeNull] CodeBlock elseBlock = null)
        {
            Condition = condition ?? throw new System.ArgumentNullException(nameof(condition));
            ThenBlock = thenBlock ?? throw new System.ArgumentNullException(nameof(thenBlock));
            ElseBlock = elseBlock;
        }

        public override object Compile(
            NCS ncs,
            CodeRoot root,
            CodeBlock block,
            NCSInstruction returnInstruction,
            [CanBeNull] NCSInstruction breakInstruction,
            [CanBeNull] NCSInstruction continueInstruction)
        {
            // Save temp_stack state before condition
            int initialTempStack = block.TempStack;

            // Compile condition
            DynamicDataType conditionType = Condition.Compile(ncs, root, block);

            if (conditionType.Builtin != DataType.Int)
            {
                throw new CompileError(
                    $"Condition must be int type, got {conditionType.Builtin.ToScriptString()}\n" +
                    "  Note: In NWScript, conditions must evaluate to int (0=false, non-zero=true)");
            }

            if (ElseBlock != null)
            {
                // If condition is false (zero), jump to else block
                NCSInstruction jumpToElse = ncs.Add(NCSInstructionType.JZ, new List<object>());
                // JZ consumes the condition value from stack
                block.TempStack = initialTempStack;

                // Save temp_stack before compiling block
                int blockTempStackBefore = block.TempStack;
                // Compile then block
                ThenBlock.Compile(ncs, root, block, returnInstruction, breakInstruction, continueInstruction);
                // Block should clear its own temp_stack, restore parent's temp_stack
                block.TempStack = blockTempStackBefore;

                // Jump over else block after then completes
                NCSInstruction jumpToEnd = ncs.Add(NCSInstructionType.JMP, new List<object>());

                // Else block starts here
                NCSInstruction elseStart = ncs.Add(NCSInstructionType.NOP, new List<object>());
                jumpToElse.Jump = elseStart;

                // Save temp_stack before compiling else block
                int elseTempStackBefore = block.TempStack;
                // Compile else block
                ElseBlock.Compile(ncs, root, block, returnInstruction, breakInstruction, continueInstruction);
                // Else block should clear its own temp_stack, restore parent's temp_stack
                block.TempStack = elseTempStackBefore;

                // End marker
                NCSInstruction endMarker = ncs.Add(NCSInstructionType.NOP, new List<object>());
                jumpToEnd.Jump = endMarker;
            }
            else
            {
                // If condition is false (zero), jump past then block
                NCSInstruction jumpToEnd = ncs.Add(NCSInstructionType.JZ, new List<object>());
                // JZ consumes the condition value from stack
                block.TempStack = initialTempStack;

                // Save temp_stack before compiling block
                int blockTempStackBefore = block.TempStack;
                // Compile then block
                ThenBlock.Compile(ncs, root, block, returnInstruction, breakInstruction, continueInstruction);
                // Block should clear its own temp_stack, restore parent's temp_stack
                block.TempStack = blockTempStackBefore;

                // End marker
                NCSInstruction endMarker = ncs.Add(NCSInstructionType.NOP, new List<object>());
                jumpToEnd.Jump = endMarker;
            }

            return DynamicDataType.VOID;
        }
    }
}

