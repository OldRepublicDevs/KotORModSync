// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace KOTORModSync.Core.Services.Download
{
    /// <summary>
    /// Captures log output for the duration of a mod component download. Uses AsyncLocal to track
    /// the current component context so logs can be attributed even across asynchronous continuations.
    /// </summary>
    public static class DownloadLogCaptureManager
    {
        private sealed class CaptureBuffer
        {
            public readonly List<string> Logs = new List<string>();
        }

        private sealed class CaptureScope : IDisposable
        {
            private readonly Guid _componentId;
            private readonly Guid? _previousComponentId;
            private bool _disposed;

            public CaptureScope(Guid componentId)
            {
                _componentId = componentId;
                _previousComponentId = s_currentComponentId.Value;
                s_currentComponentId.Value = componentId;

                CaptureBuffer buffer = s_captures.GetOrAdd(componentId, _ => new CaptureBuffer());
                lock (buffer)
                {
                    if (!_previousComponentId.HasValue || _previousComponentId.Value != componentId)
                    {
                        buffer.Logs.Clear();
                    }
                }
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                s_currentComponentId.Value = _previousComponentId;
                _disposed = true;
            }
        }

        private static readonly ConcurrentDictionary<Guid, CaptureBuffer> s_captures =
            new ConcurrentDictionary<Guid, CaptureBuffer>();

        private static readonly AsyncLocal<Guid?> s_currentComponentId = new AsyncLocal<Guid?>();
        private static readonly object s_subscriptionLock = new object();
        private static bool s_isSubscribed;

        private static void EnsureSubscribed()
        {
            if (s_isSubscribed)
            {
                return;
            }

            lock (s_subscriptionLock)
            {
                if (s_isSubscribed)
                {
                    return;
                }

                Logger.Logged += OnLogged;
                s_isSubscribed = true;
            }
        }

        private static void OnLogged(string logMessage)
        {
            Guid? componentId = s_currentComponentId.Value;
            if (!componentId.HasValue)
            {
                return;
            }

            CaptureBuffer buffer = s_captures.GetOrAdd(componentId.Value, _ => new CaptureBuffer());
            lock (buffer)
            {
                buffer.Logs.Add(logMessage);
            }
        }

        public static IDisposable BeginCapture(Guid componentId)
        {
            EnsureSubscribed();
            return new CaptureScope(componentId);
        }

        public static IReadOnlyList<string> GetCapturedLogs(Guid? componentId)
        {
            if (!componentId.HasValue)
            {
                return Array.Empty<string>();
            }

            if (!s_captures.TryGetValue(componentId.Value, out CaptureBuffer buffer))
            {
                return Array.Empty<string>();
            }

            lock (buffer)
            {
                return buffer.Logs.ToList();
            }
        }
    }
}

