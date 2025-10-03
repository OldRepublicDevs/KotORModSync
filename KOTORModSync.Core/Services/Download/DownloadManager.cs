using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KOTORModSync.Core.Services.Download
{
	public sealed class DownloadManager
	{
		private readonly List<IDownloadHandler> _handlers;

		public DownloadManager(IEnumerable<IDownloadHandler> handlers)
		{
			_handlers = new List<IDownloadHandler>(handlers);
		}

		public async Task<List<DownloadResult>> DownloadAllAsync(IEnumerable<string> urls, string destinationDirectory)
		{
			var results = new List<DownloadResult>();

			foreach ( string url in urls )
			{
				IDownloadHandler handler = _handlers.FirstOrDefault(h => h.CanHandle(url));
				if ( handler == null )
				{
					results.Add(DownloadResult.Failed("No handler configured for URL: " + url));
					continue;
				}

				DownloadResult result = await handler.DownloadAsync(url, destinationDirectory).ConfigureAwait(false);
				results.Add(result);
			}

			return results;
		}
	}
}
