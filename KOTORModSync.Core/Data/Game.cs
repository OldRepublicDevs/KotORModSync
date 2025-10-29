// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Collections.Generic;

namespace KOTORModSync.Core.Data
{
	public static class Game
	{
		public static readonly List<string> TextureOverridePriorityList = new List<string>
		{
			".dds", ".tpc", ".tga",
		};
	}
}