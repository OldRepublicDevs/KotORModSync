using System.Collections.Generic;
using System.Linq;
using KOTORModSync.Formats.NCS;
using JetBrains.Annotations;

namespace KOTORModSync.Formats.NCS.Compiler
{

    /// <summary>
    /// Represents a block of code containing statements and local variable declarations.
    /// </summary>
    public class CodeBlock
    {
        public List<Statement> Statements { get; set; }
        public List<ScopedValue> Scope { get; set; }
        public Dictionary<Identifier, (DynamicDataType DataType, int Offset)> LocalVariables { get; set; }
        public int StackOffset { get; set; }
        public int TempStack { get; set; }
        [CanBeNull]
        public CodeBlock Parent { get; set; }
        private bool _breakScope;

        public CodeBlock([CanBeNull] CodeBlock parent = null)
        {
            Statements = new List<Statement>();
            Scope = new List<ScopedValue>();
            LocalVariables = new Dictionary<Identifier, (DynamicDataType, int)>();
            StackOffset = 0;
            TempStack = 0;
            Parent = parent;
            _breakScope = false;
        }

        public void AddLocal(Identifier identifier, DynamicDataType dataType, CodeRoot root)
        {
            int size = dataType.Size(root);
            LocalVariables[identifier] = (dataType, StackOffset);
            StackOffset += size;
            // Also add to Scope so GetScoped can find it
            AddScoped(identifier, dataType);
        }

        public bool TryGetLocal(Identifier identifier, out (DynamicDataType DataType, int Offset) result)
        {
            if (LocalVariables.TryGetValue(identifier, out result))
            {
                return true;
            }

            if (Parent != null)
            {
                return Parent.TryGetLocal(identifier, out result);
            }

            return false;
        }

        public void Compile(
            NCS ncs,
            CodeRoot root,
            [CanBeNull] CodeBlock block,
            NCSInstruction returnInstruction,
            [CanBeNull] NCSInstruction breakInstruction,
            [CanBeNull] NCSInstruction continueInstruction)
        {
            Parent = block;
            // Reset temp_stack at the start of block compilation
            // Each block tracks its own temporary stack independently
            TempStack = 0;

            foreach (Statement statement in Statements)
            {
                // ReturnStatement.Compile handles all the return logic (CPDOWNSP, MOVSP, JMP) internally
                // Just call Compile for all statements - ReturnStatement will generate the correct code
                statement.Compile(ncs, root, this, returnInstruction, breakInstruction, continueInstruction);
                
                // If this was a return statement, exit early - the rest of the block is unreachable
                if (statement is ReturnStatement)
                {
                    return;
                }
            }
            // Matching PyKotor implementation: only add MOVSP if ScopeSize is non-zero
            // External compiler optimizes away MOVSP with offset 0, so we should match that behavior
            int finalScopeSize = ScopeSize(root);
            if (finalScopeSize != 0)
            {
                ncs.Instructions.Add(new NCSInstruction(NCSInstructionType.MOVSP, new List<object> { -finalScopeSize }));
            }

            if (TempStack != 0)
            {
                // Defensive cleanup: ensure temp stack is balanced before leaving the block.
                ncs.Instructions.Add(new NCSInstruction(NCSInstructionType.MOVSP, new List<object> { -TempStack }));
                TempStack = 0;
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/classes.py:632-633
        public void AddScoped(Identifier identifier, DynamicDataType dataType, bool isConst = false)
        {
            // Insert at beginning to match Python's list.insert(0, ...)
            Scope.Insert(0, new ScopedValue(identifier, dataType, isConst));
        }

        public void MarkBreakScope()
        {
            _breakScope = true;
        }

        public GetScopedResult GetScoped(Identifier identifier, CodeRoot root, int? offset = null)
        {
            return GetScopedInternal(identifier, root, offset, new HashSet<CodeBlock>());
        }

        private GetScopedResult GetScopedInternal(Identifier identifier, CodeRoot root, int? offset, HashSet<CodeBlock> visited)
        {
            // Prevent infinite recursion from circular parent chains
            if (visited.Contains(this))
            {
                throw new CompileError($"Circular parent chain detected in CodeBlock.GetScoped for identifier '{identifier}'");
            }
            visited.Add(this);

            // Matching PyKotor classes.py line 641: offset = -self.temp_stack if offset is None else offset - self.temp_stack
            int currentOffset = offset == null ? -TempStack : offset.Value - TempStack;

            // Python implementation uses for...else: loop through scope, break if found, else check parent
            ScopedValue found = null;
            foreach (ScopedValue scoped in Scope)
            {
                currentOffset -= scoped.DataType.Size(root);
                if (scoped.Identifier.Equals(identifier))
                {
                    found = scoped;
                    break;
                }
            }

            if (found == null)
            {
                // Identifier not found in this scope, check parent or root
                if (Parent != null)
                {
                    // Call the private method to maintain visited set
                    return Parent.GetScopedInternal(identifier, root, currentOffset, visited);
                }
                return root.GetScoped(identifier, root);
            }

            // Identifier found in this scope
            // Matching PyKotor classes.py line 650
            return new GetScopedResult(false, found.DataType, currentOffset, found.IsConst);
        }

        public int ScopeSize(CodeRoot root)
        {
            return Scope.Sum(scoped => scoped.DataType.Size(root));
        }

        public int FullScopeSize(CodeRoot root)
        {
            int size = ScopeSize(root);
            if (Parent != null)
            {
                size += Parent.FullScopeSize(root);
            }
            return size;
        }

        public int BreakScopeSize(CodeRoot root)
        {
            int size = ScopeSize(root);
            if (Parent != null && !Parent._breakScope)
            {
                size += Parent.BreakScopeSize(root);
            }
            return size;
        }
    }
}

