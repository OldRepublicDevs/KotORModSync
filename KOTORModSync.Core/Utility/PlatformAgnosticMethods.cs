// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using KOTORModSync.Core.FileSystemUtils;

namespace KOTORModSync.Core.Utility
{
	public static class PlatformAgnosticMethods
	{
		[StructLayout(LayoutKind.Sequential)]
		private struct MemoryStatusEx
		{
			public uint dwLength;
			public uint dwMemoryLoad;
			public ulong ullTotalPhys;
			public ulong ullAvailPhys;
			public ulong ullTotalPageFile;
			public ulong ullAvailPageFile;
			public ulong ullTotalVirtual;
			public ulong ullAvailVirtual;
			public ulong ullAvailExtendedVirtual;
		}

		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

		public static long GetAvailableMemory()
		{

			if ( Utility.GetOperatingSystem() == OSPlatform.Windows )
			{
				try
				{
					var memStatus = new MemoryStatusEx
					{
						dwLength = (uint)Marshal.SizeOf(typeof(MemoryStatusEx))
					};
					if ( GlobalMemoryStatusEx(ref memStatus) ) return (long)memStatus.ullAvailPhys;
				}
				catch ( Exception )
				{

				}
			}

			(int ExitCode, string Output, string Error) result = TryExecuteCommand("sysctl -n hw.memsize");
			string command = "sysctl";

			if ( result.ExitCode == 0 )
				return ParseAvailableMemory(result.Output, command);

			result = TryExecuteCommand("grep MemAvailable /proc/meminfo | awk '{print $2*1024}'\r\n");
			if ( long.TryParse(result.Output.TrimEnd(Environment.NewLine.ToCharArray()), out long longValue) )
				return longValue;

			result = TryExecuteCommand("free -b");
			command = "free";

			if ( result.ExitCode == 0 )
				return ParseAvailableMemory(result.Output, command);

			result = TryExecuteCommand("wmic OS get FreePhysicalMemory");
			command = "wmic";

			return result.ExitCode == 0
				? ParseAvailableMemory(result.Output, command)
				: 0;
		}

		private static long ParseAvailableMemory([NotNull] string output, [NotNull] string command)
		{
			if ( string.IsNullOrWhiteSpace(output) )
				throw new ArgumentException(message: "Value cannot be null or whitespace.", nameof(output));

			if ( string.IsNullOrWhiteSpace(command) )
				throw new ArgumentException(message: "Value cannot be null or whitespace.", nameof(command));

			string pattern = string.Empty;
			switch ( command.ToLowerInvariant() )
			{
				case "sysctl":
					pattern = @"\d+(\.\d+)?";
					break;
				case "free":
					pattern = @"Mem:\s+\d+\s+\d+\s+(\d+)";
					break;
				case "wmic":
					pattern = @"\d+";
					break;
			}

			Match match = Regex.Match(output, pattern);
			return match.Success && long.TryParse(match.Value, out long memory)
				? memory
				: 0;
		}

		public static (int ExitCode, string Output, string Error) TryExecuteCommand([CanBeNull] string command)
		{
			string shellPath = GetShellExecutable();
			if ( string.IsNullOrEmpty(shellPath) )
				return (-1, string.Empty, "Unable to retrieve shell executable path.");

			try
			{
				using ( new Process() )
				{
					string args = Utility.GetOperatingSystem() == OSPlatform.Windows
						? $"/c \"{command}\""
						: $"-c \"{command}\"";
					Task<(int, string, string)> executeProcessTask = ExecuteProcessAsync(shellPath, args);
					executeProcessTask.Wait();
					return executeProcessTask.Result;
				}
			}
			catch ( Exception ex )
			{
				return (-2, string.Empty, $"Command execution failed: {ex.Message}");
			}
		}

		[NotNull]
		public static string GetShellExecutable()
		{
			string[] shellExecutables =
			{
				"cmd.exe", "powershell.exe", "sh", "bash", "/bin/sh", "/usr/bin/sh", "/usr/local/bin/sh", "/bin/bash",
				"/usr/bin/bash", "/usr/local/bin/bash",
			};

			foreach ( string executable in shellExecutables )
			{
				if ( File.Exists(executable) )
					return executable;

				string fullExecutablePath = Path.Combine(Environment.SystemDirectory, executable);
				if ( File.Exists(fullExecutablePath) )
					return fullExecutablePath;
			}

			return string.Empty;
		}

		public static bool? IsExecutorAdmin()
		{
			if ( Utility.GetOperatingSystem() == OSPlatform.Windows )
			{
#pragma warning disable CA1416 
				var windowsIdentity = WindowsIdentity.GetCurrent();
				var windowsPrincipal = new WindowsPrincipal(windowsIdentity);
				return windowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator);
#pragma warning restore CA1416 
			}

			try
			{

				int effectiveUserId = (int)Interop.geteuid();
				return effectiveUserId == 0;
			}
			catch ( DllNotFoundException )
			{

				var process = new Process
				{
					StartInfo =
					{
						FileName = "sudo",
						Arguments = "-n true",
						UseShellExecute = false,
						RedirectStandardOutput = true,
						CreateNoWindow = true
					},
				};

				try
				{
					_ = process.Start();
					process.WaitForExit();
					return process.ExitCode == 0;
				}
				catch
				{

					return null;
				}
			}
		}

		public static async Task MakeExecutableAsync([NotNull] FileSystemInfo fileOrApp)
		{
			if ( Utility.GetOperatingSystem() == OSPlatform.Windows )
			{
				await FilePermissionHelper.FixPermissionsAsync(fileOrApp);
				return;
			}

			if ( fileOrApp is null )
				throw new ArgumentNullException(nameof(fileOrApp));

			if ( !fileOrApp.Exists && MainConfig.CaseInsensitivePathing )
				fileOrApp = PathHelper.GetCaseSensitivePath(fileOrApp);
			if ( !fileOrApp.Exists )
				throw new FileNotFoundException($"The file/app '{fileOrApp}' does not exist.");

			await Task.Run(
				() =>
				{
					var startInfo = new ProcessStartInfo
					{
						FileName = "chmod",
						Arguments = $"u+x \"{fileOrApp}\"",
						RedirectStandardOutput = true,
						RedirectStandardError = true,
						UseShellExecute = false,
						CreateNoWindow = true
					};

					using ( var process = Process.Start(startInfo) )
					{
						if ( process == null )
							throw new InvalidOperationException("Failed to start chmod process.");

						process.WaitForExit();

						if ( process.ExitCode != 0 )
						{
							throw new InvalidOperationException(
								$"chmod failed with exit code {process.ExitCode}: {process.StandardError.ReadToEnd()}"
							);
						}
					}
				}
			);
		}

		private static List<ProcessStartInfo> GetProcessStartInfo(
			[CanBeNull] string programFile,
			string args = "",
			bool askAdmin = false,
			bool? useShellExecute = null,
			bool hideProcess = true
		)
		{
			if ( programFile is null )
				throw new ArgumentNullException(nameof(programFile));
			string verb = null;
			string actualProgramFile = programFile;
			string actualArgs = args;
			ProcessWindowStyle windowStyle = ProcessWindowStyle.Hidden;
			bool createNoWindow = true;

			if ( askAdmin && !MainConfig.NoAdmin )
			{
				if ( Utility.GetOperatingSystem() == OSPlatform.Windows )
				{
					verb = "runas";
				}
				else
				{
					actualProgramFile = "sudo";
					string tempFile = programFile.Trim('"').Trim('\'');
					actualArgs = $"\"{tempFile}\" {args}";
					if ( MainConfig.NoAdmin )
						actualArgs = $"-n true {actualArgs}";
				}
			}

			if ( hideProcess is false )
			{
				windowStyle = ProcessWindowStyle.Normal;
				createNoWindow = false;
			}

			var sameShellStartInfo = new ProcessStartInfo
			{
				FileName = actualProgramFile,
				Arguments = actualArgs,
				UseShellExecute = false,
				CreateNoWindow = createNoWindow,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				RedirectStandardInput = true,
				ErrorDialog = false,
				WindowStyle = windowStyle,
			};

			var shellExecuteStartInfo = new ProcessStartInfo
			{
				FileName = actualProgramFile,
				Arguments = actualArgs,
				UseShellExecute = true,
				CreateNoWindow = createNoWindow,
				ErrorDialog = false,
				WindowStyle = windowStyle,
				Verb = verb
			};

			return useShellExecute is true || askAdmin
				? new List<ProcessStartInfo> { shellExecuteStartInfo }
				: new List<ProcessStartInfo> { sameShellStartInfo, shellExecuteStartInfo };
		}

		public static async Task<(int, string, string)> ExecuteProcessAsync(
			[NotNull] string programFile,
			string args = "",
			long timeout = 0,
			bool askAdmin = false,
			bool? useShellExecute = null,
			bool hideProcess = true,
			bool noLogging = false
		)
		{
			if ( programFile is null )
				throw new ArgumentNullException(nameof(programFile));

			List<ProcessStartInfo> processStartInfos = GetProcessStartInfo(
				programFile: programFile,
				args: args,
				askAdmin: askAdmin,
				useShellExecute: useShellExecute,
				hideProcess: hideProcess
			);

			Exception ex = null;
			bool? isAdmin = IsExecutorAdmin();
			for ( int index = 0; index < processStartInfos.Count; index++ )
			{
				ProcessStartInfo startInfo = processStartInfos[index];
				try
				{
					TimeSpan? thisTimeout = timeout != 0
						? TimeSpan.FromMilliseconds(timeout)
						: (TimeSpan?)null;
					CancellationTokenSource cancellationTokenSource = thisTimeout.HasValue
						? new CancellationTokenSource(thisTimeout.Value)
						: new CancellationTokenSource();
					using ( cancellationTokenSource )
					using ( var process = new Process() )
					{
						if ( MainConfig.NoAdmin && !startInfo.UseShellExecute )
							startInfo.EnvironmentVariables["__COMPAT_LAYER"] = "RunAsInvoker";

						process.StartInfo = startInfo;

						if ( timeout > 0 )
						{
							Process localProcess = process;

							void Callback()
							{
								try
								{
									if ( localProcess.HasExited )
										return;

									if ( !localProcess.CloseMainWindow() )
										localProcess.Kill();
								}
								catch ( Exception cancellationException )
								{
									Logger.LogException(cancellationException);
								}
							}

							_ = cancellationTokenSource.Token.Register(Callback);
						}

						var output = new StringBuilder();
						var error = new StringBuilder();

						using ( var outputWaitHandle = new AutoResetEvent(initialState: false) )
						using ( var errorWaitHandle = new AutoResetEvent(initialState: false) )
						{
							process.OutputDataReceived += (sender, e) =>
							{
								try
								{
									if ( e?.Data is null )
									{
										try
										{

											_ = outputWaitHandle.Set();
										}
										catch ( ObjectDisposedException )
										{
											Logger.LogVerbose("outputWaitHandle is already disposed, nothing to set.");
										}

										return;
									}

									_ = output.AppendLine(e.Data);
									if ( noLogging is false )
										_ = Logger.LogAsync(e.Data);
								}
								catch ( Exception exception )
								{
									_ = Logger.LogExceptionAsync(exception, $"Exception while gathering the output from '{programFile}'");
								}
							};
							process.ErrorDataReceived += (sender, e) =>
							{
								try
								{
									if ( e?.Data is null )
									{
										try
										{

											_ = errorWaitHandle.Set();
										}
										catch ( ObjectDisposedException )
										{
											Logger.LogVerbose("errorWaitHandle is already disposed, nothing to set.");
										}

										return;
									}

									_ = error.AppendLine(e.Data);
									_ = Logger.LogErrorAsync(e.Data);
								}
								catch ( Exception exception )
								{
									_ = Logger.LogExceptionAsync(exception, $"Exception while gathering the error output from '{programFile}'");
								}
							};

							if ( !process.Start() )
								throw new InvalidOperationException("Failed to start the process.");

							if ( process.StartInfo.RedirectStandardOutput )
								process.BeginOutputReadLine();
							if ( process.StartInfo.RedirectStandardError )
								process.BeginErrorReadLine();

							_ = await Task.Run(
								() =>
								{
									try
									{
										process.WaitForExit();
										return (process.ExitCode, output.ToString(), error.ToString());
									}
									catch ( Exception exception )
									{
										Logger.LogException(exception, customMessage: "Exception while running the process.");
										return (-3, null, null);
									}
								},
								cancellationTokenSource.Token
							);
						}

						return timeout > 0 && cancellationTokenSource.Token.IsCancellationRequested
							? throw new TimeoutException("Process timed out")
							: ((int, string, string))(process.ExitCode, output.ToString(), error.ToString());
					}
				}
				catch ( Win32Exception localException )
				{
					await Logger.LogVerboseAsync(
						$"Exception occurred for startInfo: '{startInfo}', attempting to use different parameters"
					);
					if ( !MainConfig.NoAdmin && isAdmin is true )
					{
						if ( Utility.GetOperatingSystem() == OSPlatform.Windows && startInfo.UseShellExecute && !startInfo.Verb.Equals("runas", StringComparison.InvariantCultureIgnoreCase) )
						{
							startInfo.Verb = "runas";
							index--;
						}
						else if ( Utility.GetOperatingSystem() != OSPlatform.Windows && !startInfo.FileName.Equals("sudo", StringComparison.InvariantCultureIgnoreCase) )
						{
							startInfo.FileName = "sudo";
							string tempFile = programFile.Trim('"').Trim('\'');
							startInfo.Arguments = $"\"{tempFile}\" {args}";
							index--;
						}
					}

					if ( !MainConfig.DebugLogging )
						continue;

					await Logger.LogExceptionAsync(localException);
					ex = localException;
				}
				catch ( Exception startinfoException )
				{
					await Logger.LogAsync($"An unplanned error has occurred trying to run '{programFile}'");
					await Logger.LogExceptionAsync(startinfoException);
					return (-2, string.Empty, string.Empty);
				}
			}

			await Logger.LogAsync("Process failed to start with all possible combinations of arguments.");
			await Logger.LogExceptionAsync(ex ?? new InvalidOperationException());
			return (-1, string.Empty, string.Empty);
		}

		private static class Interop
		{
			[DllImport(dllName: "libc")]
			public static extern uint geteuid();
		}
	}
}
