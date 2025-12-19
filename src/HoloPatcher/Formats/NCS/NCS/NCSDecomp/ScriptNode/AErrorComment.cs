// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AErrorComment.java:9-20
// Original: public class AErrorComment extends ScriptNode
using System;

namespace KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode
{
    public class AErrorComment : ScriptNode
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AErrorComment.java:10
        // Original: private final String message;
        private readonly string message;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AErrorComment.java:12-14
        // Original: public AErrorComment(String message) { this.message = message; }
        public AErrorComment(string message)
        {
            this.message = message;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AErrorComment.java:17-19
        // Original: @Override public String toString() { return this.tabs + "/* " + this.message + " */" + this.newline; }
        public override string ToString()
        {
            return tabs + "/* " + message + " */" + newline;
        }
    }
}

