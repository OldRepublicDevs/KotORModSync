namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class AActionCommand : PActionCommand
    {
        private TAction _action;
        private TIntegerConstant _pos;
        private TIntegerConstant _type;
        private TIntegerConstant _id;
        private TIntegerConstant _argCount;
        private TSemi _semi;

        public AActionCommand()
        {
        }

        public AActionCommand(TAction action, TIntegerConstant pos, TIntegerConstant type, TIntegerConstant id, TIntegerConstant argCount, TSemi semi)
        {
            SetAction(action);
            SetPos(pos);
            SetType(type);
            SetId(id);
            SetArgCount(argCount);
            SetSemi(semi);
        }

        public override object Clone()
        {
            return new AActionCommand(
                (TAction)CloneNode(_action),
                (TIntegerConstant)CloneNode(_pos),
                (TIntegerConstant)CloneNode(_type),
                (TIntegerConstant)CloneNode(_id),
                (TIntegerConstant)CloneNode(_argCount),
                (TSemi)CloneNode(_semi));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/AActionCommand.java:53-56
            // Original: @Override public void apply(Switch sw) { ((Analysis)sw).caseAActionCommand(this); }
            string debugMsg = "DEBUG AActionCommand.Apply(AnalysisAdapter): ENTERED, sw type=" + sw.GetType().Name + ", is IAnalysis=" + (sw is Analysis.IAnalysis);
            try { System.IO.File.AppendAllText("debug_ast_traversal.txt", debugMsg + "\n"); } catch { }
            if (sw is Analysis.IAnalysis analysis)
            {
                debugMsg = "DEBUG AActionCommand.Apply: calling CaseAActionCommand";
                try { System.IO.File.AppendAllText("debug_ast_traversal.txt", debugMsg + "\n"); } catch { }
                analysis.CaseAActionCommand(this);
                debugMsg = "DEBUG AActionCommand.Apply: CaseAActionCommand returned";
                try { System.IO.File.AppendAllText("debug_ast_traversal.txt", debugMsg + "\n"); } catch { }
            }
            else
            {
                debugMsg = "DEBUG AActionCommand.Apply: routing to DefaultIn";
                try { System.IO.File.AppendAllText("debug_ast_traversal.txt", debugMsg + "\n"); } catch { }
                sw.DefaultIn(this);
            }
        }

        public TAction GetAction()
        {
            return _action;
        }

        public void SetAction(TAction node)
        {
            if (_action != null)
            {
                _action.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _action = node;
        }

        public TIntegerConstant GetPos()
        {
            return _pos;
        }

        public void SetPos(TIntegerConstant node)
        {
            if (_pos != null)
            {
                _pos.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _pos = node;
        }

        public new TIntegerConstant GetType()
        {
            return _type;
        }

        public void SetType(TIntegerConstant node)
        {
            if (_type != null)
            {
                _type.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _type = node;
        }

        public TIntegerConstant GetId()
        {
            return _id;
        }

        public void SetId(TIntegerConstant node)
        {
            if (_id != null)
            {
                _id.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _id = node;
        }

        public TIntegerConstant GetArgCount()
        {
            return _argCount;
        }

        public void SetArgCount(TIntegerConstant node)
        {
            if (_argCount != null)
            {
                _argCount.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _argCount = node;
        }

        public TSemi GetSemi()
        {
            return _semi;
        }

        public void SetSemi(TSemi node)
        {
            if (_semi != null)
            {
                _semi.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _semi = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_action == child)
            {
                _action = null;
                return;
            }
            if (_pos == child)
            {
                _pos = null;
                return;
            }
            if (_type == child)
            {
                _type = null;
                return;
            }
            if (_id == child)
            {
                _id = null;
                return;
            }
            if (_argCount == child)
            {
                _argCount = null;
                return;
            }
            if (_semi == child)
            {
                _semi = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_action == oldChild)
            {
                SetAction((TAction)newChild);
                return;
            }
            if (_pos == oldChild)
            {
                SetPos((TIntegerConstant)newChild);
                return;
            }
            if (_type == oldChild)
            {
                SetType((TIntegerConstant)newChild);
                return;
            }
            if (_id == oldChild)
            {
                SetId((TIntegerConstant)newChild);
                return;
            }
            if (_argCount == oldChild)
            {
                SetArgCount((TIntegerConstant)newChild);
                return;
            }
            if (_semi == oldChild)
            {
                SetSemi((TSemi)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_action) + ToString(_pos) + ToString(_type) + ToString(_id) + ToString(_argCount) + ToString(_semi);
        }
    }
}





