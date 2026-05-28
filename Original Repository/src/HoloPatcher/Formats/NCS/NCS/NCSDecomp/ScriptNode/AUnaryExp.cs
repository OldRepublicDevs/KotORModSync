using System;
using KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode;
using KOTORModSync.Formats.NCS.NCSDecomp.Stack;

namespace KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode
{
    public class AUnaryExp : ScriptNode, AExpression
    {
        private AExpression _exp;
        private string _op;
        private StackEntry _stackEntry;

        public AUnaryExp(AExpression exp, string op)
        {
            SetExp(exp);
            _op = op;
        }

        public AExpression GetExp()
        {
            return _exp;
        }

        public void SetExp(AExpression exp)
        {
            _exp = exp;
            if (exp != null)
            {
                exp.Parent((ScriptNode)(object)this);
            }
        }

        public string GetOp()
        {
            return _op;
        }

        public void SetOp(string op)
        {
            _op = op;
        }

        public StackEntry StackEntry()
        {
            return _stackEntry;
        }

        public void SetStackEntry(StackEntry stackEntry)
        {
            _stackEntry = stackEntry;
        }

        public StackEntry Stackentry()
        {
            return _stackEntry;
        }

        public void Stackentry(StackEntry p0)
        {
            _stackEntry = p0;
        }

        ScriptNode AExpression.Parent() => (ScriptNode)(object)base.Parent();
        void AExpression.Parent(ScriptNode p0) => base.Parent((ScriptNode)(object)p0);

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AUnaryExp.java:35
        // Original: return ExpressionFormatter.format(this);
        public override string ToString()
        {
            return ExpressionFormatter.Format(this);
        }

        public override void Close()
        {
            if (_exp != null)
            {
                if (_exp is ScriptNode expNode)
                {
                    expNode.Close();
                }
                _exp = null;
            }
            if (_stackEntry != null)
            {
                _stackEntry.Close();
            }
            _stackEntry = null;
        }
    }
}





