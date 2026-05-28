using System.Collections.Generic;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode;
using KOTORModSync.Formats.NCS.NCSDecomp.Stack;

namespace KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode
{
    public class AFcnCallExp : ScriptNode, AExpression
    {
        private int _id;
        private List<AExpression> _params;
        private StackEntry _stackEntry;

        public AFcnCallExp(int idVal, List<AExpression> @params)
        {
            _id = idVal;
            _params = new List<AExpression>();
            if (@params != null)
            {
                foreach (var param in @params)
                {
                    AddParam(param);
                }
            }
        }

        public void AddParam(AExpression param)
        {
            if (param != null)
            {
                param.Parent((ScriptNode)(AExpression)this);
            }
            _params.Add(param);
        }

        public AExpression GetParam(int pos)
        {
            return _params[pos];
        }

        public List<AExpression> GetParams()
        {
            return _params;
        }

        public int GetId()
        {
            return _id;
        }

        public void SetId(int id)
        {
            _id = id;
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

        public override string ToString()
        {
            var buff = new StringBuilder();
            buff.Append("sub" + _id + "(");
            string prefix = "";
            foreach (var param in _params)
            {
                buff.Append(prefix + (param != null ? param.ToString() : ""));
                prefix = ", ";
            }
            buff.Append(")");
            return buff.ToString();
        }

        public override void Close()
        {
            if (_params != null)
            {
                foreach (var param in _params)
                {
                    if (param != null)
                    {
                        if (param is ScriptNode paramNode)
                        {
                            paramNode.Close();
                        }
                        else if (param is StackEntry paramEntry)
                        {
                            paramEntry.Close();
                        }
                    }
                }
            }
            _params = null;
            if (_stackEntry != null)
            {
                _stackEntry.Close();
            }
            _stackEntry = null;
        }
    }
}





