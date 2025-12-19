using System.Collections.Generic;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp;
using KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode;
using KOTORModSync.Formats.NCS.NCSDecomp.Stack;

namespace KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode
{
    public class AActionExp : ScriptNode, AExpression
    {
        private string _action;
        private int _id;
        private List<AExpression> _params;
        private StackEntry _stackEntry;
        private ActionsData _actionsData;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AActionExp.java:24-26
        // Original: public AActionExp(String action, int id, List<AExpression> params) { this(action, id, params, null); }
        public AActionExp(string action, int idVal, List<AExpression> @params)
            : this(action, idVal, @params, null)
        {
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AActionExp.java:28-39
        // Original: public AActionExp(String action, int id, List<AExpression> params, ActionsData actionsData) { ... }
        public AActionExp(string action, int idVal, List<AExpression> @params, ActionsData actionsData)
        {
            _action = action;
            _id = idVal;
            _params = new List<AExpression>();
            _actionsData = actionsData;
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
                param.Parent((ScriptNode)(object)this);
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

        public string GetAction()
        {
            return _action;
        }

        public void SetAction(string action)
        {
            _action = action;
        }

        public int GetId()
        {
            return _id;
        }

        public void SetId(int id)
        {
            _id = id;
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

        public override string ToString()
        {
            var buff = new StringBuilder();
            buff.Append(_action + "(");
            string prefix = "";
            foreach (var param in _params)
            {
                buff.Append(prefix + (param != null ? param.ToString() : ""));
                prefix = ", ";
            }
            buff.Append(")");
            return buff.ToString();
        }

        public virtual void Close()
        {
            base.Close();
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





