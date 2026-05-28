// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;

using KOTORModSync.Services;

using NUnit.Framework;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public sealed class AutoUpdateServiceTests
    {
        [Test]
        public async Task CheckForUpdatesAsync_WhenUpdateAvailable_InvokesInteractiveCheck()
        {
            var fakeClient = new FakeAutoUpdateClient
            {
                QuietResult = new AutoUpdateCheckResult
                {
                    UpdateAvailable = true,
                    StatusMessage = "UpdateAvailable",
                },
            };

            using (var service = new AutoUpdateService(fakeClient, new AutoUpdateSettings()))
            {
                service.Initialize();

                bool result = await service.CheckForUpdatesAsync();

                Assert.Multiple(() =>
                {
                    Assert.That(result, Is.True);
                    Assert.That(fakeClient.CheckQuietlyCalls, Is.EqualTo(1));
                    Assert.That(fakeClient.InteractiveCheckCalls, Is.EqualTo(1));
                });
            }
        }

        [Test]
        public async Task CheckForUpdatesAsync_WhenNoUpdate_DoesNotInvokeInteractiveCheck()
        {
            var fakeClient = new FakeAutoUpdateClient
            {
                QuietResult = new AutoUpdateCheckResult
                {
                    UpdateAvailable = false,
                    StatusMessage = "UpdateNotAvailable",
                },
            };

            using (var service = new AutoUpdateService(fakeClient, new AutoUpdateSettings()))
            {
                service.Initialize();

                bool result = await service.CheckForUpdatesAsync();

                Assert.Multiple(() =>
                {
                    Assert.That(result, Is.False);
                    Assert.That(fakeClient.CheckQuietlyCalls, Is.EqualTo(1));
                    Assert.That(fakeClient.InteractiveCheckCalls, Is.EqualTo(0));
                });
            }
        }

        [Test]
        public async Task StartAndStopUpdateCheckLoop_DelegateToClient()
        {
            var fakeClient = new FakeAutoUpdateClient();

            using (var service = new AutoUpdateService(fakeClient, new AutoUpdateSettings()))
            {
                service.Initialize();
                service.StartUpdateCheckLoop();
                await Task.Delay(25);
                service.StopUpdateCheckLoop();

                Assert.Multiple(() =>
                {
                    Assert.That(fakeClient.StartLoopCalls, Is.EqualTo(1));
                    Assert.That(fakeClient.StopLoopCalls, Is.EqualTo(1));
                    Assert.That(fakeClient.StartLoopInitialCheck, Is.True);
                    Assert.That(fakeClient.StartLoopFrequency, Is.EqualTo(TimeSpan.FromHours(24)));
                });
            }
        }

        [Test]
        public void CurrentConfiguration_ExposesInjectedSettings()
        {
            var settings = new AutoUpdateSettings
            {
                AppCastUrl = "https://example.com/appcast.xml",
                SignaturePublicKey = "public-key",
            };

            var service = new AutoUpdateService(new FakeAutoUpdateClient(), settings);

            Assert.Multiple(() =>
            {
                Assert.That(service.CurrentSettings.AppCastUrl, Is.EqualTo("https://example.com/appcast.xml"));
                Assert.That(service.CurrentSettings.SignaturePublicKey, Is.EqualTo("public-key"));
            });
        }

        private sealed class FakeAutoUpdateClient : IAutoUpdateClient
        {
            public AutoUpdateCheckResult QuietResult { get; set; } = new AutoUpdateCheckResult
            {
                UpdateAvailable = false,
                StatusMessage = "UpdateNotAvailable",
            };
            public int CheckQuietlyCalls { get; private set; }
            public int InteractiveCheckCalls { get; private set; }
            public int StartLoopCalls { get; private set; }
            public int StopLoopCalls { get; private set; }
            public bool StartLoopInitialCheck { get; private set; }
            public TimeSpan StartLoopFrequency { get; private set; }
            public AutoUpdateSettings InitializedSettings { get; private set; }

            public void Initialize(AutoUpdateSettings settings)
            {
                InitializedSettings = settings;
            }

            public Task<AutoUpdateCheckResult> CheckForUpdatesQuietlyAsync(CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CheckQuietlyCalls++;
                return Task.FromResult(QuietResult);
            }

            public Task ShowUpdateUiAsync(CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                InteractiveCheckCalls++;
                return Task.CompletedTask;
            }

            public Task StartLoopAsync(bool doInitialCheck, TimeSpan checkFrequency, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                StartLoopCalls++;
                StartLoopInitialCheck = doInitialCheck;
                StartLoopFrequency = checkFrequency;
                return Task.CompletedTask;
            }

            public void StopLoop()
            {
                StopLoopCalls++;
            }

            public void Dispose()
            {
            }
        }
    }
}
