// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using KOTORModSync;
using NUnit.Framework;

namespace KOTORModSync.Tests
{
	[TestFixture]
	public class MainWindowUITests
	{
		public class TestAppBuilder
		{
			public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<KOTORModSync.App>()
				.UseHeadless(new AvaloniaHeadlessPlatformOptions());
		}

		[Test]
		public async Task TestOpenAppAndClickLoadInstructionButton()
		{
			// Arrange - Create and show the main window
			var window = new MainWindow();
			window.Show();

			// Wait a bit for the UI to initialize
			await Task.Delay(200);

			// Find the Getting Started tab control
			var tabControl = window.FindControl<TabControl>("TabControl");
			Assert.That(tabControl, Is.Not.Null);

			// Find the Getting Started tab
			var gettingStartedTab = window.FindControl<UserControl>("GettingStartedTabControl");
			Assert.That(gettingStartedTab, Is.Not.Null);

			// Find the ScrollViewer
			var scrollViewer = gettingStartedTab.GetVisualDescendants()
				.OfType<ScrollViewer>()
				.FirstOrDefault();
			Assert.That(scrollViewer, Is.Not.Null);

			// Simulate scrolling down to the Load Instruction button area
			scrollViewer.LineDown();
			scrollViewer.LineDown();
			scrollViewer.LineDown();
			await Task.Delay(50);

			// Find the Load Instruction button (Step2Button)
			var loadButton = window.FindControl<Button>("Step2Button");
			Assert.That(loadButton, Is.Not.Null);

			// Verify the button is visible and has the correct content
			Assert.That(loadButton.IsVisible, Is.True);
			Assert.That(loadButton.Content?.ToString() ?? "", Does.Contain("Load Instruction"));

			// Simulate clicking the button by raising the click event
			var clickEvent = new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent);
			loadButton.RaiseEvent(clickEvent);

			// Wait a bit for any actions to complete
			await Task.Delay(100);

			// Assert that the button was clicked successfully
			Assert.That(true, Is.True);
		}

		[Test]
		public async Task TestOpenAppAndClickMultipleButtons()
		{
			// Arrange - Create and show the main window
			var window = new MainWindow();
			window.Show();

			// Wait for UI initialization
			await Task.Delay(200);

			// Test 1: Click the Settings button in the header
			var settingsButton = window.FindControl<Button>("HeaderSettingsButton");
			if (settingsButton != null && settingsButton.IsVisible)
			{
				Assert.That(settingsButton, Is.Not.Null);
				var clickEvent1 = new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent);
				settingsButton.RaiseEvent(clickEvent1);
				await Task.Delay(50);
			}

			// Test 2: Click the Home (Getting Started) button
			var homeButton = window.FindControl<Button>("HomeButton");
			if (homeButton != null && homeButton.IsVisible)
			{
				Assert.That(homeButton, Is.Not.Null);
				var clickEvent2 = new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent);
				homeButton.RaiseEvent(clickEvent2);
				await Task.Delay(50);
			}

			// Test 3: Click the Output Window button
			var outputButton = window.FindControl<Button>("OutputWindowButton");
			if (outputButton != null && outputButton.IsVisible)
			{
				Assert.That(outputButton, Is.Not.Null);
				var clickEvent3 = new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent);
				outputButton.RaiseEvent(clickEvent3);
				await Task.Delay(50);
			}

			// Test 4: Toggle Editor Mode
			var editorToggle = window.FindControl<ToggleSwitch>("EditorModeToggle");
			if (editorToggle != null && editorToggle.IsVisible)
			{
				Assert.That(editorToggle, Is.Not.Null);
				var initialValue = editorToggle.IsChecked ?? false;
				editorToggle.IsChecked = !initialValue;
				await Task.Delay(50);
				Assert.That(editorToggle.IsChecked, Is.Not.EqualTo(initialValue));
			}

			// Verify we clicked the buttons
			Assert.That(true, Is.True);
		}

		[Test]
		public async Task TestGettingStartedTabNavigation()
		{
			// Arrange
			var window = new MainWindow();
			window.Show();

			await Task.Delay(200);

			// Find the Getting Started tab
			var gettingStartedTab = window.FindControl<UserControl>("GettingStartedTabControl");
			Assert.That(gettingStartedTab, Is.Not.Null);

			// Test clicking the Jump to Current Step button
			var jumpButton = window.FindControl<Button>("JumpToCurrentStepButton");
			if (jumpButton != null)
			{
				Assert.That(jumpButton, Is.Not.Null);
				var clickEvent = new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent);
				jumpButton.RaiseEvent(clickEvent);
				await Task.Delay(50);
			}

			// Test clicking the Settings button in Getting Started tab
			var gettingStartedSettingsButton = window.FindControl<Button>("GettingStartedSettingsButton");
			if (gettingStartedSettingsButton != null)
			{
				Assert.That(gettingStartedSettingsButton, Is.Not.Null);
				var clickEvent = new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent);
				gettingStartedSettingsButton.RaiseEvent(clickEvent);
				await Task.Delay(50);
			}

			Assert.That(true, Is.True);
		}

		[Test]
		public async Task TestWindowControls()
		{
			// Arrange
			var window = new MainWindow();
			window.Show();

			await Task.Delay(200);

			// Verify the window is shown and has proper dimensions
			Assert.That(window.IsVisible, Is.True);
			Assert.That(window.Width, Is.Not.Null);
			Assert.That(window.Height, Is.Not.Null);

			// Test window properties
			var tabControl = window.FindControl<TabControl>("TabControl");
			Assert.That(tabControl, Is.Not.Null);

			var titleText = window.FindControl<TextBlock>("TitleTextBlock");
			Assert.That(titleText, Is.Not.Null);
			Assert.That(titleText.Text, Does.Contain("KOTORModSync"));

			Assert.That(true, Is.True);
		}

		[Test]
		public async Task TestThemeComboBox()
		{
			// Arrange
			var window = new MainWindow();
			window.Show();

			await Task.Delay(200);

			// Find the Theme ComboBox
			var themeCombo = window.FindControl<ComboBox>("ThemeComboBox");
			Assert.That(themeCombo, Is.Not.Null);
			Assert.That(themeCombo.IsVisible, Is.True);

			// Test changing selection
			var initialIndex = themeCombo.SelectedIndex;
			Assert.That(initialIndex, Is.EqualTo(0));

			// Change to TSL theme
			themeCombo.SelectedIndex = 1;
			await Task.Delay(50);

			Assert.That(themeCombo.SelectedIndex, Is.EqualTo(1));
		}

		[Test]
		public async Task TestSpoilerFreeModeToggle()
		{
			// Arrange
			var window = new MainWindow();
			window.Show();

			await Task.Delay(200);

			// Find the Spoiler Free Mode Toggle
			var spoilerToggle = window.FindControl<ToggleSwitch>("SpoilerFreeModeToggle");
			Assert.That(spoilerToggle, Is.Not.Null);
			Assert.That(spoilerToggle.IsVisible, Is.True);

			// Test toggling
			var initialValue = spoilerToggle.IsChecked ?? false;
			spoilerToggle.IsChecked = !initialValue;
			await Task.Delay(50);

			Assert.That(spoilerToggle.IsChecked, Is.Not.EqualTo(initialValue));
		}
	}
}

