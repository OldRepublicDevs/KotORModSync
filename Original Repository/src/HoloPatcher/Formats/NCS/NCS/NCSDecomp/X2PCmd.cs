//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    public sealed class X2PCmd : XPCmd
    {
        private PCmd _pCmd_;
        public X2PCmd()
        {
        }

        public X2PCmd(PCmd _pCmd_)
        {
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
            return new StringBuilder().Append(this.ToString(this._pCmd_)).ToString();
        }
    }
}




