using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KOTORModSync.Common.Script;
using JetBrains.Annotations;

namespace KOTORModSync.Formats.NCS.Compiler.NSS
{
    public class NssParser
    {
        private readonly List<ScriptFunction> _functions;
        private readonly List<ScriptConstant> _constants;
        private Dictionary<string, byte[]> _library;
        private readonly List<string> _lookupPaths;
        private List<NssTokenBase> _tokens;
        private int _tokenIndex;
        private List<string> _errors;

        public NssParser(
            List<ScriptFunction> functions,
            List<ScriptConstant> constants,
            Dictionary<string, byte[]> library,
            [JetBrains.Annotations.CanBeNull] List<string> lookupPaths = null)
        {
            _functions = functions;
            _constants = constants;
            _library = library;
            _lookupPaths = lookupPaths ?? new List<string>();
            _errors = new List<string>();
        }

        public Dictionary<string, byte[]> Library
        {
            get => _library;
            set => _library = value;
        }

        public List<ScriptConstant> Constants { get; set; }

        public CodeRoot Parse(string source)
        {
            // Strip BOM (Byte Order Mark) if present
            if (source.Length > 0 && source[0] == '\uFEFF')
            {
                source = source.Substring(1);
            }

            // Lex the source
            var lexer = new NssLexer();
            int lexResult = lexer.Analyse(source);
            if (lexResult != 0)
            {
                throw new CompileError("Lexer failed to tokenize source");
            }

            _tokens = lexer.Tokens;
            _tokenIndex = 0;

            var root = new CodeRoot(_constants, _functions, _lookupPaths, _library);

            // Parse all top-level objects
            while (true)
            {
                SkipWhitespaceAndComments();

                if (IsAtEnd())
                {
                    break;
                }

                // Try to parse different top-level constructs
                TopLevelObject obj = null;

                // Try include
                obj = TryParseInclude();
                if (obj != null)
                {
                    root.Objects.Add(obj);
                    continue;
                }

                // Try struct definition
                obj = TryParseStructDefinition();
                if (obj != null)
                {
                    root.Objects.Add(obj);
                    continue;
                }

                // Try function forward declaration (before function definition, since it's more specific)
                obj = TryParseFunctionForwardDeclaration();
                if (obj != null)
                {
                    root.Objects.Add(obj);
                    continue;
                }

                // Try global variable before function definitions to allow globals preceding functions
                obj = TryParseGlobalVariable();
                if (obj != null)
                {
                    root.Objects.Add(obj);
                    continue;
                }

                // Try function definition
                obj = TryParseFunctionDefinition();
                if (obj != null)
                {
                    root.Objects.Add(obj);
                    continue;
                }

                // If we got here, we couldn't parse anything
                if (!IsAtEnd())
                {
                    NssTokenBase token = CurrentToken();
                    string tokenValue = "";
                    if (token is NssIdentifier ident)
                    {
                        tokenValue = $" '{ident.Identifier}'";
                    }
                    else if (token is NssKeyword kw)
                    {
                        tokenValue = $" '{kw.Keyword}'";
                    }
                    else if (token is NssLiteral lit)
                    {
                        tokenValue = $" '{lit.Literal}'";
                    }

                    // Dump some context
                    string context = "";
                    for (int i = Math.Max(0, _tokenIndex - 3); i < Math.Min(_tokens.Count, _tokenIndex + 3); i++)
                    {
                        var t = _tokens[i];
                        string v = "";
                        if (t is NssIdentifier id) v = id.Identifier;
                        else if (t is NssKeyword k) v = k.Keyword.ToString();
                        else if (t is NssSeparator s) v = s.Separator.ToString();
                        else v = t.GetType().Name;
                        context += $"\n  [{i}] {t.GetType().Name}: {v}";
                    }

                    throw new CompileError($"Unexpected token at top level: {token.GetType().Name}{tokenValue} at index {_tokenIndex} of {_tokens.Count}. Context:{context}");
                }
            }

            return root;
        }

        private bool IsAtEnd()
        {
            int index = _tokenIndex;
            while (index < _tokens.Count)
            {
                NssTokenBase token = _tokens[index];
                if (token is NssSeparator sep && (sep.Separator == NssSeparators.Space || sep.Separator == NssSeparators.Tab || sep.Separator == NssSeparators.NewLine))
                {
                    index++;
                    continue;
                }
                if (token is NssComment)
                {
                    index++;
                    continue;
                }
                return false;
            }
            return true;
        }

        private void SkipWhitespaceAndComments()
        {
            while (_tokenIndex < _tokens.Count)
            {
                NssTokenBase token = _tokens[_tokenIndex];
                if (token is NssSeparator sep && (sep.Separator == NssSeparators.Space || sep.Separator == NssSeparators.Tab || sep.Separator == NssSeparators.NewLine))
                {
                    _tokenIndex++;
                    continue;
                }
                if (token is NssComment)
                {
                    _tokenIndex++;
                    continue;
                }
                break;
            }
        }

        private NssTokenBase CurrentToken()
        {
            if (_tokenIndex >= _tokens.Count)
            {
                return null;
            }
            return _tokens[_tokenIndex];
        }

        private NssTokenBase Advance()
        {
            if (_tokenIndex >= _tokens.Count)
            {
                return null;
            }
            return _tokens[_tokenIndex++];
        }

        private bool MatchToken<T>() where T : NssTokenBase
        {
            if (CurrentToken() is T)
            {
                Advance();
                return true;
            }
            return false;
        }

        private bool CheckToken<T>() where T : NssTokenBase
        {
            return CurrentToken() is T;
        }

        private T ConsumeToken<T>(string errorMessage) where T : NssTokenBase
        {
            SkipWhitespaceAndComments();
            if (CurrentToken() is T token)
            {
                Advance();
                return token;
            }
            throw new CompileError(errorMessage);
        }

        private TopLevelObject TryParseInclude()
        {
            int savedIndex = _tokenIndex;
            try
            {
                SkipWhitespaceAndComments();
                if (!CheckToken<NssPreprocessor>())
                {
                    _tokenIndex = savedIndex;
                    return null;
                }

                NssPreprocessor preproc = CurrentToken() as NssPreprocessor;
                if (preproc.PreprocessorType != NssPreprocessorType.Include)
                {
                    _tokenIndex = savedIndex;
                    return null;
                }

                Advance(); // consume preprocessor

                // Extract include path from preprocessor data (e.g., "#include \"nwscript\"")
                string includeData = preproc.Data;
                int firstQuote = includeData.IndexOf('"');
                if (firstQuote == -1)
                {
                    _tokenIndex = savedIndex;
                    return null;
                }
                int lastQuote = includeData.LastIndexOf('"');
                if (lastQuote <= firstQuote)
                {
                    _tokenIndex = savedIndex;
                    return null;
                }
                string includePath = includeData.Substring(firstQuote + 1, lastQuote - firstQuote - 1);

                return new IncludeScript(new StringExpression(includePath), _library);
            }
            catch (Exception)
            {
                _tokenIndex = savedIndex;
                // Silently fail for try-parse methods
                return null;
            }
        }

        private TopLevelObject TryParseStructDefinition()
        {
            int savedIndex = _tokenIndex;
            try
            {
                SkipWhitespaceAndComments();
                // Check if current token is 'struct' keyword without advancing
                if (!(CurrentToken() is NssKeyword keyword) || keyword.Keyword != NssKeywords.Struct)
                {
                    _tokenIndex = savedIndex;
                    return null;
                }
                Advance(); // Now consume the struct keyword

                // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/parser.py:162-183
                // Original: struct_definition : STRUCT IDENTIFIER '{' struct_members '}' ';'
                SkipWhitespaceAndComments();
                // Parse struct name
                NssIdentifier structName = ConsumeToken<NssIdentifier>("Expected struct name after 'struct'");

                // Parse opening brace
                ConsumeSeparator(NssSeparators.OpenCurlyBrace, "Expected '{' after struct name");

                var members = new List<StructMember>();

                // Parse members
                while (true)
                {
                    SkipWhitespaceAndComments();
                    if (CheckSeparator(NssSeparators.CloseCurlyBrace))
                    {
                        break;
                    }

                    DynamicDataType memberType = ParseDataType();
                    if (memberType == null)
                    {
                        throw new CompileError("Expected struct member type");
                    }
                    NssIdentifier memberName = ConsumeToken<NssIdentifier>("Expected member name");
                    ConsumeSeparator(NssSeparators.Semicolon, "Expected ';' after struct member");
                    members.Add(new StructMember(memberType, new Identifier(memberName.Identifier)));
                }

                // Parse closing brace
                ConsumeSeparator(NssSeparators.CloseCurlyBrace, "Expected '}' to close struct");
                ConsumeSeparator(NssSeparators.Semicolon, "Expected ';' after struct definition");

                return new StructDefinition(new Identifier(structName.Identifier), members);
            }
            catch (Exception)
            {
                _tokenIndex = savedIndex;
                // Silently fail for try-parse methods
                return null;
            }
        }

        private TopLevelObject TryParseGlobalVariable()
        {
            int savedIndex = _tokenIndex;
            try
            {
                // Check if next token (after whitespace) is void - if so, this can't be a global variable
                // We need to peek ahead past whitespace to check WITHOUT modifying _tokenIndex
                int peekIndex = _tokenIndex;
                while (peekIndex < _tokens.Count)
                {
                    NssTokenBase peekToken = _tokens[peekIndex];
                    if (peekToken is NssSeparator sep && (sep.Separator == NssSeparators.Space || sep.Separator == NssSeparators.Tab || sep.Separator == NssSeparators.NewLine))
                    {
                        peekIndex++;
                        continue;
                    }
                    if (peekToken is NssComment)
                    {
                        peekIndex++;
                        continue;
                    }
                    // Found non-whitespace token
                    if (peekToken is NssKeyword keyword && keyword.Keyword == NssKeywords.Void)
                    {
                        // Next token is void, so this can't be a global variable
                        // Don't modify _tokenIndex, just return null
                        return null;
                    }
                    break;
                }

                // Only skip whitespace if we didn't detect void
                SkipWhitespaceAndComments();

                // Check for 'const' keyword without advancing
                bool isConst = CheckToken<NssKeyword>() && (CurrentToken() as NssKeyword)?.Keyword == NssKeywords.Const;
                if (isConst)
                {
                    Advance(); // Only advance if it's actually const
                    SkipWhitespaceAndComments(); // Skip whitespace after const
                }

                DynamicDataType type = ParseDataType();
                if (type is null)
                {
                    _tokenIndex = savedIndex;
                    return null;
                }

                // Global variables cannot be void type - let function definition handle it
                if (type.Builtin == DataType.Void)
                {
                    _tokenIndex = savedIndex;
                    return null;
                }

                // Check if next token is an identifier before consuming
                // This allows graceful failure for cases like "void main()" where ParseDataType
                // might have parsed an identifier as a struct type
                SkipWhitespaceAndComments();
                if (!CheckToken<NssIdentifier>())
                {
                    _tokenIndex = savedIndex;
                    return null;
                }

                NssIdentifier varName = ConsumeToken<NssIdentifier>("Expected variable name");

                SkipWhitespaceAndComments();

                // Check if it's a function (has parentheses) - let forward declaration handle it
                if (CheckSeparator(NssSeparators.OpenParen))
                {
                    _tokenIndex = savedIndex;
                    return null;
                }

                // Check if it's an initialization or just declaration
                if (MatchOperator(NssOperators.Assignment))
                {
                    // Matching PyKotor parser.py line 201: GlobalVariableInitialization with is_const
                    // It's an initialization
                    Expression initExpr = ParseExpression();
                    ConsumeSeparator(NssSeparators.Semicolon, "Expected ';' after variable initialization");
                    return new GlobalVariableInitialization(new Identifier(varName.Identifier), type, initExpr, isConst);
                }
                else
                {
                    // Matching PyKotor parser.py line 213: GlobalVariableDeclaration with is_const
                    // Just a declaration
                    ConsumeSeparator(NssSeparators.Semicolon, "Expected ';' after variable declaration");
                    return new GlobalVariableDeclaration(new Identifier(varName.Identifier), type, isConst);
                }
            }
            catch (CompileError)
            {
                // Re-throw CompileError so we can see what went wrong
                throw;
            }
            catch (Exception)
            {
                // For other exceptions, restore state and return null
                _tokenIndex = savedIndex;
                return null;
            }
        }

        private TopLevelObject TryParseFunctionDefinition()
        {
            int savedIndex = _tokenIndex;
            try
            {
                SkipWhitespaceAndComments();
                DynamicDataType returnType = ParseDataType();
                if (returnType is null)
                {
                    _tokenIndex = savedIndex;
                    return null;
                }
                SkipWhitespaceAndComments();
                NssIdentifier funcName = ConsumeToken<NssIdentifier>("Expected function name");

                SkipWhitespaceAndComments();
                ConsumeSeparator(NssSeparators.OpenParen, "Expected '(' after function name");

                var parameters = new List<FunctionParameter>();
                SkipWhitespaceAndComments();
                if (!CheckSeparator(NssSeparators.CloseParen))
                {
                    do
                    {
                        SkipWhitespaceAndComments();
                        DynamicDataType paramType = ParseDataType();
                        SkipWhitespaceAndComments();
                        NssIdentifier paramName = ConsumeToken<NssIdentifier>("Expected parameter name");
                        Expression defaultValue = null;

                        SkipWhitespaceAndComments();
                        if (MatchOperator(NssOperators.Assignment))
                        {
                            defaultValue = ParseExpression();
                        }

                        parameters.Add(new FunctionParameter(new Identifier(paramName.Identifier), paramType, defaultValue));

                        SkipWhitespaceAndComments();
                        if (!MatchSeparator(NssSeparators.Comma))
                        {
                            break;
                        }
                    } while (true);
                }

                SkipWhitespaceAndComments();
                ConsumeSeparator(NssSeparators.CloseParen, "Expected ')' after function parameters");

                SkipWhitespaceAndComments();
                // Check if it's a forward declaration (semicolon) or implementation (brace)
                if (MatchSeparator(NssSeparators.Semicolon))
                {
                    // Forward declaration - handled separately
                    _tokenIndex = savedIndex;
                    return null;
                }

                ConsumeSeparator(NssSeparators.OpenCurlyBrace, "Expected '{' for function body");

                CodeBlock body = ParseCodeBlock();
                SkipWhitespaceAndComments();
                ConsumeSeparator(NssSeparators.CloseCurlyBrace, "Expected '}' to close function body");

                return new FunctionDefinition(new Identifier(funcName.Identifier), returnType, parameters, body);
            }
            catch (CompileError)
            {
                // Re-throw CompileError so we can see what went wrong
                throw;
            }
            catch (Exception)
            {
                // For other exceptions, restore state and return null
                _tokenIndex = savedIndex;
                return null;
            }
        }

        private TopLevelObject TryParseFunctionForwardDeclaration()
        {
            int savedIndex = _tokenIndex;
            try
            {
                SkipWhitespaceAndComments();
                DynamicDataType returnType = ParseDataType();
                if (returnType is null)
                {
                    _tokenIndex = savedIndex;
                    return null;
                }
                SkipWhitespaceAndComments();
                NssIdentifier funcName = ConsumeToken<NssIdentifier>("Expected function name");

                SkipWhitespaceAndComments();
                ConsumeSeparator(NssSeparators.OpenParen, "Expected '(' after function name");

                var parameters = new List<FunctionParameter>();
                if (!CheckSeparator(NssSeparators.CloseParen))
                {
                    do
                    {
                        SkipWhitespaceAndComments();
                        DynamicDataType paramType = ParseDataType();
                        SkipWhitespaceAndComments();
                        NssIdentifier paramName = ConsumeToken<NssIdentifier>("Expected parameter name");
                        Expression defaultValue = null;

                        SkipWhitespaceAndComments();
                        if (MatchOperator(NssOperators.Assignment))
                        {
                            SkipWhitespaceAndComments();
                            defaultValue = ParseExpression();
                        }

                        parameters.Add(new FunctionParameter(new Identifier(paramName.Identifier), paramType, defaultValue));

                        SkipWhitespaceAndComments();
                        if (!MatchSeparator(NssSeparators.Comma))
                        {
                            break;
                        }
                    } while (true);
                }

                SkipWhitespaceAndComments();
                ConsumeSeparator(NssSeparators.CloseParen, "Expected ')' after function parameters");
                SkipWhitespaceAndComments();
                ConsumeSeparator(NssSeparators.Semicolon, "Expected ';' after function forward declaration");

                return new FunctionForwardDeclaration(returnType, new Identifier(funcName.Identifier), parameters);
            }
            catch (Exception)
            {
                _tokenIndex = savedIndex;
                // Silently fail for try-parse methods
                return null;
            }
        }

        private DynamicDataType ParseDataType()
        {
            // Don't skip whitespace here - callers should have already skipped it
            // This matches PyKotor's grammar parser behavior where whitespace is handled by the grammar rules
            NssTokenBase token = CurrentToken();
            if (token is null)
            {
                return null;
            }

            // Check if token is a keyword
            NssKeyword keyword = token as NssKeyword;
            if (keyword == null)
            {
                // Not a keyword - check if it's an identifier (struct type)
                // In PyKotor's grammar parser, data_type : IDENTIFIER accepts any identifier as a struct type
                // The grammar parser will backtrack if the declaration fails
                // We need to match this behavior: accept identifier as struct type, let caller handle failure
                NssIdentifier identifier = token as NssIdentifier;
                if (identifier != null)
                {
                    // Match PyKotor: accept identifier as struct type (grammar will backtrack if wrong)
                    Advance(); // consume identifier
                    return new DynamicDataType(DataType.Struct, identifier.Identifier);
                }
                return null;
            }

            // Save the keyword before we advance
            NssKeywords keywordType = keyword.Keyword;

            DynamicDataType type = null;
            switch (keywordType)
            {
                case NssKeywords.Void:
                    Advance(); // consume keyword
                    type = DynamicDataType.VOID;
                    break;
                case NssKeywords.Int:
                    Advance(); // consume keyword
                    type = DynamicDataType.INT;
                    break;
                case NssKeywords.Float:
                    Advance(); // consume keyword
                    type = DynamicDataType.FLOAT;
                    break;
                case NssKeywords.String:
                    Advance(); // consume keyword
                    type = DynamicDataType.STRING;
                    break;
                case NssKeywords.Object:
                    Advance(); // consume keyword
                    type = DynamicDataType.OBJECT;
                    break;
                case NssKeywords.Vector:
                    Advance(); // consume keyword
                    type = DynamicDataType.VECTOR;
                    break;
                case NssKeywords.Struct:
                    Advance(); // consume 'struct' keyword
                    SkipWhitespaceAndComments();
                    NssIdentifier structName = ConsumeToken<NssIdentifier>("Expected struct type name");
                    type = new DynamicDataType(DataType.Struct, structName.Identifier);
                    break;
                // Matching PyKotor parser.py line 648: LOCATION_TYPE
                case NssKeywords.Location:
                    Advance(); // consume keyword
                    type = DynamicDataType.LOCATION;
                    break;
                case NssKeywords.Effect:
                    Advance(); // consume keyword
                    type = DynamicDataType.EFFECT;
                    break;
                case NssKeywords.Talent:
                    Advance(); // consume keyword
                    type = DynamicDataType.TALENT;
                    break;
                case NssKeywords.Event:
                    Advance(); // consume keyword
                    type = DynamicDataType.EVENT;
                    break;
                case NssKeywords.Action:
                    Advance(); // consume keyword
                    type = new DynamicDataType(DataType.Action);
                    break;
                case NssKeywords.ItemProperty:
                    Advance(); // consume keyword
                    type = new DynamicDataType(DataType.ItemProperty);
                    break;
                default:
                    return null;
            }

            return type;
        }

        private CodeBlock ParseCodeBlock()
        {
            var block = new CodeBlock();

            while (true)
            {
                Statement stmt = ParseStatement();
                if (stmt != null)
                {
                    block.Statements.Add(stmt);
                }
                else
                {
                    SkipWhitespaceAndComments();
                    if (CheckSeparator(NssSeparators.CloseCurlyBrace))
                    {
                        break;
                    }
                    NssTokenBase current = CurrentToken();
                    string tokenInfo = current != null ? $"token type: {current.GetType().Name}, value: {current}" : "end of input";
                    throw new CompileError($"Unexpected token while parsing code block: {tokenInfo}");
                }
            }

            return block;
        }

        private Statement ParseStatement()
        {
            int savedIndex = _tokenIndex;
            SkipWhitespaceAndComments();

            // Matching PyKotor parser.py line 308-321: statement grammar rule order
            // Empty statement
            if (MatchSeparator(NssSeparators.Semicolon))
            {
                return new EmptyStatement();
            }

            // Declaration statement (second in PyKotor grammar)
            Statement declStmt = TryParseDeclarationStatement();
            if (declStmt != null)
            {
                return declStmt;
            }

            // Condition statement (if) - third in PyKotor grammar
            Statement ifStmt = TryParseIfStatement();
            if (ifStmt != null)
            {
                return ifStmt;
            }

            // Return statement - fourth in PyKotor grammar
            Statement returnStmt = TryParseReturnStatement();
            if (returnStmt != null)
            {
                return returnStmt;
            }

            // While loop - fifth in PyKotor grammar
            Statement whileStmt = TryParseWhileStatement();
            if (whileStmt != null)
            {
                return whileStmt;
            }

            // Do-while loop - sixth in PyKotor grammar
            Statement doWhileStmt = TryParseDoWhileStatement();
            if (doWhileStmt != null)
            {
                return doWhileStmt;
            }

            // For loop - seventh in PyKotor grammar
            Statement forStmt = TryParseForStatement();
            if (forStmt != null)
            {
                return forStmt;
            }

            // Switch statement - eighth in PyKotor grammar
            Statement switchStmt = TryParseSwitchStatement();
            if (switchStmt != null)
            {
                return switchStmt;
            }

            // Break statement - ninth in PyKotor grammar
            Statement breakStmt = TryParseBreakStatement();
            if (breakStmt != null)
            {
                return breakStmt;
            }

            // Continue statement - tenth in PyKotor grammar
            Statement continueStmt = TryParseContinueStatement();
            if (continueStmt != null)
            {
                return continueStmt;
            }

            // NOP statement - matching PyKotor parser.py line 327
            Statement nopStmt = TryParseNopStatement();
            if (nopStmt != null)
            {
                return nopStmt;
            }

            // Scoped block - eleventh in PyKotor grammar
            Statement scopedBlock = TryParseScopedBlock();
            if (scopedBlock != null)
            {
                return scopedBlock;
            }

            // Expression statement (function call, assignment, etc.)
            Statement exprStmt = TryParseExpressionStatement();
            if (exprStmt != null)
            {
                return exprStmt;
            }

            // All parsers failed - return null without restoring
            // ParseCodeBlock() will handle checking for closing brace
            return null;
        }

        private Statement TryParseDeclarationStatement()
        {
            // Don't skip whitespace here - ParseStatement() already did it
            // Save index before trying to parse - this is where we'll restore to if parsing fails
            // This matches PyKotor's grammar parser behavior where backtracking restores to before the rule was tried
            int savedIndex = _tokenIndex;
            try
            {
                // Check for 'const' keyword without advancing
                bool isConst = CheckToken<NssKeyword>() && (CurrentToken() as NssKeyword)?.Keyword == NssKeywords.Const;
                if (isConst)
                {
                    Advance(); // Only advance if it's actually const
                    savedIndex = _tokenIndex; // Update saved index after consuming const
                }

                // Skip whitespace before calling ParseDataType() (all callers must do this)
                // But ParseStatement() already skipped whitespace, so this won't advance
                SkipWhitespaceAndComments();
                // ParseDataType() will consume the type (identifier or keyword)
                // If it's an identifier and we can't parse a variable name, we'll restore to savedIndex
                DynamicDataType type = ParseDataType();
                if (type is null)
                {
                    _tokenIndex = savedIndex;
                    return null;
                }

                var declarators = new List<VariableDeclarator>();

                while (true)
                {
                    SkipWhitespaceAndComments();
                    // Check if next token is an identifier before consuming
                    // This allows graceful failure without throwing an exception
                    if (!CheckToken<NssIdentifier>())
                    {
                        _tokenIndex = savedIndex;
                        return null;
                    }
                    NssIdentifier varName = ConsumeToken<NssIdentifier>("Expected variable name");

                    SkipWhitespaceAndComments(); // Skip whitespace before checking for '='

                    Expression initializer = null;
                    if (MatchOperator(NssOperators.Assignment))
                    {
                        SkipWhitespaceAndComments();
                        initializer = ParseExpression();
                    }

                    declarators.Add(new VariableDeclarator(new Identifier(varName.Identifier), initializer));

                    SkipWhitespaceAndComments();
                    if (!MatchSeparator(NssSeparators.Comma))
                    {
                        break;
                    }
                }

                SkipWhitespaceAndComments();
                ConsumeSeparator(NssSeparators.Semicolon, "Expected ';' after declaration");

                return new DeclarationStatement(type, declarators);
            }
            catch (Exception)
            {
                _tokenIndex = savedIndex;
                // Silently fail for try-parse methods
                return null;
            }
        }

        private Statement TryParseReturnStatement()
        {
            int savedIndex = _tokenIndex;
            try
            {
                SkipWhitespaceAndComments();
                // Check for 'return' keyword without advancing
                if (!CheckToken<NssKeyword>() || (CurrentToken() as NssKeyword)?.Keyword != NssKeywords.Return)
                {
                    _tokenIndex = savedIndex;
                    return null;
                }

                Advance(); // consume 'return' keyword

                Expression expr = ParseExpression();
                ConsumeSeparator(NssSeparators.Semicolon, "Expected ';' after return statement");

                return new ReturnStatement(expr);
            }
            catch (Exception)
            {
                _tokenIndex = savedIndex;
                // Silently fail for try-parse methods
                return null;
            }
        }

        private Statement TryParseExpressionStatement()
        {
            // Don't skip whitespace here - ParseStatement() already did it
            int savedIndex = _tokenIndex;
            try
            {
                // ParseExpression() will skip whitespace internally via ParsePrimaryExpression()
                // But since ParseStatement() already skipped whitespace, we're already positioned correctly
                Expression expr = ParseExpression();
                if (expr is null)
                {
                    _tokenIndex = savedIndex;
                    return null;
                }

                ConsumeSeparator(NssSeparators.Semicolon, "Expected ';' after expression");

                return new ExpressionStatement(expr);
            }
            catch (Exception)
            {
                _tokenIndex = savedIndex;
                // Silently fail for try-parse methods
                return null;
            }
        }

        private Statement TryParseBreakStatement()
        {
            int savedIndex = _tokenIndex;
            try
            {
                SkipWhitespaceAndComments();
                if (!CheckToken<NssKeyword>() || (CurrentToken() as NssKeyword)?.Keyword != NssKeywords.Break)
                {
                    _tokenIndex = savedIndex;
                    return null;
                }

                Advance();
                ConsumeSeparator(NssSeparators.Semicolon, "Expected ';' after break");
                return new BreakStatement();
            }
            catch (CompileError)
            {
                // Re-throw CompileError so we can see what went wrong
                throw;
            }
            catch (Exception)
            {
                // For other exceptions, restore state and return null
                _tokenIndex = savedIndex;
                return null;
            }
        }

        private Statement TryParseContinueStatement()
        {
            int savedIndex = _tokenIndex;
            try
            {
                SkipWhitespaceAndComments();
                if (!CheckToken<NssKeyword>() || (CurrentToken() as NssKeyword)?.Keyword != NssKeywords.Continue)
                {
                    _tokenIndex = savedIndex;
                    return null;
                }

                Advance();
                ConsumeSeparator(NssSeparators.Semicolon, "Expected ';' after continue");
                return new ContinueStatement();
            }
            catch (CompileError)
            {
                // Re-throw CompileError so we can see what went wrong
                throw;
            }
            catch (Exception)
            {
                // For other exceptions, restore state and return null
                _tokenIndex = savedIndex;
                return null;
            }
        }

        // Matching PyKotor parser.py line 327: p_nop_statement
        // Original: statement : NOP STRING_VALUE ';'
        private Statement TryParseNopStatement()
        {
            int savedIndex = _tokenIndex;
            try
            {
                SkipWhitespaceAndComments();
                // Check for NOP keyword
                if (!CheckToken<NssIdentifier>() || (CurrentToken() as NssIdentifier)?.Identifier != "NOP")
                {
                    _tokenIndex = savedIndex;
                    return null;
                }

                Advance(); // Consume NOP
                SkipWhitespaceAndComments();

                // Consume string value - matching PyKotor parser.py line 332: string_expr = p[2]
                NssLiteral stringLiteral = ConsumeToken<NssLiteral>("Expected string value after NOP");
                if (stringLiteral.LiteralType != NssLiteralType.String)
                {
                    throw new CompileError("Expected string literal after NOP");
                }
                // Remove quotes - matching ParsePrimaryExpression string handling
                string strVal = stringLiteral.Literal;
                if (strVal.Length >= 2 && strVal[0] == '"' && strVal[strVal.Length - 1] == '"')
                {
                    strVal = strVal.Substring(1, strVal.Length - 2);
                }
                SkipWhitespaceAndComments();

                // Consume semicolon
                ConsumeSeparator(NssSeparators.Semicolon, "Expected ';' after NOP statement");
                return new NopStatement(strVal);
            }
            catch (CompileError)
            {
                // Re-throw CompileError so we can see what went wrong
                throw;
            }
            catch (Exception)
            {
                // For other exceptions, restore state and return null
                _tokenIndex = savedIndex;
                return null;
            }
        }

        // Matching PyKotor parser.py lines 461-522: condition_statement structure
        private Statement TryParseIfStatement()
        {
            int savedIndex = _tokenIndex;
            try
            {
                SkipWhitespaceAndComments();
                if (!CheckToken<NssKeyword>() || (CurrentToken() as NssKeyword)?.Keyword != NssKeywords.If)
                {
                    _tokenIndex = savedIndex;
                    return null;
                }

                // Parse if_statement: IF_CONTROL '(' expression ')' '{' code_block '}' or IF_CONTROL '(' expression ')' statement
                // Matching PyKotor parser.py lines 468-480
                Advance();
                ConsumeSeparator(NssSeparators.OpenParen, "Expected '(' after if");
                Expression condition = ParseExpression();
                if (condition == null)
                {
                    throw new CompileError("Expected expression after '(' in if statement");
                }
                // Matching PyKotor parser.py line 470: expression is p[3], closing paren is consumed by grammar
                // In recursive descent, we need to consume it explicitly after parsing the expression
                SkipWhitespaceAndComments();
                if (!CheckSeparator(NssSeparators.CloseParen))
                {
                    NssTokenBase current = CurrentToken();
                    string tokenInfo = current != null ? $"token type: {current.GetType().Name}, value: {current}" : "end of input";
                    throw new CompileError($"Expected ')' after if condition, but found: {tokenInfo}");
                }
                ConsumeSeparator(NssSeparators.CloseParen, "Expected ')' after if condition");

                CodeBlock thenBlock = null;
                if (MatchSeparator(NssSeparators.OpenCurlyBrace))
                {
                    thenBlock = ParseCodeBlock();
                    SkipWhitespaceAndComments();
                    ConsumeSeparator(NssSeparators.CloseCurlyBrace, "Expected '}' after if block");
                }
                else
                {
                    thenBlock = new CodeBlock();
                    Statement singleStmt = ParseStatement();
                    if (singleStmt != null)
                    {
                        thenBlock.Statements.Add(singleStmt);
                    }
                }

                ConditionAndBlock ifBlock = new ConditionAndBlock(condition, thenBlock);

                // Parse else_if_statements: zero or more else-if statements
                // Matching PyKotor parser.py lines 511-520
                List<ConditionAndBlock> elseIfBlocks = new List<ConditionAndBlock>();
                while (true)
                {
                    SkipWhitespaceAndComments();
                    if (!CheckToken<NssKeyword>())
                    {
                        break;
                    }
                    var kw = CurrentToken() as NssKeyword;
                    if (kw.Keyword != NssKeywords.Else)
                    {
                        break;
                    }
                    int elseTokenIndex = _tokenIndex;
                    Advance();
                    SkipWhitespaceAndComments();
                    if (!CheckToken<NssKeyword>() || (CurrentToken() as NssKeyword)?.Keyword != NssKeywords.If)
                    {
                        // Not an else-if, restore token position so else parsing can handle it
                        _tokenIndex = elseTokenIndex;
                        break;
                    }

                    // Parse else_if_statement: ELSE_CONTROL IF_CONTROL '(' expression ')' '{' code_block '}' or ELSE_CONTROL IF_CONTROL '(' expression ')' statement
                    // Matching PyKotor parser.py lines 497-509
                    Advance();
                    ConsumeSeparator(NssSeparators.OpenParen, "Expected '(' after else if");
                    Expression elseIfCondition = ParseExpression();
                    ConsumeSeparator(NssSeparators.CloseParen, "Expected ')' after else if condition");

                    CodeBlock elseIfBlock = null;
                    if (MatchSeparator(NssSeparators.OpenCurlyBrace))
                    {
                        elseIfBlock = ParseCodeBlock();
                        SkipWhitespaceAndComments();
                        ConsumeSeparator(NssSeparators.CloseCurlyBrace, "Expected '}' after else if block");
                    }
                    else
                    {
                        elseIfBlock = new CodeBlock();
                        Statement singleStmt = ParseStatement();
                        if (singleStmt != null)
                        {
                            elseIfBlock.Statements.Add(singleStmt);
                        }
                    }

                    elseIfBlocks.Add(new ConditionAndBlock(elseIfCondition, elseIfBlock));
                }

                // Parse else_statement: ELSE_CONTROL '{' code_block '}' or ELSE_CONTROL statement or empty
                // Matching PyKotor parser.py lines 482-495
                CodeBlock elseBlock = null;
                SkipWhitespaceAndComments();
                if (CheckToken<NssKeyword>())
                {
                    var kw = CurrentToken() as NssKeyword;
                    if (kw.Keyword == NssKeywords.Else)
                    {
                        Advance();
                        SkipWhitespaceAndComments();
                        if (MatchSeparator(NssSeparators.OpenCurlyBrace))
                        {
                            elseBlock = ParseCodeBlock();
                            SkipWhitespaceAndComments();
                            ConsumeSeparator(NssSeparators.CloseCurlyBrace, "Expected '}' after else block");
                        }
                        else
                        {
                            elseBlock = new CodeBlock();
                            Statement singleStmt = ParseStatement();
                            if (singleStmt != null)
                            {
                                elseBlock.Statements.Add(singleStmt);
                            }
                        }
                    }
                }

                // Create ConditionalBlock matching PyKotor parser.py line 465
                List<ConditionAndBlock> allIfBlocks = new List<ConditionAndBlock> { ifBlock };
                allIfBlocks.AddRange(elseIfBlocks);
                return new ConditionalBlock(allIfBlocks, elseBlock);
            }
            catch (CompileError)
            {
                // Re-throw CompileError so we can see what went wrong
                throw;
            }
            catch (Exception)
            {
                // For other exceptions, restore state and return null
                _tokenIndex = savedIndex;
                return null;
            }
        }

        private Statement TryParseWhileStatement()
        {
            int savedIndex = _tokenIndex;
            try
            {
                SkipWhitespaceAndComments();
                if (!CheckToken<NssKeyword>() || (CurrentToken() as NssKeyword)?.Keyword != NssKeywords.While)
                {
                    _tokenIndex = savedIndex;
                    return null;
                }

                Advance();
                ConsumeSeparator(NssSeparators.OpenParen, "Expected '(' after while");
                Expression condition = ParseExpression();
                if (condition == null)
                {
                    throw new CompileError("Expected expression in while condition");
                }
                ConsumeSeparator(NssSeparators.CloseParen, "Expected ')' after while condition");
                ConsumeSeparator(NssSeparators.OpenCurlyBrace, "Expected '{' after while condition");
                CodeBlock body = ParseCodeBlock();
                SkipWhitespaceAndComments();
                ConsumeSeparator(NssSeparators.CloseCurlyBrace, "Expected '}' after while body");

                return new WhileStatement(condition, body);
            }
            catch (CompileError)
            {
                // Re-throw CompileError so we can see what went wrong
                throw;
            }
            catch (Exception)
            {
                // For other exceptions, restore state and return null
                _tokenIndex = savedIndex;
                return null;
            }
        }

        private Statement TryParseDoWhileStatement()
        {
            int savedIndex = _tokenIndex;
            try
            {
                SkipWhitespaceAndComments();
                if (!CheckToken<NssKeyword>() || (CurrentToken() as NssKeyword)?.Keyword != NssKeywords.Do)
                {
                    _tokenIndex = savedIndex;
                    return null;
                }

                Advance();
                ConsumeSeparator(NssSeparators.OpenCurlyBrace, "Expected '{' after do");
                CodeBlock body = ParseCodeBlock();
                ConsumeSeparator(NssSeparators.CloseCurlyBrace, "Expected '}' after do body");
                SkipWhitespaceAndComments();

                if (!CheckToken<NssKeyword>() || (CurrentToken() as NssKeyword)?.Keyword != NssKeywords.While)
                {
                    _tokenIndex = savedIndex;
                    return null;
                }

                Advance();
                ConsumeSeparator(NssSeparators.OpenParen, "Expected '(' after while");
                Expression condition = ParseExpression();
                ConsumeSeparator(NssSeparators.CloseParen, "Expected ')' after while condition");
                ConsumeSeparator(NssSeparators.Semicolon, "Expected ';' after do-while");

                return new DoWhileStatement(condition, body);
            }
            catch (CompileError)
            {
                // Re-throw CompileError so we can see what went wrong
                throw;
            }
            catch (Exception)
            {
                // For other exceptions, restore state and return null
                _tokenIndex = savedIndex;
                return null;
            }
        }

        private Statement TryParseForStatement()
        {
            int savedIndex = _tokenIndex;
            try
            {
                SkipWhitespaceAndComments();
                if (!CheckToken<NssKeyword>() || (CurrentToken() as NssKeyword)?.Keyword != NssKeywords.For)
                {
                    _tokenIndex = savedIndex;
                    return null;
                }

                Advance();
                ConsumeSeparator(NssSeparators.OpenParen, "Expected '(' after for");

                Expression initExpr = null;
                Statement initStmt = null;
                SkipWhitespaceAndComments();

                // Check if init is empty (semicolon immediately)
                if (!CheckSeparator(NssSeparators.Semicolon))
                {
                    Statement declStmt = TryParseDeclarationStatement();
                    if (declStmt != null)
                    {
                        initStmt = declStmt;
                        // Declaration statement already consumed its semicolon
                    }
                    else
                    {
                        // Parse an expression for the init (required if not empty)
                        initExpr = ParseExpression();
                        if (initExpr == null)
                        {
                            throw new CompileError("Expected expression or declaration statement in for loop initialization");
                        }
                        SkipWhitespaceAndComments();
                        ConsumeSeparator(NssSeparators.Semicolon, "Expected ';' after for init");
                    }
                }

                Expression condition = new IntExpression(1);
                SkipWhitespaceAndComments();
                if (!CheckSeparator(NssSeparators.Semicolon))
                {
                    Expression condExpr = ParseExpression();
                    if (condExpr != null)
                    {
                        condition = condExpr;
                    }
                }
                SkipWhitespaceAndComments();
                ConsumeSeparator(NssSeparators.Semicolon, "Expected ';' after for condition");

                Expression increment = new IntExpression(0);
                SkipWhitespaceAndComments();
                if (!CheckSeparator(NssSeparators.CloseParen))
                {
                    Expression incrExpr = ParseExpression();
                    if (incrExpr != null)
                    {
                        increment = incrExpr;
                    }
                }
                SkipWhitespaceAndComments();
                ConsumeSeparator(NssSeparators.CloseParen, "Expected ')' after for");

                ConsumeSeparator(NssSeparators.OpenCurlyBrace, "Expected '{' after for");
                CodeBlock body = ParseCodeBlock();
                SkipWhitespaceAndComments();
                ConsumeSeparator(NssSeparators.CloseCurlyBrace, "Expected '}' after for body");

                if (initStmt != null)
                {
                    return new ForStatement(initStmt, condition, increment, body);
                }
                else
                {
                    if (initExpr == null)
                    {
                        initExpr = new IntExpression(0);
                    }
                    return new ForStatement(initExpr, condition, increment, body);
                }
            }
            catch (CompileError)
            {
                // Re-throw CompileError so we can see what went wrong
                throw;
            }
            catch (Exception)
            {
                // For other exceptions, restore state and return null
                _tokenIndex = savedIndex;
                return null;
            }
        }

        private Statement TryParseSwitchStatement()
        {
            int savedIndex = _tokenIndex;
            try
            {
                SkipWhitespaceAndComments();
                if (!CheckToken<NssKeyword>() || (CurrentToken() as NssKeyword)?.Keyword != NssKeywords.Switch)
                {
                    _tokenIndex = savedIndex;
                    return null;
                }

                Advance();
                ConsumeSeparator(NssSeparators.OpenParen, "Expected '(' after switch");
                Expression expr = ParseExpression();
                ConsumeSeparator(NssSeparators.CloseParen, "Expected ')' after switch expression");
                ConsumeSeparator(NssSeparators.OpenCurlyBrace, "Expected '{' after switch");

                var blocks = new List<SwitchBlock>();
                while (true)
                {
                    SkipWhitespaceAndComments();
                    if (CheckSeparator(NssSeparators.CloseCurlyBrace))
                    {
                        break;
                    }

                    var labels = new List<SwitchLabel>();
                    while (true)
                    {
                        SkipWhitespaceAndComments();
                        if (CheckToken<NssKeyword>())
                        {
                            var kw = CurrentToken() as NssKeyword;
                            if (kw.Keyword == NssKeywords.Case)
                            {
                                Advance();
                                Expression caseExpr = ParseExpression();
                                if (!MatchOperator(NssOperators.TernaryColon))
                                {
                                    throw new CompileError("Expected ':' after case");
                                }
                                labels.Add(new CaseSwitchLabel(caseExpr));
                                continue;
                            }
                            else if (kw.Keyword == NssKeywords.Default)
                            {
                                Advance();
                                if (!MatchOperator(NssOperators.TernaryColon))
                                {
                                    throw new CompileError("Expected ':' after default");
                                }
                                labels.Add(new DefaultSwitchLabel());
                                break;
                            }
                        }
                        break;
                    }

                    if (labels.Count == 0)
                    {
                        break;
                    }

                    var statements = new List<Statement>();
                    while (true)
                    {
                        SkipWhitespaceAndComments();
                        if (CheckSeparator(NssSeparators.CloseCurlyBrace) ||
                            (CheckToken<NssKeyword>() &&
                             ((CurrentToken() as NssKeyword)?.Keyword == NssKeywords.Case ||
                              (CurrentToken() as NssKeyword)?.Keyword == NssKeywords.Default)))
                        {
                            break;
                        }

                        Statement stmt = ParseStatement();
                        if (stmt != null)
                        {
                            statements.Add(stmt);
                        }
                        else
                        {
                            break;
                        }
                    }

                    blocks.Add(new SwitchBlock(labels, statements));
                }

                ConsumeSeparator(NssSeparators.CloseCurlyBrace, "Expected '}' after switch");

                return new SwitchStatement(expr, blocks);
            }
            catch (CompileError)
            {
                // Re-throw CompileError so we can see what went wrong
                throw;
            }
            catch (Exception)
            {
                // For other exceptions, restore state and return null
                _tokenIndex = savedIndex;
                return null;
            }
        }

        private Statement TryParseScopedBlock()
        {
            int savedIndex = _tokenIndex;
            try
            {
                SkipWhitespaceAndComments();
                if (!MatchSeparator(NssSeparators.OpenCurlyBrace))
                {
                    _tokenIndex = savedIndex;
                    return null;
                }

                CodeBlock block = ParseCodeBlock();
                SkipWhitespaceAndComments();
                ConsumeSeparator(NssSeparators.CloseCurlyBrace, "Expected '}' to close scoped block");

                return new ScopedBlockStatement(block);
            }
            catch (CompileError)
            {
                // Re-throw CompileError so we can see what went wrong
                throw;
            }
            catch (Exception)
            {
                // For other exceptions, restore state and return null
                _tokenIndex = savedIndex;
                return null;
            }
        }

        private Expression ParseExpression()
        {
            return ParseAssignmentExpression();
        }

        private Expression ParseAssignmentExpression()
        {
            Expression left = ParseTernaryExpression();
            if (left == null) return null;

            SkipWhitespaceAndComments();
            int savedIndex = _tokenIndex;

            // Compound assignments
            if (MatchOperator(NssOperators.ModuloAssignment) || MatchCompoundOperator(NssOperators.Modulo, NssOperators.Assignment))
            {
                Expression right = ParseTernaryExpression();
                FieldAccess fa = ConvertToFieldAccess(left);
                return new ModuloAssignmentExpression(fa, right);
            }
            if (MatchOperator(NssOperators.BitwiseAndAssignment) || MatchCompoundOperator(NssOperators.And, NssOperators.Assignment))
            {
                Expression right = ParseTernaryExpression();
                FieldAccess fa = ConvertToFieldAccess(left);
                return new BitwiseAndAssignmentExpression(fa, right);
            }
            if (MatchOperator(NssOperators.BitwiseOrAssignment) || MatchCompoundOperator(NssOperators.Or, NssOperators.Assignment))
            {
                Expression right = ParseTernaryExpression();
                FieldAccess fa = ConvertToFieldAccess(left);
                return new BitwiseOrAssignmentExpression(fa, right);
            }
            if (MatchOperator(NssOperators.BitwiseXorAssignment) || MatchCompoundOperator(NssOperators.Xor, NssOperators.Assignment))
            {
                Expression right = ParseTernaryExpression();
                FieldAccess fa = ConvertToFieldAccess(left);
                return new BitwiseXorAssignmentExpression(fa, right);
            }
            if (MatchOperator(NssOperators.BitwiseLeftAssignment) || MatchCompoundOperator(NssOperators.LessThan, NssOperators.LessThan, NssOperators.Assignment))
            {
                Expression right = ParseTernaryExpression();
                FieldAccess fa = ConvertToFieldAccess(left);
                return new BitwiseLeftAssignmentExpression(fa, right);
            }
            if (MatchOperator(NssOperators.BitwiseRightAssignment) || MatchCompoundOperator(NssOperators.GreaterThan, NssOperators.GreaterThan, NssOperators.Assignment))
            {
                Expression right = ParseTernaryExpression();
                FieldAccess fa = ConvertToFieldAccess(left);
                return new BitwiseRightAssignmentExpression(fa, right);
            }
            if (MatchOperator(NssOperators.BitwiseUnsignedRightAssignment) || MatchCompoundOperator(NssOperators.GreaterThan, NssOperators.GreaterThan, NssOperators.GreaterThan, NssOperators.Assignment))
            {
                Expression right = ParseTernaryExpression();
                FieldAccess fa = ConvertToFieldAccess(left);
                return new BitwiseUnsignedRightAssignmentExpression(fa, right);
            }
            if (MatchOperator(NssOperators.AdditionAssignment) || MatchCompoundOperator(NssOperators.Addition, NssOperators.Assignment))
            {
                Expression right = ParseTernaryExpression();
                FieldAccess fa = ConvertToFieldAccess(left);
                return new AdditionAssignmentExpression(fa, right);
            }
            if (MatchOperator(NssOperators.SubtractionAssignment) || MatchCompoundOperator(NssOperators.Subtraction, NssOperators.Assignment))
            {
                Expression right = ParseTernaryExpression();
                FieldAccess fa = ConvertToFieldAccess(left);
                return new SubtractionAssignmentExpression(fa, right);
            }
            if (MatchOperator(NssOperators.MultiplicationAssignment) || MatchCompoundOperator(NssOperators.Multiplication, NssOperators.Assignment))
            {
                Expression right = ParseTernaryExpression();
                FieldAccess fa = ConvertToFieldAccess(left);
                return new MultiplicationAssignmentExpression(fa, right);
            }
            if (MatchOperator(NssOperators.DivisionAssignment) || MatchCompoundOperator(NssOperators.Division, NssOperators.Assignment))
            {
                Expression right = ParseTernaryExpression();
                FieldAccess fa = ConvertToFieldAccess(left);
                return new DivisionAssignmentExpression(fa, right);
            }

            // Simple assignment = (right-associative)
            if (MatchOperator(NssOperators.Assignment))
            {
                Expression right = ParseAssignmentExpression();
                FieldAccess fa = ConvertToFieldAccess(left);
                return new AssignmentExpression(fa, right);
            }

            return left;
        }

        private Expression ParseTernaryExpression()
        {
            Expression condition = ParseLogicalOrExpression();
            if (condition == null) return null;

            SkipWhitespaceAndComments();
            if (MatchOperator(NssOperators.TernaryQuestionMark))
            {
                Expression trueExpr = ParseExpression();
                SkipWhitespaceAndComments();
                if (!MatchOperator(NssOperators.TernaryColon))
                {
                    throw new CompileError("Expected ':' in ternary expression");
                }
                Expression falseExpr = ParseExpression();
                return new TernaryConditionalExpression(condition, trueExpr, falseExpr);
            }

            return condition;
        }

        private Expression ParseLogicalOrExpression()
        {
            Expression left = ParseLogicalAndExpression();
            if (left == null) return null;

            while (true)
            {
                SkipWhitespaceAndComments();
                if (MatchOperator(NssOperators.Or))
                {
                    SkipWhitespaceAndComments();
                    if (MatchOperator(NssOperators.Or))
                    {
                        // || operator
                        Expression right = ParseLogicalAndExpression();
                        left = new BinaryOperatorExpression(left, right, Operator.OR, OperatorMappings.LogicalOr);
                        continue;
                    }
                    else
                    {
                        // Just a single |, backtrack
                        _tokenIndex--;
                        break;
                    }
                }
                break;
            }

            return left;
        }

        private Expression ParseLogicalAndExpression()
        {
            Expression left = ParseBitwiseOrExpression();
            if (left == null) return null;

            while (true)
            {
                SkipWhitespaceAndComments();
                if (MatchOperator(NssOperators.And))
                {
                    SkipWhitespaceAndComments();
                    if (MatchOperator(NssOperators.And))
                    {
                        // && operator
                        Expression right = ParseBitwiseOrExpression();
                        left = new BinaryOperatorExpression(left, right, Operator.AND, OperatorMappings.LogicalAnd);
                        continue;
                    }
                    else
                    {
                        // Just a single &, backtrack
                        _tokenIndex--;
                        break;
                    }
                }
                break;
            }

            return left;
        }

        private Expression ParseBitwiseOrExpression()
        {
            Expression left = ParseBitwiseXorExpression();
            if (left == null) return null;

            while (true)
            {
                SkipWhitespaceAndComments();
                // Check for single | (not ||)
                if (CheckOperator(NssOperators.Or))
                {
                    int savedIdx = _tokenIndex;
                    Advance();
                    SkipWhitespaceAndComments();
                    if (!CheckOperator(NssOperators.Or))
                    {
                        // Single | - bitwise or
                        Expression right = ParseBitwiseXorExpression();
                        left = new BinaryOperatorExpression(left, right, Operator.BITWISE_OR, OperatorMappings.BitwiseOr);
                        continue;
                    }
                    else
                    {
                        // Double || - backtrack, let logical or handle it
                        _tokenIndex = savedIdx;
                        break;
                    }
                }
                break;
            }

            return left;
        }

        private Expression ParseBitwiseXorExpression()
        {
            Expression left = ParseBitwiseAndExpression();
            if (left == null) return null;

            while (true)
            {
                SkipWhitespaceAndComments();
                if (MatchOperator(NssOperators.Xor))
                {
                    Expression right = ParseBitwiseAndExpression();
                    left = new BinaryOperatorExpression(left, right, Operator.BITWISE_XOR, OperatorMappings.BitwiseXor);
                    continue;
                }
                break;
            }

            return left;
        }

        private Expression ParseBitwiseAndExpression()
        {
            Expression left = ParseEqualityExpression();
            if (left == null) return null;

            while (true)
            {
                SkipWhitespaceAndComments();
                // Check for single & (not &&)
                if (CheckOperator(NssOperators.And))
                {
                    int savedIdx = _tokenIndex;
                    Advance();
                    SkipWhitespaceAndComments();
                    if (!CheckOperator(NssOperators.And))
                    {
                        // Single & - bitwise and
                        Expression right = ParseEqualityExpression();
                        left = new BinaryOperatorExpression(left, right, Operator.BITWISE_AND, OperatorMappings.BitwiseAnd);
                        continue;
                    }
                    else
                    {
                        // Double && - backtrack, let logical and handle it
                        _tokenIndex = savedIdx;
                        break;
                    }
                }
                break;
            }

            return left;
        }

        private Expression ParseEqualityExpression()
        {
            Expression left = ParseRelationalExpression();
            if (left == null) return null;

            while (true)
            {
                SkipWhitespaceAndComments();
                // Matching PyKotor parser.py line 538: expression EQUALS expression
                // In PyKotor, == is tokenized as a single EQUALS token (lexer.py line 506-507)
                // In our lexer, == is also tokenized as NssOperators.Equals (NssLexer.cs line 465-472)
                if (MatchOperator(NssOperators.Equals))
                {
                    // == operator (single token, not two = tokens)
                    SkipWhitespaceAndComments();
                    Expression right = ParseRelationalExpression();
                    if (right == null)
                    {
                        NssTokenBase current = CurrentToken();
                        string tokenInfo = current != null ? $"token type: {current.GetType().Name}, value: {current}" : "end of input";
                        throw new CompileError($"Expected expression after '==' operator, but found: {tokenInfo}");
                    }
                    left = new BinaryOperatorExpression(left, right, Operator.EQUAL, OperatorMappings.Equal);
                    continue;
                }
                // Matching PyKotor parser.py line 537: expression NOT_EQUALS expression
                // In PyKotor, != is tokenized as a single NOT_EQUALS token (lexer.py line 519-520)
                if (MatchOperator(NssOperators.NotEqual))
                {
                    // != operator (single token)
                    SkipWhitespaceAndComments();
                    Expression right = ParseRelationalExpression();
                    if (right == null)
                    {
                        throw new CompileError("Expected expression after '!=' operator");
                    }
                    left = new BinaryOperatorExpression(left, right, Operator.NOT_EQUAL, OperatorMappings.NotEqual);
                    continue;
                }
                break;
            }

            return left;
        }

        private Expression ParseRelationalExpression()
        {
            Expression left = ParseShiftExpression();
            if (left == null) return null;

            while (true)
            {
                SkipWhitespaceAndComments();
                // Matching PyKotor parser.py line 535: expression LESS_THAN_OR_EQUALS expression
                // In PyKotor, <= is tokenized as a single LESS_THAN_OR_EQUALS token (lexer.py line 532-533)
                // In our lexer, <= is also tokenized as NssOperators.LessThanOrEqual (NssLexer.cs line 447-454)
                if (MatchOperator(NssOperators.LessThanOrEqual))
                {
                    // <= operator (single token)
                    SkipWhitespaceAndComments();
                    Expression right = ParseShiftExpression();
                    if (right == null)
                    {
                        throw new CompileError("Expected expression after '<=' operator");
                    }
                    left = new BinaryOperatorExpression(left, right, Operator.LESS_THAN_OR_EQUAL, OperatorMappings.LessThanOrEqual);
                    continue;
                }
                // Matching PyKotor parser.py line 534: expression LESS_THAN expression
                if (MatchOperator(NssOperators.LessThan))
                {
                    // Check for << operator (bitwise left shift)
                    SkipWhitespaceAndComments();
                    if (CheckOperator(NssOperators.LessThan))
                    {
                        // << operator - handled by shift expression
                        _tokenIndex--;
                        break;
                    }
                    // < operator
                    SkipWhitespaceAndComments();
                    Expression right = ParseShiftExpression();
                    if (right == null)
                    {
                        throw new CompileError("Expected expression after '<' operator");
                    }
                    left = new BinaryOperatorExpression(left, right, Operator.LESS_THAN, OperatorMappings.LessThan);
                    continue;
                }
                // Matching PyKotor parser.py line 533: expression GREATER_THAN_OR_EQUALS expression
                // In PyKotor, >= is tokenized as a single GREATER_THAN_OR_EQUALS token (lexer.py line 532-533)
                // In our lexer, >= is also tokenized as NssOperators.GreaterThanOrEqual (NssLexer.cs line 456-461)
                if (MatchOperator(NssOperators.GreaterThanOrEqual))
                {
                    // >= operator (single token)
                    SkipWhitespaceAndComments();
                    Expression right = ParseShiftExpression();
                    if (right == null)
                    {
                        throw new CompileError("Expected expression after '>=' operator");
                    }
                    left = new BinaryOperatorExpression(left, right, Operator.GREATER_THAN_OR_EQUAL, OperatorMappings.GreaterThanOrEqual);
                    continue;
                }
                // Matching PyKotor parser.py line 532: expression GREATER_THAN expression
                if (MatchOperator(NssOperators.GreaterThan))
                {
                    // Check for >> or >>> operators (bitwise right shift)
                    SkipWhitespaceAndComments();
                    if (CheckOperator(NssOperators.GreaterThan))
                    {
                        // >> or >>> operator - handled by shift expression
                        _tokenIndex--;
                        break;
                    }
                    // > operator
                    SkipWhitespaceAndComments();
                    Expression right = ParseShiftExpression();
                    if (right == null)
                    {
                        throw new CompileError("Expected expression after '>' operator");
                    }
                    left = new BinaryOperatorExpression(left, right, Operator.GREATER_THAN, OperatorMappings.GreaterThan);
                    continue;
                }
                break;
            }

            return left;
        }

        private Expression ParseShiftExpression()
        {
            Expression left = ParseAdditiveExpression();
            if (left == null) return null;

            while (true)
            {
                SkipWhitespaceAndComments();
                int savedIndex = _tokenIndex;
                if (MatchOperator(NssOperators.LessThan))
                {
                    if (MatchOperator(NssOperators.LessThan))
                    {
                        // << operator
                        Expression right = ParseAdditiveExpression();
                        left = new BinaryOperatorExpression(left, right, Operator.BITWISE_LEFT, OperatorMappings.BitwiseLeft);
                        continue;
                    }
                    else
                    {
                        // Just a single <, backtrack for relational
                        _tokenIndex = savedIndex;
                        break;
                    }
                }
                if (MatchOperator(NssOperators.GreaterThan))
                {
                    if (MatchOperator(NssOperators.GreaterThan))
                    {
                        if (MatchOperator(NssOperators.GreaterThan))
                        {
                            // >>> operator
                            Expression right = ParseAdditiveExpression();
                            left = new BinaryOperatorExpression(left, right, Operator.BITWISE_RIGHT, OperatorMappings.BitwiseUnsignedRight);
                            continue;
                        }
                        else
                        {
                            // >> operator
                            Expression right = ParseAdditiveExpression();
                            left = new BinaryOperatorExpression(left, right, Operator.BITWISE_RIGHT, OperatorMappings.BitwiseRight);
                            continue;
                        }
                    }
                    else
                    {
                        // Just a single >, backtrack for relational
                        _tokenIndex = savedIndex;
                        break;
                    }
                }
                break;
            }

            return left;
        }

        private Expression ParseAdditiveExpression()
        {
            Expression left = ParseMultiplicativeExpression();
            if (left == null) return null;

            while (true)
            {
                SkipWhitespaceAndComments();
                if (MatchOperator(NssOperators.Addition))
                {
                    Expression right = ParseMultiplicativeExpression();
                    left = new BinaryOperatorExpression(left, right, Operator.ADDITION, OperatorMappings.Addition);
                    continue;
                }
                if (MatchOperator(NssOperators.Subtraction))
                {
                    Expression right = ParseMultiplicativeExpression();
                    left = new BinaryOperatorExpression(left, right, Operator.SUBTRACT, OperatorMappings.Subtraction);
                    continue;
                }
                break;
            }

            return left;
        }

        private Expression ParseMultiplicativeExpression()
        {
            Expression left = ParseUnaryExpression();
            if (left == null) return null;

            while (true)
            {
                SkipWhitespaceAndComments();
                if (MatchOperator(NssOperators.Multiplication))
                {
                    Expression right = ParseUnaryExpression();
                    left = new BinaryOperatorExpression(left, right, Operator.MULTIPLY, OperatorMappings.Multiplication);
                    continue;
                }
                if (MatchOperator(NssOperators.Division))
                {
                    Expression right = ParseUnaryExpression();
                    left = new BinaryOperatorExpression(left, right, Operator.DIVIDE, OperatorMappings.Division);
                    continue;
                }
                if (MatchOperator(NssOperators.Modulo))
                {
                    Expression right = ParseUnaryExpression();
                    left = new BinaryOperatorExpression(left, right, Operator.MODULUS, OperatorMappings.Modulus);
                    continue;
                }
                break;
            }

            return left;
        }

        private Expression ParseUnaryExpression()
        {
            SkipWhitespaceAndComments();

            // Prefix increment (++) - must check BEFORE unary plus
            // Matching PyKotor implementation order: multi-character operators first
            {
                int savedIndex = _tokenIndex;
                if (MatchOperator(NssOperators.Addition))
                {
                    if (MatchOperator(NssOperators.Addition))
                    {
                        Expression operand = ParseUnaryExpression();
                        FieldAccess fieldAccess = ConvertToFieldAccess(operand);
                        return new PreIncrementExpression(fieldAccess);
                    }
                    else
                    {
                        _tokenIndex = savedIndex;
                    }
                }
            }

            // Prefix decrement (--) - must check BEFORE unary minus
            // Matching PyKotor implementation order: multi-character operators first
            {
                int savedIndex = _tokenIndex;
                if (MatchOperator(NssOperators.Subtraction))
                {
                    if (MatchOperator(NssOperators.Subtraction))
                    {
                        Expression operand = ParseUnaryExpression();
                        FieldAccess fieldAccess = ConvertToFieldAccess(operand);
                        return new PreDecrementExpression(fieldAccess);
                    }
                    else
                    {
                        _tokenIndex = savedIndex;
                    }
                }
            }

            // Unary minus (after checking for --)
            if (MatchOperator(NssOperators.Subtraction))
            {
                Expression operand = ParseUnaryExpression();
                return new UnaryOperatorExpression(operand, Operator.SUBTRACT, OperatorMappings.Negation);
            }

            // Logical not
            if (MatchOperator(NssOperators.Not))
            {
                Expression operand = ParseUnaryExpression();
                return new UnaryOperatorExpression(operand, Operator.NOT, OperatorMappings.LogicalNot);
            }

            // Bitwise not
            if (MatchOperator(NssOperators.Inversion))
            {
                Expression operand = ParseUnaryExpression();
                return new UnaryOperatorExpression(operand, Operator.ONES_COMPLEMENT, OperatorMappings.BitwiseNot);
            }

            return ParsePostfixExpression();
        }

        private Expression ParsePostfixExpression()
        {
            Expression expr = ParsePrimaryExpression();
            if (expr == null) return null;

            while (true)
            {
                SkipWhitespaceAndComments();
                int savedIndex = _tokenIndex;

                // Postfix increment (++)
                if (MatchOperator(NssOperators.Addition))
                {
                    if (MatchOperator(NssOperators.Addition))
                    {
                        FieldAccess fieldAccess = ConvertToFieldAccess(expr);
                        expr = new PostIncrementExpression(fieldAccess);
                        continue;
                    }
                    else
                    {
                        _tokenIndex = savedIndex;
                        break;
                    }
                }

                // Postfix decrement (--)
                if (MatchOperator(NssOperators.Subtraction))
                {
                    if (MatchOperator(NssOperators.Subtraction))
                    {
                        FieldAccess fieldAccess = ConvertToFieldAccess(expr);
                        expr = new PostDecrementExpression(fieldAccess);
                        continue;
                    }
                    else
                    {
                        _tokenIndex = savedIndex;
                        break;
                    }
                }

                // Field access (.)
                if (MatchSeparator(NssSeparators.Dot))
                {
                    NssIdentifier field = ConsumeToken<NssIdentifier>("Expected field name after '.'");
                    FieldAccess existingAccess = ConvertToFieldAccess(expr);
                    existingAccess.Identifiers.Add(new Identifier(field.Identifier));
                    expr = existingAccess;
                    continue;
                }

                break;
            }

            return expr;
        }

        private FieldAccess ConvertToFieldAccess(Expression expr)
        {
            if (expr is FieldAccess fa)
            {
                return fa;
            }
            if (expr is IdentifierExpression ie)
            {
                return new FieldAccess(new List<Identifier> { ie.Identifier });
            }
            throw new CompileError($"Cannot use {expr.GetType().Name} as lvalue for increment/decrement or field access");
        }

        private Expression ParsePrimaryExpression()
        {
            SkipWhitespaceAndComments();

            // Parenthesized expression
            if (MatchSeparator(NssSeparators.OpenParen))
            {
                Expression inner = ParseExpression();
                SkipWhitespaceAndComments();
                ConsumeSeparator(NssSeparators.CloseParen, "Expected ')' after expression");
                return inner;
            }

            // Vector literal: [x, y, z]
            if (MatchSeparator(NssSeparators.OpenSquareBracket))
            {
                Expression x = ParseExpression();
                ConsumeSeparator(NssSeparators.Comma, "Expected ',' after vector x component");
                Expression y = ParseExpression();
                ConsumeSeparator(NssSeparators.Comma, "Expected ',' after vector y component");
                Expression z = ParseExpression();
                ConsumeSeparator(NssSeparators.CloseSquareBracket, "Expected ']' after vector components");
                return new VectorExpression(x, y, z);
            }

            // Vector constructor: Vector(x, y, z)
            if (CheckToken<NssKeyword>())
            {
                var kw = CurrentToken() as NssKeyword;
                if (kw.Keyword == NssKeywords.Vector)
                {
                    Advance();
                    ConsumeSeparator(NssSeparators.OpenParen, "Expected '(' after Vector");
                    Expression x = ParseExpression();
                    ConsumeSeparator(NssSeparators.Comma, "Expected ',' after x component");
                    Expression y = ParseExpression();
                    ConsumeSeparator(NssSeparators.Comma, "Expected ',' after y component");
                    Expression z = ParseExpression();
                    ConsumeSeparator(NssSeparators.CloseParen, "Expected ')' after Vector components");
                    return new VectorExpression(x, y, z);
                }
            }

            // Try function call
            Expression funcCall = TryParseFunctionCall();
            if (funcCall != null)
            {
                return funcCall;
            }

            // TryParseFunctionCall should have restored us to the identifier position
            // (identifierIndex if identifier found but no '(', or savedIndex if no identifier)
            // Since ParsePrimaryExpression already skipped whitespace at the start,
            // savedIndex in TryParseFunctionCall is at the identifier, so we should be at the identifier
            // Try identifier - should work since TryParseFunctionCall restored to identifierIndex
            NssTokenBase current = CurrentToken();
            if (current is NssIdentifier ident)
            {
                Advance();
                return new IdentifierExpression(new Identifier(ident.Identifier));
            }

            // Try literal
            if (CheckToken<NssLiteral>())
            {
                NssLiteral lit = ConsumeToken<NssLiteral>("");
                return ParseLiteral(lit);
            }

            // Constants
            if (CheckToken<NssKeyword>())
            {
                var kw = CurrentToken() as NssKeyword;
                if (kw.Keyword == NssKeywords.ObjectSelf)
                {
                    Advance();
                    return new ObjectExpression(0); // OBJECT_SELF
                }
                if (kw.Keyword == NssKeywords.ObjectInvalid)
                {
                    Advance();
                    return new ObjectExpression(1); // OBJECT_INVALID
                }
            }

            return null;
        }

        private bool CheckOperator(NssOperators op)
        {
            SkipWhitespaceAndComments();
            return CurrentToken() is NssOperator oper && oper.Operator == op;
        }

        private Expression TryParseFunctionCall()
        {
            // Don't skip whitespace here - ParsePrimaryExpression already did that
            // Save the current position (should be at identifier after ParsePrimaryExpression's whitespace skip)
            int savedIndex = _tokenIndex;
            try
            {
                if (!CheckToken<NssIdentifier>())
                {
                    _tokenIndex = savedIndex;
                    return null;
                }

                // Save index before consuming identifier (this is the identifier position)
                int identifierIndex = _tokenIndex;
                NssIdentifier funcName = ConsumeToken<NssIdentifier>("");
                // Skip whitespace before checking for opening paren
                SkipWhitespaceAndComments();
                if (!CheckSeparator(NssSeparators.OpenParen))
                {
                    // Restore to identifier position so ParsePrimaryExpression can parse it as an identifier
                    _tokenIndex = identifierIndex;
                    return null;
                }
                // Consume the opening paren
                Advance();

                // Check if it's an engine function
                ScriptFunction engineFunc = _functions?.FirstOrDefault(f => f.Name == funcName.Identifier);
                if (engineFunc != null)
                {
                    var args = new List<Expression>();

                    if (!CheckSeparator(NssSeparators.CloseParen))
                    {
                        do
                        {
                            SkipWhitespaceAndComments();
                            Expression arg = ParseExpression();
                            if (arg != null)
                            {
                                args.Add(arg);
                            }

                            if (!MatchSeparator(NssSeparators.Comma))
                            {
                                break;
                            }
                        } while (true);
                    }

                    ConsumeSeparator(NssSeparators.CloseParen, "Expected ')' after function arguments");

                    // Find routine ID
                    int routineId = _functions.IndexOf(engineFunc);

                    return new EngineCallExpression(engineFunc, routineId, args);
                }
                else
                {
                    // Regular function call
                    var args = new List<Expression>();

                    if (!CheckSeparator(NssSeparators.CloseParen))
                    {
                        do
                        {
                            SkipWhitespaceAndComments();
                            Expression arg = ParseExpression();
                            if (arg != null)
                            {
                                args.Add(arg);
                            }

                            if (!MatchSeparator(NssSeparators.Comma))
                            {
                                break;
                            }
                        } while (true);
                    }

                    ConsumeSeparator(NssSeparators.CloseParen, "Expected ')' after function arguments");

                    return new FunctionCallExpression(new Identifier(funcName.Identifier), args);
                }
            }
            catch (CompileError)
            {
                // Re-throw CompileError so we can see what went wrong
                throw;
            }
            catch (Exception)
            {
                // Restore to original position on any error
                _tokenIndex = savedIndex;
                // Silently fail for try-parse methods
                return null;
            }
        }

        private Expression ParseLiteral(NssLiteral lit)
        {
            switch (lit.LiteralType)
            {
                case NssLiteralType.Int:
                    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/lexer.py:327-335
                    // Original: def t_INT_HEX_VALUE(self, t): "0x[0-9a-fA-F]+" / def t_INT_VALUE(self, t): "[0-9]+"
                    string literalText = lit.Literal;
                    if (!string.IsNullOrEmpty(literalText) &&
                        literalText.Length > 2 &&
                        literalText.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase))
                    {
                        string hexText = literalText.Substring(2);
                        int hexVal;
                        if (int.TryParse(hexText, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out hexVal))
                        {
                            return new IntExpression(hexVal);
                        }
                    }
                    else
                    {
                        int intVal;
                        if (int.TryParse(lit.Literal, out intVal))
                        {
                            return new IntExpression(intVal);
                        }
                    }
                    break;
                case NssLiteralType.Float:
                    string floatStr = lit.Literal.TrimEnd('f', 'F');
                    if (float.TryParse(floatStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatVal))
                    {
                        return new FloatExpression(floatVal);
                    }
                    break;
                case NssLiteralType.String:
                    // Remove quotes
                    string strVal = lit.Literal;
                    if (strVal.Length >= 2 && strVal[0] == '"' && strVal[strVal.Length - 1] == '"')
                    {
                        strVal = strVal.Substring(1, strVal.Length - 2);
                        // Unescape string: handle common escape sequences (\n, \t, \r, \\, \", \', \0)
                        strVal = UnescapeString(strVal);
                    }
                    return new StringExpression(strVal);
            }
            return null;
        }

        private bool MatchSeparator(NssSeparators sep)
        {
            SkipWhitespaceAndComments();
            if (CurrentToken() is NssSeparator separator && separator.Separator == sep)
            {
                Advance();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Unescapes a string literal by processing escape sequences.
        /// Handles common C-style escape sequences: \n, \t, \r, \\, \", \', \0
        /// </summary>
        /// <param name="str">The string to unescape</param>
        /// <returns>The unescaped string</returns>
        private string UnescapeString(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return str;
            }

            var result = new System.Text.StringBuilder(str.Length);
            for (int i = 0; i < str.Length; i++)
            {
                if (str[i] == '\\' && i + 1 < str.Length)
                {
                    char next = str[i + 1];
                    switch (next)
                    {
                        case 'n':
                            result.Append('\n');
                            i++; // Skip the 'n'
                            break;
                        case 't':
                            result.Append('\t');
                            i++; // Skip the 't'
                            break;
                        case 'r':
                            result.Append('\r');
                            i++; // Skip the 'r'
                            break;
                        case '\\':
                            result.Append('\\');
                            i++; // Skip the second '\'
                            break;
                        case '"':
                            result.Append('"');
                            i++; // Skip the '"'
                            break;
                        case '\'':
                            result.Append('\'');
                            i++; // Skip the '\''
                            break;
                        case '0':
                            result.Append('\0');
                            i++; // Skip the '0'
                            break;
                        default:
                            // Unknown escape sequence, keep as-is
                            result.Append('\\');
                            result.Append(next);
                            i++; // Skip the next character
                            break;
                    }
                }
                else
                {
                    result.Append(str[i]);
                }
            }
            return result.ToString();
        }

        private bool CheckSeparator(NssSeparators sep)
        {
            return CurrentToken() is NssSeparator separator && separator.Separator == sep;
        }

        private void ConsumeSeparator(NssSeparators sep, string errorMessage)
        {
            if (!MatchSeparator(sep))
            {
                throw new CompileError(errorMessage);
            }
        }

        private bool MatchOperator(NssOperators op)
        {
            if (CurrentToken() is NssOperator oper && oper.Operator == op)
            {
                Advance();
                return true;
            }
            return false;
        }

        private bool MatchCompoundOperator(params NssOperators[] ops)
        {
            SkipWhitespaceAndComments();
            int savedIndex = _tokenIndex;
            foreach (NssOperators op in ops)
            {
                if (CurrentToken() is NssOperator oper && oper.Operator == op)
                {
                    Advance();
                    SkipWhitespaceAndComments();
                    continue;
                }
                _tokenIndex = savedIndex;
                return false;
            }
            return true;
        }
    }
}

