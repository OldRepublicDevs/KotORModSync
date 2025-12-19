// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/VarStruct.java:18-256
// Original: public class VarStruct extends Variable
using System;
using System.Collections.Generic;
using KOTORModSync.Formats.NCS.NCSDecomp.Utils;
using NCSDecompLinkedList = KOTORModSync.Formats.NCS.NCSDecomp.LinkedList;
using UtilsType = KOTORModSync.Formats.NCS.NCSDecomp.Utils.Type;
namespace KOTORModSync.Formats.NCS.NCSDecomp.Stack
{
    public class VarStruct : Variable
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/VarStruct.java:19
        // Original: protected LinkedList<Variable> vars = new LinkedList<>();
        // Note: C# LinkedList is not generic, so we use the non-generic version
        protected LinkedList vars = new LinkedList();
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/VarStruct.java:20
        // Original: protected StructType structtype;
        protected StructType structtype;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/VarStruct.java:22-26
        // Original: public VarStruct() { super(new Type((byte)-15)); this.size = 0; this.structtype = new StructType(); }
        public VarStruct() : base(new UtilsType(unchecked((byte)(-15))))
        {
            this.size = 0;
            this.structtype = new StructType();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/VarStruct.java:28-40
        // Original: public VarStruct(StructType structtype) { this(); this.structtype = structtype; List<Type> types = structtype.types(); for (Type type : types) { if (StructType.class.isInstance(type)) { this.addVar(new VarStruct((StructType)type)); } else { this.addVar(new Variable(type)); } } }
        public VarStruct(StructType structtype) : this()
        {
            this.structtype = structtype;

            List<object> types = structtype.Types();
            foreach (object typeObj in types)
            {
                UtilsType type = (UtilsType)typeObj;
                if (type is StructType)
                {
                    this.AddVar(new VarStruct((StructType)type));
                }
                else
                {
                    this.AddVar(new Variable(type));
                }
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/VarStruct.java:42-57
        // Original: @Override public void close() { ... for (int i = 0; i < this.vars.size(); i++) { this.vars.get(i).close(); } ... }
        public override void Close()
        {
            base.Close();
            if (this.vars != null)
            {
                for (int i = 0; i < this.vars.Count; i++)
                {
                    ((Variable)this.vars[i]).Close();
                }
            }

            this.vars = null;
            if (this.structtype != null)
            {
                this.structtype.Close();
            }

            this.structtype = null;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/VarStruct.java:59-64
        // Original: public void addVar(Variable var) { this.vars.addFirst(var); var.varstruct(this); this.structtype.addType(var.type()); this.size = this.size + var.size(); }
        public virtual void AddVar(Variable var)
        {
            this.vars.AddFirst(var);
            var.Varstruct(this);
            this.structtype.AddType(var.Type());
            this.size = this.size + var.Size();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/VarStruct.java:66-71
        // Original: public void addVarStackOrder(Variable var) { this.vars.add(var); var.varstruct(this); this.structtype.addTypeStackOrder(var.type()); this.size = this.size + var.size(); }
        public virtual void AddVarStackOrder(Variable var)
        {
            this.vars.Add(var);
            var.Varstruct(this);
            this.structtype.AddTypeStackOrder(var.Type());
            this.size = this.size + var.Size();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/VarStruct.java:74-76
        // Original: @Override public void name(String prefix, byte count) { this.name = prefix + "struct" + Byte.toString(count); }
        public override void Name(string prefix, byte count)
        {
            this.name = prefix + "struct" + count.ToString();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/VarStruct.java:78-80
        // Original: public String name() { return this.name; }
        public virtual string Name()
        {
            return this.name;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/VarStruct.java:82-84
        // Original: public void structType(StructType structtype) { this.structtype = structtype; }
        public virtual void StructType(StructType structtype)
        {
            this.structtype = structtype;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/VarStruct.java:87-89
        // Original: @Override public String toString() { return this.name; }
        public override string ToString()
        {
            return this.name;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/VarStruct.java:91-93
        // Original: public String typeName() { return this.structtype.typeName(); }
        public virtual string TypeName()
        {
            return this.structtype.TypeName();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/VarStruct.java:96-98
        // Original: @Override public String toDeclString() { return this.structtype.toDeclString() + " " + this.name; }
        public override string ToDeclString()
        {
            return this.structtype.ToDeclString() + " " + this.name;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/VarStruct.java:100-110
        // Original: public void updateNames() { if (this.structtype.isVector()) { ... } else { ... } }
        public virtual void UpdateNames()
        {
            if (this.structtype.IsVector())
            {
                ((Variable)this.vars[0]).Name("z");
                ((Variable)this.vars[1]).Name("y");
                ((Variable)this.vars[2]).Name("x");
            }
            else
            {
                for (int i = 0; i < this.vars.Count; i++)
                {
                    ((Variable)this.vars[i]).Name(this.structtype.ElementName(this.vars.Count - i - 1));
                }
            }
        }

        public override void Assigned()
        {
            for (int i = 0; i < this.vars.Count; ++i)
            {
                ((Variable)this.vars[i]).Assigned();
            }
        }

        public override void AddedToStack(LocalStack stack)
        {
            for (int i = 0; i < this.vars.Count; ++i)
            {
                ((Variable)this.vars[i]).AddedToStack(stack);
            }
        }

        public virtual bool Contains(Variable var)
        {
            return this.vars.Contains(var);
        }

        public virtual StructType StructType()
        {
            return this.structtype;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/VarStruct.java:134-151
        // Original: @Override public StackEntry getElement(int stackpos) { int pos = 0; for (int i = this.vars.size() - 1; i >= 0; i--) { StackEntry entry = this.vars.get(i); pos += entry.size(); if (pos == stackpos) { return entry.getElement(1); } if (pos > stackpos) { return entry.getElement(pos - stackpos + 1); } } throw new RuntimeException("Stackpos was greater than stack size"); }
        public override StackEntry GetElement(int stackpos)
        {
            int pos = 0;

            for (int i = this.vars.Count - 1; i >= 0; i--)
            {
                StackEntry entry = (StackEntry)this.vars[i];
                pos += entry.Size();
                if (pos == stackpos)
                {
                    return entry.GetElement(1);
                }

                if (pos > stackpos)
                {
                    return entry.GetElement(pos - stackpos + 1);
                }
            }

            throw new Exception("Stackpos was greater than stack size");
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/VarStruct.java:153-190
        // Original: public VarStruct structify(int firstelement, int count, SubroutineAnalysisData subdata) { ListIterator<Variable> it = this.vars.listIterator(); int pos = 0; while (it.hasNext()) { StackEntry entry = (StackEntry)it.next(); pos += entry.size(); if (pos == firstelement) { VarStruct varstruct = new VarStruct(); varstruct.addVarStackOrder((Variable)entry); it.set(varstruct); entry = (StackEntry)it.next(); for (int var8 = pos + entry.size(); var8 <= firstelement + count - 1; var8 += entry.size()) { it.remove(); varstruct.addVarStackOrder((Variable)entry); if (!it.hasNext()) { break; } entry = (StackEntry)it.next(); } subdata.addStruct(varstruct); return varstruct; } if (pos == firstelement + count - 1) { return (VarStruct)entry; } if (pos > firstelement + count - 1) { return ((VarStruct)entry).structify(firstelement - (pos - entry.size()), count, subdata); } } return null; }
        public virtual VarStruct Structify(int firstelement, int count, SubroutineAnalysisData subdata)
        {
            ListIterator it = this.vars.ListIterator();
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
                    entry = (StackEntry)it.Next();

                    for (int var8 = pos + entry.Size(); var8 <= firstelement + count - 1; var8 += entry.Size())
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
    }
}




