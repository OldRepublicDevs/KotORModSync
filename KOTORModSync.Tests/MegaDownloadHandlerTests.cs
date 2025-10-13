// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using KOTORModSync.Core.Services.Download;

namespace KOTORModSync.Tests
{
	[TestFixture]
	public class MegaDownloadHandlerTests
	{
		private MegaDownloadHandler? _megaHandler;

		[SetUp]
		public void SetUp() => _megaHandler = new MegaDownloadHandler();

		[Test]
		public void CanHandle_ValidMegaUrl_ReturnsTrue()
		{

			const string url = "https://mega.nz/file/1A4RCLha#Ro2GNVUPRfgot-woqh80jVaukixr-cnUmTdakuc0Ca4";

			bool? canHandle = _megaHandler?.CanHandle(url);

			Assert.That(canHandle, Is.True);
		}

		[Test]
		public void CanHandle_InvalidMegaUrl_ReturnsFalse()
		{

			const string url = "https://www.nexusmods.com/kotor2/mods/1100";

			bool? canHandle = _megaHandler?.CanHandle(url);

			Assert.That(canHandle, Is.False);
		}

		[Test]
		public void CanHandle_NullUrl_ReturnsFalse()
		{

			string? url = null;

			bool? canHandle = _megaHandler?.CanHandle(url);

			Assert.That(canHandle, Is.False);
		}

		[Test]
		public void ConvertMegaUrl_OldFormatFileShare_ConvertsCorrectly()
		{

			const string oldUrl = "https://mega.nz/#!1A4RCLha!Ro2GNVUPRfgot-woqh80jVaukixr-cnUmTdakuc0Ca4";

			string? convertedUrl = _megaHandler?.ConvertMegaUrl(oldUrl);

			string expectedUrl = "https://mega.nz/file/1A4RCLha#Ro2GNVUPRfgot-woqh80jVaukixr-cnUmTdakuc0Ca4";
			Assert.That(convertedUrl, Is.EqualTo(expectedUrl));
		}

		[Test]
		public void ConvertMegaUrl_OldFormatFolderShare_ConvertsCorrectly()
		{

			const string oldFolderUrl = "https://mega.nz/#F!folderId!folderKey";

			string? convertedUrl = _megaHandler?.ConvertMegaUrl(oldFolderUrl);

			const string expectedUrl = "https://mega.nz/folder/folderId#folderKey";
			Assert.That(convertedUrl, Is.EqualTo(expectedUrl));
		}

		[Test]
		public void ConvertMegaUrl_NewFormat_Unchanged()
		{

			const string newUrl = "https://mega.nz/file/1A4RCLha#Ro2GNVUPRfgot-woqh80jVaukixr-cnUmTdakuc0Ca4";

			string? convertedUrl = _megaHandler?.ConvertMegaUrl(newUrl);

			Assert.That(convertedUrl, Is.EqualTo(newUrl));
		}

		[Test]
		public void ConvertMegaUrl_EmptyOrNull_ReturnsOriginal()
		{

			const string emptyUrl = "";
			string? nullUrl = null;

			Assert.Multiple(() =>
			{

				Assert.That(_megaHandler?.ConvertMegaUrl(emptyUrl), Is.EqualTo(emptyUrl));
				Assert.That(_megaHandler?.ConvertMegaUrl(nullUrl), Is.EqualTo(nullUrl));
			});
		}

		[Test]
		public void ConvertMegaUrl_MalformedUrl_HandlesGracefully()
		{

			const string malformedUrl = "https://mega.nz/#!invalid";

			string? convertedUrl = _megaHandler?.ConvertMegaUrl(malformedUrl);

			Assert.That(convertedUrl, Is.EqualTo(malformedUrl));
		}

		[Test]
		public void ConvertMegaUrl_ComplexKey_ConvertsCorrectly()
		{

			const string complexUrl = "https://mega.nz/#!1A4RCLha!Ro2GNVUPRfgot-woqh80jVaukixr-cnUmTdakuc0Ca4";

			string? convertedUrl = _megaHandler?.ConvertMegaUrl(complexUrl);

			const string expectedUrl = "https://mega.nz/file/1A4RCLha#Ro2GNVUPRfgot-woqh80jVaukixr-cnUmTdakuc0Ca4";
			Assert.That(convertedUrl, Is.EqualTo(expectedUrl));

			Assert.That(convertedUrl, Does.Contain("Ro2GNVUPRfgot-woqh80jVaukixr-cnUmTdakuc0Ca4"));
		}

	}

	public static class MegaDownloadHandlerExtensions
	{
		public static string? ConvertMegaUrl(this MegaDownloadHandler handler, string? url)
		{

			System.Reflection.MethodInfo? method = typeof(MegaDownloadHandler).GetMethod("ConvertMegaUrl",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

			return method is not null ? (string?)method.Invoke(null, [url]) : url;
		}
	}
}
