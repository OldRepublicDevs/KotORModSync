// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using KOTORModSync.Core.Utility;
using Octodiff.Core;
using Octodiff.Diagnostics;

namespace KOTORModSync.Core.Services.ImmutableCheckpoint
{
	/// <summary>
	/// Handles binary diff/patch operations using Octodiff for efficient file storage.
	/// Supports bidirectional deltas for fast forward/backward navigation.
	/// </summary>
	public class BinaryDiffService
	{
		private const int MIN_FILE_SIZE_FOR_DIFF = 1024 * 100; // 100KB minimum to use diff
		private readonly ContentAddressableStore _casStore;

		public BinaryDiffService(ContentAddressableStore casStore)
		{
			_casStore = casStore ?? throw new ArgumentNullException(nameof(casStore));
		}

		/// <summary>
		/// Creates bidirectional binary deltas between two files and stores them in CAS.
		/// Returns FileDelta with both forward and reverse delta hashes.
		/// </summary>
		public async Task<FileDelta> CreateBidirectionalDeltaAsync(
			string sourceFilePath,
			string targetFilePath,
			string relativePath,
			CancellationToken cancellationToken = default)
		{
			if ( !File.Exists(sourceFilePath) )
				throw new FileNotFoundException($"Source file not found: {sourceFilePath}");
			if ( !File.Exists(targetFilePath) )
				throw new FileNotFoundException($"Target file not found: {targetFilePath}");

			var sourceInfo = new FileInfo(sourceFilePath);
			var targetInfo = new FileInfo(targetFilePath);

			await Logger.LogVerboseAsync($"[BinaryDiff] Creating bidirectional delta for {relativePath}");

			// Compute file hashes
			string sourceHash = await ComputeFileHashAsync(sourceFilePath, cancellationToken);
			string targetHash = await ComputeFileHashAsync(targetFilePath, cancellationToken);

			// If files are identical, no delta needed
			if ( sourceHash == targetHash )
			{
				await Logger.LogVerboseAsync($"[BinaryDiff] Files identical, skipping delta for {relativePath}");
				return null;
			}

			// Store source and target in CAS (for deduplication)
			string sourceCASHash = await _casStore.StoreFileAsync(sourceFilePath);
			string targetCASHash = await _casStore.StoreFileAsync(targetFilePath);

			// For small files, just reference the full files in CAS
			if ( targetInfo.Length < MIN_FILE_SIZE_FOR_DIFF )
			{
				await Logger.LogVerboseAsync($"[BinaryDiff] File too small for delta, storing full file: {relativePath}");
				return new FileDelta
				{
					Path = relativePath,
					SourceHash = sourceHash,
					TargetHash = targetHash,
					SourceCASHash = sourceCASHash,
					TargetCASHash = targetCASHash,
					SourceSize = sourceInfo.Length,
					TargetSize = targetInfo.Length,
					ForwardDeltaSize = targetInfo.Length,
					ReverseDeltaSize = sourceInfo.Length,
					Method = "full_copy"
				};
			}

			// Create forward delta (source -> target)
			string forwardDeltaHash;
			long forwardDeltaSize;
			using ( var forwardDeltaStream = new MemoryStream() )
			{
				forwardDeltaSize = await BinaryDiffService.CreateOctodiffDeltaAsync(
					sourceFilePath,
					targetFilePath,
					forwardDeltaStream,
					cancellationToken);

				forwardDeltaHash = await _casStore.StoreStreamAsync(forwardDeltaStream);
			}

			// Create reverse delta (target -> source)
			string reverseDeltaHash;
			long reverseDeltaSize;
			using ( var reverseDeltaStream = new MemoryStream() )
			{
				reverseDeltaSize = await BinaryDiffService.CreateOctodiffDeltaAsync(
					targetFilePath,
					sourceFilePath,
					reverseDeltaStream,
					cancellationToken);

				reverseDeltaHash = await _casStore.StoreStreamAsync(reverseDeltaStream);
			}

			await Logger.LogAsync(
				$"[BinaryDiff] Created deltas for {relativePath}: " +
				$"Forward={forwardDeltaSize:N0} bytes, Reverse={reverseDeltaSize:N0} bytes " +
				$"(Original: {targetInfo.Length:N0} bytes, Saved: {targetInfo.Length - forwardDeltaSize:N0} bytes)"
			);

			return new FileDelta
			{
				Path = relativePath,
				SourceHash = sourceHash,
				TargetHash = targetHash,
				SourceCASHash = sourceCASHash,
				TargetCASHash = targetCASHash,
				ForwardDeltaCASHash = forwardDeltaHash,
				ReverseDeltaCASHash = reverseDeltaHash,
				SourceSize = sourceInfo.Length,
				TargetSize = targetInfo.Length,
				ForwardDeltaSize = forwardDeltaSize,
				ReverseDeltaSize = reverseDeltaSize,
				Method = "octodiff"
			};
		}

		/// <summary>
		/// Applies a forward delta to recreate the target file.
		/// </summary>
		public async Task ApplyForwardDeltaAsync(
			FileDelta delta,
			string outputFilePath,
			CancellationToken cancellationToken = default)
		{
			if ( delta == null )
				throw new ArgumentNullException(nameof(delta));

			await Logger.LogVerboseAsync($"[BinaryDiff] Applying forward delta for {delta.Path}");

			// If it's a full copy, just retrieve from CAS
			if ( delta.Method == "full_copy" )
			{
				await _casStore.RetrieveFileAsync(delta.TargetCASHash, outputFilePath);
				return;
			}

			// Apply octodiff delta
			using ( var sourceStream = _casStore.OpenReadStream(delta.SourceCASHash) )
			using ( var deltaStream = _casStore.OpenReadStream(delta.ForwardDeltaCASHash) )
			{
				await BinaryDiffService.ApplyOctodiffDeltaAsync(sourceStream, deltaStream, outputFilePath, cancellationToken);
			}
		}

		/// <summary>
		/// Applies a reverse delta to recreate the source file.
		/// </summary>
		public async Task ApplyReverseDeltaAsync(
			FileDelta delta,
			string outputFilePath,
			CancellationToken cancellationToken = default)
		{
			if ( delta == null )
				throw new ArgumentNullException(nameof(delta));

			await Logger.LogVerboseAsync($"[BinaryDiff] Applying reverse delta for {delta.Path}");

			// If it's a full copy, just retrieve from CAS
			if ( delta.Method == "full_copy" )
			{
				await _casStore.RetrieveFileAsync(delta.SourceCASHash, outputFilePath);
				return;
			}

			// Apply octodiff reverse delta
			using ( var targetStream = _casStore.OpenReadStream(delta.TargetCASHash) )
			using ( var reverseDeltaStream = _casStore.OpenReadStream(delta.ReverseDeltaCASHash) )
			{
				await BinaryDiffService.ApplyOctodiffDeltaAsync(targetStream, reverseDeltaStream, outputFilePath, cancellationToken);
			}
		}

		/// <summary>
		/// Creates an octodiff delta between source and target files.
		/// </summary>
		private static async Task<long> CreateOctodiffDeltaAsync(
			string basisFilePath,
			string newFilePath,
			Stream deltaOutputStream,
			CancellationToken cancellationToken)
		{
			await Task.Run(() =>
			{
				cancellationToken.ThrowIfCancellationRequested();

				// Create signature of basis file
				using ( var basisStream = new FileStream(basisFilePath, FileMode.Open, FileAccess.Read, FileShare.Read) )
				using ( var signatureStream = new MemoryStream() )
				{
					var signatureBuilder = new SignatureBuilder();
					signatureBuilder.Build(basisStream, new SignatureWriter(signatureStream));
					signatureStream.Position = 0;

					// Create delta from signature and new file
					using ( var newFileStream = new FileStream(newFilePath, FileMode.Open, FileAccess.Read, FileShare.Read) )
					{
						var deltaBuilder = new DeltaBuilder();
						deltaBuilder.BuildDelta(
							newFileStream,
							new SignatureReader(signatureStream, new NullProgressReporter()),
							new AggregateCopyOperationsDecorator(new BinaryDeltaWriter(deltaOutputStream))
						);
					}
				}
			}, cancellationToken);

			return deltaOutputStream.Length;
		}

		/// <summary>
		/// Applies an octodiff delta to a basis file to recreate the new file.
		/// </summary>
		private static async Task ApplyOctodiffDeltaAsync(
			Stream basisStream,
			Stream deltaStream,
			string outputFilePath,
			CancellationToken cancellationToken)
		{
			await Task.Run(() =>
			{
				cancellationToken.ThrowIfCancellationRequested();

				// Ensure output directory exists
				string outputDir = Path.GetDirectoryName(outputFilePath);
				if ( !string.IsNullOrEmpty(outputDir) )
					Directory.CreateDirectory(outputDir);

				using ( var outputStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.None) )
				{
					var deltaApplier = new DeltaApplier();
					deltaApplier.Apply(
						basisStream,
						new BinaryDeltaReader(deltaStream, new NullProgressReporter()),
						outputStream
					);
				}
			}, cancellationToken);
		}

		/// <summary>
		/// Computes SHA256 hash of a file.
		/// </summary>
		private async Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken)
		{
			return await ContentAddressableStore.ComputeFileHashAsync(filePath);
		}

		/// <summary>
		/// Null progress reporter for Octodiff (we log progress ourselves).
		/// </summary>
		private class NullProgressReporter : IProgressReporter
		{
			public void ReportProgress(string operation, long currentPosition, long total)
			{
				// No-op
			}
		}
	}
}
