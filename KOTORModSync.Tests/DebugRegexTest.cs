// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System.Text.RegularExpressions;
using KOTORModSync.Core.Parsing;

namespace KOTORModSync.Tests
{
	[TestFixture]
	public class DebugRegexTest
	{
		[Test]
		public void DebugRegexMatching()
		{
			const string sample = @"### Qel-Droma Reskin

**Name:** [Qel-Droma Robes Reskin](https://deadlystream.com/files/file/2019-effixians-qel-droma-robes-reskin-for-jcs-cloaked-jedi-robes/)

**Author:** Effix

**Description:** This mod reskins the Qel-Droma robes to look closer to their canon counterparts, while also improving their general appearance and making them compatible with JC's Jedi Tailor.

**Category & Tier:** Immersion, Appearance Change & Graphics Improvement / 2 - Recommended

**Non-English Functionality:** YES

**Installation Method:** Loose-File Mod

**Masters:** JC's Cloaked Jedi Robes

___

### Quanon's HK-47

**Name:** [Quanon's HK-47](http://deadlystream.com/files/file/1001-quanons-hk-47-reskin/)

**Author:** Quanon

**Description:** Improves the appearance of HK-47 by adding more detail, dimming the shine of his armor, and generally making his appearance in the first game more closely approximate a cleaner version of his appearance from KOTOR 2.

**Category & Tier:** Graphics Improvement / 2 - Recommended

**Non-English Functionality:** YES

**Installation Method:** Loose-File Mod

**Installation Instructions:** Delete PO_phk47.tga before moving the two other files to the override.

___";

			var profile = MarkdownImportProfile.CreateDefault();
			var regex = new Regex(profile.RawRegexPattern, profile.RawRegexOptions);
			MatchCollection matches = regex.Matches(sample);

			Console.WriteLine($"Found {matches.Count} matches");
			for (int i = 0; i < matches.Count; i++)
			{
				Match match = matches[i];
				Console.WriteLine($"Match {i + 1}:");
				Console.WriteLine($"  Heading: '{match.Groups["heading"].Value.Trim()}'");
				Console.WriteLine($"  Name: '{match.Groups["name"].Value.Trim()}'");
				Console.WriteLine($"  Author: '{match.Groups["author"].Value.Trim()}'");
				Console.WriteLine($"  CategoryTier: '{match.Groups["category_tier"].Value.Trim()}'");
				Console.WriteLine();
			}

			// Assert that we found exactly 2 matches (not 1!)
			Assert.That(matches.Count, Is.EqualTo(2), "Should find exactly 2 separate mod entries");
			Assert.That(matches[0].Groups["name"].Value.Trim(), Is.EqualTo("Qel-Droma Robes Reskin"));
			Assert.That(matches[1].Groups["name"].Value.Trim(), Is.EqualTo("Quanon's HK-47"));
		}
	}
}
