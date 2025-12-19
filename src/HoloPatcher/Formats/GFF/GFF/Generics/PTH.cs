using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using KOTORModSync;
using KOTORModSync.Resource;
using Andastra.Utility;
using JetBrains.Annotations;

namespace KOTORModSync.Resource.Generics
{
    /// <summary>
    /// Stores the path data for a module.
    ///
    /// PTH files are GFF-based format files that store pathfinding data including
    /// waypoints and connections for NPC navigation.
    /// </summary>
    [PublicAPI]
    public sealed class PTH : IEnumerable<Vector2>
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/pth.py:33
        // Original: BINARY_TYPE = ResourceType.PTH
        public static readonly ResourceType BinaryType = ResourceType.PTH;

        private readonly List<Vector2> _points = new List<Vector2>();
        private readonly List<PTHEdge> _connections = new List<PTHEdge>();

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/pth.py:35-39
        // Original: def __init__(self):
        public PTH()
        {
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/pth.py:41-44
        // Original: def __iter__(self):
        public IEnumerator<Vector2> GetEnumerator()
        {
            return _points.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/pth.py:46-49
        // Original: def __len__(self):
        public int Count => _points.Count;

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/pth.py:51-55
        // Original: def __getitem__(self, item: int) -> Vector2:
        public Vector2 this[int index]
        {
            get => _points[index];
        }

        /// <summary>
        /// Updates the position of an existing point.
        /// </summary>
        /// <param name="index">Index of the point to update.</param>
        /// <param name="point">New coordinates for the point.</param>
        public void SetPoint(int index, Vector2 point)
        {
            _points[index] = point;
        }

        /// <summary>
        /// Updates the position of an existing point using individual components.
        /// </summary>
        /// <param name="index">Index of the point to update.</param>
        /// <param name="x">New X coordinate.</param>
        /// <param name="y">New Y coordinate.</param>
        public void SetPoint(int index, float x, float y)
        {
            _points[index] = new Vector2(x, y);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/pth.py:57-63
        // Original: def add(self, x: float, y: float) -> int:
        public int Add(float x, float y)
        {
            _points.Add(new Vector2(x, y));
            return _points.Count - 1;
        }

        public void AddPoint(Vector2 point)
        {
            _points.Add(point);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/pth.py:65-75
        // Original: def remove(self, index: int):
        public void Remove(int index)
        {
            _points.RemoveAt(index);

            _connections.RemoveAll(x => x.SourceIndex == index || x.TargetIndex == index);

            foreach (var connection in _connections)
            {
                if (connection.SourceIndex > index)
                {
                    connection.SourceIndex = connection.SourceIndex - 1;
                }
                if (connection.TargetIndex > index)
                {
                    connection.TargetIndex = connection.TargetIndex - 1;
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/pth.py:77-85
        // Original: def get(self, index: int) -> Vector2 | None:
        public Vector2 Get(int index)
        {
            try
            {
                return _points[index];
            }
            catch (Exception e)
            {
                ErrorHandling.UniversalSimplifyException(e);
            }
                return default(Vector2);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/pth.py:87-91
        // Original: def find(self, point: Vector2) -> int | None:
        public int? Find(Vector2 point)
        {
            int index = _points.IndexOf(point);
            return index >= 0 ? (int?)index : null;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/pth.py:93-98
        // Original: def connect(self, source: int, target: int):
        public void Connect(int source, int target)
        {
            _connections.Add(new PTHEdge(source, target));
        }

        public void AddConnection(PTHEdge edge)
        {
            _connections.Add(edge);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/pth.py:100-110
        // Original: def disconnect(self, source: int, target: int):
        public void Disconnect(int source, int target)
        {
            var toRemove = new List<PTHEdge>();
            foreach (var edge in _connections)
            {
                if ((edge.SourceIndex == source || edge.SourceIndex == target) &&
                    (edge.TargetIndex == source || edge.TargetIndex == target))
                {
                    toRemove.Add(edge);
                }
            }
            foreach (var edge in toRemove)
            {
                _connections.Remove(edge);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/pth.py:112-117
        // Original: def is_connected(self, source: int, target: int) -> bool:
        public bool IsConnected(int source, int target)
        {
            return _connections.Any(x => x.SourceIndex == source && x.TargetIndex == target);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/pth.py:119-123
        // Original: def outgoing(self, source: int) -> list[PTHEdge]:
        public List<PTHEdge> Outgoing(int source)
        {
            return _connections.Where(connection => connection.SourceIndex == source).ToList();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/pth.py:125-129
        // Original: def incoming(self, target: int) -> list[PTHEdge]:
        public List<PTHEdge> Incoming(int target)
        {
            return _connections.Where(connection => connection.TargetIndex == target).ToList();
        }

        public IEnumerable<Vector2> GetPoints()
        {
            return _points;
        }

        public List<PTHEdge> GetConnections()
        {
            return new List<PTHEdge>(_connections);
        }

        public Vector2 GetPoint(int index)
        {
            return _points[index];
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/pth.py:132-157
    // Original: class PTHEdge:
    [PublicAPI]
    public sealed class PTHEdge : IEquatable<PTHEdge>
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/pth.py:133-139
        // Original: def __init__(self, source: int, target: int):
        public int SourceIndex { get; set; }
        public int TargetIndex { get; set; }

        public PTHEdge(int sourceIndex, int targetIndex)
        {
            SourceIndex = sourceIndex;
            TargetIndex = targetIndex;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/pth.py:141-144
        // Original: def __repr__(self):
        public override string ToString()
        {
            return $"{GetType().Name}(source={SourceIndex}, target={TargetIndex})";
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/pth.py:146-154
        // Original: def __eq__(self, other: PTHEdge | object):
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            if (obj is PTHEdge other)
            {
                return SourceIndex == other.SourceIndex && TargetIndex == other.TargetIndex;
            }
            return false;
        }

        public bool Equals(PTHEdge other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            return SourceIndex == other.SourceIndex && TargetIndex == other.TargetIndex;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/pth.py:156-157
        // Original: def __hash__(self):
        public override int GetHashCode()
        {
            return (SourceIndex, TargetIndex).GetHashCode();
        }

        public static bool operator ==(PTHEdge left, PTHEdge right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }
            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
            {
                return false;
            }
            return left.Equals(right);
        }

        public static bool operator !=(PTHEdge left, PTHEdge right)
        {
            return !(left == right);
        }
    }
}
