// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    public static class LinkedListExtensions
    {
        public static ListIterator ListIterator<T>(this LinkedList<T> list)
        {
            return new LinkedListListIterator<T>(list);
        }

        public static ListIterator ListIterator<T>(this LinkedList<T> list, int index)
        {
            return new LinkedListListIterator<T>(list, index);
        }

        // Extension for List<T> to support ListIterator (needed since ScriptRootNode.GetChildren() returns List<ScriptNode>)
        public static ListIterator ListIterator<T>(this List<T> list)
        {
            return new ListListIterator<T>(list);
        }

        public static ListIterator ListIterator<T>(this List<T> list, int index)
        {
            return new ListListIterator<T>(list, index);
        }

        private class LinkedListListIterator<T> : ListIterator
        {
            private readonly LinkedList<T> _list;
            private LinkedListNode<T> _current;
            private int _index;

            public LinkedListListIterator(LinkedList<T> list)
            {
                _list = list;
                _current = list.First;
                _index = 0;
            }

            public LinkedListListIterator(LinkedList<T> list, int index)
            {
                _list = list;
                _index = index;
                _current = list.First;
                for (int i = 0; i < index && _current != null; i++)
                {
                    _current = _current.Next;
                }
            }

            public bool HasNext()
            {
                return _current != null;
            }

            public object Next()
            {
                if (_current == null)
                    throw new InvalidOperationException("No next element");
                object result = _current.Value;
                _current = _current.Next;
                _index++;
                return result;
            }

            public bool HasPrevious()
            {
                return _current != null && _current.Previous != null;
            }

            public object Previous()
            {
                if (_current == null || _current.Previous == null)
                    throw new InvalidOperationException("No previous element");
                _current = _current.Previous;
                _index--;
                return _current.Value;
            }

            public int NextIndex()
            {
                return _index;
            }

            public int PreviousIndex()
            {
                return _index - 1;
            }

            public void Remove()
            {
                if (_current == null)
                    throw new InvalidOperationException("Cannot remove");
                LinkedListNode<T> toRemove = _current.Previous ?? _current;
                _list.Remove(toRemove);
            }

            public void Set(object o)
            {
                if (_current == null || _current.Previous == null)
                    throw new InvalidOperationException("Cannot set");
                _current.Previous.Value = (T)o;
            }

            public void Add(object o)
            {
                if (_current == null)
                {
                    _list.AddLast((T)o);
                }
                else
                {
                    _list.AddBefore(_current, (T)o);
                }
                _index++;
            }
        }

        private class ListListIterator<T> : ListIterator
        {
            private readonly List<T> _list;
            private int _index;

            public ListListIterator(List<T> list)
            {
                _list = list;
                _index = 0;
            }

            public ListListIterator(List<T> list, int index)
            {
                _list = list;
                _index = index;
            }

            public bool HasNext()
            {
                return _index < _list.Count;
            }

            public object Next()
            {
                if (_index >= _list.Count)
                    throw new InvalidOperationException("No next element");
                object result = _list[_index];
                _index++;
                return result;
            }

            public bool HasPrevious()
            {
                return _index > 0;
            }

            public object Previous()
            {
                if (_index <= 0)
                    throw new InvalidOperationException("No previous element");
                _index--;
                return _list[_index];
            }

            public int NextIndex()
            {
                return _index;
            }

            public int PreviousIndex()
            {
                return _index - 1;
            }

            public void Remove()
            {
                if (_index <= 0 || _index > _list.Count)
                    throw new InvalidOperationException("Cannot remove");
                _list.RemoveAt(_index - 1);
                _index--;
            }

            public void Set(object o)
            {
                if (_index <= 0 || _index > _list.Count)
                    throw new InvalidOperationException("Cannot set");
                _list[_index - 1] = (T)o;
            }

            public void Add(object o)
            {
                _list.Insert(_index, (T)o);
                _index++;
            }
        }
    }
}





