namespace KOTORModSync.Formats.SSF
{

    /// <summary>
    /// Represents the different types of sounds in an SSF file.
    /// Matches Python SSFSound enum exactly.
    /// </summary>
    public enum SSFSound
    {
        BATTLE_CRY_1 = 0,
        BATTLE_CRY_2 = 1,
        BATTLE_CRY_3 = 2,
        BATTLE_CRY_4 = 3,
        BATTLE_CRY_5 = 4,
        BATTLE_CRY_6 = 5,
        SELECT_1 = 6,
        SELECT_2 = 7,
        SELECT_3 = 8,
        ATTACK_GRUNT_1 = 9,
        ATTACK_GRUNT_2 = 10,
        ATTACK_GRUNT_3 = 11,
        PAIN_GRUNT_1 = 12,
        PAIN_GRUNT_2 = 13,
        LOW_HEALTH = 14,
        DEAD = 15,
        CRITICAL_HIT = 16,
        TARGET_IMMUNE = 17,
        LAY_MINE = 18,
        DISARM_MINE = 19,
        BEGIN_STEALTH = 20,
        BEGIN_SEARCH = 21,
        BEGIN_UNLOCK = 22,
        UNLOCK_FAILED = 23,
        UNLOCK_SUCCESS = 24,
        SEPARATED_FROM_PARTY = 25,
        REJOINED_PARTY = 26,
        POISONED = 27
    }
}
