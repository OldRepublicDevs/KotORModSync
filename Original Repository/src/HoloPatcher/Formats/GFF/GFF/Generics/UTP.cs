using System.Collections.Generic;
using KOTORModSync;
using KOTORModSync.Formats.GFF;
using KOTORModSync.Resource;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics
{
    /// <summary>
    /// Stores placeable data.
    ///
    /// UTP files are GFF-based format files that store placeable object definitions including
    /// lock/unlock mechanics, HP, inventory, scripts, and appearance.
    /// </summary>
    [PublicAPI]
    public sealed class UTP
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utp.py:17
        // Original: BINARY_TYPE = ResourceType.UTP
        public static readonly ResourceType BinaryType = ResourceType.UTP;

        // Basic placeable properties
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utp.py:33-41
        // Original: resref: "TemplateResRef" field
        public ResRef ResRef { get; set; }

        // Original: tag: "Tag" field
        public string Tag { get; set; }

        // Original: name: "LocName" field
        public LocalizedString Name { get; set; }

        // Original: appearance_id: "Appearance" field
        public int AppearanceId { get; set; }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utp.py:17
        // Original: def __init__(self):
        public UTP()
        {
            ResRef = ResRef.FromBlank();
            Tag = string.Empty;
            Name = LocalizedString.FromInvalid();
            AppearanceId = 0;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utp.py:110-183
        // Additional properties
        public ResRef Conversation { get; set; } = ResRef.FromBlank();
        public string Comment { get; set; } = string.Empty;
        public int FactionId { get; set; }
        public int AnimationState { get; set; }
        public bool AutoRemoveKey { get; set; }
        public string KeyName { get; set; } = string.Empty;
        public bool KeyRequired { get; set; }
        public bool Lockable { get; set; }
        public bool Locked { get; set; }
        public int UnlockDc { get; set; }
        public int UnlockDiff { get; set; } // KotOR 2 Only
        public int UnlockDiffMod { get; set; } // KotOR 2 Only
        public bool Min1Hp { get; set; } // KotOR 2 Only
        public bool NotBlastable { get; set; } // KotOR 2 Only
        public bool Plot { get; set; }
        public bool Static { get; set; }
        public bool Useable { get; set; }
        public bool PartyInteract { get; set; }
        public int MaximumHp { get; set; }
        public int CurrentHp { get; set; }
        public int Hardness { get; set; }
        public int Fortitude { get; set; }
        public int Reflex { get; set; }
        public int Will { get; set; }
        public bool HasInventory { get; set; }
        public List<InventoryItem> Inventory { get; set; } = new List<InventoryItem>();
        public LocalizedString Description { get; set; } = LocalizedString.FromInvalid();

        // Script hooks
        public ResRef OnClick { get; set; } = ResRef.FromBlank();
        public ResRef OnClosed { get; set; } = ResRef.FromBlank();
        public ResRef OnDamaged { get; set; } = ResRef.FromBlank();
        public ResRef OnDeath { get; set; } = ResRef.FromBlank();
        public ResRef OnEndDialog { get; set; } = ResRef.FromBlank();
        public ResRef OnOpenFailed { get; set; } = ResRef.FromBlank();
        public ResRef OnHeartbeat { get; set; } = ResRef.FromBlank();
        public ResRef OnInventory { get; set; } = ResRef.FromBlank();
        public ResRef OnMelee { get; set; } = ResRef.FromBlank();
        public ResRef OnOpen { get; set; } = ResRef.FromBlank();
        public ResRef OnUnlock { get; set; } = ResRef.FromBlank();
        public ResRef OnUsed { get; set; } = ResRef.FromBlank();
        public ResRef OnUserDefined { get; set; } = ResRef.FromBlank();
        public ResRef OnLock { get; set; } = ResRef.FromBlank();
        public ResRef OnPower { get; set; } = ResRef.FromBlank(); // OnForcePower

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