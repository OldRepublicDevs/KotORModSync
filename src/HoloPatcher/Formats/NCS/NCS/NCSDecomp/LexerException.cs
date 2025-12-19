// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/lexer/LexerException.java:10-15
// Original: public class LexerException extends Exception
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace KOTORModSync.Formats.NCS.NCSDecomp.Lexer
{
    public class LexerException : Exception
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/lexer/LexerException.java:12-14
        // Original: public LexerException(String message) { super(message); }
        public LexerException(string message) : base(message)
        {
        }
    }
}




