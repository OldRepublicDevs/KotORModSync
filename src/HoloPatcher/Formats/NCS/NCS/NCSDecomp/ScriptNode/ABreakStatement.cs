// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ABreakStatement.java:7-12
// Original: public class ABreakStatement extends ScriptNode
using KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode;

namespace KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode
{
    public class ABreakStatement : ScriptNode
    {
        public ABreakStatement()
        {
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ABreakStatement.java:8-11
        // Original: @Override public String toString() { return this.tabs + "break;" + this.newline; }
        public override string ToString()
        {
            return this.tabs + "break;" + this.newline;
        }
    }
}





