namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class ALogiiCmd : PCmd
    {
        private PLogiiCommand _logiiCommand;

        public ALogiiCmd()
        {
        }

        public ALogiiCmd(PLogiiCommand logiiCommand)
        {
            SetLogiiCommand(logiiCommand);
        }

        public override object Clone()
        {
            return new ALogiiCmd((PLogiiCommand)CloneNode(_logiiCommand));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public PLogiiCommand GetLogiiCommand()
        {
            return _logiiCommand;
        }

        public void SetLogiiCommand(PLogiiCommand node)
        {
            if (_logiiCommand != null)
            {
                _logiiCommand.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _logiiCommand = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_logiiCommand == child)
            {
                _logiiCommand = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_logiiCommand == oldChild)
            {
                SetLogiiCommand((PLogiiCommand)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_logiiCommand);
        }
    }
}





