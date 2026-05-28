// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/Switchable.java:7-9
// Original: public interface Switchable { void apply(Switch var1); }
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    public interface Switchable
    {
        void Apply(Switch p0);
    }
}




