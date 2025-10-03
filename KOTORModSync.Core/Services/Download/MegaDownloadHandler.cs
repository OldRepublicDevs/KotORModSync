using System;
using System.IO;
using System.Threading.Tasks;
using CG.Web.MegaApiClient;

namespace KOTORModSync.Core.Services.Download
{
	public sealed class MegaDownloadHandler : IDownloadHandler
	{
		private readonly MegaApiClient _client = new MegaApiClient();

		public bool CanHandle(string url) => url != null && url.IndexOf("mega.nz", StringComparison.OrdinalIgnoreCase) >= 0;

		public async Task<DownloadResult> DownloadAsync(string url, string destinationDirectory)
		{
			try
			{
				await _client.LoginAnonymousAsync().ConfigureAwait(false);
				INode node = await _client.GetNodeFromLinkAsync(new Uri(url)).ConfigureAwait(false);
				Directory.CreateDirectory(destinationDirectory);
				string filePath = Path.Combine(destinationDirectory, node.Name);
				await _client.DownloadFileAsync(node, filePath).ConfigureAwait(false);
				await _client.LogoutAsync().ConfigureAwait(false);
				return DownloadResult.Succeeded(filePath, "Downloaded from MEGA");
			}
			catch ( Exception ex )
			{
				return DownloadResult.Failed("MEGA.nz download failed: " + ex.Message);
			}
		}
	}
}
