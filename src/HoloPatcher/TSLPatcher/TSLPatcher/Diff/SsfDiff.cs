using System;
using System.Collections.Generic;
using KOTORModSync.Formats.SSF;

namespace KOTORModSync.Diff
{

    public class SsfCompareResult
    {
        public Dictionary<SSFSound, int> ChangedSounds { get; } = new Dictionary<SSFSound, int>();
    }

    public static class SsfDiff
    {
        public static SsfCompareResult Compare(SSF original, SSF modified)
        {
            var result = new SsfCompareResult();

            foreach (SSFSound sound in Enum.GetValues(typeof(SSFSound)))
            {
                // Valid indices are 0-27 based on SSF implementation
                if ((int)sound < 0 || (int)sound >= 28)
                {
                    continue;
                }

                int origVal = original[sound];
                int modVal = modified[sound];

                if (origVal != modVal)
                {
                    result.ChangedSounds[sound] = modVal;
                }
            }

            return result;
        }
    }
}

