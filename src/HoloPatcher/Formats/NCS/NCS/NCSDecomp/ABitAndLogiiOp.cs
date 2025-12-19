// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp.Analysis;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    public sealed class ABitAndLogiiOp : PLogiiOp
    {
        private TBoolandii _boolandii_;
        public ABitAndLogiiOp()
        {
        }

        public ABitAndLogiiOp(TBoolandii _boolandii_)
        {
            this.SetBoolandii(_boolandii_);
        }

        public override object Clone()
        {
            return new ABitAndLogiiOp((TBoolandii)this.CloneNode(this._boolandii_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseABitAndLogiiOp(this);
        }

        public TBoolandii GetBoolandii()
        {
            return this._boolandii_;
        }

        public void SetBoolandii(TBoolandii node)
        {
            if (this._boolandii_ != null)
            {
                this._boolandii_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._boolandii_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._boolandii_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._boolandii_ == child)
            {
                this._boolandii_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._boolandii_ == oldChild)
            {
                this.SetBoolandii((TBoolandii)newChild);
            }
        }
    }
}




