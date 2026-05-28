using System.Collections;
using System.Collections.Generic;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    public class LinkedList : Collection, IEnumerable<object>
    {
        private readonly System.Collections.Generic.LinkedList<object> _list = new System.Collections.Generic.LinkedList<object>();

        public override IEnumerator<object> Iterator()
        {
            return new LinkedListEnumeratorAdapter(_list);
        }

        private class LinkedListEnumeratorAdapter : IEnumerator<object>
        {
            private readonly System.Collections.Generic.LinkedList<object> _list;
            private System.Collections.Generic.LinkedListNode<object> _current;
            private System.Collections.Generic.LinkedListNode<object> _started;

            public LinkedListEnumeratorAdapter(System.Collections.Generic.LinkedList<object> list)
            {
                _list = list;
                _current = list.First;
                _started = null;
            }

            public bool HasNext()
            {
                return _current != null;
            }

            public object Next()
            {
                if (!HasNext())
                    throw new System.InvalidOperationException("No next element");
                _started = _current;
                object value = _current.Value;
                _current = _current.Next;
                return value;
            }

            // IEnumerator<object> implementation
            public object Current => _started?.Value;
            object System.Collections.IEnumerator.Current => Current;
            public bool MoveNext()
            {
                if (!HasNext()) return false;
                Next();
                return true;
            }
            public void Reset() { _current = _list.First; _started = null; }
            public void Dispose() { }
        }

        public override bool AddAll(Collection c)
        {
            IEnumerator<object> i = c.Iterator();
            while (i.HasNext())
            {
                Add(i.Next());
            }
            return true;
        }

        public override bool AddAll(int index, Collection c)
        {
            int pos = index;
            IEnumerator<object> i = c.Iterator();
            while (i.HasNext())
            {
                Add(pos++, i.Next());
            }
            return true;
        }

        public virtual void Add(int index, object element)
        {
            var node = GetNodeAt(index);
            if (node == null)
            {
                _list.AddLast(element);
            }
            else
            {
                _list.AddBefore(node, element);
            }
        }

        public virtual bool Add(object o)
        {
            _list.AddLast(o);
            return true;
        }

        public virtual void AddFirst(object o)
        {
            _list.AddFirst(o);
        }

        public virtual void AddLast(object o)
        {
            _list.AddLast(o);
        }

        public virtual object Set(int index, object element)
        {
            var node = GetNodeAt(index);
            if (node == null)
            {
                throw new System.ArgumentOutOfRangeException(nameof(index));
            }
            object oldValue = node.Value;
            node.Value = element;
            return oldValue;
        }

        public virtual object this[int index]
        {
            get
            {
                var node = GetNodeAt(index);
                if (node == null)
                {
                    throw new System.ArgumentOutOfRangeException(nameof(index));
                }
                return node.Value;
            }
            set
            {
                Set(index, value);
            }
        }

        public virtual ListIterator ListIterator(int index)
        {
            return _list.ListIterator(index);
        }

        public virtual ListIterator ListIterator()
        {
            return ListIterator(0);
        }

        private System.Collections.Generic.LinkedListNode<object> GetNodeAt(int index)
        {
            if (index < 0 || index >= _list.Count)
            {
                return null;
            }

            var current = _list.First;
            for (int i = 0; i < index && current != null; i++)
            {
                current = current.Next;
            }
            return current;
        }

        public virtual object RemoveAt(int index)
        {
            var node = GetNodeAt(index);
            if (node == null)
            {
                throw new System.ArgumentOutOfRangeException(nameof(index));
            }
            object value = node.Value;
            _list.Remove(node);
            return value;
        }

        public virtual object GetLast()
        {
            if (_list.Count == 0)
                return null;
            return _list.Last.Value;
        }

        public virtual object GetFirst()
        {
            if (_list.First == null)
                return null;
            return _list.First.Value;
        }

        public virtual bool IsEmpty()
        {
            return _list.Count == 0;
        }

        public virtual object RemoveFirst()
        {
            if (_list.First == null)
            {
                throw new System.InvalidOperationException("The LinkedList is empty.");
            }
            object value = _list.First.Value;
            _list.RemoveFirst();
            return value;
        }

        public virtual object RemoveLast()
        {
            if (_list.Last == null)
            {
                throw new System.InvalidOperationException("The LinkedList is empty.");
            }
            object value = _list.Last.Value;
            _list.RemoveLast();
            return value;
        }

        public virtual int Count
        {
            get { return _list.Count; }
        }

        public virtual bool Remove(object item)
        {
            return _list.Remove(item);
        }

        public virtual bool Contains(object item)
        {
            return _list.Contains(item);
        }

        public virtual void Clear()
        {
            _list.Clear();
        }

        public virtual LinkedList Clone()
        {
            LinkedList clone = new LinkedList();
            foreach (object item in _list)
            {
                clone.AddLast(item);
            }
            return clone;
        }

        public System.Collections.Generic.IEnumerator<object> GetEnumerator()
        {
            return new LinkedListEnumerator(_list);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private class LinkedListEnumerator : System.Collections.Generic.IEnumerator<object>
        {
            private readonly System.Collections.Generic.LinkedList<object> _list;
            private System.Collections.Generic.LinkedListNode<object> _current;

            public LinkedListEnumerator(System.Collections.Generic.LinkedList<object> list)
            {
                _list = list;
                _current = null;
            }

            public object Current => _current?.Value;

            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                if (_current == null)
                {
                    _current = _list.First;
                }
                else
                {
                    _current = _current.Next;
                }
                return _current != null;
            }

            public void Reset()
            {
                _current = null;
            }

            public void Dispose() { }

            public bool HasNext()
            {
                return _current?.Next != null;
            }

            public object Next()
            {
                if (!MoveNext())
                {
                    throw new System.InvalidOperationException();
                }
                return Current;
            }
        }
    }
}





