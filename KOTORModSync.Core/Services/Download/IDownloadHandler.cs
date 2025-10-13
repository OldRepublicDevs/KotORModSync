


using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KOTORModSync.Core.Services.Download
{
	public interface IDownloadHandler
	{
		bool CanHandle(string url);

		
		
		
		
		
		
		Task<List<string>> ResolveFilenamesAsync(string url, CancellationToken cancellationToken = default);

		
		
		
		
		
		
		
		
		
		
		Task<DownloadResult> DownloadAsync(string url, string destinationDirectory, IProgress<DownloadProgress> progress = null, CancellationToken cancellationToken = default);
	}
}
