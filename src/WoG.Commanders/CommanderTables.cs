namespace WoG.Commanders;

/// <summary>Numeric tables of npc.cpp, copied verbatim.</summary>
public static class CommanderTables
{
    public const int MaxLevel = 74;
    public const int ArtBase = 146;
    public const int HireCost = 1000;

    /// <summary>NPC::Levels — total experience for internal level i.</summary>
    public static readonly int[] Levels =
    {
                 0,       1000,       2000,       3200,       4600,
              6200,       8000,      10000,      12200,      14700,
             17500,      20600,      24320,      28784,      34140,
             40567,      48279,      57533,      68637,      81961,
             97949,     117134,     140156,     167782,     200933,
            240714,     288451,     345735,     414475,     496963,
            595948,     714730,     857268,    1028313,    1233567,
           1479871,    1775435,    2130111,    2555722,    3066455,
           3679334,    4414788,    5297332,    6356384,    7627246,
           9152280,   10982320,   13178368,   15813625,   18975933,
          22770702,   27324424,   32788890,   39346249,   47215079,
          56657675,   67988790,   81586128,   97902933,  117483099,
         140979298,  169174736,  203009261,  243610691,  292332407,
         350798469,  420957739,  505148863,  606178211,  727413428,
         872895688, 1047474816, 1256969772, 1508363724, 1810036464,
    };

    /// <summary>NPC::Bonus[skill][level-1]: AT, DF, HP%, DM%, MP, SP, MR%.</summary>
    public static readonly int[][] Bonus =
    {
        new[] { 2, 5, 9, 15, 25 },
        new[] { 4, 10, 18, 30, 50 },
        new[] { 10, 25, 45, 70, 100 },
        new[] { 10, 25, 45, 70, 100 },
        new[] { 1, 3, 6, 14, 29 },
        new[] { 1, 2, 3, 4, 6 },
        new[] { 5, 15, 35, 60, 90 },
    };

    /// <summary>NPC::AISkillsChance[heroClass][AT,DF,HP,DM,MP,SP] — weights for automatic level-ups.</summary>
    public static readonly int[][] AiSkillsChance =
    {
        new[] { 5, 15, 20, 5, 35, 20 },
        new[] { 13, 35, 5, 20, 7, 20 },
        new[] { 13, 5, 20, 20, 35, 7 },
        new[] { 35, 13, 7, 20, 5, 20 },
        new[] { 10, 5, 20, 20, 15, 30 },
        new[] { 30, 15, 35, 10, 5, 5 },
        new[] { 20, 13, 5, 35, 7, 20 },
        new[] { 10, 15, 30, 20, 5, 20 },
        new[] { 15, 10, 10, 5, 20, 40 },
        new[] { 5, 10, 15, 20, 10, 40 },
        new[] { 5, 9, 30, 18, 25, 13 },
        new[] { 20, 5, 20, 30, 10, 15 },
        new[] { 5, 25, 5, 5, 25, 35 },
        new[] { 20, 15, 10, 5, 30, 20 },
        new[] { 35, 5, 15, 5, 10, 30 },
        new[] { 35, 30, 5, 5, 10, 15 },
        new[] { 15, 10, 20, 30, 5, 20 },
        new[] { 30, 20, 20, 15, 5, 10 },
    };

    /// <summary>Spell cast by commander class (SetMonInitAfter, H3 spell numbers).</summary>
    public static readonly int[] ClassSpell = { 37, 27, 44, 29, 39, 43, 46, 53, 58 };

    /// <summary>Special-bonus bit masks (npc.cpp #defines).</summary>
    public const uint AT_DF = 0x0001, AT_HP = 0x0002, AT_DM = 0x0004, AT_MP = 0x0008, AT_SP = 0x0010,
        DF_HP = 0x0020, DF_DM = 0x0040, DF_MP = 0x0080, DF_SP = 0x0100, HP_DM = 0x0200, HP_MP = 0x0400,
        HP_SP = 0x0800, DM_MP = 0x1000, DM_SP = 0x2000, MP_SP = 0x4000;

    public static int CalcHp(int level) => 40 + level * 20;
    public static int CalcDm(int level) => 12 + level * 4;
}
