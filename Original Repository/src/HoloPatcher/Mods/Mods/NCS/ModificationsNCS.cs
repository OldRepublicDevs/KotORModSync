using System;
using System.Collections.Generic;
using System.IO;
using KOTORModSync;
using KOTORModSync.Logger;
using KOTORModSync.Memory;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Mods.NCS
{

    /// <summary>
    /// NCS modification algorithms for TSLPatcher/HoloPatcher.
    ///
    /// This module implements NCS bytecode modification logic for applying patches from changes.ini files.
    /// Handles byte-level modifications for memory tokens (StrRef, 2DAMemory) in compiled scripts.
    ///
    /// References:
    /// ----------
    ///     vendor/TSLPatcher/TSLPatcher.pl - Perl NCS modification logic (likely unfinished)
    ///     vendor/Kotor.NET/Kotor.NET.Patcher/ - Incomplete C# patcher
    /// </summary>

    /// <summary>
    /// Token types for NCS bytecode modifications.
    /// 1:1 port from Python NCSTokenType enum
    /// </summary>
    public enum NCSTokenType
    {
        /// <summary>16-bit unsigned TLK string reference</summary>
        STRREF,
        /// <summary>32-bit signed TLK string reference (CONSTI instruction)</summary>
        STRREF32,
        /// <summary>16-bit unsigned 2DA memory reference</summary>
        MEMORY_2DA,
        /// <summary>32-bit signed 2DA memory reference (CONSTI instruction)</summary>
        MEMORY_2DA32,
        /// <summary>32-bit unsigned integer literal</summary>
        UINT32,
        /// <summary>16-bit unsigned integer literal</summary>
        UINT16,
        /// <summary>8-bit unsigned integer literal</summary>
        UINT8
    }

    /// <summary>
    /// Represents a single NCS bytecode modification operation.
    /// 1:1 port from Python ModifyNCS class
    /// </summary>
    public class ModifyNCS
    {
        public NCSTokenType TokenType { get; }
        public int Offset { get; }
        public int TokenIdOrValue { get; }

        /// <summary>
        /// Initialize an NCS modification.
        ///
        /// Args:
        /// ----
        ///     token_type: Type of modification (NCSTokenType enum)
        ///     offset: Byte offset in the NCS file to write to
        ///     token_id_or_value: Token ID for memory lookup or direct value to write
        /// </summary>
        public ModifyNCS(NCSTokenType tokenType, int offset, int tokenIdOrValue)
        {
            TokenType = tokenType;
            Offset = offset;
            TokenIdOrValue = tokenIdOrValue;
        }

        /// <summary>
        /// Apply the NCS modification to the bytecode.
        ///
        /// Args:
        /// ----
        ///     writer: BinaryWriter positioned in the NCS bytearray
        ///     memory: PatcherMemory object for token lookups
        ///     logger: PatchLogger for logging operations
        ///     sourcefile: Name of the source file being modified (for logging)
        /// </summary>
        public void Apply(System.IO.BinaryWriter writer, PatcherMemory memory, PatchLogger logger, string sourcefile)
        {
            logger.AddVerbose($"HACKList {sourcefile}: seeking to offset {Offset:#X}");
            writer.Seek(Offset, SeekOrigin.Begin);

            switch (TokenType)
            {
                case NCSTokenType.STRREF:
                    WriteStrRef(writer, memory, logger, sourcefile);
                    break;
                case NCSTokenType.STRREF32:
                    WriteStrRef32(writer, memory, logger, sourcefile);
                    break;
                case NCSTokenType.MEMORY_2DA:
                    Write2DAMemory(writer, memory, logger, sourcefile);
                    break;
                case NCSTokenType.MEMORY_2DA32:
                    Write2DAMemory32(writer, memory, logger, sourcefile);
                    break;
                case NCSTokenType.UINT32:
                    WriteUInt32(writer, logger, sourcefile);
                    break;
                case NCSTokenType.UINT16:
                    WriteUInt16(writer, logger, sourcefile);
                    break;
                case NCSTokenType.UINT8:
                    WriteUInt8(writer, logger, sourcefile);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown token type '{TokenType}' in HACKList patch");
            }
        }

        /// <summary>Write a 16-bit unsigned TLK string reference.</summary>
        private void WriteStrRef(System.IO.BinaryWriter writer, PatcherMemory memory, PatchLogger logger, string sourcefile)
        {
            if (!memory.MemoryStr.TryGetValue(TokenIdOrValue, out int memoryStrval))
            {
                throw new KeyNotFoundException($"StrRef{TokenIdOrValue} was not defined before use");
            }
            int value = memory.MemoryStr[TokenIdOrValue];
            logger.AddVerbose($"HACKList {sourcefile}: writing unsigned WORD (16-bit) {value} at offset {Offset:#X}");
            WriteUInt16BigEndian(writer, (ushort)value);
        }

        /// <summary>Write a 32-bit signed TLK string reference (CONSTI instruction).</summary>
        private void WriteStrRef32(System.IO.BinaryWriter writer, PatcherMemory memory, PatchLogger logger, string sourcefile)
        {
            if (!memory.MemoryStr.TryGetValue(TokenIdOrValue, out int memoryStrval))
            {
                throw new KeyNotFoundException($"StrRef{TokenIdOrValue} was not defined before use");
            }
            int value = memory.MemoryStr[TokenIdOrValue];
            logger.AddVerbose($"HACKList {sourcefile}: writing signed DWORD (32-bit) {value} at offset {Offset:#X}");
            WriteInt32BigEndian(writer, value);
        }

        /// <summary>Write a 16-bit unsigned 2DA memory reference.</summary>
        private void Write2DAMemory(System.IO.BinaryWriter writer, PatcherMemory memory, PatchLogger logger, string sourcefile)
        {
            // Can be null if not found
            if (!memory.Memory2DA.TryGetValue(TokenIdOrValue, out string memoryVal))
            {
                throw new KeyNotFoundException($"2DAMEMORY{TokenIdOrValue} was not defined before use");
            }
            if (memoryVal != null && (memoryVal.Contains('/') || memoryVal.Contains('\\')))
            {
                throw new InvalidOperationException($"Memory value cannot be !FieldPath in [HACKList] patches, got '{memoryVal}'");
            }
            int value = int.Parse(memoryVal ?? "0");
            logger.AddVerbose($"HACKList {sourcefile}: writing unsigned WORD (16-bit) {value} at offset {Offset:#X}");
            WriteUInt16BigEndian(writer, (ushort)value);
        }

        /// <summary>Write a 32-bit signed 2DA memory reference (CONSTI instruction).</summary>
        private void Write2DAMemory32(System.IO.BinaryWriter writer, PatcherMemory memory, PatchLogger logger, string sourcefile)
        {
            // Can be null if not found
            if (!memory.Memory2DA.TryGetValue(TokenIdOrValue, out string memoryVal))
            {
                throw new KeyNotFoundException($"2DAMEMORY{TokenIdOrValue} was not defined before use");
            }
            if (memoryVal != null && (memoryVal.Contains('/') || memoryVal.Contains('\\')))
            {
                throw new InvalidOperationException($"Memory value cannot be !FieldPath in [HACKList] patches, got '{memoryVal}'");
            }
            int value = int.Parse(memoryVal ?? "0");
            logger.AddVerbose($"HACKList {sourcefile}: writing signed DWORD (32-bit) {value} at offset {Offset:#X}");
            WriteInt32BigEndian(writer, value);
        }

        /// <summary>Write a 32-bit unsigned integer literal.</summary>
        private void WriteUInt32(System.IO.BinaryWriter writer, PatchLogger logger, string sourcefile)
        {
            int value = TokenIdOrValue;
            logger.AddVerbose($"HACKList {sourcefile}: writing unsigned DWORD (32-bit) {value} at offset {Offset:#X}");
            WriteUInt32BigEndian(writer, (uint)value);
        }

        /// <summary>Write a 16-bit unsigned integer literal.</summary>
        private void WriteUInt16(System.IO.BinaryWriter writer, PatchLogger logger, string sourcefile)
        {
            int value = TokenIdOrValue;
            logger.AddVerbose($"HACKList {sourcefile}: writing unsigned WORD (16-bit) {value} at offset {Offset:#X}");
            WriteUInt16BigEndian(writer, (ushort)value);
        }

        /// <summary>Write an 8-bit unsigned integer literal.</summary>
        private void WriteUInt8(System.IO.BinaryWriter writer, PatchLogger logger, string sourcefile)
        {
            int value = TokenIdOrValue;
            logger.AddVerbose($"HACKList {sourcefile}: writing unsigned BYTE (8-bit) {value} at offset {Offset:#X}");
            writer.Write((byte)value);
        }

        private static void WriteUInt16BigEndian(System.IO.BinaryWriter writer, ushort value)
        {
            writer.Write((byte)((value >> 8) & 0xFF));
            writer.Write((byte)(value & 0xFF));
        }

        private static void WriteUInt32BigEndian(System.IO.BinaryWriter writer, uint value)
        {
            writer.Write((byte)((value >> 24) & 0xFF));
            writer.Write((byte)((value >> 16) & 0xFF));
            writer.Write((byte)((value >> 8) & 0xFF));
            writer.Write((byte)(value & 0xFF));
        }

        private static void WriteInt32BigEndian(System.IO.BinaryWriter writer, int value)
        {
            writer.Write((byte)((value >> 24) & 0xFF));
            writer.Write((byte)((value >> 16) & 0xFF));
            writer.Write((byte)((value >> 8) & 0xFF));
            writer.Write((byte)(value & 0xFF));
        }
    }

    /// <summary>
    /// Container for NCS (compiled NWScript) modifications.
    /// 1:1 port from Python ModificationsNCS in pykotor/tslpatcher/mods/ncs.py
    /// </summary>
    public class ModificationsNCS : PatcherModifications
    {
        public List<ModifyNCS> Modifiers { get; set; } = new List<ModifyNCS>();

        public ModificationsNCS(string filename, bool? replace = null, [CanBeNull] List<ModifyNCS> modifiers = null)
            : base(filename, replace)
        {
            Action = "Hack ";
            Modifiers = modifiers ?? new List<ModifyNCS>();
        }

        public override object PatchResource(
            byte[] source,
            PatcherMemory memory,
            PatchLogger logger,
            Game game)
        {
            byte[] ncsBytearray = (byte[])source.Clone();
            Apply(ncsBytearray, memory, logger, game);
            return ncsBytearray;
        }

        /// <summary>
        /// Apply all NCS modifications to the bytecode.
        ///
        /// Args:
        /// ----
        ///     mutable_data: bytearray - The NCS bytecode to modify in-place
        ///     memory: PatcherMemory - Memory context for token lookups
        ///     logger: PatchLogger - Logger for recording operations
        ///     game: Game - The game being patched (unused but required by interface)
        /// </summary>
        public override void Apply(
            object mutableData,
            PatcherMemory memory,
            PatchLogger logger,
            Game game)
        {
            if (mutableData is byte[] ncsBytearray)
            {
                using (var stream = new MemoryStream(ncsBytearray))
                using (var writer = new System.IO.BinaryWriter(stream))
                {
                    foreach (ModifyNCS modifier in Modifiers)
                    {
                        modifier.Apply(writer, memory, logger, SourceFile);
                    }
                }
            }
            else
            {
                logger.AddError($"Expected byte[] for ModificationsNCS, but got {mutableData.GetType().Name}");
            }
        }

        public override void PopTslPatcherVars(
            Dictionary<string, string> fileSectionDict,
            [CanBeNull] string defaultDestination = null,
            [NotNull] string defaultSourceFolder = ".")
        {
            base.PopTslPatcherVars(fileSectionDict, defaultDestination, defaultSourceFolder);
            // Can be null if not found
            if (fileSectionDict.TryGetValue("ReplaceFile", out string replaceFile))
            {
                ReplaceFile = ConvertToBool(replaceFile);
                fileSectionDict.Remove("ReplaceFile");
            }
            // NOTE: tslpatcher's hacklist does NOT prefix with an exclamation point.
        }
    }
}