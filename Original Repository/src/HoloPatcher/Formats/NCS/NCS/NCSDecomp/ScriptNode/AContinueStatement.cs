// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AContinueStatement.java:7-12
// Original: public class AContinueStatement extends ScriptNode
using KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode;

namespace KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode
{
    public class AContinueStatement : ScriptNode
    {
        public AContinueStatement()
        {
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AContinueStatement.java:8-11
        // Original: @Override public String toString() { return this.tabs + "continue;" + this.newline; }
        public override string ToString()
        {
            return this.tabs + "continue;" + this.newline;
        }
    }
}





