// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/parser/ParserException.java:12-24
// Original: public class ParserException extends Exception
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;

namespace KOTORModSync.Formats.NCS.NCSDecomp.Parser
{
    public class ParserException : Exception
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/parser/ParserException.java:14
        // Original: private final transient Token token;
        Token token;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/parser/ParserException.java:16-19
        // Original: public ParserException(Token token, String message) { super(message); this.token = token; }
        public ParserException(Token token, string message) : base(message)
        {
            this.token = token;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/parser/ParserException.java:21-23
        // Original: public Token getToken() { return this.token; }
        public virtual Token GetToken()
        {
            return this.token;
        }
    }
}




