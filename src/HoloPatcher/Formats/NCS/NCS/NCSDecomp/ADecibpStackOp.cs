// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp.Analysis;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    public sealed class ADecibpStackOp : PStackOp
    {
        private TDecibp _decibp_;
        public ADecibpStackOp()
        {
        }

        public ADecibpStackOp(TDecibp _decibp_)
        {
            this.SetDecibp(_decibp_);
        }

        public override object Clone()
        {
            return new ADecibpStackOp((TDecibp)this.CloneNode(this._decibp_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseADecibpStackOp(this);
        }

        public TDecibp GetDecibp()
        {
            return this._decibp_;
        }

        public void SetDecibp(TDecibp node)
        {
            if (this._decibp_ != null)
            {
                this._decibp_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._decibp_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._decibp_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._decibp_ == child)
            {
                this._decibp_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._decibp_ == oldChild)
            {
                this.SetDecibp((TDecibp)newChild);
            }
        }
    }
}




