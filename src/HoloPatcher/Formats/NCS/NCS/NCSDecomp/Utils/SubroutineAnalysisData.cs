// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineAnalysisData.java:23-569
// Original: public class SubroutineAnalysisData
using System;
using static KOTORModSync.Formats.NCS.NCSDecomp.DecompilerLogger;
using System.Collections;
using System.Collections.Generic;
using KOTORModSync.Formats.NCS.NCSDecomp;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;
using KOTORModSync.Formats.NCS.NCSDecomp.Scriptutils;
using KOTORModSync.Formats.NCS.NCSDecomp.Stack;
using IEnumerator = KOTORModSync.Formats.NCS.NCSDecomp.IEnumerator<object>;
using IAnalysis = KOTORModSync.Formats.NCS.NCSDecomp.Analysis.IAnalysis;

namespace KOTORModSync.Formats.NCS.NCSDecomp.Utils
{
    public class SubroutineAnalysisData
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineAnalysisData.java:24-31
        // Original: private NodeAnalysisData nodedata; private LinkedHashMap<Integer, ASubroutine> subroutines; private Hashtable<Node, SubroutineState> substates; private ASubroutine mainsub; private ASubroutine globalsub; private LocalVarStack globalstack; private ArrayList<StructType> globalstructs; private SubScriptState globalstate;
        private NodeAnalysisData nodedata;
        private Dictionary<object, object> subroutines;
        private Dictionary<object, object> substates;
        private ASubroutine mainsub;
        private ASubroutine globalsub;
        private LocalVarStack globalstack;
        private List<object> globalstructs;
        private SubScriptState globalstate;
        //private bool result;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineAnalysisData.java:33-41
        // Original: public SubroutineAnalysisData(NodeAnalysisData nodedata) { ... }
        public SubroutineAnalysisData(NodeAnalysisData nodedata)
        {
            this.nodedata = nodedata;
            this.subroutines = new Dictionary<object, object>();
            this.substates = new Dictionary<object, object>();
            this.globalsub = null;
            this.globalstack = null;
            this.mainsub = null;
            this.globalstructs = new List<object>();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineAnalysisData.java:43-60
        // Original: public void parseDone() { this.nodedata = null; if (this.substates != null) { Enumeration<SubroutineState> subs = this.substates.elements(); while (subs.hasMoreElements()) { subs.nextElement().parseDone(); } subs = null; this.substates = null; } this.subroutines = null; this.mainsub = null; this.globalsub = null; this.globalstate = null; }
        public void ParseDone()
        {
            this.nodedata = null;
            if (this.substates != null)
            {
                foreach (object sub in this.substates.Values)
                {
                    ((SubroutineState)sub).ParseDone();
                }
                this.substates = null;
            }
            this.subroutines = null;
            this.mainsub = null;
            this.globalsub = null;
            this.globalstate = null;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineAnalysisData.java:62-105
        // Original: public void close()
        public void Close()
        {
            if (this.nodedata != null)
            {
                this.nodedata.Close();
                this.nodedata = null;
            }

            if (this.substates != null)
            {
                foreach (object sub in this.substates.Values)
                {
                    ((SubroutineState)sub).Close();
                }

                this.substates = null;
            }

            if (this.subroutines != null)
            {
                this.subroutines.Clear();
                this.subroutines = null;
            }

            this.mainsub = null;
            this.globalsub = null;
            if (this.globalstack != null)
            {
                this.globalstack.Close();
                this.globalstack = null;
            }

            if (this.globalstructs != null)
            {
                foreach (object item in this.globalstructs)
                {
                    if (item is StructType structType)
                    {
                        structType.Close();
                    }
                }

                this.globalstructs = null;
            }

            if (this.globalstate != null)
            {
                this.globalstate.Close();
                this.globalstate = null;
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineAnalysisData.java:107-116
        // Original: public void printStates() { Enumeration<Node> subnodes = this.substates.keys(); while (subnodes.hasMoreElements()) { ... } }
        public void PrintStates()
        {
            IEnumerator<object> subnodes = new DictionaryKeyEnumeratorAdapter(this.substates);
            while (subnodes.HasNext())
            {
                Node node = (Node)subnodes.Next();
                SubroutineState state = (SubroutineState)this.substates[node];
                Console.WriteLine("Printing state for subroutine at " + this.nodedata.GetPos(node).ToString());
                state.PrintState();
            }
        }

        public SubScriptState GlobalState()
        {
            return this.globalstate;
        }

        public void GlobalState(SubScriptState globalstate)
        {
            this.globalstate = globalstate;
        }

        public ASubroutine GetGlobalsSub()
        {
            return this.globalsub;
        }

        public void SetGlobalsSub(ASubroutine globalsub)
        {
            this.globalsub = globalsub;
        }

        public ASubroutine GetMainSub()
        {
            return this.mainsub;
        }

        public void SetMainSub(ASubroutine mainsub)
        {
            this.mainsub = mainsub;
        }

        public LocalVarStack GetGlobalStack()
        {
            return this.globalstack;
        }

        public void SetGlobalStack(LocalVarStack stack)
        {
            this.globalstack = stack;
        }

        public int NumSubs()
        {
            return this.subroutines.Count;
        }

        public int CountSubsDone()
        {
            IEnumerator<object> subs = new DictionaryValueEnumeratorAdapter(this.substates);
            int count = 0;
            while (subs.HasNext())
            {
                if (((SubroutineState)subs.Next()).IsTotallyPrototyped())
                {
                    ++count;
                }
            }
            return count;
        }

        public SubroutineState GetState(Node sub)
        {
            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineAnalysisData.java:188-192
            // Original: public SubroutineState getState(Node sub) { return this.substates.get(sub); }
            // Use TryGetValue to avoid KeyNotFoundException if state wasn't added
            if (this.substates.TryGetValue(sub, out object stateObj))
            {
                return (SubroutineState)stateObj;
            }
            return null;
        }

        public bool IsPrototyped(int pos, bool nullok)
        {
            object subObj;
            if (!this.subroutines.TryGetValue(pos, out subObj))
            {
                if (nullok)
                {
                    return false;
                }
                throw new Exception("Checking prototype on a subroutine not in the hash");
            }
            Node sub = (Node)subObj;
            if (sub != null)
            {
                SubroutineState state = (SubroutineState)this.substates[sub];
                return state != null && state.IsPrototyped();
            }
            if (nullok)
            {
                return false;
            }
            throw new Exception("Checking prototype on a subroutine not in the hash");
        }

        public bool IsBeingPrototyped(int pos)
        {
            object subObj;
            if (!this.subroutines.TryGetValue(pos, out subObj))
            {
                throw new Exception("Checking prototype on a subroutine not in the hash");
            }
            Node sub = (Node)subObj;
            if (sub == null)
            {
                throw new Exception("Checking prototype on a subroutine not in the hash");
            }
            SubroutineState state = (SubroutineState)this.substates[sub];
            return state != null && state.IsBeingPrototyped();
        }

        public bool IsFullyPrototyped(int pos)
        {
            object subObj;
            if (!this.subroutines.TryGetValue(pos, out subObj))
            {
                throw new Exception("Checking prototype on a subroutine not in the hash");
            }
            Node sub = (Node)subObj;
            if (sub == null)
            {
                throw new Exception("Checking prototype on a subroutine not in the hash");
            }
            SubroutineState state = (SubroutineState)this.substates[sub];
            return state != null && state.IsTotallyPrototyped();
        }

        public void AddStruct(StructType structType)
        {
            if (!this.globalstructs.Contains(structType))
            {
                this.globalstructs.Add(structType);
                structType.TypeName("structtype" + this.globalstructs.Count);
            }
        }

        public void AddStruct(VarStruct structVar)
        {
            StructType structtype = structVar.StructType();
            if (!this.globalstructs.Contains(structtype))
            {
                this.globalstructs.Add(structtype);
                structtype.TypeName("structtype" + this.globalstructs.Count);
            }
            else
            {
                structVar.StructType(this.GetStructPrototype(structtype));
            }
        }

        public string GetStructDeclarations()
        {
            string newline = Environment.NewLine;
            System.Text.StringBuilder buff = new System.Text.StringBuilder();
            for (int i = 0; i < this.globalstructs.Count; ++i)
            {
                StructType structtype = (StructType)this.globalstructs[i];
                if (!structtype.IsVector())
                {
                    buff.Append(structtype.ToDeclString() + " {" + newline);
                    List<object> types = structtype.Types();
                    for (int j = 0; j < types.Count; ++j)
                    {
                        buff.Append("\t" + ((Type)types[j]).ToDeclString() + " " + structtype.ElementName(j) + ";" + newline);
                    }
                    buff.Append("};" + newline + newline);
                    types = null;
                }
            }
            return buff.ToString();
        }

        public string GetStructTypeName(StructType structtype)
        {
            StructType protostruct = this.GetStructPrototype(structtype);
            return protostruct.TypeName();
        }

        public StructType GetStructPrototype(StructType structtype)
        {
            int index = this.globalstructs.IndexOf(structtype);
            if (index == -1)
            {
                this.globalstructs.Add(structtype);
                index = this.globalstructs.Count - 1;
            }
            return (StructType)this.globalstructs[index];
        }

        private void AddSubroutine(int pos, Node node, byte id)
        {
            this.subroutines[pos] = node;
            // Ensure the position is set in nodedata for the subroutine node
            if (pos >= 0)
            {
                this.nodedata.SetPos(node, pos);
            }
            this.AddSubState(node, id);
        }

        private void AddSubState(Node sub, byte id)
        {
            SubroutineState state = new SubroutineState(this.nodedata, sub, id);
            this.substates[sub] = state;
        }

        private void AddSubState(Node sub, byte id, Type type)
        {
            SubroutineState state = new SubroutineState(this.nodedata, sub, id);
            state.SetReturnType(type, 1);
            this.substates[sub] = state;
        }

        private void AddMain(ASubroutine sub, bool conditional)
        {
            this.mainsub = sub;
            // Ensure main subroutine has position set (main is always at position 0)
            int mainPos = this.nodedata.TryGetPos(this.mainsub);
            if (mainPos < 0)
            {
                // Try to get position from first command
                try
                {
                    Node firstCmd = NodeUtils.GetCommandChild(this.mainsub);
                    if (firstCmd != null)
                    {
                        mainPos = this.nodedata.TryGetPos(firstCmd);
                    }
                }
                catch (Exception ex)
                {
                    Debug("Warning: Could not get first command for main subroutine: " + ex.Message);
                    // Continue with default position
                }
                // If still no position, set to 0 (main is always at position 0)
                if (mainPos < 0)
                {
                    mainPos = 0;
                }
                this.nodedata.SetPos(this.mainsub, mainPos);
            }
            // Always add the state, even if GetCommandChild failed
            if (conditional)
            {
                this.AddSubState(this.mainsub, (byte)0, new Type((byte)3));
            }
            else
            {
                this.AddSubState(this.mainsub, (byte)0);
            }
        }

        private void AddGlobals(ASubroutine sub)
        {
            this.globalsub = sub;
        }

        public IEnumerator<object> GetSubroutines()
        {
            List<object> subs = new List<object>();
            SortedSet<int> keys = new SortedSet<int>(Comparer<int>.Create((x, y) => y.CompareTo(x)));
            foreach (int key in this.subroutines.Keys)
            {
                keys.Add(key);
            }
            foreach (int key in keys)
            {
                subs.Add(this.subroutines[key]);
            }
            return new ListEnumeratorAdapter(subs);
        }

        private class ListEnumeratorAdapter : IEnumerator<object>
        {
            private readonly List<object> _list;
            private int _index;
            private object _current;

            public ListEnumeratorAdapter(List<object> list)
            {
                _list = list;
                _index = 0;
                _current = null;
            }

            public bool HasNext()
            {
                return _index < _list.Count;
            }

            public object Next()
            {
                if (!HasNext())
                    throw new InvalidOperationException("No next element");
                _current = _list[_index++];
                return _current;
            }

            // IEnumerator<object> implementation
            public object Current => _current;
            object System.Collections.IEnumerator.Current => Current;
            public bool MoveNext()
            {
                if (!HasNext()) return false;
                Next();
                return true;
            }
            public void Reset() { _index = 0; _current = null; }
            public void Dispose() { }
        }

        private class DictionaryKeyEnumeratorAdapter : IEnumerator<object>
        {
            private readonly List<object> _keys;
            private int _index;
            private object _current;

            public DictionaryKeyEnumeratorAdapter(Dictionary<object, object> dictionary)
            {
                _keys = new List<object>(dictionary.Keys);
                _index = 0;
                _current = null;
            }

            public bool HasNext()
            {
                return _index < _keys.Count;
            }

            public object Next()
            {
                if (!HasNext())
                    throw new InvalidOperationException("No next element");
                _current = _keys[_index++];
                return _current;
            }

            // IEnumerator<object> implementation
            public object Current => _current;
            object System.Collections.IEnumerator.Current => Current;
            public bool MoveNext()
            {
                if (!HasNext()) return false;
                Next();
                return true;
            }
            public void Reset() { _index = 0; _current = null; }
            public void Dispose() { }
        }

        private class DictionaryValueEnumeratorAdapter : IEnumerator<object>
        {
            private readonly List<object> _values;
            private int _index;
            private object _current;

            public DictionaryValueEnumeratorAdapter(Dictionary<object, object> dictionary)
            {
                _values = new List<object>(dictionary.Values);
                _index = 0;
                _current = null;
            }

            public bool HasNext()
            {
                return _index < _values.Count;
            }

            public object Next()
            {
                if (!HasNext())
                    throw new InvalidOperationException("No next element");
                _current = _values[_index++];
                return _current;
            }

            // IEnumerator<object> implementation
            public object Current => _current;
            object System.Collections.IEnumerator.Current => Current;
            public bool MoveNext()
            {
                if (!HasNext()) return false;
                Next();
                return true;
            }
            public void Reset() { _index = 0; _current = null; }
            public void Dispose() { }
        }

        public void SplitOffSubroutines(Node ast)
        {
            Start rootStart = ast as Start;
            if (rootStart == null)
            {
                Debug("WARNING SplitOffSubroutines: AST is not a Start node, type=" + (ast != null ? ast.GetType().Name : "null"));
                return;
            }

            PProgram pProgram = rootStart.GetPProgram();
            if (pProgram == null)
            {
                Debug("WARNING SplitOffSubroutines: GetPProgram() returned null");
                return;
            }

            AProgram aProgram = pProgram as AProgram;
            if (aProgram == null)
            {
                Debug("WARNING SplitOffSubroutines: PProgram is not an AProgram, type=" + pProgram.GetType().Name);
                return;
            }

            // Try to detect conditional first from bytecode structure
            bool conditional = NodeUtils.IsConditionalProgram(rootStart);
            var subList = aProgram.GetSubroutine();
            Debug($"DEBUG SplitOffSubroutines: AST program has {subList.Count} subroutines");
            TypedLinkedList subroutines = new TypedLinkedList();
            foreach (var sub in subList)
            {
                subroutines.Add(sub);
            }

            // Ensure we have at least one subroutine (the main)
            if (subroutines.Count == 0)
            {
                Debug("WARNING: No subroutines found in AST - cannot decompile.");
                Debug("DEBUG SplitOffSubroutines: subList.Count=" + subList.Count + ", subroutines.Count=" + subroutines.Count);
                return;
            }

            ASubroutine node = (ASubroutine)subroutines.RemoveFirst();
            // Check if first subroutine is globals - only separate it if there are other subroutines
            if (subroutines.Count > 0 && this.IsGlobalsSub(node))
            {
                this.AddGlobals(node);
                node = (ASubroutine)subroutines.RemoveFirst();
            }
            
            // Use return type stored in ASubroutine if available (type 3 = int = StartingConditional)
            // This is more reliable than IsConditionalProgram which analyzes bytecode structure
            byte returnType = node.GetReturnType();
            if (returnType == 3)
            {
                conditional = true;
                Debug($"DEBUG SplitOffSubroutines: Main subroutine has return type {returnType} (int) - marking as StartingConditional");
            }
            else if (returnType != 0)
            {
                Debug($"DEBUG SplitOffSubroutines: Main subroutine has return type {returnType} - using IsConditionalProgram result: {conditional}");
            }
            
            // Always add the first (or second if globals was first) subroutine as main
            this.AddMain(node, conditional);
            byte id = 1;
            while (subroutines.Count > 0)
            {
                node = (ASubroutine)subroutines.RemoveFirst();
                int pos = this.nodedata.TryGetPos(node);
                // If the subroutine doesn't have a position set, try to get it from the first command
                if (pos < 0)
                {
                    Node firstCmd = NodeUtils.GetCommandChild(node);
                    if (firstCmd != null)
                    {
                        pos = this.nodedata.TryGetPos(firstCmd);
                        // If we got a position from the first command, set it on the subroutine node too
                        if (pos >= 0)
                        {
                            this.nodedata.SetPos(node, pos);
                        }
                        else
                        {
                            // Fallback: try GetCommandPos if position wasn't set yet
                            int cmdPos = NodeUtils.GetCommandPos(firstCmd);
                            if (cmdPos >= 0)
                            {
                                pos = cmdPos;
                                this.nodedata.SetPos(node, pos);
                            }
                        }
                    }
                }
                // Matching Java implementation: always add subroutine, even if position is -1 or 0
                // The position will be used for indexing, but we shouldn't skip subroutines
                // Note: position 0 is the main subroutine, which is already added, but we still process others
                this.AddSubroutine(pos, node, id++);
            }
            subroutines = null;
            node = null;
        }

        private bool IsGlobalsSub(ASubroutine node)
        {
            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineAnalysisData.java:310-314
            // Original: CheckIsGlobals cig = new CheckIsGlobals(); node.apply(cig); return cig.getIsGlobals();
            CheckIsGlobals cig = new CheckIsGlobals();
            node.Apply(cig);
            return cig.GetIsGlobals();
        }

    }
}




