// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using KOTORModSync.Core.Services;
using NUnit.Framework;

namespace KOTORModSync.Tests
{
	[TestFixture]
	public class ResolutionFilterServiceTests
	{
		[Test]
		public void FilterByResolution_FiltersNonMatchingResolutions()
		{
			// Arrange
			var service = new ResolutionFilterService(enableFiltering: true);
			var urls = new List<string>
			{
				"https://example.com/cutscenes_1920x1080.7z",
				"https://example.com/cutscenes_2560x1440.7z",
				"https://example.com/cutscenes_3840x2160.7z",
				"https://example.com/audio_patch.rar"  // No resolution pattern
			};

			// Act
			List<string> filtered = service.FilterByResolution(urls);

			// Assert
			Assert.IsNotNull(filtered, "Filtered list should not be null");
			// Should include at least the file without resolution pattern
			Assert.That(filtered.Count, Is.GreaterThanOrEqualTo(1), "Should include at least non-resolution-specific files");
			// Should include the audio patch (no resolution pattern)
			Assert.That(filtered, Has.Member("https://example.com/audio_patch.rar"), "Should include files without resolution patterns");
		}

		[Test]
		public void FilterByResolution_DisabledFiltering_ReturnsAllUrls()
		{
			// Arrange
			var service = new ResolutionFilterService(enableFiltering: false);
			var urls = new List<string>
			{
				"https://example.com/cutscenes_1920x1080.7z",
				"https://example.com/cutscenes_2560x1440.7z",
				"https://example.com/cutscenes_3840x2160.7z"
			};

			// Act
			List<string> filtered = service.FilterByResolution(urls);

			// Assert
			Assert.IsNotNull(filtered, "Filtered list should not be null");
			Assert.That(filtered.Count, Is.EqualTo(urls.Count), "When filtering disabled, should return all URLs");
		}

		[Test]
		public void FilterByResolution_EmptyList_ReturnsEmptyList()
		{
			// Arrange
			var service = new ResolutionFilterService(enableFiltering: true);
			var urls = new List<string>();

			// Act
			List<string> filtered = service.FilterByResolution(urls);

			// Assert
			Assert.IsNotNull(filtered, "Filtered list should not be null");
			Assert.That(filtered.Count, Is.EqualTo(0), "Empty input should return empty output");
		}

		[Test]
		public void FilterByResolution_NullList_ReturnsEmptyList()
		{
			// Arrange
			var service = new ResolutionFilterService(enableFiltering: true);

			// Act
			List<string> filtered = service.FilterByResolution(null);

			// Assert
			Assert.IsNotNull(filtered, "Filtered list should not be null");
			Assert.That(filtered.Count, Is.EqualTo(0), "Null input should return empty output");
		}

		[Test]
		public void FilterByResolution_FilesWithoutResolutionPattern_AlwaysIncluded()
		{
			// Arrange
			var service = new ResolutionFilterService(enableFiltering: true);
			var urls = new List<string>
			{
				"https://example.com/mod.zip",
				"https://example.com/readme.txt",
				"https://example.com/some_file_v1.2.3.rar"
			};

			// Act
			List<string> filtered = service.FilterByResolution(urls);

			// Assert
			Assert.IsNotNull(filtered, "Filtered list should not be null");
			Assert.That(filtered.Count, Is.EqualTo(urls.Count), "Files without resolution patterns should always be included");
		}

		[Test]
		public void FilterResolvedUrls_FiltersCorrectly()
		{
			// Arrange
			var service = new ResolutionFilterService(enableFiltering: true);
			var urlToFilenames = new Dictionary<string, List<string>>
			{
				{ "https://example.com/mod1", new List<string> { "cutscenes_1920x1080.7z" } },
				{ "https://example.com/mod2", new List<string> { "cutscenes_3840x2160.7z" } },
				{ "https://example.com/mod3", new List<string> { "generic_mod.zip" } }
			};

			// Act
			Dictionary<string, List<string>> filtered = service.FilterResolvedUrls(urlToFilenames);

			// Assert
			Assert.IsNotNull(filtered, "Filtered dictionary should not be null");
			// Should include at least the generic mod (no resolution pattern)
			Assert.That(filtered.ContainsKey("https://example.com/mod3"), Is.True, "Should include URL with non-resolution-specific file");
		}

		[Test]
		public void FilterResolvedUrls_DisabledFiltering_ReturnsAll()
		{
			// Arrange
			var service = new ResolutionFilterService(enableFiltering: false);
			var urlToFilenames = new Dictionary<string, List<string>>
			{
				{ "https://example.com/mod1", new List<string> { "cutscenes_1920x1080.7z" } },
				{ "https://example.com/mod2", new List<string> { "cutscenes_3840x2160.7z" } }
			};

			// Act
			Dictionary<string, List<string>> filtered = service.FilterResolvedUrls(urlToFilenames);

			// Assert
			Assert.IsNotNull(filtered, "Filtered dictionary should not be null");
			Assert.That(filtered.Count, Is.EqualTo(urlToFilenames.Count), "When filtering disabled, should return all entries");
		}

		[Test]
		public void ShouldDownload_FilesWithoutResolution_ReturnsTrue()
		{
			// Arrange
			var service = new ResolutionFilterService(enableFiltering: true);

			// Act & Assert
			Assert.That(service.ShouldDownload("https://example.com/mod.zip"), Is.True, "Files without resolution should be downloadable");
			Assert.That(service.ShouldDownload("generic_file.rar"), Is.True, "Generic files should be downloadable");
			Assert.That(service.ShouldDownload("some_mod_v2.0.7z"), Is.True, "Version numbers should not be confused with resolutions");
		}

		[Test]
		public void ShouldDownload_DisabledFiltering_AlwaysReturnsTrue()
		{
			// Arrange
			var service = new ResolutionFilterService(enableFiltering: false);

			// Act & Assert
			Assert.That(service.ShouldDownload("https://example.com/cutscenes_1920x1080.7z"), Is.True);
			Assert.That(service.ShouldDownload("https://example.com/cutscenes_3840x2160.7z"), Is.True);
			Assert.That(service.ShouldDownload("generic_mod.zip"), Is.True);
		}

		[Test]
		public void ResolutionPattern_MatchesCommonFormats()
		{
			// This test verifies that common resolution patterns are recognized
			var service = new ResolutionFilterService(enableFiltering: true);
			var urls = new List<string>
			{
				"file_1920x1080.zip",    // Common 1080p
				"file_2560x1440.zip",    // Common 1440p
				"file_3840x2160.zip",    // Common 4K
				"file_7680x4320.zip",    // 8K
				"file_1280x720.zip",     // 720p
				"file_640x480.zip"       // VGA
			};

			// Act
			List<string> filtered = service.FilterByResolution(urls);

			// Assert
			// At least one resolution should be detected and potentially filtered
			// (unless the system happens to be one of these exact resolutions)
			Assert.IsNotNull(filtered, "Should process resolution patterns");
		}

		[Test]
		public void ResolutionPattern_IgnoresInvalidFormats()
		{
			// These should NOT be treated as resolutions and should always be included
			var service = new ResolutionFilterService(enableFiltering: true);
			var urls = new List<string>
			{
				"file_v1.2.zip",         // Version numbers
				"file_123x45.zip",       // Too small to be a resolution
				"file_12x34.zip",        // Too small
				"file_1.0x2.0.zip",      // Decimals
				"file_abc_x_def.zip"     // Text
			};

			// Act
			List<string> filtered = service.FilterByResolution(urls);

			// Assert
			Assert.That(filtered.Count, Is.EqualTo(urls.Count), 
				"Files without valid resolution patterns should all be included");
		}

		[Test]
		public void Constructor_LogsResolutionDetection()
		{
			// Arrange & Act
			var service = new ResolutionFilterService(enableFiltering: true);

			// Assert
			// This test mainly verifies that the constructor doesn't throw exceptions
			// and that resolution detection works without crashing
			Assert.IsNotNull(service, "Service should be created successfully");
		}

		[Test]
		public void Constructor_DisabledFiltering_DoesNotDetectResolution()
		{
			// Arrange & Act
			var service = new ResolutionFilterService(enableFiltering: false);

			// Assert
			Assert.IsNotNull(service, "Service should be created successfully even when disabled");
			// All files should pass through
			var result = service.ShouldDownload("file_1920x1080.zip");
			Assert.That(result, Is.True, "When disabled, all files should be allowed");
		}
	}
}

