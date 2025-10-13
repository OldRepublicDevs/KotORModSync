// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Collections.Generic;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedType.Global

namespace KOTORModSync.Core
{
	public static class ModDirectory
	{
		public class ArchiveEntry
		{
			public string Name { get; set; }
			public string Path { get; set; }
		}

		public class ZipTree
		{
			public string Filename { get; set; }
			public string Name { get; set; }
			public bool IsFile { get; set; }
			public List<ZipTree> Children { get; set; } = new List<ZipTree>();
		}
	}
}
