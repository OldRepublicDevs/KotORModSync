// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/ObjectConst.java:13-34
// Original: public class ObjectConst extends Const { private Integer value; public ObjectConst(Integer value) { this.type = new Type((byte)6); this.value = value; this.size = 1; } public Integer value() { return this.value; } @Override public String toString() { if (this.value == 0) { return "OBJECT_SELF"; } else { return this.value == 1 ? "OBJECT_INVALID" : this.value.toString(); } } }
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp.Utils;
using UtilsType = KOTORModSync.Formats.NCS.NCSDecomp.Utils.Type;

namespace KOTORModSync.Formats.NCS.NCSDecomp.Stack
{
    public class ObjectConst : Const
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/ObjectConst.java:14-20
        // Original: private Integer value; public ObjectConst(Integer value) { this.type = new Type((byte)6); this.value = value; this.size = 1; }
        private int value;
        public ObjectConst(int value)
        {
            this.type = new UtilsType((byte)6);
            this.value = value;
            this.size = 1;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/ObjectConst.java:22-24
        // Original: public Integer value() { return this.value; }
        public virtual int Value()
        {
            return this.value;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/ObjectConst.java:26-33
        // Original: if (this.value == 0) { return "OBJECT_SELF"; } else { return this.value == 1 ? "OBJECT_INVALID" : this.value.toString(); }
        public override string ToString()
        {
            if (this.value == 0)
            {
                return "OBJECT_SELF";
            }
            else
            {
                return this.value == 1 ? "OBJECT_INVALID" : this.value.ToString();
            }
        }
    }
}




