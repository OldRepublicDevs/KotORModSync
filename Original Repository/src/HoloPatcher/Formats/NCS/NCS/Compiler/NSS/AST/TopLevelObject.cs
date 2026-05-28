using KOTORModSync.Formats.NCS;

namespace KOTORModSync.Formats.NCS.Compiler
{

    /// <summary>
    /// Base class for top-level NSS declarations (functions, globals, structs, includes).
    /// </summary>
    public abstract class TopLevelObject
    {
        /// <summary>
        /// Compile this top-level object to NCS bytecode.
        /// </summary>
        /// <param name="ncs">NCS object to emit instructions to</param>
        /// <param name="root">Root compilation context</param>
        public abstract void Compile(NCS ncs, CodeRoot root);
    }
}

