// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ScriptNode.java:8-27
// Original: public abstract class ScriptNode
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace KOTORModSync.Formats.NCS.NCSDecomp.Scriptnode
{
    public abstract class ScriptNode
    {
        private ScriptNode parent;
        protected string tabs;
        protected string newline = System.Environment.NewLine;

        public ScriptNode()
        {
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ScriptNode.java:13-15
        // Original: public ScriptNode parent()
        public ScriptNode Parent()
        {
            return this.parent;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ScriptNode.java:17-22
        // Original: public void parent(ScriptNode parent)
        public virtual void Parent(ScriptNode parent)
        {
            this.parent = parent;
            if (parent != null)
            {
                this.tabs = parent.tabs + "\t";
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ScriptNode.java:24-26
        // Original: public void close()
        public virtual void Close()
        {
            this.parent = null;
        }
    }
}




