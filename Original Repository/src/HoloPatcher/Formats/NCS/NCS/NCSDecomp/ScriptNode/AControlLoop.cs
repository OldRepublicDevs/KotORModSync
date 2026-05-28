// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AControlLoop.java:7-49
// Original: public class AControlLoop extends ScriptRootNode
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode;

namespace KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode
{
    public class AControlLoop : ScriptRootNode
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AControlLoop.java:8
        // Original: protected AExpression condition;
        protected AExpression condition;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AControlLoop.java:10-12
        // Original: public AControlLoop(int start, int end) { super(start, end); }
        public AControlLoop(int start, int end) : base(start, end)
        {
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AControlLoop.java:14-16
        // Original: public void end(int end) { this.end = end; }
        public virtual void End(int end)
        {
            this.end = end;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AControlLoop.java:18-21
        // Original: public void condition(AExpression condition) { condition.parent(this); this.condition = condition; }
        public virtual void Condition(AExpression condition)
        {
            condition.Parent(this);
            this.condition = condition;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AControlLoop.java:23-25
        // Original: public AExpression condition() { return this.condition; }
        public virtual AExpression Condition()
        {
            return this.condition;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AControlLoop.java:27-39
        // Original: protected String formattedCondition() { if (this.condition == null) { return " ()"; } String cond = this.condition.toString().trim(); boolean wrapped = cond.startsWith("(") && cond.endsWith(")"); String wrappedCond = wrapped ? cond : "(" + cond + ")"; return " " + wrappedCond; }
        protected string FormattedCondition()
        {
            if (this.condition == null)
            {
                return " ()";
            }

            string cond = this.condition.ToString().Trim();
            bool wrapped = cond.StartsWith("(") && cond.EndsWith(")");
            string wrappedCond = wrapped ? cond : "(" + cond + ")";
            return " " + wrappedCond;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AControlLoop.java:41-48
        // Original: @Override public void close() { super.close(); if (this.condition != null) { ((ScriptNode)this.condition).close(); this.condition = null; } }
        public override void Close()
        {
            base.Close();
            if (this.condition != null)
            {
                ((ScriptNode)this.condition).Close();
                this.condition = null;
            }
        }
    }
}





