using System;
using KOTORModSync;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics.DLG
{
    /// <summary>
    /// Represents a stunt model in a dialog.
    /// </summary>
    [PublicAPI]
    public sealed class DLGStunt : IEquatable<DLGStunt>
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/stunts.py:12
        // Original: class DLGStunt:
        private readonly int _hashCache;

        public string Participant { get; set; } = string.Empty;
        public ResRef StuntModel { get; set; } = ResRef.FromBlank();

        public DLGStunt()
        {
            _hashCache = Guid.NewGuid().GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is DLGStunt other && Equals(other);
        }

        public bool Equals(DLGStunt other)
        {
            if (other == null) return false;
            return _hashCache == other._hashCache;
        }

        public override int GetHashCode()
        {
            return _hashCache;
        }
    }
}
