// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AIf.java
// Original: public class AIf extends AControlLoop
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode;

namespace KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode
{
    // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AIf.java:8-26
    // Original: public AIf(int start, int end, AExpression condition) { super(start, end); this.condition(condition); }
    public class AIf : AControlLoop
    {
        public AIf(int start, int end, AExpression condition) : base(start, end)
        {
            this.Condition(condition);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AIf.java:14-26
        // Original: @Override public String toString() { StringBuffer buff = new StringBuffer(); String cond = this.formattedCondition(); buff.append(this.tabs + "if" + cond + " {" + this.newline); for (int i = 0; i < this.children.size(); i++) { buff.append(this.children.get(i).toString()); } buff.append(this.tabs + "}" + this.newline); return buff.toString(); }
        public override string ToString()
        {
            StringBuilder buff = new StringBuilder();
            string cond = this.FormattedCondition();
            buff.Append(this.tabs + "if" + cond + " {" + this.newline);

            for (int i = 0; i < this.children.Count; i++)
            {
                buff.Append(this.children[i].ToString());
            }

            buff.Append(this.tabs + "}" + this.newline);
            return buff.ToString();
        }
    }
}





