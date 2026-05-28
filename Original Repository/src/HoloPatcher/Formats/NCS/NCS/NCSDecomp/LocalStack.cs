// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalStack.java
// Original: public class LocalStack<T> implements Cloneable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
namespace KOTORModSync.Formats.NCS.NCSDecomp.Stack
{
    // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalStack.java:13-31
    // Original: public class LocalStack<T> implements Cloneable
    public class LocalStack
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalStack.java:14
        // Original: protected LinkedList<T> stack = new LinkedList<>();
        protected LinkedList stack = new LinkedList();

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalStack.java:16-18
        // Original: public int size() { return this.stack.size(); }
        public virtual int Size()
        {
            return this.stack.Count;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalStack.java:20-26
        // Original: @Override @SuppressWarnings("unchecked") public LocalStack<T> clone() { LocalStack<T> newStack = new LocalStack<>(); newStack.stack = new LinkedList<>(this.stack); return newStack; }
        public virtual object Clone()
        {
            LocalStack newStack = new LocalStack();
            // Clone the custom LinkedList - matching Java's new LinkedList<>(this.stack) copy constructor
            newStack.stack = this.stack.Clone();
            return newStack;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalStack.java:28-30
        // Original: public void close()
        public virtual void Close()
        {
            this.stack = null;
        }
    }
}




