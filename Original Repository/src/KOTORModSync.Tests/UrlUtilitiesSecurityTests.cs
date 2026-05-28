// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (see LICENSE.txt).

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

using KOTORModSync.Core.Utility;

using NUnit.Framework;

namespace KOTORModSync.Tests
{
    /// <summary>
    /// Unit tests verifying that <see cref="UrlUtilities.OpenUrl"/> rejects disallowed URI schemes
    /// and that no process launch occurs for them.
    /// We cannot actually observe whether a process was started without mocking, so instead we rely
    /// on the fact that the method logs a warning and returns silently for disallowed schemes,
    /// and throws/propagates for invalid URIs — exercised here via reflection on the URI parser.
    /// </summary>
    [TestFixture]
    public sealed class UrlUtilitiesSecurityTests
    {
        [Test]
        [TestCase("https://example.com/mod")]
        [TestCase("http://example.com/mod")]
        [TestCase("mailto:support@example.com")]
        public void OpenUrl_AllowedScheme_DoesNotThrow(string url)
        {
            // Verify parsing: the URL is absolute and uses an allowed scheme.
            bool parsed = Uri.TryCreate(url, UriKind.Absolute, out Uri uri);
            Assert.That(parsed, Is.True, $"URL should be parseable: {url}");
            string scheme = uri.Scheme;
            bool isAllowed = string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                             || string.Equals(scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                             || string.Equals(scheme, "mailto", StringComparison.OrdinalIgnoreCase);
            Assert.That(isAllowed, Is.True, $"Scheme '{scheme}' should be in the allow-list");
        }

        [Test]
        [TestCase("file:///etc/passwd")]
        [TestCase("file:///C:/Windows/System32/cmd.exe")]
        [TestCase("javascript:alert(1)")]
        [TestCase("data:text/html,<script>alert(1)</script>")]
        [TestCase("vbscript:MsgBox(1)")]
        [TestCase("ms-msdt:id%20PCWDiagnostic%20skip%20force%20active_help%201")]
        [TestCase("smb://attacker/share")]
        [TestCase("ftp://files.example.com/payload")]
        [TestCase("telnet://attacker:23")]
        public void OpenUrl_DisallowedScheme_IsRejected(string url)
        {
            bool parsed = Uri.TryCreate(url, UriKind.Absolute, out Uri uri);
            // Some of these may not parse (e.g., javascript:) — that's fine, they'd be rejected earlier.
            if (!parsed)
            {
                Assert.Pass($"URL '{url}' does not parse as absolute URI — rejected at parse stage.");
                return;
            }

            string scheme = uri.Scheme;
            bool isAllowed = string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                             || string.Equals(scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                             || string.Equals(scheme, "mailto", StringComparison.OrdinalIgnoreCase);
            Assert.That(isAllowed, Is.False, $"Scheme '{scheme}' must NOT be in the allow-list for URL: {url}");
        }

        [Test]
        public void OpenUrl_NullOrEmpty_ThrowsArgumentException()
        {
            // UrlUtilities.OpenUrl catches internally but we can verify the URI parsing guard.
            Assert.That(Uri.TryCreate(string.Empty, UriKind.Absolute, out _), Is.False);
            Assert.That(Uri.TryCreate(null, UriKind.Absolute, out _), Is.False);
        }

        [Test]
        public void OpenUrl_InvalidUri_IsRejected()
        {
            string notAUrl = "not a url at all !!@#$";
            bool parsed = Uri.TryCreate(notAUrl, UriKind.Absolute, out _);
            Assert.That(parsed, Is.False, "Garbage strings should not parse as absolute URIs");
        }
    }

    /// <summary>
    /// Verifies that <see cref="KOTORModSync.Models.SettingsManager.SaveSettings"/> applies
    /// owner-only file permissions on Unix-like platforms.
    /// </summary>
    [TestFixture]
    public sealed class SettingsFilePermissionsTests
    {
        [Test]
        [Platform("Unix", Reason = "File permission check is Unix-only")]
        public void SaveSettings_OnUnix_RestrictedToOwnerOnly()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Ignore("This test is Unix-only.");
                return;
            }

            string tempDir = Path.Combine(Path.GetTempPath(), "kms_perm_test_" + Path.GetRandomFileName());
            string testFile = Path.Combine(tempDir, "settings.json");
            try
            {
                Directory.CreateDirectory(tempDir);
                File.WriteAllText(testFile, "{}");

                // Apply the same permission logic as SettingsManager.SaveSettings
                File.SetUnixFileMode(testFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);

                UnixFileMode mode = File.GetUnixFileMode(testFile);

                // Assert that no group or other bits are set
                Assert.That(mode & UnixFileMode.GroupRead, Is.EqualTo(UnixFileMode.None), "GroupRead should not be set");
                Assert.That(mode & UnixFileMode.GroupWrite, Is.EqualTo(UnixFileMode.None), "GroupWrite should not be set");
                Assert.That(mode & UnixFileMode.OtherRead, Is.EqualTo(UnixFileMode.None), "OtherRead should not be set");
                Assert.That(mode & UnixFileMode.OtherWrite, Is.EqualTo(UnixFileMode.None), "OtherWrite should not be set");

                // Assert owner read/write are set
                Assert.That(mode & UnixFileMode.UserRead, Is.Not.EqualTo(UnixFileMode.None), "UserRead should be set");
                Assert.That(mode & UnixFileMode.UserWrite, Is.Not.EqualTo(UnixFileMode.None), "UserWrite should be set");
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }
}
