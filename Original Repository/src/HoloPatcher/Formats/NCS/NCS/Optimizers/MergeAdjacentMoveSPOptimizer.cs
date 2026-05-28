using System.Collections.Generic;

namespace KOTORModSync.Formats.NCS.Optimizers
{
    /// <summary>
    /// Merges consecutive MOVSP instructions into a single instruction.
    ///
    /// Multiple adjacent stack pointer movements can be combined into one,
    /// reducing bytecode size and improving execution efficiency.
    /// </summary>
    public class MergeAdjacentMoveSPOptimizer : NCSOptimizer
    {
        public override void Optimize(NCS ncs)
        {
            int i = 0;
            while (i < ncs.Instructions.Count - 1)
            {
                NCSInstruction instruction = ncs.Instructions[i];

                if (instruction.InsType != NCSInstructionType.MOVSP)
                {
                    i++;
                    continue;
                }

                // Check if next instruction is also MOVSP and nothing jumps to it
                NCSInstruction nextInst = ncs.Instructions[i + 1];
                if (nextInst.InsType == NCSInstructionType.MOVSP && ncs.LinksTo(nextInst).Count == 0)
                {
                    // Merge: add the offsets together
                    int combinedOffset = (int)instruction.Args[0] + (int)nextInst.Args[0];
                    instruction.Args[0] = combinedOffset;

                    // Remove the second MOVSP
                    ncs.Instructions.RemoveAt(i + 1);
                    InstructionsCleared++;

                    // Don't increment i, check if we can merge more
                    continue;
                }

                i++;
            }
        }
    }
}

