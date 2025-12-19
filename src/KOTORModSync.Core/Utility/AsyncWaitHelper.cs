// Copyright 2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace KOTORModSync.Core.Utility
{
    /// <summary>
    /// Provides reusable helpers for polling asynchronous conditions with timeouts.
    /// </summary>
    public static class AsyncWaitHelper
    {
        /// <summary>
        /// Repeatedly evaluates the supplied condition until it succeeds or the timeout elapses.
        /// </summary>
        /// <param name="condition">Condition to evaluate. Should return <c>true</c> when satisfied.</param>
        /// <param name="timeout">Maximum duration to wait before raising a timeout exception.</param>
        /// <param name="pollInterval">Optional custom poll interval. Defaults to one second.</param>
        /// <param name="errorFactory">Factory for the timeout exception. If <c>null</c>, <see cref="TimeoutException"/> is used.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public static async Task WaitUntilAsync(
            Func<Task<bool>> condition,
            TimeSpan timeout,
            TimeSpan? pollInterval = null,
            Func<Exception> errorFactory = null,
            CancellationToken cancellationToken = default)
        {
            if (condition is null)
            {
                throw new ArgumentNullException(nameof(condition));
            }

            TimeSpan interval = pollInterval ?? TimeSpan.FromSeconds(1);
            DateTime deadline = DateTime.UtcNow.Add(timeout);

            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (await condition().ConfigureAwait(false))
                {
                    return;
                }

                await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            }

            Exception exception = errorFactory != null ? errorFactory() : new TimeoutException("The awaited condition was not satisfied within the allotted time.");
            throw exception;
        }
    }
}

