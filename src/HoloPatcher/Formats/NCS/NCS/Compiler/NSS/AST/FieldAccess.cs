using System;
using System.Collections.Generic;
using System.Linq;
using KOTORModSync.Common.Script;
using KOTORModSync.Formats.NCS;
using JetBrains.Annotations;

namespace KOTORModSync.Formats.NCS.Compiler
{

    /// <summary>
    /// Represents a field access chain (e.g., myStruct.field, myVector.x).
    /// </summary>
    public class FieldAccess : Expression
    {
        public List<Identifier> Identifiers { get; set; }

        public FieldAccess(List<Identifier> identifiers)
        {
            Identifiers = identifiers ?? throw new ArgumentNullException(nameof(identifiers));

            if (!Identifiers.Any())
            {
                throw new ArgumentException("FieldAccess must have at least one identifier", nameof(identifiers));
            }
        }

        /// <summary>
        /// Get scoped variable information for this field access.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/classes.py:974-1029
        /// </summary>
        public GetScopedResult GetScoped([CanBeNull] CodeBlock block, CodeRoot root)
        {
            if (!Identifiers.Any())
            {
                throw new CompileError("Internal error: FieldAccess has no identifiers");
            }

            // Matching PyKotor classes.py line 995
            // Original: scoped: GetScopedResult = block.get_scoped(first_ident, root)
            Identifier first = Identifiers[0];
            GetScopedResult scoped = block.GetScoped(first, root);
            bool isGlobal = scoped.IsGlobal;
            DynamicDataType dataType = scoped.Datatype;
            int offset = scoped.Offset;
            bool isConst = scoped.IsConst; // Get is_const from the first call

            // Process remaining identifiers as member accesses
            // Matching PyKotor classes.py lines 1002-1028
            foreach (Identifier nextIdent in Identifiers.Skip(1))
            {
                if (dataType.Builtin == DataType.Vector)
                {
                    dataType = new DynamicDataType(DataType.Float);

                    if (nextIdent.Label == "x")
                    {
                        offset += 0;
                    }
                    else if (nextIdent.Label == "y")
                    {
                        offset += 4;
                    }
                    else if (nextIdent.Label == "z")
                    {
                        offset += 8;
                    }
                    else
                    {
                        throw new CompileError(
                            $"Attempting to access unknown member '{nextIdent}' on vector.\n" +
                            "  Valid members: x, y, z");
                    }
                }
                else if (dataType.Builtin == DataType.Struct)
                {
                    if (dataType.Struct == null)
                    {
                        throw new CompileError($"Internal error: Struct datatype has no struct name");
                    }

                    // Can be null if struct not found
                    if (!root.StructMap.TryGetValue(dataType.Struct, out Struct structDef))
                    {
                        throw new CompileError($"Unknown struct type: {dataType.Struct}");
                    }

                    offset += structDef.ChildOffset(root, nextIdent);
                    dataType = structDef.ChildType(root, nextIdent);
                }
                else
                {
                    throw new CompileError(
                        $"Attempting to access member '{nextIdent}' on non-composite type '{dataType.Builtin.ToScriptString()}'.\n" +
                        "  Only struct and vector types have members.");
                }
            }

            // Matching PyKotor classes.py line 1029
            return new GetScopedResult(isGlobal, dataType, offset, isConst);
        }

        public override DynamicDataType Compile(NCS ncs, CodeRoot root, CodeBlock block)
        {
            GetScopedResult scoped = GetScoped(block, root);
            bool isGlobal = scoped.IsGlobal;
            DynamicDataType variableType = scoped.Datatype;
            int stackIndex = scoped.Offset;
            NCSInstructionType instructionType = isGlobal ? NCSInstructionType.CPTOPBP : NCSInstructionType.CPTOPSP;

            ncs.Add(instructionType, new List<object> { stackIndex, variableType.Size(root) });
            // Python: FieldAccess.compile does NOT add to temp_stack
            // The caller (BinaryOperatorExpression, ExpressionStatement, etc.) will track it

            return variableType;
        }

        public override string ToString()
        {
            return string.Join(".", Identifiers.Select(i => i.Label));
        }
    }
}

