using System;
using System.Linq;
using JetBrains.Annotations;

namespace KOTORModSync.Formats.SSF
{
    /// <summary>
    /// Represents the data stored in an SSF (sound set file) file.
    /// </summary>
    public class SSF
    {
        private readonly int[] _sounds = new int[28];

        public SSF()
        {
            Reset();
        }

        public void Reset()
        {
            for (int i = 0; i < 28; i++)
            {
                _sounds[i] = -1;
            }
        }

        public void SetData(SSFSound sound, int stringref)
        {
            _sounds[(int)sound] = stringref;
        }

        public int? Get(SSFSound sound)
        {
            return _sounds[(int)sound];
        }

        public int this[SSFSound sound]
        {
            get => _sounds[(int)sound];
            set => _sounds[(int)sound] = value;
        }

        public override bool Equals([CanBeNull] object obj)
        {
            if (obj is SSF other)
            {
                return _sounds.SequenceEqual(other._sounds);
            }
            return false;
        }

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            foreach (int sound in _sounds)
            {
                hash.Add(sound);
            }
            return hash.ToHashCode();
        }

        /// <summary>
        /// Serializes the SSF to a byte array.
        /// </summary>
        public byte[] ToBytes()
        {
            var writer = new SSFBinaryWriter(this);
            return writer.Write();
        }

        /// <summary>
        /// Deserializes an SSF from a byte array.
        /// </summary>
        public static SSF FromBytes(byte[] data)
        {
            var reader = new SSFBinaryReader(data);
            return reader.Load();
        }
    }

}
