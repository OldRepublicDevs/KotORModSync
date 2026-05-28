// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp.Analysis;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    public sealed class AIncibpStackOp : PStackOp
    {
        private TIncibp _incibp_;
        public AIncibpStackOp()
        {
        }

        public AIncibpStackOp(TIncibp _incibp_)
        {
            this.SetIncibp(_incibp_);
        }

        public override object Clone()
        {
            return new AIncibpStackOp((TIncibp)this.CloneNode(this._incibp_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseAIncibpStackOp(this);
        }

        public TIncibp GetIncibp()
        {
            return this._incibp_;
        }

        public void SetIncibp(TIncibp node)
        {
            if (this._incibp_ != null)
            {
                this._incibp_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._incibp_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._incibp_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._incibp_ == child)
            {
                this._incibp_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._incibp_ == oldChild)
            {
                this.SetIncibp((TIncibp)newChild);
            }
        }
    }
}




