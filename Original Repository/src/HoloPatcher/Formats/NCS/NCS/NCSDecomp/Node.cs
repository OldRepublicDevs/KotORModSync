// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/Node.java:1-115
// Original: public abstract class Node extends Switchable implements Cloneable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp.Analysis;
namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    public abstract class Node : Switchable, Cloneable
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/Node.java
        // Original: private Node parent;
        private Node parent;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/Node.java
        // Original: public void apply(Switch sw) { ... }
        // NOTE: Base implementation - subclasses override this to call their visitor method
        public virtual void Apply(Switch sw)
        {
            // Default implementation - subclasses like AActionCmd override this
            // to call the appropriate visitor method on the switch
            if (sw is AnalysisAdapter adapter)
            {
                Apply(adapter);
            }
        }

        public virtual void Apply(AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }
        public virtual object Clone()
        {
            throw new System.NotImplementedException("Clone must be implemented in derived classes");
        }
        public override string ToString()
        {
            return base.ToString();
        }
        public virtual Node Parent()
        {
            return this.parent;
        }

        public virtual void Parent(Node parent)
        {
            this.parent = parent;
        }

        public abstract void RemoveChild(Node p0);
        public abstract void ReplaceChild(Node p0, Node p1);
        public virtual void ReplaceBy(Node node)
        {
            if (this.parent != null)
            {
                this.parent.ReplaceChild(this, node);
            }
        }

        protected virtual string ToString(Node node)
        {
            if (node != null)
            {
                return node.ToString();
            }

            return "";
        }

        protected virtual string ToString(IList<object> list)
        {
            StringBuilder s = new StringBuilder();
            foreach (object item in list)
            {
                s.Append(item);
            }

            return s.ToString();
        }

        protected virtual Node CloneNode(Node node)
        {
            if (node != null)
            {
                return (Node)node.Clone();
            }

            return null;
        }

        protected virtual IList<object> CloneList(IList<object> list)
        {
            IList<object> clone = new List<object>();
            foreach (object item in list)
            {
                if (item is ICloneable cloneable)
                {
                    clone.Add(cloneable.Clone());
                }
                else
                {
                    clone.Add(item);
                }
            }

            return clone;
        }
    }
}




