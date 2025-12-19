namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    /// <summary>
    /// Java-style enumerator interface with HasNext() and Next() methods.
    /// Extends the standard C# IEnumerator interface for compatibility.
    /// </summary>
    public interface IEnumerator<T> : System.Collections.Generic.IEnumerator<T>
    {
        /// <summary>
        /// Returns true if there is another element to enumerate.
        /// </summary>
        bool HasNext();

        /// <summary>
        /// Returns the next element and advances the enumerator.
        /// </summary>
        T Next();
    }
}




