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
    public sealed class AStackOpCmd : PCmd
    {
        private PStackCommand _stackCommand_;
        public AStackOpCmd()
        {
        }

        public AStackOpCmd(PStackCommand _stackCommand_)
        {
            this.SetStackCommand(_stackCommand_);
        }

        public override object Clone()
        {
            return new AStackOpCmd((PStackCommand)this.CloneNode(this._stackCommand_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseAStackOpCmd(this);
        }

        public PStackCommand GetStackCommand()
        {
            return this._stackCommand_;
        }

        public void SetStackCommand(PStackCommand node)
        {
            if (this._stackCommand_ != null)
            {
                this._stackCommand_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._stackCommand_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._stackCommand_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._stackCommand_ == child)
            {
                this._stackCommand_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._stackCommand_ == oldChild)
            {
                this.SetStackCommand((PStackCommand)newChild);
            }
        }
    }
}




