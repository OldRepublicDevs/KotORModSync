using System;
using JetBrains.Annotations;

namespace KOTORModSync.Formats.NCS.Compiler.NSS
{

    /// <summary>
    /// Base exception for NSS compilation errors.
    ///
    /// Provides detailed error messages to help debug script issues.
    ///
    /// References:
    ///     vendor/HoloLSP/server/src/nwscript-parser.ts (NSS parser error handling)
    ///     vendor/xoreos-tools/src/nwscript/compiler.cpp (NSS compiler error handling)
    ///     vendor/KotOR.js/src/nwscript/NWScriptCompiler.ts (TypeScript compiler errors)
    /// </summary>
    public class CompileError : Exception
    {
        public int? LineNumber { get; }
        [CanBeNull]
        public string Context { get; }

        public CompileError(string message, int? lineNumber = null, [CanBeNull] string context = null)
            : base(FormatMessage(message, lineNumber, context))
        {
            LineNumber = lineNumber;
            Context = context;
        }

        public CompileError(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        private static string FormatMessage(string message, int? lineNumber, [CanBeNull] string context)
        {
            string fullMessage = message;
            if (lineNumber.HasValue)
            {
                fullMessage = $"Line {lineNumber}: {message}";
            }
            if (!string.IsNullOrEmpty(context))
            {
                fullMessage = $"{fullMessage}\n  Context: {context}";
            }
            return fullMessage;
        }
    }

    /// <summary>
    /// Raised when script has no valid entry point (main or StartingConditional).
    /// </summary>
    public class EntryPointError : CompileError
    {
        public EntryPointError(string message) : base(message)
        {
        }

        public EntryPointError(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Raised when a #include file cannot be found.
    /// </summary>
    public class MissingIncludeError : CompileError
    {
        public string IncludePath { get; }

        public MissingIncludeError(string message, string includePath) : base(message)
        {
            IncludePath = includePath;
        }

        public MissingIncludeError(string message, string includePath, Exception innerException)
            : base(message, innerException)
        {
            IncludePath = includePath;
        }
    }
}

