// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/TypedLinkedList.java:15-153
// Original: public class TypedLinkedList<T> extends LinkedList<T>
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    public class TypedLinkedList : LinkedList
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/TypedLinkedList.java:17
        // Original: private final Cast<T> cast;
        ICast cast;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/TypedLinkedList.java:19-21
        // Original: public TypedLinkedList() { this.cast = NoCast.instance(); }
        public TypedLinkedList()
        {
            this.cast = NoCast.instance;
        }

        public TypedLinkedList(Collection c)
        {
            this.cast = NoCast.instance;
            this.AddAll(c);
        }

        public TypedLinkedList(ICast cast)
        {
            this.cast = cast;
        }

        public TypedLinkedList(Collection c, ICast cast)
        {
            this.cast = cast;
            this.AddAll(c);
        }

        public virtual ICast GetCast()
        {
            return this.cast;
        }

        public override object Set(int index, object element)
        {
            return base[index] = this.cast.Cast(element);
        }

        public override void Add(int index, object element)
        {
            base.Add(index, this.cast.Cast(element));
        }

        public override bool Add(object o)
        {
            return base.Add(this.cast.Cast(o));
        }

        public override bool AddAll(Collection c)
        {
            IEnumerator<object> i = c.Iterator();
            while (i.HasNext())
            {
                base.Add(this.cast.Cast(i.Next()));
            }

            return true;
        }

        public override bool AddAll(int index, Collection c)
        {
            int pos = index;
            IEnumerator<object> i = c.Iterator();
            while (i.HasNext())
            {
                base.Add(pos++, this.cast.Cast(i.Next()));
            }

            return true;
        }

        public override void AddFirst(object o)
        {
            base.AddFirst(this.cast.Cast(o));
        }

        public override void AddLast(object o)
        {
            base.AddLast(this.cast.Cast(o));
        }

        public override ListIterator ListIterator(int index)
        {
            return new TypedLinkedListIterator(base.ListIterator(index), this.cast);
        }

        private class TypedLinkedListIterator : ListIterator
        {
            ListIterator iterator;
            ICast cast;
            internal TypedLinkedListIterator(ListIterator iterator, ICast cast)
            {
                this.iterator = iterator;
                this.cast = cast;
            }

            public virtual bool HasNext()
            {
                return this.iterator.HasNext();
            }

            public virtual object Next()
            {
                return this.iterator.Next();
            }

            public virtual bool HasPrevious()
            {
                return this.iterator.HasPrevious();
            }

            public virtual object Previous()
            {
                return this.iterator.Previous();
            }

            public virtual int NextIndex()
            {
                return this.iterator.NextIndex();
            }

            public virtual int PreviousIndex()
            {
                return this.iterator.PreviousIndex();
            }

            public virtual void Remove()
            {
                this.iterator.Remove();
            }

            public virtual void Set(object o)
            {
                this.iterator.Set(this.cast.Cast(o));
            }

            public virtual void Add(object o)
            {
                this.iterator.Add(this.cast.Cast(o));
            }
        }
    }
}




