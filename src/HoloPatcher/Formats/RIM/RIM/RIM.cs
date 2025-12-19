using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KOTORModSync;
using KOTORModSync.Resource;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Formats.RIM
{

    /// <summary>
    /// Represents the data of a RIM file.
    /// </summary>
    public class RIM : IEnumerable<RIMResource>
    {
        private readonly List<RIMResource> _resources = new List<RIMResource>();

        public int Count => _resources.Count;

        /// <summary>
        /// Gets a resource by index or resref string.
        /// </summary>
        public RIMResource this[int index] => _resources[index];

        /// <summary>
        /// Gets a resource by resname string.
        /// </summary>
        public RIMResource this[string resname]
        {
            get
            {
                RIMResource resource = _resources.FirstOrDefault(r =>
                    r.ResRef.ToString().Equals(resname, StringComparison.OrdinalIgnoreCase));

                if (resource != null)
                {
                    return new RIMResource(resource.ResRef, resource.ResType, resource.Data);
                }

                throw new KeyNotFoundException($"{resname} not found in RIM");
            }
        }

        public void SetData(string resname, ResourceType restype, byte[] data)
        {
            RIMResource resource = _resources.FirstOrDefault(r =>
                r.ResRef.ToString().Equals(resname, StringComparison.OrdinalIgnoreCase) &&
                r.ResType == restype);

            if (resource == null)
            {
                _resources.Add(new RIMResource(new ResRef(resname), restype, data));
            }
            else
            {
                resource.ResRef = new ResRef(resname);
                resource.ResType = restype;
                resource.Data = data;
            }
        }

        [CanBeNull]
        public byte[] Get(string resname, ResourceType restype)
        {
            RIMResource resource = _resources.FirstOrDefault(r =>
                r.ResRef.ToString().Equals(resname, StringComparison.OrdinalIgnoreCase) &&
                r.ResType == restype);

            return resource?.Data;
        }

        public void Remove(string resname, ResourceType restype)
        {
            _resources.RemoveAll(r =>
                r.ResRef.ToString().Equals(resname, StringComparison.OrdinalIgnoreCase) &&
                r.ResType == restype);
        }

        public ERF.ERF ToErf()
        {
            var erf = new ERF.ERF();
            foreach (RIMResource resource in _resources)
            {
                erf.SetData(resource.ResRef.ToString(), resource.ResType, resource.Data);
            }
            return erf;
        }

        public static RIM operator +(RIM a, RIM b)
        {
            var combined = new RIM();
            foreach (RIMResource resource in a._resources)
            {
                combined.SetData(resource.ResRef.ToString(), resource.ResType, resource.Data);
            }
            foreach (RIMResource resource in b._resources)
            {
                combined.SetData(resource.ResRef.ToString(), resource.ResType, resource.Data);
            }
            return combined;
        }

        public override bool Equals(object obj)
        {
            if (obj is RIM other)
            {
                return _resources.ToHashSet().SetEquals(other._resources);
            }
            if (obj is ERF.ERF erf)
            {
                return _resources.ToHashSet().SetEquals(erf.ToRim()._resources);
            }
            return false;
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (RIMResource resource in _resources.OrderBy(r => r.GetHashCode()))
            {
                hash.Add(resource);
            }
            return hash.ToHashCode();
        }

        public List<RIMResource> GetResources() => _resources.ToList();

        public IEnumerator<RIMResource> GetEnumerator()
        {
            // Return copies like Python does
            foreach (RIMResource resource in _resources)
            {
                yield return new RIMResource(resource.ResRef, resource.ResType, resource.Data);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// Represents a single resource within a RIM archive.
    /// </summary>
    public class RIMResource
    {
        public ResRef ResRef { get; set; }
        public ResourceType ResType { get; set; }
        public byte[] Data { get; set; }

        public RIMResource(ResRef resref, ResourceType restype, byte[] data)
        {
            ResRef = resref;
            ResType = restype;
            // Handle bytearray conversion if needed
            Data = data is byte[]? data : data.ToArray();
        }

        public ResourceIdentifier Identifier() => new ResourceIdentifier(ResRef.ToString(), ResType);

        public override bool Equals(object obj)
        {
            if (obj is RIMResource other)
            {
                return ResRef.Equals(other.ResRef) &&
                       ResType == other.ResType &&
                       Data.SequenceEqual(other.Data);
            }
            if (obj is ERF.ERFResource erfRes)
            {
                return ResRef.Equals(erfRes.ResRef) &&
                       ResType == erfRes.ResType &&
                       Data.SequenceEqual(erfRes.Data);
            }
            return false;
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(ResRef);
            hash.Add(ResType);
            // Hash first 1000 bytes for performance
            foreach (byte b in Data.Take(1000))
            {
                hash.Add(b);
            }
            return hash.ToHashCode();
        }

        public override string ToString() => $"{ResRef}.{ResType.Extension}";
    }
}