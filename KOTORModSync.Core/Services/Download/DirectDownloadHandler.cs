using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace KOTORModSync.Core.Services.Download
{
	public sealed class DirectDownloadHandler : IDownloadHandler
	{
		private readonly HttpClient _httpClient;

		public DirectDownloadHandler(HttpClient httpClient) => _httpClient = httpClient;

		public bool CanHandle(string url) => Uri.TryCreate(url, UriKind.Absolute, out Uri uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

		public async Task<DownloadResult> DownloadAsync(string url, string destinationDirectory)
		{
			try
			{
				HttpResponseMessage response = await _httpClient.GetAsync(url).ConfigureAwait(continueOnCapturedContext: false);
				_ = response.EnsureSuccessStatusCode();

				string fileName = Path.GetFileName(response.RequestMessage != null && response.RequestMessage.RequestUri != null
					? response.RequestMessage.RequestUri.AbsolutePath
					: "download");
				if ( string.IsNullOrWhiteSpace(fileName) )
					fileName = "download";

				_ = Directory.CreateDirectory(destinationDirectory);
				string filePath = Path.Combine(destinationDirectory, fileName);
				using ( FileStream stream = File.Create(filePath) )
					await response.Content.CopyToAsync(stream).ConfigureAwait(continueOnCapturedContext: false);

				response.Dispose();

				return DownloadResult.Succeeded(filePath, message: "Downloaded via direct link");
			}
			catch ( Exception ex )
			{
				return DownloadResult.Failed("Direct download failed: " + ex.Message);
			}
		}
	}
}
