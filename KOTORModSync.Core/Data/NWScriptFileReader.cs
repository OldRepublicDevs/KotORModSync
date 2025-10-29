// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.IO;

using JetBrains.Annotations;

namespace KOTORModSync.Core.Data
{

	public static class NWScriptFileReader
	{

		public static void ReadHeader( [NotNull] Stream stream, out NWScriptHeader header )
		{
			BinaryReader reader = new BinaryReader( stream );

			header.FileType = reader.ReadUInt32();
			header.Version = reader.ReadUInt32();
			header.Language = reader.ReadUInt32();
			header.NumVariables = reader.ReadUInt32();
			header.CodeSize = reader.ReadUInt32();
			header.NumFunctions = reader.ReadUInt32();
			header.NumActions = reader.ReadUInt32();
			header.NumConstants = reader.ReadUInt32();
			header.SymbolTableSize = reader.ReadUInt32();
			header.SymbolTableOffset = reader.ReadUInt32();
		}
	}
}