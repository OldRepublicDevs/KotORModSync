// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.


using System.Diagnostics;
using KOTORModSync.Core;
using KOTORModSync.Core.Utility;
using ThreadState = System.Threading.ThreadState;

namespace KOTORModSync.Tests
{
	[TestFixture]
	public class CommandExecutorTests
	{
		private static void ExecuteCommand(
			string command,
			EventWaitHandle completed,
			IDictionary<string, object> sharedData
		)
		{
			try
			{
				Logger.Log("Testing TryExecuteCommand...");
				(int exitCode, string output, string error) = PlatformAgnosticMethods.TryExecuteCommand(command);
				sharedData["success"] = exitCode == 0;
				sharedData["output"] = output;
				sharedData["error"] = error;
			}
			catch ( Exception ex ) when ( ex is TimeoutException )
			{
				Logger.Log(
					"The test timed out. Make sure the command execution is completing within the expected time."
				);
				Logger.Log("Here are the currently running processes on the machine:");
				Process[] processes = Process.GetProcesses();
				foreach ( Process process in processes )
				{
					Logger.Log($"'{process.ProcessName}' (ID: {process.Id})");
				}

				Logger.Log("Standard output from the timed-out process:");
				(int _, string output, string _) = PlatformAgnosticMethods.TryExecuteCommand("echo");
				Logger.Log(output);
			}
			finally
			{
				_ = completed.Set();
			}
		}

		[Test]
		[CancelAfter(10000)]
		public void TryExecuteCommand_ShouldReturnSuccessAndOutput()
		{

			const string command = "echo Hello, Windows!";
			const string expectedOutput = "Hello, Windows!";

			var completed = new ManualResetEvent(false);
			var sharedData = new Dictionary<string, object>();

			var thread = new Thread(() => ExecuteCommand(command, completed, sharedData));
			thread.Start();

			if ( !completed.WaitOne(11000) )
			{
				Logger.Log("The test did not complete within the expected time.");
				Logger.Log("The test thread is still running.");

				thread.Interrupt();

				thread.Join();

			}
			else if ( thread.ThreadState != ThreadState.Stopped )
			{

				Logger.Log("The test thread is still running.");
			}

			bool success = (bool)sharedData["success"];
			string output = (string)sharedData["output"];
			Assert.Multiple(
				() =>
				{
					Assert.That(success);
					Assert.That(output.Trim(), Is.EqualTo(expectedOutput));
				}
			);
		}

		[Test]
		public void GetAvailableMemory_ShouldReturnNonZero_OnSupportedPlatform()
		{

			long availableMemory = PlatformAgnosticMethods.GetAvailableMemory();

			Assert.That(availableMemory, Is.GreaterThan(0));
		}
	}
}
