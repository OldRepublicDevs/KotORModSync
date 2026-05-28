using System.Collections.Generic;

namespace KOTORModSync.Memory
{

    /// <summary>
    /// Stores memory tokens used during patching
    /// </summary>
    public class PatcherMemory
    {
        /// <summary>
        /// 2DAMemory# (token) -> string value
        /// </summary>
        public Dictionary<int, string> Memory2DA { get; } = new Dictionary<int, string>();

        /// <summary>
        /// StrRef# (token) -> dialog.tlk index
        /// </summary>
        public Dictionary<int, int> MemoryStr { get; } = new Dictionary<int, int>();

        public override string ToString()
        {
            return $"PatcherMemory(memory_2da={Memory2DA.Count} items, memory_str={MemoryStr.Count} items)";
        }
    }
}

