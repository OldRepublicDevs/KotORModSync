using System;
using System.Linq;
using JetBrains.Annotations;

namespace KOTORModSync.Resource
{

    /// <summary>
    /// Class for storing resource name and type, facilitating case-insensitive object comparisons
    /// and hashing equal to their string representations.
    /// </summary>
    public class ResourceIdentifier : IEquatable<ResourceIdentifier>
    {
        public string ResName { get; }
        public ResourceType ResType { get; }

        private readonly string _cachedFilenameStr;
        private readonly string _lowerResName;

        public ResourceIdentifier(string resName, ResourceType resType)
        {
            ResName = resName;
            ResType = resType;

            string ext = resType.Extension;
            string suffix = string.IsNullOrEmpty(ext) ? "" : $".{ext}";
            _cachedFilenameStr = $"{resName}{suffix}".ToLower();
            _lowerResName = resName.ToLower();
        }

        public static ResourceIdentifier FromPath(string path)
        {
            string filename = System.IO.Path.GetFileName(path);
            string lowerFilename = filename.ToLowerInvariant();

            // Find the best matching ResourceType by trying to match extension
            // [CanBeNull] Since ResourceType is a class with static fields, we try the most common ones first
            ResourceType chosenResType = null;
            int chosenSuffixLength = 0;

            // Get all known resource types via reflection
            System.Collections.Generic.IEnumerable<ResourceType> knownTypes = typeof(ResourceType).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(f => f.FieldType == typeof(ResourceType))
                .Select(f => (ResourceType)f.GetValue(null))
                .Where(rt => rt != null && !rt.IsInvalid);

            foreach (ResourceType candidate in knownTypes)
            {
                string extension = candidate.Extension;
                if (string.IsNullOrEmpty(extension))
                {
                    continue;
                }

                string suffix = $".{extension}";
                if (lowerFilename.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && suffix.Length > chosenSuffixLength)
                {
                    chosenResType = candidate;
                    chosenSuffixLength = suffix.Length;
                }
            }

            if (chosenResType != null && chosenSuffixLength > 0)
            {
                string resname = filename.Substring(0, filename.Length - chosenSuffixLength);
                return new ResourceIdentifier(resname, chosenResType);
            }

            // Fallback: use Path methods
            string stem = System.IO.Path.GetFileNameWithoutExtension(path);
            string ext = System.IO.Path.GetExtension(path);
            ResourceType restype = ResourceType.FromExtension(ext);
            return new ResourceIdentifier(stem, restype);
        }

        public ResourceIdentifier Validate()
        {
            if (ResType == ResourceType.INVALID || ResType.IsInvalid)
            {
                throw new InvalidOperationException($"Invalid resource: '{this}'");
            }
            return this;
        }

        public (string, ResourceType) Unpack() => (ResName, ResType);

        public string LowerResName => _lowerResName;

        public override string ToString() => _cachedFilenameStr;

        public override int GetHashCode() => _cachedFilenameStr.GetHashCode();

        public override bool Equals([CanBeNull] object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj is ResourceIdentifier other)
            {
                return Equals(other);
            }

            if (obj is string str)
            {
                return _cachedFilenameStr == str.ToLower();
            }

            return false;
        }

        public bool Equals([CanBeNull] ResourceIdentifier other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return _cachedFilenameStr == other._cachedFilenameStr;
        }

        public static bool operator ==([CanBeNull] ResourceIdentifier left, [CanBeNull] ResourceIdentifier right)
        {
            if (left is null)
            {
                return right is null;
            }

            return left.Equals(right);
        }

        public static bool operator !=([CanBeNull] ResourceIdentifier left, [CanBeNull] ResourceIdentifier right)
        {
            return !(left == right);
        }
    }
}

