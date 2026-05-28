// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/Type.java:12-427
// Original: public class Type
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace KOTORModSync.Formats.NCS.NCSDecomp.Utils
{
    public class Type
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/Type.java:13-38
        // Original: public static final byte VT_NONE = 0; ... public static final byte VT_INVALID = -1;
        public const sbyte VT_NONE = 0;
        public const sbyte VT_STACK = 1;
        public const sbyte VT_INTEGER = 3;
        public const sbyte VT_FLOAT = 4;
        public const sbyte VT_STRING = 5;
        public const sbyte VT_OBJECT = 6;
        public const sbyte VT_EFFECT = 16;
        public const sbyte VT_EVENT = 17;
        public const sbyte VT_LOCATION = 18;
        public const sbyte VT_TALENT = 19;
        public const sbyte VT_INTINT = 32;
        public const sbyte VT_FLOATFLOAT = 33;
        public const sbyte VT_OBJECTOBJECT = 34;
        public const sbyte VT_STRINGSTRING = 35;
        public const sbyte VT_STRUCTSTRUCT = 36;
        public const sbyte VT_INTFLOAT = 37;
        public const sbyte VT_FLOATINT = 38;
        public const sbyte VT_EFFECTEFFECT = 48;
        public const sbyte VT_EVENTEVENT = 49;
        public const sbyte VT_LOCLOC = 50;
        public const sbyte VT_TALTAL = 51;
        public const sbyte VT_VECTORVECTOR = 58;
        public const sbyte VT_VECTORFLOAT = 59;
        public const sbyte VT_FLOATVECTOR = 60;
        public const sbyte VT_VECTOR = -16;
        public const sbyte VT_STRUCT = -15;
        public const sbyte VT_INVALID = -1;
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/Type.java:40-41
        // Original: protected byte type; protected int size;
        protected sbyte type;
        protected int size;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/Type.java:43-46
        // Original: public Type(byte type) { this.type = type; this.size = 1; }
        public Type(byte type)
            : this(unchecked((sbyte)type))
        {
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/Type.java:43-46
        // Original: public Type(byte type) { this.type = type; this.size = 1; }
        public Type(sbyte type)
        {
            this.type = type;
            this.size = 1;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/Type.java:48-51
        // Original: public Type(String str) { this.type = decode(str); this.size = typeSize(this.type) / 4; }
        public Type(string str)
        {
            this.type = Decode(str);
            this.size = TypeSize(this.type) / 4;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/Type.java:53-55
        // Original: public static Type parseType(String str) { return new Type(str); }
        public static Type ParseType(string str)
        {
            return new Type(str);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/Type.java:57-59
        // Original: public void close() { }
        public virtual void Close()
        {
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/Type.java:60-62
        // Original: public byte byteValue() { return this.type; }
        public virtual sbyte ByteValue()
        {
            return this.type;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/Type.java:64-67
        // Original: @Override public String toString() { return toString(this.type); }
        public override string ToString()
        {
            return ToString(this.type);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/Type.java:69-71
        // Original: public static String toString(Type atype) { return toString(atype.type); }
        public static string ToString(Type atype)
        {
            return ToString(atype.type);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/Type.java:73-75
        // Original: public String toDeclString() { return this.toString(); }
        public virtual string ToDeclString()
        {
            return this.ToString();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/Type.java:77-79
        // Original: public int size()
        public virtual int Size()
        {
            return this.size;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/Type.java:81-83
        // Original: public boolean isTyped()
        public virtual bool IsTyped()
        {
            return this.type != -1;
        }

        public virtual string ToValueString()
        {
            return this.type.ToString();
        }

        protected static string ToString(sbyte type)
        {
            switch (type)
            {
                case VT_INTEGER:
                    {
                        return "int";
                    }

                case VT_FLOAT:
                    {
                        return "float";
                    }

                case VT_STRING:
                    {
                        return "string";
                    }

                case VT_OBJECT:
                    {
                        return "object";
                    }

                case VT_EFFECT:
                    {
                        return "effect";
                    }

                case VT_LOCATION:
                    {
                        return "location";
                    }

                case VT_TALENT:
                    {
                        return "talent";
                    }

                case VT_INTINT:
                    {
                        return "intint";
                    }

                case VT_FLOATFLOAT:
                    {
                        return "floatfloat";
                    }

                case VT_OBJECTOBJECT:
                    {
                        return "objectobject";
                    }

                case VT_STRINGSTRING:
                    {
                        return "stringstring";
                    }

                case VT_STRUCTSTRUCT:
                    {
                        return "structstruct";
                    }

                case VT_INTFLOAT:
                    {
                        return "intfloat";
                    }

                case VT_FLOATINT:
                    {
                        return "floatint";
                    }

                case VT_EFFECTEFFECT:
                    {
                        return "effecteffect";
                    }

                case VT_EVENTEVENT:
                    {
                        return "eventevent";
                    }

                case VT_LOCLOC:
                    {
                        return "locloc";
                    }

                case VT_TALTAL:
                    {
                        return "taltal";
                    }

                case VT_VECTORVECTOR:
                    {
                        return "vectorvector";
                    }

                case VT_VECTORFLOAT:
                    {
                        return "vectorfloat";
                    }

                case VT_FLOATVECTOR:
                    {
                        return "floatvector";
                    }

                case VT_NONE:
                    {
                        return "void";
                    }

                case VT_STACK:
                    {
                        return "stack";
                    }

                case VT_VECTOR:
                    {
                        return "vector";
                    }

                case VT_INVALID:
                    {
                        return "invalid";
                    }

                case VT_STRUCT:
                    {
                        return "struct";
                    }

                default:
                    {
                        return "unknown";
                    }
            }
        }

        private static sbyte Decode(string type)
        {
            if (type.Equals("void"))
            {
                return 0;
            }

            if (type.Equals("int"))
            {
                return 3;
            }

            if (type.Equals("float"))
            {
                return 4;
            }

            if (type.Equals("string"))
            {
                return 5;
            }

            if (type.Equals("object"))
            {
                return 6;
            }

            if (type.Equals("effect"))
            {
                return 16;
            }

            if (type.Equals("event"))
            {
                return 17;
            }

            if (type.Equals("location"))
            {
                return 18;
            }

            if (type.Equals("talent"))
            {
                return 19;
            }

            if (type.Equals("vector"))
            {
                return -16;
            }

            if (type.Equals("action"))
            {
                return 0;
            }

            if (type.Equals("INT"))
            {
                return 3;
            }

            if (type.Equals("OBJECT_ID"))
            {
                return 6;
            }

            throw new Exception("Attempted to get unknown type " + type);
        }

        public virtual int TypeSize()
        {
            return TypeSize(this.type);
        }

        public static int TypeSize(string type)
        {
            return TypeSize(Decode(type));
        }

        private static int TypeSize(sbyte type)
        {
            switch (type)
            {
                case VT_INTEGER:
                    {
                        return 4;
                    }

                case VT_FLOAT:
                    {
                        return 4;
                    }

                case VT_STRING:
                    {
                        return 4;
                    }

                case VT_OBJECT:
                    {
                        return 4;
                    }

                case VT_EFFECT:
                    {
                        return 4;
                    }

                case VT_LOCATION:
                    {
                        return 4;
                    }

                case VT_TALENT:
                    {
                        return 4;
                    }

                case VT_EVENT:
                    {
                        return 4;
                    }

                case VT_NONE:
                    {
                        return 0;
                    }

                case VT_VECTOR:
                    {
                        return 12;
                    }

                default:
                    {
                        throw new Exception("Unknown type code: " + type.ToString());
                    }
            }
        }

        public virtual Type GetElement(int pos)
        {
            if (pos != 1)
            {
                throw new Exception("Position > 1 for type, not struct");
            }

            return this;
        }

        public override bool Equals(object obj)
        {
            return typeof(Type).IsInstanceOfType(obj) && this.type == ((Type)obj).type;
        }

        public virtual bool Equals(sbyte type)
        {
            return this.type == type;
        }

        public virtual bool Equals(byte type)
        {
            return this.type == unchecked((sbyte)type);
        }

        public override int GetHashCode()
        {
            return this.type;
        }
    }
}




