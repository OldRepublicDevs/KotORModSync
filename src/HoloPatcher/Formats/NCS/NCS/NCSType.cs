using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace KOTORModSync.Formats.NCS
{
    /// <summary>
    /// Represents a type in the NCS type system.
    /// Provides type information, size calculations, and type conversion utilities.
    /// Based on NCSDecomp Type.java implementation.
    /// </summary>
    public class NCSType : IEquatable<NCSType>
    {
        private readonly NCSTypeCode _typeCode;
        private readonly int _size;

        /// <summary>
        /// Initialize an NCS type.
        ///
        /// Args:
        ///     typeCode: Type code as integer, NCSTypeCode enum, or string name
        /// </summary>
        public NCSType(int typeCode)
        {
            _typeCode = (NCSTypeCode)typeCode;
            _size = CalculateSize();
        }

        public NCSType(NCSTypeCode typeCode)
        {
            _typeCode = typeCode;
            _size = CalculateSize();
        }

        public NCSType(string typeStr)
        {
            _typeCode = DecodeString(typeStr);
            _size = CalculateSize();
        }

        private static NCSTypeCode DecodeString(string typeStr)
        {
            var typeMap = new Dictionary<string, NCSTypeCode>
            {
                { "void", NCSTypeCode.NONE },
                { "int", NCSTypeCode.INTEGER },
                { "float", NCSTypeCode.FLOAT },
                { "string", NCSTypeCode.STRING },
                { "object", NCSTypeCode.OBJECT },
                { "effect", NCSTypeCode.EFFECT },
                { "event", NCSTypeCode.EVENT },
                { "location", NCSTypeCode.LOCATION },
                { "talent", NCSTypeCode.TALENT },
                { "vector", NCSTypeCode.VECTOR },
                { "action", NCSTypeCode.NONE }, // Actions have void type
                { "struct", NCSTypeCode.STRUCT },
                { "stack", NCSTypeCode.STACK },
                // Aliases
                { "INT", NCSTypeCode.INTEGER },
                { "OBJECT_ID", NCSTypeCode.OBJECT }
            };

            if (!typeMap.TryGetValue(typeStr, out NCSTypeCode result))
            {
                throw new ArgumentException($"Unknown type string: {typeStr}", nameof(typeStr));
            }

            return result;
        }

        private int CalculateSize()
        {
            var sizeMap = new Dictionary<NCSTypeCode, int>
            {
                { NCSTypeCode.NONE, 0 },
                { NCSTypeCode.STACK, 1 },
                { NCSTypeCode.INTEGER, 1 },
                { NCSTypeCode.FLOAT, 1 },
                { NCSTypeCode.STRING, 1 },
                { NCSTypeCode.OBJECT, 1 },
                { NCSTypeCode.EFFECT, 1 },
                { NCSTypeCode.EVENT, 1 },
                { NCSTypeCode.LOCATION, 1 },
                { NCSTypeCode.TALENT, 1 },
                { NCSTypeCode.VECTOR, 3 } // 12 bytes = 3 words
            };

            if (sizeMap.TryGetValue(_typeCode, out int size))
            {
                return size;
            }

            // Struct size must be determined externally
            if (_typeCode == NCSTypeCode.STRUCT)
            {
                throw new InvalidOperationException("Struct size must be determined by context");
            }

            throw new InvalidOperationException($"Cannot determine size of type code: {_typeCode}");
        }

        /// <summary>
        /// Get the type code.
        /// </summary>
        public NCSTypeCode TypeCode => _typeCode;

        /// <summary>
        /// Get the size in 4-byte words.
        /// </summary>
        public int Size => _size;

        /// <summary>
        /// Get the size in bytes.
        /// </summary>
        public int ByteSize => _size * 4;

        /// <summary>
        /// Check if this is a valid typed value (not invalid/stack).
        /// </summary>
        public bool IsTyped()
        {
            return _typeCode != NCSTypeCode.INVALID && _typeCode != NCSTypeCode.STACK;
        }

        /// <summary>
        /// Check if this is a numeric type (int or float).
        /// </summary>
        public bool IsNumeric()
        {
            return _typeCode == NCSTypeCode.INTEGER || _typeCode == NCSTypeCode.FLOAT;
        }

        /// <summary>
        /// Check if this is an engine-specific type.
        /// </summary>
        public bool IsEngineType()
        {
            return _typeCode == NCSTypeCode.EFFECT ||
                   _typeCode == NCSTypeCode.EVENT ||
                   _typeCode == NCSTypeCode.LOCATION ||
                   _typeCode == NCSTypeCode.TALENT;
        }

        /// <summary>
        /// Convert type to its string representation.
        /// </summary>
        public string ToStringRepresentation()
        {
            var typeNames = new Dictionary<NCSTypeCode, string>
            {
                { NCSTypeCode.NONE, "void" },
                { NCSTypeCode.STACK, "stack" },
                { NCSTypeCode.INTEGER, "int" },
                { NCSTypeCode.FLOAT, "float" },
                { NCSTypeCode.STRING, "string" },
                { NCSTypeCode.OBJECT, "object" },
                { NCSTypeCode.EFFECT, "effect" },
                { NCSTypeCode.EVENT, "event" },
                { NCSTypeCode.LOCATION, "location" },
                { NCSTypeCode.TALENT, "talent" },
                { NCSTypeCode.VECTOR, "vector" },
                { NCSTypeCode.STRUCT, "struct" },
                { NCSTypeCode.INVALID, "invalid" },
                // Compound types
                { NCSTypeCode.INTINT, "intint" },
                { NCSTypeCode.FLOATFLOAT, "floatfloat" },
                { NCSTypeCode.OBJECTOBJECT, "objectobject" },
                { NCSTypeCode.STRINGSTRING, "stringstring" },
                { NCSTypeCode.STRUCTSTRUCT, "structstruct" },
                { NCSTypeCode.INTFLOAT, "intfloat" },
                { NCSTypeCode.FLOATINT, "floatint" },
                { NCSTypeCode.EFFECTEFFECT, "effecteffect" },
                { NCSTypeCode.EVENTEVENT, "eventevent" },
                { NCSTypeCode.LOCLOC, "locloc" },
                { NCSTypeCode.TALTAL, "taltal" },
                { NCSTypeCode.VECTORVECTOR, "vectorvector" },
                { NCSTypeCode.VECTORFLOAT, "vectorfloat" },
                { NCSTypeCode.FLOATVECTOR, "floatvector" }
            };

            return typeNames.TryGetValue(_typeCode, out string name) ? name : "unknown";
        }

        public NCSType GetElement(int position)
        {
            if (position != 1)
            {
                throw new ArgumentException($"Position {position} > 1 for non-struct type", nameof(position));
            }
            return this;
        }

        public NCSTypeCode GetComponentType()
        {
            if (_typeCode == NCSTypeCode.INTINT || _typeCode == NCSTypeCode.INTFLOAT)
            {
                return NCSTypeCode.INTEGER;
            }
            if (_typeCode == NCSTypeCode.FLOATFLOAT || _typeCode == NCSTypeCode.FLOATINT || _typeCode == NCSTypeCode.FLOATVECTOR)
            {
                return NCSTypeCode.FLOAT;
            }
            if (_typeCode == NCSTypeCode.STRINGSTRING)
            {
                return NCSTypeCode.STRING;
            }
            if (_typeCode == NCSTypeCode.OBJECTOBJECT)
            {
                return NCSTypeCode.OBJECT;
            }
            if (_typeCode == NCSTypeCode.VECTOR)
            {
                return NCSTypeCode.FLOAT; // Vector components are floats
            }
            return _typeCode;
        }

        public NCSTypeCode GetLeftType()
        {
            if (_typeCode == NCSTypeCode.INTINT || _typeCode == NCSTypeCode.INTFLOAT)
            {
                return NCSTypeCode.INTEGER;
            }
            if (_typeCode == NCSTypeCode.FLOATFLOAT || _typeCode == NCSTypeCode.FLOATINT || _typeCode == NCSTypeCode.FLOATVECTOR)
            {
                return NCSTypeCode.FLOAT;
            }
            if (_typeCode == NCSTypeCode.STRINGSTRING)
            {
                return NCSTypeCode.STRING;
            }
            if (_typeCode == NCSTypeCode.OBJECTOBJECT)
            {
                return NCSTypeCode.OBJECT;
            }
            if (_typeCode == NCSTypeCode.EFFECTEFFECT)
            {
                return NCSTypeCode.EFFECT;
            }
            if (_typeCode == NCSTypeCode.EVENTEVENT)
            {
                return NCSTypeCode.EVENT;
            }
            if (_typeCode == NCSTypeCode.LOCLOC)
            {
                return NCSTypeCode.LOCATION;
            }
            if (_typeCode == NCSTypeCode.TALTAL)
            {
                return NCSTypeCode.TALENT;
            }
            if (_typeCode == NCSTypeCode.VECTORVECTOR || _typeCode == NCSTypeCode.VECTORFLOAT)
            {
                return NCSTypeCode.VECTOR;
            }
            if (_typeCode == NCSTypeCode.STRUCTSTRUCT)
            {
                return NCSTypeCode.STRUCT;
            }
            return _typeCode;
        }

        public NCSTypeCode GetRightType()
        {
            if (_typeCode == NCSTypeCode.INTINT || _typeCode == NCSTypeCode.FLOATINT)
            {
                return NCSTypeCode.INTEGER;
            }
            if (_typeCode == NCSTypeCode.FLOATFLOAT || _typeCode == NCSTypeCode.INTFLOAT || _typeCode == NCSTypeCode.VECTORFLOAT)
            {
                return NCSTypeCode.FLOAT;
            }
            if (_typeCode == NCSTypeCode.STRINGSTRING)
            {
                return NCSTypeCode.STRING;
            }
            if (_typeCode == NCSTypeCode.OBJECTOBJECT)
            {
                return NCSTypeCode.OBJECT;
            }
            if (_typeCode == NCSTypeCode.EFFECTEFFECT)
            {
                return NCSTypeCode.EFFECT;
            }
            if (_typeCode == NCSTypeCode.EVENTEVENT)
            {
                return NCSTypeCode.EVENT;
            }
            if (_typeCode == NCSTypeCode.LOCLOC)
            {
                return NCSTypeCode.LOCATION;
            }
            if (_typeCode == NCSTypeCode.TALTAL)
            {
                return NCSTypeCode.TALENT;
            }
            if (_typeCode == NCSTypeCode.VECTORVECTOR || _typeCode == NCSTypeCode.FLOATVECTOR)
            {
                return NCSTypeCode.VECTOR;
            }
            if (_typeCode == NCSTypeCode.STRUCTSTRUCT)
            {
                return NCSTypeCode.STRUCT;
            }
            return _typeCode;
        }

        /// <summary>
        /// Check if this is a compound type (e.g., INTINT, FLOATFLOAT).
        /// </summary>
        public bool IsCompound()
        {
            return _typeCode == NCSTypeCode.INTINT ||
                   _typeCode == NCSTypeCode.FLOATFLOAT ||
                   _typeCode == NCSTypeCode.OBJECTOBJECT ||
                   _typeCode == NCSTypeCode.STRINGSTRING ||
                   _typeCode == NCSTypeCode.STRUCTSTRUCT ||
                   _typeCode == NCSTypeCode.INTFLOAT ||
                   _typeCode == NCSTypeCode.FLOATINT ||
                   _typeCode == NCSTypeCode.EFFECTEFFECT ||
                   _typeCode == NCSTypeCode.EVENTEVENT ||
                   _typeCode == NCSTypeCode.LOCLOC ||
                   _typeCode == NCSTypeCode.TALTAL ||
                   _typeCode == NCSTypeCode.VECTORVECTOR ||
                   _typeCode == NCSTypeCode.VECTORFLOAT ||
                   _typeCode == NCSTypeCode.FLOATVECTOR;
        }

        /// <summary>
        /// Check if this is a vector type.
        /// </summary>
        public bool IsVector()
        {
            return _typeCode == NCSTypeCode.VECTOR;
        }

        /// <summary>
        /// Check if this is a struct type.
        /// </summary>
        public bool IsStruct()
        {
            return _typeCode == NCSTypeCode.STRUCT;
        }

        /// <summary>
        /// Check if this type can be converted to target type.
        /// </summary>
        public bool CanConvertTo(NCSTypeCode targetType)
        {
            // Numeric types can convert between each other
            if (IsNumeric() && (targetType == NCSTypeCode.INTEGER || targetType == NCSTypeCode.FLOAT))
            {
                return true;
            }
            // Same type always works
            if (_typeCode == targetType)
            {
                return true;
            }
            // Vector to float (component access)
            return _typeCode == NCSTypeCode.VECTOR && targetType == NCSTypeCode.FLOAT;
        }

        /// <summary>
        /// Create a compound type code from two operand types.
        /// </summary>
        public static NCSTypeCode CreateCompound(NCSTypeCode leftType, NCSTypeCode rightType)
        {
            if (leftType == NCSTypeCode.INTEGER && rightType == NCSTypeCode.INTEGER)
            {
                return NCSTypeCode.INTINT;
            }
            if (leftType == NCSTypeCode.FLOAT && rightType == NCSTypeCode.FLOAT)
            {
                return NCSTypeCode.FLOATFLOAT;
            }
            if (leftType == NCSTypeCode.INTEGER && rightType == NCSTypeCode.FLOAT)
            {
                return NCSTypeCode.INTFLOAT;
            }
            if (leftType == NCSTypeCode.FLOAT && rightType == NCSTypeCode.INTEGER)
            {
                return NCSTypeCode.FLOATINT;
            }
            if (leftType == NCSTypeCode.STRING && rightType == NCSTypeCode.STRING)
            {
                return NCSTypeCode.STRINGSTRING;
            }
            if (leftType == NCSTypeCode.OBJECT && rightType == NCSTypeCode.OBJECT)
            {
                return NCSTypeCode.OBJECTOBJECT;
            }
            if (leftType == NCSTypeCode.EFFECT && rightType == NCSTypeCode.EFFECT)
            {
                return NCSTypeCode.EFFECTEFFECT;
            }
            if (leftType == NCSTypeCode.EVENT && rightType == NCSTypeCode.EVENT)
            {
                return NCSTypeCode.EVENTEVENT;
            }
            if (leftType == NCSTypeCode.LOCATION && rightType == NCSTypeCode.LOCATION)
            {
                return NCSTypeCode.LOCLOC;
            }
            if (leftType == NCSTypeCode.TALENT && rightType == NCSTypeCode.TALENT)
            {
                return NCSTypeCode.TALTAL;
            }
            if (leftType == NCSTypeCode.VECTOR && rightType == NCSTypeCode.VECTOR)
            {
                return NCSTypeCode.VECTORVECTOR;
            }
            if (leftType == NCSTypeCode.VECTOR && rightType == NCSTypeCode.FLOAT)
            {
                return NCSTypeCode.VECTORFLOAT;
            }
            if (leftType == NCSTypeCode.FLOAT && rightType == NCSTypeCode.VECTOR)
            {
                return NCSTypeCode.FLOATVECTOR;
            }
            if (leftType == NCSTypeCode.STRUCT && rightType == NCSTypeCode.STRUCT)
            {
                return NCSTypeCode.STRUCTSTRUCT;
            }
            return NCSTypeCode.INVALID;
        }

        public override string ToString()
        {
            return ToStringRepresentation();
        }

        public bool Equals([CanBeNull] NCSType other)
        {
            if (other is null)
            {
                return false;
            }
            return _typeCode == other._typeCode;
        }

        public override bool Equals([CanBeNull] object obj)
        {
            return obj is NCSType other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (int)_typeCode;
        }

        public static bool operator ==(NCSType left, NCSType right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(NCSType left, NCSType right)
        {
            return !Equals(left, right);
        }
    }

    /// <summary>
    /// Create an NCSType from various input types.
    /// </summary>
    public static class NCSTypeHelper
    {
        public static NCSType CreateType(int typeInput)
        {
            return new NCSType(typeInput);
        }

        public static NCSType CreateType(NCSTypeCode typeInput)
        {
            return new NCSType(typeInput);
        }

        public static NCSType CreateType(string typeInput)
        {
            return new NCSType(typeInput);
        }

        public static NCSType CreateType(NCSType typeInput)
        {
            return typeInput;
        }
    }
}

