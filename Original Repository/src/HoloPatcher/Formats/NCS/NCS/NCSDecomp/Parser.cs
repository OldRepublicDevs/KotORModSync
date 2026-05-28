// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/parser/Parser.java:189-1517
// Original: public class Parser
// Note: This file is generated from parser grammar, matching the Java implementation structure
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp;
using KOTORModSync.Formats.NCS.NCSDecomp.Analysis;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;
using LexerClass = KOTORModSync.Formats.NCS.NCSDecomp.Lexer;

// Decompiled by Procyon v0.6.0
// 
namespace KOTORModSync.Formats.NCS.NCSDecomp.Parser
{
    public class Parser
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/parser/Parser.java:190-207
        // Original: private static final int SHIFT = 0; private static final int REDUCE = 1; private static final int ACCEPT = 2; private static final int ERROR = 3; private static int[][][] actionTable; private static int[][][] gotoTable; private static String[] errorMessages; private static int[] errors; public final Analysis ignoredTokens = new AnalysisAdapter(); protected Node node; private final Lexer lexer; private final ListIterator<State> stack = new LinkedList<State>().listIterator(); private int last_shift; private int last_pos; private int last_line; private Token last_token; private final TokenIndex converter = new TokenIndex(); private final int[] action = new int[2];
        private const int SHIFT = 0;
        private const int REDUCE = 1;
        private const int ACCEPT = 2;
        private const int ERROR = 3;
        private static int[][][] actionTable;
        private static int[][][] gotoTable;
        private static String[] errorMessages;
        private static int[] errors;
        public readonly IAnalysis ignoredTokens;
        protected Node node;
        private readonly KOTORModSync.Formats.NCS.NCSDecomp.Lexer.Lexer lexer;
        private readonly ListIterator stack;
        private int last_shift;
        private int last_pos;
        private int last_line;
        private Token last_token;
        private readonly TokenIndex converter;
        private readonly int[] action;
        public Parser(KOTORModSync.Formats.NCS.NCSDecomp.Lexer.Lexer lexer)
        {
            this.ignoredTokens = new AnalysisAdapter();
            this.stack = new LinkedList<object>().ListIterator();
            this.converter = new TokenIndex();
            this.action = new int[2];
            this.lexer = lexer;
            if (Parser.actionTable == null)
            {
                try
                {
                    // Match Lexer.cs pattern: try executing assembly first, then calling assembly
                    System.IO.Stream stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("KOTORModSync.Formats.NCS.NCSDecomp.parser.dat") ??
                                              System.Reflection.Assembly.GetCallingAssembly().GetManifestResourceStream("KOTORModSync.Formats.NCS.NCSDecomp.parser.dat") ??
                                              typeof(Parser).Assembly.GetManifestResourceStream("KOTORModSync.Formats.NCS.NCSDecomp.parser.dat") ??
                                              System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("parser.dat") ??
                                              System.Reflection.Assembly.GetCallingAssembly().GetManifestResourceStream("parser.dat");
                    if (stream == null)
                    {
                        throw new Exception("The file \"parser.dat\" is either missing or corrupted.");
                    }
                    System.IO.BinaryReader s = new System.IO.BinaryReader(stream);
                    int length = s.ReadInt32();
                    Parser.actionTable = new int[length][][];
                    for (int i = 0; i < Parser.actionTable.Length; ++i)
                    {
                        length = s.ReadInt32();
                        Parser.actionTable[i] = new int[length][];
                        for (int j = 0; j < length; ++j)
                        {
                            Parser.actionTable[i][j] = new int[3];
                        }
                        for (int j = 0; j < Parser.actionTable[i].Length; ++j)
                        {
                            for (int k = 0; k < 3; ++k)
                            {
                                Parser.actionTable[i][j][k] = s.ReadInt32();
                            }
                        }
                    }

                    length = s.ReadInt32();
                    Parser.gotoTable = new int[length][][];
                    for (int i = 0; i < Parser.gotoTable.Length; ++i)
                    {
                        length = s.ReadInt32();
                        Parser.gotoTable[i] = new int[length][];
                        for (int j = 0; j < length; ++j)
                        {
                            Parser.gotoTable[i][j] = new int[2];
                        }
                        for (int j = 0; j < Parser.gotoTable[i].Length; ++j)
                        {
                            for (int k = 0; k < 2; ++k)
                            {
                                Parser.gotoTable[i][j][k] = s.ReadInt32();
                            }
                        }
                    }

                    length = s.ReadInt32();
                    Parser.errorMessages = new string[length];
                    for (int i = 0; i < Parser.errorMessages.Length; ++i)
                    {
                        length = s.ReadInt32();
                        StringBuilder buffer = new StringBuilder();
                        for (int l = 0; l < length; ++l)
                        {
                            buffer.Append((char)s.ReadByte());
                        }

                        Parser.errorMessages[i] = buffer.ToString();
                    }

                    length = s.ReadInt32();
                    Parser.errors = new int[length];
                    for (int i = 0; i < Parser.errors.Length; ++i)
                    {
                        Parser.errors[i] = s.ReadInt32();
                    }

                    s.Dispose();
                }
                catch (Exception)
                {
                    throw new Exception("The file \"parser.dat\" is either missing or corrupted.");
                }
            }
        }

        protected virtual void Filter()
        {
        }

        private int GoTo(int index)
        {
            int state = this.State();
            int low = 1;
            int high = Parser.gotoTable[index].Length - 1;
            int value = Parser.gotoTable[index][0][1];
            while (low <= high)
            {
                int middle = (low + high) / 2;
                if (state < Parser.gotoTable[index][middle][0])
                {
                    high = middle - 1;
                }
                else
                {
                    if (state <= Parser.gotoTable[index][middle][0])
                    {
                        value = Parser.gotoTable[index][middle][1];
                        break;
                    }

                    low = middle + 1;
                }
            }

            return value;
        }

        private void Push(int state, Node node, bool filter)
        {
            this.node = node;
            if (filter)
            {
                this.Filter();
            }

            if (!this.stack.HasNext())
            {
                this.stack.Add(new State(state, this.node));
                return;
            }

            State s = (State)this.stack.Next();
            s.state = state;
            s.node = this.node;
        }

        private int State()
        {
            State s = (State)this.stack.Previous();
            this.stack.Next();
            return s.state;
        }

        private Node Pop()
        {
            State s = (State)this.stack.Previous();
            return (Node)s.node;
        }

        private int Index(Switchable token)
        {
            this.converter.index = -1;
            token.Apply(this.converter);
            return this.converter.index;
        }

        public virtual Start Parse()
        {
            this.Push(0, null, false);
            IList<object> ign = null;
            while (true)
            {
                while (this.Index(this.lexer.Peek()) == -1)
                {
                    if (ign == null)
                    {
                        ign = (IList<object>)new TypedLinkedList(NodeCast.instance);
                    }

                    ign.Add(this.lexer.Next());
                }

                if (ign != null)
                {
                    this.ignoredTokens.SetIn(this.lexer.Peek(), ign);
                    ign = null;
                }

                this.last_pos = this.lexer.Peek().GetPos();
                this.last_line = this.lexer.Peek().GetLine();
                this.last_token = this.lexer.Peek();
                int index = this.Index(this.lexer.Peek());
                this.action[0] = Parser.actionTable[this.State()][0][1];
                this.action[1] = Parser.actionTable[this.State()][0][2];
                int low = 1;
                int high = Parser.actionTable[this.State()].Length - 1;
                while (low <= high)
                {
                    int middle = (low + high) / 2;
                    if (index < Parser.actionTable[this.State()][middle][0])
                    {
                        high = middle - 1;
                    }
                    else
                    {
                        if (index <= Parser.actionTable[this.State()][middle][0])
                        {
                            this.action[0] = Parser.actionTable[this.State()][middle][1];
                            this.action[1] = Parser.actionTable[this.State()][middle][2];
                            break;
                        }

                        low = middle + 1;
                    }
                }

                switch (this.action[0])
                {
                    case 0:
                        {
                            this.Push(this.action[1], this.lexer.Next(), true);
                            this.last_shift = this.action[1];
                            continue;
                        }

                    case 1:
                        {
                            switch (this.action[1])
                            {
                                case 0:
                                    {
                                        Node node = this.New0();
                                        this.Push(this.GoTo(0), node, true);
                                        continue;
                                    }

                                case 1:
                                    {
                                        Node node = this.New1();
                                        this.Push(this.GoTo(31), node, false);
                                        continue;
                                    }

                                case 2:
                                    {
                                        Node node = this.New2();
                                        this.Push(this.GoTo(31), node, false);
                                        continue;
                                    }

                                case 3:
                                    {
                                        Node node = this.New3();
                                        this.Push(this.GoTo(0), node, true);
                                        continue;
                                    }

                                case 4:
                                    {
                                        Node node = this.New4();
                                        this.Push(this.GoTo(1), node, true);
                                        continue;
                                    }

                                case 5:
                                    {
                                        Node node = this.New5();
                                        this.Push(this.GoTo(1), node, true);
                                        continue;
                                    }

                                case 6:
                                    {
                                        Node node = this.New6();
                                        this.Push(this.GoTo(2), node, true);
                                        continue;
                                    }

                                case 7:
                                    {
                                        Node node = this.New7();
                                        this.Push(this.GoTo(32), node, false);
                                        continue;
                                    }

                                case 8:
                                    {
                                        Node node = this.New8();
                                        this.Push(this.GoTo(32), node, false);
                                        continue;
                                    }

                                case 9:
                                    {
                                        Node node = this.New9();
                                        this.Push(this.GoTo(3), node, true);
                                        continue;
                                    }

                                case 10:
                                    {
                                        Node node = this.New10();
                                        this.Push(this.GoTo(3), node, true);
                                        continue;
                                    }

                                case 11:
                                    {
                                        Node node = this.New11();
                                        this.Push(this.GoTo(3), node, true);
                                        continue;
                                    }

                                case 12:
                                    {
                                        Node node = this.New12();
                                        this.Push(this.GoTo(3), node, true);
                                        continue;
                                    }

                                case 13:
                                    {
                                        Node node = this.New13();
                                        this.Push(this.GoTo(3), node, true);
                                        continue;
                                    }

                                case 14:
                                    {
                                        Node node = this.New14();
                                        this.Push(this.GoTo(3), node, true);
                                        continue;
                                    }

                                case 15:
                                    {
                                        Node node = this.New15();
                                        this.Push(this.GoTo(3), node, true);
                                        continue;
                                    }

                                case 16:
                                    {
                                        Node node = this.New16();
                                        this.Push(this.GoTo(3), node, true);
                                        continue;
                                    }

                                case 17:
                                    {
                                        Node node = this.New17();
                                        this.Push(this.GoTo(3), node, true);
                                        continue;
                                    }

                                case 18:
                                    {
                                        Node node = this.New18();
                                        this.Push(this.GoTo(3), node, true);
                                        continue;
                                    }

                                case 19:
                                    {
                                        Node node = this.New19();
                                        this.Push(this.GoTo(3), node, true);
                                        continue;
                                    }

                                case 20:
                                    {
                                        Node node = this.New20();
                                        this.Push(this.GoTo(3), node, true);
                                        continue;
                                    }

                                case 21:
                                    {
                                        Node node = this.New21();
                                        this.Push(this.GoTo(3), node, true);
                                        continue;
                                    }

                                case 22:
                                    {
                                        Node node = this.New22();
                                        this.Push(this.GoTo(3), node, true);
                                        continue;
                                    }

                                case 23:
                                    {
                                        Node node = this.New23();
                                        this.Push(this.GoTo(3), node, true);
                                        continue;
                                    }

                                case 24:
                                    {
                                        Node node = this.New24();
                                        this.Push(this.GoTo(3), node, true);
                                        continue;
                                    }

                                case 25:
                                    {
                                        Node node = this.New25();
                                        this.Push(this.GoTo(3), node, true);
                                        continue;
                                    }

                                case 26:
                                    {
                                        Node node = this.New26();
                                        this.Push(this.GoTo(3), node, true);
                                        continue;
                                    }

                                case 27:
                                    {
                                        Node node = this.New27();
                                        this.Push(this.GoTo(4), node, true);
                                        continue;
                                    }

                                case 28:
                                    {
                                        Node node = this.New28();
                                        this.Push(this.GoTo(4), node, true);
                                        continue;
                                    }

                                case 29:
                                    {
                                        Node node = this.New29();
                                        this.Push(this.GoTo(4), node, true);
                                        continue;
                                    }

                                case 30:
                                    {
                                        Node node = this.New30();
                                        this.Push(this.GoTo(4), node, true);
                                        continue;
                                    }

                                case 31:
                                    {
                                        Node node = this.New31();
                                        this.Push(this.GoTo(4), node, true);
                                        continue;
                                    }

                                case 32:
                                    {
                                        Node node = this.New32();
                                        this.Push(this.GoTo(5), node, true);
                                        continue;
                                    }

                                case 33:
                                    {
                                        Node node = this.New33();
                                        this.Push(this.GoTo(5), node, true);
                                        continue;
                                    }

                                case 34:
                                    {
                                        Node node = this.New34();
                                        this.Push(this.GoTo(5), node, true);
                                        continue;
                                    }

                                case 35:
                                    {
                                        Node node = this.New35();
                                        this.Push(this.GoTo(5), node, true);
                                        continue;
                                    }

                                case 36:
                                    {
                                        Node node = this.New36();
                                        this.Push(this.GoTo(5), node, true);
                                        continue;
                                    }

                                case 37:
                                    {
                                        Node node = this.New37();
                                        this.Push(this.GoTo(5), node, true);
                                        continue;
                                    }

                                case 38:
                                    {
                                        Node node = this.New38();
                                        this.Push(this.GoTo(5), node, true);
                                        continue;
                                    }

                                case 39:
                                    {
                                        Node node = this.New39();
                                        this.Push(this.GoTo(5), node, true);
                                        continue;
                                    }

                                case 40:
                                    {
                                        Node node = this.New40();
                                        this.Push(this.GoTo(5), node, true);
                                        continue;
                                    }

                                case 41:
                                    {
                                        Node node = this.New41();
                                        this.Push(this.GoTo(5), node, true);
                                        continue;
                                    }

                                case 42:
                                    {
                                        Node node = this.New42();
                                        this.Push(this.GoTo(5), node, true);
                                        continue;
                                    }

                                case 43:
                                    {
                                        Node node = this.New43();
                                        this.Push(this.GoTo(5), node, true);
                                        continue;
                                    }

                                case 44:
                                    {
                                        Node node = this.New44();
                                        this.Push(this.GoTo(5), node, true);
                                        continue;
                                    }

                                case 45:
                                    {
                                        Node node = this.New45();
                                        this.Push(this.GoTo(5), node, true);
                                        continue;
                                    }

                                case 46:
                                    {
                                        Node node = this.New46();
                                        this.Push(this.GoTo(6), node, true);
                                        continue;
                                    }

                                case 47:
                                    {
                                        Node node = this.New47();
                                        this.Push(this.GoTo(6), node, true);
                                        continue;
                                    }

                                case 48:
                                    {
                                        Node node = this.New48();
                                        this.Push(this.GoTo(6), node, true);
                                        continue;
                                    }

                                case 49:
                                    {
                                        Node node = this.New49();
                                        this.Push(this.GoTo(7), node, true);
                                        continue;
                                    }

                                case 50:
                                    {
                                        Node node = this.New50();
                                        this.Push(this.GoTo(7), node, true);
                                        continue;
                                    }

                                case 51:
                                    {
                                        Node node = this.New51();
                                        this.Push(this.GoTo(7), node, true);
                                        continue;
                                    }

                                case 52:
                                    {
                                        Node node = this.New52();
                                        this.Push(this.GoTo(7), node, true);
                                        continue;
                                    }

                                case 53:
                                    {
                                        Node node = this.New53();
                                        this.Push(this.GoTo(8), node, true);
                                        continue;
                                    }

                                case 54:
                                    {
                                        Node node = this.New54();
                                        this.Push(this.GoTo(8), node, true);
                                        continue;
                                    }

                                case 55:
                                    {
                                        Node node = this.New55();
                                        this.Push(this.GoTo(8), node, true);
                                        continue;
                                    }

                                case 56:
                                    {
                                        Node node = this.New56();
                                        this.Push(this.GoTo(9), node, true);
                                        continue;
                                    }

                                case 57:
                                    {
                                        Node node = this.New57();
                                        this.Push(this.GoTo(9), node, true);
                                        continue;
                                    }

                                case 58:
                                    {
                                        Node node = this.New58();
                                        this.Push(this.GoTo(10), node, true);
                                        continue;
                                    }

                                case 59:
                                    {
                                        Node node = this.New59();
                                        this.Push(this.GoTo(10), node, true);
                                        continue;
                                    }

                                case 60:
                                    {
                                        Node node = this.New60();
                                        this.Push(this.GoTo(11), node, true);
                                        continue;
                                    }

                                case 61:
                                    {
                                        Node node = this.New61();
                                        this.Push(this.GoTo(12), node, true);
                                        continue;
                                    }

                                case 62:
                                    {
                                        Node node = this.New62();
                                        this.Push(this.GoTo(13), node, true);
                                        continue;
                                    }

                                case 63:
                                    {
                                        Node node = this.New63();
                                        this.Push(this.GoTo(14), node, true);
                                        continue;
                                    }

                                case 64:
                                    {
                                        Node node = this.New64();
                                        this.Push(this.GoTo(15), node, true);
                                        continue;
                                    }

                                case 65:
                                    {
                                        Node node = this.New65();
                                        this.Push(this.GoTo(16), node, true);
                                        continue;
                                    }

                                case 66:
                                    {
                                        Node node = this.New66();
                                        this.Push(this.GoTo(17), node, true);
                                        continue;
                                    }

                                case 67:
                                    {
                                        Node node = this.New67();
                                        this.Push(this.GoTo(18), node, true);
                                        continue;
                                    }

                                case 68:
                                    {
                                        Node node = this.New68();
                                        this.Push(this.GoTo(19), node, true);
                                        continue;
                                    }

                                case 69:
                                    {
                                        Node node = this.New69();
                                        this.Push(this.GoTo(20), node, true);
                                        continue;
                                    }

                                case 70:
                                    {
                                        Node node = this.New70();
                                        this.Push(this.GoTo(21), node, true);
                                        continue;
                                    }

                                case 71:
                                    {
                                        Node node = this.New71();
                                        this.Push(this.GoTo(22), node, true);
                                        continue;
                                    }

                                case 72:
                                    {
                                        Node node = this.New72();
                                        this.Push(this.GoTo(23), node, true);
                                        continue;
                                    }

                                case 73:
                                    {
                                        Node node = this.New73();
                                        this.Push(this.GoTo(24), node, true);
                                        continue;
                                    }

                                case 74:
                                    {
                                        Node node = this.New74();
                                        this.Push(this.GoTo(24), node, true);
                                        continue;
                                    }

                                case 75:
                                    {
                                        Node node = this.New75();
                                        this.Push(this.GoTo(25), node, true);
                                        continue;
                                    }

                                case 76:
                                    {
                                        Node node = this.New76();
                                        this.Push(this.GoTo(26), node, true);
                                        continue;
                                    }

                                case 77:
                                    {
                                        Node node = this.New77();
                                        this.Push(this.GoTo(27), node, true);
                                        continue;
                                    }

                                case 78:
                                    {
                                        Node node = this.New78();
                                        this.Push(this.GoTo(28), node, true);
                                        continue;
                                    }

                                case 79:
                                    {
                                        Node node = this.New79();
                                        this.Push(this.GoTo(29), node, true);
                                        continue;
                                    }

                                case 80:
                                    {
                                        Node node = this.New80();
                                        this.Push(this.GoTo(30), node, true);
                                        continue;
                                    }
                            }

                            continue;
                        }

                    case 2:
                        {
                            EOF node2 = (EOF)this.lexer.Next();
                            PProgram node3 = (PProgram)this.Pop();
                            Start node4 = new Start(node3, node2);
                            return node4;
                        }

                    case 3:
                        {
                            throw new ParserException(this.last_token, "[" + this.last_line + "," + this.last_pos + "] " + Parser.errorMessages[Parser.errors[this.action[1]]]);
                        }
                }
            }
        }

        protected virtual Node New0()
        {
            XPSubroutine node5 = (XPSubroutine)this.Pop();
            PReturn node6 = (PReturn)this.Pop();
            PJumpToSubroutine node7 = (PJumpToSubroutine)this.Pop();
            PRsaddCommand node8 = null;
            PSize node9 = (PSize)this.Pop();
            List<PSubroutine> subroutines = new List<PSubroutine>();
            if (node5 is X1PSubroutine x1)
            {
                PSubroutine sub = x1.GetPSubroutine();
                if (sub != null)
                {
                    subroutines.Add(sub);
                }
            }
            else if (node5 is X2PSubroutine x2)
            {
                PSubroutine sub = x2.GetPSubroutine();
                if (sub != null)
                {
                    subroutines.Add(sub);
                }
            }
            AProgram node10 = new AProgram(node9, node8, node7, node6, subroutines);
            return node10;
        }

        protected virtual Node New1()
        {
            PSubroutine node2 = (PSubroutine)this.Pop();
            XPSubroutine node3 = (XPSubroutine)this.Pop();
            X1PSubroutine node4 = new X1PSubroutine(node3, node2);
            return node4;
        }

        protected virtual Node New2()
        {
            PSubroutine node1 = (PSubroutine)this.Pop();
            X2PSubroutine node2 = new X2PSubroutine(node1);
            return node2;
        }

        protected virtual Node New3()
        {
            XPSubroutine node5 = (XPSubroutine)this.Pop();
            PReturn node6 = (PReturn)this.Pop();
            PJumpToSubroutine node7 = (PJumpToSubroutine)this.Pop();
            PRsaddCommand node8 = (PRsaddCommand)this.Pop();
            PSize node9 = (PSize)this.Pop();
            List<PSubroutine> subroutines = new List<PSubroutine>();
            if (node5 is X1PSubroutine x1)
            {
                PSubroutine sub = x1.GetPSubroutine();
                if (sub != null)
                {
                    subroutines.Add(sub);
                }
            }
            else if (node5 is X2PSubroutine x2)
            {
                PSubroutine sub = x2.GetPSubroutine();
                if (sub != null)
                {
                    subroutines.Add(sub);
                }
            }
            AProgram node10 = new AProgram(node9, node8, node7, node6, subroutines);
            return node10;
        }

        protected virtual Node New4()
        {
            PReturn node2 = (PReturn)this.Pop();
            PCommandBlock node3 = null;
            ASubroutine node4 = new ASubroutine(node3, node2);
            return node4;
        }

        protected virtual Node New5()
        {
            PReturn node2 = (PReturn)this.Pop();
            PCommandBlock node3 = (PCommandBlock)this.Pop();
            ASubroutine node4 = new ASubroutine(node3, node2);
            return node4;
        }

        protected virtual Node New6()
        {
            XPCmd node1 = (XPCmd)this.Pop();
            List<PCmd> cmds = new List<PCmd>();
            if (node1 is X1PCmd x1)
            {
                PCmd cmd = x1.GetPCmd();
                if (cmd != null)
                {
                    cmds.Add(cmd);
                }
            }
            else if (node1 is X2PCmd x2)
            {
                PCmd cmd = x2.GetPCmd();
                if (cmd != null)
                {
                    cmds.Add(cmd);
                }
            }
            ACommandBlock node2 = new ACommandBlock(cmds);
            return node2;
        }

        protected virtual Node New7()
        {
            PCmd node2 = (PCmd)this.Pop();
            XPCmd node3 = (XPCmd)this.Pop();
            X1PCmd node4 = new X1PCmd(node3, node2);
            return node4;
        }

        protected virtual Node New8()
        {
            PCmd node1 = (PCmd)this.Pop();
            X2PCmd node2 = new X2PCmd(node1);
            return node2;
        }

        protected virtual Node New9()
        {
            PRsaddCommand node1 = (PRsaddCommand)this.Pop();
            AAddVarCmd node2 = new AAddVarCmd(node1);
            return node2;
        }

        protected virtual Node New10()
        {
            PReturn node4 = (PReturn)this.Pop();
            PCommandBlock node5 = (PCommandBlock)this.Pop();
            PJumpCommand node6 = (PJumpCommand)this.Pop();
            PStoreStateCommand node7 = (PStoreStateCommand)this.Pop();
            AActionJumpCmd node8 = new AActionJumpCmd(node7, node6, node5, node4);
            return node8;
        }

        protected virtual Node New11()
        {
            PConstCommand node1 = (PConstCommand)this.Pop();
            AConstCmd node2 = new AConstCmd(node1);
            return node2;
        }

        protected virtual Node New12()
        {
            PCopyDownSpCommand node1 = (PCopyDownSpCommand)this.Pop();
            ACopydownspCmd node2 = new ACopydownspCmd(node1);
            return node2;
        }

        protected virtual Node New13()
        {
            PCopyTopSpCommand node1 = (PCopyTopSpCommand)this.Pop();
            ACopytopspCmd node2 = new ACopytopspCmd(node1);
            return node2;
        }

        protected virtual Node New14()
        {
            PCopyDownBpCommand node1 = (PCopyDownBpCommand)this.Pop();
            ACopydownbpCmd node2 = new ACopydownbpCmd(node1);
            return node2;
        }

        protected virtual Node New15()
        {
            PCopyTopBpCommand node1 = (PCopyTopBpCommand)this.Pop();
            ACopytopbpCmd node2 = new ACopytopbpCmd(node1);
            return node2;
        }

        protected virtual Node New16()
        {
            PConditionalJumpCommand node1 = (PConditionalJumpCommand)this.Pop();
            ACondJumpCmd node2 = new ACondJumpCmd(node1);
            return node2;
        }

        protected virtual Node New17()
        {
            PJumpCommand node1 = (PJumpCommand)this.Pop();
            AJumpCmd node2 = new AJumpCmd(node1);
            return node2;
        }

        protected virtual Node New18()
        {
            PJumpToSubroutine node1 = (PJumpToSubroutine)this.Pop();
            AJumpSubCmd node2 = new AJumpSubCmd(node1);
            return node2;
        }

        protected virtual Node New19()
        {
            PMoveSpCommand node1 = (PMoveSpCommand)this.Pop();
            AMovespCmd node2 = new AMovespCmd(node1);
            return node2;
        }

        protected virtual Node New20()
        {
            PLogiiCommand node1 = (PLogiiCommand)this.Pop();
            ALogiiCmd node2 = new ALogiiCmd(node1);
            return node2;
        }

        protected virtual Node New21()
        {
            PUnaryCommand node1 = (PUnaryCommand)this.Pop();
            AUnaryCmd node2 = new AUnaryCmd(node1);
            return node2;
        }

        protected virtual Node New22()
        {
            PBinaryCommand node1 = (PBinaryCommand)this.Pop();
            ABinaryCmd node2 = new ABinaryCmd(node1);
            return node2;
        }

        protected virtual Node New23()
        {
            PDestructCommand node1 = (PDestructCommand)this.Pop();
            ADestructCmd node2 = new ADestructCmd(node1);
            return node2;
        }

        protected virtual Node New24()
        {
            PBpCommand node1 = (PBpCommand)this.Pop();
            ABpCmd node2 = new ABpCmd(node1);
            return node2;
        }

        protected virtual Node New25()
        {
            AST.PActionCommand node1 = (AST.PActionCommand)this.Pop();
            AActionCmd node2 = new AActionCmd(node1);
            return node2;
        }

        protected virtual Node New26()
        {
            PStackCommand node1 = (PStackCommand)this.Pop();
            AStackOpCmd node2 = new AStackOpCmd(node1);
            return node2;
        }

        protected virtual Node New27()
        {
            TLogandii node1 = (TLogandii)this.Pop();
            AAndLogiiOp node2 = new AAndLogiiOp(node1);
            return node2;
        }

        protected virtual Node New28()
        {
            TLogorii node1 = (TLogorii)this.Pop();
            AOrLogiiOp node2 = new AOrLogiiOp(node1);
            return node2;
        }

        protected virtual Node New29()
        {
            TIncorii node1 = (TIncorii)this.Pop();
            AInclOrLogiiOp node2 = new AInclOrLogiiOp(node1);
            return node2;
        }

        protected virtual Node New30()
        {
            TExcorii node1 = (TExcorii)this.Pop();
            AExclOrLogiiOp node2 = new AExclOrLogiiOp(node1);
            return node2;
        }

        protected virtual Node New31()
        {
            TBoolandii node1 = (TBoolandii)this.Pop();
            ABitAndLogiiOp node2 = new ABitAndLogiiOp(node1);
            return node2;
        }

        protected virtual Node New32()
        {
            TEqual node1 = (TEqual)this.Pop();
            AEqualBinaryOp node2 = new AEqualBinaryOp(node1);
            return node2;
        }

        protected virtual Node New33()
        {
            TNequal node1 = (TNequal)this.Pop();
            ANequalBinaryOp node2 = new ANequalBinaryOp(node1);
            return node2;
        }

        protected virtual Node New34()
        {
            TGeq node1 = (TGeq)this.Pop();
            AGeqBinaryOp node2 = new AGeqBinaryOp(node1);
            return node2;
        }

        protected virtual Node New35()
        {
            TGt node1 = (TGt)this.Pop();
            AGtBinaryOp node2 = new AGtBinaryOp(node1);
            return node2;
        }

        protected virtual Node New36()
        {
            TLt node1 = (TLt)this.Pop();
            ALtBinaryOp node2 = new ALtBinaryOp(node1);
            return node2;
        }

        protected virtual Node New37()
        {
            TLeq node1 = (TLeq)this.Pop();
            ALeqBinaryOp node2 = new ALeqBinaryOp(node1);
            return node2;
        }

        protected virtual Node New38()
        {
            TShright node1 = (TShright)this.Pop();
            AShrightBinaryOp node2 = new AShrightBinaryOp(node1);
            return node2;
        }

        protected virtual Node New39()
        {
            TShleft node1 = (TShleft)this.Pop();
            AShleftBinaryOp node2 = new AShleftBinaryOp(node1);
            return node2;
        }

        protected virtual Node New40()
        {
            TUnright node1 = (TUnright)this.Pop();
            AUnrightBinaryOp node2 = new AUnrightBinaryOp(node1);
            return node2;
        }

        protected virtual Node New41()
        {
            TAdd node1 = (TAdd)this.Pop();
            AAddBinaryOp node2 = new AAddBinaryOp(node1);
            return node2;
        }

        protected virtual Node New42()
        {
            TSub node1 = (TSub)this.Pop();
            ASubBinaryOp node2 = new ASubBinaryOp(node1);
            return node2;
        }

        protected virtual Node New43()
        {
            TMul node1 = (TMul)this.Pop();
            AMulBinaryOp node2 = new AMulBinaryOp(node1);
            return node2;
        }

        protected virtual Node New44()
        {
            TDiv node1 = (TDiv)this.Pop();
            ADivBinaryOp node2 = new ADivBinaryOp(node1);
            return node2;
        }

        protected virtual Node New45()
        {
            TMod node1 = (TMod)this.Pop();
            AModBinaryOp node2 = new AModBinaryOp(node1);
            return node2;
        }

        protected virtual Node New46()
        {
            TNeg node1 = (TNeg)this.Pop();
            ANegUnaryOp node2 = new ANegUnaryOp(node1);
            return node2;
        }

        protected virtual Node New47()
        {
            TComp node1 = (TComp)this.Pop();
            ACompUnaryOp node2 = new ACompUnaryOp(node1);
            return node2;
        }

        protected virtual Node New48()
        {
            TNot node1 = (TNot)this.Pop();
            ANotUnaryOp node2 = new ANotUnaryOp(node1);
            return node2;
        }

        protected virtual Node New49()
        {
            TDecisp node1 = (TDecisp)this.Pop();
            ADecispStackOp node2 = new ADecispStackOp(node1);
            return node2;
        }

        protected virtual Node New50()
        {
            TIncisp node1 = (TIncisp)this.Pop();
            AIncispStackOp node2 = new AIncispStackOp(node1);
            return node2;
        }

        protected virtual Node New51()
        {
            TDecibp node1 = (TDecibp)this.Pop();
            ADecibpStackOp node2 = new ADecibpStackOp(node1);
            return node2;
        }

        protected virtual Node New52()
        {
            TIncibp node1 = (TIncibp)this.Pop();
            AIncibpStackOp node2 = new AIncibpStackOp(node1);
            return node2;
        }

        protected virtual Node New53()
        {
            TIntegerConstant node1 = (TIntegerConstant)this.Pop();
            AIntConstant node2 = new AIntConstant(node1);
            return node2;
        }

        protected virtual Node New54()
        {
            TFloatConstant node1 = (TFloatConstant)this.Pop();
            AFloatConstant node2 = new AFloatConstant(node1);
            return node2;
        }

        protected virtual Node New55()
        {
            TStringLiteral node1 = (TStringLiteral)this.Pop();
            AStringConstant node2 = new AStringConstant(node1);
            return node2;
        }

        protected virtual Node New56()
        {
            TJz node1 = (TJz)this.Pop();
            AZeroJumpIf node2 = new AZeroJumpIf(node1);
            return node2;
        }

        protected virtual Node New57()
        {
            TJnz node1 = (TJnz)this.Pop();
            ANonzeroJumpIf node2 = new ANonzeroJumpIf(node1);
            return node2;
        }

        protected virtual Node New58()
        {
            TSavebp node1 = (TSavebp)this.Pop();
            ASavebpBpOp node2 = new ASavebpBpOp(node1);
            return node2;
        }

        protected virtual Node New59()
        {
            TRestorebp node1 = (TRestorebp)this.Pop();
            ARestorebpBpOp node2 = new ARestorebpBpOp(node1);
            return node2;
        }

        protected virtual Node New60()
        {
            TSemi node5 = (TSemi)this.Pop();
            TIntegerConstant node6 = (TIntegerConstant)this.Pop();
            TIntegerConstant node7 = (TIntegerConstant)this.Pop();
            TIntegerConstant node8 = (TIntegerConstant)this.Pop();
            PJumpIf node9 = (PJumpIf)this.Pop();
            AConditionalJumpCommand node10 = new AConditionalJumpCommand(node9, node8, node7, node6, node5);
            return node10;
        }

        protected virtual Node New61()
        {
            TSemi node5 = (TSemi)this.Pop();
            TIntegerConstant node6 = (TIntegerConstant)this.Pop();
            TIntegerConstant node7 = (TIntegerConstant)this.Pop();
            TIntegerConstant node8 = (TIntegerConstant)this.Pop();
            TJmp node9 = (TJmp)this.Pop();
            AJumpCommand node10 = new AJumpCommand(node9, node8, node7, node6, node5);
            return node10;
        }

        protected virtual Node New62()
        {
            TSemi node5 = (TSemi)this.Pop();
            TIntegerConstant node6 = (TIntegerConstant)this.Pop();
            TIntegerConstant node7 = (TIntegerConstant)this.Pop();
            TIntegerConstant node8 = (TIntegerConstant)this.Pop();
            TJsr node9 = (TJsr)this.Pop();
            AJumpToSubroutine node10 = new AJumpToSubroutine(node9, node8, node7, node6, node5);
            return node10;
        }

        protected virtual Node New63()
        {
            TSemi node4 = (TSemi)this.Pop();
            TIntegerConstant node5 = (TIntegerConstant)this.Pop();
            TIntegerConstant node6 = (TIntegerConstant)this.Pop();
            TRetn node7 = (TRetn)this.Pop();
            AReturn node8 = new AReturn(node7, node6, node5, node4);
            return node8;
        }

        protected virtual Node New64()
        {
            TSemi node6 = (TSemi)this.Pop();
            TIntegerConstant node7 = (TIntegerConstant)this.Pop();
            TIntegerConstant node8 = (TIntegerConstant)this.Pop();
            TIntegerConstant node9 = (TIntegerConstant)this.Pop();
            TIntegerConstant node10 = (TIntegerConstant)this.Pop();
            TCpdownsp node11 = (TCpdownsp)this.Pop();
            ACopyDownSpCommand node12 = new ACopyDownSpCommand(node11, node10, node9, node8, node7, node6);
            return node12;
        }

        protected virtual Node New65()
        {
            TSemi node6 = (TSemi)this.Pop();
            TIntegerConstant node7 = (TIntegerConstant)this.Pop();
            TIntegerConstant node8 = (TIntegerConstant)this.Pop();
            TIntegerConstant node9 = (TIntegerConstant)this.Pop();
            TIntegerConstant node10 = (TIntegerConstant)this.Pop();
            TCptopsp node11 = (TCptopsp)this.Pop();
            ACopyTopSpCommand node12 = new ACopyTopSpCommand(node11, node10, node9, node8, node7, node6);
            return node12;
        }

        protected virtual Node New66()
        {
            TSemi node6 = (TSemi)this.Pop();
            TIntegerConstant node7 = (TIntegerConstant)this.Pop();
            TIntegerConstant node8 = (TIntegerConstant)this.Pop();
            TIntegerConstant node9 = (TIntegerConstant)this.Pop();
            TIntegerConstant node10 = (TIntegerConstant)this.Pop();
            TCpdownbp node11 = (TCpdownbp)this.Pop();
            ACopyDownBpCommand node12 = new ACopyDownBpCommand(node11, node10, node9, node8, node7, node6);
            return node12;
        }

        protected virtual Node New67()
        {
            TSemi node6 = (TSemi)this.Pop();
            TIntegerConstant node7 = (TIntegerConstant)this.Pop();
            TIntegerConstant node8 = (TIntegerConstant)this.Pop();
            TIntegerConstant node9 = (TIntegerConstant)this.Pop();
            TIntegerConstant node10 = (TIntegerConstant)this.Pop();
            TCptopbp node11 = (TCptopbp)this.Pop();
            ACopyTopBpCommand node12 = new ACopyTopBpCommand(node11, node10, node9, node8, node7, node6);
            return node12;
        }

        protected virtual Node New68()
        {
            TSemi node5 = (TSemi)this.Pop();
            TIntegerConstant node6 = (TIntegerConstant)this.Pop();
            TIntegerConstant node7 = (TIntegerConstant)this.Pop();
            TIntegerConstant node8 = (TIntegerConstant)this.Pop();
            TMovsp node9 = (TMovsp)this.Pop();
            AMoveSpCommand node10 = new AMoveSpCommand(node9, node8, node7, node6, node5);
            return node10;
        }

        protected virtual Node New69()
        {
            TSemi node4 = (TSemi)this.Pop();
            TIntegerConstant node5 = (TIntegerConstant)this.Pop();
            TIntegerConstant node6 = (TIntegerConstant)this.Pop();
            TRsadd node7 = (TRsadd)this.Pop();
            ARsaddCommand node8 = new ARsaddCommand(node7, node6, node5, node4);
            return node8;
        }

        protected virtual Node New70()
        {
            TSemi node5 = (TSemi)this.Pop();
            PConstant node6 = (PConstant)this.Pop();
            TIntegerConstant node7 = (TIntegerConstant)this.Pop();
            TIntegerConstant node8 = (TIntegerConstant)this.Pop();
            TConst node9 = (TConst)this.Pop();
            AConstCommand node10 = new AConstCommand(node9, node8, node7, node6, node5);
            return node10;
        }

        protected virtual Node New71()
        {
            TSemi node6 = (TSemi)this.Pop();
            TIntegerConstant node7 = (TIntegerConstant)this.Pop();
            TIntegerConstant node8 = (TIntegerConstant)this.Pop();
            TIntegerConstant node9 = (TIntegerConstant)this.Pop();
            TIntegerConstant node10 = (TIntegerConstant)this.Pop();
            TAction node11 = (TAction)this.Pop();
            AActionCommand node12 = new AActionCommand(node11, node10, node9, node8, node7, node6);
            return node12;
        }

        protected virtual Node New72()
        {
            TSemi node4 = (TSemi)this.Pop();
            TIntegerConstant node5 = (TIntegerConstant)this.Pop();
            TIntegerConstant node6 = (TIntegerConstant)this.Pop();
            PLogiiOp node7 = (PLogiiOp)this.Pop();
            ALogiiCommand node8 = new ALogiiCommand(node7, node6, node5, node4);
            return node8;
        }

        protected virtual Node New73()
        {
            TSemi node5 = (TSemi)this.Pop();
            TIntegerConstant node6 = null;
            TIntegerConstant node7 = (TIntegerConstant)this.Pop();
            TIntegerConstant node8 = (TIntegerConstant)this.Pop();
            PBinaryOp node9 = (PBinaryOp)this.Pop();
            ABinaryCommand node10 = new ABinaryCommand(node9, node8, node7, node6, node5);
            return node10;
        }

        protected virtual Node New74()
        {
            TSemi node5 = (TSemi)this.Pop();
            TIntegerConstant node6 = (TIntegerConstant)this.Pop();
            TIntegerConstant node7 = (TIntegerConstant)this.Pop();
            TIntegerConstant node8 = (TIntegerConstant)this.Pop();
            PBinaryOp node9 = (PBinaryOp)this.Pop();
            ABinaryCommand node10 = new ABinaryCommand(node9, node8, node7, node6, node5);
            return node10;
        }

        protected virtual Node New75()
        {
            TSemi node4 = (TSemi)this.Pop();
            TIntegerConstant node5 = (TIntegerConstant)this.Pop();
            TIntegerConstant node6 = (TIntegerConstant)this.Pop();
            PUnaryOp node7 = (PUnaryOp)this.Pop();
            AUnaryCommand node8 = new AUnaryCommand(node7, node6, node5, node4);
            return node8;
        }

        protected virtual Node New76()
        {
            TSemi node5 = (TSemi)this.Pop();
            TIntegerConstant node6 = (TIntegerConstant)this.Pop();
            TIntegerConstant node7 = (TIntegerConstant)this.Pop();
            TIntegerConstant node8 = (TIntegerConstant)this.Pop();
            PStackOp node9 = (PStackOp)this.Pop();
            AStackCommand node10 = new AStackCommand(node9, node8, node7, node6, node5);
            return node10;
        }

        protected virtual Node New77()
        {
            TSemi node7 = (TSemi)this.Pop();
            TIntegerConstant node8 = (TIntegerConstant)this.Pop();
            TIntegerConstant node9 = (TIntegerConstant)this.Pop();
            TIntegerConstant node10 = (TIntegerConstant)this.Pop();
            TIntegerConstant node11 = (TIntegerConstant)this.Pop();
            TIntegerConstant node12 = (TIntegerConstant)this.Pop();
            TDestruct node13 = (TDestruct)this.Pop();
            ADestructCommand node14 = new ADestructCommand(node13, node12, node11, node10, node9, node8, node7);
            return node14;
        }

        protected virtual Node New78()
        {
            TSemi node4 = (TSemi)this.Pop();
            TIntegerConstant node5 = (TIntegerConstant)this.Pop();
            TIntegerConstant node6 = (TIntegerConstant)this.Pop();
            PBpOp node7 = (PBpOp)this.Pop();
            ABpCommand node8 = new ABpCommand(node7, node6, node5, node4);
            return node8;
        }

        protected virtual Node New79()
        {
            TSemi node6 = (TSemi)this.Pop();
            TIntegerConstant node7 = (TIntegerConstant)this.Pop();
            TIntegerConstant node8 = (TIntegerConstant)this.Pop();
            TIntegerConstant node9 = (TIntegerConstant)this.Pop();
            TIntegerConstant node10 = (TIntegerConstant)this.Pop();
            TStorestate node11 = (TStorestate)this.Pop();
            AStoreStateCommand node12 = new AStoreStateCommand(node11, node10, node9, node8, node7, node6);
            return node12;
        }

        protected virtual Node New80()
        {
            TSemi node4 = (TSemi)this.Pop();
            TIntegerConstant node5 = (TIntegerConstant)this.Pop();
            TIntegerConstant node6 = (TIntegerConstant)this.Pop();
            TT node7 = (TT)this.Pop();
            ASize node8 = new ASize(node7, node6, node5, node4);
            return node8;
        }
    }
}




