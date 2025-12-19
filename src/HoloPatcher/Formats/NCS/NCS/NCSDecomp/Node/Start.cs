using System.Collections.Generic;

namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class Start : Node
    {
        private PProgram _pProgram;
        private EOF _eof;

        public Start()
        {
            _eof = new EOF();
        }

        public Start(PProgram pProgram, EOF eof)
        {
            SetPProgram(pProgram);
            SetEOF(eof);
        }

        public override object Clone()
        {
            return new Start((PProgram)CloneNode(_pProgram), (EOF)CloneNode(_eof));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            // AST.Start is different from NCSDecomp.Start, so use DefaultIn
            sw.DefaultIn(this);
        }

        public PProgram GetPProgram()
        {
            return _pProgram;
        }

        public void SetPProgram(PProgram node)
        {
            if (_pProgram != null)
            {
                _pProgram.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _pProgram = node;
        }

        public EOF GetEOF()
        {
            return _eof;
        }

        public void SetEOF(EOF node)
        {
            if (_eof != null)
            {
                _eof.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _eof = node ?? new EOF();
        }

        public override void RemoveChild(Node child)
        {
            if (_pProgram == child)
            {
                _pProgram = null;
                return;
            }
            if (_eof == child)
            {
                _eof = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_pProgram == oldChild)
            {
                SetPProgram((PProgram)newChild);
                return;
            }
            if (_eof == oldChild)
            {
                SetEOF((EOF)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_pProgram) + ToString(_eof);
        }
    }
}





