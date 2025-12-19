// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    public interface ListIterator
    {
        bool HasNext();
        object Next();
        bool HasPrevious();
        object Previous();
        int NextIndex();
        int PreviousIndex();
        void Remove();
        void Set(object o);
        void Add(object o);
    }
}





