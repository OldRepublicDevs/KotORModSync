// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AUnkLoopControl.java:8-23
// Original: public class AUnkLoopControl extends ScriptNode
namespace KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode
{
    public class AUnkLoopControl : ScriptNode
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AUnkLoopControl.java:9
        // Original: protected int dest;
        protected int dest;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AUnkLoopControl.java:11-13
        // Original: public AUnkLoopControl(int dest) { this.dest = dest; }
        public AUnkLoopControl(int dest)
        {
            this.dest = dest;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AUnkLoopControl.java:15-17
        // Original: public int getDestination() { return this.dest; }
        public int GetDestination()
        {
            return this.dest;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AUnkLoopControl.java:19-22
        // Original: @Override public String toString() { return "BREAK or CONTINUE undetermined"; }
        public override string ToString()
        {
            return "BREAK or CONTINUE undetermined";
        }
    }
}





