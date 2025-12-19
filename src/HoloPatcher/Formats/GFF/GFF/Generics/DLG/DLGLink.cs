using System;
using KOTORModSync;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics.DLG
{
    /// <summary>
    /// Represents a directed edge from a source node to a target node (DLGNode).
    /// </summary>
    [PublicAPI]
    public sealed class DLGLink : IEquatable<DLGLink>
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/links.py:21
        // Original: class DLGLink(Generic[T_co]):
        private readonly int _hashCache;

        public DLGNode Node { get; set; }
        public int ListIndex { get; set; } = -1;

        // Conditional scripts
        public ResRef Active1 { get; set; } = ResRef.FromBlank();
        public ResRef Active2 { get; set; } = ResRef.FromBlank();
        public bool Logic { get; set; }
        public bool Active1Not { get; set; }
        public bool Active2Not { get; set; }

        // Script parameters
        public int Active1Param1 { get; set; }
        public int Active1Param2 { get; set; }
        public int Active1Param3 { get; set; }
        public int Active1Param4 { get; set; }
        public int Active1Param5 { get; set; }
        public string Active1Param6 { get; set; } = string.Empty;
        public int Active2Param1 { get; set; }
        public int Active2Param2 { get; set; }
        public int Active2Param3 { get; set; }
        public int Active2Param4 { get; set; }
        public int Active2Param5 { get; set; }
        public string Active2Param6 { get; set; } = string.Empty;

        // Other
        public bool IsChild { get; set; }
        public string Comment { get; set; } = string.Empty;

        public DLGLink(DLGNode node, int listIndex = -1)
        {
            _hashCache = Guid.NewGuid().GetHashCode();
            Node = node;
            ListIndex = listIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is DLGLink other && Equals(other);
        }

        public bool Equals(DLGLink other)
        {
            if (other == null) return false;
            return _hashCache == other._hashCache;
        }

        public override int GetHashCode()
        {
            return _hashCache;
        }

        public string PartialPath(bool isStarter)
        {
            string p1 = isStarter ? "StartingList" : (Node is DLGEntry ? "EntriesList" : "RepliesList");
            return $"{p1}\\{ListIndex}";
        }
    }
}
