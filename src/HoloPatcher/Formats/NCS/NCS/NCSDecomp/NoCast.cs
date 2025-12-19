// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/NoCast.java:10-25
// Original: public class NoCast implements Cast<Object>
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    public class NoCast : ICast
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/NoCast.java:11
        // Original: public static final NoCast instance = new NoCast();
        public static readonly NoCast instance;
        static NoCast()
        {
            instance = new NoCast();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/NoCast.java:13-15
        // Original: private NoCast() { }
        private NoCast()
        {
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/NoCast.java:17-23
        // Original: @Override public Object cast(Object o) { return o; }
        public virtual object Cast(object o)
        {
            return o;
        }
    }
}




