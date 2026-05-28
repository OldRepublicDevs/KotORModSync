// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/NodeAnalysisData.java:18-321
// Original: public class NodeAnalysisData
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp;
using KOTORModSync.Formats.NCS.NCSDecomp.Stack;

namespace KOTORModSync.Formats.NCS.NCSDecomp.Utils
{
    public class NodeAnalysisData
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/NodeAnalysisData.java:19
        // Original: private Hashtable<Node, NodeData> nodedatahash = new Hashtable<>(1);
        private Dictionary<object, object> nodedatahash = new Dictionary<object, object>();

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/NodeAnalysisData.java:21-31
        // Original: public void close() { ... data.nextElement().close(); ... }
        public virtual void Close()
        {
            if (this.nodedatahash != null)
            {
                foreach (NodeData data in this.nodedatahash.Values)
                {
                    data.Close();
                }

                this.nodedatahash = null;
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/NodeAnalysisData.java:33-41
        // Original: public void setPos(Node node, int pos) { NodeAnalysisData.NodeData data = this.nodedatahash.get(node); if (data == null) { data = new NodeAnalysisData.NodeData(pos); this.nodedatahash.put(node, data); } else { data.pos = pos; } }
        public virtual void SetPos(Node node, int pos)
        {
            object existing;
            NodeData data;
            if (!this.nodedatahash.TryGetValue(node, out existing))
            {
                data = new NodeData(pos);
                this.nodedatahash[node] = data;
            }
            else
            {
                data = (NodeData)existing;
                data.pos = pos;
            }
        }


        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/NodeAnalysisData.java:43-50
        // Original: public int getPos(Node node)
        public virtual int GetPos(Node node)
        {
            object existing;
            if (!this.nodedatahash.TryGetValue(node, out existing))
            {
                throw new Exception("Attempted to read position on a node not in the hashtable.");
            }
            else
            {
                return ((NodeData)existing).pos;
            }
        }

        // Helper method to safely get position without throwing exception
        // Returns -1 if node is not in hashtable
        public virtual int TryGetPos(Node node)
        {
            object existing;
            if (!this.nodedatahash.TryGetValue(node, out existing))
            {
                return -1;
            }
            else
            {
                return ((NodeData)existing).pos;
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/NodeAnalysisData.java:52-61
        // Original: public void setDestination(Node jump, Node destination)
        public virtual void SetDestination(Node jump, Node destination)
        {
            object existing;
            NodeData data;
            if (!this.nodedatahash.TryGetValue(jump, out existing))
            {
                data = new NodeData();
                data.jumpDestination = destination;
                this.nodedatahash[jump] = data;
            }
            else
            {
                data = (NodeData)existing;
                data.jumpDestination = destination;
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/NodeAnalysisData.java:63-70
        // Original: public Node getDestination(Node node)
        public virtual Node GetDestination(Node node)
        {
            object existing;
            if (!this.nodedatahash.TryGetValue(node, out existing))
            {
                throw new Exception("Attempted to read destination on a node not in the hashtable.");
            }
            else
            {
                return ((NodeData)existing).jumpDestination;
            }
        }

        // Helper method to safely get destination without throwing exception
        // Returns null if node is not in hashtable or has no destination
        public virtual Node TryGetDestination(Node node)
        {
            object existing;
            if (!this.nodedatahash.TryGetValue(node, out existing))
            {
                return null;
            }
            else
            {
                return ((NodeData)existing).jumpDestination;
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/NodeAnalysisData.java:72-81
        // Original: public void setCodeState(Node node, byte state)
        public virtual void SetCodeState(Node node, byte state)
        {
            object existing;
            NodeData data;
            if (!this.nodedatahash.TryGetValue(node, out existing))
            {
                data = new NodeData();
                data.state = state;
                this.nodedatahash[node] = data;
            }
            else
            {
                data = (NodeData)existing;
                data.state = state;
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/NodeAnalysisData.java:83-94
        // Original: public void deadCode(Node node, boolean deadcode)
        public virtual void DeadCode(Node node, bool deadcode)
        {
            object existing;
            if (!this.nodedatahash.TryGetValue(node, out existing))
            {
                throw new Exception("Attempted to set status on a node not in the hashtable.");
            }
            else
            {
                NodeData data = (NodeData)existing;
                if (deadcode)
                {
                    data.state = 1;
                }
                else
                {
                    data.state = 0;
                }
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/NodeAnalysisData.java:96-103
        // Original: public boolean deadCode(Node node)
        public virtual bool DeadCode(Node node)
        {
            object existing;
            if (!this.nodedatahash.TryGetValue(node, out existing))
            {
                throw new Exception("Attempted to read status on a node not in the hashtable.");
            }
            else
            {
                NodeData data = (NodeData)existing;
                return data.state == 1 || data.state == 3;
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/NodeAnalysisData.java:105-112
        // Original: public boolean processCode(Node node)
        public virtual bool ProcessCode(Node node)
        {
            object existing;
            if (!this.nodedatahash.TryGetValue(node, out existing))
            {
                throw new Exception("Attempted to read status on a node not in the hashtable.");
            }
            else
            {
                NodeData data = (NodeData)existing;
                return data.state != 1;
            }
        }

        // Helper method to safely check if code should be processed without throwing exception
        // Returns true if node is not in hashtable (assume it should be processed)
        public virtual bool TryProcessCode(Node node)
        {
            object existing;
            if (!this.nodedatahash.TryGetValue(node, out existing))
            {
                return true; // Default to processing if node not found
            }
            else
            {
                NodeData data = (NodeData)existing;
                return data.state != 1;
            }
        }

        private NodeData GetOrCreateNodeData(Node node)
        {
            object existing;
            NodeData data;
            if (!this.nodedatahash.TryGetValue(node, out existing))
            {
                data = new NodeData();
                this.nodedatahash[node] = data;
            }
            else
            {
                data = (NodeData)existing;
            }
            return data;
        }

        public virtual void LogOrCode(Node node, bool logor)
        {
            NodeData data = this.GetOrCreateNodeData(node);
            data.state = logor ? (byte)2 : (byte)0;
        }

        public virtual bool LogOrCode(Node node)
        {
            object existing;
            if (!this.nodedatahash.TryGetValue(node, out existing))
            {
                return false;
            }

            return ((NodeData)existing).state == 2;
        }

        public virtual void AddOrigin(Node node, Node origin)
        {
            NodeData data = this.GetOrCreateNodeData(node);
            data.AddOrigin(origin);
        }

        public virtual Node RemoveLastOrigin(Node node)
        {
            object existing;
            if (!this.nodedatahash.TryGetValue(node, out existing))
            {
                return null;
            }

            NodeData data = (NodeData)existing;
            if (data.origins == null || data.origins.Count == 0)
            {
                return null;
            }

            object removed = data.origins[data.origins.Count - 1];
            data.origins.RemoveAt(data.origins.Count - 1);
            return (Node)removed;
        }

        public virtual void SetStack(Node node, LocalStack stack, bool overwrite)
        {
            object existing;
            if (!this.nodedatahash.TryGetValue(node, out existing))
            {
                NodeData data = new NodeData();
                data.stack = stack;
                this.nodedatahash[node] = data;
            }
            else
            {
                NodeData data = (NodeData)existing;
                if (data.stack == null || overwrite)
                {
                    data.stack = stack;
                }
            }
        }

        public virtual LocalStack GetStack(Node node)
        {
            object existing;
            if (this.nodedatahash.TryGetValue(node, out existing))
            {
                return ((NodeData)existing).stack;
            }

            return null;
        }

        public virtual void ClearProtoData()
        {
            foreach (NodeData e in this.nodedatahash.Values)
            {
                e.stack = null;
            }
        }

        public class NodeData
        {
            public static readonly byte STATE_NORMAL = 0;
            public static readonly byte STATE_DEAD = 1;
            public static readonly byte STATE_LOGOR = 2;
            public static readonly byte STATE_DEAD_PROCESS = 3;
            public int pos;
            public Node jumpDestination;
            public LocalStack stack;
            public byte state;
            public List<object> origins;
            public NodeData()
            {
                this.pos = -1;
                this.jumpDestination = null;
                this.stack = null;
                this.state = 0;
            }

            public NodeData(int pos)
            {
                this.jumpDestination = null;
                this.pos = pos;
                this.stack = null;
                this.state = 0;
            }

            public virtual void AddOrigin(Node origin)
            {
                if (this.origins == null)
                {
                    this.origins = new List<object>();
                }

                this.origins.Add(origin);
            }

            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/NodeAnalysisData.java:213-217
            // Original: public void close() { ... }
            public virtual void Close()
            {
                this.jumpDestination = null;
                this.stack = null;
                this.origins = null;
            }
        }
    }
}




