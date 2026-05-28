// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/NoOpRegistrySpoofer.java:1-40
// Copyright 2021-2025 NCSDecomp
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using static KOTORModSync.Formats.NCS.NCSDecomp.DecompilerLogger;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/NoOpRegistrySpoofer.java:14-39
    // Original: public class NoOpRegistrySpoofer implements AutoCloseable
    /// <summary>
    /// A no-op registry spoofer for compilers that don't require registry spoofing.
    /// This class provides the same interface as RegistrySpoofer but performs no operations.
    /// </summary>
    public class NoOpRegistrySpoofer : IDisposable
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/NoOpRegistrySpoofer.java:18-20
        // Original: public NoOpRegistrySpoofer()
        public NoOpRegistrySpoofer()
        {
            Error("DEBUG NoOpRegistrySpoofer: Created (no registry spoofing needed)");
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/NoOpRegistrySpoofer.java:27-30
        // Original: public NoOpRegistrySpoofer activate()
        public NoOpRegistrySpoofer Activate()
        {
            Error("DEBUG NoOpRegistrySpoofer: activate() called (no-op)");
            return this;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/NoOpRegistrySpoofer.java:35-38
        // Original: public void close()
        public void Dispose()
        {
            Error("DEBUG NoOpRegistrySpoofer: close() called (no-op)");
        }
    }
}

