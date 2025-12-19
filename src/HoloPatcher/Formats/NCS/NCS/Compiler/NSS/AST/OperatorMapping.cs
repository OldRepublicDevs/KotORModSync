using System.Collections.Generic;
using KOTORModSync.Common.Script;
using KOTORModSync.Formats.NCS;

namespace KOTORModSync.Formats.NCS.Compiler
{

    /// <summary>
    /// Maps operators to NCS instruction types for different operand type combinations.
    /// </summary>
    public class OperatorMapping
    {
        public List<UnaryOperatorMapping> Unary { get; set; }
        public List<BinaryOperatorMapping> Binary { get; set; }

        public OperatorMapping(List<UnaryOperatorMapping> unary, List<BinaryOperatorMapping> binary)
        {
            Unary = unary ?? new List<UnaryOperatorMapping>();
            Binary = binary ?? new List<BinaryOperatorMapping>();
        }
    }

    /// <summary>
    /// Maps a binary operator to its NCS instruction for specific operand types.
    /// </summary>
    public class BinaryOperatorMapping
    {
        public NCSInstructionType Instruction { get; set; }
        public DataType Result { get; set; }
        public DataType Lhs { get; set; }
        public DataType Rhs { get; set; }

        public BinaryOperatorMapping(
            NCSInstructionType instruction,
            DataType result,
            DataType lhs,
            DataType rhs)
        {
            Instruction = instruction;
            Result = result;
            Lhs = lhs;
            Rhs = rhs;
        }

        public override string ToString()
        {
            return $"BinaryOperatorMapping(instruction={Instruction}, result={Result}, lhs={Lhs}, rhs={Rhs})";
        }
    }

    /// <summary>
    /// Maps a unary operator to its NCS instruction for a specific operand type.
    /// </summary>
    public class UnaryOperatorMapping
    {
        public NCSInstructionType Instruction { get; set; }
        public DataType Rhs { get; set; }

        public UnaryOperatorMapping(NCSInstructionType instruction, DataType rhs)
        {
            Instruction = instruction;
            Rhs = rhs;
        }

        public override string ToString()
        {
            return $"UnaryOperatorMapping(instruction={Instruction}, rhs={Rhs})";
        }
    }
}

