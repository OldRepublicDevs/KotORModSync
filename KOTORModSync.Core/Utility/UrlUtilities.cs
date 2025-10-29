// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

using JetBrains.Annotations;

namespace KOTORModSync.Core.Utility
{

	public static class UrlUtilities
	{



		public static void OpenUrl( [NotNull] string url )
		{
			try
			{
				if (string.IsNullOrEmpty( url ))
					throw new ArgumentException( message: "Value cannot be null or empty.", nameof( url ) );
				if (!Uri.TryCreate( url, UriKind.Absolute, out Uri _ ))
					throw new InvalidOperationException( $"Invalid URL: '{url}'" );

				OSPlatform runningOs = UtilityHelper.GetOperatingSystem();
				if (runningOs == OSPlatform.Windows)
				{
					_ = Process.Start( new ProcessStartInfo { FileName = url, UseShellExecute = true } );
				}
				else if (runningOs == OSPlatform.OSX)
				{
					_ = Process.Start( fileName: "open", url );
				}
				else if (runningOs == OSPlatform.Linux)
				{
					_ = Process.Start( fileName: "xdg-open", url );
				}
			}
			catch (Exception e)
			{
				Logger.LogException( e, customMessage: $"Failed to open URL: {url}" );
			}
		}
	}
}