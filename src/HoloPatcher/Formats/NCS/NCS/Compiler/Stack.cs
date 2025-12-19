using System;
using System.Collections.Generic;
using System.Linq;
using KOTORModSync;
using KOTORModSync.Common.Script;
using KOTORModSync.Formats.NCS;
using JetBrains.Annotations;

namespace KOTORModSync.Formats.NCS.Compiler
{

    /// <summary>
    /// Stack implementation for NCS bytecode execution.
    /// </summary>
    public class Stack
    {
        private readonly List<StackObject> _stack = new List<StackObject>();
        // These fields are intentionally mutable and will be used by save_bp/restore_bp methods (to be implemented)
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        // ReSharper disable once UnusedMember.Local
        private int _bp; // Mutable - modified by save_bp/restore_bp
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        // ReSharper disable once UnusedMember.Local
        private int _globalBp; // Mutable - set in save_bp when entering main
        private readonly List<int> _bpBuffer = new List<int>();

        /// <summary>
        /// Get a copy of the current stack state.
        /// </summary>
        public List<StackObject> State()
        {
            return _stack.ToList();
        }

        /// <summary>
        /// Add a value to the stack.
        /// </summary>
        public void Add(DataType dataType, [CanBeNull] object value)
        {
            // Ensure proper type boxing: if DataType is Float but value is double, convert to float
            // This prevents issues when unboxing later (can't unbox double to float directly)
            if (dataType == DataType.Float && value is double d)
            {
                value = (float)d;
            }
            else if (dataType == DataType.Int && value is double di)
            {
                value = (int)di;
            }
            _stack.Add(new StackObject(dataType, value));
        }

        /// <summary>
        /// Get the stack pointer (size in bytes, assuming 4 bytes per element).
        /// </summary>
        public int StackPointer()
        {
            return _stack.Count * 4;
        }

        /// <summary>
        /// Get the base pointer.
        /// </summary>
        public int BasePointer()
        {
            return _bp;
        }

        /// <summary>
        /// Peek at a value at the specified offset from the top of the stack.
        /// Returns the StackObject at that position.
        /// </summary>
        public StackObject Peek(int offset)
        {
            int realIndex = StackIndex(offset);
            return _stack[realIndex];
        }

        /// <summary>
        /// Move the stack pointer by offset (shrink or grow stack).
        /// </summary>
        public void Move(int offset)
        {
            if (offset == 0)
            {
                return;
            }

            if (offset > 0)
            {
                if (offset % 4 != 0)
                {
                    throw new ArgumentException($"Stack growth offset must be multiple of 4, got {offset}");
                }
                int words = offset / 4;
                for (int i = 0; i < words; i++)
                {
                    _stack.Add(new StackObject(DataType.Int, 0));
                }
                return;
            }

            // For negative offset (shrinking stack), handle empty stack as no-op
            if (_stack.Count == 0)
            {
                return;
            }

            int removeTo = StackIndex(offset);
            _stack.RemoveRange(removeTo, _stack.Count - removeTo);
        }

        /// <summary>
        /// Copy values from the top of the stack down to the specified offset.
        /// </summary>
        public void CopyDown(int offset, int size)
        {
            if (size % 4 != 0)
            {
                throw new ArgumentException("Size must be divisible by 4");
            }

            int numElements = size / 4;

            if (numElements > _stack.Count)
            {
                throw new IndexOutOfRangeException("Size exceeds the current stack size");
            }

            // Find the target indices first
            var targetIndices = new List<int>();
            int tempOffset = offset;

            for (int i = 0; i < numElements; i++)
            {
                int targetIndex = StackIndex(tempOffset);
                targetIndices.Add(targetIndex);
                tempOffset += 4; // Move to the next position
            }

            // Now copy the elements down the stack
            for (int i = 0; i < numElements; i++)
            {
                int sourceIndex = _stack.Count - 1 - i; // Counting from the end of the list
                int targetIndex = targetIndices[numElements - 1 - i]; // The last target index corresponds to the first source index
                _stack[targetIndex] = new StackObject(_stack[sourceIndex].DataType, _stack[sourceIndex].Value);
            }
        }

        /// <summary>
        /// Copy values from the stack to the top.
        /// </summary>
        public void CopyToTop(int offset, int size)
        {
            if (size <= 0 || size % 4 != 0)
            {
                throw new ArgumentException($"Size must be a positive multiple of 4, got {size}");
            }
            if (offset == 0 || offset % 4 != 0)
            {
                throw new ArgumentException($"Offset must be a non-zero multiple of 4, got {offset}");
            }

            // CPTOPSP can reference beyond current stack when accessing globals via base pointer
            // So we need a more flexible approach:
            // 1. If stack is empty, we can't copy anything
            // 2. Try to copy from the specified offset, but don't fail if offset > stack size
            //    as this might be accessing globals via BP

            if (_stack.Count == 0)
            {
                // Empty stack - check if this is trying to access globals via BP
                // In that case, we should just push default values
                int words = size / 4;
                for (int i = 0; i < words; i++)
                {
                    _stack.Add(new StackObject(DataType.Int, 0));
                }
                return;
            }

            int offsetAbs = Math.Abs(offset);
            int totalBytes = _stack.Sum(obj => obj.DataType.Size());

            // If offset exceeds stack size, this might be accessing uninit globals - push defaults
            if (offsetAbs > totalBytes)
            {
                int words = size / 4;
                for (int i = 0; i < words; i++)
                {
                    _stack.Add(new StackObject(DataType.Int, 0));
                }
                return;
            }

            int lowerBound = offsetAbs - size;
            var copied = new List<StackObject>();
            int accumulated = 0;

            for (int index = _stack.Count - 1; index >= 0; index--)
            {
                StackObject element = _stack[index];
                accumulated += element.DataType.Size();
                if (accumulated <= lowerBound)
                {
                    continue;
                }
                if (accumulated > offsetAbs)
                {
                    break;
                }
                copied.Add(new StackObject(element.DataType, element.Value));
            }

            if (copied.Count == 0 || copied.Sum(obj => obj.DataType.Size()) != size)
            {
                // If we can't copy the exact block, try to gracefully handle it
                int words = size / 4;
                for (int i = 0; i < words; i++)
                {
                    _stack.Add(new StackObject(DataType.Int, 0));
                }
                return;
            }

            copied.Reverse();
            _stack.AddRange(copied);
        }

        /// <summary>
        /// Pop and return the top stack element.
        /// </summary>
        public StackObject Pop()
        {
            if (_stack.Count == 0)
            {
                throw new IndexOutOfRangeException("Cannot pop from empty stack");
            }
            StackObject top = _stack[_stack.Count - 1];
            _stack.RemoveAt(_stack.Count - 1);
            return top;
        }

        /// <summary>
        /// Save the base pointer.
        /// </summary>
        public void SaveBp()
        {
            int prevBp = _bp;
            int newBp = StackPointer();
            _stack.Add(new StackObject(DataType.Int, prevBp));
            _bpBuffer.Add(prevBp);
            if (_globalBp == 0 && prevBp == 0)
            {
                _globalBp = newBp;
            }
            _bp = newBp;
        }

        /// <summary>
        /// Restore the base pointer.
        /// </summary>
        public void RestoreBp()
        {
            if (_stack.Count == 0)
            {
                throw new IndexOutOfRangeException("Cannot restore base pointer from an empty stack");
            }
            StackObject savedBp = _stack[_stack.Count - 1];
            _stack.RemoveAt(_stack.Count - 1);
            if (savedBp.DataType != DataType.Int)
            {
                throw new InvalidOperationException(
                    $"Encountered non-integer value while restoring base pointer; found {savedBp.DataType}");
            }
            if (!(savedBp.Value is int))
            {
                throw new InvalidOperationException(
                    $"Base pointer restore requires integer value but received {savedBp.Value?.GetType()}");
            }
            _bp = (int)savedBp.Value;
            if (_bpBuffer.Count > 0)
            {
                _bpBuffer.RemoveAt(_bpBuffer.Count - 1);
            }
        }

        /// <summary>
        /// Increment value at stack offset.
        /// </summary>
        public void Increment(int offset)
        {
            int index = StackIndex(offset);
            StackObject element = _stack[index];
            StackObject newValue = new StackObject(element.DataType, element.Value);
            if (newValue.Value is int intVal)
            {
                newValue.Value = intVal + 1;
            }
            else if (newValue.Value is float floatVal)
            {
                newValue.Value = floatVal + 1.0f;
            }
            else
            {
                throw new InvalidOperationException($"Cannot increment non-numeric type {newValue.DataType}");
            }
            _stack[index] = newValue;
        }

        /// <summary>
        /// Decrement value at stack offset.
        /// </summary>
        public void Decrement(int offset)
        {
            int index = StackIndex(offset);
            StackObject element = _stack[index];
            StackObject newValue = new StackObject(element.DataType, element.Value);
            if (newValue.Value is int intVal)
            {
                newValue.Value = intVal - 1;
            }
            else if (newValue.Value is float floatVal)
            {
                newValue.Value = floatVal - 1.0f;
            }
            else
            {
                throw new InvalidOperationException($"Cannot decrement non-numeric type {newValue.DataType}");
            }
            _stack[index] = newValue;
        }

        /// <summary>
        /// Convert a base-pointer-relative offset to an actual list index.
        /// </summary>
        private int StackIndexBp(int offset)
        {
            if (offset >= 0)
            {
                throw new ArgumentException($"BP-relative offset must be negative, got {offset}");
            }

            int bpIndex = _bp / 4;
            int relativeIndex = Math.Abs(offset) / 4;
            int absoluteIndex = bpIndex - relativeIndex;

            if (absoluteIndex < 0 || absoluteIndex >= _stack.Count)
            {
                throw new ArgumentException($"BP-relative offset {offset} results in invalid index {absoluteIndex}");
            }

            return absoluteIndex;
        }

        /// <summary>
        /// Increment value at base-pointer-relative offset.
        /// </summary>
        public void IncrementBp(int offset)
        {
            int index = StackIndexBp(offset);
            StackObject element = _stack[index];
            StackObject newValue = new StackObject(element.DataType, element.Value);
            if (newValue.Value is int intVal)
            {
                newValue.Value = intVal + 1;
            }
            else if (newValue.Value is float floatVal)
            {
                newValue.Value = floatVal + 1.0f;
            }
            else
            {
                throw new InvalidOperationException($"Cannot increment non-numeric type {newValue.DataType}");
            }
            _stack[index] = newValue;
        }

        /// <summary>
        /// Decrement value at base-pointer-relative offset.
        /// </summary>
        public void DecrementBp(int offset)
        {
            int index = StackIndexBp(offset);
            StackObject element = _stack[index];
            StackObject newValue = new StackObject(element.DataType, element.Value);
            if (newValue.Value is int intVal)
            {
                newValue.Value = intVal - 1;
            }
            else if (newValue.Value is float floatVal)
            {
                newValue.Value = floatVal - 1.0f;
            }
            else
            {
                throw new InvalidOperationException($"Cannot decrement non-numeric type {newValue.DataType}");
            }
            _stack[index] = newValue;
        }

        /// <summary>
        /// Copy value from base-pointer-relative location to stack top.
        /// </summary>
        public void CopyTopBp(int offset, int size)
        {
            if (size % 4 != 0)
            {
                throw new ArgumentException($"Size must be multiple of 4, got {size}");
            }

            int copyIndex = StackIndexBp(offset);
            StackObject topValue = _stack[copyIndex];
            _stack.Add(new StackObject(topValue.DataType, topValue.Value));
        }

        /// <summary>
        /// Copy value from stack top down to base-pointer-relative location.
        /// </summary>
        public void CopyDownBp(int offset, int size)
        {
            if (_stack.Count == 0)
            {
                throw new IndexOutOfRangeException("Cannot copy from empty stack");
            }

            if (size % 4 != 0)
            {
                throw new ArgumentException($"Size must be multiple of 4, got {size}");
            }

            StackObject topValue = _stack[_stack.Count - 1];
            int toIndex = StackIndexBp(offset);
            _stack[toIndex] = new StackObject(topValue.DataType, topValue.Value);
        }

        /// <summary>
        /// Perform addition operation on top two stack values.
        /// </summary>
        public void AdditionOp(NCSInstructionType instructionType = NCSInstructionType.ADDII)
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/interpreter.py:1427
            // Original: def addition_op(self, instruction_type: NCSInstructionType | None = None):
            // Handle vector addition (ADDVV)
            if (instructionType == NCSInstructionType.ADDVV)
            {
                // Matching PyKotor interpreter.py lines 1430-1445
                // Original: Pop vectors (each is 3 floats: z, y, x from top to bottom)
                if (_stack.Count < 6)
                {
                    throw new IndexOutOfRangeException("Stack underflow in vector addition operation");
                }
                // Pop vectors (each is 3 floats: z, y, x from top to bottom)
                float z2 = Convert.ToSingle(_stack[_stack.Count - 1].Value);
                float y2 = Convert.ToSingle(_stack[_stack.Count - 2].Value);
                float x2 = Convert.ToSingle(_stack[_stack.Count - 3].Value);
                float z1 = Convert.ToSingle(_stack[_stack.Count - 4].Value);
                float y1 = Convert.ToSingle(_stack[_stack.Count - 5].Value);
                float x1 = Convert.ToSingle(_stack[_stack.Count - 6].Value);
                _stack.RemoveRange(_stack.Count - 6, 6);
                // Add component-wise and push result (x, y, z order)
                // Original: self.add(DataType.FLOAT, x1 + x2)
                Add(DataType.Float, x1 + x2);
                Add(DataType.Float, y1 + y2);
                Add(DataType.Float, z1 + z2);
                return;
            }

            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in addition operation");
            }
            StackObject value1 = _stack[_stack.Count - 1];
            StackObject value2 = _stack[_stack.Count - 2];
            if (value1.Value is int || value1.Value is float || value1.Value is double
                || value2.Value is int || value2.Value is float || value2.Value is double)
            {
                _stack.RemoveAt(_stack.Count - 1);
                _stack.RemoveAt(_stack.Count - 1);
                // Matching PyKotor interpreter.py lines 1460-1468
                // Result type is determined by the data type of the second operand (value2)
                if (value2.DataType == DataType.Int)
                {
                    double result = Convert.ToDouble(value2.Value) + Convert.ToDouble(value1.Value);
                    Add(DataType.Int, (int)result);
                }
                else if (value2.DataType == DataType.Float)
                {
                    double result = Convert.ToDouble(value2.Value) + Convert.ToDouble(value1.Value);
                    Add(DataType.Float, (float)result);
                }
                else
                {
                    // Fallback: determine type from result value
                    double result = Convert.ToDouble(value2.Value) + Convert.ToDouble(value1.Value);
                    Add(result % 1.0 < double.Epsilon ? DataType.Int : DataType.Float, result);
                }
                return;
            }
            if (value1.Value is string && value2.Value is string)
            {
                string result = (string)value2.Value + (string)value1.Value;
                _stack.RemoveAt(_stack.Count - 1);
                _stack.RemoveAt(_stack.Count - 1);
                Add(DataType.String, result);
                return;
            }
            throw new InvalidOperationException(
                $"Addition requires numeric or string operands; got {value2.DataType} and {value1.DataType}");
        }

        /// <summary>
        /// Perform subtraction operation on top two stack values.
        /// </summary>
        public void SubtractionOp(NCSInstructionType instructionType = NCSInstructionType.SUBII)
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/interpreter.py:1482
            // Original: def subtraction_op(self, instruction_type: NCSInstructionType | None = None):
            // Handle vector subtraction (SUBVV)
            if (instructionType == NCSInstructionType.SUBVV)
            {
                // Matching PyKotor interpreter.py lines 1485-1500
                if (_stack.Count < 6)
                {
                    throw new IndexOutOfRangeException("Stack underflow in vector subtraction operation");
                }
                // Pop vectors (each is 3 floats: z, y, x from top to bottom)
                float z2 = Convert.ToSingle(_stack[_stack.Count - 1].Value);
                float y2 = Convert.ToSingle(_stack[_stack.Count - 2].Value);
                float x2 = Convert.ToSingle(_stack[_stack.Count - 3].Value);
                float z1 = Convert.ToSingle(_stack[_stack.Count - 4].Value);
                float y1 = Convert.ToSingle(_stack[_stack.Count - 5].Value);
                float x1 = Convert.ToSingle(_stack[_stack.Count - 6].Value);
                _stack.RemoveRange(_stack.Count - 6, 6);
                // Subtract component-wise (v1 - v2) and push result (x, y, z order)
                Add(DataType.Float, x1 - x2);
                Add(DataType.Float, y1 - y2);
                Add(DataType.Float, z1 - z2);
                return;
            }

            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in subtraction operation");
            }
            StackObject value1 = _stack[_stack.Count - 1];
            StackObject value2 = _stack[_stack.Count - 2];
            // Accept int, float, and double as numeric types
            bool value1IsNumeric = value1.Value is int || value1.Value is float || value1.Value is double;
            bool value2IsNumeric = value2.Value is int || value2.Value is float || value2.Value is double;
            if (!value1IsNumeric || !value2IsNumeric)
            {
                throw new InvalidOperationException("Subtraction requires numeric operands");
            }
            // Matching PyKotor interpreter.py line 1517
            // Result type is determined by the data type of the second operand (value2)
            double result = Convert.ToDouble(value2.Value) - Convert.ToDouble(value1.Value);
            _stack.RemoveAt(_stack.Count - 1);
            _stack.RemoveAt(_stack.Count - 1);
            if (value2.DataType == DataType.Int)
            {
                Add(DataType.Int, (int)result);
            }
            else if (value2.DataType == DataType.Float)
            {
                Add(DataType.Float, (float)result);
            }
            else
            {
                // Fallback: determine type from result value
                Add(Math.Abs(result % 1) < double.Epsilon ? DataType.Int : DataType.Float, result);
            }
        }

        /// <summary>
        /// Perform multiplication operation on top two stack values.
        /// </summary>
        public void MultiplicationOp(NCSInstructionType instructionType = NCSInstructionType.MULII)
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/interpreter.py:1519
            // Original: def multiplication_op(self, instruction_type: NCSInstructionType | None = None):
            // Handle vector multiplication
            if (instructionType == NCSInstructionType.MULVF)
            {
                // Matching PyKotor interpreter.py lines 1522-1536
                // MULVF: vector * float (vector is lhs, float is rhs, so float is on top)
                if (_stack.Count < 4)
                {
                    throw new IndexOutOfRangeException("Stack underflow in vector multiplication operation");
                }
                // Stack: [x, y, z, scalar] with scalar on top
                float scalar = Convert.ToSingle(_stack[_stack.Count - 1].Value);
                float z = Convert.ToSingle(_stack[_stack.Count - 2].Value);
                float y = Convert.ToSingle(_stack[_stack.Count - 3].Value);
                float x = Convert.ToSingle(_stack[_stack.Count - 4].Value);
                _stack.RemoveRange(_stack.Count - 4, 4);
                // Multiply component-wise and push result (x, y, z order)
                Add(DataType.Float, x * scalar);
                Add(DataType.Float, y * scalar);
                Add(DataType.Float, z * scalar);
                return;
            }
            else if (instructionType == NCSInstructionType.MULFV)
            {
                // Matching PyKotor interpreter.py lines 1537-1551
                // MULFV: float * vector (float is lhs, vector is rhs, so vector is on top)
                if (_stack.Count < 4)
                {
                    throw new IndexOutOfRangeException("Stack underflow in vector multiplication operation");
                }
                // Stack: [scalar, x, y, z] with z on top
                float z = Convert.ToSingle(_stack[_stack.Count - 1].Value);
                float y = Convert.ToSingle(_stack[_stack.Count - 2].Value);
                float x = Convert.ToSingle(_stack[_stack.Count - 3].Value);
                float scalar = Convert.ToSingle(_stack[_stack.Count - 4].Value);
                _stack.RemoveRange(_stack.Count - 4, 4);
                // Multiply component-wise and push result (x, y, z order)
                Add(DataType.Float, x * scalar);
                Add(DataType.Float, y * scalar);
                Add(DataType.Float, z * scalar);
                return;
            }

            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in multiplication operation");
            }
            StackObject value1 = _stack[_stack.Count - 1];
            StackObject value2 = _stack[_stack.Count - 2];
            // Accept int, float, and double as numeric types
            bool value1IsNumeric = value1.Value is int || value1.Value is float || value1.Value is double;
            bool value2IsNumeric = value2.Value is int || value2.Value is float || value2.Value is double;
            if (!value1IsNumeric || !value2IsNumeric)
            {
                throw new InvalidOperationException("Multiplication requires numeric operands");
            }
            // Matching PyKotor interpreter.py line 1568
            // Result type is determined by the data type of the second operand (value2)
            double result = Convert.ToDouble(value2.Value) * Convert.ToDouble(value1.Value);
            _stack.RemoveAt(_stack.Count - 1);
            _stack.RemoveAt(_stack.Count - 1);
            if (value2.DataType == DataType.Int)
            {
                Add(DataType.Int, (int)result);
            }
            else if (value2.DataType == DataType.Float)
            {
                Add(DataType.Float, (float)result);
            }
            else
            {
                // Fallback: determine type from result value
                Add(Math.Abs(result % 1) < double.Epsilon ? DataType.Int : DataType.Float, result);
            }
        }

        /// <summary>
        /// Perform division operation on top two stack values.
        /// </summary>
        public void DivisionOp(NCSInstructionType instructionType = NCSInstructionType.DIVII)
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/interpreter.py:1570
            // Original: def division_op(self, instruction_type: NCSInstructionType | None = None):
            // Handle vector division (DIVVF: vector / float)
            if (instructionType == NCSInstructionType.DIVVF)
            {
                // Matching PyKotor interpreter.py lines 1573-1590
                // DIVVF: vector / float (vector is lhs, float is rhs, so float is on top)
                if (_stack.Count < 4)
                {
                    throw new IndexOutOfRangeException("Stack underflow in vector division operation");
                }
                // Stack: [x, y, z, scalar] with scalar on top
                float scalar = Convert.ToSingle(_stack[_stack.Count - 1].Value);
                if (Math.Abs(scalar) < float.Epsilon)
                {
                    throw new DivideByZeroException("Division by zero in vector division operation");
                }
                float z = Convert.ToSingle(_stack[_stack.Count - 2].Value);
                float y = Convert.ToSingle(_stack[_stack.Count - 3].Value);
                float x = Convert.ToSingle(_stack[_stack.Count - 4].Value);
                _stack.RemoveRange(_stack.Count - 4, 4);
                // Divide component-wise and push result (x, y, z order)
                Add(DataType.Float, x / scalar);
                Add(DataType.Float, y / scalar);
                Add(DataType.Float, z / scalar);
                return;
            }

            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in division operation");
            }
            StackObject value1 = _stack[_stack.Count - 1];
            StackObject value2 = _stack[_stack.Count - 2];
            // Accept int, float, and double as numeric types (double can result from arithmetic operations)
            bool value1IsNumeric = value1.Value is int || value1.Value is float || value1.Value is double;
            bool value2IsNumeric = value2.Value is int || value2.Value is float || value2.Value is double;
            if (!value1IsNumeric || !value2IsNumeric)
            {
                throw new InvalidOperationException(
                    $"Division requires numeric operands. " +
                    $"Top value: type={value1.DataType}, value={value1.Value} (C# type: {value1.Value?.GetType().Name ?? "null"}). " +
                    $"Second value: type={value2.DataType}, value={value2.Value} (C# type: {value2.Value?.GetType().Name ?? "null"})");
            }
            double divisor = Convert.ToDouble(value1.Value);
            if (Math.Abs(divisor) < double.Epsilon)
            {
                throw new DivideByZeroException("Division by zero in NCS interpreter");
            }
            // Matching PyKotor interpreter.py lines 1607-1613
            // Result type is determined by the data type of the second operand (value2)
            double result = Convert.ToDouble(value2.Value) / Convert.ToDouble(value1.Value);
            // For integer division, truncate toward zero
            if (value1.DataType == DataType.Int && value2.DataType == DataType.Int)
            {
                result = result >= 0 ? Math.Floor(result) : Math.Ceiling(result);
            }
            _stack.RemoveAt(_stack.Count - 1);
            _stack.RemoveAt(_stack.Count - 1);
            if (value2.DataType == DataType.Int)
            {
                Add(DataType.Int, (int)result);
            }
            else if (value2.DataType == DataType.Float)
            {
                Add(DataType.Float, (float)result);
            }
            else
            {
                // Fallback: determine type from result value
                Add(Math.Abs(result % 1) < double.Epsilon ? DataType.Int : DataType.Float, result);
            }
        }

        /// <summary>
        /// Perform modulus operation on top two stack values.
        /// </summary>
        public void ModulusOp()
        {
            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in modulus operation");
            }
            StackObject value1 = _stack[_stack.Count - 1];
            StackObject value2 = _stack[_stack.Count - 2];
            if (!(value1.Value is int i1) || !(value2.Value is int i2))
            {
                throw new InvalidOperationException("Modulus operation requires integer operands");
            }
            _stack.RemoveAt(_stack.Count - 1);
            _stack.RemoveAt(_stack.Count - 1);
            Add(DataType.Int, i2 % i1);
        }

        /// <summary>
        /// Perform unary negation on top stack value.
        /// </summary>
        public void NegationOp()
        {
            if (_stack.Count == 0)
            {
                throw new IndexOutOfRangeException("Stack underflow in negation operation");
            }
            StackObject value1 = _stack[_stack.Count - 1];
            // Accept int, float, and double as numeric types
            if (!(value1.Value is int || value1.Value is float || value1.Value is double))
            {
                throw new InvalidOperationException($"Cannot negate non-numeric type {value1.DataType}");
            }
            double result = -Convert.ToDouble(value1.Value);
            _stack.RemoveAt(_stack.Count - 1);
            Add(value1.DataType, result);
        }

        /// <summary>
        /// Perform logical NOT on top stack value.
        /// </summary>
        public void LogicalNotOp()
        {
            if (_stack.Count == 0)
            {
                throw new IndexOutOfRangeException("Stack underflow in logical NOT operation");
            }
            StackObject value1 = Pop();
            int result = value1.Value != null && !IsZeroValue(value1.Value) ? 0 : 1;
            Add(value1.DataType, result);
        }

        /// <summary>
        /// Perform logical AND on top two stack values.
        /// </summary>
        public void LogicalAndOp()
        {
            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in logical AND operation");
            }
            StackObject value1 = Pop();
            StackObject value2 = Pop();
            int result = (!IsZeroValue(value1.Value) && !IsZeroValue(value2.Value)) ? 1 : 0;
            Add(value1.DataType, result);
        }

        /// <summary>
        /// Perform logical OR on top two stack values.
        /// </summary>
        public void LogicalOrOp()
        {
            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in logical OR operation");
            }
            StackObject value1 = Pop();
            StackObject value2 = Pop();
            int result = (!IsZeroValue(value1.Value) || !IsZeroValue(value2.Value)) ? 1 : 0;
            Add(value1.DataType, result);
        }

        /// <summary>
        /// Perform bitwise NOT on top stack value.
        /// </summary>
        public void BitwiseNotOp()
        {
            if (_stack.Count == 0)
            {
                throw new IndexOutOfRangeException("Stack underflow in bitwise NOT operation");
            }
            StackObject value1 = Pop();
            if (!(value1.Value is int))
            {
                throw new InvalidOperationException($"Cannot perform bitwise NOT on non-integer type {value1.DataType}");
            }
            Add(value1.DataType, ~(int)value1.Value);
        }

        /// <summary>
        /// Perform bitwise OR on top two stack values.
        /// </summary>
        public void BitwiseOrOp()
        {
            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in bitwise OR operation");
            }
            StackObject value1 = Pop();
            StackObject value2 = Pop();
            if (!(value1.Value is int) || !(value2.Value is int))
            {
                throw new InvalidOperationException("Bitwise OR requires integer operands");
            }
            Add(value1.DataType, (int)value1.Value | (int)value2.Value);
        }

        /// <summary>
        /// Perform bitwise XOR on top two stack values.
        /// </summary>
        public void BitwiseXorOp()
        {
            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in bitwise XOR operation");
            }
            StackObject value1 = Pop();
            StackObject value2 = Pop();
            if (!(value1.Value is int) || !(value2.Value is int))
            {
                throw new InvalidOperationException("Bitwise XOR requires integer operands");
            }
            Add(value1.DataType, (int)value1.Value ^ (int)value2.Value);
        }

        /// <summary>
        /// Perform bitwise AND on top two stack values.
        /// </summary>
        public void BitwiseAndOp()
        {
            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in bitwise AND operation");
            }
            StackObject value1 = Pop();
            StackObject value2 = Pop();
            if (!(value1.Value is int) || !(value2.Value is int))
            {
                throw new InvalidOperationException("Bitwise AND requires integer operands");
            }
            Add(value1.DataType, (int)value1.Value & (int)value2.Value);
        }

        /// <summary>
        /// Perform bitwise left shift on top two stack values.
        /// </summary>
        public void BitwiseLeftShiftOp()
        {
            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in left shift operation");
            }
            StackObject value1 = Pop();
            StackObject value2 = Pop();
            if (!(value1.Value is int) || !(value2.Value is int))
            {
                throw new InvalidOperationException("Bitwise shift requires integer operands");
            }
            Add(value1.DataType, (int)value2.Value << (int)value1.Value);
        }

        /// <summary>
        /// Perform bitwise right shift on top two stack values.
        /// </summary>
        public void BitwiseRightShiftOp()
        {
            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in right shift operation");
            }
            StackObject value1 = Pop();
            StackObject value2 = Pop();
            if (!(value1.Value is int) || !(value2.Value is int))
            {
                throw new InvalidOperationException("Bitwise shift requires integer operands");
            }
            Add(value1.DataType, (int)value2.Value >> (int)value1.Value);
        }

        /// <summary>
        /// Perform unsigned bitwise right shift on top two stack values.
        /// </summary>
        public void BitwiseUnsignedRightShiftOp()
        {
            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in unsigned right shift operation");
            }
            StackObject value1 = Pop();
            StackObject value2 = Pop();
            if (!(value1.Value is int) || !(value2.Value is int))
            {
                throw new InvalidOperationException("Bitwise shift requires integer operands");
            }
            uint left = unchecked((uint)(int)value2.Value);
            uint shift = unchecked((uint)(int)value1.Value);
            uint result = left >> (int)shift;
            Add(value1.DataType, unchecked((int)result));
        }

        /// <summary>
        /// Perform equality comparison on top two stack values.
        /// </summary>
        public void LogicalEqualityOp()
        {
            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in equality comparison");
            }
            StackObject value1 = Pop();
            StackObject value2 = Pop();
            int result = Equals(value1.Value, value2.Value) ? 1 : 0;
            Add(value1.DataType, result);
        }

        /// <summary>
        /// Perform inequality comparison on top two stack values.
        /// </summary>
        public void LogicalInequalityOp()
        {
            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in inequality comparison");
            }
            StackObject value1 = Pop();
            StackObject value2 = Pop();
            int result = !Equals(value1.Value, value2.Value) ? 1 : 0;
            Add(value1.DataType, result);
        }

        /// <summary>
        /// Perform greater-than comparison on top two stack values.
        /// </summary>
        public void CompareGreaterThanOp()
        {
            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in greater-than comparison");
            }
            StackObject value1 = Pop();
            StackObject value2 = Pop();
            // Accept int, float, and double as numeric types
            bool value1IsNumeric = value1.Value is int || value1.Value is float || value1.Value is double;
            bool value2IsNumeric = value2.Value is int || value2.Value is float || value2.Value is double;
            if (!value1IsNumeric || !value2IsNumeric)
            {
                throw new InvalidOperationException("Comparison requires numeric operands");
            }
            double result = Convert.ToDouble(value2.Value) > Convert.ToDouble(value1.Value) ? 1 : 0;
            Add(value1.DataType, (int)result);
        }

        /// <summary>
        /// Perform greater-than-or-equal comparison on top two stack values.
        /// </summary>
        public void CompareGreaterThanOrEqualOp()
        {
            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in greater-than-or-equal comparison");
            }
            StackObject value1 = Pop();
            StackObject value2 = Pop();
            // Accept int, float, and double as numeric types
            bool value1IsNumeric = value1.Value is int || value1.Value is float || value1.Value is double;
            bool value2IsNumeric = value2.Value is int || value2.Value is float || value2.Value is double;
            if (!value1IsNumeric || !value2IsNumeric)
            {
                throw new InvalidOperationException("Comparison requires numeric operands");
            }
            double result = Convert.ToDouble(value2.Value) >= Convert.ToDouble(value1.Value) ? 1 : 0;
            Add(value1.DataType, (int)result);
        }

        /// <summary>
        /// Perform less-than comparison on top two stack values.
        /// </summary>
        public void CompareLessThanOp()
        {
            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in less-than comparison");
            }
            StackObject value1 = Pop();
            StackObject value2 = Pop();
            // Accept int, float, and double as numeric types
            bool value1IsNumeric = value1.Value is int || value1.Value is float || value1.Value is double;
            bool value2IsNumeric = value2.Value is int || value2.Value is float || value2.Value is double;
            if (!value1IsNumeric || !value2IsNumeric)
            {
                throw new InvalidOperationException("Comparison requires numeric operands");
            }
            double result = Convert.ToDouble(value2.Value) < Convert.ToDouble(value1.Value) ? 1 : 0;
            Add(value1.DataType, (int)result);
        }

        /// <summary>
        /// Perform less-than-or-equal comparison on top two stack values.
        /// </summary>
        public void CompareLessThanOrEqualOp()
        {
            if (_stack.Count < 2)
            {
                throw new IndexOutOfRangeException("Stack underflow in less-than-or-equal comparison");
            }
            StackObject value1 = Pop();
            StackObject value2 = Pop();
            // Accept int, float, and double as numeric types
            bool value1IsNumeric = value1.Value is int || value1.Value is float || value1.Value is double;
            bool value2IsNumeric = value2.Value is int || value2.Value is float || value2.Value is double;
            if (!value1IsNumeric || !value2IsNumeric)
            {
                throw new InvalidOperationException("Comparison requires numeric operands");
            }
            double result = Convert.ToDouble(value2.Value) <= Convert.ToDouble(value1.Value) ? 1 : 0;
            Add(value1.DataType, (int)result);
        }

        /// <summary>
        /// Store state (for action queue).
        /// </summary>
        public void StoreState()
        {
            // Placeholder - full implementation would handle action queue
        }

        private static bool IsZeroValue([CanBeNull] object value)
        {
            if (value == null)
            {
                return true;
            }
            if (value is int i)
            {
                return i == 0;
            }
            if (value is float f)
            {
                return Math.Abs(f) < float.Epsilon;
            }
            if (value is string s)
            {
                return string.IsNullOrEmpty(s);
            }
            return false;
        }

        private int StackIndex(int offset)
        {
            if (offset == 0)
            {
                throw new ArgumentException("Stack offset of zero is not valid");
            }
            if (offset % 4 != 0)
            {
                throw new ArgumentException($"Stack offset must be a multiple of 4 bytes, got {offset}");
            }
            // Allow negative offsets even on empty stack (for MOVSP to shrink empty stack)
            // But for positive offsets or when we need to access elements, stack must not be empty
            if (_stack.Count == 0 && offset > 0)
            {
                throw new ArgumentException("Cannot resolve positive stack offset on an empty stack");
            }
            if (_stack.Count == 0 && offset < 0)
            {
                // For negative offsets on empty stack, return 0 (no elements to remove)
                return 0;
            }

            int remaining = Math.Abs(offset);
            // Python uses negative indexing starting from -1 (top of stack)
            // In C#, we convert negative index to positive: index = _stack.Count + pythonIndex
            // So pythonIndex -1 becomes _stack.Count - 1, pythonIndex -2 becomes _stack.Count - 2, etc.
            int index = -1; // Python-style negative index (will be converted when accessing)

            while (true)
            {
                // Convert Python negative index to C# positive index
                int listIndex = _stack.Count + index;
                if (listIndex < 0 || listIndex >= _stack.Count)
                {
                    // Equivalent to Python's: if -index > len(self._stack)
                    throw new ArgumentException($"Stack offset {offset} is out of range");
                }

                StackObject element = _stack[listIndex];
                int elementSize = element.DataType.Size();
                if (elementSize <= 0)
                {
                    throw new ArgumentException($"Unsupported element size {elementSize} for {element.DataType}");
                }

                if (remaining <= elementSize)
                {
                    // Return the C# positive index
                    return listIndex;
                }

                remaining -= elementSize;
                index -= 1;
            }
        }

    }
}

