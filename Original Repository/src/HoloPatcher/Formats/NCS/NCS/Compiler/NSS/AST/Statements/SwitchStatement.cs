using System;
using System.Collections.Generic;
using KOTORModSync.Formats.NCS;
using JetBrains.Annotations;

namespace KOTORModSync.Formats.NCS.Compiler
{

    /// <summary>
    /// Represents a switch statement.
    /// </summary>
    public class SwitchStatement : Statement
    {
        public Expression Expression { get; set; }
        public List<SwitchBlock> Blocks { get; set; }
        public CodeBlock RealBlock { get; set; }

        public SwitchStatement(Expression expression, List<SwitchBlock> blocks)
        {
            Expression = expression ?? throw new ArgumentNullException(nameof(expression));
            Blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));
            RealBlock = new CodeBlock();
        }

        public override object Compile(
            NCS ncs,
            CodeRoot root,
            CodeBlock block,
            NCSInstruction returnInstruction,
            [CanBeNull] NCSInstruction breakInstruction,
            [CanBeNull] NCSInstruction continueInstruction)
        {
            // Switch establishes its own break scope
            block.MarkBreakScope();

            RealBlock.Parent = block;
            CodeBlock switchBlock = RealBlock;

            // Compile the switch expression
            DynamicDataType expressionType = Expression.Compile(ncs, root, switchBlock);
            switchBlock.TempStack += expressionType.Size(root);

            NCSInstruction endOfSwitch = new NCSInstruction(NCSInstructionType.NOP, new List<object>());

            var tempNcs = new NCS();
            var switchBlockToInstruction = new Dictionary<SwitchBlock, NCSInstruction>();

            foreach (SwitchBlock switchBlockDef in Blocks)
            {
                NCSInstruction switchBlockStart = tempNcs.Add(NCSInstructionType.NOP, new List<object>());
                switchBlockToInstruction[switchBlockDef] = switchBlockStart;

                foreach (Statement statement in switchBlockDef.Statements)
                {
                    statement.Compile(tempNcs, root, switchBlock, returnInstruction, endOfSwitch, null);
                }
            }

            // Compile all case/default labels to check which block to jump to
            foreach (SwitchBlock switchBlockDef in Blocks)
            {
                foreach (SwitchLabel label in switchBlockDef.Labels)
                {
                    // Copy the switch expression to the top (so we can compare it multiple times)
                    ncs.Add(
                        NCSInstructionType.CPTOPSP,
                        new List<object> { -expressionType.Size(root), expressionType.Size(root) });

                    label.Compile(ncs, root, switchBlock, switchBlockToInstruction[switchBlockDef], expressionType);
                }
            }

            // If none of the labels match, jump over the code block
            ncs.Add(NCSInstructionType.JMP, new List<object>()).Jump = endOfSwitch;

            // Merge the switch block code into the main NCS
            ncs.Merge(tempNcs);
            ncs.Instructions.Add(endOfSwitch);

            // Pop the switch expression
            ncs.Add(NCSInstructionType.MOVSP, new List<object> { -4 });
            switchBlock.TempStack -= expressionType.Size(root);

            return DynamicDataType.VOID;
        }
    }

    /// <summary>
    /// Represents a block of statements within a switch (associated with case/default labels).
    /// </summary>
    public class SwitchBlock
    {
        public List<SwitchLabel> Labels { get; set; }
        public List<Statement> Statements { get; set; }

        public SwitchBlock(List<SwitchLabel> labels, List<Statement> statements)
        {
            Labels = labels ?? new List<SwitchLabel>();
            Statements = statements ?? new List<Statement>();
        }
    }

    /// <summary>
    /// Base class for switch labels (case and default).
    /// </summary>
    public abstract class SwitchLabel
    {
        public abstract void Compile(
            NCS ncs,
            CodeRoot root,
            CodeBlock block,
            NCSInstruction jumpTo,
            DynamicDataType expressionType);
    }

    /// <summary>
    /// Represents a case label in a switch statement.
    /// </summary>
    public class CaseSwitchLabel : SwitchLabel
    {
        public Expression Expression { get; set; }

        public CaseSwitchLabel(Expression expression)
        {
            Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }

        public override void Compile(
            NCS ncs,
            CodeRoot root,
            CodeBlock block,
            NCSInstruction jumpTo,
            DynamicDataType expressionType)
        {
            // Compare the copied switch expression to the case label expression
            DynamicDataType labelType = Expression.Compile(ncs, root, block);

            NCSInstructionType equalityInstruction = GetLogicalEqualityInstruction(expressionType, labelType);
            ncs.Add(equalityInstruction, new List<object>());

            // If the expressions match, jump to the appropriate block
            ncs.Add(NCSInstructionType.JNZ, new List<object>()).Jump = jumpTo;
        }

        private static NCSInstructionType GetLogicalEqualityInstruction(DynamicDataType type1, DynamicDataType type2)
        {
            if (type1.Builtin == Common.Script.DataType.Int && type2.Builtin == Common.Script.DataType.Int)
            {
                return NCSInstructionType.EQUALII;
            }
            if (type1.Builtin == Common.Script.DataType.Float && type2.Builtin == Common.Script.DataType.Float)
            {
                return NCSInstructionType.EQUALFF;
            }
            throw new CompileError($"Unsupported comparison between '{type1}' and '{type2}' in switch statement");
        }

        public override string ToString()
        {
            return $"case {Expression}:";
        }
    }

    /// <summary>
    /// Represents a default label in a switch statement.
    /// </summary>
    public class DefaultSwitchLabel : SwitchLabel
    {
        public override void Compile(
            NCS ncs,
            CodeRoot root,
            CodeBlock block,
            NCSInstruction jumpTo,
            DynamicDataType expressionType)
        {
            // Default always jumps to its block
            ncs.Add(NCSInstructionType.JMP, new List<object>()).Jump = jumpTo;
        }

        public override string ToString()
        {
            return "default:";
        }
    }
}

