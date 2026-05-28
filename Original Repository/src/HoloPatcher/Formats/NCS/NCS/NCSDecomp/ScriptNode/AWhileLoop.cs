// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AWhileLoop.java
// Original: public class AWhileLoop extends AControlLoop
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode;

namespace KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode
{
    // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AWhileLoop.java:8-24
    // Original: public AWhileLoop(int start, int end) { super(start, end); }
    public class AWhileLoop : AControlLoop
    {
        public AWhileLoop(int start, int end) : base(start, end)
        {
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AWhileLoop.java:13-24
        // Original: @Override public String toString() { StringBuffer buff = new StringBuffer(); buff.append(this.tabs + "while" + this.formattedCondition() + " {" + this.newline); for (int i = 0; i < this.children.size(); i++) { buff.append(this.children.get(i).toString()); } buff.append(this.tabs + "}" + this.newline); return buff.toString(); }
        public override string ToString()
        {
            StringBuilder buff = new StringBuilder();
            buff.Append(this.tabs + "while" + this.FormattedCondition() + " {" + this.newline);

            for (int i = 0; i < this.children.Count; i++)
            {
                buff.Append(this.children[i].ToString());
            }

            buff.Append(this.tabs + "}" + this.newline);
            return buff.ToString();
        }
    }
}





