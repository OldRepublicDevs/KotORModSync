using System.Collections.Generic;
using System.IO;
using System.Linq;
using KOTORModSync.Common.Script;
using KOTORModSync.Formats.NCS.Compiler.NSS;
using JetBrains.Annotations;

namespace KOTORModSync.Formats.NCS.Compiler
{

    /// <summary>
    /// Root compilation context for NSS compilation.
    ///
    /// Manages global scope, function definitions, constants, and compilation state.
    /// Provides symbol resolution and type checking during NSS to NCS compilation.
    ///
    /// References:
    ///     vendor/KotOR.js/src/nwscript/NWScriptCompiler.ts (TypeScript compiler architecture)
    ///     vendor/xoreos-tools/src/nwscript/decompiler.cpp (NCS decompiler, reverse reference for compilation)
    ///     vendor/HoloLSP/server/src/nwscript-parser.ts (NSS parser and AST generation)
    /// </summary>
    public class CodeRoot
    {
        public List<TopLevelObject> Objects { get; set; }
        public Dictionary<string, byte[]> Library { get; set; }
        public List<ScriptFunction> Functions { get; set; }
        public List<ScriptConstant> Constants { get; set; }
        public List<string> LibraryLookup { get; set; }
        public Dictionary<string, FunctionReference> FunctionMap { get; set; }
        private readonly List<ScopedValue> _globalScope = new List<ScopedValue>();
        public Dictionary<string, Struct> StructMap { get; set; }

        public CodeRoot(
            [CanBeNull] List<ScriptConstant> constants,
            List<ScriptFunction> functions,
            IEnumerable<string> libraryLookup,
            Dictionary<string, byte[]> library)
        {
            Objects = new List<TopLevelObject>();
            Library = library ?? new Dictionary<string, byte[]>();
            Functions = functions ?? new List<ScriptFunction>();
            Constants = constants ?? new List<ScriptConstant>();
            LibraryLookup = libraryLookup?.ToList() ?? new List<string>();
            FunctionMap = new Dictionary<string, FunctionReference>();
            StructMap = new Dictionary<string, Struct>();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/classes.py:537-538
        public void AddScoped(Identifier identifier, DynamicDataType datatype, bool isConst = false)
        {
            _globalScope.Insert(0, new ScopedValue(identifier, datatype, isConst));
        }

        public GetScopedResult GetScoped(Identifier identifier, CodeRoot root)
        {
            int offset = 0;
            ScopedValue found = null;
            foreach (ScopedValue scoped in _globalScope)
            {
                offset -= scoped.DataType.Size(root);
                if (scoped.Identifier.Equals(identifier))
                {
                    found = scoped;
                    break;
                }
            }

            if (found == null)
            {
                // Provide helpful error with available globals
                List<string> available = _globalScope.Take(10).Select(s => s.Identifier.Label).ToList();
                int more = _globalScope.Count - 10;
                string moreText = more > 0 ? $" (and {more} more)" : "";
                string msg = $"Undefined variable '{identifier}'\n" +
                             $"  Available globals: {string.Join(", ", available)}{moreText}";
                throw new NSS.CompileError(msg);
            }

            // Matching PyKotor classes.py line 556
            return new GetScopedResult(true, found.DataType, offset, found.IsConst);
        }

        public void Compile(NCS ncs)
        {
            // nwnnsscomp processes the includes and global variable declarations before functions regardless if they are
            // placed before or after function definitions. We will replicate this behavior.

            bool debug = System.Environment.GetEnvironmentVariable("NCS_INTERPRETER_DEBUG") == "true";

            List<IncludeScript> included = new List<IncludeScript>();
            while (Objects.Any(obj => obj is IncludeScript))
            {
                List<IncludeScript> includes = Objects.OfType<IncludeScript>().ToList();
                IncludeScript include = includes.Last();
                Objects.Remove(include);
                included.Add(include);
                include.Compile(ncs, this);
            }

            List<TopLevelObject> scriptGlobals = Objects.OfType<GlobalVariableDeclaration>()
                .Concat<TopLevelObject>(Objects.OfType<GlobalVariableInitialization>())
                .Concat(Objects.OfType<StructDefinition>())
                .ToList();
            List<TopLevelObject> others = Objects.Where(obj => !included.Contains(obj) && !scriptGlobals.Contains(obj)).ToList();

            // The external compiler (nwnnsscomp) always places the entry stub at the BEGINNING (index 0)
            // When there are globals: JSR jumps to first global, RETN, then globals, SAVEBP, then RESTOREBP, MOVSP, RETN after SAVEBP
            // When there are no globals: JSR jumps to main, RETN
            bool hasGlobals = scriptGlobals.Any();
            NCSInstruction firstGlobalInstruction = null;

            if (hasGlobals)
            {
                // Compile globals first (they'll be after the entry stub we insert at index 0)
                foreach (TopLevelObject globalDef in scriptGlobals)
                {
                    if (firstGlobalInstruction == null)
                    {
                        // Remember the first global instruction for the JSR jump target
                        int beforeCount = ncs.Instructions.Count;
                        globalDef.Compile(ncs, this);
                        if (ncs.Instructions.Count > beforeCount)
                        {
                            firstGlobalInstruction = ncs.Instructions[beforeCount];
                        }
                    }
                    else
                    {
                        globalDef.Compile(ncs, this);
                    }
                }
                ncs.Add(NCSInstructionType.SAVEBP, new List<object>());
            }

            // Compile functions
            foreach (TopLevelObject obj in others)
            {
                obj.Compile(ncs, this);
            }

            if (debug)
            {
                System.Console.WriteLine("=== Function map ===");
                foreach (var kvp in FunctionMap)
                {
                    int idx = ncs.GetInstructionIndex(kvp.Value.Instruction);
                    System.Console.WriteLine($"{kvp.Key} -> idx {idx} ins {kvp.Value.Instruction?.InsType}");
                }
                System.Console.WriteLine("=== Instruction listing ===");
                for (int i = 0; i < ncs.Instructions.Count; i++)
                {
                    NCSInstruction inst = ncs.Instructions[i];
                    int jumpIdx = inst.Jump != null ? ncs.GetInstructionIndex(inst.Jump) : -1;
                    System.Console.WriteLine($"{i}: {inst.InsType} args=[{string.Join(",", inst.Args ?? new List<object>())}] jumpIdx={jumpIdx}");
                }
            }

            if (FunctionMap.ContainsKey("main"))
            {
                NCSInstruction mainStart = FirstNonNop(FunctionMap["main"].Instruction, ncs);
                FunctionMap["main"] = new FunctionReference(mainStart, FunctionMap["main"].Definition);

                // The external compiler (nwnnsscomp) always places entry stub at BEGINNING (index 0)
                // Insert RETN first, then JSR at the same index, so JSR comes first in final order
                NCSInstruction entryJsrTarget = hasGlobals ? (firstGlobalInstruction ?? mainStart) : mainStart;
                ncs.Add(NCSInstructionType.RETN, new List<object>(), null, 0);
                NCSInstruction entryJsr = ncs.Add(NCSInstructionType.JSR, new List<object>(), entryJsrTarget, 0);
                entryJsr.Jump = entryJsrTarget;

                if (hasGlobals)
                {
                    // After SAVEBP, the external compiler adds: JSR (to main), RESTOREBP, MOVSP, RETN
                    // Find SAVEBP index (it was added before functions were compiled)
                    int savebpIndex = -1;
                    for (int i = 0; i < ncs.Instructions.Count; i++)
                    {
                        if (ncs.Instructions[i].InsType == NCSInstructionType.SAVEBP)
                        {
                            savebpIndex = i;
                            break;
                        }
                    }
                    if (savebpIndex >= 0)
                    {
                        int globalsSize = ScopeSize();
                        int afterSavebpIndex = savebpIndex + 1;
                        // Insert in reverse order so final order is: JSR, RESTOREBP, MOVSP, RETN
                        // Last inserted comes first, so insert: RETN, MOVSP, RESTOREBP, JSR
                        ncs.Add(NCSInstructionType.RETN, new List<object>(), null, afterSavebpIndex);
                        ncs.Add(NCSInstructionType.MOVSP, new List<object> { globalsSize }, null, afterSavebpIndex);
                        ncs.Add(NCSInstructionType.RESTOREBP, new List<object>(), null, afterSavebpIndex);
                        NCSInstruction afterSavebpJsr = ncs.Add(NCSInstructionType.JSR, new List<object>(), mainStart, afterSavebpIndex);
                        afterSavebpJsr.Jump = mainStart;
                    }
                }

                if (debug)
                {
                    System.Console.WriteLine($"Entry JSR targets main at idx {ncs.GetInstructionIndex(mainStart)}");
                    System.Console.WriteLine("=== Instructions after entry stub ===");
                    for (int i = 0; i < ncs.Instructions.Count; i++)
                    {
                        NCSInstruction inst = ncs.Instructions[i];
                        int jumpIdx = inst.Jump != null ? ncs.GetInstructionIndex(inst.Jump) : -1;
                        System.Console.WriteLine($"{i}: {inst.InsType} args=[{string.Join(",", inst.Args ?? new List<object>())}] jumpIdx={jumpIdx}");
                    }
                }
            }
            else if (FunctionMap.ContainsKey("StartingConditional"))
            {
                NCSInstruction scStart = FirstNonNop(FunctionMap["StartingConditional"].Instruction, ncs);
                FunctionMap["StartingConditional"] = new FunctionReference(scStart, FunctionMap["StartingConditional"].Definition);
                // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/classes.py:417-423
                // Original: ncs.add(NCSInstructionType.RETN, args=[], index=entry_index) then JSR then RSADDI, all at entry_index
                // Adding RETN first, then JSR, then RSADDI at the same index, so final order is RSADDI, JSR, RETN
                // The external compiler places entry stub at BEGINNING (index 0) for StartingConditional too
                ncs.Add(NCSInstructionType.RETN, new List<object>(), null, 0);
                NCSInstruction entryJsr = ncs.Add(NCSInstructionType.JSR, new List<object>(), scStart, 0);
                entryJsr.Jump = scStart;
                ncs.Add(NCSInstructionType.RSADDI, new List<object>(), null, 0);

                if (debug)
                {
                    System.Console.WriteLine($"Entry JSR targets StartingConditional at idx {ncs.GetInstructionIndex(scStart)}");
                }
            }
            else
            {
                string msg = "This file has no entry point and cannot be compiled (Most likely an include file).";
                throw new EntryPointError(msg);
            }
        }

        private static NCSInstruction FirstNonNop(NCSInstruction start, NCS ncs)
        {
            int idx = ncs.GetInstructionIndex(start);
            if (idx < 0)
            {
                return start;
            }
            for (int i = idx; i < ncs.Instructions.Count; i++)
            {
                NCSInstruction inst = ncs.Instructions[i];
                if (inst.InsType != NCSInstructionType.NOP)
                {
                    return inst;
                }
            }
            return start;
        }

        public int ScopeSize()
        {
            return 0 - _globalScope.Sum(scoped => scoped.DataType.Size(this));
        }

        public DynamicDataType CompileJsr(NCS ncs, CodeBlock block, string name, List<Expression> args)
        {
            List<Expression> argsList = new List<Expression>(args);

            FunctionReference funcMap = FunctionMap[name];
            object definition = funcMap.Definition;
            NCSInstruction startInstruction = funcMap.Instruction;

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/classes.py:428-536
            DynamicDataType returnType = GetReturnType(definition);
            int returnTypeSize = 0;
            if (returnType == DynamicDataType.INT)
            {
                ncs.Add(NCSInstructionType.RSADDI, new List<object>());
                returnTypeSize = 4;
            }
            else if (returnType == DynamicDataType.FLOAT)
            {
                ncs.Add(NCSInstructionType.RSADDF, new List<object>());
                returnTypeSize = 4;
            }
            else if (returnType == DynamicDataType.STRING)
            {
                ncs.Add(NCSInstructionType.RSADDS, new List<object>());
                returnTypeSize = 4;
            }
            else if (returnType == DynamicDataType.VECTOR)
            {
                ncs.Add(NCSInstructionType.RSADDF, new List<object>());
                ncs.Add(NCSInstructionType.RSADDF, new List<object>());
                ncs.Add(NCSInstructionType.RSADDF, new List<object>());
                returnTypeSize = 12;
            }
            else if (returnType == DynamicDataType.OBJECT)
            {
                ncs.Add(NCSInstructionType.RSADDO, new List<object>());
                returnTypeSize = 4;
            }
            else if (returnType == DynamicDataType.TALENT)
            {
                ncs.Add(NCSInstructionType.RSADDTAL, new List<object>());
                returnTypeSize = 4;
            }
            else if (returnType == DynamicDataType.EVENT)
            {
                ncs.Add(NCSInstructionType.RSADDEVT, new List<object>());
                returnTypeSize = 4;
            }
            else if (returnType == DynamicDataType.LOCATION)
            {
                ncs.Add(NCSInstructionType.RSADDLOC, new List<object>());
                returnTypeSize = 4;
            }
            else if (returnType == DynamicDataType.EFFECT)
            {
                ncs.Add(NCSInstructionType.RSADDEFF, new List<object>());
                returnTypeSize = 4;
            }
            else if (returnType == DynamicDataType.VOID)
            {
                returnTypeSize = 0;
            }
            else if (returnType.Builtin == DataType.Struct)
            {
                if (returnType.Struct != null && StructMap.TryGetValue(returnType.Struct, out Struct structDef))
                {
                    structDef.Initialize(ncs, this);
                    returnTypeSize = returnType.Size(this);
                }
                else
                {
                    throw new NSS.CompileError("Unknown struct type for return value");
                }
            }
            else
            {
                throw new NSS.CompileError($"Trying to return unsupported type '{returnType.Builtin}'");
            }

            // Track return value space in temp_stack
            // Matching PyKotor classes.py line 490
            block.TempStack += returnTypeSize;

            List<FunctionParameter> parameters = GetParameters(definition);
            List<FunctionParameter> requiredParams = parameters.Where(p => p.DefaultValue == null).ToList();

            if (requiredParams.Count > argsList.Count)
            {
                List<string> requiredNames = requiredParams.Select(p => p.Name.Label).ToList();
                string msg = $"Missing required parameters in call to '{name}'\n" +
                             $"  Required: {string.Join(", ", requiredNames)}\n" +
                             $"  Provided {argsList.Count} of {parameters.Count} parameters";
                throw new NSS.CompileError(msg);
            }

            while (parameters.Count > argsList.Count)
            {
                int paramIndex = argsList.Count;
                Expression defaultExpr = parameters[paramIndex].DefaultValue;
                if (defaultExpr == null)
                {
                    throw new NSS.CompileError($"Missing default value for parameter {paramIndex} in '{name}'");
                }
                argsList.Add(defaultExpr);
            }

            // Matching PyKotor classes.py lines 514-532
            // Push arguments in declaration order so the last parameter ends up deepest on the stack.
            int offset = 0;
            for (int i = 0; i < parameters.Count; i++)
            {
                FunctionParameter param = parameters[i];
                Expression arg = argsList[i];
                int tempStackBefore = block.TempStack;
                DynamicDataType argDatatype = arg.Compile(ncs, this, block);
                int tempStackAfter = block.TempStack;
                offset += argDatatype.Size(this);
                // Only add to temp_stack if the argument's compile method didn't already add it
                if (tempStackAfter == tempStackBefore)
                {
                    block.TempStack += argDatatype.Size(this);
                }
                if (param.DataType != argDatatype)
                {
                    string msg = $"Parameter type mismatch in call to '{GetIdentifier(definition)}'\n" +
                                 $"  Parameter '{param.Name}' expects: {param.DataType.Builtin}\n" +
                                 $"  Got: {argDatatype.Builtin}";
                    throw new NSS.CompileError(msg);
                }
            }
            // JSR consumes all arguments, so subtract their total size
            block.TempStack -= offset;
            ncs.Add(NCSInstructionType.JSR, new List<object>(), startInstruction);

            return returnType;
        }

        private DynamicDataType GetReturnType(object definition)
        {
            if (definition is FunctionDefinition fd)
            {
                return fd.ReturnType;
            }
            if (definition is FunctionForwardDeclaration ffd)
            {
                return ffd.ReturnType;
            }
            throw new NSS.CompileError("Invalid function definition type");
        }

        private List<FunctionParameter> GetParameters(object definition)
        {
            if (definition is FunctionDefinition fd)
            {
                return fd.Parameters;
            }
            if (definition is FunctionForwardDeclaration ffd)
            {
                return ffd.Parameters;
            }
            throw new NSS.CompileError("Invalid function definition type");
        }

        private Identifier GetIdentifier(object definition)
        {
            if (definition is FunctionDefinition fd)
            {
                return fd.Name;
            }
            if (definition is FunctionForwardDeclaration ffd)
            {
                return ffd.Identifier;
            }
            throw new NSS.CompileError("Invalid function definition type");
        }

        [CanBeNull]
        public ScriptFunction FindEngineFunction(string name)
        {
            return Functions.FirstOrDefault(f => f.Name == name);
        }

        [CanBeNull]
        public ScriptConstant FindConstant(string name)
        {
            return Constants.FirstOrDefault(c => c.Name == name);
        }

        [CanBeNull]
        public byte[] FindInclude(string includePath)
        {
            // Can be null if data not found
            if (Library.TryGetValue(includePath, out byte[] data))
            {
                return data;
            }

            foreach (string lookupPath in LibraryLookup)
            {
                string fullPath = Path.Combine(lookupPath, includePath);
                if (File.Exists(fullPath))
                {
                    return File.ReadAllBytes(fullPath);
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Represents a user-defined struct type.
    /// </summary>
    public class Struct
    {
        public Identifier Identifier { get; set; }
        public List<StructMember> Members { get; set; }
        private int? _cachedSize;

        public Struct(Identifier identifier, List<StructMember> members)
        {
            Identifier = identifier;
            Members = members;
        }

        public void Initialize(NCS ncs, CodeRoot root)
        {
            foreach (StructMember member in Members)
            {
                member.Initialize(ncs, root);
            }
        }

        public int Size(CodeRoot root)
        {
            if (_cachedSize.HasValue)
            {
                return _cachedSize.Value;
            }

            _cachedSize = Members.Sum(m => m.Size(root));
            return _cachedSize.Value;
        }

        public int ChildOffset(CodeRoot root, Identifier identifier)
        {
            int size = 0;
            foreach (StructMember member in Members)
            {
                if (member.Identifier.Equals(identifier))
                {
                    return size;
                }
                size += member.Size(root);
            }

            string available = string.Join(", ", Members.Select(m => m.Identifier.Label));
            throw new NSS.CompileError(
                $"Unknown member '{identifier}' in struct '{Identifier}'\n" +
                $"  Available members: {available}");
        }

        public DynamicDataType ChildType(CodeRoot root, Identifier identifier)
        {
            foreach (StructMember member in Members)
            {
                if (member.Identifier.Equals(identifier))
                {
                    return member.DataType;
                }
            }

            string available = string.Join(", ", Members.Select(m => m.Identifier.Label));
            throw new NSS.CompileError(
                $"Member '{identifier}' not found in struct '{Identifier}'\n" +
                $"  Available members: {available}");
        }
    }

    /// <summary>
    /// Represents a member of a user-defined struct.
    /// </summary>
    public class StructMember
    {
        public DynamicDataType DataType { get; set; }
        public Identifier Identifier { get; set; }

        public StructMember(DynamicDataType dataType, Identifier identifier)
        {
            DataType = dataType;
            Identifier = identifier;
        }

        public void Initialize(NCS ncs, CodeRoot root)
        {
            switch (DataType.Builtin)
            {
                case Common.Script.DataType.Int:
                    ncs.Add(NCSInstructionType.RSADDI, new List<object>());
                    break;
                case Common.Script.DataType.Float:
                    ncs.Add(NCSInstructionType.RSADDF, new List<object>());
                    break;
                case Common.Script.DataType.String:
                    ncs.Add(NCSInstructionType.RSADDS, new List<object>());
                    break;
                case Common.Script.DataType.Object:
                    ncs.Add(NCSInstructionType.RSADDO, new List<object>());
                    break;
                case Common.Script.DataType.Event:
                    ncs.Add(NCSInstructionType.RSADDEVT, new List<object>());
                    break;
                case Common.Script.DataType.Effect:
                    ncs.Add(NCSInstructionType.RSADDEFF, new List<object>());
                    break;
                case Common.Script.DataType.Location:
                    ncs.Add(NCSInstructionType.RSADDLOC, new List<object>());
                    break;
                case Common.Script.DataType.Talent:
                    ncs.Add(NCSInstructionType.RSADDTAL, new List<object>());
                    break;
                case Common.Script.DataType.Vector:
                    ncs.Add(NCSInstructionType.RSADDF, new List<object>());
                    ncs.Add(NCSInstructionType.RSADDF, new List<object>());
                    ncs.Add(NCSInstructionType.RSADDF, new List<object>());
                    break;
                case Common.Script.DataType.Struct:
                    // Can be null if struct not found
                    if (DataType.Struct != null && root.StructMap.TryGetValue(DataType.Struct, out Struct structDef))
                    {
                        structDef.Initialize(ncs, root);
                    }
                    else
                    {
                        throw new NSS.CompileError($"Unknown struct type for member '{Identifier}'");
                    }
                    break;
                default:
                    throw new NSS.CompileError($"Unsupported struct member type: {DataType.Builtin}");
            }
        }

        public int Size(CodeRoot root)
        {
            return DataType.Size(root);
        }
    }

    /// <summary>
    /// Represents a function definition with implementation.
    /// Contains the function signature (return type, parameters) and the code block
    /// that implements the function body.
    /// <summary>
    /// Represents a function definition with implementation.
    ///
    /// Contains the function signature (return type, parameters) and the code block
    /// that implements the function body.
    ///
    /// Note: Signature and block are currently coupled in this class. Future refactoring
    /// could split these into separate FunctionSignature and CodeBlock for better reusability.
    /// </summary>
    public class FunctionDefinition : TopLevelObject
    {
        public Identifier Name { get; set; }
        public DynamicDataType ReturnType { get; set; }
        public List<FunctionParameter> Parameters { get; set; }
        public CodeBlock Body { get; set; }

        public FunctionDefinition(Identifier name, DynamicDataType returnType, List<FunctionParameter> parameters, CodeBlock body)
        {
            Name = name;
            ReturnType = returnType;
            Parameters = parameters;
            Body = body;

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/classes.py:731-732
            // Original: for param in parameters: block.add_scoped(param.identifier, param.data_type)
            // Parameters are added in forward order; add_scoped uses insert(0, ...) which reverses them
            foreach (FunctionParameter param in parameters)
            {
                body.AddScoped(param.Name, param.DataType);
            }
        }

        public override void Compile(NCS ncs, CodeRoot root)
        {
            string name = Name.Label;
            bool debug = System.Environment.GetEnvironmentVariable("NCS_INTERPRETER_DEBUG") == "true";

            // Make sure all default parameters appear after the required parameters
            bool previousIsDefault = false;
            foreach (FunctionParameter param in Parameters)
            {
                bool isDefault = param.DefaultValue != null;
                if (previousIsDefault && !isDefault)
                {
                    throw new NSS.CompileError(
                        "Function parameter without a default value can't follow one with a default value.");
                }
                previousIsDefault = isDefault;
            }

            // Make sure params are all constant values
            foreach (FunctionParameter param in Parameters)
            {
                if (param.DefaultValue is IdentifierExpression identifierExpr)
                {
                    // Check if it's a constant
                    if (!identifierExpr.IsConstant(root))
                    {
                        throw new NSS.CompileError(
                            $"Non-constant default value specified for function prototype parameter '{param.Name}'.");
                    }
                }
            }

            if (root.FunctionMap.ContainsKey(name) && !root.FunctionMap[name].IsPrototype())
            {
                throw new NSS.CompileError(
                    $"Function '{name}' is already defined\n" +
                    "  Cannot redefine a function that already has an implementation");
            }
            if (root.FunctionMap.ContainsKey(name) && root.FunctionMap[name].IsPrototype())
            {
                CompileFunctionWithPrototype(root, name, ncs);
            }
            else
            {
                NCSInstruction retn = new NCSInstruction(NCSInstructionType.RETN);

                NCSInstruction functionStart = ncs.Add(NCSInstructionType.NOP, new List<object>());
                Body.Compile(ncs, root, null, retn, null, null);
                ncs.Instructions.Add(retn);

                root.FunctionMap[name] = new FunctionReference(functionStart, this);

                if (debug)
                {
                    System.Console.WriteLine($"Compiled function {name} start idx {ncs.GetInstructionIndex(functionStart)} count {ncs.Instructions.Count}");
                }
            }
        }

        private void CompileFunctionWithPrototype(CodeRoot root, string name, NCS ncs)
        {
            bool debug = System.Environment.GetEnvironmentVariable("NCS_INTERPRETER_DEBUG") == "true";
            object prototypeDef = root.FunctionMap[name].Definition;
            if (!IsMatchingSignature(prototypeDef))
            {
                // Build detailed error message
                List<string> details = new List<string>();
                if (ReturnType != GetReturnType(prototypeDef))
                {
                    details.Add(
                        $"Return type mismatch: prototype has {GetReturnType(prototypeDef).Builtin}, " +
                        $"definition has {ReturnType.Builtin}");
                }
                if (Parameters.Count != GetParameters(prototypeDef).Count)
                {
                    details.Add(
                        $"Parameter count mismatch: prototype has {GetParameters(prototypeDef).Count}, " +
                        $"definition has {Parameters.Count}");
                }
                else
                {
                    List<FunctionParameter> protoParams = GetParameters(prototypeDef);
                    for (int i = 0; i < Parameters.Count; i++)
                    {
                        if (Parameters[i].DataType != protoParams[i].DataType)
                        {
                            details.Add(
                                $"Parameter {i + 1} type mismatch: prototype has {protoParams[i].DataType.Builtin}, " +
                                $"definition has {Parameters[i].DataType.Builtin}");
                        }
                    }
                }

                string msg = $"Function '{name}' definition does not match its prototype\n" +
                            "  " + string.Join("\n  ", details);
                throw new NSS.CompileError(msg);
            }

            // Function has forward declaration, insert the compiled definition after the stub
            NCS temp = new NCS();
            NCSInstruction retn = new NCSInstruction(NCSInstructionType.RETN);
            Body.Compile(temp, root, null, retn, null, null);
            temp.Instructions.Add(retn);

            NCSInstruction stubInstruction = root.FunctionMap[name].Instruction;
            int stubIndex = ncs.GetInstructionIndex(stubInstruction);
            if (debug)
            {
                System.Console.WriteLine($"CompileFunctionWithPrototype for {name}: stubIndex={stubIndex} countBefore={ncs.Instructions.Count}");
            }
            NCSInstruction newStart;
            if (stubIndex >= 0)
            {
                ncs.Instructions.RemoveAt(stubIndex);
                ncs.Instructions.InsertRange(stubIndex, temp.Instructions);
                newStart = ncs.Instructions[stubIndex];
            }
            else
            {
                ncs.Instructions.AddRange(temp.Instructions);
                newStart = temp.Instructions[0];
            }

            // Redirect any existing jumps that pointed to the prototype stub to the new function start
            int updated = 0;
            foreach (NCSInstruction inst in ncs.Instructions)
            {
                if (ReferenceEquals(inst.Jump, root.FunctionMap[name].Instruction))
                {
                    inst.Jump = newStart;
                    updated++;
                }
                else if (inst.Jump != null && !ncs.Instructions.Contains(inst.Jump))
                {
                    // Jump target no longer exists (prototype stub removed) – redirect to new function start.
                    inst.Jump = newStart;
                    updated++;
                }
            }

            root.FunctionMap[name] = new FunctionReference(newStart, this);

            if (debug)
            {
                System.Console.WriteLine($"CompileFunctionWithPrototype for {name}: newStartIdx={ncs.GetInstructionIndex(newStart)} updatedJumps={updated} countAfter={ncs.Instructions.Count}");
            }
        }

        private bool IsMatchingSignature(object prototype)
        {
            if (ReturnType != GetReturnType(prototype))
            {
                return false;
            }
            if (Parameters.Count != GetParameters(prototype).Count)
            {
                return false;
            }
            List<FunctionParameter> protoParams = GetParameters(prototype);
            for (int i = 0; i < Parameters.Count; i++)
            {
                if (Parameters[i].DataType != protoParams[i].DataType)
                {
                    return false;
                }
            }
            return true;
        }

        private DynamicDataType GetReturnType(object definition)
        {
            if (definition is FunctionDefinition fd)
            {
                return fd.ReturnType;
            }
            if (definition is FunctionForwardDeclaration ffd)
            {
                return ffd.ReturnType;
            }
            throw new NSS.CompileError("Invalid function definition type");
        }

        private List<FunctionParameter> GetParameters(object definition)
        {
            if (definition is FunctionDefinition fd)
            {
                return fd.Parameters;
            }
            if (definition is FunctionForwardDeclaration ffd)
            {
                return ffd.Parameters;
            }
            throw new NSS.CompileError("Invalid function definition type");
        }
    }

    /// <summary>
    /// Represents a function parameter with optional default value.
    /// </summary>
    public class FunctionParameter
    {
        public Identifier Name { get; set; }
        public DynamicDataType DataType { get; set; }
        [CanBeNull]
        public Expression DefaultValue { get; set; }

        public FunctionParameter(Identifier name, [CanBeNull] DynamicDataType dataType, [CanBeNull] Expression defaultValue = null)
        {
            Name = name;
            DataType = dataType;
            DefaultValue = defaultValue;
        }
    }
}

