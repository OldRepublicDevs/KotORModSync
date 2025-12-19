namespace KOTORModSync.Formats.NCS.Compiler.NSS
{

    /// <summary>
    /// NSS lexer token types.
    /// 
    /// References:
    ///     vendor/HoloLSP/server/src/nwscript-lexer.ts (TypeScript NSS lexer)
    ///     vendor/KotOR.js/src/nwscript/NWScriptCompiler.ts (Token handling)
    ///     vendor/xoreos-tools/src/nwscript/ (NSS lexer implementation)
    /// </summary>
    public enum NssToken
    {
        STRING_VALUE,
        INT_VALUE,
        FLOAT_VALUE,
        IDENTIFIER,
        INT_TYPE,
        INT_HEX_VALUE,
        FLOAT_TYPE,
        OBJECT_TYPE,
        VOID_TYPE,
        EVENT_TYPE,
        EFFECT_TYPE,
        ITEMPROPERTY_TYPE,
        LOCATION_TYPE,
        STRING_TYPE,
        TALENT_TYPE,
        VECTOR_TYPE,
        ACTION_TYPE,
        BREAK_CONTROL,
        CASE_CONTROL,
        DEFAULT_CONTROL,
        DO_CONTROL,
        ELSE_CONTROL,
        SWITCH_CONTROL,
        WHILE_CONTROL,
        FOR_CONTROL,
        IF_CONTROL,
        TRUE_VALUE,
        FALSE_VALUE,
        OBJECTSELF_VALUE,
        OBJECTINVALID_VALUE,
        ADD,
        MINUS,
        MULTIPLY,
        DIVIDE,
        MOD,
        EQUALS,
        NOT_EQUALS,
        GREATER_THAN,
        LESS_THAN,
        LESS_THAN_OR_EQUALS,
        GREATER_THAN_OR_EQUALS,
        AND,
        OR,
        NOT,
        BITWISE_AND,
        BITWISE_OR,
        BITWISE_LEFT,
        BITWISE_RIGHT,
        BITWISE_XOR,
        BITWISE_NOT,
        INCLUDE,
        RETURN,
        ADDITION_ASSIGNMENT_OPERATOR,
        SUBTRACTION_ASSIGNMENT_OPERATOR,
        MULTIPLICATION_ASSIGNMENT_OPERATOR,
        DIVISION_ASSIGNMENT_OPERATOR,
        CONTINUE_CONTROL,
        STRUCT,
        INCREMENT,
        DECREMENT,
        NOP,

        LEFT_BRACE,
        RIGHT_BRACE,
        LEFT_PAREN,
        RIGHT_PAREN,
        SEMICOLON,
        ASSIGN,
        COMMA,
        COLON,
        DOT,
        LEFT_BRACKET,
        RIGHT_BRACKET
    }
}

