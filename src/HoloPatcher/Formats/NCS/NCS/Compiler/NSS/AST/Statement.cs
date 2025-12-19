using KOTORModSync.Formats.NCS;
using JetBrains.Annotations;

namespace KOTORModSync.Formats.NCS.Compiler
{

    /// <summary>
    /// Base class for all NSS statements.
    /// 
    /// Statements perform actions and control flow, but do not produce values.
    /// </summary>
    public abstract class Statement
    {
        public int? LineNum { get; set; }

        /// <summary>
        /// Compile this statement to NCS bytecode.
        /// </summary>
        /// <param name="ncs">NCS object to emit instructions to</param>
        /// <param name="root">Root compilation context</param>
        /// <param name="block">Current code block context</param>
        /// <param name="returnInstruction">Instruction to jump to for return</param>
        /// <param name="breakInstruction">Instruction to jump to for break (can be null)</param>
        /// <param name="continueInstruction">Instruction to jump to for continue (can be null)</param>
        /// <returns>Return type (DynamicDataType) or null/void</returns>
        public abstract object Compile(
            NCS ncs,
            CodeRoot root,
            CodeBlock block,
            NCSInstruction returnInstruction,
            [CanBeNull] NCSInstruction breakInstruction,
            [CanBeNull] NCSInstruction continueInstruction);
    }
}

