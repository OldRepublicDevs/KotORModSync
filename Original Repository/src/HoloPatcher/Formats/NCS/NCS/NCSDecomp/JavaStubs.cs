using System;
using System.Collections;
using System.Collections.Generic;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    // Extension methods for Java-style APIs
    public static class JavaExtensions
    {
        public static void PrintStackTrace(this Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
        public static void PrintStackTrace(this Exception ex, object output)
        {
            Console.WriteLine(ex.ToString());
        }
        public static string GetMessage(this Exception ex) => ex.Message;
        public static string GetStackTrace(this Exception ex) => ex.StackTrace ?? "";
    }

    // System class stub for Java System methods (deprecated - use DecompilerLogger instead)
    // Kept for backward compatibility with code that hasn't been migrated yet
    [Obsolete("Use DecompilerLogger instead of JavaSystem.@out.Println")]
    public static class JavaSystem
    {
        public static void Exit(int code)
        {
            Environment.Exit(code);
        }
        public static string GetProperty(string key)
        {
            if (key == "user.dir")
                return Environment.CurrentDirectory;
            if (key == "line.separator")
                return Environment.NewLine;
            if (key == "NCSDecomp.debug.stack")
                return Environment.GetEnvironmentVariable("NCSDecomp_DEBUG_STACK") ?? "";
            return "";
        }

        [Obsolete("Use DecompilerLogger.Debug or DecompilerLogger.Info instead")]
        public static JavaPrintStream @out = new JavaPrintStream();

        [Obsolete("Use DecompilerLogger.Error instead")]
        public static JavaPrintStream @err = new JavaPrintStream();
    }

    [Obsolete("Use DecompilerLogger instead")]
    public class JavaPrintStream
    {
        public void Println(string message)
        {
            DecompilerLogger.Debug(message);
        }
        public void Println(object obj)
        {
            DecompilerLogger.Debug(obj?.ToString() ?? "null");
        }
        public void Print(string message)
        {
            DecompilerLogger.Debug(message);
        }
        public void Println()
        {
            DecompilerLogger.Debug("");
        }
    }

    // Java file I/O classes
    public class NcsFile
    {
        private System.IO.FileInfo _fileInfo;
        public NcsFile(string path)
        {
            _fileInfo = new System.IO.FileInfo(path);
        }
        public NcsFile(NcsFile parent, string child)
        {
            _fileInfo = new System.IO.FileInfo(System.IO.Path.Combine(parent.GetAbsolutePath(), child));
        }
        public string GetAbsolutePath() { return _fileInfo.FullName; }
        public string GetCanonicalPath() { return _fileInfo.FullName; }
        public bool RenameTo(NcsFile dest)
        {
            try
            {
                System.IO.File.Move(_fileInfo.FullName, dest.GetAbsolutePath());
                return true;
            }
            catch { return false; }
        }
        public override string ToString() { return _fileInfo.FullName; }
        public string Name { get { return _fileInfo.Name; } }
        public string FullName { get { return _fileInfo.FullName; } }
        public long Length { get { return _fileInfo.Exists ? _fileInfo.Length : 0; } }
        public System.DateTime LastWriteTime { get { return _fileInfo.Exists ? _fileInfo.LastWriteTime : System.DateTime.MinValue; } }
        public System.IO.DirectoryInfo Directory { get { return _fileInfo.Directory; } }
        public string DirectoryName { get { return _fileInfo.DirectoryName; } }
        public bool IsFile() { return _fileInfo.Exists && !_fileInfo.Attributes.HasFlag(System.IO.FileAttributes.Directory); }
        public bool IsDirectory() { return _fileInfo.Attributes.HasFlag(System.IO.FileAttributes.Directory); }
        public void Delete() { _fileInfo.Delete(); }
        public bool Exists()
        {
            // Refresh FileInfo to ensure we get the current state from the file system
            // This is important after file operations like DecompileToFile
            _fileInfo.Refresh();
            return _fileInfo.Exists;
        }
        public bool Create()
        {
            try
            {
                if (!_fileInfo.Exists)
                {
                    System.IO.File.Create(_fileInfo.FullName).Close();
                    _fileInfo.Refresh();
                    return true;
                }
                return false;
            }
            catch (System.IO.IOException)
            {
                throw;
            }
            catch (System.Exception ex)
            {
                throw new IOException(ex.Message, ex);
            }
        }
        public NcsFile GetAbsoluteFile() { return new NcsFile(_fileInfo.FullName); }
        public NcsFile GetParentFile()
        {
            if (_fileInfo.Directory != null)
            {
                return new NcsFile(_fileInfo.Directory);
            }
            return null;
        }
        public bool Mkdirs()
        {
            try
            {
                // Create the directory and all parent directories (matching Java File.mkdirs() behavior)
                if (_fileInfo.Directory != null && !_fileInfo.Directory.Exists)
                {
                    System.IO.Directory.CreateDirectory(_fileInfo.Directory.FullName);
                }
                // Also create the file/directory itself if it's a directory path
                if (!_fileInfo.Exists && _fileInfo.Directory != null)
                {
                    System.IO.Directory.CreateDirectory(_fileInfo.Directory.FullName);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
        public NcsFile(System.IO.DirectoryInfo dirInfo)
        {
            if (dirInfo == null)
                throw new System.ArgumentNullException(nameof(dirInfo));
            _fileInfo = new System.IO.FileInfo(dirInfo.FullName);
        }
    }

    public class FileInputStream : System.IO.FileStream
    {
        public FileInputStream(string path) : base(path, System.IO.FileMode.Open) { }
        public FileInputStream(NcsFile file) : base(file.GetAbsolutePath(), System.IO.FileMode.Open) { }
    }

    public class FileOutputStream : System.IO.FileStream
    {
        public FileOutputStream(string path) : base(path, System.IO.FileMode.Create) { }
        public FileOutputStream(NcsFile file) : base(file.GetAbsolutePath(), System.IO.FileMode.Create) { }
    }

    public class ByteArrayInputStream : System.IO.MemoryStream
    {
        public ByteArrayInputStream(byte[] buffer) : base(buffer) { }
    }

    // Extension methods for BinaryReader (Java-style mark/reset)
    public static class BinaryReaderExtensions
    {
        private static System.Collections.Generic.Dictionary<System.IO.BinaryReader, long> _marks =
            new System.Collections.Generic.Dictionary<System.IO.BinaryReader, long>();

        public static void Mark(this System.IO.BinaryReader reader, int readLimit)
        {
            _marks[reader] = reader.BaseStream.Position;
        }

        public static void Reset(this System.IO.BinaryReader reader)
        {
            if (_marks.TryGetValue(reader, out long position))
            {
                reader.BaseStream.Position = position;
            }
        }
    }

    public class FileReader : System.IO.StreamReader
    {
        public FileReader(NcsFile file) : base(file.GetAbsolutePath()) { }
        public FileReader(string path) : base(path) { }
    }

    public class FileWriter : System.IO.StreamWriter
    {
        public FileWriter(string path) : base(path) { }
        public FileWriter(NcsFile file) : base(file.GetAbsolutePath()) { }
    }

    public class BufferedReader : System.IO.StreamReader
    {
        private System.IO.StreamReader _reader;
        public BufferedReader(FileReader reader) : base(reader.BaseStream)
        {
            _reader = reader;
        }
        public BufferedReader(System.IO.StreamReader reader) : base(reader.BaseStream)
        {
            _reader = reader;
        }
        public new string ReadLine()
        {
            return _reader.ReadLine();
        }
    }

    public class BufferedWriter : System.IO.StreamWriter
    {
        public BufferedWriter(FileWriter writer) : base(writer.BaseStream) { }
        public BufferedWriter(System.IO.StreamWriter writer) : base(writer.BaseStream) { }
        public new void Write(string text)
        {
            base.Write(text);
        }
    }

    // Java exception types
    public class Throwable : Exception
    {
        public Throwable() : base() { }
        public Throwable(string message) : base(message) { }
        public Throwable(string message, Exception inner) : base(message, inner) { }
    }

    public class IOException : Exception
    {
        public IOException() : base() { }
        public IOException(string message) : base(message) { }
        public IOException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class FileNotFoundException : IOException
    {
        public FileNotFoundException() : base() { }
        public FileNotFoundException(string message) : base(message) { }
    }

    // Java collection classes
    public class HashMap : Dictionary<object, object>
    {
        public HashMap() : base() { }
        public void Put(object key, object value) { this[key] = value; }
        public ICollection<object> KeySet() => this.Keys;
    }

    public class Vector : List<object>
    {
        public Vector() : base() { }
        public Vector(int initialCapacity) : base(initialCapacity) { }
        public Vector(IEnumerable<object> collection) : base(collection) { }
        public IEnumerator<object> Iterator() => CollectionExtensions.Iterator(this);
        public void AddAll(IEnumerable<object> collection)
        {
            foreach (var item in collection)
                this.Add(item);
        }
        public void AddElement(object item) => this.Add(item);
        public void RemoveElement(object item) => this.Remove(item);
        public bool IsEmpty() => this.Count == 0;
    }

    public class TreeSet : SortedSet<object>
    {
        public TreeSet() : base() { }
        public TreeSet(IComparer<object> comparer) : base(comparer) { }
    }

    // Java Properties class
    public class Properties
    {
        private Dictionary<string, string> _properties = new Dictionary<string, string>();

        public string GetProperty(string key)
        {
            return _properties.TryGetValue(key, out var value) ? value : "";
        }
        public string GetProperty(string key, string defaultValue)
        {
            return _properties.TryGetValue(key, out var value) ? value : defaultValue;
        }
        public void SetProperty(string key, string value)
        {
            _properties[key] = value;
        }
        public void Save() { }
        public void Load() { }
        public void Load(System.IO.Stream stream)
        {
            using (var reader = new System.IO.StreamReader(stream))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith("!"))
                        continue;
                    int equalIndex = line.IndexOf('=');
                    if (equalIndex > 0)
                    {
                        string key = line.Substring(0, equalIndex).Trim();
                        string value = line.Substring(equalIndex + 1).Trim();
                        _properties[key] = value;
                    }
                }
            }
        }
        public void Store(System.IO.Stream stream, string comments)
        {
            using (var writer = new System.IO.StreamWriter(stream))
            {
                if (!string.IsNullOrEmpty(comments))
                    writer.WriteLine("# " + comments);
                foreach (var kvp in _properties)
                {
                    writer.WriteLine($"{kvp.Key}={kvp.Value}");
                }
            }
        }
        public void Reset()
        {
            _properties.Clear();
        }
        public ICollection<string> Keys => _properties.Keys;
    }

    // Java regex Pattern and Matcher
    public class Pattern
    {
        private System.Text.RegularExpressions.Regex _regex;
        private Pattern(System.Text.RegularExpressions.Regex regex)
        {
            _regex = regex;
        }
        public static Pattern Compile(string pattern)
        {
            return new Pattern(new System.Text.RegularExpressions.Regex(pattern));
        }
        public Matcher Matcher(string input)
        {
            return new Matcher(_regex, input);
        }
    }

    public class Matcher
    {
        private System.Text.RegularExpressions.Regex _regex;
        private string _input;
        private System.Text.RegularExpressions.Match _match;
        public Matcher(System.Text.RegularExpressions.Regex regex, string input)
        {
            _regex = regex;
            _input = input;
        }
        public bool Matches()
        {
            _match = _regex.Match(_input);
            return _match.Success && _match.Length == _input.Length;
        }
        public string Group(int groupNumber)
        {
            if (_match != null && _match.Success && _match.Groups.Count > groupNumber)
            {
                return _match.Groups[groupNumber].Value;
            }
            return "";
        }
    }

    // Java Integer wrapper class
    public static class Integer
    {
        public static int ParseInt(string s)
        {
            return int.Parse(s);
        }
        public static string ToString(int value)
        {
            return value.ToString();
        }
    }

    // Java Byte wrapper class
    public static class Byte
    {
        public static byte ParseByte(string s)
        {
            return byte.Parse(s);
        }
    }

    // Java Long wrapper class
    public static class Long
    {
        public static long ParseLong(string s, int radix = 10)
        {
            return Convert.ToInt64(s, radix);
        }

        public static string ToHexString(long value)
        {
            return value.ToString("X");
        }
    }

    // Java BigInteger wrapper class
    public class BigInteger
    {
        private System.Numerics.BigInteger _value;

        public BigInteger(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                _value = System.Numerics.BigInteger.Zero;
                return;
            }

            // Handle sign extension for Java's signed BigInteger
            bool isNegative = (bytes[0] & 0x80) != 0;
            if (isNegative)
            {
                byte[] extended = new byte[bytes.Length + 1];
                extended[0] = 0xFF;
                Array.Copy(bytes, 0, extended, 1, bytes.Length);
                _value = new System.Numerics.BigInteger(extended);
            }
            else
            {
                _value = new System.Numerics.BigInteger(bytes);
            }
        }

        public BigInteger(long value)
        {
            _value = new System.Numerics.BigInteger(value);
        }

        public int IntValue()
        {
            return (int)_value;
        }

        public long LongValue()
        {
            return (long)_value;
        }

        public override string ToString()
        {
            return _value.ToString();
        }
    }

    // Java Float wrapper class
    public static class Float
    {
        public static float IntBitsToFloat(int bits)
        {
            byte[] bytes = BitConverter.GetBytes(bits);
            return BitConverter.ToSingle(bytes, 0);
        }

        public static int FloatToIntBits(float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            return BitConverter.ToInt32(bytes, 0);
        }
    }

    // Extension methods for Java-style collections
    public static class CollectionExtensions
    {
        public static IEnumerator<object> Iterator(this List<object> list)
        {
            return new ListEnumeratorAdapter(list);
        }

        public static IEnumerator<object> Iterator(this System.Collections.Generic.LinkedList<object> list)
        {
            return new LinkedListEnumeratorAdapter(list);
        }

        private class ListEnumeratorAdapter : IEnumerator<object>
        {
            private readonly List<object> _list;
            private int _index;

            public ListEnumeratorAdapter(List<object> list)
            {
                _list = list;
                _index = 0;
            }

            public bool HasNext()
            {
                return _index < _list.Count;
            }

            public object Next()
            {
                if (!HasNext())
                    throw new InvalidOperationException("No next element");
                return _list[_index++];
            }

            // IEnumerator<object> implementation
            public object Current => _list[_index - 1];
            object IEnumerator.Current => Current;
            public bool MoveNext() => HasNext() ? (Next() != null || true) : false;
            public void Reset() { _index = 0; }
            public void Dispose() { }
        }

        private class LinkedListEnumeratorAdapter : IEnumerator<object>
        {
            private readonly System.Collections.Generic.LinkedList<object> _list;
            private System.Collections.Generic.LinkedListNode<object> _current;
            private System.Collections.Generic.LinkedListNode<object> _started;

            public LinkedListEnumeratorAdapter(System.Collections.Generic.LinkedList<object> list)
            {
                _list = list;
                _current = list.First;
                _started = null;
            }

            public bool HasNext()
            {
                return _current != null;
            }

            public object Next()
            {
                if (!HasNext())
                    throw new InvalidOperationException("No next element");
                _started = _current;
                object value = _current.Value;
                _current = _current.Next;
                return value;
            }

            // IEnumerator<object> implementation
            public object Current => _started?.Value;
            object IEnumerator.Current => Current;
            public bool MoveNext()
            {
                if (!HasNext()) return false;
                Next();
                return true;
            }
            public void Reset() { _current = _list.First; _started = null; }
            public void Dispose() { }
        }
    }

}




