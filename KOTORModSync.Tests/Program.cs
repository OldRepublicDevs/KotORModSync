// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

namespace KOTORModSync.Tests
{
	/// <summary>
	/// CLI entry point for test utilities
	/// </summary>
	public static class Program
	{
		public static int Main(string[] args)
		{
			// Delegate to the converter CLI
			return MarkdownToTomlConverter.Run(args);
		}
	}
}

