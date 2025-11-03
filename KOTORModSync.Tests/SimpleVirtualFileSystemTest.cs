// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using KOTORModSync.Core;
using KOTORModSync.Core.Services.FileSystem;

using NUnit.Framework;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public class SimpleVirtualFileSystemTest
    {
        [Test]
        public void Test_VirtualFileSystemProvider_BasicCreation()
        {

            var provider = new VirtualFileSystemProvider();

            Assert.That(provider, Is.Not.Null);
            Assert.That(provider.IsDryRun, Is.True);
        }

        [Test]
        public void Test_RealFileSystemProvider_BasicCreation()
        {

            var provider = new RealFileSystemProvider();

            Assert.That(provider, Is.Not.Null);
            Assert.That(provider.IsDryRun, Is.False);
        }

        [Test]
        public void Test_VirtualFileSystemProvider_FileOperations()
        {

            var provider = new VirtualFileSystemProvider();

            provider.WriteFileAsync("test.txt", "content").Wait();

            Assert.Multiple(() =>
            {
                Assert.That(provider.FileExists("test.txt"), Is.True);
                Assert.That(provider.FileExists("nonexistent.txt"), Is.False);
            });
        }

        [Test]
        public void Test_MainConfig_Initialization()
        {

            var config = new MainConfig();

            Assert.Multiple(() =>
            {
                Assert.That(config.caseInsensitivePathing, Is.TypeOf<bool>());
                Assert.That(config.useMultiThreadedIO, Is.False);
            });
        }
    }
}
