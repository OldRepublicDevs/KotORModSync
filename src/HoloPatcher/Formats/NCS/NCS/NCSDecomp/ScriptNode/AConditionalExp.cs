using KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode;
using KOTORModSync.Formats.NCS.NCSDecomp.Stack;

namespace KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode
{
    public class AConditionalExp : ScriptNode, AExpression
    {
        private AExpression _left;
        private AExpression _right;
        private string _op;
        private StackEntry _stackEntry;
        private ScriptNode _interfaceParent;

        public AConditionalExp(AExpression left, AExpression right, string op)
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

        ScriptNode AExpression.Parent() => _interfaceParent;
        void AExpression.Parent(ScriptNode p0) => _interfaceParent = p0;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AConditionalExp.java:45
        // Original: return ExpressionFormatter.format(this);
        public override string ToString()
        {
            return ExpressionFormatter.Format(this);
        }

        public override void Close()
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
            _interfaceParent = null;
            base.Close();
        }
    }
}





