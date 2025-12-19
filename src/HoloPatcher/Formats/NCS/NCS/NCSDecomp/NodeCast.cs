// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/NodeCast.java:10-24
// Original: public class NodeCast implements Cast<Node>
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    public class NodeCast : ICast
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/NodeCast.java:12
        // Original: public static final NodeCast instance = new NodeCast();
        public static readonly NodeCast instance;
        static NodeCast()
        {
            instance = new NodeCast();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/NodeCast.java:14-15
        // Original: private NodeCast() { }
        private NodeCast()
        {
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/NodeCast.java:17-23
        // Original: @Override public Node cast(Object o) { if (!(o instanceof Node)) { throw new ClassCastException(...); } return (Node)o; }
        public virtual object Cast(object o)
        {
            if (!(o is Node))
            {
                throw new InvalidCastException("Expected Node but got: " + (o != null ? o.GetType().Name : "null"));
            }
            return o;
        }
    }
}




