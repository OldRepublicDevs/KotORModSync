// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/IntConst.java:13-30
// Original: public class IntConst extends Const { private Long value; public IntConst(Long value) { this.type = new Type((byte)3); this.value = value; this.size = 1; } public Long value() { return this.value; } @Override public String toString() { return this.value == Long.parseLong("FFFFFFFF", 16) ? "0xFFFFFFFF" : this.value.toString(); } }
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp.Utils;
using UtilsType = KOTORModSync.Formats.NCS.NCSDecomp.Utils.Type;

namespace KOTORModSync.Formats.NCS.NCSDecomp.Stack
{
    public class IntConst : Const
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/IntConst.java:14-20
        // Original: private Long value; public IntConst(Long value) { this.type = new Type((byte)3); this.value = value; this.size = 1; }
        private long value;
        public IntConst(long value)
        {
            this.type = new UtilsType((byte)3);
            this.value = value;
            this.size = 1;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/IntConst.java:22-24
        // Original: public Long value() { return this.value; }
        public virtual long Value()
        {
            return this.value;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/IntConst.java:26-29
        // Original: return this.value == Long.parseLong("FFFFFFFF", 16) ? "0xFFFFFFFF" : this.value.toString();
        public override string ToString()
        {
            return this.value == Convert.ToInt64("FFFFFFFF", 16) ? "0xFFFFFFFF" : this.value.ToString();
        }
    }
}




