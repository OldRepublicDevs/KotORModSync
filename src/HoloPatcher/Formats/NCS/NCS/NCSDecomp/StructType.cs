// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/StructType.java:15-228
// Original: public class StructType extends Type
using System;
using static KOTORModSync.Formats.NCS.NCSDecomp.DecompilerLogger;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace KOTORModSync.Formats.NCS.NCSDecomp.Utils
{
    public class StructType : Type
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/StructType.java:16-19
        // Original: private ArrayList<Type> types = new ArrayList<>(); private boolean alltyped = true; private String typename; private ArrayList<String> elements;
        private List<object> types;
        private bool alltyped;
        private string typename;
        private List<object> elements;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/StructType.java:21-24
        // Original: public StructType() { super((byte)-15); this.size = 0; }
        public StructType() : base(unchecked((byte)(-15)))
        {
            this.types = new List<object>();
            this.alltyped = true;
            this.size = 0;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/StructType.java:26-39
        // Original: @Override public void close() { if (this.types != null) { Iterator<Type> it = this.types.iterator(); while (it.hasNext()) { it.next().close(); } this.types = null; } this.elements = null; }
        public override void Close()
        {
            if (this.types != null)
            {
                IEnumerator<object> it = this.types.Iterator();

                while (it.HasNext())
                {
                    Type type = (Type)it.Next();
                    type.Close();
                }

                this.types = null;
            }

            this.elements = null;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/StructType.java:41-52
        // Original: public void print() { System.out.println("Struct has " + Integer.toString(this.types.size()) + " entries."); ... }
        public virtual void Print()
        {
            Debug("Struct has " + this.types.Count.ToString() + " entries.");
            if (this.alltyped)
            {
                Debug("They have all been typed");
            }
            else
            {
                Debug("They have not all been typed");
            }

            for (int i = 0; i < this.types.Count; i++)
            {
                Debug("  Type: " + ((Type)this.types[i]).ToString());
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/StructType.java:54-61
        // Original: public void addType(Type type) { this.types.add(type); if (type.equals(new Type((byte)-1))) { this.alltyped = false; } this.size = this.size + type.size(); }
        public virtual void AddType(Type type)
        {
            this.types.Add(type);
            if (type.Equals(new Type(unchecked((byte)(-1)))))
            {
                this.alltyped = false;
            }

            this.size = this.size + type.Size();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/StructType.java:63-70
        // Original: public void addTypeStackOrder(Type type) { this.types.add(0, type); if (type.equals(new Type((byte)-1))) { this.alltyped = false; } this.size = this.size + type.size(); }
        public virtual void AddTypeStackOrder(Type type)
        {
            this.types.Insert(0, type);
            if (type.Equals(new Type(unchecked((byte)(-1)))))
            {
                this.alltyped = false;
            }

            this.size = this.size + type.Size();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/StructType.java:72-84
        // Original: public boolean isVector() { if (this.size != 3) { return false; } else { for (int i = 0; i < 3; i++) { if (!(this.types.get(i)).equals((byte)4)) { return false; } } return true; } }
        public virtual bool IsVector()
        {
            if (this.size != 3)
            {
                return false;
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    if (!((Type)this.types[i]).Equals((byte)4))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/StructType.java:86-89
        // Original: @Override public boolean isTyped() { return this.alltyped; }
        public override bool IsTyped()
        {
            return this.alltyped;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/StructType.java:91-94
        // Original: public void updateType(int pos, Type type) { this.types.set(pos, type); this.updateTyped(); }
        public virtual void UpdateType(int pos, Type type)
        {
            this.types[pos] = type;
            this.UpdateTyped();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/StructType.java:96-98
        // Original: public ArrayList<Type> types() { return this.types; }
        public virtual List<object> Types()
        {
            return this.types;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/StructType.java:100-109
        // Original: protected void updateTyped() { this.alltyped = true; for (int i = 0; i < this.types.size(); i++) { if (!this.types.get(i).isTyped()) { this.alltyped = false; return; } } }
        protected virtual void UpdateTyped()
        {
            this.alltyped = true;

            for (int i = 0; i < this.types.Count; i++)
            {
                if (!((Type)this.types[i]).IsTyped())
                {
                    this.alltyped = false;
                    return;
                }
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/StructType.java:111-121
        // Original: @Override public boolean equals(Object obj) { if (this == obj) { return true; } if (!(obj instanceof StructType)) { return false; } StructType other = (StructType)obj; return this.types.equals(other.types()); }
        public override bool Equals(object obj)
        {
            if (this == obj)
            {
                return true;
            }
            if (!typeof(StructType).IsInstanceOfType(obj))
            {
                return false;
            }
            StructType other = (StructType)obj;
            return this.types.Equals(other.Types());
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/StructType.java:123-126
        // Original: @Override public int hashCode() { return this.types.hashCode(); }
        public override int GetHashCode()
        {
            return this.types.GetHashCode();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/StructType.java:128-130
        // Original: public void typeName(String name) { this.typename = name; }
        public virtual void TypeName(string name)
        {
            this.typename = name;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/StructType.java:132-134
        // Original: public String typeName() { return this.typename; }
        public virtual string TypeName()
        {
            return this.typename;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/StructType.java:136-139
        // Original: @Override public String toDeclString() { return this.isVector() ? Type.toString((byte)-16) : this.toString() + " " + this.typename; }
        public override string ToDeclString()
        {
            return this.IsVector() ? Type.ToString(new Type(unchecked((byte)(-16)))) : this.ToString() + " " + this.typename;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/StructType.java:141-147
        // Original: public String elementName(int i) { if (this.elements == null) { this.setElementNames(); } return this.elements.get(i); }
        public virtual string ElementName(int i)
        {
            if (this.elements == null)
            {
                this.SetElementNames();
            }

            return (string)this.elements[i];
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/StructType.java:149-162
        // Original: @Override public Type getElement(int pos) { int remaining = pos; for (Type entry : this.types) { int size = entry.size(); if (remaining <= size) { return entry.getElement(remaining); } remaining -= size; } throw new RuntimeException("Pos was greater than struct size"); }
        public override Type GetElement(int pos)
        {
            int remaining = pos;

            foreach (object entryObj in this.types)
            {
                Type entry = (Type)entryObj;
                int size = entry.Size();
                if (remaining <= size)
                {
                    return entry.GetElement(remaining);
                }
                remaining -= size;
            }

            throw new Exception("Pos was greater than struct size");
        }

        private void SetElementNames()
        {
            this.elements = new List<object>();
            Dictionary<object, object> typecounts = new Dictionary<object, object>();
            if (this.IsVector())
            {
                this.elements.Add("x");
                this.elements.Add("y");
                this.elements.Add("z");
            }
            else
            {
                for (int i = 0; i < this.types.Count; ++i)
                {
                    Type type = (Type)this.types[i];
                    object typecountObj = typecounts.ContainsKey(type) ? typecounts[type] : null;
                    int typecount = typecountObj != null ? (int)typecountObj : 0;
                    int count;
                    if (typecount != 0)
                    {
                        count = 1 + typecount;
                    }
                    else
                    {
                        count = 1;
                    }

                    this.elements.Add(type.ToString() + count);
                    typecounts[type] = count + 1;
                }
            }

            typecounts = null;
        }
    }
}




