// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using KOTORModSync.Core;
using KOTORModSync.Core.Utility;

namespace KOTORModSync
{
	public sealed class OutputViewModel : INotifyPropertyChanged
	{
		public readonly Queue<string> _logBuilder = new Queue<string>();
		public string LogText { get; set; } = string.Empty;
		public ObservableCollection<LogLine> LogLines { get; } = new ObservableCollection<LogLine>();

		// used for ui
		public event PropertyChangedEventHandler PropertyChanged;

		public void AppendLog(string message)
		{
			_logBuilder.Enqueue(message);
			LogLines.Add(LogLine.FromMessage(message));
			OnPropertyChanged(nameof( LogText ));
		}

		public void RemoveOldestLog()
		{
			_ = _logBuilder.Dequeue();
			if ( LogLines.Count > 0 )
				LogLines.RemoveAt(0);
			OnPropertyChanged(nameof( LogText ));
		}

		private void OnPropertyChanged(string propertyName)
		{
			LogText = string.Join(Environment.NewLine, _logBuilder);
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	public sealed class LogLine
	{
		public string Timestamp { get; set; }
		public string Message { get; set; }
		public string Level { get; set; }
		public string LevelColor { get; set; }
		public bool IsHighlighted { get; set; }

		public static LogLine FromMessage(string raw)
		{
			string level = "INFO";
			string color = "#00AA00";
			if ( raw?.IndexOf("[Error]", StringComparison.OrdinalIgnoreCase) >= 0 )
			{
				level = "Error";
				color = "#FF4444";
			}
			else if ( raw?.IndexOf("[Warning]", StringComparison.OrdinalIgnoreCase) >= 0 )
			{
				level = "Warning";
				color = "#FFAA00";
			}

			return new LogLine
			{
				Timestamp = DateTime.Now.ToString("HH:mm:ss"),
				Message = raw ?? string.Empty,
				Level = level,
				LevelColor = color,
			};
		}
	}

	public partial class OutputWindow : Window
	{
		private readonly object _logLock = new object();
		private readonly int _maxLinesShown = 150;
		public readonly OutputViewModel _viewModel;

		public OutputWindow()
		{
			InitializeComponent();
			_viewModel = new OutputViewModel();
			DataContext = _viewModel;
			InitializeControls();
			ThemeManager.ApplyCurrentToWindow(this);
			ThemeManager.StyleChanged += OnGlobalStyleChanged;
		}

		private void InitializeControls()
		{
			Logger.Logged += AppendLog;

			Logger.ExceptionLogged += ex =>
			{
				string exceptionLog = $"Exception: {ex.GetType().Name}: {ex.Message}\nStack trace: {ex.StackTrace}";
				AppendLog(exceptionLog);
			};

			string logfileName = $"{Logger.LogFileName}{DateTime.Now:yyyy-MM-dd}";
			string logDir = Path.Combine(Utility.GetBaseDirectory(), "Logs");
			if ( !Directory.Exists(logDir) )
				_ = Directory.CreateDirectory(logDir);
			
			string logFilePath = Path.Combine(logDir, logfileName + ".log");
			if ( !File.Exists(logFilePath) )
				_ = File.Create(logFilePath);

			string[] lines = File.ReadAllLines(logFilePath);
			int startIndex = Math.Max(0, lines.Length - _maxLinesShown);
			foreach ( string line in lines.Skip(startIndex) )
			{
				AppendLog(line);
			}

			LogScrollViewer.ScrollToEnd();
		}

		private void OnGlobalStyleChanged(Uri _) => ThemeManager.ApplyCurrentToWindow(this);

		private async void CopySelected_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				if ( Clipboard is null || this.FindControl<ListBox>("LogListBox") is null )
					return;

				ListBox list = this.FindControl<ListBox>("LogListBox");
				var selected = list.SelectedItems?.OfType<LogLine>()?.ToList();
				if ( selected == null || selected.Count == 0 )
					return;

				string text = string.Join(Environment.NewLine, selected.Select(l => $"[{l.Timestamp}] {l.Message}"));
				await Clipboard.SetTextAsync(text);
			}
			catch ( Exception ex )
			{
				Console.WriteLine($"Failed to copy logs: {ex.Message}");
			}
		}

		private void AppendLog(string message)
		{
			try
			{
				lock ( _logLock )
				{
					if ( _viewModel._logBuilder.Count >= _maxLinesShown )
					{
						_viewModel.RemoveOldestLog();
					}

					_viewModel.AppendLog(message);
					LogLine last = _viewModel.LogLines.LastOrDefault();
					if ( last != null )
						last.IsHighlighted = last.Level == "Error" || last.Level == "Warning";
				}

				// Scroll to the end of the content
				_ = Dispatcher.UIThread.InvokeAsync(() => LogScrollViewer.ScrollToEnd());
			}
			catch ( Exception ex )
			{
				Console.WriteLine($"An error occurred appending the log to the output window: '{ex.Message}'");
			}
		}

		private void LogListBox_KeyDown(object sender, Avalonia.Input.KeyEventArgs e)
		{
			try
			{
				if ( !(sender is ListBox list) )
					return;
				if ( e.Key == Avalonia.Input.Key.H )
				{
					foreach ( LogLine line in list.SelectedItems?.OfType<LogLine>() ?? Enumerable.Empty<LogLine>() )
					{
						line.IsHighlighted = !line.IsHighlighted;
					}
				}
			}
			catch { }
		}

		private void ClearLog_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				lock ( _logLock )
				{
					_viewModel._logBuilder.Clear();
					_viewModel.AppendLog(string.Empty);
					_viewModel.LogLines.Clear();
				}

				_ = Dispatcher.UIThread.InvokeAsync(() =>
				{
					LogScrollViewer.Offset = new Avalonia.Vector(0, 0);
				});
			}
			catch ( Exception ex )
			{
				Console.WriteLine($"Failed to clear logs: {ex.Message}");
			}
		}

		private async void SaveLog_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				var saveOptions = new FilePickerSaveOptions
				{
					SuggestedFileName = $"KOTORModSync_Log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log",
					ShowOverwritePrompt = true,
					FileTypeChoices = new[] { FilePickerFileTypes.All, FilePickerFileTypes.TextPlain },
				};

				IStorageFile file = await StorageProvider.SaveFilePickerAsync(saveOptions);
				string filePath = file?.TryGetLocalPath();
				if ( string.IsNullOrEmpty(filePath) )
					return;

				await File.WriteAllTextAsync(filePath, _viewModel.LogText ?? string.Empty);
			}
			catch ( Exception ex )
			{
				Console.WriteLine($"Failed to save logs: {ex.Message}");
			}
		}
	}
}
