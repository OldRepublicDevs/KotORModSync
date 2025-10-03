using System.Threading.Tasks;

namespace KOTORModSync.Core.Services.Download
{
	public interface IDownloadHandler
	{
		bool CanHandle(string url);
		Task<DownloadResult> DownloadAsync(string url, string destinationDirectory);
	}
}
