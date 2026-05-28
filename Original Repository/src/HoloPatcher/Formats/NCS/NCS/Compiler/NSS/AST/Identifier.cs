using System;
using JetBrains.Annotations;

namespace KOTORModSync.Formats.NCS.Compiler
{

    /// <summary>
    /// Represents an identifier in NSS source code.
    /// </summary>
    public class Identifier : IEquatable<Identifier>
    {
        public string Label { get; set; }

        public Identifier(string label)
        {
            Label = label ?? throw new ArgumentNullException(nameof(label));
        }

        public bool Equals([CanBeNull] Identifier other)
        {
            if (other is null)
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            return Label == other.Label;
        }

        public override bool Equals([CanBeNull] object obj)
        {
            if (obj is Identifier id)
            {
                return Equals(id);
            }
            if (obj is string str)
            {
                return Label == str;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Label.GetHashCode();
        }

        public override string ToString()
        {
            return Label;
        }

        public static implicit operator string(Identifier identifier) => identifier.Label;
    }
}

