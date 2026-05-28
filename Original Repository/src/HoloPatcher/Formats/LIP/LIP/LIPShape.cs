using System;
using System.Collections.Generic;

namespace KOTORModSync.Formats.LIP
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lip/lip_data.py:50-221
    // Original: class LIPShape(IntEnum)
    public enum LIPShape
    {
        Neutral = 0,    // Neutral/rest position (used for pauses)
        EE = 1,         // Teeth slightly apart, corners wide (as in "see")
        EH = 2,         // Mouth relaxed, slightly open (as in "get")
        AH = 3,         // Mouth open (as in "father")
        OH = 4,         // Rounded lips (as in "go")
        OOH = 5,        // Pursed lips (as in "too")
        Y = 6,          // Slight smile (as in "yes")
        STS = 7,        // Teeth together (as in "stop")
        FV = 8,         // Lower lip touching upper teeth (as in "five")
        NG = 9,         // Back of tongue up (as in "ring")
        TH = 10,        // Tongue between teeth (as in "thin")
        MPB = 11,       // Lips pressed together (as in "bump")
        TD = 12,        // Tongue up (as in "top")
        SH = 13,        // Rounded but relaxed (as in "measure")
        L = 14,         // Tongue forward (as in "lip")
        KG = 15         // Back of tongue raised (as in "kick")
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lip/lip_data.py:170-220
    // Original: @classmethod def from_phoneme(cls, phoneme: str) -> LIPShape
    public static class LIPShapeExtensions
    {
        private static readonly Dictionary<string, LIPShape> PhonemeMapping = new Dictionary<string, LIPShape>
        {
            { "AA", LIPShape.AH },    // father
            { "AE", LIPShape.AH },    // cat
            { "AH", LIPShape.AH },    // but
            { "AO", LIPShape.OH },    // bought
            { "AW", LIPShape.AH },    // down
            { "AY", LIPShape.AH },    // bite
            { "B", LIPShape.MPB },    // be
            { "CH", LIPShape.SH },    // cheese
            { "D", LIPShape.TD },     // dee
            { "DH", LIPShape.TH },    // thee
            { "EH", LIPShape.EH },    // bet
            { "ER", LIPShape.EH },    // bird
            { "EY", LIPShape.EE },    // bait
            { "F", LIPShape.FV },     // fee
            { "G", LIPShape.KG },     // green
            { "HH", LIPShape.KG },    // he
            { "IH", LIPShape.EE },    // bit
            { "IY", LIPShape.EE },    // beet
            { "JH", LIPShape.SH },    // jee
            { "K", LIPShape.KG },     // key
            { "L", LIPShape.L },      // lee
            { "M", LIPShape.MPB },    // me
            { "N", LIPShape.NG },     // knee
            { "NG", LIPShape.NG },    // ping
            { "OW", LIPShape.OH },    // boat
            { "OY", LIPShape.OH },    // boy
            { "P", LIPShape.MPB },    // pee
            { "R", LIPShape.L },      // read
            { "S", LIPShape.STS },    // sea
            { "SH", LIPShape.SH },    // she
            { "T", LIPShape.TD },     // tea
            { "TH", LIPShape.TH },    // theta
            { "UH", LIPShape.OOH },   // book
            { "UW", LIPShape.OOH },   // boot
            { "V", LIPShape.FV },     // vee
            { "W", LIPShape.OOH },    // we
            { "Y", LIPShape.Y },      // yield
            { "Z", LIPShape.STS },    // zee
            { "ZH", LIPShape.SH },    // seizure
            { " ", LIPShape.Neutral },  // pause
            { "-", LIPShape.Neutral }   // pause
        };

        public static LIPShape FromPhoneme(string phoneme)
        {
            if (string.IsNullOrEmpty(phoneme))
            {
                return LIPShape.Neutral;
            }
            string upper = phoneme.ToUpperInvariant();
            return PhonemeMapping.TryGetValue(upper, out LIPShape shape) ? shape : LIPShape.Neutral;
        }
    }
}

