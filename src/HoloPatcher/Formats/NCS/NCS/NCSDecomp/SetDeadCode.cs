// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SetDeadCode.java:24-203
// Original: public class SetDeadCode extends PrunedDepthFirstAdapter
using System.Collections.Generic;
using KOTORModSync.Formats.NCS.NCSDecomp.Analysis;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;

namespace KOTORModSync.Formats.NCS.NCSDecomp.Utils
{
    public class SetDeadCode : PrunedDepthFirstAdapter
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SetDeadCode.java:25-28
        // Original: private static final byte STATE_NORMAL = 0; private static final byte STATE_JZ1_CP = 1; private static final byte STATE_JZ2_JZ = 2; private static final byte STATE_JZ3_CP2 = 3;
        private const byte STATE_NORMAL = 0;
        private const byte STATE_JZ1_CP = 1;
        private const byte STATE_JZ2_JZ = 2;
        private const byte STATE_JZ3_CP2 = 3;
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SetDeadCode.java:29-35
        // Original: private NodeAnalysisData nodedata; private SubroutineAnalysisData subdata; private int actionarg; private Hashtable<Node, ArrayList<Node>> origins; private Hashtable<Node, ArrayList<Node>> deadorigins; private byte deadstate; private byte state;
        private NodeAnalysisData nodedata;
        private SubroutineAnalysisData subdata;
        private int actionarg;
        private Dictionary<object, object> origins;
        private Dictionary<object, object> deadorigins;
        private byte deadstate;
        private byte state;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SetDeadCode.java:37-45
        // Original: public SetDeadCode(NodeAnalysisData nodedata, SubroutineAnalysisData subdata, Hashtable<Node, ArrayList<Node>> origins) { this.nodedata = nodedata; this.origins = origins; this.subdata = subdata; this.actionarg = 0; this.deadstate = 0; this.state = 0; this.deadorigins = new Hashtable<Node, ArrayList<Node>>(1); }
        public SetDeadCode(NodeAnalysisData nodedata, SubroutineAnalysisData subdata, Dictionary<object, object> origins)
        {
            this.nodedata = nodedata;
            this.origins = origins;
            this.subdata = subdata;
            this.actionarg = 0;
            this.deadstate = 0;
            this.state = 0;
            this.deadorigins = new Dictionary<object, object>();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SetDeadCode.java:47-52
        // Original: public void done() { this.nodedata = null; this.subdata = null; this.origins = null; this.deadorigins = null; }
        public virtual void Done()
        {
            this.nodedata = null;
            this.subdata = null;
            this.origins = null;
            this.deadorigins = null;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SetDeadCode.java:54-69
        // Original: @Override public void defaultIn(Node node) { ... }
        public override void DefaultIn(Node node)
        {
            if (this.actionarg > 0 && this.origins.ContainsKey(node))
            {
                --this.actionarg;
            }

            if (this.origins.ContainsKey(node))
            {
                this.deadstate = 0;
            }
            else if (this.deadorigins.ContainsKey(node))
            {
                this.deadstate = 3;
            }

            if (NodeUtils.IsCommandNode(node))
            {
                this.nodedata.SetCodeState(node, this.deadstate);
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SetDeadCode.java:71-76
        // Original: @Override public void defaultOut(Node node) { ... }
        public override void DefaultOut(Node node)
        {
            if (NodeUtils.IsCommandNode(node))
            {
                this.state = 0;
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SetDeadCode.java:78-98
        // Original: @Override public void outAConditionalJumpCommand(AConditionalJumpCommand node) { ... }
        public override void OutAConditionalJumpCommand(AConditionalJumpCommand node)
        {
            if (this.deadstate == 1)
            {
                this.RemoveDestination(node, this.nodedata.GetDestination(node));
            }
            else if (this.deadstate == 3)
            {
                this.TransferDestination(node, this.nodedata.GetDestination(node));
            }

            if (NodeUtils.IsJz(node))
            {
                if (this.state == 1)
                {
                    this.state++;
                    return;
                }

                if (this.state == 3)
                {
                    this.nodedata.LogOrCode(node, true);
                }
            }

            this.state = 0;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SetDeadCode.java:101-113
        // Original: @Override public void outACopyTopSpCommand(ACopyTopSpCommand node) { if (this.state != 0 && this.state != 2) { this.state = 0; } else { ... } }
        public override void OutACopyTopSpCommand(ACopyTopSpCommand node)
        {
            if (this.state != 0 && this.state != 2)
            {
                this.state = 0;
            }
            else
            {
                int copy = NodeUtils.StackSizeToPos(node.GetSize());
                int loc = NodeUtils.StackOffsetToPos(node.GetOffset());
                if (copy == 1 && loc == 1)
                {
                    this.state++;
                }
                else
                {
                    this.state = 0;
                }
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SetDeadCode.java:115-128
        // Original: @Override public void outAJumpCommand(AJumpCommand node) { ... }
        public override void OutAJumpCommand(AJumpCommand node)
        {
            if (this.deadstate == 1)
            {
                this.RemoveDestination(node, this.nodedata.GetDestination(node));
            }
            else if (this.deadstate == 3)
            {
                this.TransferDestination(node, this.nodedata.GetDestination(node));
            }

            // CRITICAL FOR ROUNDTRIP FIDELITY: Don't mark cleanup code after return JMPs as dead code
            // The external compiler adds cleanup code (MOVSP+RETN) after return JMPs
            // We need to preserve this cleanup code even though it's unreachable
            bool isReturnJmp = this.IsJumpToReturn(node);
            if (this.actionarg == 0 && !isReturnJmp)
            {
                this.deadstate = 3;
            }
            // For return JMPs, we keep deadstate as-is (don't mark subsequent code as dead)
            // This allows cleanup code after return JMPs to be preserved

            this.DefaultOut(node);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SetDeadCode.java:130-134
        // Original: @Override public void outAStoreStateCommand(AStoreStateCommand node) { this.actionarg++; this.defaultOut(node); }
        public override void OutAStoreStateCommand(AStoreStateCommand node)
        {
            ++this.actionarg;
            this.DefaultOut(node);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SetDeadCode.java:136-139
        // Original: public boolean isJumpToReturn(AJumpCommand node) { Node dest = this.nodedata.getDestination(node); return AReturn.class.isInstance(dest); }
        public virtual bool IsJumpToReturn(AJumpCommand node)
        {
            Node dest = this.nodedata.GetDestination(node);
            return typeof(AReturn).IsInstanceOfType(dest);
        }

        private void RemoveDestination(Node origin, Node destination)
        {
            this.RemoveDestination(origin, destination, this.origins);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SetDeadCode.java:145-153
        // Original: private void removeDestination(Node origin, Node destination, Hashtable<Node, ArrayList<Node>> hash)
        private void RemoveDestination(Node origin, Node destination, Dictionary<object, object> hash)
        {
            object originListObj = hash.ContainsKey(destination) ? hash[destination] : null;
            List<object> originList = originListObj as List<object>;
            if (originList != null)
            {
                originList.Remove(origin);
                if (originList.Count == 0)
                {
                    hash.Remove(destination);
                }
            }
        }

        private void TransferDestination(Node origin, Node destination)
        {
            this.RemoveDestination(origin, destination, this.origins);
            this.AddDestination(origin, destination, this.deadorigins);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SetDeadCode.java:160-168
        // Original: private void addDestination(Node origin, Node destination, Hashtable<Node, ArrayList<Node>> hash)
        private void AddDestination(Node origin, Node destination, Dictionary<object, object> hash)
        {
            object originsListObj = hash.ContainsKey(destination) ? hash[destination] : null;
            List<object> originsList = originsListObj as List<object>;
            if (originsList == null)
            {
                originsList = new List<object>(1);
                hash[destination] = originsList;
            }

            originsList.Add(origin);
        }
    }
}




