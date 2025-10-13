using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KOTORModSync.Core.Services.Download
{
	public interface IDownloadHandler
	{
		bool CanHandle(string url);

		/// <summary>
		/// Resolves a URL to the filename(s) that would be downloaded, without actually downloading.
		/// </summary>
		/// <param name="url">The URL to resolve</param>
		/// <param name="cancellationToken">Cancellation token</param>
		/// <returns>List of filenames that would be downloaded from this URL (empty if cannot be determined)</returns>
		Task<List<string>> ResolveFilenamesAsync(string url, CancellationToken cancellationToken = default);

		/// <summary>
		/// Downloads a file or files from the given URL to the specified destination directory.
		/// Reports download progress via the supplied progress reporter, if provided.
		/// Returns a <see cref="DownloadResult"/> indicating the outcome of the download operation.
		/// </summary>
		/// <param name="url">The URL to download from</param>
		/// <param name="destinationDirectory">The directory where downloaded files should be saved</param>
		/// <param name="progress">Optional progress reporter for download progress updates</param>
		/// <param name="cancellationToken">Cancellation token</param>
		/// <returns>A <see cref="DownloadResult"/> object representing the result of the download</returns>
		Task<DownloadResult> DownloadAsync(string url, string destinationDirectory, IProgress<DownloadProgress> progress = null, CancellationToken cancellationToken = default);
	}
}
