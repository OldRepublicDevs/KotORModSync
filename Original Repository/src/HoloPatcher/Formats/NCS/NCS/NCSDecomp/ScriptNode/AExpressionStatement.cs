using KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode;
using KOTORModSync.Formats.NCS.NCSDecomp.Stack;

namespace KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode
{
    public class AExpressionStatement : ScriptNode
    {
        private AExpression _exp;

        public AExpressionStatement(AExpression exp)
        {
            SetExp(exp);
        }

        public AExpression GetExp()
        {
            return _exp;
        }

        public void SetExp(AExpression exp)
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

        public override string ToString()
        {
            return this.tabs + (_exp != null ? _exp.ToString() : "") + ";" + this.newline;
        }

        public override void Close()
        {
            base.Close();
            if (_exp != null)
            {
                if (_exp is ScriptNode expNode)
                {
                    expNode.Close();
                }
                else if (_exp is StackEntry expEntry)
                {
                    expEntry.Close();
                }
            }
            _exp = null;
        }
    }
}





