using System.Collections.Generic;
using KOTORModSync.Common.Script;
using KOTORModSync.Formats.NCS;
using JetBrains.Annotations;

namespace KOTORModSync.Formats.NCS.Compiler
{

    /// <summary>
    /// Represents a while loop statement.
    /// </summary>
    public class WhileStatement : Statement
    {
        public Expression Condition { get; set; }
        public CodeBlock Body { get; set; }
        public List<BreakStatement> BreakStatements { get; set; }
        public List<ContinueStatement> ContinueStatements { get; set; }

        public WhileStatement(Expression condition, CodeBlock body)
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
            block.MarkBreakScope();

            // Loop start (condition evaluation point)
            NCSInstruction loopStart = ncs.Add(NCSInstructionType.NOP, new List<object>());
            var loopEnd = new NCSInstruction(NCSInstructionType.NOP);

            // Save temp_stack before condition (condition pushes a value, JZ consumes it)
            int initialTempStack = block.TempStack;
            // Compile condition
            DynamicDataType conditionType = Condition.Compile(ncs, root, block);

            if (conditionType.Builtin != DataType.Int)
            {
                throw new CompileError(
                    $"While condition must be int type, got {conditionType.Builtin.ToScriptString()}\n" +
                    "  Note: In NWScript, conditions must evaluate to int (0=false, non-zero=true)");
            }

            // JZ consumes the condition value from stack
            // Matching PyKotor classes.py line 2623: ncs.add(NCSInstructionType.JZ, jump=loopend)
            ncs.Add(NCSInstructionType.JZ, jump: loopEnd);
            // Restore temp_stack since JZ consumed the condition
            block.TempStack = initialTempStack;

            // Compile loop body
            // Matching PyKotor classes.py line 2627: self.block.compile(ncs, root, block, return_instruction, loopend, loopstart)
            Body.Compile(ncs, root, block, returnInstruction, loopEnd, loopStart);

            // Jump back to loop start
            // Matching PyKotor classes.py line 2628: ncs.add(NCSInstructionType.JMP, jump=loopstart)
            ncs.Add(NCSInstructionType.JMP, jump: loopStart);

            // Loop end marker
            // Matching PyKotor classes.py line 2630: ncs.instructions.append(loopend)
            ncs.Instructions.Add(loopEnd);

            return DynamicDataType.VOID;
        }
    }
}

