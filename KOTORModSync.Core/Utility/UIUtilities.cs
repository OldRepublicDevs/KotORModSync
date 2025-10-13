// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;
using System.Threading.Tasks;
using JetBrains.Annotations;
using KOTORModSync.Core.Services;

namespace KOTORModSync.Core.Utility
{
	/// <summary>
	/// Utility methods for UI-related operations that don't depend on specific UI frameworks.
	/// </summary>
	public static class UIUtilities
	{
		/// <summary>
		/// Fixes iOS case sensitivity issues by renaming files and folders to lowercase.
		/// </summary>
		/// <param name="gameDirectory">The game directory to process.</param>
		/// <returns>The number of objects renamed, or -1 if an error occurred.</returns>
		/// <exception cref="ArgumentNullException">Thrown when gameDirectory is null.</exception>
		public static async Task<int> FixIOSCaseSensitivity([NotNull] DirectoryInfo gameDirectory)
		{
			if ( gameDirectory == null )
				throw new ArgumentNullException(nameof(gameDirectory));

			var fileOperationService = new FileOperationService();
			return await FileOperationService.FixIOSCaseSensitivityAsync(gameDirectory.FullName);
		}
	}
}