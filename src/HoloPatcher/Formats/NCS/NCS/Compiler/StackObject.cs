using System;
using KOTORModSync.Common.Script;
using JetBrains.Annotations;

namespace KOTORModSync.Formats.NCS.Compiler
{

    /// <summary>
    /// Represents a single value on the NCS execution stack.
    /// </summary>
    public class StackObject
    {
        public DataType DataType { get; set; }
        public object Value { get; set; }

        public StackObject(DataType dataType, [CanBeNull] object value)
        {
            DataType = dataType;
            Value = value;
        }

        public override string ToString()
        {
            return $"{DataType}={Value}";
        }

        public override bool Equals([CanBeNull] object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj is StackObject other)
            {
                return Value != null ? Value.Equals(other.Value) : other.Value == null;
            }
            return Value != null ? Value.Equals(obj) : obj == null;
        }

        public override int GetHashCode()
        {
            // Avoid using HashCode.Combine with Value directly as it might cause type conversion issues
            // Calculate hash manually to avoid any float/double conversion problems
            int dataTypeHash = DataType.GetHashCode();
            int valueHash = 0;
            if (Value != null)
            {
                // Handle common types explicitly to avoid any implicit conversions
                if (Value is int i)
                {
                    valueHash = i.GetHashCode();
                }
                else if (Value is float f)
                {
                    valueHash = f.GetHashCode();
                }
                else if (Value is string s)
                {
                    valueHash = s.GetHashCode();
                }
                else
                {
                    valueHash = Value.GetHashCode();
                }
            }
            // Manual hash combination to avoid HashCode.Combine potential issues
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + dataTypeHash;
                hash = hash * 31 + valueHash;
                return hash;
            }
        }
    }
}

