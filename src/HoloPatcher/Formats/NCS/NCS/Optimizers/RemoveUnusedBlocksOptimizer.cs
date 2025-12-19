using System.Collections.Generic;
using System.Linq;

namespace KOTORModSync.Formats.NCS.Optimizers
{
    /// <summary>
    /// Removes unreachable instruction blocks (dead code elimination).
    /// </summary>
    public class RemoveUnusedBlocksOptimizer : NCSOptimizer
    {
        public override void Optimize(NCS ncs)
        {
            // Find list of unreachable instructions
            var reachable = new HashSet<NCSInstruction>();
            var checking = new Queue<int>();
            checking.Enqueue(0);

            while (checking.Count > 0)
            {
                int check = checking.Dequeue();
                if (check >= ncs.Instructions.Count)
                {
                    continue;
                }

                NCSInstruction instruction = ncs.Instructions[check];
                if (reachable.Contains(instruction))
                {
                    continue;
                }

                reachable.Add(instruction);

                if (instruction.InsType == NCSInstructionType.JZ ||
                    instruction.InsType == NCSInstructionType.JNZ ||
                    instruction.InsType == NCSInstructionType.JSR)
                {
                    if (instruction.Jump == null)
                    {
                        throw new System.InvalidOperationException($"{instruction} has a NoneType jump.");
                    }
                    checking.Enqueue(ncs.GetInstructionIndex(instruction.Jump));
                    checking.Enqueue(check + 1);
                }
                else if (instruction.InsType == NCSInstructionType.JMP)
                {
                    if (instruction.Jump == null)
                    {
                        throw new System.InvalidOperationException($"{instruction} has a NoneType jump.");
                    }
                    checking.Enqueue(ncs.GetInstructionIndex(instruction.Jump));
                }
                else if (instruction.InsType == NCSInstructionType.RETN)
                {
                    // RETN ends execution
                }
                else
                {
                    checking.Enqueue(check + 1);
                }
            }

            var unreachable = ncs.Instructions.Where(instruction => !reachable.Contains(instruction)).ToList();
            foreach (NCSInstruction instruction in unreachable)
            {
                // We do not have to worry about fixing any instructions that JMP since the target instructions here should
                // be detached for the actual (reachable) script.
                ncs.Instructions.Remove(instruction);
                InstructionsCleared++;
            }
        }
    }
}


