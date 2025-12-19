using KOTORModSync.Formats.NCS;
using JetBrains.Annotations;

namespace KOTORModSync.Formats.NCS.Compiler
{

    /// <summary>
    /// Represents an empty statement (just a semicolon).
    /// </summary>
    public class EmptyStatement : Statement
    {
        public override object Compile(
            NCS ncs,
            CodeRoot root,
            CodeBlock block,
            NCSInstruction returnInstruction,
            [CanBeNull] NCSInstruction breakInstruction,
            [CanBeNull] NCSInstruction continueInstruction)
        {
            // No code generation needed for empty statement
            return DynamicDataType.VOID;
        }
    }
}

