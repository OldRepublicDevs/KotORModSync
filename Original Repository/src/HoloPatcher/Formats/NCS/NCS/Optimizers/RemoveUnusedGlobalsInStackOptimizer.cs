using System;

namespace KOTORModSync.Formats.NCS.Optimizers
{
    /// <summary>
    /// Optimizer to remove unused global variables from the stack.
    /// Currently not implemented in Python (raises NotImplementedError).
    /// </summary>
    public class RemoveUnusedGlobalsInStackOptimizer : NCSOptimizer
    {
        public override void Optimize(NCS ncs)
        {
            throw new NotImplementedException("RemoveUnusedGlobalsInStackOptimizer is not yet implemented. This matches the Python implementation which also raises NotImplementedError.");
        }
    }
}

