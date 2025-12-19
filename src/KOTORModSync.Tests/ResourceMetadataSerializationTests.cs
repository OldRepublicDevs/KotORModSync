// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;

using KOTORModSync.Core;
using KOTORModSync.Core.Services;

using NUnit.Framework;

namespace KOTORModSync.Tests
{
	[TestFixture]
	public class ResourceMetadataSerializationTests
	{
		[Test]
		public void SerializeResourceRegistry_WithSingleEntry_ProducesValidDict()
		{
			var registry = new Dictionary<string, ResourceMetadata>
(StringComparer.Ordinal)
			{
				["hash123"] = new ResourceMetadata
				{
					ContentId = "abc123",
					MetadataHash = "hash123",
					PrimaryUrl = "https://example.com/file.zip",
					FileSize = 1024,
					SchemaVersion = 1,
					TrustLevel = MappingTrustLevel.Verified
				}
			};

			var serialized = ModComponentSerializationService.SerializeResourceRegistry(registry);

			Assert.That(serialized, Is.Not.Null);
			Assert.That(serialized.ContainsKey("resources"), Is.True);
		}

		[Test]
		public void DeserializeResourceRegistry_WithValidData_ReconstructsRegistry()
		{
			var original = new Dictionary<string, ResourceMetadata>
(StringComparer.Ordinal)
			{
				["hash123"] = new ResourceMetadata
				{
					ContentId = "abc123",
					MetadataHash = "hash123",
					PrimaryUrl = "https://example.com/file.zip",
					FileSize = 1024,
					FirstSeen = DateTime.UtcNow,
					SchemaVersion = 1,
					TrustLevel = MappingTrustLevel.Verified
				}
			};

			var serialized = ModComponentSerializationService.SerializeResourceRegistry(original);
			var deserialized = ModComponentSerializationService.DeserializeResourceRegistry(serialized);

			Assert.Multiple(() =>
			{
				Assert.That(original, Is.Not.Null, "Original registry should not be null");
				Assert.That(original.Count, Is.EqualTo(1), "Original registry should contain exactly 1 entry");
				Assert.That(serialized, Is.Not.Null, "Serialized dictionary should not be null");
				Assert.That(deserialized, Is.Not.Null, "Deserialized registry should not be null");
				Assert.That(deserialized, Has.Count.EqualTo(1), "Deserialized registry should contain exactly 1 entry");
				Assert.That(deserialized.ContainsKey("hash123"), Is.True, "Deserialized registry should contain hash123 key");
				Assert.That(deserialized["hash123"], Is.Not.Null, "Deserialized metadata should not be null");
				Assert.That(deserialized["hash123"].ContentId, Is.Not.Null, "ContentId should not be null");
				Assert.That(deserialized["hash123"].ContentId, Is.EqualTo("abc123"), "ContentId should match original");
				Assert.That(deserialized["hash123"].PrimaryUrl, Is.Not.Null, "PrimaryUrl should not be null");
				Assert.That(deserialized["hash123"].PrimaryUrl, Is.EqualTo("https://example.com/file.zip"), "PrimaryUrl should match original");
			});
		}

		[Test]
		public void SerializeResourceRegistry_WithMultipleEntries_PreservesAll()
		{
			var registry = new Dictionary<string, ResourceMetadata>
(StringComparer.Ordinal)
			{
				["hash1"] = new ResourceMetadata { ContentId = "id1", MetadataHash = "hash1" },
				["hash2"] = new ResourceMetadata { ContentId = "id2", MetadataHash = "hash2" },
				["hash3"] = new ResourceMetadata { ContentId = "id3", MetadataHash = "hash3" }
			};

			var serialized = ModComponentSerializationService.SerializeResourceRegistry(registry);
			var deserialized = ModComponentSerializationService.DeserializeResourceRegistry(serialized);

			Assert.Multiple(() =>
			{
				Assert.That(registry, Is.Not.Null, "Registry should not be null");
				Assert.That(registry.Count, Is.EqualTo(3), "Registry should contain exactly 3 entries");
				Assert.That(serialized, Is.Not.Null, "Serialized dictionary should not be null");
				Assert.That(deserialized, Is.Not.Null, "Deserialized registry should not be null");
				Assert.That(deserialized, Has.Count.EqualTo(3), "Deserialized registry should contain exactly 3 entries");
				Assert.That(deserialized.ContainsKey("hash1"), Is.True, "Deserialized registry should contain hash1");
				Assert.That(deserialized.ContainsKey("hash2"), Is.True, "Deserialized registry should contain hash2");
				Assert.That(deserialized.ContainsKey("hash3"), Is.True, "Deserialized registry should contain hash3");
				Assert.That(deserialized["hash1"], Is.Not.Null, "Hash1 metadata should not be null");
				Assert.That(deserialized["hash2"], Is.Not.Null, "Hash2 metadata should not be null");
				Assert.That(deserialized["hash3"], Is.Not.Null, "Hash3 metadata should not be null");
			});
		}

		[Test]
		public void SerializeResourceRegistry_WithEmptyRegistry_ProducesEmptyDict()
		{
			var registry = new Dictionary<string, ResourceMetadata>(StringComparer.Ordinal);

			var serialized = ModComponentSerializationService.SerializeResourceRegistry(registry);

			Assert.Multiple(() =>
			{
				Assert.That(registry, Is.Not.Null, "Registry should not be null");
				Assert.That(registry, Is.Empty, "Registry should be empty");
				Assert.That(serialized, Is.Not.Null, "Serialized dictionary should not be null");
				Assert.That(serialized.ContainsKey("resources"), Is.True, "Serialized dictionary should contain 'resources' key");
			});
		}

		[Test]
		public void DeserializeResourceRegistry_WithEmptyDict_ReturnsEmptyRegistry()
		{
			var emptyDict = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["resources"] = new List<object>()
			};

			var deserialized = ModComponentSerializationService.DeserializeResourceRegistry(emptyDict);

			Assert.Multiple(() =>
			{
				Assert.That(emptyDict, Is.Not.Null, "Empty dictionary should not be null");
				Assert.That(emptyDict.ContainsKey("resources"), Is.True, "Empty dictionary should contain 'resources' key");
				Assert.That(deserialized, Is.Not.Null, "Deserialized registry should not be null");
				Assert.That(deserialized, Is.Empty, "Deserialized registry should be empty");
			});
		}

		[Test]
		public void SerializeResourceRegistry_WithNullValues_HandlesGracefully()
		{
			var registry = new Dictionary<string, ResourceMetadata>
(StringComparer.Ordinal)
			{
				["hash1"] = new ResourceMetadata
				{
					ContentId = "id1",
					MetadataHash = "hash1",
					PrimaryUrl = "https://example.com/file.zip",
					ContentHashSHA256 = null, // Null before download
					PieceHashes = null
				}
			};

			var serialized = ModComponentSerializationService.SerializeResourceRegistry(registry);
			var deserialized = ModComponentSerializationService.DeserializeResourceRegistry(serialized);

			Assert.Multiple(() =>
			{
				Assert.That(registry, Is.Not.Null, "Registry should not be null");
				Assert.That(registry.Count, Is.EqualTo(1), "Registry should contain exactly 1 entry");
				Assert.That(serialized, Is.Not.Null, "Serialized dictionary should not be null");
				Assert.That(deserialized, Is.Not.Null, "Deserialized registry should not be null");
				Assert.That(deserialized.ContainsKey("hash1"), Is.True, "Deserialized registry should contain hash1");
				Assert.That(deserialized["hash1"], Is.Not.Null, "Hash1 metadata should not be null");
				Assert.That(deserialized["hash1"].ContentHashSHA256, Is.Null, "ContentHashSHA256 should remain null after round-trip");
				Assert.That(deserialized["hash1"].PieceHashes, Is.Null, "PieceHashes should remain null after round-trip");
			});
		}

		[Test]
		public void SerializeResourceRegistry_WithFiles_PreservesFilesDictionary()
		{
			var registry = new Dictionary<string, ResourceMetadata>
(StringComparer.Ordinal)
			{
				["hash1"] = new ResourceMetadata
				{
					ContentId = "id1",
					MetadataHash = "hash1",
					PrimaryUrl = "https://example.com/file.zip",
					Files = new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase)
					{
						["file1.zip"] = true,
						["file2.zip"] = false
					}
				}
			};

			var serialized = ModComponentSerializationService.SerializeResourceRegistry(registry);
			var deserialized = ModComponentSerializationService.DeserializeResourceRegistry(serialized);

			Assert.Multiple(() =>
			{
				Assert.That(registry, Is.Not.Null, "Registry should not be null");
				Assert.That(registry.Count, Is.EqualTo(1), "Registry should contain exactly 1 entry");
				Assert.That(serialized, Is.Not.Null, "Serialized dictionary should not be null");
				Assert.That(deserialized, Is.Not.Null, "Deserialized registry should not be null");
				Assert.That(deserialized.ContainsKey("hash1"), Is.True, "Deserialized registry should contain hash1");
				Assert.That(deserialized["hash1"], Is.Not.Null, "Hash1 metadata should not be null");
				Assert.That(deserialized["hash1"].Files, Is.Not.Null, "Files dictionary should not be null");
				Assert.That(deserialized["hash1"].Files, Has.Count.EqualTo(2), "Files dictionary should contain exactly 2 entries");
				Assert.That(deserialized["hash1"].Files.ContainsKey("file1.zip"), Is.True, "Files dictionary should contain file1.zip");
				Assert.That(deserialized["hash1"].Files.ContainsKey("file2.zip"), Is.True, "Files dictionary should contain file2.zip");
				Assert.That(deserialized["hash1"].Files["file1.zip"], Is.True, "file1.zip should have value true");
				Assert.That(deserialized["hash1"].Files["file2.zip"], Is.False, "file2.zip should have value false");
			});
		}

		[Test]
		public void SerializeResourceRegistry_WithTrustLevels_PreservesTrust()
		{
			var registry = new Dictionary<string, ResourceMetadata>
(StringComparer.Ordinal)
			{
				["hash1"] = new ResourceMetadata { ContentId = "id1", MetadataHash = "hash1", TrustLevel = MappingTrustLevel.Unverified },
				["hash2"] = new ResourceMetadata { ContentId = "id2", MetadataHash = "hash2", TrustLevel = MappingTrustLevel.ObservedOnce },
				["hash3"] = new ResourceMetadata { ContentId = "id3", MetadataHash = "hash3", TrustLevel = MappingTrustLevel.Verified }
			};

			var serialized = ModComponentSerializationService.SerializeResourceRegistry(registry);
			var deserialized = ModComponentSerializationService.DeserializeResourceRegistry(serialized);

			Assert.Multiple(() =>
			{
				Assert.That(registry, Is.Not.Null, "Registry should not be null");
				Assert.That(registry.Count, Is.EqualTo(3), "Registry should contain exactly 3 entries");
				Assert.That(serialized, Is.Not.Null, "Serialized dictionary should not be null");
				Assert.That(deserialized, Is.Not.Null, "Deserialized registry should not be null");
				Assert.That(deserialized, Has.Count.EqualTo(3), "Deserialized registry should contain exactly 3 entries");
				Assert.That(deserialized.ContainsKey("hash1"), Is.True, "Deserialized registry should contain hash1");
				Assert.That(deserialized.ContainsKey("hash2"), Is.True, "Deserialized registry should contain hash2");
				Assert.That(deserialized.ContainsKey("hash3"), Is.True, "Deserialized registry should contain hash3");
				Assert.That(deserialized["hash1"], Is.Not.Null, "Hash1 metadata should not be null");
				Assert.That(deserialized["hash2"], Is.Not.Null, "Hash2 metadata should not be null");
				Assert.That(deserialized["hash3"], Is.Not.Null, "Hash3 metadata should not be null");
				Assert.That(deserialized["hash1"].TrustLevel, Is.EqualTo(MappingTrustLevel.Unverified), "Hash1 trust level should be Unverified");
				Assert.That(deserialized["hash2"].TrustLevel, Is.EqualTo(MappingTrustLevel.ObservedOnce), "Hash2 trust level should be ObservedOnce");
				Assert.That(deserialized["hash3"].TrustLevel, Is.EqualTo(MappingTrustLevel.Verified), "Hash3 trust level should be Verified");
			});
		}

		[Test]
		public void SerializeResourceRegistry_WithHandlerMetadata_PreservesMetadata()
		{
			var registry = new Dictionary<string, ResourceMetadata>
(StringComparer.Ordinal)
			{
				["hash1"] = new ResourceMetadata
				{
					ContentId = "id1",
					MetadataHash = "hash1",
					HandlerMetadata = new Dictionary<string, object>(StringComparer.Ordinal)
					{
						["provider"] = "deadlystream",
						["fileId"] = "1234",
						["version"] = "1.0"
					}
				}
			};

			var serialized = ModComponentSerializationService.SerializeResourceRegistry(registry);
			var deserialized = ModComponentSerializationService.DeserializeResourceRegistry(serialized);

			Assert.Multiple(() =>
			{
				Assert.That(registry, Is.Not.Null, "Registry should not be null");
				Assert.That(registry.Count, Is.EqualTo(1), "Registry should contain exactly 1 entry");
				Assert.That(serialized, Is.Not.Null, "Serialized dictionary should not be null");
				Assert.That(deserialized, Is.Not.Null, "Deserialized registry should not be null");
				Assert.That(deserialized.ContainsKey("hash1"), Is.True, "Deserialized registry should contain hash1");
				Assert.That(deserialized["hash1"], Is.Not.Null, "Hash1 metadata should not be null");
				Assert.That(deserialized["hash1"].HandlerMetadata, Is.Not.Null, "HandlerMetadata should not be null");
				Assert.That(deserialized["hash1"].HandlerMetadata.ContainsKey("provider"), Is.True, "HandlerMetadata should contain provider key");
				Assert.That(deserialized["hash1"].HandlerMetadata.ContainsKey("fileId"), Is.True, "HandlerMetadata should contain fileId key");
				Assert.That(deserialized["hash1"].HandlerMetadata["provider"], Is.EqualTo("deadlystream"), "Provider should match original");
				Assert.That(deserialized["hash1"].HandlerMetadata["fileId"], Is.EqualTo("1234"), "FileId should match original");
			});
		}

		[Test]
		public void RoundTrip_CompleteResourceMetadata_PreservesAllFields()
		{
			var original = new ResourceMetadata
			{
				ContentId = "abc123def456",
				MetadataHash = "hash789",
				PrimaryUrl = "https://example.com/mod.zip",
				FileSize = 2048,
				ContentHashSHA256 = "sha256hash",
				PieceLength = 65536,
				PieceHashes = "abcdef123456",
				FirstSeen = DateTime.UtcNow,
				LastVerified = DateTime.UtcNow.AddHours(-1),
				SchemaVersion = 1,
				TrustLevel = MappingTrustLevel.Verified,
				Files = new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase) { ["test.zip"] = true },
				HandlerMetadata = new Dictionary<string, object>(StringComparer.Ordinal) { ["key"] = "value" }
			};

			var registry = new Dictionary<string, ResourceMetadata>(StringComparer.Ordinal) { ["hash789"] = original };

			var serialized = ModComponentSerializationService.SerializeResourceRegistry(registry);
			var deserialized = ModComponentSerializationService.DeserializeResourceRegistry(serialized);
			var result = deserialized["hash789"];

			Assert.Multiple(() =>
			{
				Assert.That(original, Is.Not.Null, "Original metadata should not be null");
				Assert.That(registry, Is.Not.Null, "Registry should not be null");
				Assert.That(registry.Count, Is.EqualTo(1), "Registry should contain exactly 1 entry");
				Assert.That(serialized, Is.Not.Null, "Serialized dictionary should not be null");
				Assert.That(deserialized, Is.Not.Null, "Deserialized registry should not be null");
				Assert.That(deserialized.ContainsKey("hash789"), Is.True, "Deserialized registry should contain hash789");
				Assert.That(result, Is.Not.Null, "Result metadata should not be null");
				Assert.That(result.ContentId, Is.EqualTo(original.ContentId), "ContentId should match original");
				Assert.That(result.MetadataHash, Is.EqualTo(original.MetadataHash), "MetadataHash should match original");
				Assert.That(result.PrimaryUrl, Is.EqualTo(original.PrimaryUrl), "PrimaryUrl should match original");
				Assert.That(result.FileSize, Is.EqualTo(original.FileSize), "FileSize should match original");
				Assert.That(result.ContentHashSHA256, Is.EqualTo(original.ContentHashSHA256), "ContentHashSHA256 should match original");
				Assert.That(result.PieceLength, Is.EqualTo(original.PieceLength), "PieceLength should match original");
				Assert.That(result.PieceHashes, Is.EqualTo(original.PieceHashes), "PieceHashes should match original");
				Assert.That(result.SchemaVersion, Is.EqualTo(original.SchemaVersion), "SchemaVersion should match original");
				Assert.That(result.TrustLevel, Is.EqualTo(original.TrustLevel), "TrustLevel should match original");
			});
		}
	}
}
