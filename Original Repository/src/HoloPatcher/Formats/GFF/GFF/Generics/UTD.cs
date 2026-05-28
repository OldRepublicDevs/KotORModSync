using KOTORModSync;
using KOTORModSync.Formats.GFF;
using KOTORModSync.Resource;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics
{
    /// <summary>
    /// Stores door data.
    ///
    /// UTD files are GFF-based format files that store door definitions including
    /// lock/unlock mechanics, HP, scripts, and appearance.
    /// </summary>
    [PublicAPI]
    public sealed class UTD
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utd.py:16
        // Original: BINARY_TYPE = ResourceType.UTD
        public static readonly ResourceType BinaryType = ResourceType.UTD;

        // Basic door properties
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utd.py:35-46
        // Original: resref: "TemplateResRef" field
        public ResRef ResRef { get; set; }

        // Original: tag: "Tag" field
        public string Tag { get; set; }

        // Original: name: "LocName" field
        public LocalizedString Name { get; set; }

        // Original: description: "Description" field
        public LocalizedString Description { get; set; }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utd.py:16
        // Original: def __init__(self):
        public UTD()
        {
            ResRef = ResRef.FromBlank();
            Tag = string.Empty;
            Name = LocalizedString.FromInvalid();
            Description = LocalizedString.FromInvalid();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utd.py:410-477
        // Additional properties
        public ResRef Conversation { get; set; } = ResRef.FromBlank();
        public string Comment { get; set; } = string.Empty;
        public int FactionId { get; set; }
        public int AppearanceId { get; set; }
        public int AnimationState { get; set; }
        public bool AutoRemoveKey { get; set; }
        public string KeyName { get; set; } = string.Empty;
        public bool KeyRequired { get; set; }
        public bool Lockable { get; set; }
        public bool Locked { get; set; }
        public int UnlockDc { get; set; }
        public int UnlockDiff { get; set; } // KotOR 2 Only
        public int UnlockDiffMod { get; set; } // KotOR 2 Only
        public int OpenState { get; set; } // KotOR 2 Only
        public bool Min1Hp { get; set; } // KotOR 2 Only
        public bool NotBlastable { get; set; } // KotOR 2 Only
        public bool Plot { get; set; }
        public bool Static { get; set; }
        public int MaximumHp { get; set; }
        public int CurrentHp { get; set; }
        public int Hardness { get; set; }
        public int Fortitude { get; set; }
        public int Reflex { get; set; }
        public int Willpower { get; set; }

        // Trap properties (deprecated, toolset only)
        public bool TrapDetectable { get; set; }
        public bool TrapDisarmable { get; set; }
        public int DisarmDc { get; set; }
        public bool TrapOneShot { get; set; }
        public int TrapType { get; set; }
        public int PaletteId { get; set; }

        // Script hooks
        public ResRef OnClick { get; set; } = ResRef.FromBlank();
        public ResRef OnClosed { get; set; } = ResRef.FromBlank();
        public ResRef OnDamaged { get; set; } = ResRef.FromBlank();
        public ResRef OnDeath { get; set; } = ResRef.FromBlank();
        public ResRef OnOpenFailed { get; set; } = ResRef.FromBlank();
        public ResRef OnHeartbeat { get; set; } = ResRef.FromBlank();
        public ResRef OnMelee { get; set; } = ResRef.FromBlank();
        public ResRef OnOpen { get; set; } = ResRef.FromBlank();
        public ResRef OnUnlock { get; set; } = ResRef.FromBlank();
        public ResRef OnUserDefined { get; set; } = ResRef.FromBlank();
        public ResRef OnLock { get; set; } = ResRef.FromBlank();
        public ResRef OnPower { get; set; } = ResRef.FromBlank(); // OnSpellCastAt

        // Legacy/backward compatibility properties
        public int HP
        {
            get { return MaximumHp; }
            set { MaximumHp = value; }
        }

        public int CurrentHP
        {
            get { return CurrentHp; }
            set { CurrentHp = value; }
        }

        public int DC
        {
            get { return UnlockDc; }
            set { UnlockDc = value; }
        }

        public ResRef KeyRequiredResRef
        {
            get { return new ResRef(KeyName); }
            set { KeyName = value.ToString(); }
        }

        public ResRef OnMeleeAttacked
        {
            get { return OnMelee; }
            set { OnMelee = value; }
        }

        public ResRef OnSpellCastAt
        {
            get { return OnPower; }
            set { OnPower = value; }
        }

        public ResRef OnFailToOpen
        {
            get { return OnOpenFailed; }
            set { OnOpenFailed = value; }
        }
    }
}