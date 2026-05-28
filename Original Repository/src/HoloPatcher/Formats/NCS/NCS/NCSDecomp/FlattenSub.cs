// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/FlattenSub.java:39-143
// Original: public class FlattenSub extends PrunedDepthFirstAdapter
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp;
using KOTORModSync.Formats.NCS.NCSDecomp.Analysis;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;

namespace KOTORModSync.Formats.NCS.NCSDecomp.Utils
{
    public class FlattenSub : PrunedDepthFirstAdapter
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/FlattenSub.java:40-44
        // Original: private ASubroutine sub; private boolean actionjumpfound; private int i; private LinkedList<PCmd> commands; private NodeAnalysisData nodedata;
        private ASubroutine sub;
        private bool actionjumpfound;
        private int i;
        private TypedLinkedList commands;
        private NodeAnalysisData nodedata;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/FlattenSub.java:46-50
        // Original: public FlattenSub(ASubroutine sub, NodeAnalysisData nodedata) { this.setSub(sub); this.actionjumpfound = false; this.nodedata = nodedata; }
        public FlattenSub(ASubroutine sub, NodeAnalysisData nodedata)
        {
            this.SetSub(sub);
            this.actionjumpfound = false;
            this.nodedata = nodedata;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/FlattenSub.java:52-56
        // Original: public void done() { this.sub = null; this.commands = null; this.nodedata = null; }
        public virtual void Done()
        {
            this.sub = null;
            this.commands = null;
            this.nodedata = null;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/FlattenSub.java:58-60
        // Original: public void setSub(ASubroutine sub) { this.sub = sub; }
        public virtual void SetSub(ASubroutine sub)
        {
            this.sub = sub;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/FlattenSub.java:63-75
        // Original: @Override public void caseACommandBlock(ACommandBlock node) { ... }
        public override void CaseACommandBlock(ACommandBlock node)
        {
            var cmdList = node.GetCmd();
            this.commands = new TypedLinkedList();
            foreach (var cmd in cmdList)
            {
                this.commands.Add(cmd);
            }
            this.i = 0;
            while (this.i < this.commands.Count)
            {
                ((Node)this.commands[this.i]).Apply(this);
                if (this.actionjumpfound)
                {
                    this.actionjumpfound = false;
                }
                else
                {
                    this.i++;
                }
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/FlattenSub.java:78-100
        // Original: @Override public void caseAActionJumpCmd(AActionJumpCmd node) { ... }
        public override void CaseAActionJumpCmd(AActionJumpCmd node)
        {
            AStoreStateCommand sscommand = (AStoreStateCommand)node.GetStoreStateCommand();
            AJumpCommand jmpcommand = (AJumpCommand)node.GetJumpCommand();
            ACommandBlock cmdblock = (ACommandBlock)node.GetCommandBlock();
            AReturn rtn = (AReturn)node.GetReturn();
            AStoreStateCmd sscmd = new AStoreStateCmd(sscommand);
            AJumpCmd jmpcmd = new AJumpCmd(jmpcommand);
            AReturnCmd rtncmd = new AReturnCmd(rtn);
            this.nodedata.SetPos(sscmd, this.nodedata.GetPos(sscommand));
            this.nodedata.SetPos(jmpcmd, this.nodedata.GetPos(jmpcommand));
            this.nodedata.SetPos(rtncmd, this.nodedata.GetPos(rtn));
            int j = this.i;
            this.commands[j++] = sscmd;
            this.commands.Add(j++, jmpcmd);
            var cmdList2 = cmdblock.GetCmd();
            TypedLinkedList subcmds = new TypedLinkedList();
            foreach (var cmd in cmdList2)
            {
                subcmds.Add(cmd);
            }
            while (subcmds.Count > 0)
            {
                this.commands.Add(j++, subcmds.Remove(0));
            }

            this.commands.Add(j++, rtncmd);
            subcmds = null;
        }

        public override void CaseAAddVarCmd(AAddVarCmd node)
        {
        }

        public override void CaseAConstCmd(AConstCmd node)
        {
        }

        public override void CaseACopydownspCmd(ACopydownspCmd node)
        {
        }

        public override void CaseACopytopspCmd(ACopytopspCmd node)
        {
        }

        public override void CaseACopydownbpCmd(ACopydownbpCmd node)
        {
        }

        public override void CaseACopytopbpCmd(ACopytopbpCmd node)
        {
        }

        public override void CaseAMovespCmd(AMovespCmd node)
        {
        }

        public override void CaseALogiiCmd(ALogiiCmd node)
        {
        }

        public override void CaseAUnaryCmd(AUnaryCmd node)
        {
        }

        public override void CaseABinaryCmd(ABinaryCmd node)
        {
        }

        public override void CaseADestructCmd(ADestructCmd node)
        {
        }

        public override void CaseABpCmd(ABpCmd node)
        {
        }

        public override void CaseAActionCmd(AActionCmd node)
        {
        }

        public override void CaseAStackOpCmd(AStackOpCmd node)
        {
        }
    }
}




