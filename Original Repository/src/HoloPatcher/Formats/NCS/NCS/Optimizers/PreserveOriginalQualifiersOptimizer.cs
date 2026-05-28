using System;
using System.Collections.Generic;
using System.Linq;
using KOTORModSync.Formats.NCS;

namespace KOTORModSync.Formats.NCS.Optimizers
{
    /// <summary>
    /// Preserves original bytecode/qualifier values from the original NCS file during roundtrip compilation.
    ///
    /// This optimizer is used during roundtrip tests to ensure 1:1 bytecode parity. When compiling
    /// decompiled source code, the compiler creates new instructions with canonical qualifiers.
    /// This optimizer matches the compiled instructions to the original instructions and copies
    /// the OriginalBytecode/OriginalQualifier fields to preserve invalid qualifiers that may
    /// exist in the original file (e.g., 0x19 for LOGANDxx instead of 0x20).
    ///
    /// Matching is done by:
    /// 1. Instruction type (must match)
    /// 2. Instruction position (relative to start of instructions)
    /// 3. Instruction arguments (must match)
    /// </summary>
    public class PreserveOriginalQualifiersOptimizer : NCSOptimizer
    {
        private readonly NCS _originalNcs;

        public PreserveOriginalQualifiersOptimizer(NCS originalNcs)
        {
            _originalNcs = originalNcs;
        }

        public override void Optimize(NCS ncs)
        {
            if (_originalNcs == null || _originalNcs.Instructions == null || ncs.Instructions == null)
            {
                return;
            }

            // Match instructions by position, type, and arguments
            int minCount = System.Math.Min(_originalNcs.Instructions.Count, ncs.Instructions.Count);
            for (int i = 0; i < minCount; i++)
            {
                NCSInstruction original = _originalNcs.Instructions[i];
                NCSInstruction compiled = ncs.Instructions[i];

                // Only copy qualifiers if:
                // 1. Instruction types match
                // 2. Original has preserved qualifiers
                // 3. Compiled doesn't already have preserved qualifiers
                if (original.InsType == compiled.InsType &&
                    original.OriginalBytecode.HasValue &&
                    original.OriginalQualifier.HasValue &&
                    !compiled.OriginalBytecode.HasValue &&
                    !compiled.OriginalQualifier.HasValue)
                {
                    // Check if arguments match (for instructions with arguments)
                    bool argsMatch = true;
                    if (original.Args != null && compiled.Args != null)
                    {
                        if (original.Args.Count != compiled.Args.Count)
                        {
                            argsMatch = false;
                        }
                        else
                        {
                            for (int j = 0; j < original.Args.Count; j++)
                            {
                                if (!AreArgsEqual(original.Args[j], compiled.Args[j]))
                                {
                                    argsMatch = false;
                                    break;
                                }
                            }
                        }
                    }
                    else if (original.Args != null || compiled.Args != null)
                    {
                        argsMatch = false;
                    }

                    // Check if jump targets match (for jump instructions)
                    bool jumpsMatch = true;
                    if (original.Jump != null || compiled.Jump != null)
                    {
                        if (original.Jump == null || compiled.Jump == null)
                        {
                            jumpsMatch = false;
                        }
                        else
                        {
                            int origJumpIdx = _originalNcs.GetInstructionIndex(original.Jump);
                            int compJumpIdx = ncs.GetInstructionIndex(compiled.Jump);
                            jumpsMatch = (origJumpIdx == compJumpIdx);
                        }
                    }

                    if (argsMatch && jumpsMatch)
                    {
                        // Copy preserved qualifiers from original to compiled
                        compiled.OriginalBytecode = original.OriginalBytecode;
                        compiled.OriginalQualifier = original.OriginalQualifier;

                        // Debug output
                        if (original.InsType.ToString().Contains("LOGAND") || original.InsType.ToString().Contains("LOGOR"))
                        {
                            System.Console.WriteLine($"PreserveOriginalQualifiers: Copied qualifier 0x{original.OriginalQualifier.Value:X2} for {original.InsType} at index {i}");
                        }
                    }
                }
            }
        }

        private static bool AreArgsEqual(object arg1, object arg2)
        {
            if (arg1 == null && arg2 == null) return true;
            if (arg1 == null || arg2 == null) return false;

            // For numeric types, compare values
            if (arg1 is IConvertible conv1 && arg2 is IConvertible conv2)
            {
                try
                {
                    // Try to compare as same type
                    if (arg1.GetType() == arg2.GetType())
                    {
                        return arg1.Equals(arg2);
                    }
                    // Try numeric comparison
                    double d1 = conv1.ToDouble(System.Globalization.CultureInfo.InvariantCulture);
                    double d2 = conv2.ToDouble(System.Globalization.CultureInfo.InvariantCulture);
                    return System.Math.Abs(d1 - d2) < 0.0001;
                }
                catch
                {
                    return arg1.Equals(arg2);
                }
            }

            return arg1.Equals(arg2);
        }
    }
}

