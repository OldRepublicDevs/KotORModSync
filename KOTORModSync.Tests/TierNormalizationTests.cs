// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;

using KOTORModSync.Core;
using KOTORModSync.Core.Utility;

using NUnit.Framework;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public class TierNormalizationTests
    {
        [Test]
        [TestCase("1 - Essential", "1 - Essential")]
        [TestCase("2 - Recommended", "2 - Recommended")]
        [TestCase("3 - Suggested", "3 - Suggested")]
        [TestCase("4 - Optional", "4 - Optional")]
        [TestCase("Essential", "1 - Essential")]
        [TestCase("Recommended", "2 - Recommended")]
        [TestCase("Suggested", "3 - Suggested")]
        [TestCase("Optional", "4 - Optional")]
        [TestCase("1-Essential", "1 - Essential")]
        [TestCase("2-Recommended", "2 - Recommended")]
        [TestCase("1 essential", "1 - Essential")]
        [TestCase("2 recommended", "2 - Recommended")]
        [TestCase("Essential - 1", "1 - Essential")]
        [TestCase("Recommended - 2", "2 - Recommended")]
        [TestCase("1 - Recommended", "2 - Recommended")]
        [TestCase("3 - Essential", "1 - Essential")]
        [TestCase("2 - Optional", "4 - Optional")]
        [TestCase("essential", "1 - Essential")]
        [TestCase("RECOMMENDED", "2 - Recommended")]
        [TestCase("option", "4 - Optional")]
        public void NormalizeTier_HandlesVariations(string input, string expected)
        {
            string result = CategoryTierDefinitions.NormalizeTier(input);
            Assert.That(result, Is.EqualTo(expected),
                $"Failed to normalize '{input}' to '{expected}', got '{result}'");
        }

        [Test]
        public void ModComponent_TierProperty_NormalizesOnSet()
        {
            var component = new ModComponent
            {
                Name = "Test Mod",
                Guid = Guid.NewGuid(),
                Tier = "1 - Recommended",
            };

            Assert.That(component.Tier, Is.EqualTo("2 - Recommended"),
                "ModComponent.Tier should normalize to correct format");
        }

        [Test]
        public void ModComponent_TierProperty_HandlesReversedFormat()
        {
            var component = new ModComponent
            {
                Name = "Test Mod",
                Guid = Guid.NewGuid(),
                Tier = "Essential - 1",
            };

            Assert.That(component.Tier, Is.EqualTo("1 - Essential"),
                "ModComponent.Tier should handle reversed format");
        }

        [Test]
        public void ModComponent_TierProperty_HandlesNoNumber()
        {
            var component = new ModComponent
            {
                Name = "Test Mod",
                Guid = Guid.NewGuid(),
                Tier = "Recommended",
            };

            Assert.That(component.Tier, Is.EqualTo("2 - Recommended"),
                "ModComponent.Tier should add correct number when missing");
        }

        [Test]
        public void NormalizeTier_ReturnsOriginalForUnknownTier()
        {
            string unknown = "Custom Tier Name";
            string result = CategoryTierDefinitions.NormalizeTier(unknown);

            Assert.That(result, Is.EqualTo(unknown),
                "Unknown tier names should be returned as-is");
        }

        [Test]
        public void NormalizeTier_HandlesNullAndEmpty()
        {
            Assert.Multiple(() =>
            {
                Assert.That(CategoryTierDefinitions.NormalizeTier(tier: null), Is.EqualTo(string.Empty));
                Assert.That(CategoryTierDefinitions.NormalizeTier(""), Is.EqualTo(string.Empty));
                Assert.That(CategoryTierDefinitions.NormalizeTier("   "), Is.EqualTo(string.Empty));
            });
        }
    }
}
