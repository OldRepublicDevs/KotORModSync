// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace KOTORModSync.Core.CLI
{
	/// <summary>
	/// Provides a Docker-like dynamic progress display for CLI operations.
	/// Shows active operations at the bottom, failed URLs above them, and scrolling logs at the top.
	/// </summary>
	public class ConsoleProgressDisplay : IDisposable
	{
		private readonly object _lock = new object();
		private readonly ConcurrentDictionary<string, ProgressItem> _activeItems = new ConcurrentDictionary<string, ProgressItem>();
		private readonly Dictionary<string, FailedItem> _failedItems = new Dictionary<string, FailedItem>(); // Use Dictionary to prevent duplicates
		private readonly Queue<string> _scrollingLogs = new Queue<string>();
		private readonly Timer _refreshTimer;
		private bool _disposed = false;
		private bool _isEnabled = false;
		private bool _usePlainText = false;
		private bool _needsRender = false;
		private string _lastRenderedContent = string.Empty;
		private int _consoleWidth = 80;
		private int _maxActiveItems = 5;
		private int _maxFailedItems = 10;
		private int _maxScrollingLogs = 100;

		// ANSI escape codes for cursor control
		private const string SAVE_CURSOR = "\x1b[s";
		private const string RESTORE_CURSOR = "\x1b[u";
		private const string CLEAR_LINE = "\x1b[2K";
		private const string HIDE_CURSOR = "\x1b[?25l";
		private const string SHOW_CURSOR = "\x1b[?25h";

		public ConsoleProgressDisplay(bool usePlainText = false)
		{
			_usePlainText = usePlainText;

			try
			{
				_consoleWidth = Console.WindowWidth;
				_isEnabled = !Console.IsOutputRedirected && Environment.UserInteractive;
			}
			catch
			{
				_isEnabled = false;
			}

			// If plaintext mode is enabled, disable the fancy display
			if ( _usePlainText )
			{
				_isEnabled = false;
			}

			if ( _isEnabled )
			{
				Console.Write(HIDE_CURSOR);
				// Refresh display every 16ms (~60fps) to keep status area anchored at bottom even with rapid log output
				_refreshTimer = new Timer(_ => Render(), null, TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));
			}
		}

		private class ProgressItem
		{
			public string Key { get; set; }
			public string DisplayText { get; set; }
			public double Progress { get; set; }
			public DateTime LastUpdate { get; set; }
			public string Status { get; set; } // "downloading", "processing", "extracting", etc.
		}

		private class FailedItem
		{
			public string Url { get; set; }
			public string Error { get; set; }
			public DateTime Timestamp { get; set; }
		}

		/// <summary>
		/// Add or update a progress item
		/// </summary>
		public void UpdateProgress(string key, string displayText, double progress, string status = "processing")
		{
			// In plaintext mode, output progress updates directly
			if ( _usePlainText )
			{
				Console.WriteLine($"[{status.ToUpper()}] {displayText} - {progress:F1}%");
				return;
			}

			if ( !_isEnabled ) return;

			bool isNewItem = !_activeItems.ContainsKey(key);
			bool shouldRender = isNewItem; // Always render for new items

			_activeItems.AddOrUpdate(key,
				new ProgressItem
				{
					Key = key,
					DisplayText = displayText,
					Progress = progress,
					LastUpdate = DateTime.Now,
					Status = status
				},
			(k, existing) =>
			{
				// Only trigger render if progress changed by at least 0.5% or status changed
				if ( Math.Abs(existing.Progress - progress) >= 0.5 || existing.Status != status )
				{
					shouldRender = true;
				}
				existing.DisplayText = displayText;
				existing.Progress = progress;
				existing.LastUpdate = DateTime.Now;
				existing.Status = status;
				return existing;
			});

			if ( shouldRender )
			{
				_needsRender = true;
			}
		}

		/// <summary>
		/// Remove a progress item (when completed)
		/// </summary>
		public void RemoveProgress(string key)
		{
			// In plaintext mode, output completion message
			if ( _usePlainText )
			{
				if ( _activeItems.TryGetValue(key, out var item) )
				{
					Console.WriteLine($"[COMPLETED] {item.DisplayText}");
				}
				return;
			}

			if ( !_isEnabled ) return;
			_activeItems.TryRemove(key, out _);
			_needsRender = true;
		}

		/// <summary>
		/// Add a failed item
		/// </summary>
		public void AddFailedItem(string url, string error)
		{
			// In plaintext mode, output error directly
			if ( _usePlainText )
			{
				Console.WriteLine($"[FAILED] {url}");
				Console.WriteLine($"  Error: {error}");
				return;
			}

			if ( !_isEnabled ) return;

			lock ( _lock )
			{
				// Use URL as key to prevent duplicates
				_failedItems[url] = new FailedItem
				{
					Url = url,
					Error = error,
					Timestamp = DateTime.Now
				};

				// Keep only recent failed items
				if ( _failedItems.Count > _maxFailedItems * 2 )
				{
					var oldestKeys = _failedItems
						.OrderBy(kvp => kvp.Value.Timestamp)
						.Take(_failedItems.Count - _maxFailedItems)
						.Select(kvp => kvp.Key)
						.ToList();

					foreach ( var key in oldestKeys )
					{
						_failedItems.Remove(key);
					}
				}

				_needsRender = true;
			}
		}

		/// <summary>
		/// Add a scrolling log message
		/// </summary>
		public void AddLog(string message)
		{
			// In plaintext mode, output log directly
			if ( _usePlainText )
			{
				Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
				return;
			}

			if ( !_isEnabled )
			{
				// Fallback to normal console output
				Console.WriteLine(message);
				return;
			}

			lock ( _lock )
			{
				_scrollingLogs.Enqueue(message);

				// Keep only recent logs
				while ( _scrollingLogs.Count > _maxScrollingLogs )
				{
					_scrollingLogs.Dequeue();
				}

				_needsRender = true;
			}
		}

		/// <summary>
		/// Render the entire display
		/// </summary>
		private void Render()
		{
			if ( !_isEnabled || _disposed ) return;

			// Skip rendering if nothing has changed
			if ( !_needsRender ) return;

			lock ( _lock )
			{
				try
				{
					_consoleWidth = Console.WindowWidth;
				}
				catch
				{
					// Ignore if console size can't be retrieved
				}

				var sb = new StringBuilder();

				// Calculate how many lines we need for the status area
				int statusLines = CalculateStatusLines();

				// Move to the status area (bottom of console)
				try
				{
					int consoleHeight = Console.WindowHeight;
					// Ensure status area is always at the very bottom
					int statusStartLine = Math.Max(1, consoleHeight - statusLines + 1);

					// Move to status area start and clear from there to bottom
					sb.Append($"\x1b[{statusStartLine};1H");
					sb.Append("\x1b[0J"); // Clear from cursor to end of screen

					// Render failed items first (at top of status area, single line per failure)
					var failedItems = _failedItems.Values
						.OrderByDescending(f => f.Timestamp)
						.Take(_maxFailedItems)
						.ToList();

					if ( failedItems.Count > 0 )
					{
						sb.AppendLine("╔═══ FAILED DOWNLOADS ═══");
						foreach ( var failed in failedItems )
						{
							// Single line: URL with abbreviated error in parentheses
							string truncatedUrl = TruncateString(failed.Url, _consoleWidth - 35);
							string shortError = failed.Error.Length > 25 ? failed.Error.Substring(0, 22) + "..." : failed.Error;
							sb.AppendLine($"║ ✗ {truncatedUrl} ({shortError})");
						}
						sb.AppendLine("╚" + new string('═', Math.Min(_consoleWidth - 2, 50)));
					}

					// Render active progress items last (at bottom)
					var activeItems = _activeItems.Values
						.OrderBy(x => x.LastUpdate)
						.Take(_maxActiveItems)
						.ToList();

					if ( activeItems.Count > 0 )
					{
						sb.AppendLine("╔═══ ACTIVE DOWNLOADS ═══");
						foreach ( var item in activeItems )
						{
							string progressBar = RenderProgressBar(item.Progress, 30);
							string statusIcon = GetStatusIcon(item.Status);
							string displayText = TruncateString(item.DisplayText, _consoleWidth - 45);
							sb.AppendLine($"{statusIcon} {displayText} {progressBar} {item.Progress:F1}%");
						}
						sb.AppendLine("╚" + new string('═', Math.Min(_consoleWidth - 2, 50)));
					}

					string newContent = sb.ToString();

					// Only write if content actually changed
					if ( newContent != _lastRenderedContent )
					{
						Console.Write(newContent);
						_lastRenderedContent = newContent;
					}

					_needsRender = false;
				}
				catch
				{
					// If rendering fails, don't crash - just skip this frame
				}
			}
		}

		private int CalculateStatusLines()
		{
			int lines = 2; // Padding

			// Failed items section (now single line per item)
			if ( _failedItems.Count > 0 )
			{
				lines += 2; // Header and footer
				lines += Math.Min(_failedItems.Count, _maxFailedItems); // Each failed item takes 1 line now
			}

			// Active items section
			if ( !_activeItems.IsEmpty )
			{
				lines += 2; // Header and footer for active downloads box
				lines += Math.Min(_activeItems.Count, _maxActiveItems);
			}

			return Math.Min(lines, 20); // Cap at 20 lines
		}

		private static string RenderProgressBar(double progress, int width)
		{
			int filled = (int)((progress / 100.0) * width);
			int empty = width - filled;

			return $"[{new string('█', filled)}{new string('░', empty)}]";
		}

		private static string GetStatusIcon(string status)
		{
			// Traditional switch for .NET Framework compatibility
			switch ( status.ToLowerInvariant() )
			{
				case "downloading":
					return "⬇";
				case "processing":
					return "⚙";
				case "extracting":
					return "📦";
				case "resolving":
					return "🔍";
				case "completed":
					return "✓";
				case "failed":
					return "✗";
				default:
					return "●";
			}
		}

		private static string TruncateString(string text, int maxLength)
		{
			if ( string.IsNullOrEmpty(text) ) return string.Empty;
			if ( text.Length <= maxLength ) return text;
			return text.Substring(0, maxLength - 3) + "...";
		}

		/// <summary>
		/// Write a log that scrolls above the status area
		/// </summary>
		public void WriteScrollingLog(string message)
		{
			// In plaintext mode, just write directly
			if ( _usePlainText || !_isEnabled )
			{
				Console.WriteLine(message);
				return;
			}

			lock ( _lock )
			{
				try
				{
					// Calculate position above status area
					int statusLines = CalculateStatusLines();
					int consoleHeight = Console.WindowHeight;
					int statusStartLine = Math.Max(1, consoleHeight - statusLines);

					// Clear the status area, write message, then trigger re-render
					Console.Write($"\x1b[{statusStartLine};1H");
					Console.Write("\x1b[0J"); // Clear from cursor to end of screen
					Console.Write($"\x1b[{statusStartLine - 1};1H");
					Console.WriteLine(message);

					// Force immediate re-render of status area
					_needsRender = true;
					Render();
				}
				catch
				{
					// Fallback to normal output
					Console.WriteLine(message);
				}
			}
		}

		public void Dispose()
		{
			if ( _disposed ) return;
			_disposed = true;

			_refreshTimer?.Dispose();

			if ( _isEnabled )
			{
				lock ( _lock )
				{
					try
					{
						// Clear status area
						int statusLines = CalculateStatusLines();
						int consoleHeight = Console.WindowHeight;
						int statusStartLine = Math.Max(1, consoleHeight - statusLines);

						Console.Write($"\x1b[{statusStartLine};1H");
						for ( int i = 0; i < statusLines; i++ )
						{
							Console.WriteLine(CLEAR_LINE);
						}

						// Show cursor again
						Console.Write(SHOW_CURSOR);
					}
					catch
					{
						// Best effort cleanup
					}
				}
			}
		}
	}
}

