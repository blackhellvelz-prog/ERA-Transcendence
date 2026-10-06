namespace WoG.Core.Model;

/// <summary>
/// State of one WoG commander (class <c>NPC</c> in npc.cpp). Pure data — the rules live in
/// WoG.Commanders. Field names follow the WoG source so the mapping stays reviewable.
/// </summary>
public sealed class WoGCommander
{
    /// <summary>Hero number = commander number.</summary>
    public int Number { get; set; }
    /// <summary>NPC::Used — 0 not present/fired, 1 enabled, &lt;0 forbidden.</summary>
    public int Used { get; set; }
    public int Dead { get; set; }
    /// <summary>Commander class 0..8 (hero class / 2).</summary>
    public int Type { get; set; }
    /// <summary>Owning hero's class 0..17 (AI skill weights).</summary>
    public int HType { get; set; }
    public bool CustomPrimary { get; set; }
    /// <summary>AT, DF, HP, DM, MP, SP, MR.</summary>
    public int[] Primary { get; set; } = new int[7];
    /// <summary>Skill levels 0..5 for AT, DF, HP, DM, MP, SP, MR (MR follows MP).</summary>
    public int[] Skills { get; set; } = new int[7];
    /// <summary>10 slots × (artifact id, battles won); slots 0..5 are reachable from ERM.</summary>
    public int[][] Arts { get; set; } = NewArts();
    public string Name { get; set; } = "";
    public int OldHeroExp { get; set; }
    public int Exp { get; set; }
    /// <summary>Internal level (0 = displayed level 1), max 74.</summary>
    public int Level { get; set; }
    /// <summary>[0] = owned special bonuses, [1] = forbidden special bonuses (15 bits).</summary>
    public uint[] SpecBon { get; set; } = new uint[2];
    public int LastExpoInBattle { get; set; }

    static int[][] NewArts()
    {
        var a = new int[10][];
        for (int i = 0; i < 10; i++) a[i] = new int[2];
        return a;
    }
}
