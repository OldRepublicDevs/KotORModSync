using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace KOTORModSync.Core.Services.Download
{
	public sealed class DeadlyStreamDownloadHandler : IDownloadHandler
	{
		private readonly HttpClient _httpClient;

		public DeadlyStreamDownloadHandler(HttpClient httpClient)
		{
			_httpClient = httpClient;
		}

		public bool CanHandle(string url) => url != null && url.IndexOf("deadlystream.com", StringComparison.OrdinalIgnoreCase) >= 0;

		public async Task<DownloadResult> DownloadAsync(string url, string destinationDirectory)
		{
			try
			{
				HttpResponseMessage pageResponse = await _httpClient.GetAsync(url).ConfigureAwait(false);
				pageResponse.EnsureSuccessStatusCode();
				string html = await pageResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

				string downloadLink = ExtractDownloadLink(html);
				if ( string.IsNullOrWhiteSpace(downloadLink) )
				{
					pageResponse.Dispose();
					return DownloadResult.Failed("Unable to locate DeadlyStream download button on page.");
				}

				HttpResponseMessage fileResponse = await _httpClient.GetAsync(downloadLink).ConfigureAwait(false);
				fileResponse.EnsureSuccessStatusCode();

				string fileName = GetFileNameFromContentDisposition(fileResponse);
				if ( string.IsNullOrWhiteSpace(fileName) )
					fileName = "deadlystream_download";

				Directory.CreateDirectory(destinationDirectory);
				string filePath = Path.Combine(destinationDirectory, fileName);
				using ( FileStream fileStream = File.Create(filePath) )
				{
					await fileResponse.Content.CopyToAsync(fileStream).ConfigureAwait(false);
				}

				pageResponse.Dispose();
				fileResponse.Dispose();

				return DownloadResult.Succeeded(filePath, "Downloaded from DeadlyStream");
			}
			catch ( Exception ex )
			{
				return DownloadResult.Failed("DeadlyStream download failed: " + ex.Message);
			}
		}

		private static string ExtractDownloadLink(string html)
		{
			if ( string.IsNullOrEmpty(html) )
				return null;

			var document = new HtmlDocument();
			document.LoadHtml(html);
			HtmlNodeCollection nodes = document.DocumentNode.SelectNodes("//a[contains(@class,'ipsButton') and contains(@class,'ipsButton_fullWidth') and contains(@class,'ipsButton_large') and contains(@class,'ipsButton_important')]");
			if ( nodes == null || nodes.Count == 0 )
				return null;

			foreach ( HtmlNode node in nodes )
			{
				string href = node.GetAttributeValue("href", string.Empty);
				if ( !string.IsNullOrWhiteSpace(href) )
					return href;
			}

			return null;
		}

		private static string GetFileNameFromContentDisposition(HttpResponseMessage response)
		{
			if ( response == null || response.Content == null || response.Content.Headers.ContentDisposition == null )
				return null;

			string fileName = response.Content.Headers.ContentDisposition.FileNameStar;
			if ( string.IsNullOrWhiteSpace(fileName) )
				fileName = response.Content.Headers.ContentDisposition.FileName;

			if ( string.IsNullOrWhiteSpace(fileName) )
				return null;

			string trimmed = Regex.Replace(fileName, pattern: "^\"|\"$", string.Empty);
			return Uri.UnescapeDataString(trimmed);
		}
	}
}
