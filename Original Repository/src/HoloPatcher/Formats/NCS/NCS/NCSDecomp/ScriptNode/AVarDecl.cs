using System;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode;
using KOTORModSync.Formats.NCS.NCSDecomp.Stack;
using KOTORModSync.Formats.NCS.NCSDecomp.Utils;

namespace KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode
{
    public class AVarDecl : ScriptNode
    {
        private Variable _var;
        private AExpression _exp;
        private bool _isFcnReturn;

        public AVarDecl(Variable var)
        {
            SetVarVar(var);
            _isFcnReturn = false;
        }

        public Variable GetVarVar()
        {
            return _var;
        }

        public void SetVarVar(Variable var)
        {
            _var = var;
        }

        public bool IsFcnReturn()
        {
            return _isFcnReturn;
        }

        public void SetIsFcnReturn(bool isVal)
        {
            _isFcnReturn = isVal;
        }

        public new Utils.Type GetType()
        {
            if (_var != null)
            {
                return _var.Type();
            }
            return null;
        }

        public void InitializeExp(AExpression exp)
        {
            if (_exp != null)
            {
                _exp.Parent(null);
            }
            if (exp != null)
            {
                exp.Parent((ScriptNode)(object)this);
            }
            _exp = exp;
        }

        public AExpression RemoveExp()
        {
            var aexp = _exp;
            if (_exp != null)
            {
                _exp.Parent(null);
            }
            _exp = null;
            return aexp;
        }

        public AExpression GetExp()
        {
            return _exp;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AVarDecl.java:54-56
        // Original: public AExpression exp() { return this.exp; }
        public AExpression Exp()
        {
            return _exp;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AVarDecl.java:62
        // Original: : this.tabs + this.var.toDeclString() + " = " + ExpressionFormatter.formatValue(this.exp) + ";" + this.newline;
        public override string ToString()
        {
            if (_exp == null)
            {
                return this.tabs + (_var != null ? _var.ToDeclString() : "") + ";" + this.newline;
            }
            return this.tabs + (_var != null ? _var.ToDeclString() : "") + " = " + ExpressionFormatter.FormatValue(_exp) + ";" + this.newline;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AVarDecl.java
        // Original: @Override public void close()
        public override void Close()
        {
            base.Close();
            if (_exp != null)
            {
                if (_exp is ScriptNode expNode)
                {
                    expNode.Close();
                }
                _exp = null;
            }
            if (_var != null)
            {
                // Variable may have Close() method, but we check for it dynamically
                var varType = _var.GetType();
                var closeMethod = varType.GetMethod("Close", System.Type.EmptyTypes);
                if (closeMethod != null)
                {
                    closeMethod.Invoke(_var, null);
                }
            }
            _var = null;
        }
    }
}





