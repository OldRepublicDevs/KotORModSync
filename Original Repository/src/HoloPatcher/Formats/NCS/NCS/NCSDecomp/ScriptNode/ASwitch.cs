using System.Collections.Generic;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode;
using KOTORModSync.Formats.NCS.NCSDecomp.Stack;

namespace KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode
{
    public class ASwitch : ScriptNode
    {
        private int _start;
        private int _end;
        private AExpression _switchExp;
        private List<ASwitchCase> _cases;
        private ASwitchCase _defaultCase;

        public ASwitch(int start, AExpression switchExp)
        {
            _start = start;
            _end = -1;
            _cases = new List<ASwitchCase>();
            SetSwitchExp(switchExp);
        }

        public void SetSwitchExp(AExpression switchExp)
        {
            if (_switchExp != null)
            {
                _switchExp.Parent(null);
            }
            if (switchExp != null)
            {
                switchExp.Parent(this);
            }
            _switchExp = switchExp;
        }

        public AExpression GetSwitchExp()
        {
            return _switchExp;
        }

        public void SetEnd(int end)
        {
            _end = end;
            if (_defaultCase != null)
            {
                _defaultCase.End(end);
            }
            else if (_cases.Count > 0)
            {
                _cases[_cases.Count - 1].End(end);
            }
        }

        public int GetEnd()
        {
            return _end;
        }

        public int GetStart()
        {
            return _start;
        }

        public void SetStart(int start)
        {
            _start = start;
        }

        public void AddCase(ASwitchCase aCase)
        {
            if (aCase != null)
            {
                aCase.Parent(this);
            }
            _cases.Add(aCase);
        }

        public void AddDefaultCase(ASwitchCase aCase)
        {
            if (_defaultCase != null)
            {
                _defaultCase.Parent(null);
            }
            if (aCase != null)
            {
                aCase.Parent(this);
            }
            _defaultCase = aCase;
        }

        public ASwitchCase GetLastCase()
        {
            if (_cases.Count == 0)
            {
                return null;
            }
            return _cases[_cases.Count - 1];
        }

        public ASwitchCase GetNextCase(ASwitchCase lastCase)
        {
            if (lastCase == null)
            {
                return GetFirstCase();
            }
            if (lastCase == _defaultCase)
            {
                return null;
            }
            int index = _cases.IndexOf(lastCase);
            if (index < 0)
            {
                throw new System.Exception("invalid last case passed in");
            }
            index++;
            if (_cases.Count > index)
            {
                return _cases[index];
            }
            return _defaultCase;
        }

        public ASwitchCase GetFirstCase()
        {
            if (_cases.Count > 0)
            {
                return _cases[0];
            }
            return _defaultCase;
        }

        public int GetFirstCaseStart()
        {
            if (_cases.Count > 0)
            {
                return _cases[0].GetStart();
            }
            if (_defaultCase != null)
            {
                return _defaultCase.GetStart();
            }
            return -1;
        }

        public List<ASwitchCase> GetCases()
        {
            return _cases;
        }

        public ASwitchCase GetDefaultCase()
        {
            return _defaultCase;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ASwitch.java:91-106
        // Original: @Override public String toString() { ... buff.append(this.tabs + "switch(" + this.switchexp + ") {" + this.newline); ... }
        public override string ToString()
        {
            var buff = new StringBuilder();
            buff.Append(this.tabs + "switch (" + (_switchExp != null ? _switchExp.ToString() : "") + ") {" + this.newline);
            foreach (var case_ in _cases)
            {
                buff.Append(case_.ToString());
            }
            if (_defaultCase != null)
            {
                buff.Append(_defaultCase.ToString());
            }
            buff.Append(this.tabs + "}" + this.newline);
            return buff.ToString();
        }

        public void Close()
        {
            if (_cases != null)
            {
                foreach (var case_ in _cases)
                {
                    case_.Close();
                }
            }
            _cases = null;
            if (_defaultCase != null)
            {
                _defaultCase.Close();
            }
            _defaultCase = null;
            if (_switchExp != null)
            {
                if (_switchExp is ScriptNode switchExpNode)
                {
                    switchExpNode.Close();
                }
                else if (_switchExp is StackEntry switchExpEntry)
                {
                    switchExpEntry.Close();
                }
            }
            _switchExp = null;
        }
    }
}





