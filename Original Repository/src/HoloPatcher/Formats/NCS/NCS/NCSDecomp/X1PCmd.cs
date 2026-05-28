//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    public sealed class X1PCmd : XPCmd
    {
        private XPCmd _xPCmd_;
        private PCmd _pCmd_;
        public X1PCmd()
        {
        }

        public X1PCmd(XPCmd _xPCmd_, PCmd _pCmd_)
        {
            this.SetXPCmd(_xPCmd_);
            this.SetPCmd(_pCmd_);
        }

        public override object Clone()
        {
            throw new Exception("Unsupported Operation");
        }
        public override void Apply(Switch sw)
        {
            throw new Exception("Switch not supported.");
        }

        public XPCmd GetXPCmd()
        {
            return this._xPCmd_;
        }

        public void SetXPCmd(XPCmd node)
        {
            if (this._xPCmd_ != null)
            {
                this._xPCmd_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._xPCmd_ = node;
        }

        public PCmd GetPCmd()
        {
            return this._pCmd_;
        }

        public void SetPCmd(PCmd node)
        {
            if (this._pCmd_ != null)
            {
                this._pCmd_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._pCmd_ = node;
        }

        public override void RemoveChild(Node child)
        {
            if (this._xPCmd_ == child)
            {
                this._xPCmd_ = null;
            }

            if (this._pCmd_ == child)
            {
                this._pCmd_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
        }

        public override string ToString()
        {
            return this.ToString(this._xPCmd_) + this.ToString(this._pCmd_);
        }
    }
}




