using System;
using System.Collections.Generic;
using System.Linq;
using KOTORModSync.Formats.NCS;

namespace KOTORModSync.Formats.NCS.Compiler
{

    /// <summary>
    /// Represents a call to a user-defined function.
    /// </summary>
    public class FunctionCallExpression : Expression
    {
        public Identifier FunctionName { get; set; }
        public List<Expression> Arguments { get; set; }

        public FunctionCallExpression(Identifier functionName, List<Expression> arguments)
        {
            FunctionName = functionName ?? throw new ArgumentNullException(nameof(functionName));
            Arguments = arguments ?? new List<Expression>();
        }

        public override DynamicDataType Compile(NCS ncs, CodeRoot root, CodeBlock block)
        {
            string functionName = FunctionName.Label;
            // Can be null if function not found
            if (!root.FunctionMap.TryGetValue(functionName, out FunctionReference funcRef))
            {
                // Provide helpful error with similar function names
                IEnumerable<string> availableFuncs = root.FunctionMap.Keys.Take(10);
                throw new NSS.CompileError(
                    $"Undefined function '{FunctionName}'\n" +
                    $"  Available functions: {string.Join(", ", availableFuncs)}" +
                    $"{(root.FunctionMap.Count > 10 ? "..." : "")}");
            }

            // Compile JSR (Jump to Subroutine)
            DynamicDataType returnType = root.CompileJsr(ncs, block, functionName, Arguments);

            return returnType;
        }

        public override string ToString()
        {
            string args = string.Join(", ", Arguments.Select(a => a.ToString()));
            return $"{FunctionName}({args})";
        }
    }
}

