
using System;
using KOTORModSync;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics.CNV
{
    /// <summary>
    /// Represents a directed edge from a source node to a target node (CNVNode).
    /// </summary>
    /// <remarks>
    /// CNV Link:
    /// - Eclipse Engine conversation format (Dragon Age, )
    /// - Links conversation nodes together (entries to replies, replies to entries)
    /// - Contains conditional logic for link availability
    /// - Similar structure to DLG links but adapted for Eclipse conversation system
    /// </remarks>
    [PublicAPI]
    public sealed class CNVLink : IEquatable<CNVLink>
    {
        // Matching pattern from DLGLink
        // Original: Link between conversation nodes in Eclipse format
        private readonly int _hashCache;

        /// <summary>
        /// Target node this link points to.
        /// </summary>
        public CNVNode Node { get; set; }

        /// <summary>
        /// Index in the link list.
        /// </summary>
        public int ListIndex { get; set; } = -1;

        // Conditional scripts
        /// <summary>
        /// Primary conditional script ResRef (must pass for link to be available).
        /// </summary>
        public ResRef Active1 { get; set; } = ResRef.FromBlank();

        /// <summary>
        /// Secondary conditional script ResRef (Eclipse-specific).
        /// </summary>
        public ResRef Active2 { get; set; } = ResRef.FromBlank();

        /// <summary>
        /// Logic operator (true = AND, false = OR) for combining Active1 and Active2.
        /// </summary>
        public bool Logic { get; set; }

        /// <summary>
        /// Whether to negate Active1 condition.
        /// </summary>
        public bool Active1Not { get; set; }

        /// <summary>
        /// Whether to negate Active2 condition.
        /// </summary>
        public bool Active2Not { get; set; }

        // Script parameters
        /// <summary>
        /// Active1 parameter 1.
        /// </summary>
        public int Active1Param1 { get; set; }

        /// <summary>
        /// Active1 parameter 2.
        /// </summary>
        public int Active1Param2 { get; set; }

        /// <summary>
        /// Active1 parameter 3.
        /// </summary>
        public int Active1Param3 { get; set; }

        /// <summary>
        /// Active1 parameter 4.
        /// </summary>
        public int Active1Param4 { get; set; }

        /// <summary>
        /// Active1 parameter 5.
        /// </summary>
        public int Active1Param5 { get; set; }

        /// <summary>
        /// Active1 parameter 6 (string).
        /// </summary>
        public string Active1Param6 { get; set; } = string.Empty;

        /// <summary>
        /// Active2 parameter 1.
        /// </summary>
        public int Active2Param1 { get; set; }

        /// <summary>
        /// Active2 parameter 2.
        /// </summary>
        public int Active2Param2 { get; set; }

        /// <summary>
        /// Active2 parameter 3.
        /// </summary>
        public int Active2Param3 { get; set; }

        /// <summary>
        /// Active2 parameter 4.
        /// </summary>
        public int Active2Param4 { get; set; }

        /// <summary>
        /// Active2 parameter 5.
        /// </summary>
        public int Active2Param5 { get; set; }

        /// <summary>
        /// Active2 parameter 6 (string).
        /// </summary>
        public string Active2Param6 { get; set; } = string.Empty;

        // Other
        /// <summary>
        /// Whether this link is a child link.
        /// </summary>
        public bool IsChild { get; set; }

        /// <summary>
        /// Comment/note for this link.
        /// </summary>
        public string Comment { get; set; } = string.Empty;

        public CNVLink(CNVNode node, int listIndex = -1)
        {
            _hashCache = Guid.NewGuid().GetHashCode();
            Node = node;
            ListIndex = listIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is CNVLink other && Equals(other);
        }

        public bool Equals(CNVLink other)
        {
            if (other == null) return false;
            return _hashCache == other._hashCache;
        }

        public override int GetHashCode()
        {
            return _hashCache;
        }

        /// <summary>
        /// Gets the partial path string for this link.
        /// </summary>
        public string PartialPath(bool isStarter)
        {
            string p1 = isStarter ? "StartingList" : (Node is CNVEntry ? "EntriesList" : "RepliesList");
            return $"{p1}\\{ListIndex}";
        }
    }
}

