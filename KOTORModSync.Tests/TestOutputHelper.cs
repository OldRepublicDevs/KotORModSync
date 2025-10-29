// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Diagnostics;

using NUnit.Framework;

namespace KOTORModSync.Tests
{
	/// <summary>
	/// Helper class for providing real-time output during NUnit tests.
	/// This addresses NUnit's output buffering issues by using multiple output methods.
	/// </summary>
	public static class TestOutputHelper
	{
		/// <summary>
		/// Writes a message to multiple output streams for maximum visibility during tests.
		/// This is the recommended method for real-time test output in NUnit.
		/// </summary>
		/// <param name="message">The message to output</param>
		public static void WriteLine( string message )
		{
			if (string.IsNullOrEmpty( message ))
				return;

			try
			{
				// Method 1: TestContext.Progress (should work but often doesn't due to buffering)
				TestContext.Progress.WriteLine( message );
			}
			catch
			{
				// Ignore if TestContext is not available
			}

			try
			{
				// Method 2: TestContext.Error (often more reliable than Progress)
				TestContext.Error.WriteLine( message );
			}
			catch
			{
				// Ignore if TestContext is not available
			}

			try
			{
				// Method 3: Debug.WriteLine (works with proper trace listeners)
				Debug.WriteLine( message );
			}
			catch
			{
				// Ignore if Debug is not available
			}

			try
			{
				// Method 4: Console.Error (often more reliable than Console.Out)
				Console.Error.WriteLine( message );
			}
			catch
			{
				// Ignore if Console.Error is not available
			}

			try
			{
				// Method 5: Console.Out (last resort, often buffered)
				Console.Out.WriteLine( message );
			}
			catch
			{
				// Ignore if Console.Out is not available
			}
		}

		/// <summary>
		/// Writes a formatted message to multiple output streams.
		/// </summary>
		/// <param name="format">The format string</param>
		/// <param name="args">The format arguments</param>
		public static void WriteLine( string format, params object[] args )
		{
			WriteLine( string.Format( format, args ) );
		}

		/// <summary>
		/// Writes a message without a newline to multiple output streams.
		/// </summary>
		/// <param name="message">The message to output</param>
		public static void Write( string message )
		{
			if (string.IsNullOrEmpty( message ))
				return;

			try
			{
				TestContext.Progress.Write( message );
			}
			catch { }

			try
			{
				TestContext.Error.Write( message );
			}
			catch { }

			try
			{
				Debug.Write( message );
			}
			catch { }

			try
			{
				Console.Error.Write( message );
			}
			catch { }

			try
			{
				Console.Out.Write( message );
			}
			catch { }
		}

		/// <summary>
		/// Forces a flush of all output streams to ensure immediate visibility.
		/// </summary>
		public static void Flush()
		{
			try
			{
				Console.Out.Flush();
			}
			catch { }

			try
			{
				Console.Error.Flush();
			}
			catch { }

			try
			{
				// Trace.Flush() is available in modern .NET versions
				Trace.Flush();
			}
			catch { }
		}
	}
}