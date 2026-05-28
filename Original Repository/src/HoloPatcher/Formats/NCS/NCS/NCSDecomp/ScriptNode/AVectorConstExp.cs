using KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode;
using KOTORModSync.Formats.NCS.NCSDecomp.Stack;

namespace KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode
{
    public class AVectorConstExp : ScriptNode, AExpression
    {
        private AExpression _exp1;
        private AExpression _exp2;
        private AExpression _exp3;

        public AVectorConstExp(AExpression exp1, AExpression exp2, AExpression exp3)
        {
            SetExp1(exp1);
            SetExp2(exp2);
            SetExp3(exp3);
        }

        public AExpression GetExp1()
        {
            return _exp1;
        }

        public void SetExp1(AExpression exp1)
        {
            _exp1 = exp1;
            if (exp1 != null)
            {
                exp1.Parent((ScriptNode)(object)this);
            }
        }

        public AExpression GetExp2()
        {
            return _exp2;
        }

        public void SetExp2(AExpression exp2)
        {
            _exp2 = exp2;
            if (exp2 != null)
            {
                exp2.Parent((ScriptNode)(object)this);
            }
        }

        public AExpression GetExp3()
        {
            return _exp3;
        }

        public void SetExp3(AExpression exp3)
        {
            _exp3 = exp3;
            if (exp3 != null)
            {
                exp3.Parent((ScriptNode)(object)this);
            }
        }

        ScriptNode AExpression.Parent()
        {
            return (ScriptNode)(object)this;
        }

        void AExpression.Parent(ScriptNode p0)
        {
            // This class doesn't support changing parent through the interface
        }

        StackEntry AExpression.Stackentry()
        {
            return null;
        }

        void AExpression.Stackentry(StackEntry p0)
        {
        }

        public override string ToString()
        {
            return "[" + (_exp1 != null ? _exp1.ToString() : "") + "," +
                   (_exp2 != null ? _exp2.ToString() : "") + "," +
                   (_exp3 != null ? _exp3.ToString() : "") + "]";
        }

        public override void Close()
        {
            base.Close();
            if (_exp1 != null)
            {
                if (_exp1 is ScriptNode node1)
                {
                    node1.Close();
                }
                _exp1 = null;
            }
            if (_exp2 != null)
            {
                if (_exp2 is ScriptNode node2)
                {
                    node2.Close();
                }
                _exp2 = null;
            }
            if (_exp3 != null)
            {
                if (_exp3 is ScriptNode node3)
                {
                    node3.Close();
                }
                _exp3 = null;
            }
        }
    }
}





