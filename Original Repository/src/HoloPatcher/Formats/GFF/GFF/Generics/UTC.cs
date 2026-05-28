using System.Collections.Generic;
using KOTORModSync;
using KOTORModSync.Resource;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utc.py:18
    // Original: class UTC:
    /// <summary>
    /// Stores creature data.
    ///
    /// UTC files are GFF-based format files that store creature definitions including
    /// stats, appearance, inventory, feats, and script hooks.
    /// </summary>
    /// <remarks>
    /// UTC (Creature Template) Format:
    /// - Based on swkotor2.exe creature template system
    /// - Located via string references: "Creature" @ 0x007bc544, "CreatureList" @ 0x007c0c80
    /// - Original implementation: UTC files are GFF with "UTC " signature containing creature template data
    /// </remarks>
    [PublicAPI]
    public sealed class UTC
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utc.py:18
        // Original: BINARY_TYPE = ResourceType.UTC
        public static readonly ResourceType BinaryType = ResourceType.UTC;

        // Internal use only, to preserve original order
        private readonly Dictionary<int, int> _originalFeatMapping = new Dictionary<int, int>();
        private readonly List<int> _extraUnimplementedSkills = new List<int>();

        // Basic creature properties
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utc.py:343-349
        public ResRef ResRef { get; set; } = ResRef.FromBlank();
        public ResRef Conversation { get; set; } = ResRef.FromBlank();
        public string Tag { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;

        public LocalizedString FirstName { get; set; } = LocalizedString.FromInvalid();
        public LocalizedString LastName { get; set; } = LocalizedString.FromInvalid();

        // Appearance and identity
        public int SubraceId { get; set; }
        public int PortraitId { get; set; }
        public int PerceptionId { get; set; }
        public int RaceId { get; set; }
        public int AppearanceId { get; set; }
        public int GenderId { get; set; }
        public int FactionId { get; set; }
        public int WalkrateId { get; set; }
        public int SoundsetId { get; set; }
        public int SaveWill { get; set; }
        public int SaveFortitude { get; set; }
        public int Morale { get; set; }
        public int MoraleRecovery { get; set; }
        public int MoraleBreakpoint { get; set; }
        public int BodyVariation { get; set; }
        public int TextureVariation { get; set; }
        public ResRef PortraitResRef { get; set; } = ResRef.FromBlank();

        // Boolean flags
        public bool NotReorienting { get; set; }
        public bool PartyInteract { get; set; }
        public bool NoPermDeath { get; set; }
        public bool Min1Hp { get; set; }
        public bool Plot { get; set; }
        public bool Interruptable { get; set; }
        public bool IsPc { get; set; }
        public bool Disarmable { get; set; }
        public bool IgnoreCrePath { get; set; } // KotOR 2 Only
        public bool Hologram { get; set; } // KotOR 2 Only
        public bool WillNotRender { get; set; } // KotOR 2 Only

        // Stats
        public int Alignment { get; set; }
        public float ChallengeRating { get; set; }
        public float Blindspot { get; set; } // KotOR 2 Only
        public int MultiplierSet { get; set; } // KotOR 2 Only
        public int NaturalAc { get; set; }
        public int ReflexBonus { get; set; }
        public int WillpowerBonus { get; set; }
        public int FortitudeBonus { get; set; }

        // Hit points and force points
        public int CurrentHp { get; set; }
        public int MaxHp { get; set; }
        public int Hp { get; set; }
        public int MaxFp { get; set; }
        public int Fp { get; set; }

        // Ability scores
        public int Strength { get; set; }
        public int Dexterity { get; set; }
        public int Constitution { get; set; }
        public int Intelligence { get; set; }
        public int Wisdom { get; set; }
        public int Charisma { get; set; }

        // Skills
        public int ComputerUse { get; set; }
        public int Demolitions { get; set; }
        public int Stealth { get; set; }
        public int Awareness { get; set; }
        public int Persuade { get; set; }
        public int Repair { get; set; }
        public int Security { get; set; }
        public int TreatInjury { get; set; }

        // Script hooks
        public ResRef OnEndDialog { get; set; } = ResRef.FromBlank();
        public ResRef OnBlocked { get; set; } = ResRef.FromBlank();
        public ResRef OnHeartbeat { get; set; } = ResRef.FromBlank();
        public ResRef OnNotice { get; set; } = ResRef.FromBlank();
        public ResRef OnSpell { get; set; } = ResRef.FromBlank();
        public ResRef OnAttacked { get; set; } = ResRef.FromBlank();
        public ResRef OnDamaged { get; set; } = ResRef.FromBlank();
        public ResRef OnDisturbed { get; set; } = ResRef.FromBlank();
        public ResRef OnEndRound { get; set; } = ResRef.FromBlank();
        public ResRef OnDialog { get; set; } = ResRef.FromBlank();
        public ResRef OnSpawn { get; set; } = ResRef.FromBlank();
        public ResRef OnRested { get; set; } = ResRef.FromBlank();
        public ResRef OnDeath { get; set; } = ResRef.FromBlank();
        public ResRef OnUserDefined { get; set; } = ResRef.FromBlank();

        // Classes, feats, inventory, equipment
        public List<UTCClass> Classes { get; set; } = new List<UTCClass>();
        public List<int> Feats { get; set; } = new List<int>();
        public List<InventoryItem> Inventory { get; set; } = new List<InventoryItem>();
        public Dictionary<EquipmentSlot, InventoryItem> Equipment { get; set; } = new Dictionary<EquipmentSlot, InventoryItem>();

        // Deprecated fields
        public int PaletteId { get; set; }
        public int BodybagId { get; set; } = 1;
        public string Deity { get; set; } = string.Empty;
        public LocalizedString Description { get; set; } = LocalizedString.FromInvalid();
        public int Lawfulness { get; set; }
        public int PhenotypeId { get; set; }
        public string SubraceName { get; set; } = string.Empty;

        public UTC()
        {
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utc.py:447-497
    // Original: class UTCClass:
    [PublicAPI]
    public sealed class UTCClass
    {
        // Internal use only, to preserve original order
        private readonly Dictionary<int, int> _originalPowersMapping = new Dictionary<int, int>();

        public int ClassId { get; set; }
        public int ClassLevel { get; set; }
        public List<int> Powers { get; set; } = new List<int>();

        public UTCClass(int classId, int classLevel = 0)
        {
            ClassId = classId;
            ClassLevel = classLevel;
        }
    }
}
