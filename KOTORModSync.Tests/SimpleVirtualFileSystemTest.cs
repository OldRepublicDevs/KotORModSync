// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using KOTORModSync.Core;
using KOTORModSync.Core.Services.FileSystem;

namespace KOTORModSync.Tests
{
	[TestFixture]
	public class SimpleVirtualFileSystemTest
	{
		[Test]
		public void Test_VirtualFileSystemProvider_BasicCreation()
		{
			// Arrange
			var provider = new VirtualFileSystemProvider();

			// Act & Assert
			Assert.That(provider, Is.Not.Null);
			Assert.That(provider.IsDryRun, Is.True);
		}

		[Test]
		public void Test_RealFileSystemProvider_BasicCreation()
		{
			// Arrange
			var provider = new RealFileSystemProvider();

			// Act & Assert
			Assert.That(provider, Is.Not.Null);
			Assert.That(provider.IsDryRun, Is.False);
		}

		[Test]
		public void Test_VirtualFileSystemProvider_FileOperations()
		{
			// Arrange
			var provider = new VirtualFileSystemProvider();

			// Act - Create a file
			provider.WriteFileAsync("test.txt", "content").Wait();

			// Assert
			Assert.That(provider.FileExists("test.txt"), Is.True);
			Assert.That(provider.FileExists("nonexistent.txt"), Is.False);
		}

		[Test]
		public void Test_MainConfig_Initialization()
		{
			// Arrange
			var config = new MainConfig();

			// Act & Assert - Just verify we can access the properties without error
			Assert.That(config.caseInsensitivePathing, Is.TypeOf<bool>());
			Assert.That(config.useMultiThreadedIO, Is.False); // Default should be false
		}
	}
}
