using KOTORModSync.Formats.NCS;
using JetBrains.Annotations;

namespace KOTORModSync.Formats.NCS.Compiler
{

    /// <summary>
    /// Represents an expression used as a statement (e.g., function call, assignment).
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/classes.py:2339-2382
    /// </summary>
    public class ExpressionStatement : Statement
    {
        public Expression Expression { get; set; }

        public ExpressionStatement(Expression expression)
        {
            Expression = expression
                         ?? throw new System.ArgumentNullException(nameof(expression));
        }

        // Matching PyKotor classes.py lines 2344-2355
        public override object Compile(
            NCS ncs,
            CodeRoot root,
            CodeBlock block,
            NCSInstruction returnInstruction,
            [CanBeNull] NCSInstruction breakInstruction,
            [CanBeNull] NCSInstruction continueInstruction)
        {
            int tempStackBefore = block.TempStack;
            DynamicDataType resultType = Expression.Compile(ncs, root, block);
            int tempStackAfter = block.TempStack;
            // Expression compiled, remove its result from stack and temp_stack tracking
            // Note: Some expressions (like Assignment) already remove their result from the stack,
            // so we only need to remove it from temp_stack if it's still on the stack.
            // We check temp_stack to see if the result is still tracked.
            // For void expressions, we still need to check if temp_stack increased (e.g., from nested function calls)
            // Matching PyKotor classes.py lines 2356-2382
            if (resultType != DynamicDataType.VOID)
            {
                int expressionSize = resultType.Size(root);
                // Check if expression added to temp_stack
                // Matching PyKotor classes.py lines 2363-2367
                if (tempStackAfter > tempStackBefore)
                {
                    // Expression added to temp_stack, so result is on the stack - remove it
                    ncs.Add(NCSInstructionType.MOVSP, new System.Collections.Generic.List<object> { -expressionSize });
                    block.TempStack -= expressionSize;
                }
                // Matching PyKotor classes.py lines 2368-2371
                else if (tempStackAfter == tempStackBefore)
                {
                    // Expression didn't add to temp_stack, but result is still on the stack (e.g., StringExpression, IntExpression)
                    // We need to remove it from the stack (but don't update temp_stack since it wasn't tracking it)
                    ncs.Add(NCSInstructionType.MOVSP, new System.Collections.Generic.List<object> { -expressionSize });
                }
                // Matching PyKotor classes.py lines 2372-2374
                else
                {
                    // temp_stack decreased, which means the expression already removed its result
                    // Nothing to do
                }
            }
            // Matching PyKotor classes.py lines 2375-2382
            else
            {
                // Void expression - check if temp_stack increased (shouldn't happen, but clean up if it did)
                if (tempStackAfter > tempStackBefore)
                {
                    // Something was left on the stack (e.g., from nested function call arguments)
                    int cleanupSize = tempStackAfter - tempStackBefore;
                    ncs.Add(NCSInstructionType.MOVSP, new System.Collections.Generic.List<object> { -cleanupSize });
                    block.TempStack -= cleanupSize;
                }
                // else: no cleanup needed - void expression with balanced stack
            }

            return DynamicDataType.VOID;
        }
    }
}

