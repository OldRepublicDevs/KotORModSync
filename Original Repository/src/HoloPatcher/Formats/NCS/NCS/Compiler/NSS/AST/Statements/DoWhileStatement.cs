using System.Collections.Generic;
using KOTORModSync.Common.Script;
using JetBrains.Annotations;

namespace KOTORModSync.Formats.NCS.Compiler
{

    /// <summary>
    /// Represents a do-while loop statement.
    /// </summary>
    public class DoWhileStatement : Statement
    {
        public Expression Condition { get; set; }
        public CodeBlock Body { get; set; }
        public List<BreakStatement> BreakStatements { get; set; }
        public List<ContinueStatement> ContinueStatements { get; set; }

        public DoWhileStatement(Expression condition, CodeBlock body)
        {
            Condition = condition ?? throw new System.ArgumentNullException(nameof(condition));
            Body = body ?? throw new System.ArgumentNullException(nameof(body));
            BreakStatements = new List<BreakStatement>();
            ContinueStatements = new List<ContinueStatement>();
        }

        public override object Compile(
            NCS ncs,
            CodeRoot root,
            CodeBlock block,
            NCSInstruction returnInstruction,
            [CanBeNull] NCSInstruction breakInstruction,
            [CanBeNull] NCSInstruction continueInstruction)
        {
            // Tell break/continue statements to stop here when getting scope size
            block.MarkBreakScope();

            // Loop start (body execution point)
            NCSInstruction loopStart = ncs.Add(NCSInstructionType.NOP, new List<object>());
            var conditionStart = new NCSInstruction(NCSInstructionType.NOP);
            var loopEnd = new NCSInstruction(NCSInstructionType.NOP);

            // Compile loop body
            Body.Compile(ncs, root, block, returnInstruction, loopEnd, conditionStart);

            // Condition start (continue jumps here)
            ncs.Instructions.Add(conditionStart);

            // Save temp_stack before condition (condition pushes a value, JZ consumes it)
            int initialTempStack = block.TempStack;
            // Compile condition
            DynamicDataType conditionType = Condition.Compile(ncs, root, block);

            if (conditionType.Builtin != DataType.Int)
            {
                throw new CompileError(
                    $"do-while condition must be int type, got {conditionType.Builtin.ToScriptString()}\n" +
                    "  Note: Conditions must evaluate to int (0=false, non-zero=true)");
            }

            // JZ consumes the condition value from stack
            NCSInstruction jumpToEnd = ncs.Add(NCSInstructionType.JZ, new List<object>());
            // Restore temp_stack since JZ consumed the condition
            block.TempStack = initialTempStack;

            // Jump back to loop start
            ncs.Add(NCSInstructionType.JMP, jump: loopStart);

            // Loop end marker
            ncs.Instructions.Add(loopEnd);
            jumpToEnd.Jump = loopEnd;

            return DynamicDataType.VOID;
        }
    }
}

