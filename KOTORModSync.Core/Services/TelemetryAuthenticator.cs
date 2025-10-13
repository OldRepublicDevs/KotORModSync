// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Security.Cryptography;
using System.Text;

namespace KOTORModSync.Core.Services
{
	/// <summary>
	/// Handles HMAC-SHA256 signing for telemetry requests to bolabaden.org
	/// Prevents unauthorized clients from sending fake metrics
	/// </summary>
	public class TelemetryAuthenticator
	{
		private readonly string _signingSecret;
		private readonly string _sessionId;

		public TelemetryAuthenticator(string signingSecret, string sessionId)
		{
			_signingSecret = signingSecret;
			_sessionId = sessionId;
		}

		/// <summary>
		/// Computes HMAC-SHA256 signature for a telemetry request
		/// </summary>
		/// <param name="requestPath">Request path (e.g., "/v1/metrics")</param>
		/// <param name="timestamp">Unix timestamp in seconds</param>
		/// <returns>Hex-encoded HMAC-SHA256 signature, or null if no secret available</returns>
		public string ComputeSignature(string requestPath, long timestamp)
		{
			if ( string.IsNullOrEmpty(_signingSecret) )
			{
				return null;
			}

			string message = $"POST|{requestPath}|{timestamp}|{_sessionId}";

			using ( var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_signingSecret)) )
			{
				byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
				return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
			}
		}

		/// <summary>
		/// Gets the current Unix timestamp in seconds
		/// </summary>
		public static long GetUnixTimestamp()
		{
			return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		}

		/// <summary>
		/// Checks if the authenticator has a valid signing secret
		/// </summary>
		public bool HasValidSecret()
		{
			return !string.IsNullOrEmpty(_signingSecret);
		}
	}
}

