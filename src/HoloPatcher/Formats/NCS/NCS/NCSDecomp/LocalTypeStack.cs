// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalTypeStack.java:16-154
// Original: public class LocalTypeStack extends LocalStack<Type>
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp.Utils;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;
using JavaSystem = KOTORModSync.Formats.NCS.NCSDecomp.JavaSystem;
using UtilsType = KOTORModSync.Formats.NCS.NCSDecomp.Utils.Type;
namespace KOTORModSync.Formats.NCS.NCSDecomp.Stack
{
    public class LocalTypeStack : LocalStack
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalTypeStack.java:17-19
        // Original: public void push(Type type) { this.stack.addFirst(type); }
        public virtual void Push(UtilsType type)
        {
            this.stack.AddFirst(type);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalTypeStack.java:21-38
        // Original: public Type get(int offset) { ... return new Type((byte)-1); }
        public virtual UtilsType Get(int offset)
        {
            ListIterator it = this.stack.ListIterator();
            int pos = 0;

            while (it.HasNext())
            {
                UtilsType type = (UtilsType)it.Next();
                pos += type.Size();
                if (pos > offset)
                {
                    return type.GetElement(pos - offset + 1);
                }

                if (pos == offset)
                {
                    return type.GetElement(1);
                }
            }

            return new UtilsType(unchecked((byte)(-1)));
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalTypeStack.java:40-64
        // Original: public Type get(int offset, SubroutineState state) { ... }
        public virtual UtilsType Get(int offset, SubroutineState state)
        {
            ListIterator it = this.stack.ListIterator();
            int pos = 0;

            while (it.HasNext())
            {
                UtilsType type = (UtilsType)it.Next();
                pos += type.Size();
                if (pos > offset)
                {
                    return type.GetElement(pos - offset + 1);
                }

                if (pos == offset)
                {
                    return type.GetElement(1);
                }
            }

            if (state.IsPrototyped())
            {
                UtilsType typex = state.GetParamType(offset - pos);
                if (!typex.Equals((byte)0))
                {
                    return typex;
                }
            }

            return new UtilsType(unchecked((byte)(-1)));
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalTypeStack.java:66-71
        // Original: public void remove(int count) { int actualCount = Math.min(count, this.stack.size()); for (int i = 0; i < actualCount; i++) { this.stack.removeFirst(); } }
        public virtual void Remove(int count)
        {
            int actualCount = Math.Min(count, this.stack.Count);
            for (int i = 0; i < actualCount; i++)
            {
                this.stack.RemoveFirst();
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalTypeStack.java:73-82
        // Original: public void removeParams(int count, SubroutineState state) { LinkedList<Type> params = new LinkedList<>(); for (int i = 0; i < count; i++) { Type type = this.stack.isEmpty() ? new Type((byte)-1) : this.stack.removeFirst(); params.addFirst(type); } state.updateParams(params); }
        public virtual void RemoveParams(int count, SubroutineState state)
        {
            LinkedList @params = new LinkedList();

            for (int i = 0; i < count; i++)
            {
                UtilsType type = this.stack.Count == 0 ? new UtilsType(unchecked((byte)(-1))) : (UtilsType)this.stack.RemoveFirst();
                @params.AddFirst(type);
            }

            state.UpdateParams(@params);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalTypeStack.java:84-99
        // Original: public int removePrototyping(int count) { ... }
        public virtual int RemovePrototyping(int count)
        {
            int @params = 0;
            int i = 0;

            while (i < count)
            {
                if (this.stack.Count == 0)
                {
                    @params++;
                    i++;
                }
                else
                {
                    UtilsType type = (UtilsType)this.stack.RemoveFirst();
                    i += type.Size();
                }
            }

            return @params;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalTypeStack.java:101-107
        // Original: public void remove(int start, int count) { int loc = start - 1; for (int i = 0; i < count; i++) { this.stack.remove(loc); } }
        public virtual void Remove(int start, int count)
        {
            int loc = start - 1;

            for (int i = 0; i < count; i++)
            {
                this.stack.Remove(loc);
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalTypeStack.java:109-122
        // Original: @Override public String toString() { ... }
        public override string ToString()
        {
            string newline = JavaSystem.GetProperty("line.separator");
            StringBuilder buffer = new StringBuilder();
            int max = this.stack.Count;
            buffer.Append("---stack, size " + max.ToString() + "---" + newline);

            for (int i = 1; i <= max; i++)
            {
                UtilsType type = (UtilsType)this.stack[max - i];
                buffer.Append("-->" + i.ToString() + " is type " + type + newline);
            }

            return buffer.ToString();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalTypeStack.java:124-129
        // Original: @Override public LocalTypeStack clone() { LocalTypeStack newStack = new LocalTypeStack(); newStack.stack = new LinkedList<>(this.stack); return newStack; }
        public override object Clone()
        {
            LocalTypeStack newStack = new LocalTypeStack();
            newStack.stack = new LinkedList();
            var it = this.stack.Iterator();
            while (it.HasNext())
            {
                newStack.stack.Add(it.Next());
            }
            return newStack;
        }
    }
}




