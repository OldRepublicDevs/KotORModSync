using System;
using KOTORModSync;
using JetBrains.Annotations;

namespace KOTORModSync.Resource.Generics.CNV
{
    /// <summary>
    /// Represents a unit of animation executed during a conversation node.
    /// </summary>
    /// <remarks>
    /// CNV Animation:
    /// - Eclipse Engine conversation format (Dragon Age, )
    /// - Similar to DLG animation but for Eclipse conversation system
    /// - Used for character animations during conversation lines
    /// </remarks>
    [PublicAPI]
    public sealed class CNVAnimation : IEquatable<CNVAnimation>
    {
        // Matching pattern from DLGAnimation
        // Original: Animation data for Eclipse conversation nodes
        private readonly int _hashCache;

        /// <summary>
        /// Animation ID for the conversation node.
        /// </summary>
        public int AnimationId { get; set; } = 6;

        /// <summary>
        /// Participant name (character tag) for the animation.
        /// </summary>
        public string Participant { get; set; } = string.Empty;

        public CNVAnimation()
        {
            _hashCache = Guid.NewGuid().GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is CNVAnimation other && Equals(other);
        }

        public bool Equals(CNVAnimation other)
        {
            if (other == null) return false;
            return _hashCache == other._hashCache;
        }

        public override int GetHashCode()
        {
            return _hashCache;
        }

        public override string ToString()
        {
            return $"{GetType().Name}(animation_id={AnimationId}, participant={Participant})";
        }
    }
}

