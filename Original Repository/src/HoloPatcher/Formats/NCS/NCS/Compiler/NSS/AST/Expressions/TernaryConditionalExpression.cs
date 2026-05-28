using System;
using System.Collections.Generic;
using KOTORModSync.Common.Script;
using KOTORModSync.Formats.NCS;

namespace KOTORModSync.Formats.NCS.Compiler
{
    /// <summary>
    /// Represents a ternary conditional expression (condition ? trueExpr : falseExpr).
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/classes.py:1445-1505
    /// </summary>
    public class TernaryConditionalExpression : Expression
    {
        public Expression Condition { get; set; }
        public Expression TrueExpression { get; set; }
        public Expression FalseExpression { get; set; }

        public TernaryConditionalExpression(
            Expression condition,
            Expression trueExpr,
            Expression falseExpr)
        {
            Condition = condition ?? throw new ArgumentNullException(nameof(condition));
            TrueExpression = trueExpr ?? throw new ArgumentNullException(nameof(trueExpr));
            FalseExpression = falseExpr ?? throw new ArgumentNullException(nameof(falseExpr));
        }

        public override DynamicDataType Compile(NCS ncs, CodeRoot root, CodeBlock block)
        {
            // Matching PyKotor classes.py line 1454: initial_stack = block.temp_stack
            int initialStack = block.TempStack;

            // Matching PyKotor classes.py line 1457: condition_type = self.condition.compile(ncs, root, block)
            DynamicDataType conditionType = Condition.Compile(ncs, root, block);

            // Matching PyKotor classes.py line 1458-1463
            if (conditionType.Builtin != DataType.Int)
            {
                throw new NSS.CompileError(
                    $"Ternary condition must be integer type, got {conditionType.Builtin.ToScriptString()}\n" +
                    "  Note: Conditions must evaluate to int (0 = false, non-zero = true)");
            }

            // Matching PyKotor classes.py line 1466-1467
            // Jump to false branch if condition is zero (JZ consumes the condition from stack)
            NCSInstruction falseLabel = new NCSInstruction(NCSInstructionType.NOP, new List<object>());
            ncs.Add(NCSInstructionType.JZ, new List<object>(), falseLabel);

            // Matching PyKotor classes.py line 1469: JZ consumed the condition, so update stack tracking
            block.TempStack = initialStack;

            // Matching PyKotor classes.py line 1472-1473: Compile true expression
            DynamicDataType trueType = TrueExpression.Compile(ncs, root, block);
            block.TempStack += trueType.Size(root);

            // Matching PyKotor classes.py line 1476-1477: Jump to end after true expression
            NCSInstruction endLabel = new NCSInstruction(NCSInstructionType.NOP, new List<object>());
            ncs.Add(NCSInstructionType.JMP, new List<object>(), endLabel);

            // Matching PyKotor classes.py line 1481: False branch
            // Stack state: same as after condition (condition was popped by JZ)
            ncs.Instructions.Add(falseLabel);

            // Matching PyKotor classes.py line 1483: Reset temp_stack to state after condition was popped
            block.TempStack = initialStack;

            // Matching PyKotor classes.py line 1484-1486
            DynamicDataType falseType = FalseExpression.Compile(ncs, root, block);
            // Explicitly track that false branch result is on the stack
            block.TempStack += falseType.Size(root);

            // Matching PyKotor classes.py line 1489-1496: Type check - both branches must have same type
            if (trueType.Builtin != falseType.Builtin)
            {
                throw new NSS.CompileError(
                    $"Type mismatch in ternary operator\n" +
                    $"  True branch type: {trueType.Builtin.ToScriptString()}\n" +
                    $"  False branch type: {falseType.Builtin.ToScriptString()}\n" +
                    "  Both branches must have the same type");
            }

            // Matching PyKotor classes.py line 1502: End label
            ncs.Instructions.Add(endLabel);

            // Matching PyKotor classes.py line 1504: At end, stack has result from one branch
            block.TempStack = initialStack + trueType.Size(root);

            return trueType;
        }

        public override string ToString()
        {
            return $"({Condition} ? {TrueExpression} : {FalseExpression})";
        }
    }
}

