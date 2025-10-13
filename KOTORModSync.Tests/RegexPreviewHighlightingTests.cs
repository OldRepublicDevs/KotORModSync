// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.


using System.Collections.ObjectModel;
using Avalonia.Controls.Documents;
using Avalonia.Headless.NUnit;
using Avalonia.Media;
using KOTORModSync.Core.Parsing;

namespace KOTORModSync.Tests
{
	[TestFixture]
	public class RegexPreviewHighlightingTests
	{
		[AvaloniaTest]
		public void GenerateHighlightedPreview_ProducesColoredInlines()
		{
			const string md = "___\r\n### Heading\r\n**Name:** Foo\r\n**Author:** Bar\r\n";

			var profile = MarkdownImportProfile.CreateDefault();
			profile.Mode = RegexMode.Raw;
			profile.RawRegexPattern = @"(?ms)^\s*_{3,}\s*\r?\n\s*###\s*(?<heading>[^\r\n]+)[\s\S]*?(?<name>Foo)[\s\S]*?";

			var vm = new RegexImportDialogViewModel(md, profile);
			vm.OnProfileChanged();

			ObservableCollection<Inline> inlines = vm.HighlightedPreview;
			Assert.That(inlines, Is.Not.Null, "Inlines collection should not be null");
			Assert.That(inlines, Is.Not.Empty, "Expected at least one inline generated for preview");

			
			Assert.That(inlines, Has.Count.GreaterThan(1), "Expected multiple runs inlines indicating segmentation");

			bool anyBold = false;
			bool anyColoredNonWhite = false;
			bool containsNameText = false;
			int boldCount = 0;
			int coloredCount = 0;
			int totalRunChars = 0;

			foreach ( Inline inline in inlines )
			{
				if ( inline is Run run )
				{
					totalRunChars += run.Text?.Length ?? 0;
					if ( run.Text != null && run.Text.Contains("Foo") )
						containsNameText = true;

					if ( run.FontWeight == FontWeight.Bold )
					{
						anyBold = true;
						boldCount++;
					}

					if ( run.Foreground is SolidColorBrush brush )
					{
						
						if ( brush.Color != Colors.White )
						{
							anyColoredNonWhite = true;
							coloredCount++;
						}
					}
				}
			}

			Assert.Multiple(() =>
			{
				
				Assert.That(anyBold, Is.True, "Expected bold run for highlighted group");
				
				Assert.That(anyColoredNonWhite, Is.True, "Expected a colored (non-white) run for highlighted group");
				
				Assert.That(containsNameText, Is.True, "Expected highlighted run to contain captured group text");
				
				Assert.That(totalRunChars, Is.GreaterThan(10), "Expected non-trivial text content in runs");
				
				Assert.That(boldCount, Is.GreaterThanOrEqualTo(1));
				Assert.That(coloredCount, Is.GreaterThanOrEqualTo(1));
			});

		}
	}
}

