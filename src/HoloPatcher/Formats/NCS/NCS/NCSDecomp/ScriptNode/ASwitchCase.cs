// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ASwitchCase.java:11-75
// Original: public class ASwitchCase extends ScriptRootNode
using System.Collections.Generic;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode;

namespace KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode
{
    public class ASwitchCase : ScriptRootNode
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ASwitchCase.java:12
        // Original: protected AConst val;
        protected AConst val;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ASwitchCase.java:14-17
        // Original: public ASwitchCase(int start, AConst val) { super(start, -1); this.val(val); }
        public ASwitchCase(int start, AConst val) : base(start, -1)
        {
            this.Val(val);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ASwitchCase.java:19-21
        // Original: public ASwitchCase(int start) { super(start, -1); }
        public ASwitchCase(int start) : base(start, -1)
        {
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ASwitchCase.java:23-25
        // Original: public void end(int end) { this.end = end; }
        public virtual void End(int end)
        {
            this.end = end;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ASwitchCase.java:27-30
        // Original: private void val(AConst val) { val.parent(this); this.val = val; }
        private void Val(AConst val)
        {
            val.Parent(this);
            this.val = val;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ASwitchCase.java:32-42
        // Original: public List<AUnkLoopControl> getUnknowns() { List<AUnkLoopControl> unks = new ArrayList<>(); for (ScriptNode node : this.children) { if (AUnkLoopControl.class.isInstance(node)) { unks.add((AUnkLoopControl)node); } } return unks; }
        public virtual List<AUnkLoopControl> GetUnknowns()
        {
            List<AUnkLoopControl> unks = new List<AUnkLoopControl>();

            foreach (ScriptNode node in this.children)
            {
                if (node is AUnkLoopControl unk)
                {
                    unks.Add(unk);
                }
            }

            return unks;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ASwitchCase.java:44-48
        // Original: public void replaceUnknown(AUnkLoopControl unk, ScriptNode newnode) { newnode.parent(this); this.children.set(this.children.indexOf(unk), newnode); unk.parent(null); }
        public virtual void ReplaceUnknown(AUnkLoopControl unk, ScriptNode newnode)
        {
            newnode.Parent(this);
            this.children[this.children.IndexOf(unk)] = newnode;
            unk.Parent(null);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ASwitchCase.java:50-64
        // Original: @Override public String toString() { StringBuffer buff = new StringBuffer(); if (this.val == null) { buff.append(this.tabs + "default:" + this.newline); } else { buff.append(this.tabs + "case " + this.val.toString() + ":" + this.newline); } for (int i = 0; i < this.children.size(); i++) { buff.append(this.children.get(i).toString()); } return buff.toString(); }
        public override string ToString()
        {
            StringBuilder buff = new StringBuilder();
            if (this.val == null)
            {
                buff.Append(this.tabs + "default:" + this.newline);
            }
            else
            {
                buff.Append(this.tabs + "case " + this.val.ToString() + ":" + this.newline);
            }

            for (int i = 0; i < this.children.Count; i++)
            {
                buff.Append(this.children[i].ToString());
            }

            return buff.ToString();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ASwitchCase.java:66-74
        // Original: @Override public void close() { super.close(); if (this.val != null) { this.val.close(); } this.val = null; }
        public override void Close()
        {
            base.Close();
            if (this.val != null)
            {
                this.val.Close();
            }

            this.val = null;
        }
    }
}





