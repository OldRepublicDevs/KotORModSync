// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Utility
{
	/// <summary>
	/// Utility methods for working with files.
	/// </summary>
	public static class FileUtilities
	{
		/// <summary>
		/// Saves documentation content to a file asynchronously.
		/// </summary>
		/// <param name="filePath">The path where to save the file.</param>
		/// <param name="documentation">The documentation content to save.</param>
		/// <returns>A task representing the asynchronous operation.</returns>
		/// <exception cref="ArgumentNullException">Thrown when filePath or documentation is null.</exception>
		public static async Task SaveDocsToFileAsync([NotNull] string filePath, [NotNull] string documentation)
		{
			if ( filePath is null )
				throw new ArgumentNullException(nameof(filePath));
			if ( documentation is null )
				throw new ArgumentNullException(nameof(documentation));

			try
			{
				if ( !string.IsNullOrEmpty(documentation) )
				{
					using ( var writer = new StreamWriter(filePath) )
					{
						await writer.WriteAsync(documentation);
						await writer.FlushAsync();
						// ReSharper disable once MethodHasAsyncOverload
						// not available in net462
						writer.Dispose();
					}
				}
			}
			catch ( Exception e )
			{
				await Logger.LogExceptionAsync(e);
			}
		}
	}
}
