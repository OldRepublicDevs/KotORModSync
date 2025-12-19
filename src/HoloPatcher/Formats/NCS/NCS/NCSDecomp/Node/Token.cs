namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public abstract class Token : Node
    {
        private string _text;
        private int _line;
        private int _pos;

        public Token() : this("")
        {
        }

        public Token(string text)
        {
            _text = text;
            _line = 0;
            _pos = 0;
        }

        public string GetText()
        {
            return _text;
        }

        public virtual void SetText(string text)
        {
            _text = text;
        }

        public int GetLine()
        {
            return _line;
        }

        public void SetLine(int line)
        {
            _line = line;
        }

        public int GetPos()
        {
            return _pos;
        }

        public void SetPos(int pos)
        {
            _pos = pos;
        }

        public override string ToString()
        {
            return _text + " ";
        }

        public override void RemoveChild(Node child)
        {
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
        }

        public override object Clone()
        {
            var cloned = (Token)System.Activator.CreateInstance(this.GetType(), _text);
            cloned._line = _line;
            cloned._pos = _pos;
            return cloned;
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }
    }
}





