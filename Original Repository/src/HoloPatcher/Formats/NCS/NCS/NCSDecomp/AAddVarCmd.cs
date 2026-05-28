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
    public sealed class AAddVarCmd : PCmd
    {
        private PRsaddCommand _rsaddCommand_;
        public AAddVarCmd()
        {
        }

        public AAddVarCmd(PRsaddCommand _rsaddCommand_)
        {
            this.SetRsaddCommand(_rsaddCommand_);
        }

        public override object Clone()
        {
            return new AAddVarCmd((PRsaddCommand)this.CloneNode(this._rsaddCommand_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseAAddVarCmd(this);
        }

        public PRsaddCommand GetRsaddCommand()
        {
            return this._rsaddCommand_;
        }

        public void SetRsaddCommand(PRsaddCommand node)
        {
            if (this._rsaddCommand_ != null)
            {
                this._rsaddCommand_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._rsaddCommand_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._rsaddCommand_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._rsaddCommand_ == child)
            {
                this._rsaddCommand_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._rsaddCommand_ == oldChild)
            {
                this.SetRsaddCommand((PRsaddCommand)newChild);
            }
        }
    }
}




