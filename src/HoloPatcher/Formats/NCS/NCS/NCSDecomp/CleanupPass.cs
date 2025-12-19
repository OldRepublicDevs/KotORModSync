// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptutils/CleanupPass.java:37-191
// Original: public class CleanupPass
using System;
using System.Collections.Generic;
using KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode;
using KOTORModSync.Formats.NCS.NCSDecomp.Stack;
using KOTORModSync.Formats.NCS.NCSDecomp.Utils;
namespace KOTORModSync.Formats.NCS.NCSDecomp.Scriptutils
{
    public class CleanupPass
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptutils/CleanupPass.java:38-41
        // Original: private ASub root; private NodeAnalysisData nodedata; private SubroutineAnalysisData subdata; private SubScriptState state;
        private ASub root;
        private NodeAnalysisData nodedata;
        private SubroutineAnalysisData subdata;
        private SubScriptState state;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptutils/CleanupPass.java:43-48
        // Original: public CleanupPass(ASub root, NodeAnalysisData nodedata, SubroutineAnalysisData subdata, SubScriptState state) { this.root = root; this.nodedata = nodedata; this.subdata = subdata; this.state = state; }
        public CleanupPass(ASub root, NodeAnalysisData nodedata, SubroutineAnalysisData subdata, SubScriptState state)
        {
            this.root = root;
            this.nodedata = nodedata;
            this.subdata = subdata;
            this.state = state;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptutils/CleanupPass.java:50-53
        // Original: public void apply() { this.checkSubCodeBlock(); this.apply(this.root); }
        public virtual void Apply()
        {
            this.CheckSubCodeBlock();
            this.Apply(this.root);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptutils/CleanupPass.java:55-60
        // Original: public void done() { this.root = null; this.nodedata = null; this.subdata = null; this.state = null; }
        public virtual void Done()
        {
            this.root = null;
            this.nodedata = null;
            this.subdata = null;
            this.state = null;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptutils/CleanupPass.java:62-73
        // Original: private void checkSubCodeBlock() { try { if (this.root.size() == 1 && ACodeBlock.class.isInstance(this.root.getLastChild())) { ACodeBlock block = (ACodeBlock)this.root.removeLastChild(); List<ScriptNode> children = block.removeChildren(); this.root.addChildren(children); } } finally { ACodeBlock block = null; List<ScriptNode> children = null; } }
        private void CheckSubCodeBlock()
        {
            try
            {
                if (this.root.Size() == 1 && typeof(ACodeBlock).IsInstanceOfType(this.root.GetLastChild()))
                {
                    ACodeBlock block = (ACodeBlock)this.root.RemoveLastChild();
                    List<ScriptNode.ScriptNode> children = block.RemoveChildren();
                    this.root.AddChildren(children);
                }
            }
            finally
            {
                ACodeBlock block = null;
                List<ScriptNode.ScriptNode> children = null;
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptutils/CleanupPass.java:75-164
        // Original: private void apply(ScriptRootNode rootnode) { try { LinkedList children = rootnode.getChildren(); ListIterator it = children.listIterator(); ... } finally { ... } }
        private void Apply(ScriptRootNode rootnode)
        {
            try
            {
                List<ScriptNode.ScriptNode> children = rootnode.GetChildren();
                ListIterator it = children.ListIterator();

                while (it.HasNext())
                {
                    ScriptNode.ScriptNode node1 = (ScriptNode.ScriptNode)it.Next();
                    if (typeof(AVarDecl).IsInstanceOfType(node1))
                    {
                        AVarDecl decl = (AVarDecl)node1;
                        if (decl.Exp() == null && it.HasNext())
                        {
                            ScriptNode.ScriptNode maybeAssign = (ScriptNode.ScriptNode)it.Next();
                            if (typeof(AExpressionStatement).IsInstanceOfType(maybeAssign)
                                && typeof(AModifyExp).IsInstanceOfType(((AExpressionStatement)maybeAssign).GetExp()))
                            {
                                AModifyExp modexp = (AModifyExp)((AExpressionStatement)maybeAssign).GetExp();
                                if (modexp.GetVarRef().Var() == decl.GetVarVar())
                                {
                                    decl.InitializeExp(modexp.GetExpression());
                                    it.Remove(); // drop the now-merged assignment statement
                                }
                                else
                                {
                                    it.Previous();
                                }
                            }
                            else
                            {
                                it.Previous();
                            }
                        }
                    }
                    if (typeof(AVarDecl).IsInstanceOfType(node1))
                    {
                        Variable var = ((AVarDecl)node1).GetVarVar();
                        if (var != null && var.IsStruct())
                        {
                            VarStruct structx = ((AVarDecl)node1).GetVarVar().Varstruct();
                            AVarDecl structdecx = new AVarDecl(structx);
                            if (it.HasNext())
                            {
                                node1 = (ScriptNode.ScriptNode)it.Next();
                            }
                            else
                            {
                                node1 = null;
                            }

                            while (typeof(AVarDecl).IsInstanceOfType(node1) && structx.Equals(((AVarDecl)node1).GetVarVar().Varstruct()))
                            {
                                it.Remove();
                                node1.Parent(null);
                                if (it.HasNext())
                                {
                                    node1 = (ScriptNode.ScriptNode)it.Next();
                                }
                                else
                                {
                                    node1 = null;
                                }
                            }

                            it.Previous();
                            if (node1 != null)
                            {
                                it.Previous();
                            }

                            node1 = (ScriptNode.ScriptNode)it.Next();
                            structdecx.Parent(node1.Parent());
                            it.Set(structdecx);
                            node1 = structdecx;
                        }
                    }

                    // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptutils/CleanupPass.java:133-137
                    // Original: if (this.isDanglingExpression(node1)) { AExpressionStatement expstm = new AExpressionStatement((AExpression)node1); expstm.parent(rootnode); it.set(expstm); }
                    // Handle AVarDecl with isFcnReturn=true and unused variable - convert to statement expression
                    if (typeof(AVarDecl).IsInstanceOfType(node1))
                    {
                        AVarDecl decl = (AVarDecl)node1;
                        if (decl.IsFcnReturn() && decl.GetExp() != null)
                        {
                            // Function return value is unused - extract expression and convert to statement
                            AExpression expr = decl.RemoveExp();
                            AExpressionStatement expstm = new AExpressionStatement(expr);
                            expstm.Parent(rootnode);
                            it.Set(expstm);
                        }
                    }
                    else if (this.IsDanglingExpression(node1))
                    {
                        AExpressionStatement expstm = new AExpressionStatement((AExpression)node1);
                        expstm.Parent(rootnode);
                        it.Set(expstm);
                    }

                    it.Previous();
                    it.Next();
                    if (typeof(ScriptRootNode).IsInstanceOfType(node1))
                    {
                        this.Apply((ScriptRootNode)node1);
                    }

                    ASwitchCase acase = null;
                    if (typeof(ASwitch).IsInstanceOfType(node1))
                    {
                        while ((acase = ((ASwitch)node1).GetNextCase(acase)) != null)
                        {
                            this.Apply(acase);
                        }
                    }
                }
            }
            finally
            {
                List<ScriptNode.ScriptNode> children = null;
                ListIterator it = null;
                ScriptNode.ScriptNode node1x = null;
                Variable var = null;
                VarStruct structx = null;
                AVarDecl structdecx = null;
                ScriptNode.ScriptNode node2 = null;
                AModifyExp modexp = null;
                AExpressionStatement expstm = null;
                ASwitchCase acase = null;
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptutils/CleanupPass.java:166-168
        // Original: private boolean isDanglingExpression(ScriptNode node) { return AExpression.class.isInstance(node); }
        private bool IsDanglingExpression(ScriptNode.ScriptNode node)
        {
            return typeof(AExpression).IsInstanceOfType(node);
        }
    }
}




