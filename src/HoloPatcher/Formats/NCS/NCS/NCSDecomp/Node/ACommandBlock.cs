using System.Collections.Generic;

namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class ACommandBlock : PCommandBlock
    {
        private List<PCmd> _cmd;

        public ACommandBlock()
        {
            _cmd = new List<PCmd>();
        }

        public ACommandBlock(List<PCmd> cmd)
        {
            _cmd = new List<PCmd>();
            if (cmd != null)
            {
                _cmd.AddRange(cmd);
            }
        }

        public override object Clone()
        {
            var list = new List<PCmd>();
            foreach (var item in _cmd)
            {
                list.Add((PCmd)item);
            }
            return new ACommandBlock(list);
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            // Matching DeNCS implementation - cast to IAnalysis interface and call CaseACommandBlock directly
            if (sw is Analysis.IAnalysis analysis)
            {
                analysis.CaseACommandBlock(this);
            }
            else
            {
                sw.DefaultIn(this);
            }
        }

        public List<PCmd> GetCmd()
        {
            return _cmd;
        }

        public void SetCmd(List<PCmd> cmdList)
        {
            _cmd.Clear();
            if (cmdList != null)
            {
                foreach (var cmd in cmdList)
                {
                    if (cmd.Parent() != null)
                    {
                        cmd.Parent().RemoveChild(cmd);
                    }
                    cmd.Parent(this);
                    _cmd.Add(cmd);
                }
            }
        }

        public void AddCmd(PCmd cmd)
        {
            if (cmd.Parent() != null)
            {
                cmd.Parent().RemoveChild(cmd);
            }
            cmd.Parent(this);
            _cmd.Add(cmd);
        }

        public override void RemoveChild(Node child)
        {
            _cmd.Remove((PCmd)child);
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            int index = _cmd.IndexOf((PCmd)oldChild);
            if (index >= 0)
            {
                if (newChild != null)
                {
                    _cmd[index] = (PCmd)newChild;
                    if (newChild.Parent() != null)
                    {
                        newChild.Parent().RemoveChild(newChild);
                    }
                    newChild.Parent(this);
                }
                else
                {
                    _cmd.RemoveAt(index);
                }
                if (oldChild != null)
                {
                    oldChild.Parent(null);
                }
            }
        }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var cmd in _cmd)
            {
                sb.Append(cmd != null ? cmd.ToString() : "");
            }
            return sb.ToString();
        }
    }
}





