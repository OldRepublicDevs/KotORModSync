using System.Collections.Generic;
using System.Linq;

namespace KOTORModSync.Formats.NCS.Optimizers
{
    /// <summary>
    /// Removes JMP instructions that jump to the immediately following instruction.
    ///
    /// Such jumps are redundant as execution would naturally flow to the next
    /// instruction anyway.
    /// </summary>
    public class RemoveJMPToAdjacentOptimizer : NCSOptimizer
    {
        public override void Optimize(NCS ncs)
        {
            var removals = new List<NCSInstruction>();

            for (int i = 0; i < ncs.Instructions.Count - 1; i++) // Skip last instruction
            {
                NCSInstruction instruction = ncs.Instructions[i];
                if (instruction.InsType != NCSInstructionType.JMP)
                {
                    continue;
                }

                if (instruction.Jump == null)
                {
                    continue;
                }

                // Check if this JMP targets the very next instruction
                NCSInstruction nextInstruction = ncs.Instructions[i + 1];
                if (ReferenceEquals(instruction.Jump, nextInstruction))
                {
                    // This JMP is redundant
                    removals.Add(instruction);
                }
            }

            // Remove all redundant JMPs
            foreach (NCSInstruction instruction in removals)
            {
                ncs.Instructions.Remove(instruction);
                InstructionsCleared++;
            }
        }
    }
}


