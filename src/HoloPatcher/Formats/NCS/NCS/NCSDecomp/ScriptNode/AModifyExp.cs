using KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode;
using KOTORModSync.Formats.NCS.NCSDecomp.Stack;

namespace KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode
{
    public class AModifyExp : ScriptNode, AExpression
    {
        private AVarRef _varRef;
        private AExpression _exp;
        private ScriptNode _interfaceParent;

        public AModifyExp(AVarRef varRef, AExpression exp)
        {
            SetVarRef(varRef);
            SetExpression(exp);
        }

        public AVarRef GetVarRef()
        {
            return _varRef;
        }

        public void SetVarRef(AVarRef varRef)
        {
            _varRef = varRef;
            if (varRef != null)
            {
                ((AExpression)varRef).Parent((ScriptNode)(AExpression)this);
            }
        }

        public AExpression GetExpression()
        {
            return _exp;
        }

        public void SetExpression(AExpression exp)
        {
            _exp = exp;
            if (exp != null)
            {
                exp.Parent((ScriptNode)(AExpression)this);
            }
        }

        public StackEntry StackEntry()
        {
            return _varRef != null ? _varRef.Var() : null;
        }

        public void SetStackEntry(StackEntry stackEntry)
        {
            // Do nothing - stackentry is derived from varref
        }

        public StackEntry Stackentry()
        {
            return _varRef != null ? _varRef.Var() : null;
        }

        public void Stackentry(StackEntry p0)
        {
            // Do nothing - stackentry is derived from varref
        }

        ScriptNode AExpression.Parent() => _interfaceParent;
        void AExpression.Parent(ScriptNode p0) => _interfaceParent = p0;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AModifyExp.java:39
        // Original: return ExpressionFormatter.format(this);
        public override string ToString()
        {
            return ExpressionFormatter.Format(this);
        }

        public override void Close()
        {
            if (_exp != null)
            {
                if (_exp is ScriptNode ScriptNode)
                {
                    ScriptNode.Close();
                }
                else if (_exp is StackEntry expEntry)
                {
                    expEntry.Close();
                }
                _exp = null;
            }
            if (_varRef != null)
            {
                _varRef.Close();
            }
            _varRef = null;
            _interfaceParent = null;
            base.Close();
        }
    }
}






