using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode;
using KOTORModSync.Formats.NCS.NCSDecomp.Stack;

namespace KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode
{
    public class ABinaryExp : ScriptNode, AExpression
    {
        private AExpression _left;
        private AExpression _right;
        private string _op;
        private StackEntry _stackEntry;

        public ABinaryExp(AExpression left, AExpression right, string op)
        {
            SetLeft(left);
            SetRight(right);
            _op = op;
        }

        public AExpression GetLeft()
        {
            return _left;
        }

        public void SetLeft(AExpression left)
        {
            _left = left;
            if (left != null)
            {
                left.Parent((ScriptNode)(object)this);
            }
        }

        public AExpression GetRight()
        {
            return _right;
        }

        public void SetRight(AExpression right)
        {
            _right = right;
            if (right != null)
            {
                right.Parent((ScriptNode)(object)this);
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

        public StackEntry Stackentry()
        {
            return _stackEntry;
        }

        public void Stackentry(StackEntry p0)
        {
            _stackEntry = p0;
        }

        public new ScriptNode Parent() => base.Parent();
        public new void Parent(ScriptNode p0) => base.Parent(p0);

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ABinaryExp.java:44-46
        // Original: @Override public String toString() { return ExpressionFormatter.format(this); }
        public override string ToString()
        {
            return ExpressionFormatter.Format(this);
        }

        public virtual void Close()
        {
            if (_left != null)
            {
                if (_left is ScriptNode leftNode)
                {
                    leftNode.Close();
                }
                else if (_left is StackEntry leftEntry)
                {
                    leftEntry.Close();
                }
                _left = null;
            }
            if (_right != null)
            {
                if (_right is ScriptNode rightNode)
                {
                    rightNode.Close();
                }
                else if (_right is StackEntry rightEntry)
                {
                    rightEntry.Close();
                }
                _right = null;
            }
            if (_stackEntry != null)
            {
                _stackEntry.Close();
            }
            _stackEntry = null;
        }
    }
}





