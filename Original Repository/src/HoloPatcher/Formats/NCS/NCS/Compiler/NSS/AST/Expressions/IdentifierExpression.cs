using System;
using System.Collections.Generic;
using KOTORModSync.Common.Script;
using KOTORModSync.Formats.NCS;

namespace KOTORModSync.Formats.NCS.Compiler
{

    /// <summary>
    /// Represents a variable identifier expression.
    /// </summary>
    public class IdentifierExpression : Expression
    {
        public Identifier Identifier { get; set; }

        public IdentifierExpression(Identifier identifier)
        {
            Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
        }

        public override DynamicDataType Compile(NCS ncs, CodeRoot root, CodeBlock block)
        {
            // [CanBeNull] Try to find as a constant first
            ScriptConstant constant = root.FindConstant(Identifier.Label);
            if (constant != null)
            {
                // Emit constant value
                // Python: IdentifierExpression.compile does NOT add to temp_stack for constants
                // The caller (BinaryOperatorExpression, ExpressionStatement, etc.) will track it
                switch (constant.DataType)
                {
                    case DataType.Int:
                        ncs.Add(NCSInstructionType.CONSTI, new List<object> { constant.Value });
                        return new DynamicDataType(DataType.Int);
                    case DataType.Float:
                        ncs.Add(NCSInstructionType.CONSTF, new List<object> { constant.Value });
                        return new DynamicDataType(DataType.Float);
                    case DataType.String:
                        ncs.Add(NCSInstructionType.CONSTS, new List<object> { constant.Value });
                        return new DynamicDataType(DataType.String);
                    default:
                        throw new CompileError($"Unsupported constant type: {constant.DataType}");
                }
            }

            // Otherwise, it's a variable - look up in scope
            // Python: IdentifierExpression.compile does NOT add to temp_stack for variables
            // The caller (BinaryOperatorExpression, ExpressionStatement, etc.) will track it
            // Let GetScoped handle temp_stack; do not override offset here.
            GetScopedResult scoped = block.GetScoped(Identifier, root);
            bool isGlobal = scoped.IsGlobal;
            DynamicDataType dataType = scoped.Datatype;
            int offset = scoped.Offset;
            // Always use SP-relative offsets; the temp stack handling is controlled by GetScoped.
            NCSInstructionType instructionType = isGlobal ? NCSInstructionType.CPTOPBP : NCSInstructionType.CPTOPSP;

            ncs.Add(instructionType, new List<object> { offset, dataType.Size(root) });
            // Python: FieldAccess.compile does NOT add to temp_stack
            // The caller (BinaryOperatorExpression, ExpressionStatement, etc.) will track it

            return dataType;
        }

        public bool IsConstant(CodeRoot root)
        {
            return root.FindConstant(Identifier.Label) != null;
        }

        public override string ToString()
        {
            return Identifier.Label;
        }
    }
}

