// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using JetBrains.Annotations;
using KOTORModSync.Core;

namespace KOTORModSync
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0048:File name must match type name", Justification = "<Pending>")]
    public sealed class OutputViewModel : INotifyPropertyChanged
	{
		public readonly Queue<string> _logBuilder = new Queue<string>();
		public string LogText { get; set; } = string.Empty;
		public ObservableCollection<LogLine> LogLines { get; } = new ObservableCollection<LogLine>();

		public event PropertyChangedEventHandler PropertyChanged;

		public void AppendLog(string message)
		{
			_logBuilder.Enqueue(message);
			LogLines.Add(LogLine.FromMessage(message));
			OnPropertyChanged(nameof(LogText));
		}

		public void RemoveOldestLog()
		{
			_ = _logBuilder.Dequeue();
			if (LogLines.Count > 0)
				LogLines.RemoveAt(0);
			OnPropertyChanged(nameof(LogText));
		}

		private void OnPropertyChanged(string propertyName)
		{
			LogText = string.Join(Environment.NewLine, _logBuilder);
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0048:File name must match type name", Justification = "<Pending>")]
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
			if (raw?.IndexOf("[Error]", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				level = "Error";
				color = "#FF4444";
			}
			else if (raw?.IndexOf("[Warning]", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				level = "Warning";
				color = "#FFAA00";
			}

			return new LogLine
			{
				Timestamp = DateTime.Now.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
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
		private bool _mouseDownForWindowMoving;
		private PointerPoint _originalPoint;

		public OutputWindow()
		{
			InitializeComponent();
			_viewModel = new OutputViewModel();
			DataContext = _viewModel;
			InitializeControls();
			ThemeManager.ApplyCurrentToWindow(this);
			ThemeManager.StyleChanged += OnGlobalStyleChanged;

			PointerPressed += InputElement_OnPointerPressed;
			PointerMoved += InputElement_OnPointerMoved;
			PointerReleased += InputElement_OnPointerReleased;
			PointerExited += InputElement_OnPointerReleased;
		}

		private void InitializeControls()
		{
			Logger.Logged += AppendLog;

			Logger.ExceptionLogged += ex =>
			{
				string exceptionLog = $"Exception: {ex.GetType().Name}: {ex.Message}\nStack trace: {ex.StackTrace}";
				AppendLog(exceptionLog);
			};

			// Load existing logs from NLog memory target instead of reading file directly
			// This avoids file access conflicts with NLog's file target
			try
			{
				var recentLogs = Logger.GetRecentLogMessages(_maxLinesShown);
				foreach (string logMessage in recentLogs)
				{
					AppendLog(logMessage);
				}
			}
			catch (Exception ex)
			{
				// Handle any errors loading from memory target
				AppendLog($"[Warning] Could not load existing logs from memory: {ex.Message}");
			}

			LogScrollViewer.ScrollToEnd();
		}

		private void OnGlobalStyleChanged(Uri _) => ThemeManager.ApplyCurrentToWindow(this);

		private async void CopySelected_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				if (Clipboard is null || this.FindControl<ListBox>("LogListBox") is null)
					return;

				ListBox list = this.FindControl<ListBox>("LogListBox");
				var selected = list.SelectedItems?.OfType<LogLine>().ToList();
				if (selected?.Count == 0)
					return;

				string text = string.Join(Environment.NewLine, selected.Select(l => $"[{l.Timestamp}] {l.Message}"));
				await Clipboard.SetTextAsync(text).ConfigureAwait(true);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Failed to copy logs: {ex.Message}");
			}
		}

		private void AppendLog(string message)
		{
			try
			{
				lock (_logLock)
				{
					if (_viewModel._logBuilder.Count >= _maxLinesShown)
					{
						_viewModel.RemoveOldestLog();
					}

					_viewModel.AppendLog(message);
					LogLine last = _viewModel.LogLines.LastOrDefault();
					if (last != null)
						last.IsHighlighted = string.Equals(last.Level, "Error", StringComparison.Ordinal) || string.Equals(last.Level, "Warning", StringComparison.Ordinal);
				}

				_ = Dispatcher.UIThread.InvokeAsync(() => LogScrollViewer.ScrollToEnd());
			}
			catch (Exception ex)
			{
				Console.WriteLine($"An error occurred appending the log to the output window: '{ex.Message}'");
			}
		}

		private void LogListBox_KeyDown(object sender, KeyEventArgs e)
		{
			try
			{
				if (!(sender is ListBox list))
					return;
				if (e.Key == Key.H)
				{
					foreach (LogLine line in list.SelectedItems?.OfType<LogLine>() ?? Enumerable.Empty<LogLine>())
					{
						line.IsHighlighted = !line.IsHighlighted;
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "An error occurred in LogListBox_KeyDown");
			}
		}

		private void ClearLog_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				lock (_logLock)
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
			catch (Exception ex)
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

				IStorageFile file = await StorageProvider.SaveFilePickerAsync(saveOptions).ConfigureAwait(true);
				string filePath = file.TryGetLocalPath();
				if (string.IsNullOrEmpty(filePath))
					return;

				await Task.Run(() => File.WriteAllText(filePath, _viewModel.LogText ?? string.Empty)).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Failed to save logs: {ex.Message}");
			}
		}

		private void InputElement_OnPointerMoved([NotNull] object sender, [NotNull] PointerEventArgs e)
		{
			if (!_mouseDownForWindowMoving)
				return;

			PointerPoint currentPoint = e.GetCurrentPoint(this);
			Position = new PixelPoint(
				Position.X + (int)(currentPoint.Position.X - _originalPoint.Position.X),
				Position.Y + (int)(currentPoint.Position.Y - _originalPoint.Position.Y)
			);
		}

		private void InputElement_OnPointerPressed([NotNull] object sender, [NotNull] PointerEventArgs e)
		{
			if (WindowState == WindowState.Maximized || WindowState == WindowState.FullScreen)
				return;

			_mouseDownForWindowMoving = true;
			_originalPoint = e.GetCurrentPoint(this);
		}

		private void InputElement_OnPointerReleased([NotNull] object sender, [NotNull] PointerEventArgs e) =>
			_mouseDownForWindowMoving = false;

		private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

		private void ToggleMaximizeButton_Click(object sender, RoutedEventArgs e) =>
			WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

		private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
	}
}