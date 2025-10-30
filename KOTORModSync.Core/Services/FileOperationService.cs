// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;
using System.Threading.Tasks;

using JetBrains.Annotations;

using KOTORModSync.Core.FileSystemUtils;

namespace KOTORModSync.Core.Services
{
	public class FileOperationService
	{
		public static async Task<int> FixIOSCaseSensitivityAsync( [NotNull] string folderPath )
		{
			if (string.IsNullOrEmpty( folderPath ))
				throw new ArgumentException( "Folder path cannot be null or empty", nameof( folderPath ) );

			DirectoryInfo directory = new DirectoryInfo( folderPath );
			if (!directory.Exists)

			{
				await Logger.LogErrorAsync( $"Directory not found: '{directory.FullName}', skipping..." ).ConfigureAwait( false );
				return 0;
			}

			return await FixIOSCaseSensitivityCoreAsync( directory ).ConfigureAwait( false );
		}

		private static async Task<int> FixIOSCaseSensitivityCoreAsync( [NotNull] DirectoryInfo gameDirectory )
		{
			try
			{
				int numObjectsRenamed = 0;

				foreach (FileInfo file in gameDirectory.GetFilesSafely())
				{
					string lowercaseName = file.Name.ToLowerInvariant();
					string dirName = file.DirectoryName;
					if (dirName is null)
						continue;

					string lowercasePath = Path.Combine( dirName, lowercaseName );
					if (!string.Equals( lowercasePath, file.FullName, StringComparison.Ordinal ))

					{
						await Logger.LogAsync( $"Rename file '{file.FullName}' -> '{lowercasePath}'" ).ConfigureAwait( false );
						File.Move( file.FullName, lowercasePath );
						numObjectsRenamed++;
					}
				}

				foreach (DirectoryInfo directory in gameDirectory.GetDirectoriesSafely())
				{
					string lowercaseName = directory.Name.ToLowerInvariant();
					string dirParentPath = directory.Parent?.FullName;
					if (dirParentPath is null)
						continue;

					string lowercasePath = Path.Combine( dirParentPath, lowercaseName );
					if (!string.Equals( lowercasePath, directory.FullName, StringComparison.Ordinal ))

					{
						await Logger.LogAsync( $"Rename folder '{directory.FullName}' -> '{lowercasePath}'" )

.ConfigureAwait( false );
						Directory.Move( directory.FullName, lowercasePath );
						numObjectsRenamed++;

						numObjectsRenamed += await FixIOSCaseSensitivityCoreAsync( new DirectoryInfo( lowercasePath ) )

.ConfigureAwait( false );
					}
					else
					{

						await Logger.LogAsync( $"Recursing into folder '{directory.FullName}'..." ).ConfigureAwait( false );
						numObjectsRenamed += await FixIOSCaseSensitivityCoreAsync( directory ).ConfigureAwait( false );
					}
				}

				return numObjectsRenamed;
			}
			catch (Exception exception)

			{
				await Logger.LogExceptionAsync( exception ).ConfigureAwait( false );
				return -1;
			}
		}
	}
}