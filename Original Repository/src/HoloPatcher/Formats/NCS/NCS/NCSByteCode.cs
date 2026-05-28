namespace KOTORModSync.Formats.NCS
{

    /// <summary>
    /// NCS bytecode opcodes.
    /// 
    /// References:
    ///     vendor/reone/include/reone/script/format/ncsreader.h:29-47 - NcsReader class
    ///     vendor/reone/src/libs/script/format/ncsreader.cpp:28-190 - Complete NCS reading implementation
    ///     vendor/xoreos/src/aurora/nwscript/ncsfile.h:86-280 - NCSFile class and instruction types
    ///     vendor/xoreos/src/aurora/nwscript/ncsfile.cpp:49-1649 - Complete NCS execution engine
    ///     vendor/Kotor.NET/Kotor.NET/Formats/KotorNCS/NCS.cs:9-799 - NCS instruction classes
    ///     https://github.com/xoreos/xoreos-docs - Torlack's NCS specification (mirrored)
    /// </summary>
    public enum NCSByteCode : byte
    {
        /// <summary>Reserved/unknown opcode found in some compiled scripts</summary>
        RESERVED = 0x00,

        CPDOWNSP = 0x01,
        RSADDx = 0x02,
        CPTOPSP = 0x03,
        CONSTx = 0x04,
        ACTION = 0x05,
        NOP = 0x2D,
        LOGANDxx = 0x06,
        LOGORxx = 0x07,
        INCORxx = 0x08,
        EXCORxx = 0x09,
        BOOLANDxx = 0x0A,
        EQUALxx = 0x0B,
        NEQUALxx = 0x0C,
        GEQxx = 0x0D,
        GTxx = 0x0E,
        LTxx = 0x0F,
        LEQxx = 0x10,
        SHLEFTxx = 0x11,
        SHRIGHTxx = 0x12,
        USHRIGHTxx = 0x13,
        ADDxx = 0x14,
        SUBxx = 0x15,
        MULxx = 0x16,
        DIVxx = 0x17,
        MODxx = 0x18,
        NEGx = 0x19,
        COMPx = 0x1A,
        MOVSP = 0x1B,
        JMP = 0x1D,
        JSR = 0x1E,
        JZ = 0x1F,
        RETN = 0x20,
        DESTRUCT = 0x21,
        NOTx = 0x22,
        DECxSP = 0x23,
        INCxSP = 0x24,
        JNZ = 0x25,
        CPDOWNBP = 0x26,
        CPTOPBP = 0x27,
        DECxBP = 0x28,
        INCxBP = 0x29,
        SAVEBP = 0x2A,
        RESTOREBP = 0x2B,
        STORE_STATE = 0x2C,
        NOP2 = 0x2D
    }
}

