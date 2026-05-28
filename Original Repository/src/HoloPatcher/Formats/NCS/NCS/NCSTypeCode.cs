using System;

namespace KOTORModSync.Formats.NCS
{
    /// <summary>
    /// Type codes used in NCS bytecode operations.
    /// </summary>
    public enum NCSTypeCode : byte
    {
        // Basic types
        NONE = 0x00,
        STACK = 0x01,
        INTEGER = 0x03,
        FLOAT = 0x04,
        STRING = 0x05,
        OBJECT = 0x06,

        // Engine types
        EFFECT = 0x10,
        EVENT = 0x11,
        LOCATION = 0x12,
        TALENT = 0x13,

        // Compound types
        INTINT = 0x20,
        FLOATFLOAT = 0x21,
        OBJECTOBJECT = 0x22,
        STRINGSTRING = 0x23,
        STRUCTSTRUCT = 0x24,
        INTFLOAT = 0x25,
        FLOATINT = 0x26,

        // Compound engine types
        EFFECTEFFECT = 0x30,
        EVENTEVENT = 0x31,
        LOCLOC = 0x32,
        TALTAL = 0x33,

        // Vector types
        VECTORVECTOR = 0x3A,
        VECTORFLOAT = 0x3B,
        FLOATVECTOR = 0x3C,

        // Special types
        VECTOR = 0xF0, // -16 in signed byte
        STRUCT = 0xF1, // -15 in signed byte
        INVALID = 0xFF // -1 in signed byte
    }
}

