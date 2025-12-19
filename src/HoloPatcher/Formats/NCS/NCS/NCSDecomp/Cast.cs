// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/Cast.java:12-23
// Original: public interface Cast<T> extends Serializable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/Cast.java:12-23
    // Original: public interface Cast<T> extends Serializable { T cast(Object o); }
    public interface ICast
    {
        object Cast(object p0);
    }

    // Note: C# implementation uses abstract class instead of interface to match usage patterns
    public abstract class Cast : ICast
    {
        object ICast.Cast(object p0)
        {
            return CastInternal(p0);
        }

        public abstract object CastInternal(object p0);
    }
}




