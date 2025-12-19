using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KOTORModSync.Formats.NCS;
using KOTORModSync.Formats.NCS.NCSDecomp;
using KOTORModSync.Formats.NCS.NCSDecomp.Analysis;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;
using AST = KOTORModSync.Formats.NCS.NCSDecomp.AST;
using static KOTORModSync.Formats.NCS.NCSDecomp.DecompilerLogger;

namespace KOTORModSync.Formats.NCS.NCSDecomp.Utils
{
    /*
    NCS to AST Converter - Comprehensive instruction conversion.

    This module provides comprehensive conversion of NCS (NWScript Compiled Script) bytecode
    instructions directly to NCSDecomp AST (Abstract Syntax Tree) format, bypassing the traditional
    Decoder -> Lexer -> Parser chain for improved performance and accuracy.

    The converter handles all NCS instruction types comprehensively:
    - Constants: CONSTI, CONSTF, CONSTS, CONSTO
    - Control flow: JMP, JSR, JZ, JNZ, RETN
    - Stack operations: CPDOWNSP, CPTOPSP, CPDOWNBP, CPTOPBP, MOVSP, INCxSP, DECxSP, INCxBP, DECxBP
    - RSADD variants: RSADDI, RSADDF, RSADDS, RSADDO, RSADDEFF, RSADDEVT, RSADDLOC, RSADDTAL
    - Function calls: ACTION
    - Stack management: SAVEBP, RESTOREBP, STORE_STATE, DESTRUCT
    - Arithmetic: ADDxx, SUBxx, MULxx, DIVxx, MODxx, NEGx
    - Comparison: EQUALxx, NEQUALxx, GTxx, GEQxx, LTxx, LEQxx
    - Logical: LOGANDxx, LOGORxx, NOTx
    - Bitwise: BOOLANDxx, INCORxx, EXCORxx, SHLEFTxx, SHRIGHTxx, USHRIGHTxx, COMPx
    - No-ops: NOP, NOP2, RESERVED (typically skipped during conversion)

    References:
    ----------
        vendor/reone/src/libs/script/format/ncsreader.cpp - NCS instruction reading
        vendor/xoreos/src/aurora/nwscript/ncsfile.cpp - NCS instruction execution
        NCSDecomp - Original NCS decompiler implementation
    */
    public static class NcsToAstConverter
    {
        public static Start ConvertNcsToAst(NCS ncs)
        {
            AProgram program = new AProgram();
            List<NCSInstruction> instructions = ncs != null ? ncs.Instructions : null;
            if (instructions == null || instructions.Count == 0)
            {
                Debug("DEBUG NcsToAstConverter: No instructions in NCS");
                return new Start(program, new EOF());
            }
            
            // CRITICAL DEBUG: Log instruction count and verify we have all instructions
            Debug($"DEBUG NcsToAstConverter: Converting {instructions.Count} instructions to AST");
            Console.Error.WriteLine($"DEBUG NcsToAstConverter: Received NCS with {instructions.Count} instructions");
            
            // Log instruction offsets to verify we have the full range
            if (instructions.Count > 0)
            {
                int minOffset = instructions[0].Offset;
                int maxOffset = instructions[instructions.Count - 1].Offset;
                Debug($"DEBUG NcsToAstConverter: Instruction offset range: {minOffset} to {maxOffset}");
                Console.Error.WriteLine($"DEBUG NcsToAstConverter: Instruction offset range: {minOffset} to {maxOffset}");
                
                // Count NEGI instructions
                int negiCount = instructions.Count(inst => inst != null && inst.InsType == NCSInstructionType.NEGI);
                Debug($"DEBUG NcsToAstConverter: Found {negiCount} NEGI instructions in received list");
                Console.Error.WriteLine($"DEBUG NcsToAstConverter: Found {negiCount} NEGI instructions in received list");
                
                // Log NEGI instruction indices and offsets
                if (negiCount > 0)
                {
                    var negiIndices = new List<int>();
                    for (int i = 0; i < instructions.Count; i++)
                    {
                        if (instructions[i] != null && instructions[i].InsType == NCSInstructionType.NEGI)
                        {
                            negiIndices.Add(i);
                        }
                    }
                    Debug($"DEBUG NcsToAstConverter: NEGI instructions at indices: {string.Join(", ", negiIndices.Take(10))}");
                    Console.Error.WriteLine($"DEBUG NcsToAstConverter: NEGI instructions at indices: {string.Join(", ", negiIndices.Take(10))}");
                }
            }

            // CRITICAL DEBUG: Scan ALL instructions to find ACTION instructions
            // Also check instruction offsets to understand the mapping
            int totalActionCount = 0;
            Debug($"DEBUG NcsToAstConverter: Scanning {instructions.Count} instructions for ACTION type");

            // Sample some instructions to see what types we have
            int sampleCount = Math.Min(100, instructions.Count);
            var typeCounts = new Dictionary<NCSInstructionType, int>();
            for (int i = 0; i < sampleCount; i++)
            {
                if (instructions[i] != null)
                {
                    var insType = instructions[i].InsType;
                    if (!typeCounts.ContainsKey(insType))
                    {
                        typeCounts[insType] = 0;
                    }
                    typeCounts[insType]++;
                }
            }
            Debug($"DEBUG NcsToAstConverter: Sample of first {sampleCount} instructions - type counts: {string.Join(", ", typeCounts.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");

            // Also sample around known ACTION instruction offsets (like 2463, 2476, etc.)
            // Find the instruction indices that correspond to those offsets
            if (instructions.Count > 1000)
            {
                Debug($"DEBUG NcsToAstConverter: Checking instructions around offset 2463 (known ACTION location)");
                for (int i = 0; i < Math.Min(instructions.Count, 5000); i++)
                {
                    if (instructions[i] != null && instructions[i].Offset >= 2460 && instructions[i].Offset <= 2470)
                    {
                        Debug($"DEBUG NcsToAstConverter: Instruction at index {i}, offset={instructions[i].Offset}, InsType={instructions[i].InsType}, IsAction={instructions[i].InsType == NCSInstructionType.ACTION}");
                    }
                }
            }

            // Check specific known ACTION instruction indices to verify they're found
            if (instructions.Count > 453)
            {
                Debug($"DEBUG NcsToAstConverter: Pre-check - Instruction 453: InsType={instructions[453]?.InsType}, IsAction={instructions[453]?.InsType == NCSInstructionType.ACTION}");
            }

            for (int i = 0; i < instructions.Count; i++)
            {
                // Progress logging for large files
                if (instructions.Count > 1000 && i > 0 && i % 1000 == 0)
                {
                    Debug($"DEBUG NcsToAstConverter: Progress - scanned {i}/{instructions.Count} instructions, found {totalActionCount} ACTION so far");
                }

                if (instructions[i] == null)
                {
                    if (i < 10 || (i >= 450 && i <= 460)) // Only log nulls for first 10 or around known ACTION
                    {
                        Debug($"DEBUG NcsToAstConverter: WARNING - Instruction at index {i} is null");
                    }
                    continue;
                }
                // Check if InsType is ACTION using both == and Equals for debugging
                bool isAction = instructions[i].InsType == NCSInstructionType.ACTION;
                bool isActionEquals = instructions[i].InsType.Equals(NCSInstructionType.ACTION);

                // Special logging for index 453 where we know there's an ACTION
                if (i == 453)
                {
                    Debug($"DEBUG NcsToAstConverter: At index 453 - InsType={instructions[i].InsType}, isAction={isAction}, isActionEquals={isActionEquals}, Offset={instructions[i].Offset}, totalActionCount before={totalActionCount}");
                    Debug($"DEBUG NcsToAstConverter: At index 453 - Condition check: (isAction || isActionEquals) = ({isAction} || {isActionEquals}) = {isAction || isActionEquals}");
                }

                if (isAction || isActionEquals)
                {
                    totalActionCount++;
                    Debug($"DEBUG NcsToAstConverter: Found ACTION at index {i}, incrementing count to {totalActionCount}");
                    if (totalActionCount <= 10 || i == 453) // Log first 10 ACTION instructions or index 453
                    {
                        try
                        {
                            // ACTION instruction args: [0] = routineId (UInt16), [1] = argCount (byte)
                            int routineId = -1;
                            if (instructions[i].Args.Count > 0)
                            {
                                object arg0 = instructions[i].Args[0];
                                if (arg0 is ushort ushortVal)
                                {
                                    routineId = ushortVal;
                                }
                                else if (arg0 is int intVal)
                                {
                                    routineId = intVal;
                                }
                                else
                                {
                                    routineId = Convert.ToInt32(arg0);
                                }
                            }
                            int offset = instructions[i].Offset;
                            Debug($"DEBUG NcsToAstConverter: ACTION instruction at index {i}, offset={offset}, routineId={routineId}, InsType={instructions[i].InsType}, ==={isAction}, Equals={isActionEquals}, totalActionCount now={totalActionCount}");
                        }
                        catch (Exception ex)
                        {
                            Debug($"DEBUG NcsToAstConverter: Exception logging ACTION at index {i}: {ex.Message}");
                        }
                    }
                }
                else if (i == 453)
                {
                    Debug($"DEBUG NcsToAstConverter: WARNING - Index 453 has InsType=ACTION but condition (isAction || isActionEquals) is FALSE!");
                }
                // Log first few instructions to understand the mapping
                if (i < 5)
                {
                    Debug($"DEBUG NcsToAstConverter: Instruction {i}: InsType={instructions[i].InsType}, Offset={instructions[i].Offset}");
                }
            }
            Debug($"DEBUG NcsToAstConverter: Loop completed. Total ACTION instructions in entire file: {totalActionCount}");

            HashSet<int> subroutineStarts = new HashSet<int>();
            // Matching NCSDecomp implementation: detect SAVEBP to split globals from main
            // Globals subroutine ends at SAVEBP, main starts after SAVEBP
            // CRITICAL: Find the LAST SAVEBP before the main function, not the first one
            // Some files have multiple SAVEBP instructions (e.g., in globals initialization),
            // but we need the one that marks the boundary between globals and main
            int savebpIndex = -1;
            List<int> allSavebpIndices = new List<int>();
            for (int i = 0; i < instructions.Count; i++)
            {
                if (instructions[i].InsType == NCSInstructionType.SAVEBP)
                {
                    allSavebpIndices.Add(i);
                    savebpIndex = i;
                    Debug($"DEBUG NcsToAstConverter: Found SAVEBP at instruction index {i}, offset={instructions[i].Offset}");
                    // Continue searching to find the LAST SAVEBP (don't break on first match)
                }
            }

            // CRITICAL FIX FOR INCLUDE FILES:
            // Include files (like k_inc_utility.nss, k_inc_kas.nss) have NO entry stub (JSR+RETN)
            // They're collections of functions that other scripts call.
            // Detection heuristics:
            // 1. File starts with SAVEBP (function start, not globals init)
            // 2. File starts with JMP (common for include files that jump over code)
            // 3. File has multiple SAVEBP instructions and NO entry stub pattern
            // 4. File has SAVEBP count > 1 and the entry pattern (JSR+RETN) is missing
            bool isIncludeFile = false;
            if (instructions.Count > 0 && instructions[0].InsType == NCSInstructionType.SAVEBP)
            {
                // File starts with SAVEBP - likely an include file with multiple functions
                // For include files, each SAVEBP marks the start of a function
                isIncludeFile = true;
                Debug($"DEBUG NcsToAstConverter: File starts with SAVEBP - detected as INCLUDE FILE with {allSavebpIndices.Count} functions");
            }
            // Also detect include files that start with JMP (jump over function bodies)
            else if (instructions.Count > 1 && instructions[0].InsType == NCSInstructionType.JMP)
            {
                // Check if the JMP target is followed by multiple SAVEBP-RETN patterns
                // This is common for include files that jump to an empty entry point
                if (allSavebpIndices.Count > 1)
                {
                    isIncludeFile = true;
                    Debug($"DEBUG NcsToAstConverter: File starts with JMP and has {allSavebpIndices.Count} SAVEBP instructions - detected as INCLUDE FILE");
                }
            }
            // ADDITIONAL DETECTION: Include files with globals but no entry stub
            // If there are multiple SAVEBP instructions (multiple functions) and no entry stub pattern
            // (JSR+RETN after the last SAVEBP), this is likely an include file
            else if (allSavebpIndices.Count > 1)
            {
                // Check for entry stub pattern AFTER the last SAVEBP
                // Normal scripts have: [globals up to SAVEBP] [JSR] [RETN] [main code]
                // Include files have: [globals up to SAVEBP] [function1 code] [SAVEBP] [function2 code] ...
                bool hasEntryStub = false;
                int entryStubCheck = savebpIndex + 1;
                if (instructions.Count > entryStubCheck + 1)
                {
                    bool isJsrRetn = instructions[entryStubCheck].InsType == NCSInstructionType.JSR &&
                                     instructions[entryStubCheck + 1].InsType == NCSInstructionType.RETN;
                    bool isJsrRestorebp = instructions[entryStubCheck].InsType == NCSInstructionType.JSR &&
                                          instructions[entryStubCheck + 1].InsType == NCSInstructionType.RESTOREBP;
                    hasEntryStub = isJsrRetn || isJsrRestorebp;
                }

                if (!hasEntryStub)
                {
                    isIncludeFile = true;
                    Debug($"DEBUG NcsToAstConverter: Multiple SAVEBP ({allSavebpIndices.Count}) and NO entry stub - detected as INCLUDE FILE");
                }
            }
            if (savebpIndex == -1)
            {
                Debug("DEBUG NcsToAstConverter: No SAVEBP instruction found - no globals subroutine will be created");
            }
            else
            {
                Debug($"DEBUG NcsToAstConverter: Found {allSavebpIndices.Count} SAVEBP instruction(s) at indices: {string.Join(", ", allSavebpIndices)}");
                Debug($"DEBUG NcsToAstConverter: Using LAST SAVEBP at instruction index {savebpIndex} as globals boundary");

                // Debug: Show instructions around the SAVEBP for context
                int debugStart = Math.Max(0, savebpIndex - 3);
                int debugEnd = Math.Min(instructions.Count - 1, savebpIndex + 3);
                Debug($"DEBUG NcsToAstConverter: Instructions around SAVEBP ({debugStart}-{debugEnd}):");
                for (int i = debugStart; i <= debugEnd; i++)
                {
                    string marker = (i == savebpIndex) ? " <-- SAVEBP" : "";
                    Debug($"  {i:D4}: {instructions[i].InsType}{marker}");
                }

                // CRITICAL DEBUG: Check if there are RSADDI instructions AFTER the SAVEBP we found
                // This would indicate we're using the wrong SAVEBP as the boundary
                Debug($"DEBUG NcsToAstConverter: Checking for RSADDI instructions after SAVEBP at index {savebpIndex}:");
                int rsaddiCountAfterSavebp = 0;
                for (int i = savebpIndex + 1; i < instructions.Count && i < savebpIndex + 50; i++)
                {
                    if (instructions[i].InsType == NCSInstructionType.RSADDI ||
                        instructions[i].InsType == NCSInstructionType.RSADDF ||
                        instructions[i].InsType == NCSInstructionType.RSADDS ||
                        instructions[i].InsType == NCSInstructionType.RSADDO)
                    {
                        rsaddiCountAfterSavebp++;
                        Debug($"  WARNING: Found RSADD instruction at index {i} AFTER SAVEBP at {savebpIndex} - this suggests we're using the wrong SAVEBP!");
                    }
                }
                if (rsaddiCountAfterSavebp > 0)
                {
                    Debug($"  ERROR: Found {rsaddiCountAfterSavebp} RSADD instruction(s) after SAVEBP - globals range calculation is WRONG!");
                }
                else
                {
                    Debug($"  OK: No RSADD instructions found after SAVEBP (checked up to index {Math.Min(savebpIndex + 50, instructions.Count - 1)})");
                }
            }

            // Identify entry stub pattern: JSR followed by RETN (or JSR, RESTOREBP, RETN)
            // If there's a SAVEBP, entry stub starts at savebpIndex+1
            // Otherwise, entry stub is at position 0
            // The entry JSR target is main, not a separate subroutine
            // 
            // IMPORTANT: Entry stub patterns can include RSADD* at the start for functions with return values:
            // - void main(): JSR, RETN
            // - int StartingConditional(): RSADDI, JSR, RETN
            // - float SomeFunc(): RSADDF, JSR, RETN
            // etc.
            int entryJsrTarget = -1;
            int entryStubStart = (savebpIndex >= 0) ? savebpIndex + 1 : 0;
            int entryReturnType = 0; // 0=void, 3=int, 4=float, 5=string, 6=object
            
            // Helper to check if an instruction is an RSADD* variant (reserves stack space for return value)
            bool IsRsaddInstruction(NCSInstructionType insType)
            {
                return insType == NCSInstructionType.RSADDI ||
                       insType == NCSInstructionType.RSADDF ||
                       insType == NCSInstructionType.RSADDS ||
                       insType == NCSInstructionType.RSADDO ||
                       insType == NCSInstructionType.RSADDEFF ||
                       insType == NCSInstructionType.RSADDEVT ||
                       insType == NCSInstructionType.RSADDLOC ||
                       insType == NCSInstructionType.RSADDTAL;
            }
            
            // Map RSADD* instruction to return type byte value
            int GetReturnTypeFromRsadd(NCSInstructionType insType)
            {
                switch (insType)
                {
                    case NCSInstructionType.RSADDI: return 3; // int
                    case NCSInstructionType.RSADDF: return 4; // float
                    case NCSInstructionType.RSADDS: return 5; // string
                    case NCSInstructionType.RSADDO: return 6; // object
                    case NCSInstructionType.RSADDEFF: return 16; // effect
                    case NCSInstructionType.RSADDEVT: return 17; // event
                    case NCSInstructionType.RSADDLOC: return 18; // location
                    case NCSInstructionType.RSADDTAL: return 19; // talent
                    default: return 0; // void
                }
            }
            
            if (instructions.Count >= entryStubStart + 2)
            {
                Debug($"DEBUG NcsToAstConverter: Checking entry stub at {entryStubStart}: {instructions[entryStubStart].InsType}, next: {instructions[entryStubStart + 1].InsType}");
                
                // Check if entry stub starts with RSADD* (function returns a value)
                int jsrOffset = 0; // Offset to JSR instruction from entryStubStart
                if (IsRsaddInstruction(instructions[entryStubStart].InsType))
                {
                    entryReturnType = GetReturnTypeFromRsadd(instructions[entryStubStart].InsType);
                    jsrOffset = 1; // JSR is at position entryStubStart + 1
                    Debug($"DEBUG NcsToAstConverter: Entry stub has RSADD* prefix ({instructions[entryStubStart].InsType}), return type={entryReturnType}");
                }
                
                int jsrIdx = entryStubStart + jsrOffset;
                
                // Pattern 1: [RSADD*], JSR followed by RETN (simple entry stub)
                if (instructions.Count > jsrIdx + 1 &&
                    instructions[jsrIdx].InsType == NCSInstructionType.JSR &&
                    instructions[jsrIdx].Jump != null &&
                    instructions[jsrIdx + 1].InsType == NCSInstructionType.RETN)
                {
                    try
                    {
                        entryJsrTarget = ncs.GetInstructionIndex(instructions[jsrIdx].Jump);
                        Debug($"DEBUG NcsToAstConverter: Detected entry stub pattern ({(jsrOffset > 0 ? instructions[entryStubStart].InsType + "+" : "")}JSR+RETN) - JSR at {jsrIdx} targets {entryJsrTarget} (main), returnType={entryReturnType}");
                    }
                    catch (Exception)
                    {
                    }
                }
                // Pattern 2: [RSADD*], JSR, RESTOREBP (entry stub with RESTOREBP, used by external compiler)
                else if (instructions.Count > jsrIdx + 1 &&
                         instructions[jsrIdx].InsType == NCSInstructionType.JSR &&
                         instructions[jsrIdx].Jump != null &&
                         instructions[jsrIdx + 1].InsType == NCSInstructionType.RESTOREBP)
                {
                    try
                    {
                        entryJsrTarget = ncs.GetInstructionIndex(instructions[jsrIdx].Jump);
                        Debug($"DEBUG NcsToAstConverter: Detected entry stub pattern ({(jsrOffset > 0 ? instructions[entryStubStart].InsType + "+" : "")}JSR+RESTOREBP) - JSR at {jsrIdx} targets {entryJsrTarget} (main), returnType={entryReturnType}");
                    }
                    catch (Exception)
                    {
                    }
                }
                // Fallback: Check if first instruction IS JSR (void main pattern without RSADD*)
                else if (instructions[entryStubStart].InsType == NCSInstructionType.JSR &&
                         instructions[entryStubStart].Jump != null &&
                         instructions.Count > entryStubStart + 1 &&
                         instructions[entryStubStart + 1].InsType == NCSInstructionType.RETN)
                {
                    try
                    {
                        entryJsrTarget = ncs.GetInstructionIndex(instructions[entryStubStart].Jump);
                        entryReturnType = 0; // void
                        Debug($"DEBUG NcsToAstConverter: Detected entry stub pattern (JSR+RETN, no RSADD*) - JSR at {entryStubStart} targets {entryJsrTarget} (main), returnType=void");
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            for (int i = 0; i < instructions.Count; i++)
            {
                NCSInstruction inst = instructions[i];
                if (inst.InsType == NCSInstructionType.JSR && inst.Jump != null)
                {
                    try
                    {
                        int jumpIdx = ncs.GetInstructionIndex(inst.Jump);
                        // Exclude entry JSR target (main) and position 0 from subroutine starts
                        // Also exclude positions within globals range (0 to savebpIndex+1) and entry stub
                        int globalsAndStubEnd = (savebpIndex >= 0) ? savebpIndex + 1 : 0;
                        if (savebpIndex >= 0)
                        {
                            // Check for entry stub and extend globalsAndStubEnd
                            int entryStubCheck = globalsAndStubEnd;
                            if (instructions.Count > entryStubCheck + 1 &&
                                instructions[entryStubCheck].InsType == NCSInstructionType.JSR &&
                                instructions[entryStubCheck + 1].InsType == NCSInstructionType.RETN)
                            {
                                globalsAndStubEnd = entryStubCheck + 2; // JSR + RETN
                            }
                            else if (instructions.Count > entryStubCheck + 1 &&
                                     instructions[entryStubCheck].InsType == NCSInstructionType.JSR &&
                                     instructions[entryStubCheck + 1].InsType == NCSInstructionType.RESTOREBP)
                            {
                                globalsAndStubEnd = entryStubCheck + 2; // JSR + RESTOREBP
                            }
                        }

                        // Only add if jumpIdx is after globals/entry stub and not the entry JSR target
                        // CRITICAL: Also ensure jumpIdx is within valid instruction range (0 to instructions.Count-1)
                        // A subroutine start at instructions.Count would be out of bounds
                        if (jumpIdx > globalsAndStubEnd && jumpIdx != entryJsrTarget && jumpIdx < instructions.Count)
                        {
                            subroutineStarts.Add(jumpIdx);
                            Debug($"DEBUG NcsToAstConverter: Found subroutine start at {jumpIdx} (JSR at {i} targets {jumpIdx})");
                        }
                        else if (jumpIdx >= instructions.Count)
                        {
                            Debug($"DEBUG NcsToAstConverter: Skipping JSR at {i} targeting {jumpIdx} (out of bounds, instructions.Count={instructions.Count})");
                        }
                        else
                        {
                            if (jumpIdx <= globalsAndStubEnd)
                            {
                                Debug($"DEBUG NcsToAstConverter: Skipping JSR at {i} targeting {jumpIdx} (in globals/stub range, ends at {globalsAndStubEnd})");
                            }
                            else if (jumpIdx == entryJsrTarget)
                            {
                                Debug($"DEBUG NcsToAstConverter: Skipping JSR at {i} targeting {jumpIdx} (is entry JSR target)");
                            }
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            int mainStart = 0;
            int alternativeMainStart = -1;
            int mainEnd = instructions.Count;
            bool mainStartIsAfterSavebp = false; // Flag to indicate mainStart was intentionally set to SAVEBP+1

            // Calculate mainEnd - it should be the minimum of all subroutine starts that are AFTER mainStart
            // But we need to calculate mainStart first, so we'll do this after mainStart is determined
            // TODO: SIMPLIFIED - For now, just set it to instructions.Count as default
            // We'll recalculate mainEnd after mainStart is determined

            // If SAVEBP is found, create globals subroutine (0 to SAVEBP+1)
            // Then calculate where main should start (after globals and entry stub)
            // SKIP for include files - they don't have globals, each SAVEBP is a function start
            bool entryJsrTargetIsLastRetn = (entryJsrTarget >= 0 && entryJsrTarget == instructions.Count - 1);
            bool shouldDeferGlobals = false;
            int actionCountInGlobalsEarly = 0; // Declare outside if block for later use

            if (savebpIndex >= 0 && !isIncludeFile)
            {
                // CRITICAL: Count ACTION instructions in globals range BEFORE deciding whether to defer globals
                // This helps detect when main code is in the globals range (SAVEBP is very late)
                int actionCountInGlobalsForDefer = 0;
                for (int i = 0; i <= savebpIndex && i < instructions.Count; i++)
                {
                    if (instructions[i].InsType == NCSInstructionType.ACTION)
                    {
                        actionCountInGlobalsForDefer++;
                    }
                }
                Debug($"DEBUG NcsToAstConverter: ACTION instructions in globals range (0-{savebpIndex}): {actionCountInGlobalsForDefer}");

                // Check if entry JSR targets last RETN - if so, main code might be in globals range
                // Also check if there are ACTION instructions in globals - if so, main code might be in globals
                // In that case, we'll split globals later after determining main start
                bool mightNeedSplit = entryJsrTargetIsLastRetn || actionCountInGlobalsForDefer > 0;

                if (!mightNeedSplit)
                {
                    // Normal case: create globals subroutine from 0 to SAVEBP+1
                    ASubroutine globalsSub = ConvertInstructionRangeToSubroutine(ncs, instructions, 0, savebpIndex + 1, 0);
                    if (globalsSub != null)
                    {
                        program.GetSubroutine().Add(globalsSub);
                    }
                }
                else
                {
                    // Special case: entry JSR targets last RETN OR there are ACTION instructions in globals
                    // Defer globals creation - we'll create it after determining main start, splitting if needed
                    shouldDeferGlobals = true;
                    Debug($"DEBUG NcsToAstConverter: Deferring globals creation (entry JSR targets last RETN={entryJsrTargetIsLastRetn} OR actionCountInGlobalsForDefer={actionCountInGlobalsForDefer} > 0, may need to split)");
                }

                // Calculate where globals and entry stub end
                int globalsEnd = savebpIndex + 1;
                int entryStubEnd = globalsEnd;

                // Check for entry stub pattern at savebpIndex+1
                // Pattern 1: JSR (at savebpIndex+1) + RETN (at savebpIndex+2)
                if (instructions.Count > entryStubEnd + 1 &&
                    instructions[entryStubEnd].InsType == NCSInstructionType.JSR &&
                    instructions[entryStubEnd + 1].InsType == NCSInstructionType.RETN)
                {
                    entryStubEnd = entryStubEnd + 2; // JSR + RETN
                    Debug($"DEBUG NcsToAstConverter: Entry stub pattern JSR+RETN detected, entry stub ends at {entryStubEnd}");
                }
                // Pattern 2: JSR (at savebpIndex+1) + RESTOREBP (at savebpIndex+2)
                else if (instructions.Count > entryStubEnd + 1 &&
                         instructions[entryStubEnd].InsType == NCSInstructionType.JSR &&
                         instructions[entryStubEnd + 1].InsType == NCSInstructionType.RESTOREBP)
                {
                    entryStubEnd = entryStubEnd + 2; // JSR + RESTOREBP
                    Debug($"DEBUG NcsToAstConverter: Entry stub pattern JSR+RESTOREBP detected, entry stub ends at {entryStubEnd}");
                }

                // CRITICAL: Ensure mainStart is ALWAYS after globals and entry stub
                // If entryJsrTarget points to globals range (0 to entryStubEnd), ignore it
                // Also ignore if entryJsrTarget points to the last RETN (likely wrong target)
                // The last RETN is typically at instructions.Count - 1
                bool entryJsrTargetIsLastRetn2 = (entryJsrTarget >= 0 && entryJsrTarget == instructions.Count - 1);

                // Special case: If entry JSR targets last RETN, check if there's a JSR at position 0
                // that might be the actual main entry point (common in some compiler outputs)
                // CRITICAL: Only use this if JSR at 0 targets a position AFTER SAVEBP, otherwise it's just
                // part of globals initialization (like in asd.nss where JSR 0->2 is globals, not main)
                if (entryJsrTargetIsLastRetn2 && instructions.Count > 0 &&
                    instructions[0].InsType == NCSInstructionType.JSR && instructions[0].Jump != null)
                {
                    try
                    {
                        int jsr0Target = ncs.GetInstructionIndex(instructions[0].Jump);
                        // FIXED: Only consider this as alternative main start if it's AFTER SAVEBP
                        // If it's before SAVEBP, it's part of globals initialization, not main
                        // Main function should be empty in that case (entry JSR targets last RETN)
                        if (jsr0Target > savebpIndex && jsr0Target < entryStubEnd)
                        {
                            alternativeMainStart = jsr0Target;
                            Debug($"DEBUG NcsToAstConverter: Found alternative main start at {alternativeMainStart} (JSR at 0 targets {jsr0Target}, entry JSR targets last RETN, target is after SAVEBP)");
                        }
                        else if (jsr0Target <= savebpIndex)
                        {
                            Debug($"DEBUG NcsToAstConverter: JSR at 0 targets {jsr0Target} which is before/at SAVEBP ({savebpIndex}) - this is globals initialization, not main function");
                        }
                    }
                    catch (Exception)
                    {
                    }
                }

                if (entryJsrTarget >= 0 && entryJsrTarget > entryStubEnd && !entryJsrTargetIsLastRetn2)
                {
                    // entryJsrTarget is valid and after entry stub and not the last RETN - use it
                    mainStart = entryJsrTarget;
                    Debug($"DEBUG NcsToAstConverter: Using entryJsrTarget {entryJsrTarget} as mainStart (after entry stub at {entryStubEnd})");
                }
                else if (alternativeMainStart >= 0)
                {
                    // Use alternative main start from JSR at 0
                    mainStart = alternativeMainStart;
                    Debug($"DEBUG NcsToAstConverter: Using alternative mainStart {alternativeMainStart} (JSR at 0 target, entry JSR targets last RETN)");
                }
                else if (entryJsrTargetIsLastRetn2 && entryJsrTarget >= 0 && entryJsrTarget >= entryStubEnd)
                {
                    // CRITICAL FIX: If entry JSR targets last RETN, the main function code starts right after SAVEBP
                    // (or after entry stub if there's code there), not at entryStubEnd.
                    // The entry stub (JSR+RETN) is just a wrapper - the actual main code is after globals initialization.
                    // Use SAVEBP+1 as mainStart to include all code from after globals to the last RETN.
                    // If entry stub exists, check if there's code between entryStubEnd and last RETN.
                    // If entryStubEnd is very close to last RETN (only cleanup code), use SAVEBP+1 instead.
                    int codeAfterEntryStub = instructions.Count - entryStubEnd;
                    if (codeAfterEntryStub <= 3)
                    {
                        // Only cleanup code (MOVSP+RETN+RETN) after entry stub - main code is before entry stub
                        mainStart = savebpIndex + 1;
                        mainStartIsAfterSavebp = true; // Mark that mainStart was intentionally set to SAVEBP+1
                        Debug($"DEBUG NcsToAstConverter: entryJsrTarget {entryJsrTarget} is last RETN, but only {codeAfterEntryStub} instructions after entry stub at {entryStubEnd} (likely cleanup), using SAVEBP+1 ({mainStart}) as mainStart");
                    }
                    else
                    {
                        // There's actual code after entry stub - use entryStubEnd as mainStart
                        mainStart = entryStubEnd;
                        Debug($"DEBUG NcsToAstConverter: entryJsrTarget {entryJsrTarget} is last RETN and after entry stub at {entryStubEnd}, using entryStubEnd as mainStart (main includes all code from {entryStubEnd} to last RETN)");
                    }
                }
                else
                {
                    // entryJsrTarget is invalid, points to globals, or points to last RETN before entry stub - use entryStubEnd
                    mainStart = entryStubEnd;
                    if (entryJsrTargetIsLastRetn)
                    {
                        Debug($"DEBUG NcsToAstConverter: entryJsrTarget {entryJsrTarget} points to last RETN before entry stub, using entryStubEnd {entryStubEnd} as mainStart");
                    }
                    else
                    {
                        Debug($"DEBUG NcsToAstConverter: entryJsrTarget {entryJsrTarget} invalid or in globals range, using entryStubEnd {entryStubEnd} as mainStart");
                    }
                }
            }
            else
            {
                // No SAVEBP - no globals, main starts at 0 or entryJsrTarget
                if (entryJsrTarget >= 0 && entryJsrTarget > 0)
                {
                    mainStart = entryJsrTarget;
                }
            }

            // Only create main subroutine if mainStart is valid and after globals
            // If mainStart is 0 or within globals range, main should be empty
            int globalsEndForMain = (savebpIndex >= 0) ? savebpIndex + 1 : 0;
            if (savebpIndex >= 0)
            {
                // Check for entry stub and adjust globalsEndForMain
                int entryStubCheck = globalsEndForMain;
                if (instructions.Count > entryStubCheck + 1 &&
                    instructions[entryStubCheck].InsType == NCSInstructionType.JSR &&
                    instructions[entryStubCheck + 1].InsType == NCSInstructionType.RETN)
                {
                    globalsEndForMain = entryStubCheck + 2; // JSR + RETN
                }
                else if (instructions.Count > entryStubCheck + 1 &&
                         instructions[entryStubCheck].InsType == NCSInstructionType.JSR &&
                         instructions[entryStubCheck + 1].InsType == NCSInstructionType.RESTOREBP)
                {
                    globalsEndForMain = entryStubCheck + 2; // JSR + RESTOREBP
                }
            }

            // CRITICAL: Don't adjust mainStart here if we might need to split globals later
            // The split logic (which runs later) will set mainStart correctly if main code is in globals
            // Only adjust mainStart if we're NOT deferring globals (normal case where main is after globals)
            // If shouldDeferGlobals is true, it means we detected ACTION instructions in globals, so we'll split later
            bool isAlternativeMainStart = (alternativeMainStart >= 0 && mainStart == alternativeMainStart);
            int mainStartBeforeAdjustment = mainStart;

            // Only adjust if we're NOT deferring globals (normal case)
            // If shouldDeferGlobals is true, the split logic will handle mainStart adjustment
            if (!shouldDeferGlobals && mainStart <= globalsEndForMain && !isAlternativeMainStart)
            {
                mainStart = globalsEndForMain;
                Debug($"DEBUG NcsToAstConverter: Final adjustment: mainStart set to {mainStart} (after globals/entry stub at {globalsEndForMain}, was {mainStartBeforeAdjustment})");
            }
            else if (isAlternativeMainStart)
            {
                Debug($"DEBUG NcsToAstConverter: Keeping alternative mainStart {mainStart} (in globals range, entry JSR targets last RETN)");
            }
            else if (shouldDeferGlobals)
            {
                Debug($"DEBUG NcsToAstConverter: Deferring mainStart adjustment (shouldDeferGlobals=true, split logic will set mainStart correctly)");
            }

            // Calculate mainEnd - CRITICAL: Main function should include ALL instructions from mainStart
            // to the last RETN (instructions.Count), not just up to the first subroutine start.
            // Subroutines are separate functions called from main, but they don't truncate the main function.
            // NOTE: If mainStart is in globals range (alternative main start), mainEnd will be updated
            // in the split globals logic below to include SAVEBP
            // CRITICAL FIX: Always set mainEnd to instructions.Count initially - it will be updated by split logic if needed
            // The previous code set mainEnd = savebpIndex for alternative main start, which was WRONG and caused
            // instructions after SAVEBP to be missed (e.g., k_act_com41.nss has 121 instructions but only 88 were processed)
            mainEnd = instructions.Count;
            Debug($"DEBUG NcsToAstConverter: mainEnd INITIALLY set to {mainEnd} (all {instructions.Count} instructions from mainStart={mainStart} to last RETN)");
            if (isAlternativeMainStart && savebpIndex >= 0)
            {
                // Alternative main start is in globals range - split logic below will update mainEnd if needed
                // But we still want mainEnd to include ALL instructions, not just up to SAVEBP
                Debug($"DEBUG NcsToAstConverter: Alternative main start detected (mainStart={mainStart} in globals range), mainEnd={mainEnd} will be updated by split logic if needed");
            }
            if (subroutineStarts.Count > 0)
            {
                List<int> sortedSubStarts = new List<int>(subroutineStarts);
                sortedSubStarts.Sort();
                Debug($"DEBUG NcsToAstConverter: Found {subroutineStarts.Count} subroutine starts: {string.Join(", ", sortedSubStarts)} (these are separate functions, not boundaries for main)");
            }

            // If we deferred globals creation (entry JSR targets last RETN), create it now
            // Also check if main code is in globals even when globals were created normally
            // Split globals if mainStart is in the globals range
            if (savebpIndex >= 0)
            {
                // Calculate entryStubEnd (same logic as earlier in the function)
                int globalsEnd = savebpIndex + 1;
                int entryStubEnd = globalsEnd;
                // Check for entry stub pattern at savebpIndex+1
                if (instructions.Count > entryStubEnd + 1 &&
                    instructions[entryStubEnd].InsType == NCSInstructionType.JSR &&
                    instructions[entryStubEnd + 1].InsType == NCSInstructionType.RETN)
                {
                    entryStubEnd = entryStubEnd + 2; // JSR + RETN
                }
                else if (instructions.Count > entryStubEnd + 1 &&
                         instructions[entryStubEnd].InsType == NCSInstructionType.JSR &&
                         instructions[entryStubEnd + 1].InsType == NCSInstructionType.RESTOREBP)
                {
                    entryStubEnd = entryStubEnd + 2; // JSR + RESTOREBP
                }

                // If globals were created normally OR deferred, check if we need to split them
                // This handles cases where main code is in the globals range
                Debug($"DEBUG NcsToAstConverter: Checking if we need to split globals - shouldDeferGlobals={shouldDeferGlobals}, savebpIndex={savebpIndex}, mainStart={mainStart}, entryStubEnd={entryStubEnd}");

                // Check if main code is in globals range - this applies to BOTH normal and deferred cases
                // When entry JSR targets last RETN, main code is often in the globals range (0 to SAVEBP)
                bool mainCodeInGlobals = false;
                bool entryJsrTargetIsLastRetnCheck = (entryJsrTarget >= 0 && entryJsrTarget == instructions.Count - 1);

                // First, scan the globals range (0 to SAVEBP) to see what instruction types are there
                Debug($"DEBUG NcsToAstConverter: Scanning globals range (0-{savebpIndex}) for instruction types");
                int actionCountInGlobals = 0;
                for (int i = 0; i <= savebpIndex && i < instructions.Count; i++)
                {
                    if (instructions[i].InsType == NCSInstructionType.ACTION)
                    {
                        actionCountInGlobals++;
                        if (actionCountInGlobals <= 5) // Log first 5 ACTION instructions
                        {
                            Debug($"DEBUG NcsToAstConverter: Found ACTION instruction at index {i} in globals range (0-{savebpIndex})");
                        }
                    }
                }
                Debug($"DEBUG NcsToAstConverter: Total ACTION instructions in globals range (0-{savebpIndex}): {actionCountInGlobals}");

                // CRITICAL FIX: Always check if main code is in globals range when SAVEBP is very late
                // This handles files where SAVEBP is near the end and all main code is before SAVEBP
                // The condition should check if there are ACTION instructions in globals AND no ACTION after SAVEBP
                // CRITICAL: Always check this - if shouldDeferGlobals is true, it means we detected ACTION instructions in globals
                // Also check if actionCountInGlobals > 0 (which means main code is definitely in globals)
                bool shouldCheckMainInGlobals = true; // Always check - we need to detect when main code is in globals

                if (shouldCheckMainInGlobals)
                {
                    // Check if main code is actually in the globals range (0 to SAVEBP)
                    // If mainStart is at or after entryStubEnd and there are no ACTION instructions after SAVEBP+1,
                    // the main code must be in the globals range
                    Debug($"DEBUG NcsToAstConverter: Checking if main code is in globals - entryJsrTarget={entryJsrTarget}, instructions.Count-1={instructions.Count - 1}, entryJsrTargetIsLastRetnCheck={entryJsrTargetIsLastRetnCheck}, mainStart={mainStart}, entryStubEnd={entryStubEnd}, savebpIndex={savebpIndex}, actionCountInGlobals={actionCountInGlobals}, shouldCheckMainInGlobals={shouldCheckMainInGlobals}");

                    // Check if there are ACTION instructions in the range from SAVEBP+1 to last RETN
                    int actionCount = 0;
                    int checkStart = savebpIndex + 1;
                    for (int i = checkStart; i < instructions.Count - 1; i++)
                    {
                        if (instructions[i].InsType == NCSInstructionType.ACTION)
                        {
                            actionCount++;
                            Debug($"DEBUG NcsToAstConverter: Found ACTION instruction at index {i} in range {checkStart} to {instructions.Count - 1}");
                        }
                    }

                    // CRITICAL: If there are ACTION instructions in globals but none after SAVEBP, main code is in globals
                    // Also check if shouldDeferGlobals is true (which means actionCountInGlobalsForDefer > 0 was detected)
                    // CRITICAL FIX: Always check this condition, not just when shouldCheckMainInGlobals is true
                    // This ensures we detect when main code is in globals even when shouldDeferGlobals is true
                    Debug($"DEBUG NcsToAstConverter: Checking split condition - actionCount={actionCount}, actionCountInGlobals={actionCountInGlobals}, shouldDeferGlobals={shouldDeferGlobals}");
                    Console.Error.WriteLine($"DEBUG NcsToAstConverter: Checking split condition - actionCount={actionCount}, actionCountInGlobals={actionCountInGlobals}, shouldDeferGlobals={shouldDeferGlobals}, condition={actionCount == 0 && (actionCountInGlobals > 0 || shouldDeferGlobals)}");
                    if (actionCount == 0 && (actionCountInGlobals > 0 || shouldDeferGlobals))
                    {
                        // No ACTION instructions between SAVEBP+1 and last RETN
                        // Main function code must be in the globals range (0 to SAVEBP) - need to split
                        int mainCodeStartInGlobals = -1;
                        for (int i = 0; i <= savebpIndex; i++)
                        {
                            if (instructions[i].InsType == NCSInstructionType.ACTION)
                            {
                                mainCodeStartInGlobals = i;
                                Debug($"DEBUG NcsToAstConverter: Found first ACTION instruction in globals range at index {i}");
                                break;
                            }
                        }
                        if (mainCodeStartInGlobals >= 0)
                        {
                            mainCodeInGlobals = true;
                            Debug($"DEBUG NcsToAstConverter: No ACTION instructions found between SAVEBP+1 ({checkStart}) and last RETN ({instructions.Count - 1}), but found ACTION at {mainCodeStartInGlobals} in globals range (0-{savebpIndex}) - will split globals at {mainCodeStartInGlobals}");
                            Console.Error.WriteLine($"DEBUG NcsToAstConverter: SPLIT LOGIC RUNNING - mainCodeStartInGlobals={mainCodeStartInGlobals}, will set mainStart to this value");
                        }
                        else
                        {
                            // CRITICAL FIX: No ACTION instructions found ANYWHERE in the file!
                            // This happens for scripts that only include other files with globals, but have an empty main().
                            // Example: k_act_com41.nss has #include statements that bring in globals, but main() is empty (all code commented out)
                            // In this case:
                            // - All instructions from 0 to savebpIndex are global variable declarations (RSADDI, CONSTI, NEGI, etc.)
                            // - Instructions from savebpIndex+1 to end are the empty main() (just SAVEBP + entry stub + RETN)
                            // CRITICAL: Even though main() is "empty" (no ACTION), it still contains ALL instructions from SAVEBP+1 to the end
                            // This includes entry stub (JSR+RETN), cleanup code (MOVSP+RETN+RETN), and any other instructions
                            Console.Error.WriteLine($"DEBUG NcsToAstConverter: EMPTY MAIN CASE - No ACTION instructions found anywhere! actionCount={actionCount}, actionCountInGlobals={actionCountInGlobals}, shouldDeferGlobals={shouldDeferGlobals}");
                            Debug($"DEBUG NcsToAstConverter: EMPTY MAIN CASE - No ACTION instructions found anywhere!");
                            Debug($"DEBUG NcsToAstConverter: This is a script with globals only and an empty main(). Creating globals subroutine and empty main.");
                            Debug($"DEBUG NcsToAstConverter: EMPTY MAIN CASE - instructions.Count={instructions.Count}, savebpIndex={savebpIndex}, will create main from {savebpIndex + 1} to {instructions.Count}");

                            // Create globals subroutine with all instructions up to SAVEBP
                            if (savebpIndex >= 0)
                            {
                                int globalsInitEnd = savebpIndex + 1; // Include all instructions up to SAVEBP
                                ASubroutine globalsSub = ConvertInstructionRangeToSubroutine(ncs, instructions, 0, globalsInitEnd, 0);
                                if (globalsSub != null)
                                {
                                    program.GetSubroutine().Add(globalsSub);
                                    Debug($"DEBUG NcsToAstConverter: Created globals subroutine for EMPTY MAIN case (range 0-{globalsInitEnd}, {globalsInitEnd} instructions)");
                                }

                                // Set mainStart and mainEnd for the empty main function
                                // CRITICAL FIX: The main function starts right after SAVEBP and goes to the END (instructions.Count)
                                // Even if main() is "empty" (no ACTION), it still contains entry stub, cleanup code, and RETN instructions
                                // We MUST include ALL instructions from SAVEBP+1 to the end, not just a few
                                mainStart = savebpIndex + 1;
                                mainEnd = instructions.Count; // CRITICAL: Must be instructions.Count, not a smaller value!
                                mainCodeInGlobals = true; // Mark as handled so we don't fall into other branches
                                Debug($"DEBUG NcsToAstConverter: Set mainStart={mainStart}, mainEnd={mainEnd} for EMPTY MAIN case (main includes ALL {mainEnd - mainStart} instructions from SAVEBP+1 to end)");
                                Console.Error.WriteLine($"DEBUG NcsToAstConverter: EMPTY MAIN CASE - mainStart={mainStart}, mainEnd={mainEnd}, instructions.Count={instructions.Count}, main will include {mainEnd - mainStart} instructions");
                            }
                            else
                            {
                                Debug($"DEBUG NcsToAstConverter: WARNING - savebpIndex={savebpIndex}, cannot create globals subroutine!");
                            }
                        }
                        if (mainCodeStartInGlobals >= 0)
                        {

                            // Remove the globals subroutine that was created earlier (it includes main code)
                            // We'll recreate it with the correct split
                            var subroutines = program.GetSubroutine();
                            for (int i = subroutines.Count - 1; i >= 0; i--)
                            {
                                var s = subroutines[i];
                                if (s is ASubroutine)
                                {
                                    var aSub = (ASubroutine)s;
                                    if (aSub.GetId() == 0)
                                    {
                                        subroutines.RemoveAt(i);
                                    }
                                }
                            }
                            Debug($"DEBUG NcsToAstConverter: Removed globals subroutine(s) that included main code");

                            // Create split globals
                            // CRITICAL: Globals must include ALL instructions up to SAVEBP, not just up to first ACTION
                            // This ensures RSADDI and other instructions before SAVEBP are included in globals
                            // Globals: 0 to savebpIndex+1 (includes all variable initialization up to SAVEBP)
                            // Main: mainCodeStartInGlobals to last RETN (includes main code and everything after)
                            int globalsInitEnd = savebpIndex + 1; // Always include all instructions up to SAVEBP
                            ASubroutine globalsSub = ConvertInstructionRangeToSubroutine(ncs, instructions, 0, globalsInitEnd, 0);
                            if (globalsSub != null)
                            {
                                program.GetSubroutine().Add(globalsSub);
                                Debug($"DEBUG NcsToAstConverter: Created split globals subroutine (range 0-{globalsInitEnd}, includes all instructions up to SAVEBP, first ACTION at {mainCodeStartInGlobals})");
                            }

                            // Update mainStart to include the main function code (from first ACTION to last RETN)
                            mainStart = mainCodeStartInGlobals;
                            mainEnd = instructions.Count;
                            Debug($"DEBUG NcsToAstConverter: Updated mainStart to {mainStart} and mainEnd to {mainEnd} (main includes all code from first ACTION at {mainStart} to last RETN)");
                            Console.Error.WriteLine($"DEBUG NcsToAstConverter: SPLIT LOGIC SET mainStart={mainStart}, mainEnd={mainEnd}");
                        }
                    }
                }

                // CRITICAL FIX: Only create deferred globals if mainCodeInGlobals is false
                // If mainCodeInGlobals is true, the split logic above already created the globals and set mainStart/mainEnd
                if (shouldDeferGlobals && !mainCodeInGlobals && mainStart < savebpIndex + 1 && mainStart > 0)
                {
                    // Split globals:
                    // CRITICAL: Globals must include ALL instructions up to SAVEBP, not just up to mainStart
                    // This ensures RSADDI and other instructions before SAVEBP are included in globals
                    // - Globals: 0 to savebpIndex+1 (includes all variable initialization up to SAVEBP)
                    // - Main: mainStart to last RETN (includes main code and everything after)
                    // NOTE: There will be overlap (mainStart to SAVEBP), but that's handled by the main function
                    int globalsInitEnd = savebpIndex + 1; // Always include all instructions up to SAVEBP
                    ASubroutine globalsSub = ConvertInstructionRangeToSubroutine(ncs, instructions, 0, globalsInitEnd, 0);
                    if (globalsSub != null)
                    {
                        program.GetSubroutine().Add(globalsSub);
                        Debug($"DEBUG NcsToAstConverter: Created split globals subroutine (range 0-{globalsInitEnd}, includes all instructions up to SAVEBP, mainStart={mainStart})");
                    }
                    // Update mainEnd to include all instructions up to the last RETN
                    // CRITICAL: mainEnd must be instructions.Count to include ALL instructions, not just up to SAVEBP+1
                    mainEnd = instructions.Count;
                    Debug($"DEBUG NcsToAstConverter: Updated mainEnd to {mainEnd} (all {instructions.Count} instructions, includes main code from {mainStart} to last RETN)");
                }
                else
                {
                    // Main start is at or after entry stub end
                    // CRITICAL: When entry JSR targets last RETN and mainStart = entryStubEnd,
                    // the main function code might actually be in the globals range (before entryStubEnd)
                    // Check if there's actual code between entryStubEnd and the last RETN
                    // If not (just MOVSP/RSADDI/RETN), the main code is in globals and we need to split
                    bool mainCodeInGlobalsInner = false;
                    bool entryJsrTargetIsLastRetnCheckInner = (entryJsrTarget >= 0 && entryJsrTarget == instructions.Count - 1);
                    Debug($"DEBUG NcsToAstConverter: Checking if main code is in globals - entryJsrTarget={entryJsrTarget}, instructions.Count-1={instructions.Count - 1}, entryJsrTargetIsLastRetnCheck={entryJsrTargetIsLastRetnCheckInner}, mainStart={mainStart}, entryStubEnd={entryStubEnd}, globalsEndForMain={globalsEndForMain}, savebpIndex={savebpIndex}, mainStartBeforeAdjustment={mainStartBeforeAdjustment}");
                    // Check if entry JSR targets last RETN AND mainStart is at or after entryStubEnd
                    // AND there are no ACTION instructions after SAVEBP+1
                    // This means the main code might be in the globals range
                    if (entryJsrTargetIsLastRetnCheckInner && mainStart >= entryStubEnd && savebpIndex >= 0)
                    {
                        // Check if there are ACTION instructions in the range from SAVEBP+1 to last RETN
                        // This includes the entry stub area and everything up to the last RETN
                        // If there are no ACTION instructions in this entire range, the main code must be in the globals range (0 to SAVEBP)
                        int actionCount = 0;
                        int checkStart = savebpIndex + 1; // Start checking from right after SAVEBP
                        for (int i = checkStart; i < instructions.Count - 1; i++)
                        {
                            if (instructions[i].InsType == NCSInstructionType.ACTION)
                            {
                                actionCount++;
                                Debug($"DEBUG NcsToAstConverter: Found ACTION instruction at index {i} in range {checkStart} to {instructions.Count - 1}");
                            }
                        }
                        if (actionCount == 0)
                        {
                            // No ACTION instructions between SAVEBP+1 and last RETN
                            // Main function code must be in the globals range (0 to SAVEBP) - need to split
                            // Find where the main code actually starts by looking for the first ACTION instruction in the 0 to SAVEBP range
                            int mainCodeStartInGlobals = -1;
                            for (int i = 0; i <= savebpIndex; i++)
                            {
                                if (instructions[i].InsType == NCSInstructionType.ACTION)
                                {
                                    mainCodeStartInGlobals = i;
                                    Debug($"DEBUG NcsToAstConverter: Found first ACTION instruction in globals range at index {i}");
                                    break;
                                }
                            }
                            if (mainCodeStartInGlobals >= 0)
                            {
                                mainCodeInGlobalsInner = true;
                                Debug($"DEBUG NcsToAstConverter: No ACTION instructions found between SAVEBP+1 ({checkStart}) and last RETN ({instructions.Count - 1}), but found ACTION at {mainCodeStartInGlobals} in globals range (0-{savebpIndex}) - will split globals at {mainCodeStartInGlobals}");
                            }
                            else
                            {
                                Debug($"DEBUG NcsToAstConverter: No ACTION instructions found anywhere - file may be empty or malformed");
                            }
                        }
                        else
                        {
                            Debug($"DEBUG NcsToAstConverter: Found {actionCount} ACTION instructions between SAVEBP+1 ({checkStart}) and last RETN ({instructions.Count - 1}), main code is after SAVEBP");
                        }
                    }

                    // CRITICAL FIX: Don't run duplicate split logic if mainCodeInGlobals is already true
                    // The split logic at line 698 should have already handled this case
                    if (mainCodeInGlobalsInner && !mainCodeInGlobals)
                    {
                        // Main function code is in globals range - split globals
                        // Find where the main code actually starts in the 0 to SAVEBP range
                        int mainCodeStartInGlobals = -1;
                        for (int i = 0; i <= savebpIndex; i++)
                        {
                            if (instructions[i].InsType == NCSInstructionType.ACTION)
                            {
                                mainCodeStartInGlobals = i;
                                break;
                            }
                        }

                        if (mainCodeStartInGlobals >= 0)
                        {
                            // Split at the first ACTION instruction
                            // CRITICAL: Globals must include ALL instructions up to SAVEBP, not just up to first ACTION
                            // This ensures RSADDI and other instructions before SAVEBP are included in globals
                            // Globals: 0 to savebpIndex+1 (includes all variable initialization up to SAVEBP)
                            // Main: mainCodeStartInGlobals to last RETN (includes main code and everything after)
                            // NOTE: There will be overlap (mainCodeStartInGlobals to SAVEBP), but that's handled by the main function
                            int globalsInitEnd = savebpIndex + 1; // Always include all instructions up to SAVEBP

                            // Create globals subroutine (includes all variable initialization up to SAVEBP)
                            ASubroutine globalsSub = ConvertInstructionRangeToSubroutine(ncs, instructions, 0, globalsInitEnd, 0);
                            if (globalsSub != null)
                            {
                                program.GetSubroutine().Add(globalsSub);
                                Debug($"DEBUG NcsToAstConverter: Created split globals subroutine (range 0-{globalsInitEnd}, includes all instructions up to SAVEBP, first ACTION at {mainCodeStartInGlobals})");
                            }

                            // Update mainStart to include the main function code (from first ACTION to last RETN)
                            mainStart = mainCodeStartInGlobals;
                            mainEnd = instructions.Count; // Include all instructions from first ACTION to last RETN
                            Debug($"DEBUG NcsToAstConverter: Updated mainStart to {mainStart} and mainEnd to {mainEnd} (main includes all code from first ACTION at {mainStart} to last RETN)");
                            mainCodeInGlobals = true; // Mark that split logic has run
                        }
                        else
                        {
                            // No ACTION instructions found in globals range either - use SAVEBP+1 as split point
                            int globalsInitEnd = savebpIndex + 1;
                            ASubroutine globalsSub = ConvertInstructionRangeToSubroutine(ncs, instructions, 0, globalsInitEnd, 0);
                            if (globalsSub != null)
                            {
                                program.GetSubroutine().Add(globalsSub);
                                Debug($"DEBUG NcsToAstConverter: Created split globals subroutine (range 0-{globalsInitEnd}, no ACTION found, using SAVEBP+1 as split)");
                            }
                            mainStart = globalsInitEnd;
                            mainEnd = instructions.Count;
                            Debug($"DEBUG NcsToAstConverter: Updated mainStart to {mainStart} and mainEnd to {mainEnd} (no ACTION in globals, using SAVEBP+1 as main start)");
                        }
                    }
                    else if (!mainCodeInGlobals)
                    {
                        // CRITICAL: Only create globals subroutine if mainCodeInGlobals is false
                        // If mainCodeInGlobals is true, the EMPTY MAIN case (or earlier split logic) already created it

                        // Main start is after globals - create normal globals subroutine
                        // Globals subroutine ends at SAVEBP+1 (includes SAVEBP and entry stub up to but not including main)
                        // For files like asd.nss where main is at the last RETN, globals includes everything up to entry stub end
                        int globalsSubEnd = savebpIndex + 1;
                        // If entry stub exists, extend globals to include it (but not main)
                        int entryStubCheck = globalsSubEnd;
                        if (instructions.Count > entryStubCheck + 1 &&
                            instructions[entryStubCheck].InsType == NCSInstructionType.JSR &&
                            (instructions[entryStubCheck + 1].InsType == NCSInstructionType.RETN ||
                             instructions[entryStubCheck + 1].InsType == NCSInstructionType.RESTOREBP))
                        {
                            // Entry stub exists - extend globals to include SAVEBP + entry stub pattern
                            // But NOT the cleanup code after entry stub (MOVSP+RETN before main)
                            globalsSubEnd = entryStubCheck + 2; // SAVEBP + JSR + RETN/RESTOREBP
                            Debug($"DEBUG NcsToAstConverter: Extended globals to include entry stub, globalsSubEnd={globalsSubEnd}");
                        }
                        ASubroutine globalsSub = ConvertInstructionRangeToSubroutine(ncs, instructions, 0, globalsSubEnd, 0);
                        if (globalsSub != null)
                        {
                            program.GetSubroutine().Add(globalsSub);
                            Debug($"DEBUG NcsToAstConverter: Created globals subroutine (range 0-{globalsSubEnd})");
                        }
                    }
                    else
                    {
                        Debug($"DEBUG NcsToAstConverter: Skipping globals subroutine creation - mainCodeInGlobals=true (already handled)");
                    }
                }
            }

            // CRITICAL: Ensure mainEnd always includes all instructions when there are no subroutines after main
            // This prevents missing instructions at the end of the main function (e.g., RSADDI before final RETN)
            Debug($"DEBUG NcsToAstConverter: BEFORE final mainEnd check - mainStart={mainStart}, mainEnd={mainEnd}, instructions.Count={instructions.Count}, subroutineStarts.Count={subroutineStarts.Count}");
            if (subroutineStarts.Count == 0 || !subroutineStarts.Any(subStart => subStart > mainStart))
            {
                // No subroutines after main - main should include ALL instructions up to the last RETN
                if (mainEnd < instructions.Count)
                {
                    Debug($"DEBUG NcsToAstConverter: WARNING - mainEnd ({mainEnd}) < instructions.Count ({instructions.Count}), correcting to include all instructions");
                    Console.Error.WriteLine($"DEBUG NcsToAstConverter: CRITICAL FIX - mainEnd was {mainEnd}, correcting to {instructions.Count} to include all instructions");
                    mainEnd = instructions.Count;
                }
            }
            Debug($"DEBUG NcsToAstConverter: AFTER final mainEnd check - mainStart={mainStart}, mainEnd={mainEnd}, instructions.Count={instructions.Count}, main will process {mainEnd - mainStart} instructions");
            Console.Error.WriteLine($"DEBUG NcsToAstConverter: FINAL mainEnd={mainEnd}, mainStart={mainStart}, will process instructions {mainStart} to {mainEnd - 1} (inclusive)");

            // CRITICAL FIX: Check if RESTOREBP right before mainStart is actually cleanup code at the end of main
            // If RESTOREBP is followed by MOVSP+RETN+RETN at the end of the file, it's cleanup code, not entry stub
            // In that case, include it in the main function by adjusting mainStart backward
            if (mainStart > 0 && mainStart < instructions.Count)
            {
                int prevIdx = mainStart - 1;
                if (prevIdx >= 0 && instructions[prevIdx].InsType == NCSInstructionType.RESTOREBP)
                {
                    // Check if this RESTOREBP is followed by cleanup pattern (MOVSP+RETN+RETN) at the end
                    bool isCleanupCode = false;
                    if (mainStart + 2 < instructions.Count)
                    {
                        // Check if we have MOVSP, RETN, RETN pattern after RESTOREBP
                        if (instructions[mainStart].InsType == NCSInstructionType.MOVSP &&
                            instructions[mainStart + 1].InsType == NCSInstructionType.RETN &&
                            instructions[mainStart + 2].InsType == NCSInstructionType.RETN)
                        {
                            // This is cleanup code at the end - check if it's near the end of the file
                            // (within last 3 instructions: RESTOREBP, MOVSP, RETN, RETN)
                            if (mainStart + 2 >= instructions.Count - 1)
                            {
                                isCleanupCode = true;
                                Debug($"DEBUG NcsToAstConverter: RESTOREBP at index {prevIdx} is cleanup code (followed by MOVSP+RETN+RETN at end), including in main");
                            }
                        }
                    }
                    else if (mainStart + 1 < instructions.Count)
                    {
                        // Check if we have MOVSP, RETN pattern (might be only 2 instructions at end)
                        if (instructions[mainStart].InsType == NCSInstructionType.MOVSP &&
                            instructions[mainStart + 1].InsType == NCSInstructionType.RETN &&
                            mainStart + 1 >= instructions.Count - 1)
                        {
                            isCleanupCode = true;
                            Debug($"DEBUG NcsToAstConverter: RESTOREBP at index {prevIdx} is cleanup code (followed by MOVSP+RETN at end), including in main");
                        }
                    }

                    if (isCleanupCode)
                    {
                        // Include RESTOREBP in main function
                        // CRITICAL: Don't override mainStart if it was set by split logic
                        // If mainStart is in globals range (before SAVEBP), it was set by split logic
                        bool mainStartSetBySplit = (savebpIndex >= 0 && mainStart < savebpIndex + 1 && mainStart > 0);
                        if (!mainStartSetBySplit)
                        {
                            mainStart = prevIdx;
                            Debug($"DEBUG NcsToAstConverter: Adjusted mainStart to {mainStart} to include RESTOREBP cleanup code");
                        }
                        else
                        {
                            Debug($"DEBUG NcsToAstConverter: Skipping RESTOREBP adjustment (mainStart={mainStart} is in globals range, split logic already set it correctly)");
                        }
                    }
                }
            }

            // Only create main if it has valid range AND this is NOT an include file
            // Include files have no main function - they're collections of utility functions
            Debug($"DEBUG NcsToAstConverter: Checking main subroutine creation - mainStart={mainStart}, mainEnd={mainEnd}, mainStart < mainEnd={mainStart < mainEnd}, mainStart >= 0={mainStart >= 0}, isIncludeFile={isIncludeFile}, instructions.Count={instructions.Count}");
            Console.Error.WriteLine($"DEBUG NcsToAstConverter: FINAL CHECK - mainStart={mainStart}, mainEnd={mainEnd}, mainStart < mainEnd={mainStart < mainEnd}, mainStart >= 0={mainStart >= 0}, isIncludeFile={isIncludeFile}, instructions.Count={instructions.Count}");
            if (!isIncludeFile && mainStart < mainEnd && mainStart >= 0)
            {
                Console.Error.WriteLine($"DEBUG NcsToAstConverter: Creating main subroutine, range={mainStart}-{mainEnd}, total instructions={instructions.Count}");
                Debug($"DEBUG NcsToAstConverter: Creating main subroutine, range={mainStart}-{mainEnd}, total instructions={instructions.Count}");
                // Log instruction types in main range
                for (int i = mainStart; i < Math.Min(mainEnd, instructions.Count) && i < mainStart + 20; i++)
                {
                    Console.Error.WriteLine($"DEBUG NcsToAstConverter: Main instruction[{i}]={instructions[i].InsType}");
                    Debug($"DEBUG NcsToAstConverter: Main instruction[{i}]={instructions[i].InsType}");
                }
                ASubroutine mainSub = ConvertInstructionRangeToSubroutine(ncs, instructions, mainStart, mainEnd, mainStart, (byte)entryReturnType);
                if (mainSub != null)
                {
                    // Check if main subroutine has commands
                    var cmdBlock = mainSub.GetCommandBlock();
                    int cmdCount = 0;
                    if (cmdBlock is AST.ACommandBlock aCmdBlock)
                    {
                        cmdCount = aCmdBlock.GetCmd().Count;
                    }
                    Debug($"DEBUG NcsToAstConverter: Main subroutine created with {cmdCount} commands, subId={mainSub.GetId()}");
                    Console.Error.WriteLine($"DEBUG NcsToAstConverter: Main subroutine created with {cmdCount} commands, subId={mainSub.GetId()}");
                    program.GetSubroutine().Add(mainSub);
                    Debug($"DEBUG NcsToAstConverter: Main subroutine added to program, total subroutines now: {program.GetSubroutine().Count}");
                }
                else
                {
                    Debug($"DEBUG NcsToAstConverter: WARNING - Main subroutine creation returned null for range {mainStart}-{mainEnd}");
                    Console.Error.WriteLine($"DEBUG NcsToAstConverter: WARNING - Main subroutine creation returned null for range {mainStart}-{mainEnd}");
                }
            }
            else if (isIncludeFile)
            {
                Debug($"DEBUG NcsToAstConverter: Skipping main subroutine creation for INCLUDE FILE - functions will be created from SAVEBP positions");
            }
            else
            {
                Debug($"DEBUG NcsToAstConverter: Skipping main subroutine creation - mainStart={mainStart}, mainEnd={mainEnd}, globalsEnd={globalsEndForMain}");
            }

            // CRITICAL FIX FOR INCLUDE FILES:
            // If this is an include file (detected earlier), create subroutines from each SAVEBP position
            // instead of trying to find a main function
            if (isIncludeFile && allSavebpIndices.Count > 0)
            {
                Debug($"DEBUG NcsToAstConverter: Processing INCLUDE FILE with {allSavebpIndices.Count} functions (each SAVEBP is a function start)");

                // For include files, each SAVEBP marks the start of a function
                // Functions end at RETN or the next SAVEBP
                for (int funcIdx = 0; funcIdx < allSavebpIndices.Count; funcIdx++)
                {
                    int funcStart = allSavebpIndices[funcIdx];
                    int funcEnd = instructions.Count; // Default to end of file

                    // Find the end of this function (next SAVEBP or RETN)
                    for (int i = funcStart + 1; i < instructions.Count; i++)
                    {
                        if (instructions[i].InsType == NCSInstructionType.RETN)
                        {
                            // Found RETN - this is the end of the function
                            funcEnd = i + 1; // Include the RETN
                            break;
                        }
                        // If we find another SAVEBP before RETN, the function ends here
                        // (This handles nested functions or unusual patterns)
                        if (allSavebpIndices.Contains(i) && i > funcStart)
                        {
                            funcEnd = i; // Exclude the next function's SAVEBP
                            break;
                        }
                    }

                    // Create a subroutine for this function
                    ASubroutine funcSub = ConvertInstructionRangeToSubroutine(
                        ncs,
                        instructions,
                        funcStart,
                        funcEnd,
                        program.GetSubroutine().Count);
                    if (funcSub != null)
                    {
                        program.GetSubroutine().Add(funcSub);
                        Debug($"DEBUG NcsToAstConverter: Created include file function {funcIdx} (range {funcStart}-{funcEnd}), total subs now: {program.GetSubroutine().Count}");
                    }
                    else
                    {
                        Debug($"DEBUG NcsToAstConverter: WARNING - Include file function {funcIdx} creation returned null (range {funcStart}-{funcEnd})");
                    }
                }

                // Skip the regular subroutine handling since we've processed all functions
                Debug($"DEBUG NcsToAstConverter: Include file processing complete, created {program.GetSubroutine().Count} subroutines");
            }
            else
            {
                // Regular handling for normal scripts (with main function)
                List<int> sortedStarts = new List<int>(subroutineStarts);
                sortedStarts.Sort();
                for (int idx = 0; idx < sortedStarts.Count; idx++)
                {
                    int subStart = sortedStarts[idx];
                    int subEnd = instructions.Count;
                    for (int i = subStart + 1; i < instructions.Count; i++)
                    {
                        if (subroutineStarts.Contains(i))
                        {
                            subEnd = i;
                            break;
                        }

                        if (instructions[i].InsType == NCSInstructionType.RETN)
                        {
                            subEnd = i + 1;
                            break;
                        }
                    }

                    ASubroutine sub = ConvertInstructionRangeToSubroutine(
                        ncs,
                        instructions,
                        subStart,
                        subEnd,
                        program.GetSubroutine().Count);
                    if (sub != null)
                    {
                        program.GetSubroutine().Add(sub);
                    }
                }
            }

            // CRITICAL: Ensure we always have at least one subroutine (main)
            // If no subroutines were created, create a main subroutine from the entire instruction range
            // This handles edge cases where the entry stub detection fails or files have unusual structure
            int subroutineCount = program.GetSubroutine().Count;
            Debug($"DEBUG NcsToAstConverter: Final subroutine count before fallback check: {subroutineCount}, instructions: {instructions.Count}");
            if (subroutineCount == 0 && instructions.Count > 0)
            {
                Debug("DEBUG NcsToAstConverter: No subroutines created, creating fallback main subroutine from entire instruction range");
                int fallbackMainStart = 0;
                int fallbackMainEnd = instructions.Count;
                // Skip globals if SAVEBP was found
                if (savebpIndex >= 0)
                {
                    fallbackMainStart = savebpIndex + 1;
                    // Check for entry stub
                    if (instructions.Count > fallbackMainStart + 1 &&
                        instructions[fallbackMainStart].InsType == NCSInstructionType.JSR &&
                        (instructions[fallbackMainStart + 1].InsType == NCSInstructionType.RETN ||
                         instructions[fallbackMainStart + 1].InsType == NCSInstructionType.RESTOREBP))
                    {
                        fallbackMainStart += 2; // Skip JSR + RETN/RESTOREBP
                    }
                }

                // CRITICAL FIX: If fallbackMainStart >= fallbackMainEnd, use the entire range
                // This handles very small files where the calculated start might be after the end
                if (fallbackMainStart >= fallbackMainEnd)
                {
                    Debug($"DEBUG NcsToAstConverter: Fallback start ({fallbackMainStart}) >= end ({fallbackMainEnd}), using entire range (0-{instructions.Count})");
                    fallbackMainStart = 0;
                    fallbackMainEnd = instructions.Count;
                }

                if (fallbackMainStart < fallbackMainEnd && fallbackMainStart >= 0 && fallbackMainEnd <= instructions.Count)
                {
                    ASubroutine fallbackMain = ConvertInstructionRangeToSubroutine(ncs, instructions, fallbackMainStart, fallbackMainEnd, fallbackMainStart);
                    if (fallbackMain != null)
                    {
                        program.GetSubroutine().Add(fallbackMain);
                        Debug($"DEBUG NcsToAstConverter: Created fallback main subroutine (range {fallbackMainStart}-{fallbackMainEnd})");
                    }
                    else
                    {
                        Debug($"DEBUG NcsToAstConverter: Fallback main subroutine creation returned null (range {fallbackMainStart}-{fallbackMainEnd})");
                        // Last resort: create an empty subroutine with just a RETN if we can find one
                        if (instructions.Count > 0)
                        {
                            Debug("DEBUG NcsToAstConverter: Attempting to create minimal subroutine as last resort");
                            AST.ASubroutine minimalSub = new AST.ASubroutine();
                            minimalSub.SetId(0);
                            AST.ACommandBlock minimalCmdBlock = new AST.ACommandBlock();
                            // Try to convert at least the last instruction (should be RETN)
                            for (int i = Math.Max(0, instructions.Count - 3); i < instructions.Count; i++)
                            {
                                PCmd cmd = ConvertInstructionToCmd(ncs, instructions[i], i, instructions);
                                if (cmd != null)
                                {
                                    minimalCmdBlock.AddCmd((AST.PCmd)(object)cmd);
                                }
                            }
                            minimalSub.SetCommandBlock(minimalCmdBlock);
                            // Find RETN and set it as return
                            for (int i = instructions.Count - 1; i >= 0 && i >= instructions.Count - 5; i--)
                            {
                                if (instructions[i].InsType == NCSInstructionType.RETN)
                                {
                                    AReturn ret = ConvertRetn(instructions[i], i);
                                    if (ret != null)
                                    {
                                        minimalSub.SetReturn((AST.PReturn)(object)ret);
                                    }
                                    break;
                                }
                            }
                            program.GetSubroutine().Add(minimalSub);
                            Debug("DEBUG NcsToAstConverter: Created minimal fallback subroutine");
                        }
                    }
                }
                else
                {
                    Debug($"DEBUG NcsToAstConverter: Fallback main subroutine range invalid (start={fallbackMainStart}, end={fallbackMainEnd}, instructions.Count={instructions.Count})");
                    // Last resort: create an empty subroutine
                    if (instructions.Count > 0)
                    {
                        Debug("DEBUG NcsToAstConverter: Creating empty subroutine as absolute last resort");
                        AST.ASubroutine emptySub = new AST.ASubroutine();
                        emptySub.SetId(0);
                        emptySub.SetCommandBlock(new AST.ACommandBlock());
                        program.GetSubroutine().Add(emptySub);
                        Debug("DEBUG NcsToAstConverter: Created empty fallback subroutine");
                    }
                }
            }

            return new Start(program, new EOF());
        }

        private static ASubroutine ConvertInstructionRangeToSubroutine(
            NCS ncs,
            List<NCSInstruction> instructions,
            int startIdx,
            int endIdx,
            int subId,
            byte returnType = 0)
        {
            if (startIdx >= endIdx || startIdx >= instructions.Count)
            {
                Debug($"DEBUG ConvertInstructionRangeToSubroutine: Invalid range - startIdx={startIdx}, endIdx={endIdx}, instructions.Count={instructions.Count}, returning null");
                return null;
            }

            AST.ASubroutine sub = new AST.ASubroutine();
            sub.SetId(subId);
            sub.SetReturnType(returnType); // Store return type for main function identification
            AST.ACommandBlock cmdBlock = new AST.ACommandBlock();

            int limit = Math.Min(endIdx, instructions.Count);
            int convertedCount = 0;
            int nullCount = 0;
            int actionCount = 0;
            int negiCount = 0;
            int constiCount = 0;
            
            // CRITICAL DEBUG: Log the range being processed with full context
            Debug($"DEBUG ConvertInstructionRangeToSubroutine: Processing range {startIdx} to {limit - 1} (inclusive), subId={subId}, instructions.Count={instructions.Count}");
            Console.Error.WriteLine($"DEBUG ConvertInstructionRangeToSubroutine: Processing range {startIdx} to {limit - 1} (inclusive), subId={subId}, instructions.Count={instructions.Count}");
            
            // Log first and last few instructions in the range to understand what we're processing
            if (startIdx < instructions.Count && limit > startIdx)
            {
                Debug($"DEBUG ConvertInstructionRangeToSubroutine: First instruction in range: index={startIdx}, type={instructions[startIdx]?.InsType}, offset={instructions[startIdx]?.Offset}");
                if (limit - 1 < instructions.Count)
                {
                    Debug($"DEBUG ConvertInstructionRangeToSubroutine: Last instruction in range: index={limit - 1}, type={instructions[limit - 1]?.InsType}, offset={instructions[limit - 1]?.Offset}");
                }
            }
            
            for (int i = startIdx; i < limit; i++)
            {
                if (instructions[i] == null)
                {
                    Debug($"DEBUG ConvertInstructionRangeToSubroutine: WARNING - Instruction at index {i} is null!");
                    nullCount++;
                    continue;
                }
                
                // Track critical instruction types
                if (instructions[i].InsType == NCSInstructionType.ACTION)
                {
                    actionCount++;
                    Console.Error.WriteLine($"DEBUG ConvertInstructionRangeToSubroutine: Found ACTION instruction at index {i}, offset={instructions[i].Offset}");
                    Debug($"DEBUG ConvertInstructionRangeToSubroutine: Found ACTION instruction at index {i}, offset={instructions[i].Offset}");
                }
                else if (instructions[i].InsType == NCSInstructionType.NEGI)
                {
                    negiCount++;
                    // Log NEGI instructions - they're critical for negative constants
                    Debug($"DEBUG ConvertInstructionRangeToSubroutine: Found NEGI instruction at index {i}, offset={instructions[i].Offset}");
                    Console.Error.WriteLine($"DEBUG ConvertInstructionRangeToSubroutine: Found NEGI instruction at index {i}, offset={instructions[i].Offset}");
                }
                else if (instructions[i].InsType == NCSInstructionType.CONSTI)
                {
                    constiCount++;
                    // Log CONSTI near NEGI to understand the pattern
                    if (i < limit - 1 && instructions[i + 1]?.InsType == NCSInstructionType.NEGI)
                    {
                        Debug($"DEBUG ConvertInstructionRangeToSubroutine: Found CONSTI at index {i} followed by NEGI at {i+1}");
                    }
                }
                
                PCmd cmd = ConvertInstructionToCmd(ncs, instructions[i], i, instructions);
                if (cmd != null)
                {
                    cmdBlock.AddCmd((AST.PCmd)(object)cmd);
                    convertedCount++;
                    // DEBUG: Log ACTION conversions, especially near the end
                    if (instructions[i].InsType == NCSInstructionType.ACTION && (i >= limit - 5 || i == limit - 1))
                    {
                        Debug($"DEBUG ConvertInstructionRangeToSubroutine: Successfully converted ACTION at index {i} (near end, limit={limit})");
                    }
                }
                else
                {
                    nullCount++;
                    // CRITICAL: Log if ACTION returns null (should never happen)
                    if (instructions[i].InsType == NCSInstructionType.ACTION)
                    {
                        Debug($"DEBUG ConvertInstructionRangeToSubroutine: ERROR - ACTION at index {i} returned null!");
                        Console.Error.WriteLine($"ERROR ConvertInstructionRangeToSubroutine: ACTION at index {i} returned null!");
                    }
                    // CRITICAL: Log if NEGI returns null (should never happen)
                    if (instructions[i].InsType == NCSInstructionType.NEGI)
                    {
                        Debug($"DEBUG ConvertInstructionRangeToSubroutine: ERROR - NEGI at index {i}, offset={instructions[i].Offset} returned null!");
                        Console.Error.WriteLine($"ERROR ConvertInstructionRangeToSubroutine: NEGI at index {i}, offset={instructions[i].Offset} returned null!");
                    }
                    if (nullCount <= 10) // Log first 10 null conversions
                    {
                        Debug($"DEBUG NcsToAstConverter: Instruction at index {i} ({instructions[i].InsType}, offset={instructions[i].Offset}) returned null");
                        Console.Error.WriteLine($"DEBUG NcsToAstConverter: Instruction at index {i} ({instructions[i].InsType}, offset={instructions[i].Offset}) returned null");
                    }
                }
            }
            Console.Error.WriteLine($"DEBUG ConvertInstructionRangeToSubroutine: Range {startIdx}-{limit}: Found {actionCount} ACTION, {negiCount} NEGI, {constiCount} CONSTI instructions");
            Debug($"DEBUG ConvertInstructionRangeToSubroutine: Range {startIdx}-{limit}: Found {actionCount} ACTION, {negiCount} NEGI, {constiCount} CONSTI instructions");
            Debug($"DEBUG NcsToAstConverter: Converted {convertedCount} commands, {nullCount} returned null (range {startIdx}-{limit})");

            sub.SetCommandBlock(cmdBlock);

            for (int i = startIdx; i < limit; i++)
            {
                if (instructions[i].InsType == NCSInstructionType.RETN)
                {
                    AReturn ret = ConvertRetn(instructions[i], i);
                    if (ret != null)
                    {
                        sub.SetReturn((AST.PReturn)(object)ret);
                    }

                    break;
                }
            }

            return sub;
        }

        private static PCmd ConvertInstructionToCmd(
            NCS ncs,
            NCSInstruction inst,
            int pos,
            List<NCSInstruction> instructions)
        {
            NCSInstructionType insType = inst.InsType;
            if (insType == NCSInstructionType.CONSTI ||
                insType == NCSInstructionType.CONSTF ||
                insType == NCSInstructionType.CONSTS ||
                insType == NCSInstructionType.CONSTO)
            {
                return ConvertConstCmd(inst, pos);
            }

            if (insType == NCSInstructionType.ACTION)
            {
                return ConvertActionCmd(inst, pos);
            }

            if (insType == NCSInstructionType.JMP || insType == NCSInstructionType.JSR)
            {
                return ConvertJumpCmd(ncs, inst, pos, instructions);
            }

            if (insType == NCSInstructionType.JZ || insType == NCSInstructionType.JNZ)
            {
                return ConvertConditionalJumpCmd(ncs, inst, pos, instructions);
            }

            if (insType == NCSInstructionType.RETN)
            {
                return ConvertRetnCmd(inst, pos);
            }

            if (insType == NCSInstructionType.CPDOWNSP || insType == NCSInstructionType.CPTOPSP)
            {
                return ConvertCopySpCmd(inst, pos);
            }

            if (insType == NCSInstructionType.CPDOWNBP || insType == NCSInstructionType.CPTOPBP)
            {
                return ConvertCopyBpCmd(inst, pos);
            }

            if (insType == NCSInstructionType.MOVSP)
            {
                return ConvertMovespCmd(inst, pos);
            }

            if (insType == NCSInstructionType.INCxSP ||
                insType == NCSInstructionType.DECxSP ||
                insType == NCSInstructionType.INCxBP ||
                insType == NCSInstructionType.DECxBP)
            {
                return ConvertStackOpCmd(inst, pos);
            }

            if (insType == NCSInstructionType.RSADDI ||
                insType == NCSInstructionType.RSADDF ||
                insType == NCSInstructionType.RSADDS ||
                insType == NCSInstructionType.RSADDO ||
                insType == NCSInstructionType.RSADDEFF ||
                insType == NCSInstructionType.RSADDEVT ||
                insType == NCSInstructionType.RSADDLOC ||
                insType == NCSInstructionType.RSADDTAL)
            {
                return ConvertRsaddCmd(inst, pos);
            }

            if (insType == NCSInstructionType.DESTRUCT)
            {
                return ConvertDestructCmd(inst, pos);
            }

            if (insType == NCSInstructionType.SAVEBP || insType == NCSInstructionType.RESTOREBP)
            {
                return ConvertBpCmd(inst, pos);
            }

            if (insType == NCSInstructionType.STORE_STATE)
            {
                return ConvertStoreStateCmd(inst, pos);
            }

            if (insType == NCSInstructionType.NOP ||
                insType == NCSInstructionType.NOP2 ||
                insType == NCSInstructionType.RESERVED ||
                insType == NCSInstructionType.RESERVED_01)
            {
                return null;
            }

            if (insType == NCSInstructionType.NEGI ||
                insType == NCSInstructionType.NEGF ||
                insType == NCSInstructionType.NOTI ||
                insType == NCSInstructionType.COMPI)
            {
                return ConvertUnaryCmd(inst, pos);
            }

            if (inst.IsArithmetic() || inst.IsComparison() || inst.IsBitwise())
            {
                return ConvertBinaryCmd(inst, pos);
            }

            if (inst.IsLogical() ||
                insType == NCSInstructionType.BOOLANDII ||
                insType == NCSInstructionType.INCORII ||
                insType == NCSInstructionType.EXCORII)
            {
                return ConvertLogiiCmd(inst, pos);
            }

            System.Diagnostics.Debug.WriteLine(
                "Unknown instruction type at position " + pos + ": " + insType +
                " (value: " + insType + ")");
            return null;
        }

        private static AConstCmd ConvertConstCmd(NCSInstruction inst, int pos)
        {
            AConstCmd constCmd = new AConstCmd();
            AConstCommand constCommand = new AConstCommand();

            constCommand.SetConst(new TConst(pos, 0));
            constCommand.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));

            // For constant instructions, the type should match the instruction type, not the qualifier
            // CONSTI = 3 (int), CONSTF = 4 (float), CONSTS = 5 (string), CONSTO = 6 (object)
            int typeVal;
            if (inst.InsType == NCSInstructionType.CONSTI)
            {
                typeVal = 3; // VT_INTEGER
            }
            else if (inst.InsType == NCSInstructionType.CONSTF)
            {
                typeVal = 4; // VT_FLOAT
            }
            else if (inst.InsType == NCSInstructionType.CONSTS)
            {
                typeVal = 5; // VT_STRING
            }
            else if (inst.InsType == NCSInstructionType.CONSTO)
            {
                typeVal = 6; // VT_OBJECT
            }
            else
            {
                // Fallback to qualifier for unknown constant types
                typeVal = GetQualifier(inst.InsType);
            }
            constCommand.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));

            if (inst.Args != null && inst.Args.Count > 0)
            {
                if (inst.InsType == NCSInstructionType.CONSTI)
                {
                    int intVal = GetArgAsInt(inst, 0);
                    AIntConstant constConstant = new AIntConstant();
                    constConstant.SetIntegerConstant(new TIntegerConstant(Convert.ToString(intVal, CultureInfo.InvariantCulture), pos, 0));
                    constCommand.SetConstant(constConstant);
                }
                else if (inst.InsType == NCSInstructionType.CONSTF)
                {
                    double floatVal = GetArgAsDouble(inst, 0);
                    AFloatConstant constConstant = new AFloatConstant();
                    constConstant.SetFloatConstant(new TFloatConstant(floatVal.ToString(CultureInfo.InvariantCulture), pos, 0));
                    constCommand.SetConstant(constConstant);
                }
                else if (inst.InsType == NCSInstructionType.CONSTS)
                {
                    string strVal = GetArgAsString(inst, 0, "");
                    AStringConstant constConstant = new AStringConstant();
                    constConstant.SetStringLiteral(new TStringLiteral("\"" + strVal + "\"", pos, 0));
                    constCommand.SetConstant(constConstant);
                }
                else if (inst.InsType == NCSInstructionType.CONSTO)
                {
                    int objVal = GetArgAsInt(inst, 0);
                    AIntConstant constConstant = new AIntConstant();
                    constConstant.SetIntegerConstant(new TIntegerConstant(Convert.ToString(objVal, CultureInfo.InvariantCulture), pos, 0));
                    constCommand.SetConstant(constConstant);
                }
            }

            constCommand.SetSemi(new TSemi(pos, 0));
            constCmd.SetConstCommand(constCommand);

            return constCmd;
        }

        private static AActionCmd ConvertActionCmd(NCSInstruction inst, int pos)
        {
            int idVal = 0;
            int argCountVal = 0;
            if (inst.Args != null && inst.Args.Count >= 1)
            {
                idVal = GetArgAsInt(inst, 0);
                if (inst.Args.Count > 1)
                {
                    argCountVal = GetArgAsInt(inst, 1);
                }
            }
            Console.Error.WriteLine($"DEBUG ConvertActionCmd: pos={pos}, actionId={idVal}, argCount={argCountVal}");
            Debug($"DEBUG ConvertActionCmd: pos={pos}, actionId={idVal}, argCount={argCountVal}");
            AActionCmd actionCmd = new AActionCmd();
            AActionCommand actionCommand = new AActionCommand();

            actionCommand.SetAction(new TAction(pos, 0));
            actionCommand.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));

            int typeVal = GetQualifier(inst.InsType);
            actionCommand.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));

            actionCommand.SetId(new TIntegerConstant(Convert.ToString(idVal, CultureInfo.InvariantCulture), pos, 0));
            actionCommand.SetArgCount(new TIntegerConstant(Convert.ToString(argCountVal, CultureInfo.InvariantCulture), pos, 0));
            actionCommand.SetSemi(new TSemi(pos, 0));

            actionCmd.SetActionCommand(actionCommand);
            return actionCmd;
        }

        private static PCmd ConvertJumpCmd(
            NCS ncs,
            NCSInstruction inst,
            int pos,
            List<NCSInstruction> instructions)
        {
            NCSInstructionType insType = inst.InsType;
            int typeVal = GetQualifier(insType);

            int offset = 0;
            if (inst.Jump != null)
            {
                try
                {
                    int jumpIdx = ncs.GetInstructionIndex(inst.Jump);
                    if (jumpIdx >= 0)
                    {
                        offset = jumpIdx - pos;
                    }
                }
                catch (Exception)
                {
                    offset = 0;
                }
            }

            if (insType == NCSInstructionType.JSR)
            {
                AJumpSubCmd jsrCmd = new AJumpSubCmd();
                AJumpToSubroutine jsrToSub = new AJumpToSubroutine();

                jsrToSub.SetJsr(new TJsr(pos, 0));
                jsrToSub.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
                jsrToSub.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
                jsrToSub.SetOffset(new TIntegerConstant(Convert.ToString(offset, CultureInfo.InvariantCulture), pos, 0));
                jsrToSub.SetSemi(new TSemi(pos, 0));

                jsrCmd.SetJumpToSubroutine(jsrToSub);
                return jsrCmd;
            }

            AJumpCmd jumpCmd = new AJumpCmd();
            AJumpCommand jumpCommand = new AJumpCommand();

            jumpCommand.SetJmp(new TJmp(pos, 0));
            jumpCommand.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
            jumpCommand.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            jumpCommand.SetOffset(new TIntegerConstant(Convert.ToString(offset, CultureInfo.InvariantCulture), pos, 0));
            jumpCommand.SetSemi(new TSemi(pos, 0));

            jumpCmd.SetJumpCommand(jumpCommand);
            return jumpCmd;
        }

        private static PCmd ConvertConditionalJumpCmd(
            NCS ncs,
            NCSInstruction inst,
            int pos,
            List<NCSInstruction> instructions)
        {
            NCSInstructionType insType = inst.InsType;
            int typeVal = GetQualifier(insType);

            int offset = 0;
            if (inst.Jump != null)
            {
                try
                {
                    int jumpIdx = ncs.GetInstructionIndex(inst.Jump);
                    if (jumpIdx >= 0)
                    {
                        offset = jumpIdx - pos;
                    }
                }
                catch (Exception)
                {
                    offset = 0;
                }
            }

            ACondJumpCmd condJumpCmd = new ACondJumpCmd();
            AConditionalJumpCommand condJumpCommand = new AConditionalJumpCommand();

            if (insType == NCSInstructionType.JZ)
            {
                AZeroJumpIf zeroJumpIf = new AZeroJumpIf();
                zeroJumpIf.SetJz(new TJz(pos, 0));
                condJumpCommand.SetJumpIf(zeroJumpIf);
            }
            else
            {
                ANonzeroJumpIf nonzeroJumpIf = new ANonzeroJumpIf();
                nonzeroJumpIf.SetJnz(new TJnz(pos, 0));
                condJumpCommand.SetJumpIf(nonzeroJumpIf);
            }

            condJumpCommand.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
            condJumpCommand.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            condJumpCommand.SetOffset(new TIntegerConstant(Convert.ToString(offset, CultureInfo.InvariantCulture), pos, 0));
            condJumpCommand.SetSemi(new TSemi(pos, 0));

            condJumpCmd.SetConditionalJumpCommand(condJumpCommand);
            return condJumpCmd;
        }

        private static AReturn ConvertRetn(NCSInstruction inst, int pos)
        {
            AReturn ret = new AReturn();
            ret.SetRetn(new TRetn(pos, 0));
            ret.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));

            int typeVal = GetQualifier(inst.InsType);
            ret.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            ret.SetSemi(new TSemi(pos, 0));

            return ret;
        }

        private static AReturnCmd ConvertRetnCmd(NCSInstruction inst, int pos)
        {
            AReturnCmd retnCmd = new AReturnCmd();
            AReturn retn = ConvertRetn(inst, pos);
            retnCmd.SetReturn(retn);
            return retnCmd;
        }

        private static PCmd ConvertCopySpCmd(NCSInstruction inst, int pos)
        {
            NCSInstructionType insType = inst.InsType;
            int typeVal = GetQualifier(insType);
            int offset = GetArgAsInt(inst, 0);
            int size = GetArgAsInt(inst, 1);

            if (insType == NCSInstructionType.CPDOWNSP)
            {
                ACopydownspCmd cmd = new ACopydownspCmd();
                ACopyDownSpCommand command = new ACopyDownSpCommand();
                command.SetCpdownsp(new TCpdownsp(pos, 0));
                command.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
                command.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
                command.SetOffset(new TIntegerConstant(Convert.ToString(offset, CultureInfo.InvariantCulture), pos, 0));
                command.SetSize(new TIntegerConstant(Convert.ToString(size, CultureInfo.InvariantCulture), pos, 0));
                command.SetSemi(new TSemi(pos, 0));
                cmd.SetCopyDownSpCommand(command);
                return cmd;
            }

            ACopytopspCmd topCmd = new ACopytopspCmd();
            ACopyTopSpCommand topCommand = new ACopyTopSpCommand();
            topCommand.SetCptopsp(new TCptopsp(pos, 0));
            topCommand.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
            topCommand.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            topCommand.SetOffset(new TIntegerConstant(Convert.ToString(offset, CultureInfo.InvariantCulture), pos, 0));
            topCommand.SetSize(new TIntegerConstant(Convert.ToString(size, CultureInfo.InvariantCulture), pos, 0));
            topCommand.SetSemi(new TSemi(pos, 0));
            topCmd.SetCopyTopSpCommand(topCommand);
            return topCmd;
        }

        private static PCmd ConvertCopyBpCmd(NCSInstruction inst, int pos)
        {
            NCSInstructionType insType = inst.InsType;
            int typeVal = GetQualifier(insType);
            int offset = GetArgAsInt(inst, 0);
            int size = GetArgAsInt(inst, 1);

            if (insType == NCSInstructionType.CPDOWNBP)
            {
                ACopydownbpCmd cmd = new ACopydownbpCmd();
                ACopyDownBpCommand command = new ACopyDownBpCommand();
                command.SetCpdownbp(new TCpdownbp(pos, 0));
                command.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
                command.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
                command.SetOffset(new TIntegerConstant(Convert.ToString(offset, CultureInfo.InvariantCulture), pos, 0));
                command.SetSize(new TIntegerConstant(Convert.ToString(size, CultureInfo.InvariantCulture), pos, 0));
                command.SetSemi(new TSemi(pos, 0));
                cmd.SetCopyDownBpCommand(command);
                return cmd;
            }

            ACopytopbpCmd topCmd = new ACopytopbpCmd();
            ACopyTopBpCommand topCommand = new ACopyTopBpCommand();
            topCommand.SetCptopbp(new TCptopbp(pos, 0));
            topCommand.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
            topCommand.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            topCommand.SetOffset(new TIntegerConstant(Convert.ToString(offset, CultureInfo.InvariantCulture), pos, 0));
            topCommand.SetSize(new TIntegerConstant(Convert.ToString(size, CultureInfo.InvariantCulture), pos, 0));
            topCommand.SetSemi(new TSemi(pos, 0));
            topCmd.SetCopyTopBpCommand(topCommand);
            return topCmd;
        }

        private static PCmd ConvertMovespCmd(NCSInstruction inst, int pos)
        {
            int typeVal = GetQualifier(inst.InsType);
            int offset = GetArgAsInt(inst, 0);

            AMovespCmd cmd = new AMovespCmd();
            AMoveSpCommand command = new AMoveSpCommand();
            command.SetMovsp(new TMovsp(pos, 0));
            command.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
            command.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            command.SetOffset(new TIntegerConstant(Convert.ToString(offset, CultureInfo.InvariantCulture), pos, 0));
            command.SetSemi(new TSemi(pos, 0));
            cmd.SetMoveSpCommand(command);

            return cmd;
        }

        private static PCmd ConvertRsaddCmd(NCSInstruction inst, int pos)
        {
            int typeVal = GetQualifier(inst.InsType);

            AST.ARsaddCmd cmd = new AST.ARsaddCmd();
            AST.ARsaddCommand command = new AST.ARsaddCommand();
            command.SetRsadd(new AST.TRsadd(pos, 0));
            command.SetPos(new AST.TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
            command.SetType(new AST.TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            command.SetSemi(new AST.TSemi(pos, 0));
            cmd.SetRsaddCommand(command);

            return cmd;
        }

        private static PCmd ConvertStackOpCmd(NCSInstruction inst, int pos)
        {
            int typeVal = GetQualifier(inst.InsType);
            int offset = GetArgAsInt(inst, 0);

            PCmd stackOpCmd = null;
            PStackOp stackOp = null;

            if (inst.InsType == NCSInstructionType.INCxSP)
            {
                stackOp = new AIncispStackOp();
                ((AIncispStackOp)stackOp).SetIncisp(new TIncisp(pos, 0));
            }
            else if (inst.InsType == NCSInstructionType.DECxSP)
            {
                stackOp = new ADecispStackOp();
                ((ADecispStackOp)stackOp).SetDecisp(new TDecisp(pos, 0));
            }
            else if (inst.InsType == NCSInstructionType.INCxBP)
            {
                stackOp = new AIncibpStackOp();
                ((AIncibpStackOp)stackOp).SetIncibp(new TIncibp(pos, 0));
            }
            else if (inst.InsType == NCSInstructionType.DECxBP)
            {
                stackOp = new ADecibpStackOp();
                ((ADecibpStackOp)stackOp).SetDecibp(new TDecibp(pos, 0));
            }
            else
            {
                System.Diagnostics.Debug.WriteLine(
                    "Unexpected instruction type in _convert_stack_op_cmd: " + inst.InsType);
                return null;
            }

            AStackCommand stackCommand = new AStackCommand();
            stackCommand.SetStackOp(stackOp);
            stackCommand.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
            stackCommand.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            stackCommand.SetOffset(new TIntegerConstant(Convert.ToString(offset, CultureInfo.InvariantCulture), pos, 0));
            stackCommand.SetSemi(new TSemi(pos, 0));

            AStackOpCmd cmd = new AStackOpCmd();
            cmd.SetStackCommand(stackCommand);
            stackOpCmd = cmd;

            return stackOpCmd;
        }

        private static PCmd ConvertDestructCmd(NCSInstruction inst, int pos)
        {
            int typeVal = GetQualifier(inst.InsType);
            int sizeRem = GetArgAsInt(inst, 0);
            int offset = GetArgAsInt(inst, 1);
            int sizeSave = GetArgAsInt(inst, 2);

            ADestructCmd cmd = new ADestructCmd();
            ADestructCommand command = new ADestructCommand();
            command.SetDestruct(new TDestruct(pos, 0));
            command.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
            command.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            command.SetSizeRem(new TIntegerConstant(Convert.ToString(sizeRem, CultureInfo.InvariantCulture), pos, 0));
            command.SetOffset(new TIntegerConstant(Convert.ToString(offset, CultureInfo.InvariantCulture), pos, 0));
            command.SetSizeSave(new TIntegerConstant(Convert.ToString(sizeSave, CultureInfo.InvariantCulture), pos, 0));
            command.SetSemi(new TSemi(pos, 0));
            cmd.SetDestructCommand(command);

            return cmd;
        }

        private static PCmd ConvertBpCmd(NCSInstruction inst, int pos)
        {
            int typeVal = GetQualifier(inst.InsType);

            AST.ABpCmd cmd = new AST.ABpCmd();
            AST.ABpCommand command = new AST.ABpCommand();

            if (inst.InsType == NCSInstructionType.SAVEBP)
            {
                AST.ASavebpBpOp bpOp = new AST.ASavebpBpOp();
                bpOp.SetSavebp(new AST.TSavebp(pos, 0));
                command.SetBpOp(bpOp);
            }
            else
            {
                AST.ARestorebpBpOp bpOp = new AST.ARestorebpBpOp();
                bpOp.SetRestorebp(new AST.TRestorebp(pos, 0));
                command.SetBpOp(bpOp);
            }

            command.SetPos(new AST.TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
            command.SetType(new AST.TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            command.SetSemi(new AST.TSemi(pos, 0));
            cmd.SetBpCommand(command);

            return cmd;
        }

        private static PCmd ConvertStoreStateCmd(NCSInstruction inst, int pos)
        {
            int offset = GetArgAsInt(inst, 0);
            int sizeBp = inst.Args != null && inst.Args.Count > 1 ? GetArgAsInt(inst, 1) : 0;
            int sizeSp = inst.Args != null && inst.Args.Count > 2 ? GetArgAsInt(inst, 2) : 0;

            AStoreStateCmd cmd = new AStoreStateCmd();
            AStoreStateCommand command = new AStoreStateCommand();
            command.SetStorestate(new TStorestate(pos, 0));
            command.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
            command.SetOffset(new TIntegerConstant(Convert.ToString(offset, CultureInfo.InvariantCulture), pos, 0));
            command.SetSizeBp(new TIntegerConstant(Convert.ToString(sizeBp, CultureInfo.InvariantCulture), pos, 0));
            command.SetSizeSp(new TIntegerConstant(Convert.ToString(sizeSp, CultureInfo.InvariantCulture), pos, 0));
            command.SetSemi(new TSemi(pos, 0));
            cmd.SetStoreStateCommand(command);

            return cmd;
        }

        private static PCmd ConvertBinaryCmd(NCSInstruction inst, int pos)
        {
            ABinaryCmd cmd = new ABinaryCmd();
            ABinaryCommand command = new ABinaryCommand();

            command.SetBinaryOp(CreateBinaryOperator(inst.InsType, pos));
            command.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));

            int typeVal = GetQualifier(inst.InsType);
            command.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));

            int resultSize = 1;
            command.SetSize(new TIntegerConstant(Convert.ToString(resultSize, CultureInfo.InvariantCulture), pos, 0));
            command.SetSemi(new TSemi(pos, 0));
            cmd.SetBinaryCommand(command);

            return cmd;
        }

        private static PCmd ConvertUnaryCmd(NCSInstruction inst, int pos)
        {
            Debug($"DEBUG ConvertUnaryCmd: Converting {inst.InsType} at pos={pos}");
            AUnaryCmd cmd = new AUnaryCmd();
            AUnaryCommand command = new AUnaryCommand();

            command.SetUnaryOp(CreateUnaryOperator(inst.InsType, pos));
            command.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));

            int typeVal = GetQualifier(inst.InsType);
            command.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            command.SetSemi(new TSemi(pos, 0));
            cmd.SetUnaryCommand(command);

            Debug($"DEBUG ConvertUnaryCmd: Created AUnaryCmd with op={command.GetUnaryOp()?.GetType().Name}");

            return cmd;
        }

        private static PCmd ConvertLogiiCmd(NCSInstruction inst, int pos)
        {
            ALogiiCmd cmd = new ALogiiCmd();
            ALogiiCommand command = new ALogiiCommand();

            command.SetLogiiOp(CreateLogiiOperator(inst.InsType, pos));
            command.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));

            int typeVal = GetQualifier(inst.InsType);
            command.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            command.SetSemi(new TSemi(pos, 0));
            cmd.SetLogiiCommand(command);

            return cmd;
        }

        private static PBinaryOp CreateBinaryOperator(NCSInstructionType insType, int pos)
        {
            if (insType == NCSInstructionType.USHRIGHTII)
            {
                return new AUnrightBinaryOp();
            }

            string insName = insType.ToString();
            if (insName.StartsWith("ADD"))
            {
                return new AAddBinaryOp();
            }

            if (insName.StartsWith("SUB"))
            {
                return new ASubBinaryOp();
            }

            if (insName.StartsWith("MUL"))
            {
                return new AMulBinaryOp();
            }

            if (insName.StartsWith("DIV"))
            {
                return new ADivBinaryOp();
            }

            if (insName.StartsWith("MOD"))
            {
                return new AModBinaryOp();
            }

            if (insName.StartsWith("EQUAL"))
            {
                return new AEqualBinaryOp();
            }

            if (insName.StartsWith("NEQUAL"))
            {
                return new ANequalBinaryOp();
            }

            if (insName.StartsWith("GT"))
            {
                return new AGtBinaryOp();
            }

            if (insName.StartsWith("LT"))
            {
                return new ALtBinaryOp();
            }

            if (insName.StartsWith("GEQ"))
            {
                return new AGeqBinaryOp();
            }

            if (insName.StartsWith("LEQ"))
            {
                return new ALeqBinaryOp();
            }

            if (insName.StartsWith("SHLEFT"))
            {
                return new AShleftBinaryOp();
            }

            if (insName.StartsWith("SHRIGHT"))
            {
                return new AShrightBinaryOp();
            }

            if (insName.StartsWith("USHRIGHT"))
            {
                return new AUnrightBinaryOp();
            }

            System.Diagnostics.Debug.WriteLine("Unknown binary operator: " + insType + ", creating placeholder");
            return new PlaceholderBinaryOp();
        }

        private static PUnaryOp CreateUnaryOperator(NCSInstructionType insType, int pos)
        {
            if (insType == NCSInstructionType.NEGI || insType == NCSInstructionType.NEGF)
            {
                return new ANegUnaryOp();
            }

            if (insType == NCSInstructionType.NOTI)
            {
                return new ANotUnaryOp();
            }

            if (insType == NCSInstructionType.COMPI)
            {
                return new ACompUnaryOp();
            }

            string insName = insType.ToString();
            if (insName.StartsWith("NEG"))
            {
                return new ANegUnaryOp();
            }

            if (insName.StartsWith("NOT"))
            {
                return new ANotUnaryOp();
            }

            if (insName.StartsWith("COMP"))
            {
                return new ACompUnaryOp();
            }

            System.Diagnostics.Debug.WriteLine("Unknown unary operator: " + insType + ", creating placeholder");
            return new PlaceholderUnaryOp();
        }

        private static PLogiiOp CreateLogiiOperator(NCSInstructionType insType, int pos)
        {
            if (insType == NCSInstructionType.LOGANDII)
            {
                return new AAndLogiiOp();
            }

            if (insType == NCSInstructionType.LOGORII)
            {
                return new AOrLogiiOp();
            }

            if (insType == NCSInstructionType.BOOLANDII)
            {
                return new ABitAndLogiiOp();
            }

            if (insType == NCSInstructionType.EXCORII)
            {
                return new AExclOrLogiiOp();
            }

            if (insType == NCSInstructionType.INCORII)
            {
                return new AInclOrLogiiOp();
            }

            string insName = insType.ToString();
            if (insName.StartsWith("LOGAND"))
            {
                return new AAndLogiiOp();
            }

            if (insName.StartsWith("LOGOR"))
            {
                return new AOrLogiiOp();
            }

            if (insName.StartsWith("BOOLAND"))
            {
                return new ABitAndLogiiOp();
            }

            if (insName.StartsWith("EXCOR"))
            {
                return new AExclOrLogiiOp();
            }

            if (insName.StartsWith("INCOR"))
            {
                return new AInclOrLogiiOp();
            }

            System.Diagnostics.Debug.WriteLine("Unknown logical operator: " + insType + ", creating placeholder");
            return new PlaceholderLogiiOp();
        }

        private static int GetQualifier(NCSInstructionType insType)
        {
            return insType.GetValue().Qualifier;
        }

        private static int GetArgAsInt(NCSInstruction inst, int index)
        {
            if (inst.Args != null && inst.Args.Count > index && inst.Args[index] != null)
            {
                object value = inst.Args[index];
                if (value is uint uintVal)
                {
                    return unchecked((int)uintVal);
                }

                if (value is long longVal)
                {
                    return unchecked((int)longVal);
                }

                if (value is IConvertible convertible)
                {
                    return convertible.ToInt32(CultureInfo.InvariantCulture);
                }

                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }

            return 0;
        }

        private static double GetArgAsDouble(NCSInstruction inst, int index)
        {
            if (inst.Args != null && inst.Args.Count > index && inst.Args[index] != null)
            {
                return Convert.ToDouble(inst.Args[index], CultureInfo.InvariantCulture);
            }

            return 0.0;
        }

        private static string GetArgAsString(NCSInstruction inst, int index, string defaultValue)
        {
            if (inst.Args != null && inst.Args.Count > index && inst.Args[index] != null)
            {
                return Convert.ToString(inst.Args[index], CultureInfo.InvariantCulture) ?? defaultValue;
            }

            return defaultValue;
        }

        private class PlaceholderBinaryOp : PBinaryOp
        {
            public override object Clone()
            {
                return new PlaceholderBinaryOp();
            }
            public override void Apply(Switch sw)
            {
                ((AnalysisAdapter)sw).DefaultCase(this);
            }

            public override void RemoveChild(Node child)
            {
            }

            public override void ReplaceChild(Node oldChild, Node newChild)
            {
            }
        }

        private class PlaceholderUnaryOp : PUnaryOp
        {
            public override object Clone()
            {
                return new PlaceholderUnaryOp();
            }
            public override void Apply(Switch sw)
            {
                ((AnalysisAdapter)sw).DefaultCase(this);
            }

            public override void RemoveChild(Node child)
            {
            }

            public override void ReplaceChild(Node oldChild, Node newChild)
            {
            }
        }

        private class PlaceholderLogiiOp : PLogiiOp
        {
            public override object Clone()
            {
                return new PlaceholderLogiiOp();
            }
            public override void Apply(Switch sw)
            {
                ((AnalysisAdapter)sw).DefaultCase(this);
            }

            public override void RemoveChild(Node child)
            {
            }

            public override void ReplaceChild(Node oldChild, Node newChild)
            {
            }
        }
    }
}





