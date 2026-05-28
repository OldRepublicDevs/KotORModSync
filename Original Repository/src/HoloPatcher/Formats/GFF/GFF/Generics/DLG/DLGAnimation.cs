using System;
using KOTORModSync;
using JetBrains.Annotations;

namespace KOTORModSync.Resource.Generics.DLG
{
    /// <summary>
    /// Represents a unit of animation executed during a node.
    /// </summary>
    [PublicAPI]
    public sealed class DLGAnimation : IEquatable<DLGAnimation>
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/anims.py:10
        // Original: class DLGAnimation:
        private readonly int _hashCache;

        public int AnimationId { get; set; } = 6;
        public string Participant { get; set; } = string.Empty;

        public DLGAnimation()
        {
            _hashCache = Guid.NewGuid().GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is DLGAnimation other && Equals(other);
        }

        public bool Equals(DLGAnimation other)
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

