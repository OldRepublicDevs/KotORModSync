// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ScriptNode.java:8-27
// Original: public abstract class ScriptNode
namespace KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode
{
    public class ScriptNode
    {
        private ScriptNode parent;
        protected string tabs;
        protected string newline = System.Environment.NewLine;

        public ScriptNode()
        {
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ScriptNode.java:13-15
        // Original: public ScriptNode parent()
        public ScriptNode Parent()
        {
            return this.parent;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ScriptNode.java:17-22
        // Original: public void parent(ScriptNode parent)
        public void Parent(ScriptNode parent)
        {
            this.parent = parent;
            if (parent != null)
            {
                this.tabs = parent.tabs + "\t";
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ScriptNode.java:24-26
        // Original: public void close()
        public virtual void Close()
        {
            this.parent = null;
        }

        public string GetTabs()
        {
            return this.tabs ?? "";
        }

        public string GetNewline()
        {
            return this.newline ?? System.Environment.NewLine;
        }
    }
}






