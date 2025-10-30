// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace KOTORModSync.Core.Services.ImmutableCheckpoint
{
	public class ContentAddressableStore
	{
		private readonly string _objectsDirectory;
		private readonly object _lockObject = new object();

		public ContentAddressableStore( string checkpointDirectory )
		{
			if (string.IsNullOrWhiteSpace( checkpointDirectory ))
				throw new ArgumentNullException( nameof( checkpointDirectory ) );

			_objectsDirectory = Path.Combine( checkpointDirectory, "objects" );
			Directory.CreateDirectory( _objectsDirectory );
		}

		public static async Task<string> ComputeFileHashAsync( string filePath )
		{
			if (!File.Exists( filePath ))
				throw new FileNotFoundException( $"File not found: {filePath}" );

			using (SHA256 sha256 = SHA256.Create())
			{
				using (FileStream stream = new FileStream( filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 8192, useAsync: true ))


				{
					byte[] hashBytes = await Task.Run( () => sha256.ComputeHash( stream ) ).ConfigureAwait( false );
					return BitConverter.ToString( hashBytes ).Replace( "-", "" ).ToLowerInvariant();
				}
			}
		}

		public static async Task<string> ComputeStreamHashAsync( Stream stream )
		{
			if (stream is null)
				throw new ArgumentNullException( nameof( stream ) );

			long originalPosition = stream.Position;
			stream.Position = 0;

			using (SHA256 sha256 = SHA256.Create())


			{
				byte[] hashBytes = await Task.Run( () => sha256.ComputeHash( stream ) ).ConfigureAwait( false );
				stream.Position = originalPosition;
				return BitConverter.ToString( hashBytes ).Replace( "-", "" ).ToLowerInvariant();
			}
		}

		public async Task<string> StoreFileAsync( string filePath )
		{
			if (!File.Exists( filePath ))
				throw new FileNotFoundException( $"File not found: {filePath}" );



			string hash = await ComputeFileHashAsync( filePath ).ConfigureAwait( false );

			if (HasObject( hash ))


			{
				await Logger.LogVerboseAsync( $"[CAS] File already exists in CAS: {hash}" ).ConfigureAwait( false );
				return hash;
			}

			string casPath = GetObjectPath(

hash );
			string casDirectory = Path.GetDirectoryName( casPath );
			Directory.CreateDirectory( casDirectory );

			lock (_lockObject)
			{
				if (File.Exists( casPath ))
				{
					Logger.LogVerbose( $"[CAS] File created by another thread: {hash}" );
					return hash;
				}

				File.Copy( filePath, casPath, overwrite: false );
			}

			await Logger.LogVerboseAsync( $"[CAS] Stored file: {hash} ({new FileInfo( filePath ).Length} bytes)" ).ConfigureAwait( false );
			return hash;
		}

		public async Task<string> StoreStreamAsync( Stream stream )
		{
			if (stream is null)
				throw new ArgumentNullException( nameof( stream ) );



			string hash = await ComputeStreamHashAsync( stream ).ConfigureAwait( false );

			if (HasObject( hash ))


			{
				await Logger.LogVerboseAsync( $"[CAS] Stream already exists in CAS: {hash}" ).ConfigureAwait( false );
				return hash;
			}

			string casPath = GetObjectPath( hash );
			string casDirectory = Path.GetDirectoryName( casPath );
			Directory.CreateDirectory( casDirectory );

			stream.Position = 0;

			lock (_lockObject)
			{
				if (File.Exists( casPath ))
				{
					Logger.LogVerbose( $"[CAS] Stream created by another thread: {hash}" );
					return hash;
				}

				using (FileStream fileStream = new FileStream( casPath, FileMode.CreateNew, FileAccess.Write, FileShare.None ))
				{
					stream.CopyTo( fileStream );


				}
			}

			await Logger.LogVerboseAsync( $"[CAS] Stored stream: {hash} ({stream.Length} bytes)" ).ConfigureAwait( false );
			return hash;
		}

		public async Task<string> RetrieveFileAsync( string hash, string outputPath )
		{
			if (string.IsNullOrWhiteSpace( hash ))
				throw new ArgumentNullException( nameof( hash ) );
			if (string.IsNullOrWhiteSpace( outputPath ))
				throw new ArgumentNullException( nameof( outputPath ) );

			string casPath = GetObjectPath( hash );
			if (!File.Exists( casPath ))
				throw new FileNotFoundException( $"Object not found in CAS: {hash}" );

			string outputDirectory = Path.GetDirectoryName( outputPath );
			if (!string.IsNullOrEmpty( outputDirectory ))
				Directory.CreateDirectory( outputDirectory );

			File.Copy( casPath, outputPath, overwrite: true );


			await Logger.LogVerboseAsync( $"[CAS] Retrieved file: {hash} -> {outputPath}" ).ConfigureAwait( false );
			return outputPath;
		}

		public Stream OpenReadStream( string hash )
		{
			if (string.IsNullOrWhiteSpace( hash ))
				throw new ArgumentNullException( nameof( hash ) );

			string casPath = GetObjectPath( hash );
			if (!File.Exists( casPath ))
				throw new FileNotFoundException( $"Object not found in CAS: {hash}" );

			return new FileStream( casPath, FileMode.Open, FileAccess.Read, FileShare.Read );
		}

		public bool HasObject( string hash )
		{
			if (string.IsNullOrWhiteSpace( hash ))
				return false;

			string casPath = GetObjectPath( hash );
			return File.Exists( casPath );
		}

		private string GetObjectPath( string hash )
		{
			if (string.IsNullOrWhiteSpace( hash ) || hash.Length < 3)
				throw new ArgumentException( "Invalid hash", nameof( hash ) );

			string prefix = hash.Substring( 0, 2 );
			string suffix = hash.Substring( 2 );
			return Path.Combine( _objectsDirectory, prefix, suffix );
		}

		public IEnumerable<string> GetAllObjectHashes()
		{
			if (!Directory.Exists( _objectsDirectory ))
				return Enumerable.Empty<string>();

			List<string> hashes = new List<string>();

			foreach (string prefixDir in Directory.GetDirectories( _objectsDirectory ))
			{
				string prefix = Path.GetFileName( prefixDir );
				foreach (string objectFile in Directory.GetFiles( prefixDir ))
				{
					string suffix = Path.GetFileName( objectFile );
					hashes.Add( prefix + suffix );
				}
			}

			return hashes;
		}

		public async Task<bool> DeleteObjectAsync( string hash )
		{
			if (string.IsNullOrWhiteSpace( hash ))
				return false;

			string casPath = GetObjectPath( hash );
			if (!File.Exists( casPath ))
				return false;

			try
			{
				File.Delete( casPath );


				await Logger.LogVerboseAsync( $"[CAS] Deleted object: {hash}" ).ConfigureAwait( false );
				return true;
			}
			catch (Exception ex)


			{
				await Logger.LogErrorAsync( $"[CAS] Failed to delete object {hash}: {ex.Message}" ).ConfigureAwait( false );
				return false;
			}
		}

		public long GetTotalSize()
		{
			if (!Directory.Exists( _objectsDirectory ))
				return 0;

			long total = 0;

			foreach (string prefixDir in Directory.GetDirectories( _objectsDirectory ))
			{
				foreach (string objectFile in Directory.GetFiles( prefixDir ))
				{
					try
					{
						total += new FileInfo( objectFile ).Length;
					}
					catch
					{
					}
				}
			}

			return total;
		}

		public async Task<int> GarbageCollectAsync( HashSet<string> referencedHashes )
		{
			if (referencedHashes is null)
				throw new ArgumentNullException( nameof( referencedHashes ) );



			await Logger.LogAsync( "[CAS] Starting garbage collection..." )

.ConfigureAwait( false );

			List<string> allHashes = GetAllObjectHashes().ToList();
			List<string> orphanedHashes = allHashes.Where( h => !referencedHashes.Contains( h ) ).ToList();

			await Logger.LogAsync( $"[CAS] Found {orphanedHashes.Count} orphaned objects out of {allHashes.Count} total" ).ConfigureAwait( false );

			int deleted = 0;
			foreach (string hash in orphanedHashes)
			{
				if (await DeleteObjectAsync( hash ).ConfigureAwait( false ))
					deleted++;


			}

			await Logger.LogAsync( $"[CAS] Garbage collection complete: Deleted {deleted} orphaned objects" ).ConfigureAwait( false );
			return deleted;
		}
	}
}