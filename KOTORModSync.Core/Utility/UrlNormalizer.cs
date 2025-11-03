// Copyright (C) 2025
// Licensed under the GPL version 3 license.

using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace KOTORModSync.Core.Utility
{
    /// <summary>
    /// Provides canonical URL normalization for deterministic content identification.
    /// Implements rules to ensure equivalent URLs produce identical normalized forms.
    /// </summary>
    public static class UrlNormalizer
    {
        private static readonly char[] UnreservedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~".ToCharArray();

        /// <summary>
        /// Normalizes a URL to canonical form.
        /// Rules:
        /// - Scheme and host to lowercase
        /// - Remove default ports (80 for http, 443 for https)
        /// - Percent-decode unreserved characters
        /// - Collapse /../ and /./ sequences
        /// - Remove trailing slash (except root)
        /// - Optionally sort and preserve query parameters
        /// </summary>
        public static string Normalize(string url, bool stripQueryParameters = false)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return url;
            }

            Uri uri;
            try
            {
                uri = new Uri(url);
            }
            catch
            {
                // If URL is invalid, return as-is
                return url;
            }

            var sb = new StringBuilder();

            // Scheme: lowercase
            sb.Append(uri.Scheme.ToLowerInvariant());
            sb.Append("://");

            // Host: lowercase
            sb.Append(uri.Host.ToLowerInvariant());

            // Port: omit if default
            bool isDefaultPort = (string.Equals(uri.Scheme, "http", StringComparison.Ordinal) && uri.Port == 80) ||
                                (string.Equals(uri.Scheme, "https", StringComparison.Ordinal) && uri.Port == 443);
            if (!isDefaultPort && uri.Port != -1)
            {
                sb.Append(':');
                sb.Append(uri.Port);
            }

            // Path: normalize and decode unreserved characters
            string path = NormalizePath(uri.AbsolutePath);
            sb.Append(path);

            // Query: optionally preserve and sort for consistency
            if (!stripQueryParameters && !string.IsNullOrEmpty(uri.Query))
            {
                string sortedQuery = SortQueryParameters(uri.Query);
                sb.Append(sortedQuery);
            }

            // Fragment: always removed for consistency
            // (fragments are client-side only and shouldn't affect content identity)

            return sb.ToString();
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path) || string.Equals(path, "/", StringComparison.Ordinal))
            {
                return "";
            }

            // Decode percent-encoded unreserved characters
            path = DecodeUnreservedCharacters(path);

            // Collapse /../ and /./
            path = CollapseDotSegments(path);

            // Remove trailing slash unless it's the root
            if (path.Length > 1 && path.EndsWith("/", StringComparison.Ordinal))
            {
                path = path.Substring(0, path.Length - 1);
            }

            return path;
        }

        private static string DecodeUnreservedCharacters(string path)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < path.Length; i++)
            {
                if (path[i] == '%' && i + 2 < path.Length)
                {
                    string hex = path.Substring(i + 1, 2);
                    if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int charCode))
                    {
                        char decodedChar = (char)charCode;
                        // Only decode if it's an unreserved character
                        if (UnreservedChars.Contains(decodedChar))
                        {
                            sb.Append(decodedChar);
                            i += 2;
                            continue;
                        }
                    }
                }
                sb.Append(path[i]);
            }
            return sb.ToString();
        }

        private static string CollapseDotSegments(string path)
        {
            // Split path into segments
            string[] segments = path.Split('/');
            var stack = new System.Collections.Generic.Stack<string>();

            foreach (string segment in segments)
            {
                if (string.Equals(segment, ".", StringComparison.Ordinal) || segment == "")
                {
                    // Skip single dot and empty segments (except we'll add empty for leading /)
                    if (stack.Count == 0 && segment == "")
                    {
                        stack.Push(segment);
                    }
                }
                else if (string.Equals(segment, "..", StringComparison.Ordinal))
                {
                    // Go up one level
                    if (stack.Count > 1) // Keep at least the root
                    {
                        stack.Pop();
                    }
                }
                else
                {
                    stack.Push(segment);
                }
            }

            // Reconstruct path
            string result = string.Join("/", stack.Reverse());

            // Ensure leading slash
            if (!result.StartsWith("/", StringComparison.Ordinal))
            {
                result = "/" + result;
            }

            return result;
        }

        private static string SortQueryParameters(string query)
        {
            if (string.IsNullOrEmpty(query) || query.Length <= 1)
            {
                return query;
            }

            // Remove leading '?' and split by '&'
            string queryWithoutQuestion = query.StartsWith("?", StringComparison.Ordinal) ? query.Substring(1) : query;
            string[] parameters = queryWithoutQuestion.Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries);

            if (parameters.Length <= 1)
            {
                return query;
            }

            // Sort parameters alphabetically
            System.Array.Sort(parameters, StringComparer.Ordinal);

            // Reconstruct query
            return "?" + string.Join("&", parameters);
        }
    }
}
