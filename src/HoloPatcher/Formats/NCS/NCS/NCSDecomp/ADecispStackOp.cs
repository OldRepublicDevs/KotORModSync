// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp.Analysis;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    public sealed class ADecispStackOp : PStackOp
    {
        private TDecisp _decisp_;
        public ADecispStackOp()
        {
        }

        public ADecispStackOp(TDecisp _decisp_)
        {
            this.SetDecisp(_decisp_);
        }

        public override object Clone()
        {
            return new ADecispStackOp((TDecisp)this.CloneNode(this._decisp_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseADecispStackOp(this);
        }

        public TDecisp GetDecisp()
        {
            return this._decisp_;
        }

        public void SetDecisp(TDecisp node)
        {
            if (this._decisp_ != null)
            {
                this._decisp_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._decisp_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._decisp_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._decisp_ == child)
            {
                this._decisp_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._decisp_ == oldChild)
            {
                this.SetDecisp((TDecisp)newChild);
            }
        }
    }
}




