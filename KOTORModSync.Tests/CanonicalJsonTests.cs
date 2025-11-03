// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Collections.Generic;

using KOTORModSync.Core.Utility;

using NUnit.Framework;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public class CanonicalJsonTests
    {
        [Test]
        public void SerializeObject_WithStringValues_ProducesConsistentOutput()
        {
            var dict = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["z"] = "last",
                ["a"] = "first",
                ["m"] = "middle",
            };

            string json = CanonicalJson.Serialize(dict);

            // Keys must be sorted alphabetically
            Assert.That(json, Is.EqualTo("{\"a\":\"first\",\"m\":\"middle\",\"z\":\"last\"}"));
        }

        [Test]
        public void SerializeObject_WithNumericValues_UsesInvariantCulture()
        {
            var dict = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["int"] = 42,
                ["long"] = 9876543210L,
                ["double"] = 3.14159,
            };

            string json = CanonicalJson.Serialize(dict);

            // Must use invariant culture (dot as decimal separator) and preserve full precision
            Assert.That(json, Does.Contain("\"double\":3.1415899999999999"));
            Assert.That(json, Does.Contain("\"int\":42"));
            Assert.That(json, Does.Contain("\"long\":9876543210"));
        }

        [Test]
        public void SerializeObject_WithNestedObjects_MaintainsKeyOrder()
        {
            var dict = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["outer2"] = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["z"] = "last",
                    ["a"] = "first",
                },
                ["outer1"] = "value",
            };

            string json = CanonicalJson.Serialize(dict);

            // Outer keys sorted
            Assert.That(json, Does.StartWith("{\"outer1\":"));
            // Inner keys sorted
            Assert.That(json, Does.Contain("\"outer2\":{\"a\":\"first\",\"z\":\"last\"}"));
        }

        [Test]
        public void ComputeHash_WithSameData_ProducesSameHash()
        {
            var dict1 = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["key2"] = "value2",
                ["key1"] = "value1",
            };

            var dict2 = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["key1"] = "value1",
                ["key2"] = "value2",
            };

            string hash1 = CanonicalJson.ComputeHash(dict1);
            string hash2 = CanonicalJson.ComputeHash(dict2);

            Assert.That(hash1, Is.EqualTo(hash2));
        }

        [Test]
        public void ComputeHash_WithDifferentData_ProducesDifferentHash()
        {
            var dict1 = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["key"] = "value1",
            };

            var dict2 = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["key"] = "value2",
            };

            string hash1 = CanonicalJson.ComputeHash(dict1);
            string hash2 = CanonicalJson.ComputeHash(dict2);

            Assert.That(hash1, Is.Not.EqualTo(hash2));
        }

        [Test]
        public void ComputeHash_ReturnsLowercase64CharHex()
        {
            var dict = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["test"] = "data",
            };

            string hash = CanonicalJson.ComputeHash(dict);

            // SHA-256 produces 64 hex characters
            Assert.That(hash, Has.Length.EqualTo(64));
            Assert.That(hash, Does.Match("^[0-9a-f]+$"));
        }

        [Test]
        public void SerializeObject_WithEmptyString_PreservesEmpty()
        {
            var dict = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["empty"] = "",
                ["nonempty"] = "value",
            };

            string json = CanonicalJson.Serialize(dict);

            Assert.That(json, Does.Contain("\"empty\":\"\""));
        }

        [Test]
        public void SerializeObject_WithSpecialCharacters_EscapesCorrectly()
        {
            var dict = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["quote"] = "value with \"quotes\"",
                ["backslash"] = "path\\with\\backslashes",
                ["newline"] = "line1\nline2",
            };

            string json = CanonicalJson.Serialize(dict);

            // Must escape quotes, backslashes, newlines
            Assert.That(json, Does.Contain("\\\""));
            Assert.That(json, Does.Contain("\\\\"));
            Assert.That(json, Does.Contain("\\n"));
        }
    }
}
