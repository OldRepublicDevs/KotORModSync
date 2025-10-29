// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.
using System;

using KOTORModSync.Core.CLI;
namespace KOTORModSync.Core
{
	public class Program
	{
		public static int Main( string[] args )
		{
			try
			{
				return ModBuildConverter.Run( args );
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine( $"Fatal error: {ex.Message}" );
				Console.Error.WriteLine( ex.StackTrace );
				return 1;
			}
		}
	}
}