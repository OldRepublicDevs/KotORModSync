// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net.Http;

namespace KOTORModSync.Core.Services.Download
{
	/// <summary>
	/// Centralized factory for creating download handlers in the correct order.
	/// Handler order is critical: specific handlers must come before generic ones.
	/// DirectDownloadHandler MUST always be last as it's a catch-all fallback.
	/// </summary>
	public static class DownloadHandlerFactory
	{
		/// <summary>
		/// Creates a list of download handlers in the correct priority order.
		/// </summary>
		/// <param name="httpClient">The HttpClient to use for HTTP-based handlers. If null, a new one will be created.</param>
		/// <param name="nexusModsApiKey">Optional Nexus Mods API key for authenticated downloads.</param>
		/// <param name="timeoutMinutes">Optional timeout in minutes for the HttpClient. Default is 180 minutes (3 hours).</param>
		/// <returns>A list of download handlers ordered from most specific to most generic.</returns>
		public static List<IDownloadHandler> CreateHandlers(
			HttpClient httpClient = null,
			string nexusModsApiKey = null,
			int timeoutMinutes = 180)
		{
			// Create HttpClient if not provided
			if (httpClient is null)
			{
				HttpClientHandler handler = new HttpClientHandler
				{
					AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
					MaxConnectionsPerServer = 128,
				};
				httpClient = new HttpClient(handler)
				{
					Timeout = TimeSpan.FromMinutes(timeoutMinutes),
				};
				Logger.LogVerbose($"[DownloadHandlerFactory] Created new HttpClient with {timeoutMinutes} minute timeout");
			}

			// Use the API key from parameter, or fall back to MainConfig if not provided
			string apiKey = nexusModsApiKey ?? MainConfig.NexusModsApiKey;

			// Handler order is CRITICAL:
			// 1. Most specific handlers first (DeadlyStream, Mega, NexusMods, GameFront)
			// 2. Generic fallback handler last (DirectDownload - catches ANY http/https)
			List<IDownloadHandler> handlers = new List<IDownloadHandler>
			{
				new DeadlyStreamDownloadHandler(httpClient),
				new MegaDownloadHandler(),
				new NexusModsDownloadHandler(httpClient, apiKey),
				new GameFrontDownloadHandler(httpClient),
				new DirectDownloadHandler(httpClient),  // MUST be last - fallback for any HTTP/HTTPS
			};

			Logger.LogVerbose($"[DownloadHandlerFactory] Created {handlers.Count} download handlers in priority order");
			return handlers;
		}

		/// <summary>
		/// Creates a DownloadManager with the standard handler configuration.
		/// </summary>
		/// <param name="httpClient">The HttpClient to use. If null, a new one will be created.</param>
		/// <param name="nexusModsApiKey">Optional Nexus Mods API key.</param>
		/// <param name="timeoutMinutes">Optional timeout in minutes. Default is 180 (3 hours).</param>
		/// <returns>A configured DownloadManager instance.</returns>
		public static DownloadManager CreateDownloadManager(
			HttpClient httpClient = null,
			string nexusModsApiKey = null,
			int timeoutMinutes = 180)
		{
			List<IDownloadHandler> handlers = CreateHandlers(httpClient, nexusModsApiKey, timeoutMinutes);
			return new DownloadManager(handlers);
		}
	}
}