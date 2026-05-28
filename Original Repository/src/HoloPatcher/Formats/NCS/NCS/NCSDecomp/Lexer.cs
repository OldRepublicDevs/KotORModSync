// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/lexer/Lexer.java:71-1050
// Original: public class Lexer
// Note: This file is generated from lexer grammar, matching the Java implementation structure
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;

namespace KOTORModSync.Formats.NCS.NCSDecomp.Lexer
{
    public class Lexer
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/lexer/Lexer.java:72-81
        // Original: private static int[][][][] gotoTable; private static int[][] accept; protected Token token; protected Lexer.State state = Lexer.State.INITIAL; private PushbackReader in; private int line; private int pos; private boolean cr; private boolean eof; private final StringBuffer text = new StringBuffer();
        private static int[][][][] gotoTable;
        private static int[][] accept;
        protected Token token;
        protected State state;
        private PushbackReader @in;
        private int line;
        private int pos;
        private bool cr;
        private bool eof;
        private readonly StringBuilder text;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/lexer/Lexer.java:83-103
        // Original: public Lexer(PushbackReader in) { this.in = in; if (gotoTable == null) { ... } }
        public Lexer(PushbackReader @in)
        {
            this.state = State.INITIAL;
            this.text = new StringBuilder();
            this.@in = @in;
            if (Lexer.gotoTable == null)
            {
                try
                {
                    System.IO.Stream stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("KOTORModSync.Formats.NCS.NCSDecomp.lexer.dat") ?? System.Reflection.Assembly.GetCallingAssembly().GetManifestResourceStream("KOTORModSync.Formats.NCS.NCSDecomp.lexer.dat");
                    if (stream == null)
                    {
                        throw new Exception("The file \"lexer.dat\" is either missing or corrupted.");
                    }
                    System.IO.BinaryReader s = new System.IO.BinaryReader(stream);
                    int length = s.ReadInt32();
                    Lexer.gotoTable = new int[length][][][];
                    for (int i = 0; i < Lexer.gotoTable.Length; ++i)
                    {
                        length = s.ReadInt32();
                        Lexer.gotoTable[i] = new int[length][][];
                        for (int j = 0; j < Lexer.gotoTable[i].Length; ++j)
                        {
                            length = s.ReadInt32();
                            Lexer.gotoTable[i][j] = new int[length][];
                            for (int k = 0; k < length; ++k)
                            {
                                Lexer.gotoTable[i][j][k] = new int[3];
                            }
                            for (int k = 0; k < Lexer.gotoTable[i][j].Length; ++k)
                            {
                                for (int l = 0; l < 3; ++l)
                                {
                                    Lexer.gotoTable[i][j][k][l] = s.ReadInt32();
                                }
                            }
                        }
                    }

                    length = s.ReadInt32();
                    Lexer.accept = new int[length][];
                    for (int i = 0; i < Lexer.accept.Length; ++i)
                    {
                        length = s.ReadInt32();
                        Lexer.accept[i] = new int[length];
                        for (int j = 0; j < Lexer.accept[i].Length; ++j)
                        {
                            Lexer.accept[i][j] = s.ReadInt32();
                        }
                    }

                    s.Dispose();
                }
                catch (Exception)
                {
                    throw new Exception("The file \"lexer.dat\" is either missing or corrupted.");
                }
            }
        }

        protected virtual void Filter()
        {
        }

        public virtual Token Peek()
        {
            while (this.token == null)
            {
                this.token = this.GetToken();
                this.Filter();
            }

            return this.token;
        }

        public virtual Token Next()
        {
            while (this.token == null)
            {
                this.token = this.GetToken();
                this.Filter();
            }

            Token result = this.token;
            this.token = null;
            return result;
        }

        protected virtual Token GetToken()
        {
            int dfa_state = 0;
            int start_pos = this.pos;
            int start_line = this.line;
            int accept_state = -1;
            int accept_token = -1;
            int accept_length = -1;
            int accept_pos = -1;
            int accept_line = -1;
            int[][][] gotoTable = Lexer.gotoTable[this.state.Id()];
            int[] accept = Lexer.accept[this.state.Id()];
            this.text.Clear();
            while (true)
            {
                int c = this.GetChar();
                if (c != -1)
                {
                    switch (c)
                    {
                        case 10:
                            {
                                if (this.cr)
                                {
                                    this.cr = false;
                                    break;
                                }

                                ++this.line;
                                this.pos = 0;
                                break;
                            }

                        case 13:
                            {
                                ++this.line;
                                this.pos = 0;
                                this.cr = true;
                                break;
                            }

                        default:
                            {
                                ++this.pos;
                                this.cr = false;
                                break;
                            }
                    }

                    this.text.Append((char)c);
                    do
                    {
                        int oldState = (dfa_state < -1) ? (-2 - dfa_state) : dfa_state;
                        dfa_state = -1;
                        int[][] tmp1 = gotoTable[oldState];
                        int low = 0;
                        int high = tmp1.Length - 1;
                        while (low <= high)
                        {
                            int middle = (low + high) / 2;
                            int[] tmp2 = tmp1[middle];
                            if (c < tmp2[0])
                            {
                                high = middle - 1;
                            }
                            else
                            {
                                if (c <= tmp2[1])
                                {
                                    dfa_state = tmp2[2];
                                    break;
                                }

                                low = middle + 1;
                            }
                        }
                    }
                    while (dfa_state < -1);
                }
                else
                {
                    dfa_state = -1;
                }

                if (dfa_state >= 0)
                {
                    if (accept[dfa_state] == -1)
                    {
                        continue;
                    }

                    accept_state = dfa_state;
                    accept_token = accept[dfa_state];
                    accept_length = this.text.Length;
                    accept_pos = this.pos;
                    accept_line = this.line;
                }
                else if (accept_state != -1)
                {
                    switch (accept_token)
                    {
                        case 0:
                            {
                                Token token = this.New0(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 1:
                            {
                                Token token = this.New1(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 2:
                            {
                                Token token = this.New2(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 3:
                            {
                                Token token = this.New3(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 4:
                            {
                                Token token = this.New4(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 5:
                            {
                                Token token = this.New5(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 6:
                            {
                                Token token = this.New6(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 7:
                            {
                                Token token = this.New7(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 8:
                            {
                                Token token = this.New8(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 9:
                            {
                                Token token = this.New9(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 10:
                            {
                                Token token = this.New10(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 11:
                            {
                                Token token = this.New11(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 12:
                            {
                                Token token = this.New12(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 13:
                            {
                                Token token = this.New13(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 14:
                            {
                                Token token = this.New14(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 15:
                            {
                                Token token = this.New15(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 16:
                            {
                                Token token = this.New16(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 17:
                            {
                                Token token = this.New17(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 18:
                            {
                                Token token = this.New18(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 19:
                            {
                                Token token = this.New19(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 20:
                            {
                                Token token = this.New20(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 21:
                            {
                                Token token = this.New21(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 22:
                            {
                                Token token = this.New22(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 23:
                            {
                                Token token = this.New23(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 24:
                            {
                                Token token = this.New24(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 25:
                            {
                                Token token = this.New25(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 26:
                            {
                                Token token = this.New26(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 27:
                            {
                                Token token = this.New27(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 28:
                            {
                                Token token = this.New28(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 29:
                            {
                                Token token = this.New29(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 30:
                            {
                                Token token = this.New30(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 31:
                            {
                                Token token = this.New31(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 32:
                            {
                                Token token = this.New32(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 33:
                            {
                                Token token = this.New33(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 34:
                            {
                                Token token = this.New34(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 35:
                            {
                                Token token = this.New35(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 36:
                            {
                                Token token = this.New36(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 37:
                            {
                                Token token = this.New37(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 38:
                            {
                                Token token = this.New38(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 39:
                            {
                                Token token = this.New39(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 40:
                            {
                                Token token = this.New40(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 41:
                            {
                                Token token = this.New41(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 42:
                            {
                                Token token = this.New42(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 43:
                            {
                                Token token = this.New43(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 44:
                            {
                                Token token = this.New44(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 45:
                            {
                                Token token = this.New45(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 46:
                            {
                                Token token = this.New46(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 47:
                            {
                                Token token = this.New47(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 48:
                            {
                                Token token = this.New48(start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 49:
                            {
                                Token token = this.New49(this.GetText(accept_length), start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 50:
                            {
                                Token token = this.New50(this.GetText(accept_length), start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 51:
                            {
                                Token token = this.New51(this.GetText(accept_length), start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        case 52:
                            {
                                Token token = this.New52(this.GetText(accept_length), start_line + 1, start_pos + 1);
                                this.PushBack(accept_length);
                                this.pos = accept_pos;
                                this.line = accept_line;
                                return token;
                            }

                        default:
                            {
                                continue;
                            }
                    }
                }
                else
                {
                    if (this.text.Length > 0)
                    {
                        throw new LexerException("[" + (start_line + 1) + "," + (start_pos + 1) + "]" + " Unknown token: " + (object)this.text);
                    }

                    EOF token2 = new EOF(start_line + 1, start_pos + 1);
                    return token2;
                }
            }
        }

        protected virtual Token New0(int line, int pos)
        {
            return new TLPar(line, pos);
        }

        protected virtual Token New1(int line, int pos)
        {
            return new TRPar(line, pos);
        }

        protected virtual Token New2(int line, int pos)
        {
            return new TSemi(line, pos);
        }

        protected virtual Token New3(int line, int pos)
        {
            return new TDot(line, pos);
        }

        protected virtual Token New4(int line, int pos)
        {
            return new TCpdownsp(line, pos);
        }

        protected virtual Token New5(int line, int pos)
        {
            return new TRsadd(line, pos);
        }

        protected virtual Token New6(int line, int pos)
        {
            return new TCptopsp(line, pos);
        }

        protected virtual Token New7(int line, int pos)
        {
            return new TConst(line, pos);
        }

        protected virtual Token New8(int line, int pos)
        {
            return new TAction(line, pos);
        }

        protected virtual Token New9(int line, int pos)
        {
            return new TLogandii(line, pos);
        }

        protected virtual Token New10(int line, int pos)
        {
            return new TLogorii(line, pos);
        }

        protected virtual Token New11(int line, int pos)
        {
            return new TIncorii(line, pos);
        }

        protected virtual Token New12(int line, int pos)
        {
            return new TExcorii(line, pos);
        }

        protected virtual Token New13(int line, int pos)
        {
            return new TBoolandii(line, pos);
        }

        protected virtual Token New14(int line, int pos)
        {
            return new TEqual(line, pos);
        }

        protected virtual Token New15(int line, int pos)
        {
            return new TNequal(line, pos);
        }

        protected virtual Token New16(int line, int pos)
        {
            return new TGeq(line, pos);
        }

        protected virtual Token New17(int line, int pos)
        {
            return new TGt(line, pos);
        }

        protected virtual Token New18(int line, int pos)
        {
            return new TLt(line, pos);
        }

        protected virtual Token New19(int line, int pos)
        {
            return new TLeq(line, pos);
        }

        protected virtual Token New20(int line, int pos)
        {
            return new TShleft(line, pos);
        }

        protected virtual Token New21(int line, int pos)
        {
            return new TShright(line, pos);
        }

        protected virtual Token New22(int line, int pos)
        {
            return new TUnright(line, pos);
        }

        protected virtual Token New23(int line, int pos)
        {
            return new TAdd(line, pos);
        }

        protected virtual Token New24(int line, int pos)
        {
            return new TSub(line, pos);
        }

        protected virtual Token New25(int line, int pos)
        {
            return new TMul(line, pos);
        }

        protected virtual Token New26(int line, int pos)
        {
            return new TDiv(line, pos);
        }

        protected virtual Token New27(int line, int pos)
        {
            return new TMod(line, pos);
        }

        protected virtual Token New28(int line, int pos)
        {
            return new TNeg(line, pos);
        }

        protected virtual Token New29(int line, int pos)
        {
            return new TComp(line, pos);
        }

        protected virtual Token New30(int line, int pos)
        {
            return new TMovsp(line, pos);
        }

        protected virtual Token New31(int line, int pos)
        {
            return new TJmp(line, pos);
        }

        protected virtual Token New32(int line, int pos)
        {
            return new TJsr(line, pos);
        }

        protected virtual Token New33(int line, int pos)
        {
            return new TJz(line, pos);
        }

        protected virtual Token New34(int line, int pos)
        {
            return new TRetn(line, pos);
        }

        protected virtual Token New35(int line, int pos)
        {
            return new TDestruct(line, pos);
        }

        protected virtual Token New36(int line, int pos)
        {
            return new TNot(line, pos);
        }

        protected virtual Token New37(int line, int pos)
        {
            return new TDecisp(line, pos);
        }

        protected virtual Token New38(int line, int pos)
        {
            return new TIncisp(line, pos);
        }

        protected virtual Token New39(int line, int pos)
        {
            return new TJnz(line, pos);
        }

        protected virtual Token New40(int line, int pos)
        {
            return new TCpdownbp(line, pos);
        }

        protected virtual Token New41(int line, int pos)
        {
            return new TCptopbp(line, pos);
        }

        protected virtual Token New42(int line, int pos)
        {
            return new TDecibp(line, pos);
        }

        protected virtual Token New43(int line, int pos)
        {
            return new TIncibp(line, pos);
        }

        protected virtual Token New44(int line, int pos)
        {
            return new TSavebp(line, pos);
        }

        protected virtual Token New45(int line, int pos)
        {
            return new TRestorebp(line, pos);
        }

        protected virtual Token New46(int line, int pos)
        {
            return new TStorestate(line, pos);
        }

        protected virtual Token New47(int line, int pos)
        {
            return new TNop(line, pos);
        }

        protected virtual Token New48(int line, int pos)
        {
            return new TT(line, pos);
        }

        protected virtual Token New49(string text, int line, int pos)
        {
            return new TStringLiteral(text, line, pos);
        }

        protected virtual Token New50(string text, int line, int pos)
        {
            return new TBlank(text, line, pos);
        }

        protected virtual Token New51(string text, int line, int pos)
        {
            return new TIntegerConstant(text, line, pos);
        }

        protected virtual Token New52(string text, int line, int pos)
        {
            return new TFloatConstant(text, line, pos);
        }

        private int GetChar()
        {
            if (this.eof)
            {
                return -1;
            }

            int result = this.@in.Read();
            if (result == -1)
            {
                this.eof = true;
            }

            return result;
        }

        private void PushBack(int acceptLength)
        {
            int length = this.text.Length;
            for (int i = length - 1; i >= acceptLength; --i)
            {
                this.eof = false;
                this.@in.Unread(this.text[i]);
            }
        }

        protected virtual void Unread(Token token)
        {
            string text = token.GetText();
            int length = text.Length;
            for (int i = length - 1; i >= 0; --i)
            {
                this.eof = false;
                this.@in.Unread(text[i]);
            }

            this.pos = token.GetPos() - 1;
            this.line = token.GetLine() - 1;
        }

        private string GetText(int acceptLength)
        {
            StringBuilder s = new StringBuilder(acceptLength);
            for (int i = 0; i < acceptLength; ++i)
            {
                s.Append(this.text[i]);
            }

            return s.ToString();
        }

        public class State
        {
            public static readonly State INITIAL;
            private int id;
            static State()
            {
                INITIAL = new State(0);
            }

            private State(int id)
            {
                this.id = id;
            }

            public virtual int Id()
            {
                return this.id;
            }
        }
    }
}




