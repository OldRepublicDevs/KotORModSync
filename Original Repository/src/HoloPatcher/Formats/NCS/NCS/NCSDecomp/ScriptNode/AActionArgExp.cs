// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AActionArgExp.java:9-24
// Original: public class AActionArgExp extends ScriptRootNode implements AExpression
using KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode;
using KOTORModSync.Formats.NCS.NCSDecomp.Stack;

namespace KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode
{
    public class AActionArgExp : ScriptRootNode, AExpression
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AActionArgExp.java:10-14
        // Original: public AActionArgExp(int start, int end) { super(start, end); this.start = start; this.end = end; }
        public AActionArgExp(int start, int end) : base(start, end)
        {
            this.start = start;
            this.end = end;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AActionArgExp.java:16-19
        // Original: @Override public StackEntry stackentry() { return null; }
        public StackEntry Stackentry()
        {
            return null;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AActionArgExp.java:21-23
        // Original: @Override public void stackentry(StackEntry stackentry) { }
        public void Stackentry(StackEntry stackentry)
        {
        }
    }
}





