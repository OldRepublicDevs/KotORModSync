// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AExpression.java:10-21
// Original: public interface AExpression { String toString(); ScriptNode parent(); void parent(ScriptNode var1); StackEntry stackentry(); void stackentry(StackEntry var1); }
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp.Stack;

namespace KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode
{
    public interface AExpression
    {
        string ToString();
        ScriptNode Parent();
        void Parent(ScriptNode p0);
        StackEntry Stackentry();
        void Stackentry(StackEntry p0);
    }
}




