// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;

using KOTORModSync.Core.Services;

using NUnit.Framework;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public sealed class TelemetryConfigurationTests
    {
        private string _appDataRoot = string.Empty;
        private string _configDirectory = string.Empty;
        private string _configFilePath = string.Empty;
        private string _keyFilePath = string.Empty;
        private string _originalSigningSecret = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _appDataRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "KOTORModSync");
            _configDirectory = _appDataRoot;
            _configFilePath = Path.Combine(_configDirectory, "telemetry_config.json");
            _keyFilePath = Path.Combine(_configDirectory, "telemetry.key");
            _originalSigningSecret = Environment.GetEnvironmentVariable("KOTORMODSYNC_SIGNING_SECRET");
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable("KOTORMODSYNC_SIGNING_SECRET", _originalSigningSecret);

            try
            {
                if (File.Exists(_configFilePath))
                {
                    File.Delete(_configFilePath);
                }

                if (File.Exists(_keyFilePath))
                {
                    File.Delete(_keyFilePath);
                }
            }
            catch
            {
                // Best effort cleanup for local test config.
            }
        }

        [Test]
        public void SaveThenLoad_PreservesKeyConfigurationFields()
        {
            var config = new TelemetryConfiguration
            {
                IsEnabled = true,
                UserConsented = true,
                CollectUsageData = false,
                EnableOtlpExporter = false,
                EnablePrometheusExporter = true,
                PrometheusPort = 9555,
            };

            config.Save();
            TelemetryConfiguration loaded = TelemetryConfiguration.Load();

            Assert.Multiple(() =>
            {
                Assert.That(loaded.IsEnabled, Is.True);
                Assert.That(loaded.UserConsented, Is.True);
                Assert.That(loaded.CollectUsageData, Is.False);
                Assert.That(loaded.EnableOtlpExporter, Is.False);
                Assert.That(loaded.EnablePrometheusExporter, Is.True);
                Assert.That(loaded.PrometheusPort, Is.EqualTo(9555));
                Assert.That(loaded.SessionId, Is.Not.Null.And.Not.Empty);
                Assert.That(loaded.AnonymousUserId, Is.Not.Null.And.Not.Empty);
            });
        }

        [Test]
        public void Load_UsesEnvironmentSigningSecretWhenPresent()
        {
            Environment.SetEnvironmentVariable("KOTORMODSYNC_SIGNING_SECRET", "env-secret-value");

            TelemetryConfiguration loaded = TelemetryConfiguration.Load();

            Assert.That(loaded.SigningSecret, Is.EqualTo("env-secret-value"));
        }

        [Test]
        public void SetUserConsent_DisablesTelemetryAndUpdatesSummary()
        {
            var config = new TelemetryConfiguration
            {
                IsEnabled = true,
                UserConsented = true,
            };

            config.SetUserConsent(false);

            Assert.Multiple(() =>
            {
                Assert.That(config.IsEnabled, Is.False);
                Assert.That(config.UserConsented, Is.False);
                Assert.That(config.GetPrivacySummary(), Does.Contain("Telemetry is disabled"));
            });
        }
    }
}
