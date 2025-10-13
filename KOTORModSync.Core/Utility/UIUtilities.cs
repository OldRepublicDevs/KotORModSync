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

	public static class UIUtilities
	{



		public static async Task<int> FixIOSCaseSensitivity([NotNull] DirectoryInfo gameDirectory)
		{
			if ( gameDirectory == null )
				throw new ArgumentNullException(nameof(gameDirectory));

			var fileOperationService = new FileOperationService();
			return await FileOperationService.FixIOSCaseSensitivityAsync(gameDirectory.FullName);
		}
	}
}