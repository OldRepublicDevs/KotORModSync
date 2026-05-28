using System;

namespace KOTORModSync.Formats.NCS
{

    /// <summary>
    /// Complete NCS instruction types combining bytecode and qualifier.
    /// 
    /// Each instruction type represents a specific operation with typed operands.
    /// The tuple contains (ByteCode, Qualifier) for each instruction variant.
    /// 
    /// References:
    ///     vendor/xoreos/src/aurora/nwscript/ncsfile.h:86-280 - NCSFile instruction types
    ///     vendor/Kotor.NET/Kotor.NET/Formats/KotorNCS/NCS.cs:725-798 - Instruction definitions
    ///     https://github.com/xoreos/xoreos-docs - Torlack's NCS specification
    /// </summary>
    public enum NCSInstructionType
    {
        /// <summary>Unknown/reserved instruction</summary>
        RESERVED,
        /// <summary>Variant with qualifier 0x01</summary>
        RESERVED_01,

        NOP,
        CPDOWNSP,
        RSADDI,
        RSADDF,
        RSADDS,
        RSADDO,
        RSADDEFF,
        RSADDEVT,
        RSADDLOC,
        RSADDTAL,
        CPTOPSP,
        CONSTI,
        CONSTF,
        CONSTS,
        CONSTO,
        ACTION,
        LOGANDII,
        LOGORII,
        INCORII,
        EXCORII,
        BOOLANDII,
        EQUALII,
        EQUALFF,
        EQUALSS,
        EQUALOO,
        EQUALTT,
        EQUALEFFEFF,
        EQUALEVTEVT,
        EQUALLOCLOC,
        EQUALTALTAL,
        NEQUALII,
        NEQUALFF,
        NEQUALSS,
        NEQUALOO,
        NEQUALTT,
        NEQUALEFFEFF,
        NEQUALEVTEVT,
        NEQUALLOCLOC,
        NEQUALTALTAL,
        GEQII,
        GEQFF,
        GTII,
        GTFF,
        LTII,
        LTFF,
        LEQII,
        LEQFF,
        SHLEFTII,
        SHRIGHTII,
        USHRIGHTII,
        ADDII,
        ADDIF,
        ADDFI,
        ADDFF,
        ADDSS,
        ADDVV,
        SUBII,
        SUBIF,
        SUBFI,
        SUBFF,
        SUBVV,
        MULII,
        MULIF,
        MULFI,
        MULFF,
        MULVF,
        MULFV,
        DIVII,
        DIVIF,
        DIVFI,
        DIVFF,
        DIVVF,
        DIVFV,
        MODII,
        NEGI,
        NEGF,
        COMPI,
        MOVSP,
        JMP,
        JSR,
        JZ,
        RETN,
        DESTRUCT,
        NOTI,
        DECxSP,
        INCxSP,
        JNZ,
        CPDOWNBP,
        CPTOPBP,
        DECxBP,
        INCxBP,
        SAVEBP,
        RESTOREBP,
        STORE_STATE,
        NOP2
    }

    /// <summary>
    /// Extension methods for NCSInstructionType.
    /// </summary>
    public static class NCSInstructionTypeExtensions
    {
        /// <summary>
        /// Get the bytecode and qualifier for this instruction type.
        /// </summary>
        public static (NCSByteCode ByteCode, byte Qualifier) GetValue(this NCSInstructionType type)
        {
            switch (type)
            {
                case NCSInstructionType.RESERVED: return (NCSByteCode.RESERVED, 0x00);
                case NCSInstructionType.RESERVED_01: return (NCSByteCode.RESERVED, 0x01);
                case NCSInstructionType.NOP: return (NCSByteCode.NOP, 0x00);
                case NCSInstructionType.CPDOWNSP: return (NCSByteCode.CPDOWNSP, 0x01);
                case NCSInstructionType.RSADDI: return (NCSByteCode.RSADDx, (byte)NCSInstructionQualifier.Int);
                case NCSInstructionType.RSADDF: return (NCSByteCode.RSADDx, (byte)NCSInstructionQualifier.Float);
                case NCSInstructionType.RSADDS: return (NCSByteCode.RSADDx, (byte)NCSInstructionQualifier.String);
                case NCSInstructionType.RSADDO: return (NCSByteCode.RSADDx, (byte)NCSInstructionQualifier.Object);
                case NCSInstructionType.RSADDEFF: return (NCSByteCode.RSADDx, (byte)NCSInstructionQualifier.Effect);
                case NCSInstructionType.RSADDEVT: return (NCSByteCode.RSADDx, (byte)NCSInstructionQualifier.Event);
                case NCSInstructionType.RSADDLOC: return (NCSByteCode.RSADDx, (byte)NCSInstructionQualifier.Location);
                case NCSInstructionType.RSADDTAL: return (NCSByteCode.RSADDx, (byte)NCSInstructionQualifier.Talent);
                case NCSInstructionType.CPTOPSP: return (NCSByteCode.CPTOPSP, 0x01);
                case NCSInstructionType.CONSTI: return (NCSByteCode.CONSTx, (byte)NCSInstructionQualifier.Int);
                case NCSInstructionType.CONSTF: return (NCSByteCode.CONSTx, (byte)NCSInstructionQualifier.Float);
                case NCSInstructionType.CONSTS: return (NCSByteCode.CONSTx, (byte)NCSInstructionQualifier.String);
                case NCSInstructionType.CONSTO: return (NCSByteCode.CONSTx, (byte)NCSInstructionQualifier.Object);
                case NCSInstructionType.ACTION: return (NCSByteCode.ACTION, 0x00);
                case NCSInstructionType.LOGANDII: return (NCSByteCode.LOGANDxx, (byte)NCSInstructionQualifier.IntInt);
                case NCSInstructionType.LOGORII: return (NCSByteCode.LOGORxx, (byte)NCSInstructionQualifier.IntInt);
                case NCSInstructionType.INCORII: return (NCSByteCode.INCORxx, (byte)NCSInstructionQualifier.IntInt);
                case NCSInstructionType.EXCORII: return (NCSByteCode.EXCORxx, (byte)NCSInstructionQualifier.IntInt);
                case NCSInstructionType.BOOLANDII: return (NCSByteCode.BOOLANDxx, (byte)NCSInstructionQualifier.IntInt);
                case NCSInstructionType.EQUALII: return (NCSByteCode.EQUALxx, (byte)NCSInstructionQualifier.IntInt);
                case NCSInstructionType.EQUALFF: return (NCSByteCode.EQUALxx, (byte)NCSInstructionQualifier.FloatFloat);
                case NCSInstructionType.EQUALSS: return (NCSByteCode.EQUALxx, (byte)NCSInstructionQualifier.StringString);
                case NCSInstructionType.EQUALOO: return (NCSByteCode.EQUALxx, (byte)NCSInstructionQualifier.ObjectObject);
                case NCSInstructionType.EQUALTT: return (NCSByteCode.EQUALxx, (byte)NCSInstructionQualifier.StructStruct);
                case NCSInstructionType.EQUALEFFEFF: return (NCSByteCode.EQUALxx, (byte)NCSInstructionQualifier.EffectEffect);
                case NCSInstructionType.EQUALEVTEVT: return (NCSByteCode.EQUALxx, (byte)NCSInstructionQualifier.EventEvent);
                case NCSInstructionType.EQUALLOCLOC: return (NCSByteCode.EQUALxx, (byte)NCSInstructionQualifier.LocationLocation);
                case NCSInstructionType.EQUALTALTAL: return (NCSByteCode.EQUALxx, (byte)NCSInstructionQualifier.TalentTalent);
                case NCSInstructionType.NEQUALII: return (NCSByteCode.NEQUALxx, (byte)NCSInstructionQualifier.IntInt);
                case NCSInstructionType.NEQUALFF: return (NCSByteCode.NEQUALxx, (byte)NCSInstructionQualifier.FloatFloat);
                case NCSInstructionType.NEQUALSS: return (NCSByteCode.NEQUALxx, (byte)NCSInstructionQualifier.StringString);
                case NCSInstructionType.NEQUALOO: return (NCSByteCode.NEQUALxx, (byte)NCSInstructionQualifier.ObjectObject);
                case NCSInstructionType.NEQUALTT: return (NCSByteCode.NEQUALxx, (byte)NCSInstructionQualifier.StructStruct);
                case NCSInstructionType.NEQUALEFFEFF: return (NCSByteCode.NEQUALxx, (byte)NCSInstructionQualifier.EffectEffect);
                case NCSInstructionType.NEQUALEVTEVT: return (NCSByteCode.NEQUALxx, (byte)NCSInstructionQualifier.EventEvent);
                case NCSInstructionType.NEQUALLOCLOC: return (NCSByteCode.NEQUALxx, (byte)NCSInstructionQualifier.LocationLocation);
                case NCSInstructionType.NEQUALTALTAL: return (NCSByteCode.NEQUALxx, (byte)NCSInstructionQualifier.TalentTalent);
                case NCSInstructionType.GEQII: return (NCSByteCode.GEQxx, (byte)NCSInstructionQualifier.IntInt);
                case NCSInstructionType.GEQFF: return (NCSByteCode.GEQxx, (byte)NCSInstructionQualifier.FloatFloat);
                case NCSInstructionType.GTII: return (NCSByteCode.GTxx, (byte)NCSInstructionQualifier.IntInt);
                case NCSInstructionType.GTFF: return (NCSByteCode.GTxx, (byte)NCSInstructionQualifier.FloatFloat);
                case NCSInstructionType.LTII: return (NCSByteCode.LTxx, (byte)NCSInstructionQualifier.IntInt);
                case NCSInstructionType.LTFF: return (NCSByteCode.LTxx, (byte)NCSInstructionQualifier.FloatFloat);
                case NCSInstructionType.LEQII: return (NCSByteCode.LEQxx, (byte)NCSInstructionQualifier.IntInt);
                case NCSInstructionType.LEQFF: return (NCSByteCode.LEQxx, (byte)NCSInstructionQualifier.FloatFloat);
                case NCSInstructionType.SHLEFTII: return (NCSByteCode.SHLEFTxx, (byte)NCSInstructionQualifier.IntInt);
                case NCSInstructionType.SHRIGHTII: return (NCSByteCode.SHRIGHTxx, (byte)NCSInstructionQualifier.IntInt);
                case NCSInstructionType.USHRIGHTII: return (NCSByteCode.USHRIGHTxx, (byte)NCSInstructionQualifier.IntInt);
                case NCSInstructionType.ADDII: return (NCSByteCode.ADDxx, (byte)NCSInstructionQualifier.IntInt);
                case NCSInstructionType.ADDIF: return (NCSByteCode.ADDxx, (byte)NCSInstructionQualifier.IntFloat);
                case NCSInstructionType.ADDFI: return (NCSByteCode.ADDxx, (byte)NCSInstructionQualifier.FloatInt);
                case NCSInstructionType.ADDFF: return (NCSByteCode.ADDxx, (byte)NCSInstructionQualifier.FloatFloat);
                case NCSInstructionType.ADDSS: return (NCSByteCode.ADDxx, (byte)NCSInstructionQualifier.StringString);
                case NCSInstructionType.ADDVV: return (NCSByteCode.ADDxx, (byte)NCSInstructionQualifier.VectorVector);
                case NCSInstructionType.SUBII: return (NCSByteCode.SUBxx, (byte)NCSInstructionQualifier.IntInt);
                case NCSInstructionType.SUBIF: return (NCSByteCode.SUBxx, (byte)NCSInstructionQualifier.IntFloat);
                case NCSInstructionType.SUBFI: return (NCSByteCode.SUBxx, (byte)NCSInstructionQualifier.FloatInt);
                case NCSInstructionType.SUBFF: return (NCSByteCode.SUBxx, (byte)NCSInstructionQualifier.FloatFloat);
                case NCSInstructionType.SUBVV: return (NCSByteCode.SUBxx, (byte)NCSInstructionQualifier.VectorVector);
                case NCSInstructionType.MULII: return (NCSByteCode.MULxx, (byte)NCSInstructionQualifier.IntInt);
                case NCSInstructionType.MULIF: return (NCSByteCode.MULxx, (byte)NCSInstructionQualifier.IntFloat);
                case NCSInstructionType.MULFI: return (NCSByteCode.MULxx, (byte)NCSInstructionQualifier.FloatInt);
                case NCSInstructionType.MULFF: return (NCSByteCode.MULxx, (byte)NCSInstructionQualifier.FloatFloat);
                case NCSInstructionType.MULVF: return (NCSByteCode.MULxx, (byte)NCSInstructionQualifier.VectorFloat);
                case NCSInstructionType.MULFV: return (NCSByteCode.MULxx, (byte)NCSInstructionQualifier.FloatVector);
                case NCSInstructionType.DIVII: return (NCSByteCode.DIVxx, (byte)NCSInstructionQualifier.IntInt);
                case NCSInstructionType.DIVIF: return (NCSByteCode.DIVxx, (byte)NCSInstructionQualifier.IntFloat);
                case NCSInstructionType.DIVFI: return (NCSByteCode.DIVxx, (byte)NCSInstructionQualifier.FloatInt);
                case NCSInstructionType.DIVFF: return (NCSByteCode.DIVxx, (byte)NCSInstructionQualifier.FloatFloat);
                case NCSInstructionType.DIVVF: return (NCSByteCode.DIVxx, (byte)NCSInstructionQualifier.VectorFloat);
                case NCSInstructionType.DIVFV: return (NCSByteCode.DIVxx, (byte)NCSInstructionQualifier.FloatVector);
                case NCSInstructionType.MODII: return (NCSByteCode.MODxx, (byte)NCSInstructionQualifier.IntInt);
                case NCSInstructionType.NEGI: return (NCSByteCode.NEGx, (byte)NCSInstructionQualifier.Int);
                case NCSInstructionType.NEGF: return (NCSByteCode.NEGx, (byte)NCSInstructionQualifier.Float);
                case NCSInstructionType.COMPI: return (NCSByteCode.COMPx, (byte)NCSInstructionQualifier.Int);
                case NCSInstructionType.MOVSP: return (NCSByteCode.MOVSP, 0x00);
                case NCSInstructionType.JMP: return (NCSByteCode.JMP, 0x00);
                case NCSInstructionType.JSR: return (NCSByteCode.JSR, 0x00);
                case NCSInstructionType.JZ: return (NCSByteCode.JZ, 0x00);
                case NCSInstructionType.RETN: return (NCSByteCode.RETN, 0x00);
                case NCSInstructionType.DESTRUCT: return (NCSByteCode.DESTRUCT, 0x01);
                case NCSInstructionType.NOTI: return (NCSByteCode.NOTx, (byte)NCSInstructionQualifier.Int);
                case NCSInstructionType.DECxSP: return (NCSByteCode.DECxSP, (byte)NCSInstructionQualifier.Int);
                case NCSInstructionType.INCxSP: return (NCSByteCode.INCxSP, (byte)NCSInstructionQualifier.Int);
                case NCSInstructionType.JNZ: return (NCSByteCode.JNZ, 0x00);
                case NCSInstructionType.CPDOWNBP: return (NCSByteCode.CPDOWNBP, 0x01);
                case NCSInstructionType.CPTOPBP: return (NCSByteCode.CPTOPBP, 0x01);
                case NCSInstructionType.DECxBP: return (NCSByteCode.DECxBP, (byte)NCSInstructionQualifier.Int);
                case NCSInstructionType.INCxBP: return (NCSByteCode.INCxBP, (byte)NCSInstructionQualifier.Int);
                case NCSInstructionType.SAVEBP: return (NCSByteCode.SAVEBP, 0x00);
                case NCSInstructionType.RESTOREBP: return (NCSByteCode.RESTOREBP, 0x00);
                case NCSInstructionType.STORE_STATE: return (NCSByteCode.STORE_STATE, 0x10);
                case NCSInstructionType.NOP2: return (NCSByteCode.NOP2, 0x00);
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        /// <summary>
        /// Create an NCSInstructionType from bytecode and qualifier.
        /// </summary>
        public static NCSInstructionType FromBytecode(NCSByteCode byteCode, byte qualifier)
        {
            switch (byteCode)
            {
                case NCSByteCode.RESERVED:
                    switch (qualifier)
                    {
                        case 0x00: return NCSInstructionType.RESERVED;
                        case 0x01: return NCSInstructionType.RESERVED_01;
                    }
                    break;
                case NCSByteCode.NOP:
                    if (qualifier == 0x00)
                    {
                        return NCSInstructionType.NOP;
                    }

                    break;
                case NCSByteCode.CPDOWNSP:
                    if (qualifier == 0x01)
                    {
                        return NCSInstructionType.CPDOWNSP;
                    }
                    break;
                case NCSByteCode.RSADDx:
                    switch (qualifier)
                    {
                        case (byte)NCSInstructionQualifier.Int: return NCSInstructionType.RSADDI;
                        case (byte)NCSInstructionQualifier.Float: return NCSInstructionType.RSADDF;
                        case (byte)NCSInstructionQualifier.String: return NCSInstructionType.RSADDS;
                        case (byte)NCSInstructionQualifier.Object: return NCSInstructionType.RSADDO;
                        case (byte)NCSInstructionQualifier.Effect: return NCSInstructionType.RSADDEFF;
                        case (byte)NCSInstructionQualifier.Event: return NCSInstructionType.RSADDEVT;
                        case (byte)NCSInstructionQualifier.Location: return NCSInstructionType.RSADDLOC;
                        case (byte)NCSInstructionQualifier.Talent: return NCSInstructionType.RSADDTAL;
                    }
                    break;
                case NCSByteCode.CPTOPSP:
                    if (qualifier == 0x01)
                    {
                        return NCSInstructionType.CPTOPSP;
                    }
                    break;
                case NCSByteCode.CONSTx:
                    switch (qualifier)
                    {
                        case (byte)NCSInstructionQualifier.Int: return NCSInstructionType.CONSTI;
                        case (byte)NCSInstructionQualifier.Float: return NCSInstructionType.CONSTF;
                        case (byte)NCSInstructionQualifier.String: return NCSInstructionType.CONSTS;
                        case (byte)NCSInstructionQualifier.Object: return NCSInstructionType.CONSTO;
                    }
                    break;
                case NCSByteCode.ACTION:
                    // ACTION instructions can have any qualifier value
                    // The qualifier is not used to distinguish ACTION types - all ACTION bytecodes map to ACTION type
                    return NCSInstructionType.ACTION;
                case NCSByteCode.LOGANDxx:
                    if (qualifier == (byte)NCSInstructionQualifier.IntInt)
                    {
                        return NCSInstructionType.LOGANDII;
                    }
                    break;
                case NCSByteCode.LOGORxx:
                    if (qualifier == (byte)NCSInstructionQualifier.IntInt)
                    {
                        return NCSInstructionType.LOGORII;
                    }
                    break;
                case NCSByteCode.INCORxx:
                    if (qualifier == (byte)NCSInstructionQualifier.IntInt)
                    {
                        return NCSInstructionType.INCORII;
                    }
                    break;
                case NCSByteCode.EXCORxx:
                    if (qualifier == (byte)NCSInstructionQualifier.IntInt)
                    {
                        return NCSInstructionType.EXCORII;
                    }
                    break;
                case NCSByteCode.BOOLANDxx:
                    if (qualifier == (byte)NCSInstructionQualifier.IntInt)
                    {
                        return NCSInstructionType.BOOLANDII;
                    }
                    break;
                case NCSByteCode.EQUALxx:
                    switch (qualifier)
                    {
                        case (byte)NCSInstructionQualifier.IntInt: return NCSInstructionType.EQUALII;
                        case (byte)NCSInstructionQualifier.FloatFloat: return NCSInstructionType.EQUALFF;
                        case (byte)NCSInstructionQualifier.StringString: return NCSInstructionType.EQUALSS;
                        case (byte)NCSInstructionQualifier.ObjectObject: return NCSInstructionType.EQUALOO;
                        case (byte)NCSInstructionQualifier.StructStruct: return NCSInstructionType.EQUALTT;
                        case (byte)NCSInstructionQualifier.EffectEffect: return NCSInstructionType.EQUALEFFEFF;
                        case (byte)NCSInstructionQualifier.EventEvent: return NCSInstructionType.EQUALEVTEVT;
                        case (byte)NCSInstructionQualifier.LocationLocation: return NCSInstructionType.EQUALLOCLOC;
                        case (byte)NCSInstructionQualifier.TalentTalent: return NCSInstructionType.EQUALTALTAL;
                    }
                    break;
                case NCSByteCode.NEQUALxx:
                    switch (qualifier)
                    {
                        case (byte)NCSInstructionQualifier.IntInt: return NCSInstructionType.NEQUALII;
                        case (byte)NCSInstructionQualifier.FloatFloat: return NCSInstructionType.NEQUALFF;
                        case (byte)NCSInstructionQualifier.StringString: return NCSInstructionType.NEQUALSS;
                        case (byte)NCSInstructionQualifier.ObjectObject: return NCSInstructionType.NEQUALOO;
                        case (byte)NCSInstructionQualifier.StructStruct: return NCSInstructionType.NEQUALTT;
                        case (byte)NCSInstructionQualifier.EffectEffect: return NCSInstructionType.NEQUALEFFEFF;
                        case (byte)NCSInstructionQualifier.EventEvent: return NCSInstructionType.NEQUALEVTEVT;
                        case (byte)NCSInstructionQualifier.LocationLocation: return NCSInstructionType.NEQUALLOCLOC;
                        case (byte)NCSInstructionQualifier.TalentTalent: return NCSInstructionType.NEQUALTALTAL;
                    }
                    break;
                case NCSByteCode.GEQxx:
                    switch (qualifier)
                    {
                        case (byte)NCSInstructionQualifier.IntInt: return NCSInstructionType.GEQII;
                        case (byte)NCSInstructionQualifier.FloatFloat: return NCSInstructionType.GEQFF;
                    }
                    break;
                case NCSByteCode.GTxx:
                    switch (qualifier)
                    {
                        case (byte)NCSInstructionQualifier.IntInt: return NCSInstructionType.GTII;
                        case (byte)NCSInstructionQualifier.FloatFloat: return NCSInstructionType.GTFF;
                    }
                    break;
                case NCSByteCode.LTxx:
                    switch (qualifier)
                    {
                        case (byte)NCSInstructionQualifier.IntInt: return NCSInstructionType.LTII;
                        case (byte)NCSInstructionQualifier.FloatFloat: return NCSInstructionType.LTFF;
                    }
                    break;
                case NCSByteCode.LEQxx:
                    switch (qualifier)
                    {
                        case (byte)NCSInstructionQualifier.IntInt: return NCSInstructionType.LEQII;
                        case (byte)NCSInstructionQualifier.FloatFloat: return NCSInstructionType.LEQFF;
                    }
                    break;
                case NCSByteCode.SHLEFTxx:
                    if (qualifier == (byte)NCSInstructionQualifier.IntInt)
                    {
                        return NCSInstructionType.SHLEFTII;
                    }
                    break;
                case NCSByteCode.SHRIGHTxx:
                    if (qualifier == (byte)NCSInstructionQualifier.IntInt)
                    {
                        return NCSInstructionType.SHRIGHTII;
                    }
                    break;
                case NCSByteCode.USHRIGHTxx:
                    if (qualifier == (byte)NCSInstructionQualifier.IntInt)
                    {
                        return NCSInstructionType.USHRIGHTII;
                    }
                    break;
                case NCSByteCode.ADDxx:
                    switch (qualifier)
                    {
                        case (byte)NCSInstructionQualifier.IntInt: return NCSInstructionType.ADDII;
                        case (byte)NCSInstructionQualifier.IntFloat: return NCSInstructionType.ADDIF;
                        case (byte)NCSInstructionQualifier.FloatInt: return NCSInstructionType.ADDFI;
                        case (byte)NCSInstructionQualifier.FloatFloat: return NCSInstructionType.ADDFF;
                        case (byte)NCSInstructionQualifier.StringString: return NCSInstructionType.ADDSS;
                        case (byte)NCSInstructionQualifier.VectorVector: return NCSInstructionType.ADDVV;
                    }
                    break;
                case NCSByteCode.SUBxx:
                    switch (qualifier)
                    {
                        case (byte)NCSInstructionQualifier.IntInt: return NCSInstructionType.SUBII;
                        case (byte)NCSInstructionQualifier.IntFloat: return NCSInstructionType.SUBIF;
                        case (byte)NCSInstructionQualifier.FloatInt: return NCSInstructionType.SUBFI;
                        case (byte)NCSInstructionQualifier.FloatFloat: return NCSInstructionType.SUBFF;
                        case (byte)NCSInstructionQualifier.VectorVector: return NCSInstructionType.SUBVV;
                    }
                    break;
                case NCSByteCode.MULxx:
                    switch (qualifier)
                    {
                        case (byte)NCSInstructionQualifier.IntInt: return NCSInstructionType.MULII;
                        case (byte)NCSInstructionQualifier.IntFloat: return NCSInstructionType.MULIF;
                        case (byte)NCSInstructionQualifier.FloatInt: return NCSInstructionType.MULFI;
                        case (byte)NCSInstructionQualifier.FloatFloat: return NCSInstructionType.MULFF;
                        case (byte)NCSInstructionQualifier.VectorFloat: return NCSInstructionType.MULVF;
                        case (byte)NCSInstructionQualifier.FloatVector: return NCSInstructionType.MULFV;
                    }
                    break;
                case NCSByteCode.DIVxx:
                    switch (qualifier)
                    {
                        case (byte)NCSInstructionQualifier.IntInt: return NCSInstructionType.DIVII;
                        case (byte)NCSInstructionQualifier.IntFloat: return NCSInstructionType.DIVIF;
                        case (byte)NCSInstructionQualifier.FloatInt: return NCSInstructionType.DIVFI;
                        case (byte)NCSInstructionQualifier.FloatFloat: return NCSInstructionType.DIVFF;
                        case (byte)NCSInstructionQualifier.VectorFloat: return NCSInstructionType.DIVVF;
                        case (byte)NCSInstructionQualifier.FloatVector: return NCSInstructionType.DIVFV;
                    }
                    break;
                case NCSByteCode.MODxx:
                    if (qualifier == (byte)NCSInstructionQualifier.IntInt)
                    {
                        return NCSInstructionType.MODII;
                    }
                    break;
                case NCSByteCode.NEGx:
                    switch (qualifier)
                    {
                        case (byte)NCSInstructionQualifier.Int: return NCSInstructionType.NEGI;
                        case (byte)NCSInstructionQualifier.Float: return NCSInstructionType.NEGF;
                    }
                    break;
                case NCSByteCode.COMPx:
                    if (qualifier == (byte)NCSInstructionQualifier.Int)
                    {
                        return NCSInstructionType.COMPI;
                    }
                    break;
                case NCSByteCode.MOVSP:
                    if (qualifier == 0x00)
                    {
                        return NCSInstructionType.MOVSP;
                    }
                    break;
                case NCSByteCode.JMP:
                    if (qualifier == 0x00)
                    {
                        return NCSInstructionType.JMP;
                    }
                    break;
                case NCSByteCode.JSR:
                    if (qualifier == 0x00)
                    {
                        return NCSInstructionType.JSR;
                    }
                    break;
                case NCSByteCode.JZ:
                    if (qualifier == 0x00)
                    {
                        return NCSInstructionType.JZ;
                    }
                    break;
                case NCSByteCode.RETN:
                    if (qualifier == 0x00)
                    {
                        return NCSInstructionType.RETN;
                    }
                    break;
                case NCSByteCode.DESTRUCT:
                    if (qualifier == 0x01)
                    {
                        return NCSInstructionType.DESTRUCT;
                    }
                    break;
                case NCSByteCode.NOTx:
                    if (qualifier == (byte)NCSInstructionQualifier.Int)
                    {
                        return NCSInstructionType.NOTI;
                    }
                    break;
                case NCSByteCode.DECxSP:
                    if (qualifier == (byte)NCSInstructionQualifier.Int)
                    {
                        return NCSInstructionType.DECxSP;
                    }
                    break;
                case NCSByteCode.INCxSP:
                    if (qualifier == (byte)NCSInstructionQualifier.Int)
                    {
                        return NCSInstructionType.INCxSP;
                    }
                    break;
                case NCSByteCode.JNZ:
                    if (qualifier == 0x00)
                    {
                        return NCSInstructionType.JNZ;
                    }
                    break;
                case NCSByteCode.CPDOWNBP:
                    if (qualifier == 0x01)
                    {
                        return NCSInstructionType.CPDOWNBP;
                    }
                    break;
                case NCSByteCode.CPTOPBP:
                    if (qualifier == 0x01)
                    {
                        return NCSInstructionType.CPTOPBP;
                    }
                    break;
                case NCSByteCode.DECxBP:
                    if (qualifier == (byte)NCSInstructionQualifier.Int)
                    {
                        return NCSInstructionType.DECxBP;
                    }
                    break;
                case NCSByteCode.INCxBP:
                    if (qualifier == (byte)NCSInstructionQualifier.Int)
                    {
                        return NCSInstructionType.INCxBP;
                    }
                    break;
                case NCSByteCode.SAVEBP:
                    if (qualifier == 0x00)
                    {
                        return NCSInstructionType.SAVEBP;
                    }
                    break;
                case NCSByteCode.RESTOREBP:
                    if (qualifier == 0x00)
                    {
                        return NCSInstructionType.RESTOREBP;
                    }
                    break;
                case NCSByteCode.STORE_STATE:
                    if (qualifier == 0x10)
                    {
                        return NCSInstructionType.STORE_STATE;
                    }
                    break;
            }

            // Note: NOP2 has the same bytecode value (0x2D) as NOP, so it's handled by the NOP case above
            throw new ArgumentException($"Unknown NCS instruction: bytecode=0x{(byte)byteCode:X2}, qualifier=0x{qualifier:X2}");
        }
    }
}

