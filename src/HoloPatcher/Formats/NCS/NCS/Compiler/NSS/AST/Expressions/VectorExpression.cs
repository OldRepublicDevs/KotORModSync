using System;
using System.Collections.Generic;
using KOTORModSync.Common.Script;
using KOTORModSync.Formats.NCS;

namespace KOTORModSync.Formats.NCS.Compiler
{

    /// <summary>
    /// Represents a vector literal expression (e.g., [1.0, 2.0, 3.0]).
    /// </summary>
    public class VectorExpression : Expression
    {
        public Expression X { get; set; }
        public Expression Y { get; set; }
        public Expression Z { get; set; }

        public VectorExpression(Expression x, Expression y, Expression z)
        {
            X = x ?? throw new ArgumentNullException(nameof(x));
            Y = y ?? throw new ArgumentNullException(nameof(y));
            Z = z ?? throw new ArgumentNullException(nameof(z));
        }

        public override DynamicDataType Compile(NCS ncs, CodeRoot root, CodeBlock block)
        {
            // Compile each component
            DynamicDataType xType = X.Compile(ncs, root, block);
            if (xType.Builtin != DataType.Float && xType.Builtin != DataType.Int)
            {
                throw new CompileError($"Vector component must be numeric, got {xType.Builtin.ToScriptString()}");
            }
            block.TempStack += 4;

            DynamicDataType yType = Y.Compile(ncs, root, block);
            if (yType.Builtin != DataType.Float && yType.Builtin != DataType.Int)
            {
                throw new CompileError($"Vector component must be numeric, got {yType.Builtin.ToScriptString()}");
            }
            block.TempStack += 4;

            DynamicDataType zType = Z.Compile(ncs, root, block);
            if (zType.Builtin != DataType.Float && zType.Builtin != DataType.Int)
            {
                throw new CompileError($"Vector component must be numeric, got {zType.Builtin.ToScriptString()}");
            }
            block.TempStack += 4;

            // Vector is 12 bytes (3 floats)
            return new DynamicDataType(DataType.Vector);
        }

        public override string ToString()
        {
            return $"[{X}, {Y}, {Z}]";
        }
    }
}

