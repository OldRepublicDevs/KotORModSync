// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Security.Cryptography;
using System.Text;

namespace KOTORModSync.Core.Services
{
    public class TelemetryAuthenticator
    {
        private readonly string _signingSecret;
        private readonly string _sessionId;

        public TelemetryAuthenticator(string signingSecret, string sessionId)
        {
            _signingSecret = signingSecret;
            _sessionId = sessionId;
        }

        public string ComputeSignature(string requestPath, long timestamp)
        {
            if (string.IsNullOrEmpty(_signingSecret))
            {
                return null;
            }

            string message = $"POST|{requestPath}|{timestamp}|{_sessionId}";

            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_signingSecret)))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        public static long GetUnixTimestamp()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public bool HasValidSecret()
        {
            return !string.IsNullOrEmpty(_signingSecret);
        }
    }
}
