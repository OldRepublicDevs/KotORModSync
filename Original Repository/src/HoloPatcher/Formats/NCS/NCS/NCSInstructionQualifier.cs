namespace KOTORModSync.Formats.NCS
{

    /// <summary>
    /// Type qualifiers for NCS instructions.
    /// 
    /// These qualifiers specify the operand types for bytecode instructions,
    /// allowing the same opcode to operate on different data types.
    /// </summary>
    public enum NCSInstructionQualifier : byte
    {
        Int = 0x03,
        Float = 0x04,
        String = 0x05,
        Object = 0x06,
        Effect = 0x10,
        Event = 0x11,
        Location = 0x12,
        Talent = 0x13,
        IntInt = 0x20,
        FloatFloat = 0x21,
        ObjectObject = 0x22,
        StringString = 0x23,
        StructStruct = 0x24,
        IntFloat = 0x25,
        FloatInt = 0x26,
        EffectEffect = 0x30,
        EventEvent = 0x31,
        LocationLocation = 0x32,
        TalentTalent = 0x33,
        VectorVector = 0x3A,
        VectorFloat = 0x3B,
        FloatVector = 0x3C
    }
}

