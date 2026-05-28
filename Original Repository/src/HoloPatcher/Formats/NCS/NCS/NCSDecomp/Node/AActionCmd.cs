namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class AActionCmd : PCmd
    {
        private PActionCommand _actionCommand;
        public AActionCmd()
        {
        }

        public AActionCmd(PActionCommand actionCommand)
        {
            SetActionCommand(actionCommand);
        }

        public override object Clone()
        {
            return new AActionCmd((PActionCommand)CloneNode(_actionCommand));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/AActionCmd.java:24-27
            // Original: @Override public void apply(Switch sw) { ((Analysis)sw).caseAActionCmd(this); }
            if (sw is Analysis.IAnalysis analysis)
            {
                analysis.CaseAActionCmd(this);
            }
            else
            {
                sw.DefaultIn(this);
            }
        }

        public PActionCommand GetActionCommand()
        {
            return _actionCommand;
        }

        public void SetActionCommand(PActionCommand node)
        {
            if (_actionCommand != null)
            {
                _actionCommand.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            _actionCommand = node;
        }

        public override string ToString()
        {
            return ToString(_actionCommand);
        }

        public override void RemoveChild(Node child)
        {
            if (_actionCommand == child)
            {
                _actionCommand = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_actionCommand == oldChild)
            {
                SetActionCommand((PActionCommand)newChild);
            }
        }
    }
}





