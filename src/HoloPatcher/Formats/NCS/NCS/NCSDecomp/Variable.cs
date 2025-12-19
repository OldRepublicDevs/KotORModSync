// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Variable.java:14-294
// Original: public class Variable extends StackEntry implements Comparable<Variable>
using System;
using System.Collections.Generic;
using KOTORModSync.Formats.NCS.NCSDecomp.Utils;
using Comparable = System.IComparable;
using UtilsType = KOTORModSync.Formats.NCS.NCSDecomp.Utils.Type;
namespace KOTORModSync.Formats.NCS.NCSDecomp.Stack
{
    public class Variable : StackEntry, Comparable
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Variable.java:15-17
        // Original: protected static final byte FCN_NORMAL = 0; protected static final byte FCN_RETURN = 1; protected static final byte FCN_PARAM = 2;
        protected static readonly byte FCN_NORMAL = 0;
        protected static readonly byte FCN_RETURN = 1;
        protected static readonly byte FCN_PARAM = 2;
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Variable.java:18-22
        // Original: private Hashtable<LocalStack<?>, Integer> stackcounts; protected String name; protected boolean assigned; protected VarStruct varstruct; protected byte function;
        private Dictionary<object, object> stackcounts;
        protected string name;
        protected bool assigned;
        protected VarStruct varstruct;
        protected byte function;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Variable.java:24-31
        // Original: public Variable(Type type) { this.type = type; this.varstruct = null; this.assigned = false; this.size = 1; this.function = 0; this.stackcounts = new Hashtable<>(1); }
        public Variable(UtilsType type)
        {
            this.type = type;
            this.varstruct = null;
            this.assigned = false;
            this.size = 1;
            this.function = 0;
            this.stackcounts = new Dictionary<object, object>();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Variable.java:33-35
        // Original: public Variable(byte type) { this(new Type(type)); }
        public Variable(byte type) : this(new UtilsType(type))
        {
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Variable.java:38-42
        // Original: @Override public void close()
        public override void Close()
        {
            base.Close();
            this.stackcounts = null;
            this.varstruct = null;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Variable.java:45-47
        // Original: @Override public void doneParse() { this.stackcounts = null; }
        public override void DoneParse()
        {
            this.stackcounts = null;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Variable.java:50-52
        // Original: @Override public void doneWithStack(LocalVarStack stack) { this.stackcounts.remove(stack); }
        public override void DoneWithStack(LocalVarStack stack)
        {
            this.stackcounts.Remove(stack);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Variable.java:54-60
        // Original: public void isReturn(boolean isreturn) { if (isreturn) { this.function = 1; } else { this.function = 0; } }
        public virtual void IsReturn(bool isreturn)
        {
            if (isreturn)
            {
                this.function = 1;
            }
            else
            {
                this.function = 0;
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Variable.java:62-68
        // Original: public void isParam(boolean isparam) { if (isparam) { this.function = 2; } else { this.function = 0; } }
        public virtual void IsParam(bool isparam)
        {
            if (isparam)
            {
                this.function = 2;
            }
            else
            {
                this.function = 0;
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Variable.java:70-72
        // Original: public boolean isReturn() { return this.function == 1; }
        public virtual bool IsReturn()
        {
            return this.function == 1;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Variable.java:74-76
        // Original: public boolean isParam() { return this.function == 2; }
        public virtual bool IsParam()
        {
            return this.function == 2;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Variable.java:78-80
        // Original: public void assigned() { this.assigned = true; }
        public virtual void Assigned()
        {
            this.assigned = true;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Variable.java:82-84
        // Original: public boolean isAssigned() { return this.assigned; }
        public virtual bool IsAssigned()
        {
            return this.assigned;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Variable.java:86-88
        // Original: public boolean isStruct() { return this.varstruct != null; }
        public virtual bool IsStruct()
        {
            return this.varstruct != null;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Variable.java:90-92
        // Original: public void varstruct(VarStruct varstruct) { this.varstruct = varstruct; }
        public virtual void Varstruct(VarStruct varstruct)
        {
            this.varstruct = varstruct;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Variable.java:94-96
        // Original: public VarStruct varstruct() { return this.varstruct; }
        public virtual VarStruct Varstruct()
        {
            return this.varstruct;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Variable.java:98-106
        // Original: @Override public void addedToStack(LocalStack<?> stack) { ... }
        public override void AddedToStack(LocalStack stack)
        {
            object countObj;
            this.stackcounts.TryGetValue(stack, out countObj);
            if (countObj == null)
            {
                this.stackcounts[stack] = 1;
            }
            else
            {
                int count = (int)countObj;
                this.stackcounts[stack] = count + 1;
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Variable.java:108-116
        // Original: @Override public void removedFromStack(LocalStack<?> stack) { ... }
        public override void RemovedFromStack(LocalStack stack)
        {
            object countObj;
            this.stackcounts.TryGetValue(stack, out countObj);
            if (countObj == null)
            {
                this.stackcounts.Remove(stack);
            }
            else
            {
                int count = (int)countObj;
                if (count == 0)
                {
                    this.stackcounts.Remove(stack);
                }
                else
                {
                    this.stackcounts[stack] = count - 1;
                }
            }
        }

        public virtual bool IsPlaceholder(LocalStack stack)
        {
            object countObj;
            this.stackcounts.TryGetValue(stack, out countObj);
            if (countObj == null)
            {
                return true;
            }

            int count = (int)countObj;
            return count == 0 && !this.assigned;
        }

        public virtual bool IsOnStack(LocalStack stack)
        {
            object countObj;
            this.stackcounts.TryGetValue(stack, out countObj);
            if (countObj == null)
            {
                return false;
            }

            int count = (int)countObj;
            return count > 0;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Variable.java:128-130
        // Original: public void name(String prefix, byte hint) { this.name = prefix + this.type.toString() + Byte.toString(hint); }
        public virtual void Name(string prefix, byte hint)
        {
            this.name = prefix + this.type.ToString() + hint.ToString();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Variable.java:132-134
        // Original: public void name(String infix, int hint) { this.name = this.type.toString() + infix + Integer.toString(hint); }
        public virtual void Name(string infix, int hint)
        {
            this.name = this.type.ToString() + infix + hint.ToString();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Variable.java:136-138
        // Original: public void name(String name) { this.name = name; }
        public virtual void Name(string name)
        {
            this.name = name;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Variable.java:141-147
        // Original: @Override public StackEntry getElement(int stackpos) { if (stackpos != 1) { throw new RuntimeException("Position > 1 for var, not struct"); } else { return this; } }
        public override StackEntry GetElement(int stackpos)
        {
            if (stackpos != 1)
            {
                throw new Exception("Position > 1 for var, not struct");
            }
            else
            {
                return this;
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Variable.java:149-151
        // Original: public String toDebugString() { return "type: " + this.type + " name: " + this.name + " assigned: " + Boolean.toString(this.assigned); }
        public virtual string ToDebugString()
        {
            return "type: " + this.type + " name: " + this.name + " assigned: " + this.assigned.ToString();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Variable.java:154-161
        // Original: @Override public String toString() { if (this.varstruct != null) { this.varstruct.updateNames(); return this.varstruct.name() + "." + this.name; } else { return this.name; } }
        public override string ToString()
        {
            if (this.varstruct != null)
            {
                this.varstruct.UpdateNames();
                return this.varstruct.Name() + "." + this.name;
            }

            return this.name;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Variable.java:163-165
        // Original: public String toDeclString() { return this.type + " " + this.name; }
        public virtual string ToDeclString()
        {
            return this.type + " " + this.name;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Variable.java:168-178
        // Original: @Override public int compareTo(Variable o) throws ClassCastException { if (o == null) { throw new NullPointerException(); } else if (this == o) { return 0; } else if (this.name == null) { return -1; } else { return o.name == null ? 1 : this.name.compareTo(o.name); } }
        public virtual int CompareTo(object o)
        {
            if (o == null)
            {
                throw new NullReferenceException();
            }
            else if (!typeof(Variable).IsInstanceOfType(o))
            {
                throw new InvalidCastException();
            }
            else if (this == o)
            {
                return 0;
            }
            else if (this.name == null)
            {
                return -1;
            }
            else
            {
                Variable other = (Variable)o;
                return other.name == null ? 1 : this.name.CompareTo(other.name);
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Variable.java:180-185
        // Original: public void stackWasCloned(LocalStack<?> oldstack, LocalStack<?> newstack) { Integer count = this.stackcounts.get(oldstack); if (count != null && count > 0) { this.stackcounts.put(newstack, count); } }
        public virtual void StackWasCloned(LocalStack oldstack, LocalStack newstack)
        {
            object countObj;
            this.stackcounts.TryGetValue(oldstack, out countObj);
            if (countObj != null && (int)countObj > 0)
            {
                int count = (int)countObj;
                this.stackcounts[newstack] = count;
            }
        }
    }
}




