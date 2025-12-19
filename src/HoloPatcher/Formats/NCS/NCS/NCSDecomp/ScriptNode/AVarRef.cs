// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AVarRef.java:13-68
// Original: public class AVarRef extends ScriptNode implements AExpression
using KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode;
using KOTORModSync.Formats.NCS.NCSDecomp.Stack;
using KOTORModSync.Formats.NCS.NCSDecomp.Utils;
using UtilsType = KOTORModSync.Formats.NCS.NCSDecomp.Utils.Type;

namespace KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode
{
    public class AVarRef : ScriptNode, AExpression
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AVarRef.java:14
        // Original: private Variable var;
        private Variable var;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AVarRef.java:16-18
        // Original: public AVarRef(Variable var) { this.var(var); }
        public AVarRef(Variable var)
        {
            this.Var(var);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AVarRef.java:20-22
        // Original: public AVarRef(VarStruct struct) { this.var(struct); }
        public AVarRef(VarStruct structVar)
        {
            this.Var(structVar);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AVarRef.java:24-26
        // Original: public Type type() { return this.var.type(); }
        public UtilsType Type()
        {
            return this.var.Type();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AVarRef.java:28-30
        // Original: public Variable var() { return this.var; }
        public Variable Var()
        {
            return this.var;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AVarRef.java:32-34
        // Original: public void var(Variable var) { this.var = var; }
        public void Var(Variable var)
        {
            this.var = var;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AVarRef.java:36-42
        // Original: public void chooseStructElement(Variable var) { if (VarStruct.class.isInstance(this.var) && ((VarStruct)this.var).contains(var)) { this.var = var; } else { throw new RuntimeException("Attempted to select a struct element not in struct"); } }
        public void ChooseStructElement(Variable var)
        {
            if (this.var is VarStruct varStruct && varStruct.Contains(var))
            {
                this.var = var;
            }
            else
            {
                throw new System.Exception("Attempted to select a struct element not in struct");
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AVarRef.java:44-47
        // Original: @Override public String toString() { return this.var.toString(); }
        public override string ToString()
        {
            return this.var.ToString();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AVarRef.java:49-52
        // Original: @Override public StackEntry stackentry() { return this.var; }
        public StackEntry Stackentry()
        {
            return this.var;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AVarRef.java:54-57
        // Original: @Override public void stackentry(StackEntry stackentry) { this.var((Variable)stackentry); }
        public void Stackentry(StackEntry stackentry)
        {
            this.Var((Variable)stackentry);
        }

        ScriptNode AExpression.Parent() => (ScriptNode)(object)base.Parent();
        void AExpression.Parent(ScriptNode p0) => base.Parent((ScriptNode)(object)p0);

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AVarRef.java:59-67
        // Original: @Override public void close() { super.close(); if (this.var != null) { this.var.close(); } this.var = null; }
        public override void Close()
        {
            base.Close();
            if (this.var != null)
            {
                this.var.Close();
            }
            this.var = null;
        }
    }
}




