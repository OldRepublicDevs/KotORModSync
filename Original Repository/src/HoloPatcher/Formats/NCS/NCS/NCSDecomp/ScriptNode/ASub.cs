// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ASub.java:15-194
// Original: public class ASub extends ScriptRootNode
using System.Collections.Generic;
using static KOTORModSync.Formats.NCS.NCSDecomp.DecompilerLogger;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode;
using KOTORModSync.Formats.NCS.NCSDecomp.Stack;
using KOTORModSync.Formats.NCS.NCSDecomp.Utils;

namespace KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode
{
    public class ASub : ScriptRootNode
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ASub.java:16-20
        // Original: private Type type; private byte id; private List<ScriptNode> params; private String name; private boolean ismain;
        private Type _type;
        private int _id;
        private List<AVarRef> _params;
        private string _name;
        private bool _isMain;

        public ASub() : this(0, null, null, 0, 0)
        {
        }

        public ASub(int typeVal, int? idVal, List<AVarRef> @params, int start, int end) : base(start, end)
        {
            _type = new Type((byte)typeVal);
            if (idVal.HasValue)
            {
                _id = idVal.Value;
                _params = new List<AVarRef>();
                if (@params != null)
                {
                    foreach (var param in @params)
                    {
                        AddParam(param);
                    }
                }
                _name = "sub" + _id;
            }
            else
            {
                // Matching Java ASub(int start, int end) constructor - used for globals only
                // name is intentionally not set (null in Java, null in C#)
                // This is OK because globals use toStringGlobals() which only calls getBody(), never getHeader()
                _type = new Type(0);
                _params = null;
                _name = null;  // Match Java: name is null for globals ASub
            }
            // SetTabs is not available in ScriptNode
        }

        public ASub(Type typeVal, int? idVal, List<AVarRef> @params, int start, int end) : base(start, end)
        {
            _type = typeVal;
            if (idVal.HasValue)
            {
                _id = idVal.Value;
                _params = new List<AVarRef>();
                if (@params != null)
                {
                    foreach (var param in @params)
                    {
                        AddParam(param);
                    }
                }
                _name = "sub" + _id;
            }
            else
            {
                // Matching Java ASub(int start, int end) constructor - used for globals only
                // name is intentionally not set (null in Java, null in C#)
                // This is OK because globals use toStringGlobals() which only calls getBody(), never getHeader()
                _type = new Type(0);
                _params = null;
                _name = null;  // Match Java: name is null for globals ASub
            }
            // SetTabs is not available in ScriptNode
        }

        public void AddParam(AVarRef param)
        {
            ((AExpression)param).Parent(this);
            if (_params == null)
            {
                _params = new List<AVarRef>();
            }
            _params.Add(param);
        }

        public override string ToString()
        {
            return GetHeader() + " {" + this.newline + GetBody() + "}" + this.newline;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ASub.java:53-61
        // Original: public String getBody() { StringBuffer buff = new StringBuffer(); for (int i = 0; i < this.children.size(); i++) { buff.append(this.children.get(i).toString()); } return buff.toString(); }
        public string GetBody()
        {
            var buff = new StringBuilder();
            foreach (var child in GetChildren())
            {
                // If child is an expression (not a statement), wrap it in AExpressionStatement for output
                // This matches Java behavior where expressions added directly need to be statements
                if (child is AExpression && !(child is AExpressionStatement) && !(child is AVarDecl))
                {
                    buff.Append(this.tabs + child.ToString() + ";" + this.newline);
                }
                else
                {
                    buff.Append(child.ToString());
                }
            }
            return buff.ToString();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ASub.java:63-77
        // Original: public String getHeader() { StringBuffer buff = new StringBuffer(); buff.append(this.type + " " + this.name + "("); ... }
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASub.java:63-77
        // Original: public String getHeader() { StringBuffer buff = new StringBuffer(); buff.append(this.type + " " + this.name + "("); ... }
        public string GetHeader()
        {
            // Matching Java: if name is null, this would throw NullPointerException in Java
            // In Java, getHeader() is never called for globals (which have null name)
            // For non-globals, name should always be set
            // However, if this is somehow called on a globals ASub, return empty string to avoid breaking code generation
            if (_name == null)
            {
                // This should never happen for non-globals subroutines
                // If it does, it indicates a bug, but we'll return empty string to avoid breaking the decompiler
                Debug("WARNING: GetHeader() called on ASub with null name. This should only happen for globals, which should use GetBody() instead.");
                return "";
            }
            var buff = new StringBuilder();
            string typeStr = _type != null ? _type.ToString() : "void";
            buff.Append(typeStr + " " + _name + "(");
            string link = "";
            if (_params != null)
            {
                foreach (var param in _params)
                {
                    var ptype = param.Type();
                    buff.Append(link + (ptype != null ? ptype.ToString() : "") + " " + param.ToString());
                    link = ", ";
                }
            }
            buff.Append(")");
            return buff.ToString();
        }

        public void SetIsMain(bool isMain)
        {
            _isMain = isMain;
            if (isMain)
            {
                if (_type != null && _type.Equals(3))
                {
                    _name = "StartingConditional";
                }
                else
                {
                    _name = "main";
                }
            }
        }

        public bool IsMain()
        {
            return _isMain;
        }

        public new Utils.Type GetType()
        {
            return _type;
        }

        public void SetName(string name)
        {
            _name = name;
        }

        public string GetName()
        {
            return _name;
        }

        public List<AVarRef> GetParams()
        {
            return _params;
        }

        public List<StackEntry> GetParamVars()
        {
            var varsList = new List<StackEntry>();
            if (_params != null)
            {
                foreach (var param in _params)
                {
                    varsList.Add(param.Var());
                }
            }
            return varsList;
        }

        public int GetId()
        {
            return _id;
        }

        public void SetId(int id)
        {
            _id = id;
        }

        public virtual void Close()
        {
            if (_params != null)
            {
                foreach (var param in _params)
                {
                    if (param is AVarRef paramVarRef)
                    {
                        paramVarRef.Close();
                    }
                    else if (param is ScriptNode paramNode)
                    {
                        paramNode.Close();
                    }
                }
            }
            _params = null;
            _type = null;
        }
    }
}





