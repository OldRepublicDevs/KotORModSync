using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace KOTORModSync.Core.Services.Download
{
	public sealed class NexusModsDownloadHandler : IDownloadHandler
	{
		private readonly HttpClient _httpClient;
		private readonly string _apiKey;

		public NexusModsDownloadHandler(HttpClient httpClient, string apiKey)
		{
			_httpClient = httpClient;
			_apiKey = apiKey;
		}

		public bool CanHandle(string url) => url != null && url.IndexOf("nexusmods.com", StringComparison.OrdinalIgnoreCase) >= 0;

		public async Task<DownloadResult> DownloadAsync(string url, string destinationDirectory)
		{
			if ( string.IsNullOrWhiteSpace(_apiKey) )
				return DownloadResult.Failed("Nexus Mods API key is required for automated downloads.");

			try
			{
				NexusDownloadLink linkInfo = await ResolveDownloadLinkAsync(url).ConfigureAwait(false);
				if ( linkInfo == null || string.IsNullOrEmpty(linkInfo.Url) )
					return DownloadResult.Failed("Unable to resolve Nexus Mods download link.");

				string fileName = string.IsNullOrEmpty(linkInfo.FileName) ? "nexus_download" : linkInfo.FileName;

				var request = new HttpRequestMessage(HttpMethod.Get, linkInfo.Url);
				request.Headers.Add("apikey", _apiKey);
				request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

				HttpResponseMessage response = await _httpClient.SendAsync(request).ConfigureAwait(false);
				_ = response.EnsureSuccessStatusCode();

				_ = Directory.CreateDirectory(destinationDirectory);
				string filePath = Path.Combine(destinationDirectory, fileName);
				using ( FileStream fileStream = File.Create(filePath) )
					await response.Content.CopyToAsync(fileStream).ConfigureAwait(false);

				request.Dispose();
				response.Dispose();

				return DownloadResult.Succeeded(filePath, "Downloaded from Nexus Mods");
			}
			catch ( Exception ex )
			{
				return DownloadResult.Failed("Nexus Mods download failed: " + ex.Message);
			}
		}

		private async Task<NexusDownloadLink> ResolveDownloadLinkAsync(string url)
		{
			// Placeholder: interpret Nexus Mods URL to API endpoint. Implementation will require Nexus Mods API usage.
			await Task.Delay(0).ConfigureAwait(false);
			return null;
		}

		private sealed class NexusDownloadLink
		{
			public string Url { get; set; }
			public string FileName { get; set; }
		}
	}
}
