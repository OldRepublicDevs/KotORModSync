// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:22-480
// Original: public class SubroutineState
using System;
using static KOTORModSync.Formats.NCS.NCSDecomp.DecompilerLogger;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;
using KOTORModSync.Formats.NCS.NCSDecomp.Stack;

using UtilsType = KOTORModSync.Formats.NCS.NCSDecomp.Utils.Type;

namespace KOTORModSync.Formats.NCS.NCSDecomp.Utils
{
    public class SubroutineState
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:23-38
        // Original: private static final byte PROTO_NO = 0; private static final byte PROTO_IN_PROGRESS = 1; private static final byte PROTO_DONE = 2; protected static final byte JUMP_YES = 0; protected static final byte JUMP_NO = 1; protected static final byte JUMP_NA = 2; private Type type; private ArrayList<Type> params; private int returndepth; private Node root; private int paramsize; private boolean paramstyped; private byte status; private NodeAnalysisData nodedata; private LinkedList<DecisionData> decisionqueue; private byte id;
        private static readonly byte PROTO_NO = 0;
        private static readonly byte PROTO_IN_PROGRESS = 1;
        private static readonly byte PROTO_DONE = 2;
        protected static readonly byte JUMP_YES = 0;
        protected static readonly byte JUMP_NO = 1;
        protected static readonly byte JUMP_NA = 2;
        private Type type;
        private List<Type> @params;
        private int returndepth;
        private Node root;
        private int paramsize;
        private bool paramstyped;
        private byte status;
        private NodeAnalysisData nodedata;
        private LinkedList decisionqueue;
        private byte id;
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:40-50
        // Original: public SubroutineState(NodeAnalysisData nodedata, Node root, byte id) { this.status = 0; ... }
        public SubroutineState(NodeAnalysisData nodedata, Node root, byte id)
        {
            this.nodedata = nodedata;
            this.@params = new List<Type>();
            this.decisionqueue = new LinkedList();
            this.paramstyped = true;
            this.paramsize = 0;
            this.status = 0;
            this.type = new Type((byte)0);
            this.root = root;
            this.id = id;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:52-56
        // Original: public void parseDone() { this.root = null; this.nodedata = null; this.decisionqueue = null; }
        public virtual void ParseDone()
        {
            this.root = null;
            this.nodedata = null;
            this.decisionqueue = null;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:58-73
        // Original: public void close() { this.params = null; this.root = null; this.nodedata = null; if (this.decisionqueue != null) { Iterator<DecisionData> it = this.decisionqueue.iterator(); while (it.hasNext()) { it.next().close(); } this.decisionqueue = null; } this.type = null; }
        public virtual void Close()
        {
            this.@params = null;
            this.root = null;
            this.nodedata = null;
            if (this.decisionqueue != null)
            {
                IEnumerator<object> it = this.decisionqueue.Iterator();

                while (it.HasNext())
                {
                    DecisionData data = (DecisionData)it.Next();
                    data.Close();
                }

                this.decisionqueue = null;
            }

            this.type = null;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:75-88
        // Original: public void printState() { System.out.println("Return type is " + this.type); System.out.println("There are " + Integer.toString(this.paramsize) + " parameters"); if (this.paramsize > 0) { StringBuffer buff = new StringBuffer(); buff.append(" Types: "); for (Type paramType : this.params) { buff.append(paramType + " "); } System.out.println(buff); } }
        public virtual void PrintState()
        {
            Debug("Return type is " + this.type);
            Debug("There are " + this.paramsize.ToString() + " parameters");
            if (this.paramsize > 0)
            {
                StringBuilder buff = new StringBuilder();
                buff.Append(" Types: ");

                foreach (Type paramType in this.@params)
                {
                    buff.Append(paramType + " ");
                }

                Debug(buff.ToString());
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:90-109
        // Original: public void printDecisions() { ... }
        public virtual void PrintDecisions()
        {
            Debug("-----------------------------");
            Debug("Jump Decisions");

            for (int i = 0; i < this.decisionqueue.Count; i++)
            {
                DecisionData data = (DecisionData)this.decisionqueue[i];
                string str = "  (" + (i + 1).ToString();
                str = str + ") at pos " + this.nodedata.GetPos(data.decisionnode).ToString();
                if (data.decision == 0)
                {
                    str = str + " do optional jump to ";
                }
                else if (data.decision == 2)
                {
                    str = str + " do required jump to ";
                }
                else
                {
                    str = str + " do not jump to ";
                }

                str = str + data.destination.ToString();
                Debug(str);
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:111-130
        // Original: public String toString(boolean main) { ... buff.append("sub" + Byte.toString(this.id) + "("); ... buff.append(link + ptype.toDeclString() + " param" + Integer.toString(i)); ... }
        public virtual string ToString(bool main)
        {
            StringBuilder buff = new StringBuilder();
            buff.Append(this.type + " ");
            if (main)
            {
                buff.Append("main(");
            }
            else
            {
                buff.Append("sub" + this.id.ToString() + "(");
            }

            string link = "";

            for (int i = 0; i < this.paramsize; i++)
            {
                Type ptype = this.@params[i];
                buff.Append(link + ptype.ToDeclString() + " param" + i.ToString());
                link = ", ";
            }

            buff.Append(")");
            return buff.ToString();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:132-134
        // Original: public void startPrototyping() { this.status = 1; }
        public virtual void StartPrototyping()
        {
            this.status = PROTO_IN_PROGRESS;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:136-143
        // Original: public void stopPrototyping(boolean success) { if (success) { this.status = 2; this.decisionqueue = null; } else { this.status = 0; } }
        public virtual void StopPrototyping(bool success)
        {
            if (success)
            {
                this.status = PROTO_DONE;
                this.decisionqueue = null;
            }
            else
            {
                this.status = PROTO_NO;
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:145-147
        // Original: public boolean isPrototyped() { return this.status == 2; }
        public virtual bool IsPrototyped()
        {
            return this.status == PROTO_DONE;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:149-151
        // Original: public boolean isBeingPrototyped() { return this.status == 1; }
        public virtual bool IsBeingPrototyped()
        {
            return this.status == PROTO_IN_PROGRESS;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:153-155
        // Original: public boolean isTotallyPrototyped() { return this.status == 2 && this.params.size() >= this.paramsize; }
        public virtual bool IsTotallyPrototyped()
        {
            return this.status == PROTO_DONE && this.@params.Count >= this.paramsize;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:157-172
        // Original: public boolean getSkipStart(int pos) { if (this.decisionqueue != null && !this.decisionqueue.isEmpty()) { ... } else { return false; } }
        public virtual bool GetSkipStart(int pos)
        {
            if (this.decisionqueue != null && !this.decisionqueue.IsEmpty())
            {
                DecisionData decision = (DecisionData)this.decisionqueue.GetFirst();
                if (this.nodedata.GetPos(decision.decisionnode) == pos)
                {
                    if (decision.DoJump())
                    {
                        return true;
                    }

                    this.decisionqueue.RemoveFirst();
                }

                return false;
            }
            else
            {
                return false;
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:174-182
        // Original: public boolean getSkipEnd(int pos) { if (this.decisionqueue != null && !this.decisionqueue.isEmpty()) { if (this.decisionqueue.getFirst().destination == pos) { this.decisionqueue.removeFirst(); return true; } } return false; }
        public virtual bool GetSkipEnd(int pos)
        {
            if (this.decisionqueue != null && !this.decisionqueue.IsEmpty())
            {
                if (((DecisionData)this.decisionqueue.GetFirst()).destination == pos)
                {
                    this.decisionqueue.RemoveFirst();
                    return true;
                }
            }
            return false;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:184-193
        // Original: public void setParamCount(int params) { this.paramsize = params; if (params > 0) { this.paramstyped = false; if (this.returndepth <= params) { this.type = new Type((byte)0); } } this.ensureParamPlaceholders(); }
        public virtual void SetParamCount(int @params)
        {
            this.paramsize = @params;
            if (@params > 0)
            {
                this.paramstyped = false;
                if (this.returndepth <= @params)
                {
                    this.type = new Type((byte)0);
                }
            }
            this.EnsureParamPlaceholders();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:195-197
        // Original: public int getParamCount() { return this.paramsize; }
        public virtual int GetParamCount()
        {
            return this.paramsize;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:199-201
        // Original: public Type type() { return this.type; }
        public virtual Type Type()
        {
            return this.type;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:203-205
        // Original: public ArrayList<Type> params() { return this.params; }
        public virtual List<object> Params()
        {
            return new List<object>(this.@params.Cast<object>());
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:207-210
        // Original: public void setReturnType(Type type, int depth) { this.type = type; this.returndepth = depth; }
        public virtual void SetReturnType(Type type, int depth)
        {
            this.type = type;
            this.returndepth = depth;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:212-239
        // Original: public void updateParams(LinkedList<Type> types) { ... }
        public virtual void UpdateParams(LinkedList types)
        {
            new Type(unchecked((byte)(-1)));
            this.paramstyped = true;
            bool redo = this.@params.Count > 0;
            if (types.Count < this.paramsize)
            {
                while (types.Count < this.paramsize)
                {
                    types.AddFirst(new Type(unchecked((byte)(-1))));
                }
            }
            else if (types.Count > this.paramsize)
            {
                while (types.Count > this.paramsize)
                {
                    types.RemoveFirst();
                }
            }

            for (int i = 0; i < types.Count; i++)
            {
                Type newtype = (Type)types[i];

                if (redo && !this.@params[i].IsTyped())
                {
                    this.@params[i] = newtype;
                }
                else if (!redo)
                {
                    this.@params.Add(newtype);
                }

                if (!this.@params[i].IsTyped())
                {
                    this.paramstyped = false;
                }
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:251-253
        // Original: public Type getParamType(int pos) { return this.params.size() < pos ? new Type((byte)0) : this.params.get(pos - 1); }
        public virtual Type GetParamType(int pos)
        {
            return this.@params.Count < pos ? new Type((byte)0) : this.@params[pos - 1];
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:255-275
        // Original: public void initStack(LocalTypeStack stack) { ... if (!this.type.equals((byte)-15)) { stack.push(this.type); } else { ... } ... }
        public virtual void InitStack(LocalTypeStack stack)
        {
            if (this.IsPrototyped())
            {
                if (this.type.IsTyped() && !this.type.Equals((byte)0))
                {
                    if (!this.type.Equals(unchecked((byte)(-15))))
                    {
                        stack.Push(this.type);
                    }
                    else
                    {
                        List<object> structtypes = ((StructType)this.type).Types();
                        foreach (Type structtype in structtypes)
                        {
                            stack.Push(structtype);
                        }
                    }
                }

                if (this.paramsize == this.@params.Count)
                {
                    for (int i = 0; i < this.paramsize; i++)
                    {
                        stack.Push(this.@params[i]);
                    }
                }
                else
                {
                    for (int i = 0; i < this.paramsize; i++)
                    {
                        stack.Push(new Type(unchecked((byte)(-1))));
                    }
                }
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:280-298
        // Original: public void initStack(LocalVarStack stack) { ... }
        public virtual void InitStack(LocalVarStack stack)
        {
            if (!this.type.Equals((byte)0))
            {
                Variable retvar;
                if (typeof(StructType).IsInstanceOfType(this.type))
                {
                    retvar = new VarStruct((StructType)this.type);
                }
                else
                {
                    retvar = new Variable(this.type);
                }

                retvar.IsReturn(true);
                stack.Push(retvar);
            }

            for (int i = 0; i < this.paramsize; i++)
            {
                Variable paramvar = new Variable(this.@params[i]);
                paramvar.IsParam(true);
                stack.Push(paramvar);
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:241-249
        // Original: public void ensureParamPlaceholders() { while (this.params.size() < this.paramsize) { this.params.add(new Type(Type.VT_INTEGER)); } while (this.params.size() > this.paramsize) { this.params.remove(this.params.size() - 1); } }
        public virtual void EnsureParamPlaceholders()
        {
            while (this.@params.Count < this.paramsize)
            {
                this.@params.Add(new UtilsType(UtilsType.VT_INTEGER));
            }
            while (this.@params.Count > this.paramsize)
            {
                this.@params.RemoveAt(this.@params.Count - 1);
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:300-302
        // Original: public byte getId() { return this.id; }
        public virtual byte GetId()
        {
            return this.id;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:304-306
        // Original: public int getStart() { return this.nodedata.getPos(this.root); }
        public virtual int GetStart()
        {
            // Try to get position from the subroutine node itself
            int pos = this.nodedata.TryGetPos(this.root);
            if (pos >= 0)
            {
                return pos;
            }
            // If subroutine node doesn't have position, get it from the first command
            Node firstCmd = NodeUtils.GetCommandChild(this.root);
            if (firstCmd != null)
            {
                pos = this.nodedata.TryGetPos(firstCmd);
                if (pos >= 0)
                {
                    return pos;
                }
            }
            // Fallback: return 0 if no position found (shouldn't happen in normal cases)
            return 0;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:308-310
        // Original: public int getEnd() { return NodeUtils.getSubEnd((ASubroutine)this.root); }
        public virtual int GetEnd()
        {
            return NodeUtils.GetSubEnd((ASubroutine)this.root);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:312-318
        // Original: public void addDecision(Node node, int destination) { SubroutineState.DecisionData decision = new SubroutineState.DecisionData(node, destination, false); this.decisionqueue.addLast(decision); if (this.decisionqueue.size() > 3000) { throw new RuntimeException("Decision queue size over 3000 - probable infinite loop"); } }
        public virtual void AddDecision(Node node, int destination)
        {
            DecisionData decision = new DecisionData(node, destination, false);
            this.decisionqueue.AddLast(decision);
            if (this.decisionqueue.Count > 3000)
            {
                throw new Exception("Decision queue size over 3000 - probable infinite loop");
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:320-323
        // Original: public void addJump(Node node, int destination) { SubroutineState.DecisionData decision = new SubroutineState.DecisionData(node, destination, true); this.decisionqueue.addLast(decision); }
        public virtual void AddJump(Node node, int destination)
        {
            DecisionData decision = new DecisionData(node, destination, true);
            this.decisionqueue.AddLast(decision);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:325-331
        // Original: public int getCurrentDestination() { SubroutineState.DecisionData data = this.decisionqueue.getLast(); if (data == null) { throw new RuntimeException("Attempted to get a destination but no decision nodes found."); } return data.destination; }
        public virtual int GetCurrentDestination()
        {
            DecisionData data = (DecisionData)this.decisionqueue.GetLast();
            if (data == null)
            {
                throw new Exception("Attempted to get a destination but no decision nodes found.");
            }
            return data.destination;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:333-344
        // Original: public int switchDecision() { while (this.decisionqueue.size() > 0) { SubroutineState.DecisionData data = this.decisionqueue.getLast(); if (data.switchDecision()) { return data.destination; } this.decisionqueue.removeLast(); } return -1; }
        public virtual int SwitchDecision()
        {
            while (this.decisionqueue.Count > 0)
            {
                DecisionData data = (DecisionData)this.decisionqueue.GetLast();
                if (data.SwitchDecision())
                {
                    return data.destination;
                }

                this.decisionqueue.RemoveLast();
            }

            return -1;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:346-378
        // Original: private class DecisionData { ... }
        private class DecisionData
        {
            public Node decisionnode;
            public byte decision;
            public int destination;
            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:351-360
            // Original: public DecisionData(Node node, int destination, boolean forcejump) { if (forcejump) { this.decision = 2; } else { this.decision = 1; } this.decisionnode = node; this.destination = destination; }
            public DecisionData(Node node, int destination, bool forcejump)
            {
                if (forcejump)
                {
                    this.decision = 2;
                }
                else
                {
                    this.decision = 1;
                }

                this.decisionnode = node;
                this.destination = destination;
            }

            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:362-364
            // Original: public boolean doJump() { return this.decision != 1; }
            public virtual bool DoJump()
            {
                return this.decision != 1;
            }

            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:366-373
            // Original: public boolean switchDecision() { if (this.decision == 1) { this.decision = 0; return true; } else { return false; } }
            public virtual bool SwitchDecision()
            {
                if (this.decision == 1)
                {
                    this.decision = 0;
                    return true;
                }
                else
                {
                    return false;
                }
            }

            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:375-377
            // Original: public void close() { this.decisionnode = null; }
            public virtual void Close()
            {
                this.decisionnode = null;
            }
        }
    }
}




