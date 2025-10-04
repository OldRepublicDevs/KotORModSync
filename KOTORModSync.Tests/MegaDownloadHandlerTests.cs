// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
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
			// Arrange
			const string url = "https://mega.nz/file/1A4RCLha#Ro2GNVUPRfgot-woqh80jVaukixr-cnUmTdakuc0Ca4";

			// Act
			bool? canHandle = _megaHandler?.CanHandle(url);

			// Assert
			Assert.That(canHandle, Is.True);
		}

		[Test]
		public void CanHandle_InvalidMegaUrl_ReturnsFalse()
		{
			// Arrange
			const string url = "https://www.nexusmods.com/kotor2/mods/1100";

			// Act
			bool? canHandle = _megaHandler?.CanHandle(url);

			// Assert
			Assert.That(canHandle, Is.False);
		}

		[Test]
		public void CanHandle_NullUrl_ReturnsFalse()
		{
			// Arrange
			string? url = null;

			// Act
			bool? canHandle = _megaHandler?.CanHandle(url);

			// Assert
			Assert.That(canHandle, Is.False);
		}

		[Test]
		public void ConvertMegaUrl_OldFormatFileShare_ConvertsCorrectly()
		{
			// Arrange - This is the actual failing URL from the logs
			const string oldUrl = "https://mega.nz/#!1A4RCLha!Ro2GNVUPRfgot-woqh80jVaukixr-cnUmTdakuc0Ca4";

			// Act
			string? convertedUrl = _megaHandler?.ConvertMegaUrl(oldUrl);

			// Assert - Should convert to new format
			string expectedUrl = "https://mega.nz/file/1A4RCLha#Ro2GNVUPRfgot-woqh80jVaukixr-cnUmTdakuc0Ca4";
			Assert.That(convertedUrl, Is.EqualTo(expectedUrl));
		}

		[Test]
		public void ConvertMegaUrl_OldFormatFolderShare_ConvertsCorrectly()
		{
			// Arrange
			const string oldFolderUrl = "https://mega.nz/#F!folderId!folderKey";

			// Act
			string? convertedUrl = _megaHandler?.ConvertMegaUrl(oldFolderUrl);

			// Assert - Should convert to new format
			const string expectedUrl = "https://mega.nz/folder/folderId#folderKey";
			Assert.That(convertedUrl, Is.EqualTo(expectedUrl));
		}

		[Test]
		public void ConvertMegaUrl_NewFormat_Unchanged()
		{
			// Arrange - Already in new format
			const string newUrl = "https://mega.nz/file/1A4RCLha#Ro2GNVUPRfgot-woqh80jVaukixr-cnUmTdakuc0Ca4";

			// Act
			string? convertedUrl = _megaHandler?.ConvertMegaUrl(newUrl);

			// Assert - Should remain unchanged
			Assert.That(convertedUrl, Is.EqualTo(newUrl));
		}

		[Test]
		public void ConvertMegaUrl_EmptyOrNull_ReturnsOriginal()
		{
			// Arrange
			const string emptyUrl = "";
			string? nullUrl = null;

			Assert.Multiple(() =>
			{
				// Act & Assert
				Assert.That(_megaHandler?.ConvertMegaUrl(emptyUrl), Is.EqualTo(emptyUrl));
				Assert.That(_megaHandler?.ConvertMegaUrl(nullUrl), Is.EqualTo(nullUrl));
			});
		}

		[Test]
		public void ConvertMegaUrl_MalformedUrl_HandlesGracefully()
		{
			// Arrange - URL without proper format
			const string malformedUrl = "https://mega.nz/#!invalid";

			// Act
			string? convertedUrl = _megaHandler?.ConvertMegaUrl(malformedUrl);

			// Assert - Should return original since it doesn't match expected pattern
			Assert.That(convertedUrl, Is.EqualTo(malformedUrl));
		}

		[Test]
		public void ConvertMegaUrl_ComplexKey_ConvertsCorrectly()
		{
			// Arrange - URL with complex key from logs
			const string complexUrl = "https://mega.nz/#!1A4RCLha!Ro2GNVUPRfgot-woqh80jVaukixr-cnUmTdakuc0Ca4";

			// Act
			string? convertedUrl = _megaHandler?.ConvertMegaUrl(complexUrl);

			// Assert
			const string expectedUrl = "https://mega.nz/file/1A4RCLha#Ro2GNVUPRfgot-woqh80jVaukixr-cnUmTdakuc0Ca4";
			Assert.That(convertedUrl, Is.EqualTo(expectedUrl));

			// Verify the key is preserved correctly
			Assert.That(convertedUrl, Does.Contain("Ro2GNVUPRfgot-woqh80jVaukixr-cnUmTdakuc0Ca4"));
		}

		// Note: Integration tests would require actual MEGA credentials and network access
		// These would be better suited for manual testing or CI/CD environments
	}

	// Helper extension method to access private ConvertMegaUrl method for testing
	public static class MegaDownloadHandlerExtensions
	{
		public static string? ConvertMegaUrl(this MegaDownloadHandler handler, string? url)
		{
			// Access the private method through reflection for testing
			System.Reflection.MethodInfo? method = typeof(MegaDownloadHandler).GetMethod("ConvertMegaUrl",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

			return method is not null ? (string?)method.Invoke(null, [url]) : url;
		}
	}
}
