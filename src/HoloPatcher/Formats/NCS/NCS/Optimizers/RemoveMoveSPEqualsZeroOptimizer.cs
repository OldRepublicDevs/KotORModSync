using System.Collections.Generic;
using System.Linq;

namespace KOTORModSync.Formats.NCS.Optimizers
{
    /// <summary>
    /// Removes MOVSP instructions with offset 0, which are redundant.
    /// </summary>
    public class RemoveMoveSPEqualsZeroOptimizer : NCSOptimizer
    {
        public override void Optimize(NCS ncs)
        {
            var movsp0 = ncs.Instructions
                .Where(inst => inst.InsType == NCSInstructionType.MOVSP && inst.Args.Count > 0 && inst.Args[0].Equals(0))
                .ToList();

            // Process instructions which jump to a MOVSP=0 and set them to jump to the proceeding instruction instead
            foreach (NCSInstruction op in movsp0)
            {
                int nopIndex = ncs.GetInstructionIndex(op);
                if (nopIndex < 0 || nopIndex + 1 >= ncs.Instructions.Count)
                {
                    continue;
                }

                List<NCSInstruction> links = ncs.LinksTo(op);
                foreach (NCSInstruction link in links)
                {
                    link.Jump = ncs.Instructions[nopIndex + 1];
                }
            }

            // It is now safe to remove all MOVSP=0 instructions
            ncs.Instructions = ncs.Instructions
                .Where(inst => !(inst.InsType == NCSInstructionType.MOVSP && inst.Args.Count > 0 && inst.Args[0].Equals(0)))
                .ToList();

            InstructionsCleared = movsp0.Count;
        }
    }
}

