// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;

using KOTORModSync.Core.Services;

using NUnit.Framework;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public sealed class TelemetryAuthenticatorTests
    {
        [Test]
        public void ComputeSignature_SameInputs_ReturnsDeterministicLowercaseHex()
        {
            var authenticator = new TelemetryAuthenticator("super-secret", "session-123");

            string signature1 = authenticator.ComputeSignature("/v1/metrics", 1712345678);
            string signature2 = authenticator.ComputeSignature("/v1/metrics", 1712345678);

            Assert.Multiple(() =>
            {
                Assert.That(signature1, Is.EqualTo(signature2));
                Assert.That(signature1, Is.Not.Null.And.Not.Empty);
                Assert.That(signature1, Has.Length.EqualTo(64));
                Assert.That(signature1, Does.Match("^[0-9a-f]+$"));
            });
        }

        [Test]
        public void ComputeSignature_DifferentInputs_ChangesSignature()
        {
            var authenticator = new TelemetryAuthenticator("super-secret", "session-123");

            string metricsSignature = authenticator.ComputeSignature("/v1/metrics", 1712345678);
            string tracesSignature = authenticator.ComputeSignature("/v1/traces", 1712345678);

            Assert.That(metricsSignature, Is.Not.EqualTo(tracesSignature));
        }

        [Test]
        public void HasValidSecret_ReflectsPresenceOfSigningSecret()
        {
            var validAuthenticator = new TelemetryAuthenticator("secret", "session-123");
            var invalidAuthenticator = new TelemetryAuthenticator(string.Empty, "session-123");

            Assert.Multiple(() =>
            {
                Assert.That(validAuthenticator.HasValidSecret(), Is.True);
                Assert.That(invalidAuthenticator.HasValidSecret(), Is.False);
                Assert.That(invalidAuthenticator.ComputeSignature("/v1/metrics", 1712345678), Is.Null);
            });
        }

        [Test]
        public void GetUnixTimestamp_ReturnsRecentUnixTime()
        {
            long before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long timestamp = TelemetryAuthenticator.GetUnixTimestamp();
            long after = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            Assert.That(timestamp, Is.InRange(before, after));
        }
    }
}
