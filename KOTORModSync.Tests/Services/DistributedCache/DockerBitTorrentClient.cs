// Copyright (C) 2025
// Licensed under the GPL version 3 license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KOTORModSync.Tests.Services.DistributedCache
{
    /// <summary>
    /// Manages a containerized BitTorrent client (qBittorrent, Transmission, or Deluge)
    /// for integration testing of the distributed cache system.
    /// </summary>
    public class DockerBitTorrentClient : IDisposable
    {
        private readonly string _containerEngine; // "docker" or "podman"
        private readonly BitTorrentClientType _clientType;
        private readonly int _webPort;
        private readonly int _btPort;
        private string _containerId;
        private readonly HttpClient _httpClient;
        private string _authCookie;

        public enum BitTorrentClientType
        {
            QBittorrent,
            Transmission,
            Deluge
        }

        public DockerBitTorrentClient(BitTorrentClientType clientType, int webPort = 0, int btPort = 0)
        {
            _clientType = clientType;
            _webPort = webPort == 0 ? GetRandomPort() : webPort;
            _btPort = btPort == 0 ? GetRandomPort() : btPort;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _containerEngine = DetectContainerEngine();
        }

        public int WebPort => _webPort;
        public int BitTorrentPort => _btPort;
        public string ContainerId => _containerId;

        private static string DetectContainerEngine()
        {
            // Try podman first, then docker
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "podman",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = Process.Start(psi);
                process.WaitForExit(5000);
                if (process.ExitCode == 0)
                {
                    return "podman";
                }
            }
            catch
            {
                // Podman not available
            }

            return "docker"; // Default to docker
        }

        private static int GetRandomPort()
        {
            using var socket = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            socket.Start();
            int port = ((System.Net.IPEndPoint)socket.LocalEndpoint).Port;
            socket.Stop();
            return port;
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            string imageName;
            List<string> runArgs;

            switch (_clientType)
            {
                case BitTorrentClientType.QBittorrent:
                    imageName = "linuxserver/qbittorrent:latest";
                    runArgs = new List<string>
                    {
                        "run", "-d",
                        "-p", $"{_webPort}:8080",
                        "-p", $"{_btPort}:6881/tcp",
                        "-p", $"{_btPort}:6881/udp",
                        "-e", "WEBUI_PORT=8080",
                        "-e", "PUID=1000",
                        "-e", "PGID=1000",
                        "--name", $"qbt-test-{Guid.NewGuid():N}",
                        imageName
                    };
                    break;

                case BitTorrentClientType.Transmission:
                    imageName = "linuxserver/transmission:latest";
                    runArgs = new List<string>
                    {
                        "run", "-d",
                        "-p", $"{_webPort}:9091",
                        "-p", $"{_btPort}:51413/tcp",
                        "-p", $"{_btPort}:51413/udp",
                        "-e", "PUID=1000",
                        "-e", "PGID=1000",
                        "--name", $"transmission-test-{Guid.NewGuid():N}",
                        imageName
                    };
                    break;

                case BitTorrentClientType.Deluge:
                    imageName = "linuxserver/deluge:latest";
                    runArgs = new List<string>
                    {
                        "run", "-d",
                        "-p", $"{_webPort}:8112",
                        "-p", $"{_btPort}:6881/tcp",
                        "-p", $"{_btPort}:6881/udp",
                        "-e", "PUID=1000",
                        "-e", "PGID=1000",
                        "--name", $"deluge-test-{Guid.NewGuid():N}",
                        imageName
                    };
                    break;

                default:
                    throw new ArgumentException($"Unsupported client type: {_clientType}");
            }

            // Pull image first
            await ExecuteCommandAsync(_containerEngine, $"pull {imageName}", cancellationToken).ConfigureAwait(false);

            // Start container
            string output = await ExecuteCommandAsync(_containerEngine, string.Join(" ", runArgs), cancellationToken).ConfigureAwait(false);
            _containerId = output.Trim();

            // Wait for web UI to be ready
            await WaitForWebUIAsync(cancellationToken).ConfigureAwait(false);

            // Authenticate
            await AuthenticateAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task<string> ExecuteCommandAsync(string command, string arguments, CancellationToken cancellationToken)
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                throw new InvalidOperationException($"Failed to start process: {command} {arguments}");
            }

            string output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            string error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Command failed: {command} {arguments}\nError: {error}");
            }

            return output;
        }

        private async Task WaitForWebUIAsync(CancellationToken cancellationToken)
        {
            string url = $"http://localhost:{_webPort}";
            var timeout = DateTime.UtcNow.AddMinutes(2);

            while (DateTime.UtcNow < timeout)
            {
                try
                {
                    var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        return; // Web UI is responding
                    }
                }
                catch
                {
                    // Not ready yet
                }

                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
            }

            throw new TimeoutException($"Web UI did not become ready within timeout period");
        }

        private async Task AuthenticateAsync(CancellationToken cancellationToken)
        {
            switch (_clientType)
            {
                case BitTorrentClientType.QBittorrent:
                    await AuthenticateQBittorrentAsync(cancellationToken).ConfigureAwait(false);
                    break;
                case BitTorrentClientType.Transmission:
                    // Transmission uses HTTP Basic Auth
                    _authCookie = Convert.ToBase64String(Encoding.ASCII.GetBytes("admin:admin"));
                    break;
                case BitTorrentClientType.Deluge:
                    await AuthenticateDelugeAsync(cancellationToken).ConfigureAwait(false);
                    break;
            }
        }

        private async Task AuthenticateQBittorrentAsync(CancellationToken cancellationToken)
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("username", "admin"),
                new KeyValuePair<string, string>("password", "adminadmin")
            });

            var response = await _httpClient.PostAsync($"http://localhost:{_webPort}/api/v2/auth/login", content, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                _authCookie = cookies.FirstOrDefault();
            }
        }

        private async Task AuthenticateDelugeAsync(CancellationToken cancellationToken)
        {
            var request = new
            {
                method = "auth.login",
                @params = new[] { "deluge" },
                id = 1
            };

            var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"http://localhost:{_webPort}/json", content, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                _authCookie = cookies.FirstOrDefault();
            }
        }

        public async Task<string> AddTorrentAsync(string torrentFilePath, string downloadPath, CancellationToken cancellationToken = default)
        {
            switch (_clientType)
            {
                case BitTorrentClientType.QBittorrent:
                    return await AddTorrentQBittorrentAsync(torrentFilePath, downloadPath, cancellationToken).ConfigureAwait(false);
                case BitTorrentClientType.Transmission:
                    return await AddTorrentTransmissionAsync(torrentFilePath, downloadPath, cancellationToken).ConfigureAwait(false);
                case BitTorrentClientType.Deluge:
                    return await AddTorrentDelugeAsync(torrentFilePath, downloadPath, cancellationToken).ConfigureAwait(false);
                default:
                    throw new NotSupportedException($"Client type {_clientType} not supported");
            }
        }

        private async Task<string> AddTorrentQBittorrentAsync(string torrentFilePath, string downloadPath, CancellationToken cancellationToken)
        {
            using var formData = new MultipartFormDataContent();
            using var fileStream = File.OpenRead(torrentFilePath);
            using var fileContent = new StreamContent(fileStream);
            formData.Add(fileContent, "torrents", Path.GetFileName(torrentFilePath));
            formData.Add(new StringContent(downloadPath), "savepath");

            var request = new HttpRequestMessage(HttpMethod.Post, $"http://localhost:{_webPort}/api/v2/torrents/add");
            request.Content = formData;
            if (!string.IsNullOrEmpty(_authCookie))
            {
                request.Headers.Add("Cookie", _authCookie);
            }

            var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            // Get torrent hash
            await Task.Delay(1000, cancellationToken).ConfigureAwait(false); // Wait for torrent to be added
            return Path.GetFileNameWithoutExtension(torrentFilePath);
        }

        private async Task<string> AddTorrentTransmissionAsync(string torrentFilePath, string downloadPath, CancellationToken cancellationToken)
        {
            byte[] torrentData = await File.ReadAllBytesAsync(torrentFilePath, cancellationToken).ConfigureAwait(false);
            string base64Torrent = Convert.ToBase64String(torrentData);

            var request = new
            {
                method = "torrent-add",
                arguments = new
                {
                    metainfo = base64Torrent,
                    download_dir = downloadPath
                }
            };

            var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"http://localhost:{_webPort}/transmission/rpc");
            httpRequest.Content = content;
            if (!string.IsNullOrEmpty(_authCookie))
            {
                httpRequest.Headers.Add("Authorization", $"Basic {_authCookie}");
            }

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var json = JObject.Parse(responseBody);
            return json["arguments"]?["torrent-added"]?["hashString"]?.ToString() ?? "";
        }

        private async Task<string> AddTorrentDelugeAsync(string torrentFilePath, string downloadPath, CancellationToken cancellationToken)
        {
            byte[] torrentData = await File.ReadAllBytesAsync(torrentFilePath, cancellationToken).ConfigureAwait(false);
            string base64Torrent = Convert.ToBase64String(torrentData);

            var request = new
            {
                method = "web.add_torrents",
                @params = new object[]
                {
                    new[]
                    {
                        new
                        {
                            path = torrentFilePath,
                            options = new { download_location = downloadPath }
                        }
                    }
                },
                id = 1
            };

            var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"http://localhost:{_webPort}/json");
            httpRequest.Content = content;
            if (!string.IsNullOrEmpty(_authCookie))
            {
                httpRequest.Headers.Add("Cookie", _authCookie);
            }

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return Path.GetFileNameWithoutExtension(torrentFilePath);
        }

        public async Task<TorrentStats> GetTorrentStatsAsync(string torrentHash, CancellationToken cancellationToken = default)
        {
            switch (_clientType)
            {
                case BitTorrentClientType.QBittorrent:
                    return await GetTorrentStatsQBittorrentAsync(torrentHash, cancellationToken).ConfigureAwait(false);
                case BitTorrentClientType.Transmission:
                    return await GetTorrentStatsTransmissionAsync(torrentHash, cancellationToken).ConfigureAwait(false);
                case BitTorrentClientType.Deluge:
                    return await GetTorrentStatsDelugeAsync(torrentHash, cancellationToken).ConfigureAwait(false);
                default:
                    throw new NotSupportedException($"Client type {_clientType} not supported");
            }
        }

        private async Task<TorrentStats> GetTorrentStatsQBittorrentAsync(string torrentHash, CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"http://localhost:{_webPort}/api/v2/torrents/info");
            if (!string.IsNullOrEmpty(_authCookie))
            {
                request.Headers.Add("Cookie", _authCookie);
            }

            var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var torrents = JsonConvert.DeserializeObject<JArray>(json);
            var torrent = torrents?.FirstOrDefault();

            if (torrent == null)
            {
                return new TorrentStats();
            }

            return new TorrentStats
            {
                Progress = (double)(torrent["progress"] ?? 0.0),
                Downloaded = (long)(torrent["downloaded"] ?? 0L),
                Uploaded = (long)(torrent["uploaded"] ?? 0L),
                Peers = (int)(torrent["num_leechs"] ?? 0),
                Seeds = (int)(torrent["num_seeds"] ?? 0),
                State = torrent["state"]?.ToString() ?? "unknown"
            };
        }

        private async Task<TorrentStats> GetTorrentStatsTransmissionAsync(string torrentHash, CancellationToken cancellationToken)
        {
            var request = new
            {
                method = "torrent-get",
                arguments = new
                {
                    fields = new[] { "percentDone", "downloadedEver", "uploadedEver", "peersConnected", "status" }
                }
            };

            var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"http://localhost:{_webPort}/transmission/rpc");
            httpRequest.Content = content;
            if (!string.IsNullOrEmpty(_authCookie))
            {
                httpRequest.Headers.Add("Authorization", $"Basic {_authCookie}");
            }

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var result = JObject.Parse(json);
            var torrents = result["arguments"]?["torrents"] as JArray;
            var torrent = torrents?.FirstOrDefault();

            if (torrent == null)
            {
                return new TorrentStats();
            }

            return new TorrentStats
            {
                Progress = (double)(torrent["percentDone"] ?? 0.0),
                Downloaded = (long)(torrent["downloadedEver"] ?? 0L),
                Uploaded = (long)(torrent["uploadedEver"] ?? 0L),
                Peers = (int)(torrent["peersConnected"] ?? 0),
                State = torrent["status"]?.ToString() ?? "unknown"
            };
        }

        private async Task<TorrentStats> GetTorrentStatsDelugeAsync(string torrentHash, CancellationToken cancellationToken)
        {
            var request = new
            {
                method = "web.update_ui",
                @params = new object[]
                {
                    new[] { "progress", "total_done", "total_uploaded", "num_peers", "state" },
                    new { }
                },
                id = 1
            };

            var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"http://localhost:{_webPort}/json");
            httpRequest.Content = content;
            if (!string.IsNullOrEmpty(_authCookie))
            {
                httpRequest.Headers.Add("Cookie", _authCookie);
            }

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var result = JObject.Parse(json);
            var torrents = result["result"]?["torrents"] as JObject;
            var torrent = torrents?.First as JProperty;

            if (torrent == null)
            {
                return new TorrentStats();
            }

            var torrentData = torrent.Value as JObject;
            return new TorrentStats
            {
                Progress = (double)(torrentData?["progress"] ?? 0.0) / 100.0,
                Downloaded = (long)(torrentData?["total_done"] ?? 0L),
                Uploaded = (long)(torrentData?["total_uploaded"] ?? 0L),
                Peers = (int)(torrentData?["num_peers"] ?? 0),
                State = torrentData?["state"]?.ToString() ?? "unknown"
            };
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(_containerId))
            {
                return;
            }

            try
            {
                await ExecuteCommandAsync(_containerEngine, $"stop {_containerId}", cancellationToken).ConfigureAwait(false);
                await ExecuteCommandAsync(_containerEngine, $"rm {_containerId}", cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Best effort cleanup
            }
        }

        public void Dispose()
        {
            StopAsync().GetAwaiter().GetResult();
            _httpClient?.Dispose();
        }

        public class TorrentStats
        {
            public double Progress { get; set; }
            public long Downloaded { get; set; }
            public long Uploaded { get; set; }
            public int Peers { get; set; }
            public int Seeds { get; set; }
            public string State { get; set; }
        }
    }
}

