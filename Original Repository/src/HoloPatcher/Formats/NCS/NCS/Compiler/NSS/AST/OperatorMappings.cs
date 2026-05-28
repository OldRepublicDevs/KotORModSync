using System.Collections.Generic;
using KOTORModSync.Common.Script;
using KOTORModSync.Formats.NCS;

namespace KOTORModSync.Formats.NCS.Compiler
{
    /// <summary>
    /// Provides operator mappings for binary and unary operators.
    /// This maps operator + operand types to NCS instructions.
    /// </summary>
    public static class OperatorMappings
    {
        public static OperatorMapping Addition = new OperatorMapping(
            new List<UnaryOperatorMapping>(),
            new List<BinaryOperatorMapping>
            {
                new BinaryOperatorMapping(NCSInstructionType.ADDII, DataType.Int, DataType.Int, DataType.Int),
                new BinaryOperatorMapping(NCSInstructionType.ADDIF, DataType.Int, DataType.Int, DataType.Float),
                new BinaryOperatorMapping(NCSInstructionType.ADDFI, DataType.Float, DataType.Float, DataType.Int),
                new BinaryOperatorMapping(NCSInstructionType.ADDFF, DataType.Float, DataType.Float, DataType.Float),
                new BinaryOperatorMapping(NCSInstructionType.ADDVV, DataType.Vector, DataType.Vector, DataType.Vector),
                new BinaryOperatorMapping(NCSInstructionType.ADDSS, DataType.String, DataType.String, DataType.String)
            });

        public static OperatorMapping Subtraction = new OperatorMapping(
            new List<UnaryOperatorMapping>(),
            new List<BinaryOperatorMapping>
            {
                new BinaryOperatorMapping(NCSInstructionType.SUBII, DataType.Int, DataType.Int, DataType.Int),
                new BinaryOperatorMapping(NCSInstructionType.SUBIF, DataType.Int, DataType.Int, DataType.Float),
                new BinaryOperatorMapping(NCSInstructionType.SUBFI, DataType.Float, DataType.Float, DataType.Int),
                new BinaryOperatorMapping(NCSInstructionType.SUBFF, DataType.Float, DataType.Float, DataType.Float),
                new BinaryOperatorMapping(NCSInstructionType.SUBVV, DataType.Vector, DataType.Vector, DataType.Vector)
            });

        public static OperatorMapping Multiplication = new OperatorMapping(
            new List<UnaryOperatorMapping>(),
            new List<BinaryOperatorMapping>
            {
                new BinaryOperatorMapping(NCSInstructionType.MULII, DataType.Int, DataType.Int, DataType.Int),
                new BinaryOperatorMapping(NCSInstructionType.MULIF, DataType.Int, DataType.Int, DataType.Float),
                new BinaryOperatorMapping(NCSInstructionType.MULFI, DataType.Float, DataType.Float, DataType.Int),
                new BinaryOperatorMapping(NCSInstructionType.MULFF, DataType.Float, DataType.Float, DataType.Float),
                new BinaryOperatorMapping(NCSInstructionType.MULVF, DataType.Vector, DataType.Vector, DataType.Float),
                new BinaryOperatorMapping(NCSInstructionType.MULFV, DataType.Vector, DataType.Float, DataType.Vector)
            });

        public static OperatorMapping Division = new OperatorMapping(
            new List<UnaryOperatorMapping>(),
            new List<BinaryOperatorMapping>
            {
                new BinaryOperatorMapping(NCSInstructionType.DIVII, DataType.Int, DataType.Int, DataType.Int),
                new BinaryOperatorMapping(NCSInstructionType.DIVIF, DataType.Int, DataType.Int, DataType.Float),
                new BinaryOperatorMapping(NCSInstructionType.DIVFI, DataType.Float, DataType.Float, DataType.Int),
                new BinaryOperatorMapping(NCSInstructionType.DIVFF, DataType.Float, DataType.Float, DataType.Float),
                new BinaryOperatorMapping(NCSInstructionType.DIVVF, DataType.Vector, DataType.Vector, DataType.Float)
            });

        public static OperatorMapping Modulus = new OperatorMapping(
            new List<UnaryOperatorMapping>(),
            new List<BinaryOperatorMapping>
            {
                new BinaryOperatorMapping(NCSInstructionType.MODII, DataType.Int, DataType.Int, DataType.Int)
            });

        public static OperatorMapping Equal = new OperatorMapping(
            new List<UnaryOperatorMapping>(),
            new List<BinaryOperatorMapping>
            {
                new BinaryOperatorMapping(NCSInstructionType.EQUALII, DataType.Int, DataType.Int, DataType.Int),
                new BinaryOperatorMapping(NCSInstructionType.EQUALFF, DataType.Int, DataType.Float, DataType.Float),
                new BinaryOperatorMapping(NCSInstructionType.EQUALSS, DataType.Int, DataType.String, DataType.String),
                new BinaryOperatorMapping(NCSInstructionType.EQUALOO, DataType.Int, DataType.Object, DataType.Object)
            });

        public static OperatorMapping NotEqual = new OperatorMapping(
            new List<UnaryOperatorMapping>(),
            new List<BinaryOperatorMapping>
            {
                new BinaryOperatorMapping(NCSInstructionType.NEQUALII, DataType.Int, DataType.Int, DataType.Int),
                new BinaryOperatorMapping(NCSInstructionType.NEQUALFF, DataType.Int, DataType.Float, DataType.Float),
                new BinaryOperatorMapping(NCSInstructionType.NEQUALSS, DataType.Int, DataType.String, DataType.String),
                new BinaryOperatorMapping(NCSInstructionType.NEQUALOO, DataType.Int, DataType.Object, DataType.Object)
            });

        public static OperatorMapping GreaterThan = new OperatorMapping(
            new List<UnaryOperatorMapping>(),
            new List<BinaryOperatorMapping>
            {
                new BinaryOperatorMapping(NCSInstructionType.GTII, DataType.Int, DataType.Int, DataType.Int),
                new BinaryOperatorMapping(NCSInstructionType.GTFF, DataType.Int, DataType.Float, DataType.Float)
            });

        public static OperatorMapping LessThan = new OperatorMapping(
            new List<UnaryOperatorMapping>(),
            new List<BinaryOperatorMapping>
            {
                new BinaryOperatorMapping(NCSInstructionType.LTII, DataType.Int, DataType.Int, DataType.Int),
                new BinaryOperatorMapping(NCSInstructionType.LTFF, DataType.Int, DataType.Float, DataType.Float)
            });

        public static OperatorMapping GreaterThanOrEqual = new OperatorMapping(
            new List<UnaryOperatorMapping>(),
            new List<BinaryOperatorMapping>
            {
                new BinaryOperatorMapping(NCSInstructionType.GEQII, DataType.Int, DataType.Int, DataType.Int),
                new BinaryOperatorMapping(NCSInstructionType.GEQFF, DataType.Int, DataType.Float, DataType.Float)
            });

        public static OperatorMapping LessThanOrEqual = new OperatorMapping(
            new List<UnaryOperatorMapping>(),
            new List<BinaryOperatorMapping>
            {
                new BinaryOperatorMapping(NCSInstructionType.LEQII, DataType.Int, DataType.Int, DataType.Int),
                new BinaryOperatorMapping(NCSInstructionType.LEQFF, DataType.Int, DataType.Float, DataType.Float)
            });

        public static OperatorMapping LogicalAnd = new OperatorMapping(
            new List<UnaryOperatorMapping>(),
            new List<BinaryOperatorMapping>
            {
                new BinaryOperatorMapping(NCSInstructionType.LOGANDII, DataType.Int, DataType.Int, DataType.Int)
            });

        public static OperatorMapping LogicalOr = new OperatorMapping(
            new List<UnaryOperatorMapping>(),
            new List<BinaryOperatorMapping>
            {
                new BinaryOperatorMapping(NCSInstructionType.LOGORII, DataType.Int, DataType.Int, DataType.Int)
            });

        public static OperatorMapping BitwiseAnd = new OperatorMapping(
            new List<UnaryOperatorMapping>(),
            new List<BinaryOperatorMapping>
            {
                new BinaryOperatorMapping(NCSInstructionType.BOOLANDII, DataType.Int, DataType.Int, DataType.Int)
            });

        public static OperatorMapping BitwiseOr = new OperatorMapping(
            new List<UnaryOperatorMapping>(),
            new List<BinaryOperatorMapping>
            {
                new BinaryOperatorMapping(NCSInstructionType.INCORII, DataType.Int, DataType.Int, DataType.Int)
            });

        public static OperatorMapping BitwiseXor = new OperatorMapping(
            new List<UnaryOperatorMapping>(),
            new List<BinaryOperatorMapping>
            {
                new BinaryOperatorMapping(NCSInstructionType.EXCORII, DataType.Int, DataType.Int, DataType.Int)
            });

        public static OperatorMapping BitwiseLeft = new OperatorMapping(
            new List<UnaryOperatorMapping>(),
            new List<BinaryOperatorMapping>
            {
                new BinaryOperatorMapping(NCSInstructionType.SHLEFTII, DataType.Int, DataType.Int, DataType.Int)
            });

        public static OperatorMapping BitwiseRight = new OperatorMapping(
            new List<UnaryOperatorMapping>(),
            new List<BinaryOperatorMapping>
            {
                new BinaryOperatorMapping(NCSInstructionType.SHRIGHTII, DataType.Int, DataType.Int, DataType.Int)
            });

        public static OperatorMapping BitwiseUnsignedRight = new OperatorMapping(
            new List<UnaryOperatorMapping>(),
            new List<BinaryOperatorMapping>
            {
                new BinaryOperatorMapping(NCSInstructionType.USHRIGHTII, DataType.Int, DataType.Int, DataType.Int)
            });

        public static OperatorMapping Negation = new OperatorMapping(
            new List<UnaryOperatorMapping>
            {
                new UnaryOperatorMapping(NCSInstructionType.NEGI, DataType.Int),
                new UnaryOperatorMapping(NCSInstructionType.NEGF, DataType.Float)
            },
            new List<BinaryOperatorMapping>());

        public static OperatorMapping LogicalNot = new OperatorMapping(
            new List<UnaryOperatorMapping>
            {
                new UnaryOperatorMapping(NCSInstructionType.NOTI, DataType.Int)
            },
            new List<BinaryOperatorMapping>());

        public static OperatorMapping BitwiseNot = new OperatorMapping(
            new List<UnaryOperatorMapping>
            {
                new UnaryOperatorMapping(NCSInstructionType.COMPI, DataType.Int)
            },
            new List<BinaryOperatorMapping>());
    }
}

