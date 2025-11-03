// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.


using System.Runtime.InteropServices;

namespace KOTORModSync.Core.Data
{
    [StructLayout(LayoutKind.Auto)]
    public struct NWScriptHeader
    {
        public uint FileType;
        public uint Version;
        public uint Language;
        public uint NumVariables;
        public uint CodeSize;
        public uint NumFunctions;
        public uint NumActions;
        public uint NumConstants;
        public uint SymbolTableSize;
        public uint SymbolTableOffset;
    }
}
