using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace KOTORModSync.Formats.NCS.NCSDecomp.Lexer
{
    public class PushbackReader
    {
        private TextReader reader;
        private Stack<int> pushbackStack;

        public PushbackReader(TextReader reader)
        {
            this.reader = reader;
            this.pushbackStack = new Stack<int>();
        }

        public PushbackReader(TextReader reader, int bufferSize) : this(reader)
        {
            // bufferSize is ignored in this implementation - using a stack instead
        }

        public int Read()
        {
            if (pushbackStack.Count > 0)
            {
                return pushbackStack.Pop();
            }
            return reader.Read();
        }

        public void Unread(int c)
        {
            if (c != -1)
            {
                pushbackStack.Push(c);
            }
        }
    }
}





