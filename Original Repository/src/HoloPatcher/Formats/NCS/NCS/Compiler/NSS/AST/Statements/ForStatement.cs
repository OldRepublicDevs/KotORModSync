using System.Collections.Generic;
using KOTORModSync.Common.Script;
using KOTORModSync.Formats.NCS;
using JetBrains.Annotations;

namespace KOTORModSync.Formats.NCS.Compiler
{

    /// <summary>
    /// Represents a for loop statement.
    /// </summary>
    public class ForStatement : Statement
    {
        public Expression Initializer { get; set; }
        public Statement InitialStatement { get; set; }
        public Expression Condition { get; set; }
        public Expression Iterator { get; set; }
        public CodeBlock Body { get; set; }
        public List<BreakStatement> BreakStatements { get; set; }
        public List<ContinueStatement> ContinueStatements { get; set; }

        public ForStatement(Expression initializer, Expression condition, Expression iterator, CodeBlock body)
        {
            Initializer = initializer;
            InitialStatement = null;
            Condition = condition ?? throw new System.ArgumentNullException(nameof(condition));
            Iterator = iterator ?? throw new System.ArgumentNullException(nameof(iterator));
            Body = body ?? throw new System.ArgumentNullException(nameof(body));
            BreakStatements = new List<BreakStatement>();
            ContinueStatements = new List<ContinueStatement>();
        }

        public ForStatement(Statement initialStatement, Expression condition, Expression iterator, CodeBlock body)
        {
            Initializer = null;
            InitialStatement = initialStatement;
            Condition = condition ?? throw new System.ArgumentNullException(nameof(condition));
            Iterator = iterator ?? throw new System.ArgumentNullException(nameof(iterator));
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
            if (InitialStatement != null)
            {
                InitialStatement.Compile(ncs, root, block, returnInstruction, breakInstruction, continueInstruction);
            }
            else if (Initializer != null)
            {
                int tempStackBefore = block.TempStack;
                DynamicDataType initType = Initializer.Compile(ncs, root, block);
                if (block.TempStack == tempStackBefore)
                {
                    block.TempStack += initType.Size(root);
                }
                ncs.Add(NCSInstructionType.MOVSP, new List<object> { -initType.Size(root) });
                block.TempStack -= initType.Size(root);
            }

            block.MarkBreakScope();

            // Loop start (condition evaluation point)
            NCSInstruction loopStart = ncs.Add(NCSInstructionType.NOP, new List<object>());
            var updateStart = new NCSInstruction(NCSInstructionType.NOP);
            var loopEnd = new NCSInstruction(NCSInstructionType.NOP);

            // Compile condition (value consumed by JZ)
            int initialTempStack = block.TempStack;
            DynamicDataType conditionType = Condition.Compile(ncs, root, block);
            if (conditionType.Builtin != DataType.Int)
            {
                throw new CompileError(
                    $"for loop condition must be int type, got {conditionType.Builtin.ToScriptString()}\n" +
                    "  Note: Conditions must evaluate to int (0=false, non-zero=true)");
            }
            NCSInstruction jumpToEnd = ncs.Add(NCSInstructionType.JZ, new List<object>());
            block.TempStack = initialTempStack;

            block.MarkBreakScope();

            // Compile loop body
            Body.Compile(ncs, root, block, returnInstruction, loopEnd, updateStart);

            // Update/iteration point (continue jumps here)
            ncs.Instructions.Add(updateStart);

            // Compile iterator
            int tempStackBeforeIteration = block.TempStack;
            DynamicDataType iteratorType = Iterator.Compile(ncs, root, block);
            int tempStackAfterIteration = block.TempStack;
            if (tempStackAfterIteration == tempStackBeforeIteration)
            {
                block.TempStack += iteratorType.Size(root);
            }
            ncs.Add(NCSInstructionType.MOVSP, new List<object> { -iteratorType.Size(root) });
            block.TempStack -= iteratorType.Size(root);

            // Jump back to loop start (condition re-evaluation)
            ncs.Add(NCSInstructionType.JMP, jump: loopStart);

            // Loop end marker
            ncs.Instructions.Add(loopEnd);
            jumpToEnd.Jump = loopEnd;

            return DynamicDataType.VOID;
        }
    }
}

