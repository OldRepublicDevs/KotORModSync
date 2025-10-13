



using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Installation
{
	public sealed class BackupManager
	{
		private const string SessionFolderName = ".kotor_modsync";
		private const string BackupFileName = "last_good_backup.zip";
		private const string TempWorkingFolderPrefix = "KOTORModSync_Backup_";
		private const string TempRestoreFolderPrefix = "KOTORModSync_Restore_";

		public string BackupPath { get; private set; }

		public async Task EnsureSnapshotAsync([NotNull] DirectoryInfo source, CancellationToken cancellationToken)
		{
			if ( source == null )
				throw new ArgumentNullException(nameof(source));

			BackupPath = GetBackupPath(source);
			if ( File.Exists(BackupPath) )
				return;

			await CreateSnapshotAsync(source, cancellationToken);
		}

		public async Task PromoteSnapshotAsync([NotNull] DirectoryInfo source, CancellationToken cancellationToken)
		{
			if ( source == null )
				throw new ArgumentNullException(nameof(source));

			BackupPath = GetBackupPath(source);
			await CreateSnapshotAsync(source, cancellationToken);
		}

		public async Task RestoreSnapshotAsync([NotNull] DirectoryInfo destination, CancellationToken cancellationToken)
		{
			if ( destination == null )
				throw new ArgumentNullException(nameof(destination));

			BackupPath = GetBackupPath(destination);
			if ( !File.Exists(BackupPath) )
				throw new FileNotFoundException("Backup snapshot not found", BackupPath);

			string tempExtract = Path.Combine(Path.GetTempPath(), TempRestoreFolderPrefix + Guid.NewGuid());
			_ = Directory.CreateDirectory(tempExtract);

			try
			{
				Directory.Delete(tempExtract, recursive: true);
				await Task.Run(() => ZipFile.ExtractToDirectory(BackupPath, tempExtract, System.Text.Encoding.UTF8), cancellationToken);

				
				foreach ( FileSystemInfo fsi in destination.EnumerateFileSystemInfos() )
				{
					cancellationToken.ThrowIfCancellationRequested();
					if ( string.Equals(fsi.Name, SessionFolderName, StringComparison.OrdinalIgnoreCase) )
						continue;

					SafeDelete(fsi);
				}

				
				CopyDirectory(new DirectoryInfo(tempExtract), destination, cancellationToken, skipFolder: SessionFolderName);
			}
			finally
			{
				if ( Directory.Exists(tempExtract) )
					Directory.Delete(tempExtract, recursive: true);
			}
		}

		private async Task CreateSnapshotAsync(DirectoryInfo source, CancellationToken cancellationToken)
		{
			string tempWorking = Path.Combine(Path.GetTempPath(), TempWorkingFolderPrefix + Guid.NewGuid());
			_ = Directory.CreateDirectory(tempWorking);

			try
			{
				CopyDirectory(source, new DirectoryInfo(tempWorking), cancellationToken, skipFolder: SessionFolderName);

				if ( File.Exists(BackupPath) )
					File.Delete(BackupPath);

				await Task.Run(() => ZipFile.CreateFromDirectory(tempWorking, BackupPath, CompressionLevel.Fastest, includeBaseDirectory: false), cancellationToken);
			}
			finally
			{
				if ( Directory.Exists(tempWorking) )
					Directory.Delete(tempWorking, recursive: true);
			}
		}

		private static void CopyDirectory(DirectoryInfo source, DirectoryInfo destination, CancellationToken cancellationToken, string skipFolder)
		{
			if ( !destination.Exists )
				destination.Create();

			foreach ( FileInfo file in source.EnumerateFiles() )
			{
				cancellationToken.ThrowIfCancellationRequested();
				string targetPath = Path.Combine(destination.FullName, file.Name);
				_ = file.CopyTo(targetPath, overwrite: true);
			}

			foreach ( DirectoryInfo dir in source.EnumerateDirectories() )
			{
				cancellationToken.ThrowIfCancellationRequested();
				if ( string.Equals(dir.Name, skipFolder, StringComparison.OrdinalIgnoreCase) )
					continue;

				DirectoryInfo targetDir = new DirectoryInfo(Path.Combine(destination.FullName, dir.Name));
				CopyDirectory(dir, targetDir, cancellationToken, skipFolder);
			}
		}

		private static void SafeDelete(FileSystemInfo fsi)
		{
			if ( fsi is DirectoryInfo directory )
				directory.Delete(recursive: true);
			else
				fsi.Delete();
		}

		private static string GetBackupPath(DirectoryInfo destination)
		{
			string folder = Path.Combine(destination.FullName, SessionFolderName);
			_ = Directory.CreateDirectory(folder);
			return Path.Combine(folder, BackupFileName);
		}
	}
}

