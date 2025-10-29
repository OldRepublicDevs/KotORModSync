// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Diagnostics;

using NUnit.Framework;

namespace KOTORModSync.Tests
{
	/// <summary>
	/// Global test setup fixture that configures output for real-time visibility.
	/// This addresses NUnit's output buffering issues by setting up proper trace listeners.
	/// </summary>
	[SetUpFixture]
	public class TestSetupFixture
	{
		[OneTimeSetUp]
		public void OneTimeSetUp()
		{
			// Configure trace listeners for better output visibility
			ConfigureTraceListeners();

			// Output initial setup message
			TestOutputHelper.WriteLine( "=== TEST SETUP COMPLETE ===" );
			TestOutputHelper.WriteLine( $"Test execution started at: {DateTime.Now}" );
			TestOutputHelper.WriteLine( "Real-time output is now configured for maximum visibility" );
			TestOutputHelper.WriteLine( "=== END TEST SETUP ===" );
		}

		[OneTimeTearDown]
		public void OneTimeTearDown()
		{
			// Output final teardown message
			TestOutputHelper.WriteLine( "=== TEST TEARDOWN ===" );
			TestOutputHelper.WriteLine( $"Test execution completed at: {DateTime.Now}" );
			TestOutputHelper.WriteLine( "=== END TEST TEARDOWN ===" );

			// Flush all output
			TestOutputHelper.Flush();
		}

		/// <summary>
		/// Configures trace listeners to enable real-time output from Debug.WriteLine and Trace.WriteLine.
		/// </summary>
		private static void ConfigureTraceListeners()
		{
			try
			{
				// For .NET Core/.NET 5+, we need to use Trace.Listeners only
				// Debug.Listeners is not available in modern .NET versions
				if (!HasTraceListener<ConsoleTraceListener>())
				{
					Trace.Listeners.Add( new ConsoleTraceListener() );
				}

				// Set console encoding to UTF-8 for proper character display
				try
				{
					Console.OutputEncoding = System.Text.Encoding.UTF8;
				}
				catch
				{
					// Ignore if encoding can't be set
				}
			}
			catch (Exception ex)
			{
				// If trace listener setup fails, just continue
				// We'll fall back to other output methods
				Console.WriteLine( $"Warning: Could not configure trace listeners: {ex.Message}" );
			}
		}

		/// <summary>
		/// Checks if a specific type of trace listener is already configured.
		/// </summary>
		/// <typeparam name="T">The type of trace listener to check for</typeparam>
		/// <returns>True if the listener type is already configured</returns>
		private static bool HasTraceListener<T>() where T : TraceListener
		{
			foreach (TraceListener listener in Trace.Listeners)
			{
				if (listener is T)
					return true;
			}
			return false;
		}
	}
}