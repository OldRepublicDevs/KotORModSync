using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode;

namespace KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode
{
    public class AReturnStatement : ScriptNode
    {
        private AExpression _returnExp;

        public AReturnStatement() : this(null)
        {
        }

        public AReturnStatement(AExpression returnExp)
        {
            if (returnExp != null)
            {
                SetReturnExp(returnExp);
            }
        }

        public AExpression GetReturnExp()
        {
            return _returnExp;
        }

        public AExpression GetExp()
        {
            return _returnExp;
        }

        public void SetReturnExp(AExpression returnExp)
        {
            if (_returnExp != null && _returnExp is ScriptNode returnExpNode)
            {
                returnExpNode.Parent(null);
            }
            if (returnExp != null && returnExp is ScriptNode returnExpNode2)
            {
                returnExpNode2.Parent(this);
            }
            _returnExp = returnExp;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AReturnStatement.java:31
        // Original: : this.tabs + "return " + ExpressionFormatter.formatValue(this.returnexp) + ";" + this.newline;
        public override string ToString()
        {
            if (_returnExp == null)
            {
                return this.tabs + "return;" + this.newline;
            }
            return this.tabs + "return " + ExpressionFormatter.FormatValue(_returnExp) + ";" + this.newline;
        }

        public override void Close()
        {
            base.Close();
            if (_returnExp != null && _returnExp is ScriptNode returnExpNode)
            {
                returnExpNode.Close();
            }
            _returnExp = null;
        }
    }
}





