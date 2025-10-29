// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using KOTORModSync.Core.Utility;

using NUnit.Framework;

namespace KOTORModSync.Tests
{
	[TestFixture]
	public class UrlNormalizerTests
	{
		[Test]
		public void Normalize_WithUppercaseScheme_ConvertsToLowercase()
		{
			string url = "HTTPS://example.com/path";
			string normalized = UrlNormalizer.Normalize( url );

			Assert.That( normalized, Does.StartWith( "https://" ) );
		}

		[Test]
		public void Normalize_WithUppercaseHost_ConvertsToLowercase()
		{
			string url = "https://EXAMPLE.COM/path";
			string normalized = UrlNormalizer.Normalize( url );

			Assert.That( normalized, Does.Contain( "example.com" ) );
		}

		[Test]
		public void Normalize_WithDefaultPort_RemovesPort()
		{
			string url1 = "https://example.com:443/path";
			string url2 = "http://example.com:80/path";

			string normalized1 = UrlNormalizer.Normalize( url1 );
			string normalized2 = UrlNormalizer.Normalize( url2 );

			Assert.That( normalized1, Does.Not.Contain( ":443" ) );
			Assert.That( normalized2, Does.Not.Contain( ":80" ) );
		}

		[Test]
		public void Normalize_WithNonDefaultPort_KeepsPort()
		{
			string url = "https://example.com:8443/path";
			string normalized = UrlNormalizer.Normalize( url );

			Assert.That( normalized, Does.Contain( ":8443" ) );
		}

		[Test]
		public void Normalize_WithFragment_RemovesFragment()
		{
			string url = "https://example.com/path#section";
			string normalized = UrlNormalizer.Normalize( url );

			Assert.That( normalized, Does.Not.Contain( "#section" ) );
		}

		[Test]
		public void Normalize_WithTrailingSlash_RemovesTrailingSlash()
		{
			string url = "https://example.com/path/";
			string normalized = UrlNormalizer.Normalize( url );

			Assert.That( normalized, Is.EqualTo( "https://example.com/path" ) );
		}

		[Test]
		public void Normalize_WithEmptyPath_DoesNotAddSlash()
		{
			string url = "https://example.com";
			string normalized = UrlNormalizer.Normalize( url );

			Assert.That( normalized, Is.EqualTo( "https://example.com" ) );
		}

		[Test]
		public void Normalize_WithPercentEncoding_PreservesEncoding()
		{
			string url = "https://example.com/path%20with%20spaces";
			string normalized = UrlNormalizer.Normalize( url );

			Assert.That( normalized, Does.Contain( "%20" ) );
		}

		[Test]
		public void Normalize_WithQueryParameters_SortsParameters()
		{
			string url = "https://example.com/path?z=last&a=first&m=middle";
			string normalized = UrlNormalizer.Normalize( url );

			int aIndex = normalized.IndexOf( "a=", StringComparison.Ordinal );
			int mIndex = normalized.IndexOf( "m=", StringComparison.Ordinal );
			int zIndex = normalized.IndexOf( "z=", StringComparison.Ordinal );

			Assert.That( aIndex, Is.LessThan( mIndex ) );
			Assert.That( mIndex, Is.LessThan( zIndex ) );
		}

		[Test]
		public void Normalize_WithEmptyQueryValue_Preserves()
		{
			string url = "https://example.com/path?key=";
			string normalized = UrlNormalizer.Normalize( url );

			Assert.That( normalized, Does.Contain( "key=" ) );
		}

		[Test]
		public void Normalize_WithDotSegments_ResolvesPath()
		{
			string url = "https://example.com/a/./b/../c";
			string normalized = UrlNormalizer.Normalize( url );

			// Should resolve to /a/c
			Assert.That( normalized, Does.Contain( "/a/c" ) );
		}

		[Test]
		public void Normalize_WithWWWSubdomain_PreservesWWW()
		{
			string url = "https://www.example.com/path";
			string normalized = UrlNormalizer.Normalize( url );

			Assert.That( normalized, Does.Contain( "www.example.com" ) );
		}

		[Test]
		public void Normalize_SameUrlDifferentOrder_ProducesSameResult()
		{
			// Real DeadlyStream URLs with different tab orders
			string url1 = "https://deadlystream.com/files/file/1313-kotor-dialogue-fixes/?tab=files&sort=newest";
			string url2 = "https://deadlystream.com/files/file/1313-kotor-dialogue-fixes/?sort=newest&tab=files";

			string normalized1 = UrlNormalizer.Normalize( url1 );
			string normalized2 = UrlNormalizer.Normalize( url2 );

			// Should be the same after normalization (query params sorted)
			Assert.That( normalized1, Is.EqualTo( normalized2 ) );
		}

		[Test]
		public void Normalize_WithUsernamePassword_RemovesCredentials()
		{
			string url = "https://user:pass@example.com/path";
			string normalized = UrlNormalizer.Normalize( url );

			Assert.That( normalized, Does.Not.Contain( "user" ) );
			Assert.That( normalized, Does.Not.Contain( "pass" ) );
			Assert.That( normalized, Does.Not.Contain( "@" ) );
		}
	}
}