using System;
using System.Collections.Generic;
using System.Linq;
using KOTORModSync.Resource;

namespace KOTORModSync.Formats.LIP
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lip/lip_data.py:225-458
    // Original: class LIP(ComparableMixin)
    public class LIP : IEquatable<LIP>
    {
        public static readonly ResourceType BinaryType = ResourceType.LIP;
        public const string FileHeader = "LIP V1.0";

        public float Length { get; set; }
        public List<LIPKeyFrame> Frames { get; set; }

        public LIP()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lip/lip_data.py:267-278
            // Original: def __init__(self) -> None
            Length = 0.0f;
            Frames = new List<LIPKeyFrame>();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lip/lip_data.py:280-282
        // Original: def __iter__(self) -> Iterator[LIPKeyFrame]
        public IEnumerator<LIPKeyFrame> GetEnumerator()
        {
            return Frames.GetEnumerator();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lip/lip_data.py:284-286
        // Original: def __len__(self) -> int
        public int Count => Frames.Count;

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lip/lip_data.py:288-303
        // Original: def __getitem__(self, item: int) -> LIPKeyFrame
        public LIPKeyFrame this[int index]
        {
            get => Frames[index];
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lip/lip_data.py:305-321
        // Original: def add(self, time: float, shape: LIPShape) -> None
        public void Add(float time, LIPShape shape)
        {
            // Remove any existing keyframe at this time
            Frames = Frames.Where(f => Math.Abs(f.Time - time) > 0.0001f).ToList();

            var frame = new LIPKeyFrame(time, shape);
            Frames.Add(frame);
            Frames.Sort(); // Sort by time
            Length = Math.Max(Length, time);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lip/lip_data.py:324-340
        // Original: def remove(self, index: int) -> None
        public void Remove(int index)
        {
            if (index >= 0 && index < Frames.Count)
            {
                Frames.RemoveAt(index);
                if (Frames.Count > 0)
                {
                    Length = Frames.Max(f => f.Time);
                }
                else
                {
                    Length = 0.0f;
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lip/lip_data.py:342-384
        // Original: def get_shapes(self, time: float) -> tuple[LIPShape, LIPShape, float] | None
        public Tuple<LIPShape, LIPShape, float> GetShapes(float time)
        {
            if (Frames.Count == 0)
            {
                return null;
            }

            // Handle time before first keyframe
            if (time <= Frames[0].Time)
            {
                return Tuple.Create(Frames[0].Shape, Frames[0].Shape, 0.0f);
            }

            // Handle time after last keyframe
            if (time >= Frames[Frames.Count - 1].Time)
            {
                return Tuple.Create(Frames[Frames.Count - 1].Shape, Frames[Frames.Count - 1].Shape, 0.0f);
            }

            // Find surrounding keyframes
            LIPKeyFrame leftFrame = Frames[0];
            LIPKeyFrame rightFrame = Frames[0];

            foreach (var frame in Frames)
            {
                if (time > frame.Time)
                {
                    leftFrame = frame;
                }
                if (time <= frame.Time)
                {
                    rightFrame = frame;
                    break;
                }
            }

            return leftFrame.Interpolate(rightFrame, time);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lip/lip_data.py:386-397
        // Original: def get(self, index: int) -> LIPKeyFrame | None
        public LIPKeyFrame Get(int index)
        {
            return (index >= 0 && index < Frames.Count) ? Frames[index] : null;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lip/lip_data.py:399-421
        // Original: def get_shape_at_time(self, time: float) -> LIPShape | None
        public LIPShape? GetShapeAtTime(float time)
        {
            var shapes = GetShapes(time);
            if (shapes == null)
            {
                return null;
            }

            LIPShape leftShape = shapes.Item1;
            LIPShape rightShape = shapes.Item2;
            float factor = shapes.Item3;

            // For now, just return the shape we're closest to
            // In a full implementation, this would do proper shape interpolation
            return factor > 0.5f ? rightShape : leftShape;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lip/lip_data.py:423-426
        // Original: def clear(self) -> None
        public void Clear()
        {
            Frames.Clear();
            Length = 0.0f;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lip/lip_data.py:428-457
        // Original: def validate(self) -> list[str]
        public List<string> Validate()
        {
            List<string> errors = new List<string>();

            if (Frames.Count == 0)
            {
                errors.Add("No keyframes defined");
                return errors;
            }

            // Check for negative times
            foreach (var frame in Frames)
            {
                if (frame.Time < 0)
                {
                    errors.Add($"Negative time value: {frame.Time}");
                }
            }

            // Check for proper ordering
            float lastTime = -1;
            foreach (var frame in Frames)
            {
                if (frame.Time < lastTime)
                {
                    errors.Add($"Keyframes out of order: {frame.Time} after {lastTime}");
                }
                lastTime = frame.Time;
            }

            // Check length matches last keyframe
            if (Frames.Count > 0 && Math.Abs(Length - Frames[Frames.Count - 1].Time) > 0.0001f)
            {
                errors.Add($"Length ({Length}) doesn't match last keyframe time ({Frames[Frames.Count - 1].Time})");
            }

            return errors;
        }

        public override bool Equals(object obj)
        {
            return obj is LIP other && Equals(other);
        }

        public bool Equals(LIP other)
        {
            if (other == null)
            {
                return false;
            }
            return Math.Abs(Length - other.Length) < 1e-6f && Frames.SequenceEqual(other.Frames);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Length);
            foreach (var frame in Frames)
            {
                hash.Add(frame);
            }
            return hash.ToHashCode();
        }
    }
}

