



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