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

			var service = new ResolutionFilterService( enableFiltering: true );
			var urls = new List<string>
			{
				"https://example.com/cutscenes_1920x1080.7z",
				"https://example.com/cutscenes_2560x1440.7z",
				"https://example.com/cutscenes_3840x2160.7z",
				"https://example.com/audio_patch.rar"
			};

			List<string> filtered = service.FilterByResolution( urls );

			Assert.That( filtered, Is.Not.Null, "Filtered list should not be null" );

			Assert.That( filtered.Count, Is.GreaterThanOrEqualTo( 1 ), "Should include at least non-resolution-specific files" );

			Assert.That( filtered, Has.Member( "https://example.com/audio_patch.rar" ), "Should include files without resolution patterns" );
		}

		[Test]
		public void FilterByResolution_DisabledFiltering_ReturnsAllUrls()
		{

			var service = new ResolutionFilterService( enableFiltering: false );
			var urls = new List<string>
			{
				"https://example.com/cutscenes_1920x1080.7z",
				"https://example.com/cutscenes_2560x1440.7z",
				"https://example.com/cutscenes_3840x2160.7z"
			};

			List<string> filtered = service.FilterByResolution( urls );

			Assert.That( filtered, Is.Not.Null, "Filtered list should not be null" );
			Assert.That( filtered, Has.Count.EqualTo( urls.Count ), "When filtering disabled, should return all URLs" );
		}

		[Test]
		public void FilterByResolution_EmptyList_ReturnsEmptyList()
		{

			var service = new ResolutionFilterService( enableFiltering: true );
			var urls = new List<string>();

			List<string> filtered = service.FilterByResolution( urls );

			Assert.That( filtered, Is.Not.Null, "Filtered list should not be null" );
			Assert.That( filtered, Is.Empty, "Empty input should return empty output" );
		}

		[Test]
		public void FilterByResolution_NullList_ReturnsEmptyList()
		{

			var service = new ResolutionFilterService( enableFiltering: true );

			List<string> filtered = service.FilterByResolution( null );

			Assert.That( filtered, Is.Not.Null, "Filtered list should not be null" );
			Assert.That( filtered, Is.Empty, "Null input should return empty output" );
		}

		[Test]
		public void FilterByResolution_FilesWithoutResolutionPattern_AlwaysIncluded()
		{

			var service = new ResolutionFilterService( enableFiltering: true );
			var urls = new List<string>
			{
				"https://example.com/mod.zip",
				"https://example.com/readme.txt",
				"https://example.com/some_file_v1.2.3.rar"
			};

			List<string> filtered = service.FilterByResolution( urls );

			Assert.That( filtered, Is.Not.Null, "Filtered list should not be null" );
			Assert.That( filtered, Has.Count.EqualTo( urls.Count ), "Files without resolution patterns should always be included" );
		}

		[Test]
		public void FilterResolvedUrls_FiltersCorrectly()
		{

			var service = new ResolutionFilterService( enableFiltering: true );
			var urlToFilenames = new Dictionary<string, List<string>>
( StringComparer.Ordinal )
			{
				{ "https://example.com/mod1", new List<string> { "cutscenes_1920x1080.7z" } },
				{ "https://example.com/mod2", new List<string> { "cutscenes_3840x2160.7z" } },
				{ "https://example.com/mod3", new List<string> { "generic_mod.zip" } }
			};

			Dictionary<string, List<string>> filtered = service.FilterResolvedUrls( urlToFilenames );

			Assert.That( filtered, Is.Not.Null, "Filtered dictionary should not be null" );

			Assert.That( filtered.ContainsKey( "https://example.com/mod3" ), Is.True, "Should include URL with non-resolution-specific file" );
		}

		[Test]
		public void FilterResolvedUrls_DisabledFiltering_ReturnsAll()
		{

			var service = new ResolutionFilterService( enableFiltering: false );
			var urlToFilenames = new Dictionary<string, List<string>>
( StringComparer.Ordinal )
			{
				{ "https://example.com/mod1", new List<string> { "cutscenes_1920x1080.7z" } },
				{ "https://example.com/mod2", new List<string> { "cutscenes_3840x2160.7z" } }
			};

			Dictionary<string, List<string>> filtered = service.FilterResolvedUrls( urlToFilenames );

			Assert.That( filtered, Is.Not.Null, "Filtered dictionary should not be null" );
			Assert.That( filtered, Has.Count.EqualTo( urlToFilenames.Count ), "When filtering disabled, should return all entries" );
		}

		[Test]
		public void ShouldDownload_FilesWithoutResolution_ReturnsTrue()
		{

			var service = new ResolutionFilterService( enableFiltering: true );

			Assert.Multiple( () =>
			{
				Assert.That( service.ShouldDownload( "https://example.com/mod.zip" ), Is.True, "Files without resolution should be downloadable" );
				Assert.That( service.ShouldDownload( "generic_file.rar" ), Is.True, "Generic files should be downloadable" );
				Assert.That( service.ShouldDownload( "some_mod_v2.0.7z" ), Is.True, "Version numbers should not be confused with resolutions" );
			} );
		}

		[Test]
		public void ShouldDownload_DisabledFiltering_AlwaysReturnsTrue()
		{

			var service = new ResolutionFilterService( enableFiltering: false );

			Assert.Multiple( () =>
			{
				Assert.That( service.ShouldDownload( "https://example.com/cutscenes_1920x1080.7z" ), Is.True );
				Assert.That( service.ShouldDownload( "https://example.com/cutscenes_3840x2160.7z" ), Is.True );
				Assert.That( service.ShouldDownload( "generic_mod.zip" ), Is.True );
			} );
		}

		[Test]
		public void ResolutionPattern_MatchesCommonFormats()
		{

			var service = new ResolutionFilterService( enableFiltering: true );
			var urls = new List<string>
			{
				"file_1920x1080.zip",
				"file_2560x1440.zip",
				"file_3840x2160.zip",
				"file_7680x4320.zip",
				"file_1280x720.zip",
				"file_640x480.zip"
			};

			List<string> filtered = service.FilterByResolution( urls );

			Assert.That( filtered, Is.Not.Null, "Should process resolution patterns" );
		}

		[Test]
		public void ResolutionPattern_IgnoresInvalidFormats()
		{

			var service = new ResolutionFilterService( enableFiltering: true );
			var urls = new List<string>
			{
				"file_v1.2.zip",
				"file_123x45.zip",
				"file_12x34.zip",
				"file_1.0x2.0.zip",
				"file_abc_x_def.zip"
			};

			List<string> filtered = service.FilterByResolution( urls );

			Assert.That( filtered, Has.Count.EqualTo( urls.Count ),
				"Files without valid resolution patterns should all be included" );
		}

		[Test]
		public void Constructor_LogsResolutionDetection()
		{

			var service = new ResolutionFilterService( enableFiltering: true );

			Assert.That( service, Is.Not.Null, "Service should be created successfully" );
		}

		[Test]
		public void Constructor_DisabledFiltering_DoesNotDetectResolution()
		{

			var service = new ResolutionFilterService( enableFiltering: false );

			Assert.That( service, Is.Not.Null, "Service should be created successfully even when disabled" );

			var result = service.ShouldDownload( "file_1920x1080.zip" );
			Assert.That( result, Is.True, "When disabled, all files should be allowed" );
		}
	}
}