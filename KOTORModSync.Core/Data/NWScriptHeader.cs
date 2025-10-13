





namespace KOTORModSync.Core.Data
{
	
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
