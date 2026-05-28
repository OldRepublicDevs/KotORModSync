// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp.Analysis;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    public sealed class AIncispStackOp : PStackOp
    {
        private TIncisp _incisp_;
        public AIncispStackOp()
        {
        }

        public AIncispStackOp(TIncisp _incisp_)
        {
            this.SetIncisp(_incisp_);
        }

        public override object Clone()
        {
            return new AIncispStackOp((TIncisp)this.CloneNode(this._incisp_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseAIncispStackOp(this);
        }

        public TIncisp GetIncisp()
        {
            return this._incisp_;
        }

        public void SetIncisp(TIncisp node)
        {
            if (this._incisp_ != null)
            {
                this._incisp_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._incisp_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._incisp_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._incisp_ == child)
            {
                this._incisp_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._incisp_ == oldChild)
            {
                this.SetIncisp((TIncisp)newChild);
            }
        }
    }
}




