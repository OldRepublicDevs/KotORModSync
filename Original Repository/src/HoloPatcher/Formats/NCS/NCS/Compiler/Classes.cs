using System;
using System.Collections.Generic;
using KOTORModSync.Formats.NCS.Compiler.NSS;
using JetBrains.Annotations;

namespace KOTORModSync.Formats.NCS.Compiler
{

    /// <summary>
    /// Base exception for NSS compilation errors.
    /// Provides detailed error messages to help debug script issues.
    ///
    /// References:
    ///     vendor/HoloLSP/server/src/nwscript-parser.ts (NSS parser error handling)
    ///     vendor/xoreos-tools/src/nwscript/compiler.cpp (NSS compiler error handling)
    ///     vendor/KotOR.js/src/nwscript/NWScriptCompiler.ts (TypeScript compiler errors)
    /// </summary>
    public class CompileError : Exception
    {
        public int? LineNum { get; }
        [CanBeNull]
        public string Context { get; }

        public CompileError(string message, int? lineNum = null, [CanBeNull] string context = null)
            : base(FormatMessage(message, lineNum, context))
        {
            LineNum = lineNum;
            Context = context;
        }

        private static string FormatMessage(string message, int? lineNum, [CanBeNull] string context)
        {
            string fullMessage = message;
            if (!(lineNum is null) && lineNum.HasValue)
            {
                fullMessage = $"Line {lineNum}: {message}";
            }
            if (!(context is null))
            {
                fullMessage = $"{fullMessage}\n  Context: {context}";
            }
            return fullMessage;
        }
    }

    /// <summary>Raised when script has no valid entry point (main or StartingConditional).</summary>
    public class EntryPointError : CompileError
    {
        public EntryPointError(string message, int? lineNum = null, [CanBeNull] string context = null)
            : base(message, lineNum, context)
        {
        }
    }

    /// <summary>Raised when a #include file cannot be found.</summary>
    public class MissingIncludeError : CompileError
    {
        public MissingIncludeError(string message, int? lineNum = null, [CanBeNull] string context = null)
            : base(message, lineNum, context)
        {
        }
    }

    /// <summary>
    /// Result of scoped variable lookup.
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/classes.py:251-255
    /// </summary>
    public class GetScopedResult
    {
        public bool IsGlobal { get; }
        public DynamicDataType Datatype { get; }
        public int Offset { get; }
        public bool IsConst { get; }

        public GetScopedResult(bool isGlobal, DynamicDataType datatype, int offset, bool isConst = false)
        {
            IsGlobal = isGlobal;
            Datatype = datatype;
            Offset = offset;
            IsConst = isConst;
        }

        public void Deconstruct(out bool isGlobal, out DynamicDataType datatype, out int offset, out bool isConst)
        {
            isGlobal = IsGlobal;
            datatype = Datatype;
            offset = Offset;
            isConst = IsConst;
        }

        // Backward compatibility - 3-parameter deconstruct (ignores isConst)
        public void Deconstruct(out bool isGlobal, out DynamicDataType datatype, out int offset)
        {
            isGlobal = IsGlobal;
            datatype = Datatype;
            offset = Offset;
        }
    }

    /// <summary>
    /// Scoped variable in a code block or global scope.
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/classes.py:677-680
    /// </summary>
    public class ScopedValue
    {
        public Identifier Identifier { get; }
        public DynamicDataType DataType { get; }
        public bool IsConst { get; }

        public ScopedValue(Identifier identifier, DynamicDataType dataType, bool isConst = false)
        {
            Identifier = identifier;
            DataType = dataType;
            IsConst = isConst;
        }
    }

    /// <summary>
    /// Reference to a function definition or forward declaration.
    /// </summary>
    public class FunctionReference
    {
        public NCSInstruction Instruction { get; }
        public object Definition { get; } // FunctionForwardDeclaration or FunctionDefinition

        public FunctionReference(NCSInstruction instruction, object definition)
        {
            Instruction = instruction;
            Definition = definition;
        }

        public bool IsPrototype()
        {
            return Definition is FunctionForwardDeclaration;
        }
    }

    /// <summary>
    /// Function forward declaration (prototype).
    /// </summary>
    public class FunctionForwardDeclaration : TopLevelObject
    {
        public DynamicDataType ReturnType { get; }
        public Identifier Identifier { get; }
        public List<FunctionParameter> Parameters { get; }

        public FunctionForwardDeclaration(
            DynamicDataType returnType,
            Identifier identifier,
            List<FunctionParameter> parameters)
        {
            ReturnType = returnType;
            Identifier = identifier;
            Parameters = parameters;
        }

        public override void Compile(NCS ncs, CodeRoot root)
        {
            string functionName = Identifier.Label;

            if (root.FunctionMap.ContainsKey(functionName))
            {
                throw new NSS.CompileError($"Function '{functionName}' already has a prototype or been defined.");
            }

            root.FunctionMap[functionName] = new FunctionReference(
                ncs.Add(NCSInstructionType.NOP, new List<object>()),
                this
            );
        }
    }
}
