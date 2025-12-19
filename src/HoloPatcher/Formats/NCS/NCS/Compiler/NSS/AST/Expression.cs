using KOTORModSync.Formats.NCS;

namespace KOTORModSync.Formats.NCS.Compiler
{

    /// <summary>
    /// Base class for all NSS expressions.
    /// 
    /// Expressions evaluate to a value and can be compiled to NCS bytecode.
    /// </summary>
    public abstract class Expression
    {
        /// <summary>
        /// Compile this expression to NCS bytecode.
        /// </summary>
        /// <param name="ncs">NCS object to emit instructions to</param>
        /// <param name="root">Root compilation context</param>
        /// <param name="block">Current code block context</param>
        /// <returns>The data type of the expression result</returns>
        public abstract DynamicDataType Compile(NCS ncs, CodeRoot root, CodeBlock block);
    }
}

