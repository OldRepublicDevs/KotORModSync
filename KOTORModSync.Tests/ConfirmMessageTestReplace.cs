// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Linq;
using KOTORModSync.Core.TSLPatcher;
using NUnit.Framework;


namespace KOTORModSync.Tests
{
	[TestFixture]
	public class ConfirmMessageTestReplace
	{
		private string? _testDirectoryPath;

		[SetUp]
		public void SetUp()
		{
			_testDirectoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
			_ = Directory.CreateDirectory(_testDirectoryPath);
		}

		[TearDown]
		public void TearDown()
		{
			if (_testDirectoryPath != null)
			{
				Directory.Delete(_testDirectoryPath, true);
			}
		}

		[Test]
		public void DisableConfirmations_NullDirectory_ThrowsArgumentNullException() => _ = Assert.Throws<ArgumentNullException>(() => IniHelper.ReplaceIniPattern(null!, pattern: @"^\s*ConfirmMessage\s*=\s*.*$", replacement: "ConfirmMessage=N/A"));

		[Test]
		public void DisableConfirmations_NoIniFiles_ThrowsInvalidOperationException()
		{
			Assert.That(_testDirectoryPath, Is.Not.Null);
			var directory = new DirectoryInfo(_testDirectoryPath!);

			_ = Assert.Throws<InvalidOperationException>(() => IniHelper.ReplaceIniPattern(directory, pattern: @"^\s*ConfirmMessage\s*=\s*.*$", replacement: "ConfirmMessage=N/A"));
		}

		[Test]
		public void DisableConfirmations_ConfirmMessageExists_ReplacesWithN_A()
		{
			Assert.That(_testDirectoryPath, Is.Not.Null);
			const string iniFileName = "sample.ini";
			const string content = "[Settings]\nConfirmMessage=suffer the consequences by proceeding. Continue anyway?";

			File.WriteAllText(Path.Combine(_testDirectoryPath!, iniFileName), content);

			var directory = new DirectoryInfo(_testDirectoryPath!);

			IniHelper.ReplaceIniPattern(directory, pattern:@"^\s*ConfirmMessage\s*=\s*.*$", replacement:"ConfirmMessage=N/A");

			string modifiedContent = File.ReadAllText(Path.Combine(_testDirectoryPath!, iniFileName));
			Assert.That(modifiedContent, Does.Contain("ConfirmMessage=N/A"));
		}
	}

}
