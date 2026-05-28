//
using System;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    public sealed class X2PSubroutine : XPSubroutine
    {
        private PSubroutine _pSubroutine_;
        public X2PSubroutine()
        {
        }

        public X2PSubroutine(PSubroutine _pSubroutine_)
        {
            SetPSubroutine(_pSubroutine_);
        }

        public override object Clone()
        {
            throw new Exception("Unsupported Operation");
        }
        public override void Apply(Switch sw)
        {
            throw new Exception("Switch not supported.");
        }

        public PSubroutine GetPSubroutine()
        {
            return _pSubroutine_;
        }

        public void SetPSubroutine(PSubroutine node)
        {
            if (_pSubroutine_ != null)
            {
                _pSubroutine_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            _pSubroutine_ = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_pSubroutine_ == child)
            {
                _pSubroutine_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
        }

        public override string ToString()
        {
            return new StringBuilder().Append(ToString(_pSubroutine_)).ToString();
        }
    }
}




