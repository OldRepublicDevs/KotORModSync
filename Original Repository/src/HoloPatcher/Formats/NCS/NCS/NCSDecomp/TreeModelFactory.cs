// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/TreeModelFactory.java:17-44
// Original: public class TreeModelFactory extends JTree
// Note: C# implementation uses static methods instead of extending JTree, matching Avalonia UI patterns
// TreeModelFactory for NCS Decompiler - creates tree data structures for UI binding
//
using System;
using System.Collections.Generic;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    /// <summary>
    /// Factory for creating tree model data structures for the subroutines/variables tree.
    /// Returns Dictionary-based data that can be bound to Avalonia TreeView.
    /// </summary>
    public static class TreeModelFactory
    {
        private static readonly Dictionary<object, object> EmptyModel = new Dictionary<object, object>();

        /// <summary>
        /// Creates a tree model from the variable data dictionary.
        /// </summary>
        /// <param name="variableData">Dictionary mapping subroutine names to their variables</param>
        /// <returns>A dictionary-based tree model suitable for UI binding</returns>
        public static object CreateTreeModel(object variableData)
        {
            if (variableData == null)
                return new Dictionary<object, object>();

            // Return the data as-is - it's already a dictionary structure
            return variableData;
        }

        /// <summary>
        /// Gets an empty tree model for when no file is loaded.
        /// </summary>
        public static object GetEmptyModel()
        {
            return EmptyModel;
        }
    }
}




