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
    public sealed class AStackCommand : PStackCommand
    {
        private PStackOp _stackOp_;
        private TIntegerConstant _pos_;
        private TIntegerConstant _type_;
        private TIntegerConstant _offset_;
        private TSemi _semi_;
        public AStackCommand()
        {
        }

        public AStackCommand(PStackOp _stackOp_, TIntegerConstant _pos_, TIntegerConstant _type_, TIntegerConstant _offset_, TSemi _semi_)
        {
            this.SetStackOp(_stackOp_);
            this.SetPos(_pos_);
            this.SetType(_type_);
            this.SetOffset(_offset_);
            this.SetSemi(_semi_);
        }

        public override object Clone()
        {
            return new AStackCommand((PStackOp)this.CloneNode(this._stackOp_), (TIntegerConstant)this.CloneNode(this._pos_), (TIntegerConstant)this.CloneNode(this._type_), (TIntegerConstant)this.CloneNode(this._offset_), (TSemi)this.CloneNode(this._semi_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseAStackCommand(this);
        }

        public PStackOp GetStackOp()
        {
            return this._stackOp_;
        }

        public void SetStackOp(PStackOp node)
        {
            if (this._stackOp_ != null)
            {
                this._stackOp_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._stackOp_ = node;
        }

        public TIntegerConstant GetPos()
        {
            return this._pos_;
        }

        public void SetPos(TIntegerConstant node)
        {
            if (this._pos_ != null)
            {
                this._pos_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._pos_ = node;
        }

        public new TIntegerConstant GetType()
        {
            return this._type_;
        }

        public void SetType(TIntegerConstant node)
        {
            if (this._type_ != null)
            {
                this._type_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._type_ = node;
        }

        public TIntegerConstant GetOffset()
        {
            return this._offset_;
        }

        public void SetOffset(TIntegerConstant node)
        {
            if (this._offset_ != null)
            {
                this._offset_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._offset_ = node;
        }

        public TSemi GetSemi()
        {
            return this._semi_;
        }

        public void SetSemi(TSemi node)
        {
            if (this._semi_ != null)
            {
                this._semi_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._semi_ = node;
        }

        public override string ToString()
        {
            return this.ToString(this._stackOp_) + this.ToString(this._pos_) + this.ToString(this._type_) + this.ToString(this._offset_) + this.ToString(this._semi_);
        }

        public override void RemoveChild(Node child)
        {
            if (this._stackOp_ == child)
            {
                this._stackOp_ = null;
                return;
            }

            if (this._pos_ == child)
            {
                this._pos_ = null;
                return;
            }

            if (this._type_ == child)
            {
                this._type_ = null;
                return;
            }

            if (this._offset_ == child)
            {
                this._offset_ = null;
                return;
            }

            if (this._semi_ == child)
            {
                this._semi_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._stackOp_ == oldChild)
            {
                this.SetStackOp((PStackOp)newChild);
                return;
            }

            if (this._pos_ == oldChild)
            {
                this.SetPos((TIntegerConstant)newChild);
                return;
            }

            if (this._type_ == oldChild)
            {
                this.SetType((TIntegerConstant)newChild);
                return;
            }

            if (this._offset_ == oldChild)
            {
                this.SetOffset((TIntegerConstant)newChild);
                return;
            }

            if (this._semi_ == oldChild)
            {
                this.SetSemi((TSemi)newChild);
            }
        }
    }
}




