namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class AReturnCmd : PCmd
    {
        private PReturn _return;

        public AReturnCmd()
        {
        }

        public AReturnCmd(PReturn returnNode)
        {
            SetReturn(returnNode);
        }

        public override object Clone()
        {
            return new AReturnCmd((PReturn)CloneNode(_return));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public PReturn GetReturn()
        {
            return _return;
        }

        public void SetReturn(PReturn node)
        {
            if (_return != null)
            {
                _return.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _return = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_return == child)
            {
                _return = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_return == oldChild)
            {
                SetReturn((PReturn)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_return);
        }
    }
}





