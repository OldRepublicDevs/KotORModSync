// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Const.java:13-71
// Original: public class Const extends StackEntry
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp.Utils;
using UtilsType = KOTORModSync.Formats.NCS.NCSDecomp.Utils.Type;

namespace KOTORModSync.Formats.NCS.NCSDecomp.Stack
{
    public class Const : StackEntry
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Const.java:14-19
        // Original: public static Const newConst(Type type, Long intValue)
        public static Const NewConst(UtilsType type, long intValue)
        {
            if (type.ByteValue() != 3)
            {
                throw new Exception("Invalid const type for int value: " + type);
            }
            return new IntConst(intValue);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Const.java:21-26
        // Original: public static Const newConst(Type type, Float floatValue)
        public static Const NewConst(UtilsType type, float floatValue)
        {
            if (type.ByteValue() != 4)
            {
                throw new Exception("Invalid const type for float value: " + type);
            }
            return new FloatConst(floatValue);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Const.java:28-33
        // Original: public static Const newConst(Type type, String stringValue)
        public static Const NewConst(UtilsType type, string stringValue)
        {
            if (type.ByteValue() != 5)
            {
                throw new Exception("Invalid const type for string value: " + type);
            }
            return new StringConst(stringValue);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Const.java:35-40
        // Original: public static Const newConst(Type type, Integer objectValue)
        public static Const NewConst(UtilsType type, int objectValue)
        {
            if (type.ByteValue() != 6)
            {
                throw new Exception("Invalid const type for object value: " + type);
            }
            return new ObjectConst(objectValue);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Const.java:43-44
        // Original: @Override public void removedFromStack(LocalStack<?> stack)
        public override void RemovedFromStack(LocalStack stack)
        {
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Const.java:47-48
        // Original: @Override public void addedToStack(LocalStack<?> stack)
        public override void AddedToStack(LocalStack stack)
        {
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Const.java:51-52
        // Original: @Override public void doneParse()
        public override void DoneParse()
        {
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Const.java:55-56
        // Original: @Override public void doneWithStack(LocalVarStack stack)
        public override void DoneWithStack(LocalVarStack stack)
        {
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Const.java:59-61
        // Original: @Override public String toString() { return ""; }
        public override string ToString()
        {
            return "";
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Const.java:64-70
        // Original: @Override public StackEntry getElement(int stackpos)
        public override StackEntry GetElement(int stackpos)
        {
            if (stackpos != 1)
            {
                throw new Exception("Position > 1 for const, not struct");
            }
            else
            {
                return this;
            }
        }
    }
}




