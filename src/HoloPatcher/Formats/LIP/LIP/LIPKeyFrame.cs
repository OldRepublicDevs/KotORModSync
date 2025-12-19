using System;

namespace KOTORModSync.Formats.LIP
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lip/lip_data.py:459-530
    // Original: @dataclass class LIPKeyFrame(ComparableMixin)
    public class LIPKeyFrame : IEquatable<LIPKeyFrame>, IComparable<LIPKeyFrame>
    {
        public float Time { get; set; }
        public LIPShape Shape { get; set; }

        public LIPKeyFrame(float time, LIPShape shape)
        {
            Time = time;
            Shape = shape;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lip/lip_data.py:508-526
        // Original: def interpolate(self, other: LIPKeyFrame, time: float) -> tuple[LIPShape, LIPShape, float]
        public Tuple<LIPShape, LIPShape, float> Interpolate(LIPKeyFrame other, float time)
        {
            if (Equals(other))
            {
                return Tuple.Create(Shape, other.Shape, 0.0f);
            }

            float factor = (time - Time) / (other.Time - Time);
            factor = Math.Max(0.0f, Math.Min(1.0f, factor)); // Clamp between 0 and 1

            return Tuple.Create(Shape, other.Shape, factor);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lip/lip_data.py:528-529
        // Original: def __lt__(self, other: LIPKeyFrame) -> bool
        public int CompareTo(LIPKeyFrame other)
        {
            if (other == null)
            {
                return 1;
            }
            return Time.CompareTo(other.Time);
        }

        public override bool Equals(object obj)
        {
            return obj is LIPKeyFrame other && Equals(other);
        }

        public bool Equals(LIPKeyFrame other)
        {
            if (other == null)
            {
                return false;
            }
            return Math.Abs(Time - other.Time) < 1e-6f && Shape == other.Shape;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Time, Shape);
        }
    }
}

