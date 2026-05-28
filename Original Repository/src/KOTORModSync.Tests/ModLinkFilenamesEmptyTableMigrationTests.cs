// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using KOTORModSync.Core;
using KOTORModSync.Core.Services;
using NUnit.Framework;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public sealed class ModLinkFilenamesEmptyTableMigrationTests
    {
        private const string DeadlyStreamUrl = "https://deadlystream.com/files/file/1218-helena-shan-improvement/";

        [Test]
        public void DeserializeToml_ModLinkFilenamesWithEmptyInlineTable_PopulatesResourceRegistryUrl()
        {
            string toml = $@"[[thisMod]]
ModLinkFilenames = {{ ""{DeadlyStreamUrl}"" = {{  }} }}
Guid = ""c07594e9-573b-42eb-9784-591cc3e097ac""
Name = ""Helena Shan Improvement""
Author = ""Test""
Tier = ""2 - Recommended""
Description = ""Test""
InstallationMethod = ""TSLPatcher Mod""
IsSelected = true
Category = [""Appearance Change""]
Language = [""YES""]
";

            List<ModComponent> components = ModComponentSerializationService.DeserializeModComponentFromTomlString(toml).ToList();
            Assert.That(components, Has.Count.EqualTo(1));
            ModComponent c = components[0];
            Assert.That(c.ResourceRegistry, Is.Not.Null);
            Assert.That(c.ResourceRegistry.Count, Is.EqualTo(1), "Empty {{ }} ModLinkFilenames must still register the download URL");
            Assert.That(c.ResourceRegistry.ContainsKey(DeadlyStreamUrl), Is.True);
            Assert.That(c.ResourceRegistry[DeadlyStreamUrl].Files, Is.Not.Null);
            Assert.That(c.ResourceRegistry[DeadlyStreamUrl].Files.Count, Is.EqualTo(0));
        }
    }
}
