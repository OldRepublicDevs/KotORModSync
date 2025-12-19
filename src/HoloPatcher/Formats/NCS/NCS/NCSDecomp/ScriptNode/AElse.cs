// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AElse.java:7-49
// Original: public class AElse extends ScriptRootNode
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode;

namespace KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode
{
    // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AElse.java:8-10
    // Original: public AElse(int start, int end) { super(start, end); }
    public class AElse : ScriptRootNode
    {
        public AElse(int start, int end) : base(start, end)
        {
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AElse.java:12-48
        // Original: @Override public String toString() { ... }
        public override string ToString()
        {
            StringBuilder buff = new StringBuilder();

            // Handle "else if" case: if the first (and only) child is an AIf, output "else if" instead of "else { if ... }"
            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AElse.java:17
            // Original: if (this.children.size() == 1 && AIf.class.isInstance(this.children.get(0))) { AIf ifChild = (AIf) this.children.get(0);
            if (this.children.Count == 1 && this.children[0] is AIf)
            {
                AIf ifChild = (AIf)this.children[0];
                // Format condition similar to AControlLoop.formattedCondition()
                // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AElse.java:19-29
                // Original: String cond; if (ifChild.condition() == null) { cond = " ()"; } else { String condStr = ifChild.condition().toString().trim(); boolean wrapped = condStr.startsWith("(") && condStr.endsWith(")"); cond = wrapped ? condStr : "(" + condStr + ")"; cond = " " + cond; } buff.append(this.tabs + "else if" + cond + " {" + this.newline);
                string cond;
                if (ifChild.Condition() == null)
                {
                    cond = " ()";
                }
                else
                {
                    string condStr = ifChild.Condition().ToString().Trim();
                    bool wrapped = condStr.StartsWith("(") && condStr.EndsWith(")");
                    cond = wrapped ? condStr : "(" + condStr + ")";
                    cond = " " + cond;
                }
                buff.Append(this.tabs + "else if" + cond + " {" + this.newline);

                // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AElse.java:31-33
                // Original: for (int i = 0; i < ifChild.children.size(); i++) { buff.append(ifChild.children.get(i).toString()); }
                var ifChildren = ifChild.GetChildren();
                for (int i = 0; i < ifChildren.Count; i++)
                {
                    buff.Append(ifChildren[i].ToString());
                }

                buff.Append(this.tabs + "}" + this.newline);
            }
            else
            {
                // Standard else block
                // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AElse.java:36-44
                // Original: else { buff.append(this.tabs + "else {" + this.newline); for (int i = 0; i < this.children.size(); i++) { buff.append(this.children.get(i).toString()); } buff.append(this.tabs + "}" + this.newline); }
                buff.Append(this.tabs + "else {" + this.newline);

                for (int i = 0; i < this.children.Count; i++)
                {
                    buff.Append(this.children[i].ToString());
                }

                buff.Append(this.tabs + "}" + this.newline);
            }

            return buff.ToString();
        }
    }
}





