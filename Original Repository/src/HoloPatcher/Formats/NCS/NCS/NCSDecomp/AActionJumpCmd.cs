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
    public sealed class AActionJumpCmd : PCmd
    {
        private PStoreStateCommand _storeStateCommand_;
        private PJumpCommand _jumpCommand_;
        private PCommandBlock _commandBlock_;
        private PReturn _return_;
        public AActionJumpCmd()
        {
        }

        public AActionJumpCmd(PStoreStateCommand _storeStateCommand_, PJumpCommand _jumpCommand_, PCommandBlock _commandBlock_, PReturn _return_)
        {
            this.SetStoreStateCommand(_storeStateCommand_);
            this.SetJumpCommand(_jumpCommand_);
            this.SetCommandBlock(_commandBlock_);
            this.SetReturn(_return_);
        }

        public override object Clone()
        {
            return new AActionJumpCmd((PStoreStateCommand)this.CloneNode(this._storeStateCommand_), (PJumpCommand)this.CloneNode(this._jumpCommand_), (PCommandBlock)this.CloneNode(this._commandBlock_), (PReturn)this.CloneNode(this._return_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseAActionJumpCmd(this);
        }

        public PStoreStateCommand GetStoreStateCommand()
        {
            return this._storeStateCommand_;
        }

        public void SetStoreStateCommand(PStoreStateCommand node)
        {
            if (this._storeStateCommand_ != null)
            {
                this._storeStateCommand_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._storeStateCommand_ = node;
        }

        public PJumpCommand GetJumpCommand()
        {
            return this._jumpCommand_;
        }

        public void SetJumpCommand(PJumpCommand node)
        {
            if (this._jumpCommand_ != null)
            {
                this._jumpCommand_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._jumpCommand_ = node;
        }

        public PCommandBlock GetCommandBlock()
        {
            return this._commandBlock_;
        }

        public void SetCommandBlock(PCommandBlock node)
        {
            if (this._commandBlock_ != null)
            {
                this._commandBlock_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._commandBlock_ = node;
        }

        public PReturn GetReturn()
        {
            return this._return_;
        }

        public void SetReturn(PReturn node)
        {
            if (this._return_ != null)
            {
                this._return_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._return_ = node;
        }

        public override string ToString()
        {
            return this.ToString(this._storeStateCommand_) + this.ToString(this._jumpCommand_) + this.ToString(this._commandBlock_) + this.ToString(this._return_);
        }

        public override void RemoveChild(Node child)
        {
            if (this._storeStateCommand_ == child)
            {
                this._storeStateCommand_ = null;
                return;
            }

            if (this._jumpCommand_ == child)
            {
                this._jumpCommand_ = null;
                return;
            }

            if (this._commandBlock_ == child)
            {
                this._commandBlock_ = null;
                return;
            }

            if (this._return_ == child)
            {
                this._return_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._storeStateCommand_ == oldChild)
            {
                this.SetStoreStateCommand((PStoreStateCommand)newChild);
                return;
            }

            if (this._jumpCommand_ == oldChild)
            {
                this.SetJumpCommand((PJumpCommand)newChild);
                return;
            }

            if (this._commandBlock_ == oldChild)
            {
                this.SetCommandBlock((PCommandBlock)newChild);
                return;
            }

            if (this._return_ == oldChild)
            {
                this.SetReturn((PReturn)newChild);
            }
        }
    }
}




