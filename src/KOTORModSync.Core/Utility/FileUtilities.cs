// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;
using System.Threading.Tasks;

using JetBrains.Annotations;

namespace KOTORModSync.Core.Utility
{

    public static class FileUtilities
    {


        public static async Task SaveDocsToFileAsync([NotNull] string filePath, [NotNull] string documentation)
        {
            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            if (documentation is null)
            {
                throw new ArgumentNullException(nameof(documentation));
            }

            try
            {
                if (!string.IsNullOrEmpty(documentation))
                {
                    using (var writer = new StreamWriter(filePath))
                    {
                        await writer.WriteAsync(documentation).ConfigureAwait(false);
                        await writer.FlushAsync().ConfigureAwait(false);
                        writer.Dispose();
                    }
                }
            }
            catch (Exception e)
            {
                await Logger.LogExceptionAsync(e).ConfigureAwait(false);
            }
        }
    }
}
