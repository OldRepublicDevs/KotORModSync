// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace KOTORModSync.Dialogs
{
	public class ValidationIssue
	{
		public string Icon { get; set; }
		public string ModName { get; set; }
		public string IssueType { get; set; }
		public string Description { get; set; }
		public string Solution { get; set; }
		public bool HasSolution => !string.IsNullOrEmpty(Solution);
	}

	public partial class ValidationDialog : Window
	{
		private readonly Action _openOutputCallback;

		public ValidationDialog()
		{
			AvaloniaXamlLoader.Load(this);
		}

		public ValidationDialog(bool success, string summaryMessage, List<ValidationIssue> modIssues = null,
							   List<string> systemIssues = null, Action openOutputCallback = null)
		{
			AvaloniaXamlLoader.Load(this);
			_openOutputCallback = openOutputCallback;

			InitializeDialog(success, summaryMessage, modIssues, systemIssues);
		}

		private void InitializeDialog(bool success, string summaryMessage, List<ValidationIssue> modIssues, List<string> systemIssues)
		{
			// Get controls
			Border summaryBorder = this.FindControl<Border>("SummaryBorder");
			TextBlock summaryIcon = this.FindControl<TextBlock>("SummaryIcon");
			TextBlock summaryText = this.FindControl<TextBlock>("SummaryText");
			TextBlock summaryDetails = this.FindControl<TextBlock>("SummaryDetails");
			StackPanel issuesPanel = this.FindControl<StackPanel>("IssuesPanel");
			StackPanel systemIssuesPanel = this.FindControl<StackPanel>("SystemIssuesPanel");
			ItemsControl systemIssuesList = this.FindControl<ItemsControl>("SystemIssuesList");
			StackPanel modIssuesPanel = this.FindControl<StackPanel>("ModIssuesPanel");
			ItemsControl modIssuesList = this.FindControl<ItemsControl>("ModIssuesList");
			Button openOutputButton = this.FindControl<Button>("OpenOutputButton");

			if ( summaryIcon == null || summaryText == null || summaryDetails == null || summaryBorder == null )
				return;

			if ( success )
			{
				summaryIcon.Text = "✅";
				summaryText.Text = "Validation Successful!";
				summaryDetails.Text = summaryMessage;
				summaryBorder.Background = new SolidColorBrush(Color.Parse("#1B4D3E")); // Dark green
			}
			else
			{
				summaryIcon.Text = "❌";
				summaryText.Text = "Validation Failed";
				summaryDetails.Text = summaryMessage;
				summaryBorder.Background = new SolidColorBrush(Color.Parse("#4D1B1B")); // Dark red
				if ( issuesPanel != null )
					issuesPanel.IsVisible = true;
				if ( openOutputButton != null )
					openOutputButton.IsVisible = true;

				// Show system issues
				if ( systemIssues != null && systemIssues.Count > 0 && systemIssuesPanel != null && systemIssuesList != null )
				{
					systemIssuesPanel.IsVisible = true;
					var systemIssuesChildren = new List<Control>();
					foreach ( string issue in systemIssues )
					{
						systemIssuesChildren.Add(new Border
						{
							Classes = { "summary-card" },
							Padding = new Avalonia.Thickness(12),
							Margin = new Avalonia.Thickness(0, 4),
							CornerRadius = new Avalonia.CornerRadius(6),
							Child = new TextBlock
							{
								Text = issue,
								TextWrapping = Avalonia.Media.TextWrapping.Wrap
							}
						});
					}
					systemIssuesList.ItemsSource = systemIssuesChildren;
				}

				// Show mod issues
				if ( modIssues != null && modIssues.Count > 0 && modIssuesPanel != null && modIssuesList != null )
				{
					modIssuesPanel.IsVisible = true;
					modIssuesList.ItemsSource = new ObservableCollection<ValidationIssue>(modIssues);
				}
			}
		}

		private void CloseButton_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		private void Ok_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		private void OpenOutput_Click(object sender, RoutedEventArgs e)
		{
			_openOutputCallback?.Invoke();
			Close();
		}

		public static async Task<bool> ShowValidationDialog(
			Window parent,
			bool success,
			string summaryMessage,
			List<ValidationIssue> modIssues = null,
			List<string> systemIssues = null,
			Action openOutputCallback = null)
		{
			var dialog = new ValidationDialog(success, summaryMessage, modIssues, systemIssues, openOutputCallback);
			await dialog.ShowDialog(parent);
			return true;
		}
	}
}

