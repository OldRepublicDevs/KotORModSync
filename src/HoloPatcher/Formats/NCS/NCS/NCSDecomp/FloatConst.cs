// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/FloatConst.java:13-43
// Original: public class FloatConst extends Const { private Float value; public FloatConst(Float value) { this.type = new Type((byte)4); this.value = value; this.size = 1; } public Float value() { return this.value; } @Override public String toString() { java.text.DecimalFormat df = new java.text.DecimalFormat("0.0##############"); df.setMaximumFractionDigits(15); df.setMinimumFractionDigits(0); df.setGroupingUsed(false); String result = df.format(this.value); if (result.indexOf('.') == -1) { result = result + ".0"; } return result; } }
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp.Utils;
using UtilsType = KOTORModSync.Formats.NCS.NCSDecomp.Utils.Type;

namespace KOTORModSync.Formats.NCS.NCSDecomp.Stack
{
    public class FloatConst : Const
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/FloatConst.java:14-20
        // Original: private Float value; public FloatConst(Float value) { this.type = new Type((byte)4); this.value = value; this.size = 1; }
        private float value;
        public FloatConst(float value)
        {
            this.type = new UtilsType((byte)4);
            this.value = value;
            this.size = 1;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/FloatConst.java:22-24
        // Original: public Float value() { return this.value; }
        public virtual float Value()
        {
            return this.value;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/FloatConst.java:26-42
        // Original: Format float to avoid scientific notation. Use DecimalFormat to ensure decimal notation, not scientific. Ensure we have at least one digit after the decimal point for whole-number floats.
        public override string ToString()
        {
            // Format float to avoid scientific notation (E- or E+) which the lexer/compiler doesn't support well
            // Use custom formatting to ensure we get decimal notation, not scientific
            string result = this.value.ToString("0.0###############", CultureInfo.InvariantCulture);
            // Ensure we have at least one digit after the decimal point for whole-number floats
            // This is critical: 5.0 must be output as "5.0" not "5" so the compiler knows it's a float
            if (result.IndexOf('.') == -1)
            {
                // Whole number - add .0 suffix to ensure it's treated as a float
                result = result + ".0";
            }
            return result;
        }
    }
}




