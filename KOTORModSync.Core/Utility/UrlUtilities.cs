



using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Utility
{
	
	
	
	public static class UrlUtilities
	{
		
		
		
		
		
		
		public static void OpenUrl([NotNull] string url)
		{
			try
			{
				if ( string.IsNullOrEmpty(url) )
					throw new ArgumentException(message: "Value cannot be null or empty.", nameof(url));
				if ( !Uri.TryCreate(url, UriKind.Absolute, out Uri _) )
					throw new InvalidOperationException($"Invalid URL: '{url}'");

				OSPlatform runningOs = Utility.GetOperatingSystem();
				if ( runningOs == OSPlatform.Windows )
				{
					_ = Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
				}
				else if ( runningOs == OSPlatform.OSX )
				{
					_ = Process.Start(fileName: "open", url);
				}
				else if ( runningOs == OSPlatform.Linux )
				{
					_ = Process.Start(fileName: "xdg-open", url);
				}
			}
			catch ( Exception e )
			{
				Logger.LogException(e, customMessage: $"Failed to open URL: {url}");
			}
		}
	}
}
