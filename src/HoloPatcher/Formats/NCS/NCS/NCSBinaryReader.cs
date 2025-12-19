using System;
using static KOTORModSync.Formats.NCS.NCSDecomp.DecompilerLogger;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using KOTORModSync;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Formats.NCS
{

    /// <summary>
    /// Reads NCS (NWScript Compiled Script) files.
    ///
    /// NCS files contain compiled bytecode for NWScript, the scripting language used in KotOR.
    /// Instructions include operations, constants, function calls, jumps, and control flow.
    ///
    /// References:
    ///     vendor/reone/src/libs/script/format/ncsreader.cpp:28-40 (NCS header reading)
    ///     vendor/reone/src/libs/script/format/ncsreader.cpp:42-195 (instruction reading)
    ///     vendor/xoreos-tools/src/nwscript/decompiler.cpp (NCS decompilation)
    ///     vendor/xoreos-docs/specs/torlack/ncs.html (NCS format specification)
    /// </summary>
    public class NCSBinaryReader : IDisposable
    {
        private const byte NCS_HEADER_MAGIC_BYTE = 0x42;
        private const int NCS_HEADER_SIZE = 13; // "NCS " (4) + "V1.0" (4) + magic_byte (1) + size (4)

        private readonly KOTORModSync.Common.RawBinaryReader _reader;
        [CanBeNull]
        private NCS _ncs;
        private readonly Dictionary<int, NCSInstruction> _instructions = new Dictionary<int, NCSInstruction>();
        private readonly List<(NCSInstruction instruction, int jumpToOffset)> _jumps = new List<(NCSInstruction instruction, int jumpToOffset)>();
        public bool AutoClose { get; set; } = true;

        public NCSBinaryReader(string filepath, int offset = 0, int size = 0)
        {
            // CRITICAL DEBUG: Log which file is being read
            if (System.IO.File.Exists(filepath))
            {
                long fileSize = new System.IO.FileInfo(filepath).Length;
                Console.WriteLine($"DEBUG NCSBinaryReader: Opening file: {filepath}, file size on disk: {fileSize} bytes");
                Console.Error.WriteLine($"DEBUG NCSBinaryReader: Opening file: {filepath}, file size on disk: {fileSize} bytes");
            }
            else
            {
                Console.WriteLine($"DEBUG NCSBinaryReader: WARNING - File does not exist: {filepath}");
                Console.Error.WriteLine($"DEBUG NCSBinaryReader: WARNING - File does not exist: {filepath}");
            }
            _reader = KOTORModSync.Common.RawBinaryReader.FromFile(filepath, offset, size > 0 ? size : (int?)null);
        }

        public NCSBinaryReader(byte[] data, int offset = 0, int size = 0)
        {
            _reader = KOTORModSync.Common.RawBinaryReader.FromBytes(data, offset, size > 0 ? size : (int?)null);
        }

        public NCSBinaryReader(Stream source, int offset = 0, int size = 0)
        {
            _reader = KOTORModSync.Common.RawBinaryReader.FromStream(source, offset, size > 0 ? size : (int?)null);
        }

        /// <summary>
        /// Loads an NCS file from the reader.
        ///
        /// Returns:
        ///     NCS: The loaded NCS object
        ///
        /// Raises:
        ///     ValueError - Corrupt NCS.
        ///     IOException - some operating system issue occurred.
        ///
        /// Processing Logic:
        ///     - Reads the file type and version headers
        ///     - Reads each instruction from the file into a dictionary
        ///     - Resolves jump offsets to reference the target instructions
        ///     - Adds the instructions to the NCS object
        ///     - Optionally closes the reader.
        /// </summary>
        public NCS Load()
        {
            _ncs = new NCS();

            // Check minimum file size before reading
            if (_reader.Size < NCS_HEADER_SIZE)
            {
                throw new InvalidDataException(
                    $"NCS file is too small: expected at least {NCS_HEADER_SIZE} bytes, got {_reader.Size} bytes.");
            }

            string fileType = _reader.ReadString(4, "ascii");
            string fileVersion = _reader.ReadString(4, "ascii");

            if (fileType != "NCS ")
            {
                throw new InvalidDataException("The file type that was loaded is invalid.");
            }

            if (fileVersion != "V1.0")
            {
                throw new InvalidDataException("The NCS version that was loaded is not supported.");
            }

            _instructions.Clear();
            _jumps.Clear();

            // Read the header fields
            // vendor/reone/src/libs/script/format/ncsreader.cpp:31-32
            byte magicByte = _reader.ReadUInt8(); // Position 8
            uint totalSize = _reader.ReadUInt32(bigEndian: true); // Positions 9-12: Total file size

            // Validate header
            if (magicByte != NCS_HEADER_MAGIC_BYTE)
            {
                throw new InvalidDataException(
                    $"Invalid NCS header magic byte: expected 0x{NCS_HEADER_MAGIC_BYTE:X2}, got 0x{magicByte:X2}");
            }

            // Validate size field
            // CRITICAL: Use TrueSize() to get the actual file size, not Size which may be constrained
            // This ensures we can read all instructions even if the size field is incorrect
            int actualFileSize = _reader.TrueSize();
            int readerSize = _reader.Size;
            Console.WriteLine($"DEBUG NCSBinaryReader: TrueSize()={actualFileSize}, Size={readerSize}, totalSize={totalSize}");
            if (totalSize > actualFileSize)
            {
                throw new InvalidDataException(
                    $"NCS size field ({totalSize}) is larger than actual file size ({actualFileSize}). " +
                    "File may be corrupted or truncated.");
            }

            // Check for empty or minimal NCS files
            if (totalSize <= NCS_HEADER_SIZE)
            {
                // File has only a header, no instructions
                // This is technically valid but unusual
                _ncs.Instructions = new List<NCSInstruction>();
                return _ncs;
            }

            // Now at position 13, read instructions until we reach total_size
            // total_size is the end position (includes the header)
            int codeEndPosition = (int)totalSize;

            // CRITICAL: Some NCS files have incorrect size fields that are smaller than the actual file size
            // This causes instructions to be missed. We should ALWAYS read to the actual file size if it's larger
            // than the size field, regardless of the difference. This matches behavior needed for roundtrip tests
            // where external compiler may write incorrect size fields.
            // The size field is just a hint - the actual file size is authoritative.
            Console.WriteLine($"DEBUG NCSBinaryReader: Size field={codeEndPosition}, actualFileSize={actualFileSize}, difference={actualFileSize - codeEndPosition}, headerSize={NCS_HEADER_SIZE}");
            int safeEndPosition = codeEndPosition;
            if (actualFileSize > codeEndPosition)
            {
                // Size field is smaller than actual file - use actual file size
                // This handles cases where external compiler writes incorrect size fields
                Console.WriteLine($"DEBUG NCSBinaryReader: Size field ({codeEndPosition}) is smaller than actual file size ({actualFileSize}), using actual file size to read all instructions");
                safeEndPosition = actualFileSize;
            }
            else
            {
                // Normal case: use the smaller of size field or actual file size
                safeEndPosition = Math.Min(codeEndPosition, actualFileSize);
                Console.WriteLine($"DEBUG NCSBinaryReader: Using normal case: safeEndPosition={safeEndPosition}");
            }

            int instructionCountBeforeLoop = _instructions.Count;
            // CRITICAL: Only check Position < safeEndPosition, not Remaining
            // Remaining is based on _size which may be different from TrueSize()
            // safeEndPosition is already calculated using TrueSize(), so it's authoritative
            while (_reader.Position < safeEndPosition)
            {
                int offset = _reader.Position;

                // DEBUG: Log when we're at or near known ACTION bytecode offsets
                if (offset == 138 || offset == 514 || (offset >= 135 && offset <= 145) || (offset >= 511 && offset <= 521))
                {
                    Console.WriteLine($"DEBUG NCSBinaryReader: Reading instruction at offset {offset} (near known ACTION locations: 138, 514)");
                    Console.Error.WriteLine($"DEBUG NCSBinaryReader: Reading instruction at offset {offset} (near known ACTION locations: 138, 514)");
                }

                // DEBUG: Log when we're near the end of the file (for k_act_com41 debugging)
                if (offset >= 630 && offset <= 645)
                {
                    Console.WriteLine($"DEBUG NCSBinaryReader: Reading instruction at offset {offset}, remaining={safeEndPosition - offset} bytes until safeEndPosition={safeEndPosition}");
                    Console.Error.WriteLine($"DEBUG NCSBinaryReader: Reading instruction at offset {offset}, remaining={safeEndPosition - offset} bytes until safeEndPosition={safeEndPosition}");
                }

                // DEBUG: Log bytecode at offset 635 specifically (where MOVSP should be)
                if (offset == 635)
                {
                    int savedPos = _reader.Position;
                    _reader.Seek(635);
                    byte peekByte = _reader.ReadUInt8();
                    _reader.Seek(savedPos);
                    Console.WriteLine($"DEBUG NCSBinaryReader: At offset 635, byte=0x{peekByte:X2} ({peekByte})");
                    Console.Error.WriteLine($"DEBUG NCSBinaryReader: At offset 635, byte=0x{peekByte:X2} ({peekByte})");
                }

                try
                {
                    var instruction = ReadInstruction();
                    int newPosition = _reader.Position;
                    // DEBUG: Log instruction read completion
                    if (offset >= 630 && offset <= 645)
                    {
                        Console.WriteLine($"DEBUG NCSBinaryReader: Successfully read instruction at offset {offset}, new position={newPosition}, instruction type={instruction.InsType}");
                        Console.Error.WriteLine($"DEBUG NCSBinaryReader: Successfully read instruction at offset {offset}, new position={newPosition}, instruction type={instruction.InsType}");
                    }
                    // DEBUG: Check if this offset already exists (shouldn't happen, but let's verify)
                    if (_instructions.ContainsKey(offset))
                    {
                        Console.WriteLine($"DEBUG NCSBinaryReader: WARNING - Offset {offset} already exists in dictionary! Overwriting.");
                    }
                    _instructions[offset] = instruction;
                    // DEBUG: Log ACTION instructions as they're stored
                    if (instruction.InsType == NCSInstructionType.ACTION)
                    {
                        int currentActionCount = _instructions.Values.Count(inst => inst != null && inst.InsType == NCSInstructionType.ACTION);
                        Console.WriteLine($"DEBUG NCSBinaryReader: Stored ACTION instruction at offset {offset} in dictionary (dictionary now has {_instructions.Count} instructions, {currentActionCount} ACTION instructions)");
                    }
                }
                catch (InvalidDataException e)
                {
                    string errorMsg = e.Message;

                    // Check if this is zero-padding that slipped through due to incorrect size field
                    if (errorMsg.Contains("Unknown NCS bytecode 0x00"))
                    {
                        // Peek ahead to confirm this is just padding
                        _reader.Seek(offset);

                        // Read remaining bytes up to the safe end position
                        int bytesToCheck = Math.Min(_reader.Remaining, safeEndPosition - offset);
                        byte[] remainingData = _reader.ReadBytes(bytesToCheck);

                        bool allZeros = true;
                        foreach (byte b in remainingData)
                        {
                            if (b != 0)
                            {
                                allZeros = false;
                                break;
                            }
                        }

                        if (allZeros)
                        {
                            // This is zero-padding - the size field incorrectly includes padding
                            Console.Error.WriteLine(
                                $"Warning: NCS file has incorrect size field (includes zero-padding). " +
                                $"Size field: {totalSize}, actual code ends at: {offset}, " +
                                $"found {remainingData.Length} bytes of padding");
                            break;
                        }

                        // Not all zeros - this is genuinely corrupted data
                        // Show diagnostic information
                        _reader.Seek(offset);
                        int diagnosticSize = Math.Min(32, _reader.Remaining);
                        byte[] diagnosticBytes = _reader.ReadBytes(diagnosticSize);
                        string diagnosticHex = string.Join(" ", Array.ConvertAll(diagnosticBytes, b => $"{b:X2}"));

                        string enhancedMsg =
                            $"{errorMsg}\n" +
                            $"  File size field: {totalSize}, current offset: {offset}\n" +
                            $"  Next 32 bytes (hex): {diagnosticHex}\n" +
                            "  This indicates the NCS file is genuinely corrupted or uses an unknown format variant.";

                        throw new InvalidDataException(enhancedMsg, e);
                    }

                    // Re-raise other errors with additional context
                    string enhancedErrorMsg = $"Failed to parse NCS instruction at offset {offset}: {errorMsg}";
                    throw new InvalidDataException(enhancedErrorMsg, e);
                }
            }

            foreach ((NCSInstruction instruction, int jumpToOffset) in _jumps)
            {
                instruction.Jump = _instructions[jumpToOffset];
            }

            // CRITICAL: Sort instructions by byte offset to preserve order
            // Dictionary.Values doesn't guarantee order, so we need to explicitly sort
            Console.WriteLine($"DEBUG NCSBinaryReader: Dictionary has {_instructions.Count} instructions before sorting");
            int actionCountInDict = _instructions.Values.Count(inst => inst != null && inst.InsType == NCSInstructionType.ACTION);
            int negiCountInDict = _instructions.Values.Count(inst => inst != null && inst.InsType == NCSInstructionType.NEGI);
            Console.WriteLine($"DEBUG NCSBinaryReader: Dictionary contains {actionCountInDict} ACTION instructions before sorting");
            Console.WriteLine($"DEBUG NCSBinaryReader: Dictionary contains {negiCountInDict} NEGI instructions before sorting");

            // CRITICAL DEBUG: Log all instruction offsets to verify we have all 121
            var sortedOffsets = _instructions.Keys.OrderBy(k => k).ToList();
            Console.WriteLine($"DEBUG NCSBinaryReader: Instruction offsets (first 20): {string.Join(", ", sortedOffsets.Take(20))}");
            Console.WriteLine($"DEBUG NCSBinaryReader: Instruction offsets (last 20): {string.Join(", ", sortedOffsets.Skip(Math.Max(0, sortedOffsets.Count - 20)))}");
            Console.WriteLine($"DEBUG NCSBinaryReader: Total offset count: {sortedOffsets.Count}, min offset: {sortedOffsets.FirstOrDefault()}, max offset: {sortedOffsets.LastOrDefault()}");

            var sortedInstructions = _instructions.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value).ToList();
            _ncs.Instructions = sortedInstructions;

            // CRITICAL DEBUG: Verify the NCS object has all instructions
            Console.WriteLine($"DEBUG NCSBinaryReader: After assignment, _ncs.Instructions.Count={_ncs.Instructions?.Count ?? 0}");
            if (_ncs.Instructions != null && _ncs.Instructions.Count != sortedInstructions.Count)
            {
                Console.WriteLine($"DEBUG NCSBinaryReader: ERROR - Instruction count mismatch! sortedInstructions.Count={sortedInstructions.Count}, _ncs.Instructions.Count={_ncs.Instructions.Count}");
            }

            // DEBUG: Log instruction count and offset range
            if (sortedInstructions.Count > 0)
            {
                int minOffset = sortedInstructions[0].Offset;
                int maxOffset = sortedInstructions[sortedInstructions.Count - 1].Offset;
                Console.WriteLine($"DEBUG NCSBinaryReader: Created {sortedInstructions.Count} instructions, offset range: {minOffset} to {maxOffset}");
                // Count ACTION instructions in the sorted list
                int actionCount = sortedInstructions.Count(inst => inst != null && inst.InsType == NCSInstructionType.ACTION);
                Console.WriteLine($"DEBUG NCSBinaryReader: Found {actionCount} ACTION instructions in sorted list");
                if (actionCountInDict > 0 && actionCount == 0)
                {
                    Console.WriteLine($"DEBUG NCSBinaryReader: WARNING - Dictionary had {actionCountInDict} ACTION instructions but sorted list has 0!");
                }

                // CRITICAL DEBUG: Log all instruction types and their counts
                var typeCounts = new Dictionary<NCSInstructionType, int>();
                var typeOffsets = new Dictionary<NCSInstructionType, List<int>>();
                foreach (var inst in sortedInstructions)
                {
                    if (inst != null)
                    {
                        if (!typeCounts.ContainsKey(inst.InsType))
                        {
                            typeCounts[inst.InsType] = 0;
                            typeOffsets[inst.InsType] = new List<int>();
                        }
                        typeCounts[inst.InsType]++;
                        typeOffsets[inst.InsType].Add(inst.Offset);
                    }
                }
                Console.WriteLine($"DEBUG NCSBinaryReader: Instruction type breakdown: {string.Join(", ", typeCounts.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");

                // Log NEGI instructions specifically (they're critical for negative constants)
                if (typeCounts.ContainsKey(NCSInstructionType.NEGI))
                {
                    Console.WriteLine($"DEBUG NCSBinaryReader: Found {typeCounts[NCSInstructionType.NEGI]} NEGI instructions at offsets: {string.Join(", ", typeOffsets[NCSInstructionType.NEGI].Take(10))}");
                }

                // Log SAVEBP instructions
                if (typeCounts.ContainsKey(NCSInstructionType.SAVEBP))
                {
                    Console.WriteLine($"DEBUG NCSBinaryReader: Found {typeCounts[NCSInstructionType.SAVEBP]} SAVEBP instructions at offsets: {string.Join(", ", typeOffsets[NCSInstructionType.SAVEBP])}");
                }
            }

            return _ncs;
        }

        private NCSInstruction ReadInstruction()
        {
            int instructionOffset = _reader.Position;
            byte byteCodeValue = _reader.ReadUInt8();
            byte qualifier = _reader.ReadUInt8();

            // DEBUG: Log ACTION bytecode (0x05) when encountered
            if (byteCodeValue == 0x05)
            {
                // Note: Using Console.WriteLine instead of JavaSystem since this is in the binary reader, not decompiler
                // This will appear in test output
                Console.WriteLine($"DEBUG NCSBinaryReader: Found ACTION bytecode (0x05) at offset {instructionOffset}, qualifier=0x{qualifier:X2}");
                // Also log to stderr for visibility in test output
                Console.Error.WriteLine($"DEBUG NCSBinaryReader: Found ACTION bytecode (0x05) at offset {instructionOffset}, qualifier=0x{qualifier:X2}");
            }

            // Try to convert to NCSByteCode enum
            if (!Enum.IsDefined(typeof(NCSByteCode), byteCodeValue))
            {
                // Provide detailed diagnostic information for unknown bytecodes
                // Read some context bytes around the error position
                int contextSize = 16;
                int contextStart = Math.Max(0, instructionOffset - contextSize);
                int currentPos = _reader.Position;

                _reader.Seek(contextStart);
                int contextBytesToRead = Math.Min(contextSize * 2, _reader.Size - contextStart);
                byte[] contextBytes = _reader.ReadBytes(contextBytesToRead);
                string contextHex = string.Join(" ", Array.ConvertAll(contextBytes, b => $"{b:X2}"));

                _reader.Seek(currentPos); // Restore position

                string msg =
                    $"Unknown NCS bytecode 0x{byteCodeValue:X2} with qualifier 0x{qualifier:X2} at offset {instructionOffset}.\n" +
                    $"  Context (hex): {contextHex}\n" +
                    "  This NCS file may be corrupted, from an unsupported NCS variant, or have an incorrect size field.";

                throw new InvalidDataException(msg);
            }

            NCSByteCode byteCode = (NCSByteCode)byteCodeValue;

            NCSInstruction instruction = new NCSInstruction
            {
                Offset = instructionOffset,
                OriginalBytecode = byteCodeValue,
                OriginalQualifier = qualifier
            };

            // Special handling for RESERVED opcode - it appears with various qualifiers
            // Treat all RESERVED variants as simple 2-byte no-ops
            if (byteCode == NCSByteCode.RESERVED)
            {
                // Use RESERVED (0x00, 0x00) as the canonical type regardless of actual qualifier
                instruction.InsType = NCSInstructionType.RESERVED;
            }
            else
            {
                try
                {
                    instruction.InsType = NCSInstructionTypeExtensions.FromBytecode(byteCode, qualifier);
                    // DEBUG: Log if ACTION bytecode resulted in non-ACTION instruction type
                    if (byteCode == NCSByteCode.ACTION && instruction.InsType != NCSInstructionType.ACTION)
                    {
                        Console.WriteLine($"DEBUG NCSBinaryReader: WARNING - ACTION bytecode (0x05) at offset {instructionOffset} with qualifier 0x{qualifier:X2} resulted in instruction type {instruction.InsType} instead of ACTION");
                    }
                    // DEBUG: Log successful ACTION parsing
                    if (instruction.InsType == NCSInstructionType.ACTION)
                    {
                        Console.WriteLine($"DEBUG NCSBinaryReader: Successfully parsed ACTION instruction at offset {instructionOffset}");
                        Console.Error.WriteLine($"DEBUG NCSBinaryReader: Successfully parsed ACTION instruction at offset {instructionOffset}");
                    }
                }
                catch (ArgumentException e)
                {
                    // Unknown bytecode/qualifier combination - the bytecode exists but this specific
                    // combination with the qualifier is not recognized
                    // CRITICAL: For ACTION bytecode, we should always return ACTION type regardless of qualifier
                    // This handles ACTION instructions with non-zero qualifiers (e.g., 0x01, 0x19)
                    if (byteCode == NCSByteCode.ACTION)
                    {
                        Console.WriteLine($"DEBUG NCSBinaryReader: ArgumentException for ACTION bytecode at offset {instructionOffset} with qualifier 0x{qualifier:X2}, forcing ACTION type");
                        Console.Error.WriteLine($"DEBUG NCSBinaryReader: ArgumentException for ACTION bytecode at offset {instructionOffset} with qualifier 0x{qualifier:X2}, forcing ACTION type");
                        instruction.InsType = NCSInstructionType.ACTION;
                    }
                    else
                    {
                        // For roundtrip fidelity, preserve invalid qualifiers by using a fallback type
                        // We'll use the closest matching instruction type, but preserve original bytecode/qualifier
                        // This allows us to write back the exact same bytecode/qualifier even if it's invalid
                        Console.WriteLine($"DEBUG NCSBinaryReader: Invalid qualifier 0x{qualifier:X2} for bytecode 0x{byteCodeValue:X2} at offset {instructionOffset}, preserving original for roundtrip");
                        Console.Error.WriteLine($"DEBUG NCSBinaryReader: Invalid qualifier 0x{qualifier:X2} for bytecode 0x{byteCodeValue:X2} at offset {instructionOffset}, preserving original for roundtrip");

                        // Try to find a fallback instruction type based on bytecode alone
                        // For LOGANDxx (0x06), use LOGANDII as fallback but preserve original qualifier
                        if (byteCode == NCSByteCode.LOGANDxx)
                        {
                            instruction.InsType = NCSInstructionType.LOGANDII;
                        }
                        else if (byteCode == NCSByteCode.MOVSP)
                        {
                            // MOVSP with invalid qualifier - use MOVSP as fallback but preserve original qualifier
                            instruction.InsType = NCSInstructionType.MOVSP;
                        }
                        else if (byteCode == NCSByteCode.LEQxx || byteCode == NCSByteCode.GEQxx ||
                                 byteCode == NCSByteCode.GTxx || byteCode == NCSByteCode.LTxx ||
                                 byteCode == NCSByteCode.EQUALxx || byteCode == NCSByteCode.NEQUALxx)
                        {
                            // Comparison operators with invalid qualifiers - use IntInt variant as fallback
                            // This preserves the instruction semantics while allowing roundtrip
                            Console.WriteLine($"DEBUG NCSBinaryReader: Using fallback for {byteCode} with invalid qualifier 0x{qualifier:X2} at offset {instructionOffset}");
                            if (byteCode == NCSByteCode.LEQxx)
                            {
                                instruction.InsType = NCSInstructionType.LEQII;
                            }
                            else if (byteCode == NCSByteCode.GEQxx)
                            {
                                instruction.InsType = NCSInstructionType.GEQII;
                            }
                            else if (byteCode == NCSByteCode.GTxx)
                            {
                                instruction.InsType = NCSInstructionType.GTII;
                            }
                            else if (byteCode == NCSByteCode.LTxx)
                            {
                                instruction.InsType = NCSInstructionType.LTII;
                            }
                            else if (byteCode == NCSByteCode.EQUALxx)
                            {
                                instruction.InsType = NCSInstructionType.EQUALII;
                            }
                            else if (byteCode == NCSByteCode.NEQUALxx)
                            {
                                instruction.InsType = NCSInstructionType.NEQUALII;
                            }
                        }
                        else
                        {
                            // Comprehensive fallback handling for all bytecodes with invalid qualifiers
                            // This preserves roundtrip fidelity by selecting the most appropriate fallback
                            // instruction type based on the bytecode's semantic meaning
                            switch (byteCode)
                            {
                                // Logical operators - use IntInt variant as fallback
                                case NCSByteCode.LOGORxx:
                                    instruction.InsType = NCSInstructionType.LOGORII;
                                    break;
                                case NCSByteCode.INCORxx:
                                    instruction.InsType = NCSInstructionType.INCORII;
                                    break;
                                case NCSByteCode.EXCORxx:
                                    instruction.InsType = NCSInstructionType.EXCORII;
                                    break;
                                case NCSByteCode.BOOLANDxx:
                                    instruction.InsType = NCSInstructionType.BOOLANDII;
                                    break;

                                // Shift operators - use IntInt variant as fallback
                                case NCSByteCode.SHLEFTxx:
                                    instruction.InsType = NCSInstructionType.SHLEFTII;
                                    break;
                                case NCSByteCode.SHRIGHTxx:
                                    instruction.InsType = NCSInstructionType.SHRIGHTII;
                                    break;
                                case NCSByteCode.USHRIGHTxx:
                                    instruction.InsType = NCSInstructionType.USHRIGHTII;
                                    break;

                                // Arithmetic operators - use IntInt variant as fallback
                                case NCSByteCode.ADDxx:
                                    instruction.InsType = NCSInstructionType.ADDII;
                                    break;
                                case NCSByteCode.SUBxx:
                                    instruction.InsType = NCSInstructionType.SUBII;
                                    break;
                                case NCSByteCode.MULxx:
                                    instruction.InsType = NCSInstructionType.MULII;
                                    break;
                                case NCSByteCode.DIVxx:
                                    instruction.InsType = NCSInstructionType.DIVII;
                                    break;
                                case NCSByteCode.MODxx:
                                    instruction.InsType = NCSInstructionType.MODII;
                                    break;

                                // Unary operators - use Int variant as fallback
                                case NCSByteCode.NEGx:
                                    instruction.InsType = NCSInstructionType.NEGI;
                                    break;
                                case NCSByteCode.COMPx:
                                    instruction.InsType = NCSInstructionType.COMPI;
                                    break;
                                case NCSByteCode.NOTx:
                                    instruction.InsType = NCSInstructionType.NOTI;
                                    break;

                                // Stack/BP operations with type qualifiers - use Int variant as fallback
                                case NCSByteCode.DECxSP:
                                    instruction.InsType = NCSInstructionType.DECxSP;
                                    break;
                                case NCSByteCode.INCxSP:
                                    instruction.InsType = NCSInstructionType.INCxSP;
                                    break;
                                case NCSByteCode.DECxBP:
                                    instruction.InsType = NCSInstructionType.DECxBP;
                                    break;
                                case NCSByteCode.INCxBP:
                                    instruction.InsType = NCSInstructionType.INCxBP;
                                    break;

                                // RSADD operations - use Int variant as fallback
                                case NCSByteCode.RSADDx:
                                    instruction.InsType = NCSInstructionType.RSADDI;
                                    break;

                                // Constant operations - use Int variant as fallback
                                case NCSByteCode.CONSTx:
                                    instruction.InsType = NCSInstructionType.CONSTI;
                                    break;

                                // Stack copy operations - use expected qualifier (0x01) as fallback
                                case NCSByteCode.CPDOWNSP:
                                    instruction.InsType = NCSInstructionType.CPDOWNSP;
                                    break;
                                case NCSByteCode.CPTOPSP:
                                    instruction.InsType = NCSInstructionType.CPTOPSP;
                                    break;
                                case NCSByteCode.CPDOWNBP:
                                    instruction.InsType = NCSInstructionType.CPDOWNBP;
                                    break;
                                case NCSByteCode.CPTOPBP:
                                    instruction.InsType = NCSInstructionType.CPTOPBP;
                                    break;

                                // Control flow operations - use expected qualifier (0x00) as fallback
                                case NCSByteCode.JMP:
                                    instruction.InsType = NCSInstructionType.JMP;
                                    break;
                                case NCSByteCode.JSR:
                                    instruction.InsType = NCSInstructionType.JSR;
                                    break;
                                case NCSByteCode.JZ:
                                    instruction.InsType = NCSInstructionType.JZ;
                                    break;
                                case NCSByteCode.JNZ:
                                    instruction.InsType = NCSInstructionType.JNZ;
                                    break;
                                case NCSByteCode.RETN:
                                    instruction.InsType = NCSInstructionType.RETN;
                                    break;

                                // Frame pointer operations - use expected qualifier (0x00) as fallback
                                case NCSByteCode.SAVEBP:
                                    instruction.InsType = NCSInstructionType.SAVEBP;
                                    break;
                                case NCSByteCode.RESTOREBP:
                                    instruction.InsType = NCSInstructionType.RESTOREBP;
                                    break;

                                // Destruct operation - use expected qualifier (0x01) as fallback
                                case NCSByteCode.DESTRUCT:
                                    instruction.InsType = NCSInstructionType.DESTRUCT;
                                    break;

                                // Store state operation - use expected qualifier (0x10) as fallback
                                case NCSByteCode.STORE_STATE:
                                    instruction.InsType = NCSInstructionType.STORE_STATE;
                                    break;

                                // NOP operations - use NOP as fallback
                                // Note: NOP and NOP2 both have value 0x2D, so we only need one case
                                case NCSByteCode.NOP:
                                    instruction.InsType = NCSInstructionType.NOP;
                                    break;

                                // RESERVED bytecode - already handled above, but include for completeness
                                case NCSByteCode.RESERVED:
                                    instruction.InsType = NCSInstructionType.RESERVED;
                                    break;

                                // Unknown bytecode - this should not happen as we check Enum.IsDefined above
                                // but include as final fallback for safety
                                default:
                                    Console.WriteLine($"DEBUG NCSBinaryReader: Unknown bytecode 0x{byteCodeValue:X2} with invalid qualifier 0x{qualifier:X2} at offset {instructionOffset}, using RESERVED as final fallback");
                                    Console.Error.WriteLine($"DEBUG NCSBinaryReader: Unknown bytecode 0x{byteCodeValue:X2} with invalid qualifier 0x{qualifier:X2} at offset {instructionOffset}, using RESERVED as final fallback");
                                    instruction.InsType = NCSInstructionType.RESERVED;
                                    break;
                            }

                            Console.WriteLine($"DEBUG NCSBinaryReader: Applied fallback for bytecode 0x{byteCodeValue:X2} with invalid qualifier 0x{qualifier:X2} at offset {instructionOffset}, using instruction type {instruction.InsType}");
                            Console.Error.WriteLine($"DEBUG NCSBinaryReader: Applied fallback for bytecode 0x{byteCodeValue:X2} with invalid qualifier 0x{qualifier:X2} at offset {instructionOffset}, using instruction type {instruction.InsType}");
                        }
                    }
                }
            }

            // Read instruction arguments based on type
            if (instruction.InsType == NCSInstructionType.CPDOWNSP || instruction.InsType == NCSInstructionType.CPTOPSP
                || instruction.InsType == NCSInstructionType.CPDOWNBP || instruction.InsType == NCSInstructionType.CPTOPBP)
            {
                instruction.Args.Add(_reader.ReadInt32(bigEndian: true));
                instruction.Args.Add(_reader.ReadUInt16(bigEndian: true));
            }
            else if (instruction.InsType == NCSInstructionType.CONSTI)
            {
                instruction.Args.Add(_reader.ReadUInt32(bigEndian: true));
            }
            else if (instruction.InsType == NCSInstructionType.CONSTF)
            {
                instruction.Args.Add(_reader.ReadSingle(bigEndian: true));
            }
            else if (instruction.InsType == NCSInstructionType.CONSTS)
            {
                ushort length = _reader.ReadUInt16(bigEndian: true);
                instruction.Args.Add(_reader.ReadString(length, "ascii"));
            }
            else if (instruction.InsType == NCSInstructionType.CONSTO)
            {
                // Object constants are stored as signed 32-bit integers, not 16-bit
                // See NCSDecomp Decoder.java case 4, subcase 6 (OBJECT type uses readSignedInt)
                instruction.Args.Add(_reader.ReadInt32(bigEndian: true));
            }
            else if (instruction.InsType == NCSInstructionType.ACTION)
            {
                instruction.Args.Add(_reader.ReadUInt16(bigEndian: true));
                instruction.Args.Add(_reader.ReadUInt8());
            }
            else if (instruction.InsType == NCSInstructionType.MOVSP)
            {
                int currentPos = _reader.Position;
                int offsetValue = _reader.ReadInt32(bigEndian: true);
                int newPos = _reader.Position;
                Console.WriteLine($"DEBUG NCSBinaryReader: Read MOVSP offset at position {currentPos}, value={offsetValue}, new position={newPos}");
                instruction.Args.Add(offsetValue);
            }
            else if (instruction.InsType == NCSInstructionType.JMP || instruction.InsType == NCSInstructionType.JSR
                || instruction.InsType == NCSInstructionType.JZ || instruction.InsType == NCSInstructionType.JNZ)
            {
                int jumpOffset = _reader.ReadInt32(bigEndian: true) + _reader.Position - 6;
                _jumps.Add((instruction, jumpOffset));
            }
            else if (instruction.InsType == NCSInstructionType.DESTRUCT)
            {
                instruction.Args.Add(_reader.ReadUInt16(bigEndian: true));
                instruction.Args.Add(_reader.ReadInt16(bigEndian: true));
                instruction.Args.Add(_reader.ReadUInt16(bigEndian: true));
            }
            else if (instruction.InsType == NCSInstructionType.DECxSP || instruction.InsType == NCSInstructionType.INCxSP
                || instruction.InsType == NCSInstructionType.DECxBP || instruction.InsType == NCSInstructionType.INCxBP)
            {
                instruction.Args.Add(_reader.ReadUInt32(bigEndian: true));
            }
            else if (instruction.InsType == NCSInstructionType.STORE_STATE)
            {
                instruction.Args.Add(_reader.ReadUInt32(bigEndian: true));
                instruction.Args.Add(_reader.ReadUInt32(bigEndian: true));
            }
            else if (instruction.InsType == NCSInstructionType.EQUALTT || instruction.InsType == NCSInstructionType.NEQUALTT)
            {
                // Struct equality comparisons include a size field
                // See NCSDecomp Decoder.java case 11/12 with qualifier 0x24 (36 = StructStruct)
                instruction.Args.Add(_reader.ReadUInt16(bigEndian: true));
            }
            else if (instruction.InsType == NCSInstructionType.LOGANDII || instruction.InsType == NCSInstructionType.LOGORII
                || instruction.InsType == NCSInstructionType.INCORII || instruction.InsType == NCSInstructionType.EXCORII
                || instruction.InsType == NCSInstructionType.BOOLANDII
                || instruction.InsType == NCSInstructionType.EQUALII || instruction.InsType == NCSInstructionType.EQUALFF
                || instruction.InsType == NCSInstructionType.EQUALOO || instruction.InsType == NCSInstructionType.EQUALEFFEFF
                || instruction.InsType == NCSInstructionType.EQUALEVTEVT || instruction.InsType == NCSInstructionType.EQUALLOCLOC
                || instruction.InsType == NCSInstructionType.EQUALTALTAL || instruction.InsType == NCSInstructionType.EQUALSS
                || instruction.InsType == NCSInstructionType.NEQUALII || instruction.InsType == NCSInstructionType.NEQUALFF
                || instruction.InsType == NCSInstructionType.NEQUALOO || instruction.InsType == NCSInstructionType.NEQUALEFFEFF
                || instruction.InsType == NCSInstructionType.NEQUALEVTEVT || instruction.InsType == NCSInstructionType.NEQUALLOCLOC
                || instruction.InsType == NCSInstructionType.NEQUALTALTAL || instruction.InsType == NCSInstructionType.NEQUALSS
                || instruction.InsType == NCSInstructionType.GEQII || instruction.InsType == NCSInstructionType.GEQFF
                || instruction.InsType == NCSInstructionType.GTII || instruction.InsType == NCSInstructionType.GTFF
                || instruction.InsType == NCSInstructionType.LTII || instruction.InsType == NCSInstructionType.LTFF
                || instruction.InsType == NCSInstructionType.LEQII || instruction.InsType == NCSInstructionType.LEQFF
                || instruction.InsType == NCSInstructionType.SHLEFTII || instruction.InsType == NCSInstructionType.SHRIGHTII
                || instruction.InsType == NCSInstructionType.USHRIGHTII
                || instruction.InsType == NCSInstructionType.ADDII || instruction.InsType == NCSInstructionType.ADDFF
                || instruction.InsType == NCSInstructionType.ADDFI || instruction.InsType == NCSInstructionType.ADDIF
                || instruction.InsType == NCSInstructionType.ADDSS || instruction.InsType == NCSInstructionType.ADDVV
                || instruction.InsType == NCSInstructionType.SUBII || instruction.InsType == NCSInstructionType.SUBFF
                || instruction.InsType == NCSInstructionType.SUBFI || instruction.InsType == NCSInstructionType.SUBIF
                || instruction.InsType == NCSInstructionType.SUBVV
                || instruction.InsType == NCSInstructionType.MULII || instruction.InsType == NCSInstructionType.MULFF
                || instruction.InsType == NCSInstructionType.MULFI || instruction.InsType == NCSInstructionType.MULIF
                || instruction.InsType == NCSInstructionType.MULFV || instruction.InsType == NCSInstructionType.MULVF
                || instruction.InsType == NCSInstructionType.DIVII || instruction.InsType == NCSInstructionType.DIVFF
                || instruction.InsType == NCSInstructionType.DIVFI || instruction.InsType == NCSInstructionType.DIVIF
                || instruction.InsType == NCSInstructionType.DIVFV || instruction.InsType == NCSInstructionType.DIVVF
                || instruction.InsType == NCSInstructionType.MODII
                || instruction.InsType == NCSInstructionType.NEGI || instruction.InsType == NCSInstructionType.NEGF
                || instruction.InsType == NCSInstructionType.COMPI
                || instruction.InsType == NCSInstructionType.RETN
                || instruction.InsType == NCSInstructionType.NOTI
                || instruction.InsType == NCSInstructionType.SAVEBP || instruction.InsType == NCSInstructionType.RESTOREBP
                || instruction.InsType == NCSInstructionType.NOP
                || instruction.InsType == NCSInstructionType.RESERVED || instruction.InsType == NCSInstructionType.RESERVED_01
                || instruction.InsType == NCSInstructionType.RSADDI || instruction.InsType == NCSInstructionType.RSADDF
                || instruction.InsType == NCSInstructionType.RSADDO || instruction.InsType == NCSInstructionType.RSADDS
                || instruction.InsType == NCSInstructionType.RSADDEFF || instruction.InsType == NCSInstructionType.RSADDEVT
                || instruction.InsType == NCSInstructionType.RSADDLOC || instruction.InsType == NCSInstructionType.RSADDTAL)
            {
                // All these instructions have no arguments (just opcode + qualifier)
            }
            else
            {
                string msg = $"Tried to read unsupported instruction '{instruction.InsType}' to NCS";
                throw new InvalidDataException(msg);
            }

            return instruction;
        }

        public void Dispose()
        {
            if (AutoClose)
            {
                _reader?.Dispose();
            }
        }
    }
}
