// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DecompilerException.java
// Copyright 2021-2025 NCSDecomp
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DecompilerException.java:11-32
    // Original: public class DecompilerException extends Exception
    /// <summary>
    /// Checked exception used to signal decompilation or IO failures that should be
    /// presented to the user rather than crashing the UI/CLI.
    /// </summary>
    public class DecompilerException : Exception
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DecompilerException.java:19-21
        // Original: public DecompilerException(String msg)
        /// <summary>
        /// Creates a new decompiler exception with a user-facing message.
        /// </summary>
        /// <param name="msg">description of the failure</param>
        public DecompilerException(string msg) : base(msg)
        {
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DecompilerException.java:29-31
        // Original: public DecompilerException(String msg, Throwable cause)
        /// <summary>
        /// Creates a new decompiler exception with a message and underlying cause.
        /// </summary>
        /// <param name="msg">description of the failure</param>
        /// <param name="cause">root cause for debugging</param>
        public DecompilerException(string msg, Exception cause) : base(msg, cause)
        {
        }
    }
}




