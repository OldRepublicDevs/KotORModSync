// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.


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
