//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    public sealed class X1PSubroutine : XPSubroutine
    {
        private XPSubroutine _xPSubroutine_;
        private PSubroutine _pSubroutine_;
        public X1PSubroutine()
        {
        }

        public X1PSubroutine(XPSubroutine _xPSubroutine_, PSubroutine _pSubroutine_)
        {
            this.SetXPSubroutine(_xPSubroutine_);
            this.SetPSubroutine(_pSubroutine_);
        }

        public override object Clone()
        {
            throw new Exception("Unsupported Operation");
        }
        public override void Apply(Switch sw)
        {
            throw new Exception("Switch not supported.");
        }

        public XPSubroutine GetXPSubroutine()
        {
            return this._xPSubroutine_;
        }

        public void SetXPSubroutine(XPSubroutine node)
        {
            if (this._xPSubroutine_ != null)
            {
                this._xPSubroutine_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._xPSubroutine_ = node;
        }

        public PSubroutine GetPSubroutine()
        {
            return this._pSubroutine_;
        }

        public void SetPSubroutine(PSubroutine node)
        {
            if (this._pSubroutine_ != null)
            {
                this._pSubroutine_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._pSubroutine_ = node;
        }

        public override void RemoveChild(Node child)
        {
            if (this._xPSubroutine_ == child)
            {
                this._xPSubroutine_ = null;
            }

            if (this._pSubroutine_ == child)
            {
                this._pSubroutine_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
        }

        public override string ToString()
        {
            return this.ToString(this._xPSubroutine_) + this.ToString(this._pSubroutine_);
        }
    }
}




