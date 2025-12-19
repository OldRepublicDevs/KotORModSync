// 
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
    // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalVarStack.java:18-225
    // Original: public class LocalVarStack extends LocalStack<StackEntry>
    public class LocalVarStack : LocalStack
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalVarStack.java:19
        // Original: private int placeholderCounter = 0;
        private int placeholderCounter = 0;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalVarStack.java:20-31
        // Original: @Override public void close()
        public override void Close()
        {
            if (this.stack != null)
            {
                ListIterator it = this.stack.ListIterator();
                while (it.HasNext())
                {
                    object next = it.Next();
                    if (next is StackEntry entry)
                    {
                        entry.Close();
                    }
                }
            }

            base.Close();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalVarStack.java:33-43
        // Original: public void doneParse() { if (this.stack != null) { ListIterator<StackEntry> it = this.stack.listIterator(); while (it.hasNext()) { it.next().doneParse(); } this.stack = null; } }
        public virtual void DoneParse()
        {
            if (this.stack != null)
            {
                ListIterator it = this.stack.ListIterator();
                while (it.HasNext())
                {
                    object next = it.Next();
                    if (next is StackEntry entry)
                    {
                        entry.DoneParse();
                    }
                }
            }

            this.stack = null;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalVarStack.java:45-54
        // Original: public void doneWithStack() { if (this.stack != null) { ListIterator<StackEntry> it = this.stack.listIterator(); while (it.hasNext()) { it.next().doneWithStack(this); } this.stack = null; } }
        public virtual void DoneWithStack()
        {
            if (this.stack != null)
            {
                ListIterator it = this.stack.ListIterator();
                while (it.HasNext())
                {
                    if (it.Next() is StackEntry entry)
                    {
                        entry.DoneWithStack(this);
                    }
                }

                this.stack = null;
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalVarStack.java:56-67
        // Original: @Override public int size() { int size = 0; ListIterator<StackEntry> it = this.stack.listIterator(); while (it.hasNext()) { size += it.next().size(); } return size; }
        public override int Size()
        {
            int size = 0;
            ListIterator it = this.stack.ListIterator();
            while (it.HasNext())
            {
                if (it.Next() is StackEntry entry)
                {
                    size += entry.Size();
                }
            }

            return size;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalVarStack.java:69-72
        // Original: public void push(StackEntry entry) { this.stack.addFirst(entry); entry.addedToStack(this); }
        public virtual void Push(StackEntry entry)
        {
            this.stack.AddFirst(entry);
            entry.AddedToStack(this);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalVarStack.java:74-98
        // Original: public StackEntry get(int offset) { ... }
        public virtual StackEntry Get(int offset)
        {
            ListIterator it = this.stack.ListIterator();
            int pos = 0;
            while (it.HasNext())
            {
                StackEntry entry = (StackEntry)it.Next();
                pos += entry.Size();
                if (pos > offset)
                {
                    return entry.GetElement(pos - offset + 1);
                }

                if (pos == offset)
                {
                    return entry.GetElement(1);
                }
            }

            while (pos < offset)
            {
                Variable placeholder = this.NewPlaceholderVariable();
                this.stack.AddLast(placeholder);
                placeholder.AddedToStack(this);
                pos += placeholder.Size();
            }

            return (StackEntry)this.stack.GetLast();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalVarStack.java:100-102
        // Original: public Type getType(int offset) { return this.get(offset).type(); }
        public virtual UtilsType GetType(int offset)
        {
            return this.Get(offset).Type();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalVarStack.java:104-112
        // Original: public StackEntry remove() { if (this.stack == null || this.stack.isEmpty()) { return this.newPlaceholderVariable(); } StackEntry entry = this.stack.removeFirst(); entry.removedFromStack(this); return entry; }
        public virtual StackEntry Remove()
        {
            StackEntry entry = (StackEntry)this.stack.RemoveFirst();
            entry.RemovedFromStack(this);
            return entry;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalVarStack.java:114-134
        // Original: public void destruct(int removesize, int savestart, int savesize, SubroutineAnalysisData subdata) { ... }
        public virtual void Destruct(int removesize, int savestart, int savesize, SubroutineAnalysisData subdata)
        {
            this.Structify(1, removesize, subdata);
            if (savesize > 1)
            {
                this.Structify(removesize - (savestart + savesize) + 1, savesize, subdata);
            }

            Variable @struct = (Variable)this.stack.GetFirst();
            Variable element = (Variable)@struct.GetElement(removesize - (savestart + savesize) + 1);
            this.stack[0] = element;
            @struct = null;
            element = null;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalVarStack.java:136-185
        // Original: public VarStruct structify(int firstelement, int count, SubroutineAnalysisData subdata) { ... }
        public virtual VarStruct Structify(int firstelement, int count, SubroutineAnalysisData subdata)
        {
            ListIterator it = this.stack.ListIterator();
            int pos = 0;
            while (it.HasNext())
            {
                StackEntry entry = (StackEntry)it.Next();
                pos += entry.Size();
                if (pos == firstelement)
                {
                    VarStruct varstruct = new VarStruct();
                    varstruct.AddVarStackOrder((Variable)entry);
                    it.Set(varstruct);
                    for (entry = (StackEntry)it.Next(), pos += entry.Size(); pos <= firstelement + count - 1; pos += entry.Size())
                    {
                        it.Remove();
                        varstruct.AddVarStackOrder((Variable)entry);
                        if (!it.HasNext())
                        {
                            break;
                        }

                        entry = (StackEntry)it.Next();
                    }

                    subdata.AddStruct(varstruct);
                    return varstruct;
                }

                if (pos == firstelement + count - 1)
                {
                    return (VarStruct)entry;
                }

                if (pos > firstelement + count - 1)
                {
                    return ((VarStruct)entry).Structify(firstelement - (pos - entry.Size()), count, subdata);
                }
            }

            return null;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalVarStack.java:187-200
        // Original: @Override public String toString() { ... }
        public override string ToString()
        {
            string newline = Environment.NewLine;
            StringBuilder buffer = new StringBuilder();
            int max = this.stack.Count;
            buffer.Append("---stack, size " + max + "---" + newline);
            for (int i = 0; i < max; ++i)
            {
                StackEntry entry = (StackEntry)this.stack[i];
                buffer.Append("-->" + i + entry.ToString() + newline);
            }

            return buffer.ToString();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalVarStack.java:202-215
        // Original: @Override public LocalVarStack clone() { ... }
        public override object Clone()
        {
            LocalVarStack newStack = new LocalVarStack();
            newStack.stack = this.stack.Clone();
            foreach (StackEntry entry in this.stack)
            {
                if (typeof(Variable).IsInstanceOfType(entry))
                {
                    ((Variable)entry).StackWasCloned(this, newStack);
                }
            }

            return newStack;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalVarStack.java:217-222
        // Original: private Variable newPlaceholderVariable()
        private Variable NewPlaceholderVariable()
        {
            Variable placeholder = new Variable(new UtilsType((byte)255));
            placeholder.Name("__unknown_param_" + (++this.placeholderCounter));
            placeholder.IsParam(true);
            return placeholder;
        }
    }
}




