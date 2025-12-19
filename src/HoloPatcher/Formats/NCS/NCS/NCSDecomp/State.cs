// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/parser/State.java:9-18
// Original: final class State
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace KOTORModSync.Formats.NCS.NCSDecomp.Parser
{
    // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/parser/State.java:9-18
    // Original: final class State { int state; Object node; State(int state, Object node) { this.state = state; this.node = node; } }
    sealed class State
    {
        internal int state;
        internal object node;
        internal State(int state, object node)
        {
            this.state = state;
            this.node = node;
        }
    }
}




