// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ScriptRootNode.java:17-134
// Original: public abstract class ScriptRootNode extends ScriptNode
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode
{
    public abstract class ScriptRootNode : ScriptNode
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ScriptRootNode.java:18
        // Original: protected LinkedList<ScriptNode> children = new LinkedList<>();
        // Note: Java's LinkedList supports get(index), but C#'s LinkedList doesn't. Using List<T> instead.
        protected List<ScriptNode> children = new List<ScriptNode>();
        protected int start;
        protected int end;
        protected ScriptRootNode(int start, int end)
        {
            this.start = start;
            this.end = end;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ScriptRootNode.java:27-30
        // Original: public void addChild(ScriptNode child) { this.children.add(child); child.parent(this); }
        public virtual void AddChild(ScriptNode child)
        {
            this.children.Add(child);
            child.Parent(this);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ScriptRootNode.java:32-38
        // Original: public void addChildren(List<? extends ScriptNode> children) { Iterator<? extends ScriptNode> it = children.iterator(); while (it.hasNext()) { this.addChild(it.next()); } }
        public virtual void AddChildren(List<ScriptNode> children)
        {
            foreach (ScriptNode child in children)
            {
                this.AddChild(child);
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ScriptRootNode.java:40-48
        // Original: public ArrayList<ScriptNode> removeChildren(int first, int last) { ... }
        public virtual List<ScriptNode> RemoveChildren(int first, int last)
        {
            List<ScriptNode> removed = new List<ScriptNode>(last - first + 1);
            for (int i = 0; i <= last - first; i++)
            {
                removed.Add(this.RemoveChild(first));
            }

            return removed;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ScriptRootNode.java:50-52
        // Original: public ArrayList<ScriptNode> removeChildren(int first) { return this.removeChildren(first, this.children.size() - 1); }
        public virtual List<ScriptNode> RemoveChildren(int first)
        {
            return this.RemoveChildren(first, this.children.Count - 1);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ScriptRootNode.java:54-56
        // Original: public ArrayList<ScriptNode> removeChildren() { return this.removeChildren(0, this.children.size() - 1); }
        public virtual List<ScriptNode> RemoveChildren()
        {
            return this.RemoveChildren(0, this.children.Count - 1);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ScriptRootNode.java:58-60
        // Original: public ScriptNode removeLastChild() { return this.children.removeLast(); }
        public virtual ScriptNode RemoveLastChild()
        {
            if (this.children.Count == 0)
            {
                throw new InvalidOperationException("Cannot remove last child from empty list");
            }
            ScriptNode lastNode = this.children[this.children.Count - 1];
            this.children.RemoveAt(this.children.Count - 1);
            return lastNode;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ScriptRootNode.java:62-65
        // Original: public void removeChild(ScriptNode child) { this.children.remove(child); child.parent(null); }
        public virtual void RemoveChild(ScriptNode child)
        {
            bool removed = this.children.Remove(child);
            if (removed)
            {
                child.Parent(null);
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ScriptRootNode.java:67-71
        // Original: public ScriptNode removeChild(int index) { ScriptNode child = this.children.remove(index); child.parent(null); return child; }
        public virtual ScriptNode RemoveChild(int index)
        {
            ScriptNode child = this.children[index];
            this.children.RemoveAt(index);
            child.Parent(null);
            return child;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ScriptRootNode.java:73-75
        // Original: public ScriptNode getLastChild() { return this.children.getLast(); }
        public virtual ScriptNode GetLastChild()
        {
            if (this.children.Count == 0)
            {
                throw new InvalidOperationException("Cannot get last child from empty list");
            }
            return this.children[this.children.Count - 1];
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ScriptRootNode.java:77-79
        // Original: public ScriptNode getPreviousChild(int pos) { return this.children.size() < pos ? null : this.children.get(this.children.size() - pos); }
        public virtual ScriptNode GetPreviousChild(int pos)
        {
            return this.children.Count < pos ? null : this.children[this.children.Count - pos];
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ScriptRootNode.java:81-83
        // Original: public boolean hasChildren() { return this.children.size() > 0; }
        public virtual bool HasChildren()
        {
            return this.children.Count > 0;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ScriptRootNode.java:85-87
        // Original: public int getEnd() { return this.end; }
        public virtual int GetEnd()
        {
            return this.end;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ScriptRootNode.java:89-91
        // Original: public int getStart() { return this.start; }
        public virtual int GetStart()
        {
            return this.start;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ScriptRootNode.java:93-95
        // Original: public LinkedList<ScriptNode> getChildren() { return this.children; }
        public virtual List<ScriptNode> GetChildren()
        {
            return this.children;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ScriptRootNode.java:97-99
        // Original: public int getChildLocation(ScriptNode child) { return this.children.indexOf(child); }
        public virtual int GetChildLocation(ScriptNode child)
        {
            return this.children.IndexOf(child);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ScriptRootNode.java:101-106
        // Original: public void replaceChild(ScriptNode oldchild, ScriptNode newchild) { int index = this.getChildLocation(oldchild); this.children.set(index, newchild); newchild.parent(this); oldchild.parent(null); }
        public virtual void ReplaceChild(ScriptNode oldchild, ScriptNode newchild)
        {
            int index = this.GetChildLocation(oldchild);
            this.children[index] = newchild;
            newchild.Parent(this);
            oldchild.Parent(null);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ScriptRootNode.java:108-117
        // Original: @Override public String toString() { StringBuffer buff = new StringBuffer(); for (int i = 0; i < this.children.size(); i++) { buff.append(this.children.get(i).toString()); } return buff.toString(); }
        public override string ToString()
        {
            StringBuilder buff = new StringBuilder();
            for (int i = 0; i < this.children.Count; ++i)
            {
                buff.Append(this.children[i].ToString());
            }

            return buff.ToString();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ScriptRootNode.java:119-121
        // Original: public int size() { return this.children.size(); }
        public virtual int Size()
        {
            return this.children.Count;
        }

        public virtual ScriptNode GetChild(int index)
        {
            if (index < 0 || index >= this.children.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return this.children[index];
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ScriptRootNode.java:123-133
        // Original: @Override public void close() { super.close(); Iterator<ScriptNode> it = this.children.iterator(); while (it.hasNext()) { it.next().close(); } this.children = null; }
        public virtual void Close()
        {
            base.Close();
            System.Collections.Generic.IEnumerator<ScriptNode> it = this.children.GetEnumerator();
            while (it.MoveNext())
            {
                it.Current.Close();
            }

            this.children = null;
        }
    }
}




