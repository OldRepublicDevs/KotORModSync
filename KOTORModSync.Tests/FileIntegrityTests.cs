// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.IO;
using System.Text;
using System.Threading.Tasks;

using KOTORModSync.Core;
using KOTORModSync.Core.Services.Download;

using NUnit.Framework;

namespace KOTORModSync.Tests
{
	[TestFixture]
	public class FileIntegrityTests
	{
		private string? _testDirectory;

		[SetUp]
		public void Setup()
		{
			_testDirectory = Path.Combine( Path.GetTempPath(), "KOTORModSync_IntegrityTests_" + Path.GetRandomFileName() );
			Directory.CreateDirectory( _testDirectory );
		}

		[TearDown]
		public void Teardown()
		{
			if (Directory.Exists( _testDirectory ))
			{
				Directory.Delete( _testDirectory, true );
			}
		}

		[Test]
		public async Task ComputeFileIntegrityData_WithSmallFile_ProducesCorrectHashes()
		{
			string testFile = Path.Combine( _testDirectory, "test.txt" );
			File.WriteAllText( testFile, "Hello, World!" );

			var (sha256, pieceLength, pieceHashes) = await DownloadCacheOptimizer.ComputeFileIntegrityData( testFile );

			// SHA-256 should be 64 hex characters
			Assert.That( sha256, Has.Length.EqualTo( 64 ) );
			Assert.That( sha256, Does.Match( "^[0-9a-f]+$" ) );

			// Piece length should be reasonable for small file
			Assert.That( pieceLength, Is.GreaterThan( 0 ) );

			// Piece hashes (40 hex chars per piece)
			Assert.That( pieceHashes.Length % 40, Is.EqualTo( 0 ) );
		}

		[Test]
		public async Task ComputeFileIntegrityData_WithLargeFile_UsesLargerPieceSize()
		{
			string testFile = Path.Combine( _testDirectory, "large.bin" );
			byte[] largeData = new byte[10 * 1024 * 1024]; // 10 MB
			File.WriteAllBytes( testFile, largeData );

			var (sha256, pieceLength, pieceHashes) = await DownloadCacheOptimizer.ComputeFileIntegrityData( testFile );

			// Piece length should be larger for large files
			Assert.That( pieceLength, Is.GreaterThanOrEqualTo( 65536 ) );

			// Should have multiple pieces
			int pieceCount = pieceHashes.Length / 40;
			Assert.That( pieceCount, Is.GreaterThan( 1 ) );
		}

		[Test]
		public async Task ComputeFileIntegrityData_SameFile_ProducesSameHashes()
		{
			string testFile = Path.Combine( _testDirectory, "deterministic.txt" );
			File.WriteAllText( testFile, "Deterministic content" );

			var result1 = await DownloadCacheOptimizer.ComputeFileIntegrityData( testFile );
			var result2 = await DownloadCacheOptimizer.ComputeFileIntegrityData( testFile );

			Assert.That( result1.contentHashSHA256, Is.EqualTo( result2.contentHashSHA256 ) );
			Assert.That( result1.pieceLength, Is.EqualTo( result2.pieceLength ) );
			Assert.That( result1.pieceHashes, Is.EqualTo( result2.pieceHashes ) );
		}

		[Test]
		public async Task VerifyContentIntegrity_WithValidFile_ReturnsTrue()
		{
			string testFile = Path.Combine( _testDirectory, "valid.txt" );
			File.WriteAllText( testFile, "Valid content for verification" );

			var (sha256, pieceLength, pieceHashes) = await DownloadCacheOptimizer.ComputeFileIntegrityData( testFile );

			var metadata = new ResourceMetadata
			{
				ContentHashSHA256 = sha256,
				PieceLength = pieceLength,
				PieceHashes = pieceHashes,
				FileSize = new FileInfo( testFile ).Length
			};

			bool isValid = await DownloadCacheOptimizer.VerifyContentIntegrity( testFile, metadata );

			Assert.That( isValid, Is.True );
		}

		[Test]
		public async Task VerifyContentIntegrity_WithModifiedFile_ReturnsFalse()
		{
			string testFile = Path.Combine( _testDirectory, "modified.txt" );
			File.WriteAllText( testFile, "Original content" );

			var (sha256, pieceLength, pieceHashes) = await DownloadCacheOptimizer.ComputeFileIntegrityData( testFile );

			var metadata = new ResourceMetadata
			{
				ContentHashSHA256 = sha256,
				PieceLength = pieceLength,
				PieceHashes = pieceHashes,
				FileSize = new FileInfo( testFile ).Length
			};

			// Modify the file
			File.WriteAllText( testFile, "Modified content" );

			bool isValid = await DownloadCacheOptimizer.VerifyContentIntegrity( testFile, metadata );

			Assert.That( isValid, Is.False );
		}

		[Test]
		public async Task VerifyContentIntegrity_WithWrongSize_ReturnsFalse()
		{
			string testFile = Path.Combine( _testDirectory, "size_test.txt" );
			File.WriteAllText( testFile, "Size test" );

			var (sha256, pieceLength, pieceHashes) = await DownloadCacheOptimizer.ComputeFileIntegrityData( testFile );

			var metadata = new ResourceMetadata
			{
				ContentHashSHA256 = sha256,
				PieceLength = pieceLength,
				PieceHashes = pieceHashes,
				FileSize = 99999 // Wrong size
			};

			bool isValid = await DownloadCacheOptimizer.VerifyContentIntegrity( testFile, metadata );

			Assert.That( isValid, Is.False );
		}

		[Test]
		public void DeterminePieceSize_WithSmallFile_Returns64KB()
		{
			long fileSize = 1024 * 1024; // 1 MB
			int pieceSize = DownloadCacheOptimizer.DeterminePieceSize( fileSize );

			Assert.That( pieceSize, Is.EqualTo( 65536 ) ); // 64 KB
		}

		[Test]
		public void DeterminePieceSize_WithLargeFile_ReturnsLargerPiece()
		{
			long fileSize = 100L * 1024 * 1024 * 1024; // 100 GB
			int pieceSize = DownloadCacheOptimizer.DeterminePieceSize( fileSize );

			// Should use larger piece size for huge files
			Assert.That( pieceSize, Is.GreaterThanOrEqualTo( 131072 ) ); // >= 128 KB
		}

		[Test]
		public void DeterminePieceSize_EnsuresMaxPieceCount()
		{
			long fileSize = 10L * 1024 * 1024 * 1024; // 10 GB
			int pieceSize = DownloadCacheOptimizer.DeterminePieceSize( fileSize );

			long pieceCount = (fileSize + pieceSize - 1) / pieceSize;

			// Must not exceed 2^20 pieces (1,048,576)
			Assert.That( pieceCount, Is.LessThanOrEqualTo( 1048576 ) );
		}

		[Test]
		public async Task VerifyPieceHashesFromStored_WithValidPieces_ReturnsTrue()
		{
			string testFile = Path.Combine( _testDirectory, "piece_test.txt" );
			File.WriteAllText( testFile, "Content for piece verification" );

			var (sha256, pieceLength, pieceHashes) = await DownloadCacheOptimizer.ComputeFileIntegrityData( testFile );

			bool isValid = await DownloadCacheOptimizer.VerifyPieceHashesFromStored( testFile, pieceLength, pieceHashes );

			Assert.That( isValid, Is.True );
		}

		[Test]
		public async Task VerifyPieceHashesFromStored_WithCorruptedPiece_ReturnsFalse()
		{
			string testFile = Path.Combine( _testDirectory, "corrupt_piece.txt" );
			File.WriteAllText( testFile, "Original piece data" );

			var (sha256, pieceLength, pieceHashes) = await DownloadCacheOptimizer.ComputeFileIntegrityData( testFile );

			// Corrupt the file
			File.WriteAllText( testFile, "Corrupted piece data" );

			bool isValid = await DownloadCacheOptimizer.VerifyPieceHashesFromStored( testFile, pieceLength, pieceHashes );

			Assert.That( isValid, Is.False );
		}
	}
}