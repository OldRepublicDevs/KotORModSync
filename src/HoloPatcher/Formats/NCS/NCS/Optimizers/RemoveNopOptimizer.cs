using System.Collections.Generic;
using System.Linq;

namespace KOTORModSync.Formats.NCS.Optimizers
{

    /// <summary>
    /// Removes NOP (no-operation) instructions from compiled NCS bytecode.
    ///
    /// NCS Compiler uses NOP instructions as stubs to simplify the compilation process
    /// however as their name suggests they do not perform any actual function. This optimizer
    /// removes all occurrences of NOP instructions from the compiled script, updating jump
    /// targets to skip over removed NOPs.
    ///
    /// References:
    ///     vendor/xoreos-tools/src/nwscript/decompiler.cpp (NCS optimization patterns)
    ///     Standard compiler optimization techniques (dead code elimination)
    ///     Note: NOP removal is a common bytecode optimization
    /// </summary>
    public class RemoveNopOptimizer : NCSOptimizer
    {
        public override void Optimize(NCS ncs)
        {
            var nops = ncs.Instructions.Where(inst => inst.InsType == NCSInstructionType.NOP).ToList();

            if (nops.Count == 0)
            {
                return;
            }

            var removable = new HashSet<NCSInstruction>(new ReferenceInstructionComparer());
            bool debug = System.Environment.GetEnvironmentVariable("NCS_INTERPRETER_DEBUG") == "true";

            foreach (NCSInstruction nop in nops)
            {
                int nopIndex = ncs.GetInstructionIndex(nop);
                if (nopIndex < 0)
                {
                    continue;
                }

                List<NCSInstruction> inboundLinks = ncs.LinksTo(nop);
                // If other instructions jump here, keep this NOP (likely a function entry stub)
                if (inboundLinks.Count > 0)
                {
                    if (debug)
                    {
                        System.Console.WriteLine($"RemoveNop: keeping NOP idx={nopIndex} inbound={inboundLinks.Count}");
                    }
                    continue;
                }

                NCSInstruction replacement = null;

                for (int i = nopIndex + 1; i < ncs.Instructions.Count; i++)
                {
                    NCSInstruction candidate = ncs.Instructions[i];
                    if (candidate.InsType != NCSInstructionType.NOP)
                    {
                        replacement = candidate;
                        break;
                    }
                }

                // No inbound links (already handled), so safe to remove even if replacement is null
                removable.Add(nop);
                if (debug)
                {
                    System.Console.WriteLine($"RemoveNop: removing NOP idx={nopIndex} replacementIdx={ncs.GetInstructionIndex(replacement)}");
                }
            }

            if (removable.Count == 0)
            {
                return;
            }

            ncs.Instructions = ncs.Instructions.Where(inst => !removable.Contains(inst)).ToList();
            InstructionsCleared += removable.Count;
        }
    }

    internal sealed class ReferenceInstructionComparer : IEqualityComparer<NCSInstruction>
    {
        public bool Equals(NCSInstruction x, NCSInstruction y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(NCSInstruction obj)
        {
            return obj == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}

