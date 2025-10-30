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
( StringComparer.Ordinal )
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

			var serialized = ModComponentSerializationService.SerializeResourceRegistry( registry );

			Assert.That( serialized, Is.Not.Null );
			Assert.That( serialized.ContainsKey( "resources" ), Is.True );
		}

		[Test]
		public void DeserializeResourceRegistry_WithValidData_ReconstructsRegistry()
		{
			var original = new Dictionary<string, ResourceMetadata>
( StringComparer.Ordinal )
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

			var serialized = ModComponentSerializationService.SerializeResourceRegistry( original );
			var deserialized = ModComponentSerializationService.DeserializeResourceRegistry( serialized );

			Assert.That( deserialized, Has.Count.EqualTo( 1 ) );
			Assert.That( deserialized["hash123"].ContentId, Is.EqualTo( "abc123" ) );
			Assert.That( deserialized["hash123"].PrimaryUrl, Is.EqualTo( "https://example.com/file.zip" ) );
		}

		[Test]
		public void SerializeResourceRegistry_WithMultipleEntries_PreservesAll()
		{
			var registry = new Dictionary<string, ResourceMetadata>
( StringComparer.Ordinal )
			{
				["hash1"] = new ResourceMetadata { ContentId = "id1", MetadataHash = "hash1" },
				["hash2"] = new ResourceMetadata { ContentId = "id2", MetadataHash = "hash2" },
				["hash3"] = new ResourceMetadata { ContentId = "id3", MetadataHash = "hash3" }
			};

			var serialized = ModComponentSerializationService.SerializeResourceRegistry( registry );
			var deserialized = ModComponentSerializationService.DeserializeResourceRegistry( serialized );

			Assert.That( deserialized, Has.Count.EqualTo( 3 ) );
			Assert.That( deserialized.ContainsKey( "hash1" ), Is.True );
			Assert.That( deserialized.ContainsKey( "hash2" ), Is.True );
			Assert.That( deserialized.ContainsKey( "hash3" ), Is.True );
		}

		[Test]
		public void SerializeResourceRegistry_WithEmptyRegistry_ProducesEmptyDict()
		{
			var registry = new Dictionary<string, ResourceMetadata>( StringComparer.Ordinal );

			var serialized = ModComponentSerializationService.SerializeResourceRegistry( registry );

			Assert.That( serialized, Is.Not.Null );
			Assert.That( serialized.ContainsKey( "resources" ), Is.True );
		}

		[Test]
		public void DeserializeResourceRegistry_WithEmptyDict_ReturnsEmptyRegistry()
		{
			var emptyDict = new Dictionary<string, object>( StringComparer.Ordinal )
			{
				["resources"] = new List<object>()
			};

			var deserialized = ModComponentSerializationService.DeserializeResourceRegistry( emptyDict );

			Assert.That( deserialized, Is.Not.Null );
			Assert.That( deserialized, Is.Empty );
		}

		[Test]
		public void SerializeResourceRegistry_WithNullValues_HandlesGracefully()
		{
			var registry = new Dictionary<string, ResourceMetadata>
( StringComparer.Ordinal )
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

			var serialized = ModComponentSerializationService.SerializeResourceRegistry( registry );
			var deserialized = ModComponentSerializationService.DeserializeResourceRegistry( serialized );

			Assert.That( deserialized["hash1"].ContentHashSHA256, Is.Null );
			Assert.That( deserialized["hash1"].PieceHashes, Is.Null );
		}

		[Test]
		public void SerializeResourceRegistry_WithFiles_PreservesFilesDictionary()
		{
			var registry = new Dictionary<string, ResourceMetadata>
( StringComparer.Ordinal )
			{
				["hash1"] = new ResourceMetadata
				{
					ContentId = "id1",
					MetadataHash = "hash1",
					PrimaryUrl = "https://example.com/file.zip",
					Files = new Dictionary<string, bool?>( StringComparer.OrdinalIgnoreCase )
					{
						["file1.zip"] = true,
						["file2.zip"] = false
					}
				}
			};

			var serialized = ModComponentSerializationService.SerializeResourceRegistry( registry );
			var deserialized = ModComponentSerializationService.DeserializeResourceRegistry( serialized );

			Assert.That( deserialized["hash1"].Files, Has.Count.EqualTo( 2 ) );
			Assert.That( deserialized["hash1"].Files["file1.zip"], Is.True );
			Assert.That( deserialized["hash1"].Files["file2.zip"], Is.False );
		}

		[Test]
		public void SerializeResourceRegistry_WithTrustLevels_PreservesTrust()
		{
			var registry = new Dictionary<string, ResourceMetadata>
( StringComparer.Ordinal )
			{
				["hash1"] = new ResourceMetadata { ContentId = "id1", MetadataHash = "hash1", TrustLevel = MappingTrustLevel.Unverified },
				["hash2"] = new ResourceMetadata { ContentId = "id2", MetadataHash = "hash2", TrustLevel = MappingTrustLevel.ObservedOnce },
				["hash3"] = new ResourceMetadata { ContentId = "id3", MetadataHash = "hash3", TrustLevel = MappingTrustLevel.Verified }
			};

			var serialized = ModComponentSerializationService.SerializeResourceRegistry( registry );
			var deserialized = ModComponentSerializationService.DeserializeResourceRegistry( serialized );

			Assert.That( deserialized["hash1"].TrustLevel, Is.EqualTo( MappingTrustLevel.Unverified ) );
			Assert.That( deserialized["hash2"].TrustLevel, Is.EqualTo( MappingTrustLevel.ObservedOnce ) );
			Assert.That( deserialized["hash3"].TrustLevel, Is.EqualTo( MappingTrustLevel.Verified ) );
		}

		[Test]
		public void SerializeResourceRegistry_WithHandlerMetadata_PreservesMetadata()
		{
			var registry = new Dictionary<string, ResourceMetadata>
( StringComparer.Ordinal )
			{
				["hash1"] = new ResourceMetadata
				{
					ContentId = "id1",
					MetadataHash = "hash1",
					HandlerMetadata = new Dictionary<string, object>( StringComparer.Ordinal )
					{
						["provider"] = "deadlystream",
						["fileId"] = "1234",
						["version"] = "1.0"
					}
				}
			};

			var serialized = ModComponentSerializationService.SerializeResourceRegistry( registry );
			var deserialized = ModComponentSerializationService.DeserializeResourceRegistry( serialized );

			Assert.That( deserialized["hash1"].HandlerMetadata, Is.Not.Null );
			Assert.That( deserialized["hash1"].HandlerMetadata["provider"], Is.EqualTo( "deadlystream" ) );
			Assert.That( deserialized["hash1"].HandlerMetadata["fileId"], Is.EqualTo( "1234" ) );
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
				LastVerified = DateTime.UtcNow.AddHours( -1 ),
				SchemaVersion = 1,
				TrustLevel = MappingTrustLevel.Verified,
				Files = new Dictionary<string, bool?>( StringComparer.OrdinalIgnoreCase ) { ["test.zip"] = true },
				HandlerMetadata = new Dictionary<string, object>( StringComparer.Ordinal ) { ["key"] = "value" }
			};

			var registry = new Dictionary<string, ResourceMetadata>( StringComparer.Ordinal ) { ["hash789"] = original };

			var serialized = ModComponentSerializationService.SerializeResourceRegistry( registry );
			var deserialized = ModComponentSerializationService.DeserializeResourceRegistry( serialized );
			var result = deserialized["hash789"];

			Assert.That( result.ContentId, Is.EqualTo( original.ContentId ) );
			Assert.That( result.MetadataHash, Is.EqualTo( original.MetadataHash ) );
			Assert.That( result.PrimaryUrl, Is.EqualTo( original.PrimaryUrl ) );
			Assert.That( result.FileSize, Is.EqualTo( original.FileSize ) );
			Assert.That( result.ContentHashSHA256, Is.EqualTo( original.ContentHashSHA256 ) );
			Assert.That( result.PieceLength, Is.EqualTo( original.PieceLength ) );
			Assert.That( result.PieceHashes, Is.EqualTo( original.PieceHashes ) );
			Assert.That( result.SchemaVersion, Is.EqualTo( original.SchemaVersion ) );
			Assert.That( result.TrustLevel, Is.EqualTo( original.TrustLevel ) );
		}
	}
}