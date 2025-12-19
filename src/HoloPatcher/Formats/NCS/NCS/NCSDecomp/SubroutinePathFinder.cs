// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutinePathFinder.java:37-343
// Original: public class SubroutinePathFinder extends PrunedDepthFirstAdapter
using System;
using KOTORModSync.Formats.NCS.NCSDecomp;
using KOTORModSync.Formats.NCS.NCSDecomp.Analysis;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;

namespace KOTORModSync.Formats.NCS.NCSDecomp.Utils
{
    public class SubroutinePathFinder : PrunedDepthFirstAdapter
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutinePathFinder.java:38-46
        // Original: private NodeAnalysisData nodedata; private SubroutineAnalysisData subdata; private SubroutineState state; private boolean pathfailed; private boolean forcejump; private Hashtable<Integer, Integer> destinationcommands; private boolean limitretries; private int maxretry; private int retry;
        private NodeAnalysisData nodedata;
        private SubroutineAnalysisData subdata;
        private SubroutineState state;
        private bool pathfailed;
        private bool forcejump;
        private HashMap destinationcommands;
        private bool limitretries;
        private int maxretry;
        private int retry;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutinePathFinder.java:48-67
        // Original: public SubroutinePathFinder(SubroutineState state, NodeAnalysisData nodedata, SubroutineAnalysisData subdata, int pass) { ... }
        public SubroutinePathFinder(SubroutineState state, NodeAnalysisData nodedata, SubroutineAnalysisData subdata, int pass)
        {
            this.nodedata = nodedata;
            this.subdata = subdata;
            this.state = state;
            this.pathfailed = false;
            this.forcejump = false;
            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutinePathFinder.java:54
            // Original: this.limitretries = pass < 3;
            this.limitretries = pass < 3;
            switch (pass)
            {
                case 0:
                    {
                        this.maxretry = 10;
                        break;
                    }

                case 1:
                    {
                        this.maxretry = 15;
                        break;
                    }

                case 2:
                    {
                        this.maxretry = 25;
                        break;
                    }

                default:
                    {
                        this.maxretry = 9999;
                        break;
                    }
            }

            this.retry = 0;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutinePathFinder.java:72-77
        // Original: public void done() { this.nodedata = null; this.subdata = null; this.state = null; this.destinationcommands = null; }
        public virtual void Done()
        {
            this.nodedata = null;
            this.subdata = null;
            this.state = null;
            this.destinationcommands = null;
        }

        public override void CaseASubroutine(ASubroutine node)
        {
            this.InASubroutine(node);
            node.GetCommandBlock()?.Apply(this);
            node.GetReturn()?.Apply(this);
            this.OutASubroutine(node);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutinePathFinder.java:79-82
        // Original: @Override public void inASubroutine(ASubroutine node) { this.state.startPrototyping(); }
        public override void InASubroutine(ASubroutine node)
        {
            this.state.StartPrototyping();
        }

        public override void OutASubroutine(ASubroutine node)
        {
            // Path finder completed successfully - DoTypes will call StopPrototyping(true)
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutinePathFinder.java:84-116
        // Original: @Override public void caseACommandBlock(ACommandBlock node) { ... }
        public override void CaseACommandBlock(ACommandBlock node)
        {
            this.InACommandBlock(node);
            var cmdList = node.GetCmd();
            TypedLinkedList commands = new TypedLinkedList();
            foreach (var cmd in cmdList)
            {
                commands.Add(cmd);
            }
            this.SetupDestinationCommands(commands, node);
            int i = 0;

            while (i < commands.Count)
            {
                if (this.forcejump)
                {
                    int nextPos = this.state.GetCurrentDestination();
                    i = (int)this.destinationcommands[nextPos];
                    this.forcejump = false;
                }
                else if (this.pathfailed)
                {
                    int nextPos = this.state.SwitchDecision();
                    // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutinePathFinder.java:98
                    // Original: if (nextPos == -1 || this.limitretries && this.retry > this.maxretry) { ... }
                    // Note: Operator precedence: || has lower precedence than &&, so this is: nextPos == -1 || (this.limitretries && this.retry > this.maxretry)
                    if (nextPos == -1 || this.limitretries && this.retry > this.maxretry)
                    {
                        this.state.StopPrototyping(false);
                        return;
                    }

                    i = (int)this.destinationcommands[nextPos];
                    this.pathfailed = false;
                    this.retry++;
                }

                if (i < commands.Count)
                {
                    object cmd = commands[i];
                    if (cmd is PCmd pcmd)
                    {
                        pcmd.Apply(this);
                    }
                    i++;
                }
            }

            commands = null;
            this.OutACommandBlock(node);
        }

        public override void OutAConditionalJumpCommand(AConditionalJumpCommand node)
        {
            NodeUtils.GetNextCommand(node, this.nodedata);
            if (!this.nodedata.LogOrCode(node))
            {
                this.state.AddDecision(node, NodeUtils.GetJumpDestinationPos(node));
            }
        }

        public override void OutAJumpCommand(AJumpCommand node)
        {
            if (NodeUtils.GetJumpDestinationPos(node) < this.nodedata.GetPos(node))
            {
                this.pathfailed = true;
            }
            else
            {
                this.state.AddJump(node, NodeUtils.GetJumpDestinationPos(node));
                this.forcejump = true;
            }
        }

        public override void OutAJumpToSubroutine(AJumpToSubroutine node)
        {
            if (!this.subdata.IsPrototyped(NodeUtils.GetJumpDestinationPos(node), true))
            {
                this.pathfailed = true;
            }
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

        private void SetupDestinationCommands(TypedLinkedList commands, Node ast)
        {
            this.destinationcommands = new HashMap();
            ast.Apply(new AnonymousPrunedDepthFirstAdapter(this, commands));
        }

        private sealed class AnonymousPrunedDepthFirstAdapter : PrunedDepthFirstAdapter
        {
            public AnonymousPrunedDepthFirstAdapter(SubroutinePathFinder parent, TypedLinkedList commands)
            {
                this.parent = parent;
                this.commands = commands;
            }

            private readonly SubroutinePathFinder parent;
            private readonly TypedLinkedList commands;
            public override void OutAConditionalJumpCommand(AConditionalJumpCommand node)
            {
                int pos = NodeUtils.GetJumpDestinationPos(node);
                this.parent.destinationcommands.Put(pos, this.parent.GetCommandIndexByPos(pos, this.commands));
            }

            public override void OutAJumpCommand(AJumpCommand node)
            {
                int pos = NodeUtils.GetJumpDestinationPos(node);
                this.parent.destinationcommands.Put(pos, this.parent.GetCommandIndexByPos(pos, this.commands));
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

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutinePathFinder.java:272-288
        // Original: private int getCommandIndexByPos(int pos, LinkedList<PCmd> commands) { Node node = (Node)commands.get(0); int i; for (i = 1; i < commands.size() && this.nodedata.getPos(node) < pos; i++) { ... } if (this.nodedata.getPos(node) > pos) { throw new RuntimeException(...); } else { return i; } }
        private int GetCommandIndexByPos(int pos, TypedLinkedList commands)
        {
            Node node = (Node)commands[0];

            int i;
            for (i = 1; i < commands.Count && this.nodedata.GetPos(node) < pos; i++)
            {
                node = (Node)commands[i];
                if (this.nodedata.GetPos(node) == pos)
                {
                    break;
                }
            }

            if (this.nodedata.GetPos(node) > pos)
            {
                throw new Exception("Unable to locate a command with position " + pos);
            }
            else
            {
                return i;
            }
        }
    }
}




