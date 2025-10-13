



using System;
using System.IO;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Utility
{
	
	
	
	public static class FileUtilities
	{
		
		
		
		
		
		
		
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
