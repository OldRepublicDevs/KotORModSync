using System;
using KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode;
using KOTORModSync.Formats.NCS.NCSDecomp.Stack;

namespace KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode
{
    public class AUnaryModExp : ScriptNode, AExpression
    {
        private AVarRef _varRef;
        private string _op;
        private bool _prefix;
        private StackEntry _stackEntry;

        public AUnaryModExp(AVarRef varRef, string op, bool prefix)
        {
            SetVarRef(varRef);
            _op = op;
            _prefix = prefix;
        }

        public AVarRef GetVarRef()
        {
            return _varRef;
        }

        public void SetVarRef(AVarRef varRef)
        {
            _varRef = varRef;
            if (varRef != null)
            {
                ((AExpression)varRef).Parent((ScriptNode)(object)this);
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

        public bool IsPrefix()
        {
            return _prefix;
        }

        public bool GetPrefix()
        {
            return _prefix;
        }

        public void SetPrefix(bool prefix)
        {
            _prefix = prefix;
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

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AUnaryModExp.java:41
        // Original: return ExpressionFormatter.format(this);
        public override string ToString()
        {
            return ExpressionFormatter.Format(this);
        }

        public override void Close()
        {
            if (_varRef != null)
            {
                _varRef.Close();
            }
            _varRef = null;
            if (_stackEntry != null)
            {
                // StackEntry may have Close() method, but we check for it dynamically
                var entryType = _stackEntry.GetType();
                var closeMethod = entryType.GetMethod("Close", System.Type.EmptyTypes);
                if (closeMethod != null)
                {
                    closeMethod.Invoke(_stackEntry, null);
                }
            }
            _stackEntry = null;
        }
    }
}





