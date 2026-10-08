using WoG.Core.Adapters;
using WoG.Core.Compat;
using WoG.Erm.Runtime;

namespace WoG.Erm.Receivers;

/// <summary>!!BA — the battle being set up or fought (Monsters.cpp ERM_Battle: the G2B_* globals of the battle).</summary>
public sealed class BaReceiver : ErmReceiverBase
{
    const string Note = "BA:H/O/P/Q/S/E/A — the battle as the adapter saw it start: attacker = the active hero of the player whose turn it is, defender = the monster squad next to it (hero-vs-hero and town battles are not told apart yet); read only";
    const string Change = "BA — changing the heroes, position or quick-battle flag of a battle: not mapped yet";

    public BaReceiver() : base("BA")
    {
        Declare("HOPQSEA", CompatLevel.PartiallySupported, Note);
        Declare("M", CompatLevel.Unsupported, "BA:M — the armies of the battle sides are not mapped yet");
        Declare("DB", CompatLevel.Unsupported, "BA:D/B — cancelling a battle and its background are not mapped yet");
    }

    protected override void Run(ErmCall c)
    {
        var b = c.Need(c.Rt.Services.Game.Battle.GetBattle());
        switch (c.Letter)
        {
            case 'H':
            {
                c.RequireMin(2);
                int side = c.N(0);
                if (side < 0 || side > 1) throw new ErmRuntimeException("\"!!BA:H\"-side out of range (0,1).");
                int hero = b.Heroes[side] >= 0 ? b.Heroes[side] : -2; // WoG: -2 when the side has no hero
                MapSelector.ReadOnly(c, hero, 1, Change);
                break;
            }
            case 'O':
            {
                c.RequireMin(2);
                int a = b.Owners[0], d = b.Owners[1];
                c.Apply(ref a, 0); // WoG applies to copies: a "set" changes nothing
                c.Apply(ref d, 1);
                break;
            }
            case 'P':
            {
                c.RequireMin(3);
                MapSelector.ReadOnly(c, b.Position.X, 0, Change);
                MapSelector.ReadOnly(c, b.Position.Y, 1, Change);
                MapSelector.ReadOnly(c, b.Position.L, 2, Change);
                break;
            }
            case 'Q':
                c.RequireMin(1);
                MapSelector.ReadOnly(c, b.Quick ? 1 : 0, 0, Change);
                break;
            case 'S':
            {
                c.RequireMin(1);
                int v = b.Siege ? 1 : 0;
                c.Apply(ref v, 0);
                break;
            }
            case 'E':
            {
                int v = 0; // single-player: never a human-vs-human network battle
                c.Apply(ref v, 0);
                break;
            }
            case 'A':
            {
                c.RequireMin(1);
                int v = b.CompleteAi ? 1 : 0;
                c.Apply(ref v, 0);
                break;
            }
            case 'M':
                throw new ErmUnsupportedException("BA:M — the armies of the battle sides are not mapped yet");
            case 'D':
            case 'B':
                throw new ErmUnsupportedException("BA:D/B — cancelling a battle and its background are not mapped yet");
            default:
                throw ErmCall.WrongCommand(c.Letter);
        }
    }
}

/// <summary>!!BM — a stack in battle (Monsters.cpp ERM_BRound: the stack record at combatManager+0x54CC+0x548*n).</summary>
public sealed class BmReceiver : ErmReceiverBase
{
    const string Spells = "BM:G/C/K/M/Q/V/U4/U5 — spells on a stack, casting, damage, magic obstacles, animations and spell or clone settings are not mapped yet";
    const string Kept = "BM:T/B/I/O/P/F/E/R/J/U3 — the type, start count, side, army slot, position, flags, casts, retaliations, active spells and shots of a battle stack: read where Olden Era has them, changing them is not mapped";

    public BmReceiver() : base("BM")
    {
        Declare("NLADHSU", CompatLevel.PartiallySupported,
            "BM:N/L/A/D/H/S/U1/U2 — count, hit points lost by the top creature, attack, defence, hit points, speed and damage of a battle stack: Olden Era's own unit in the battle; a changed stat is the unit's battle modifier, so the game's recalculations keep it");
        Declare("TBIOPFERJ", CompatLevel.PartiallySupported, Kept);
        Declare("GCKMQV", CompatLevel.Unsupported, Spells);
    }

    // Offsets of ERM_BRound and the stat behind each command
    static BattleStackStat? Stat(char letter) => letter switch
    {
        'T' => BattleStackStat.Type, 'N' => BattleStackStat.Count, 'L' => BattleStackStat.HitPointsLost,
        'B' => BattleStackStat.CountAtStart, 'E' => BattleStackStat.Casts, 'I' => BattleStackStat.Side,
        'A' => BattleStackStat.Attack, 'D' => BattleStackStat.Defence, 'H' => BattleStackStat.HitPoints,
        'S' => BattleStackStat.Speed, 'F' => BattleStackStat.Flags, 'O' => BattleStackStat.ArmySlot,
        'R' => BattleStackStat.Retaliations, 'P' => BattleStackStat.Position,
        _ => null,
    };

    protected override void Run(ErmCall c)
    {
        var battle = c.Rt.Services.Game.Battle;
        int mn = c.Selector(0);
        if (mn < -1 || mn > 41) throw new ErmRuntimeException("\"!!BM:\"-monster index is incorrect (-1, 0...41).");
        if (mn == -1) mn = c.Need(battle.CurrentStack());
        BattleStackStat? stat = Stat(c.Letter);
        switch (c.Letter)
        {
            case 'U': // U1/$ min damage, U2/$ max damage, U3/$ shots, U4 spell to cast, U5 clone
                switch (c.N(0))
                {
                    case 1: stat = BattleStackStat.DamageLow; break;
                    case 2: stat = BattleStackStat.DamageHigh; break;
                    case 3: stat = BattleStackStat.Shots; break;
                    case 4: case 5: throw new ErmUnsupportedException(Spells);
                    default: throw new ErmRuntimeException("wrong syntax");
                }
                Value(c, battle, mn, stat.Value, 1);
                return;
            case 'J': // ?$ the number of active spells
                throw new ErmUnsupportedException(Kept);
            case 'G': case 'C': case 'K': case 'M': case 'Q': case 'V':
                throw new ErmUnsupportedException(Spells);
        }
        if (stat == null) throw new ErmRuntimeException("wrong command");
        Value(c, battle, mn, stat.Value, 0);
    }

    static void Value(ErmCall c, Core.Adapters.IBattleAdapter battle, int mn, BattleStackStat stat, int i)
    {
        int cur = c.Need(battle.GetStack(mn, stat)), v = cur;
        if (c.Apply(ref v, i) || v == cur) return;
        c.Need(battle.SetStack(mn, stat, v));
    }
}

/// <summary>!!BG — the action of a stack in !?BG (Monsters.cpp, the G2B_* action of the battle manager).</summary>
public sealed class BgReceiver : ErmReceiverBase
{
    const string Change = "BG — changing a stack's action, its target or spell: Olden Era reports the action as it starts";

    public BgReceiver() : base("BG")
    {
        Declare("ANQHE", CompatLevel.PartiallySupported,
            "BG:A/N/Q/H/E — the action in !?BG (the hero's spell, walk, defend, attack, shoot, wait, a monster's spell, no action; Olden Era's turn events), the acting stack, its side, its hero and the targeted stack; read only");
        Declare("DSX", CompatLevel.Unsupported, "BG:D/S/X — the destination position, the spell and the second position of an action are not mapped yet");
    }

    protected override void Run(ErmCall c)
    {
        var battle = c.Rt.Services.Game.Battle;
        var b = c.Need(battle.GetBattle());
        if (b.CompleteAi) throw new ErmRuntimeException("ERROR! Attempt to use \"!!BG\" in non-human battle (use flag 1000 for checking).");
        if (b.ActionStack < 0) throw new ErmRuntimeException("\"!!BG\"-no action of a stack (use it in !?BG).");
        int side = b.ActionSide >= 0 ? b.ActionSide : b.ActionStack / 21;
        switch (c.Letter)
        {
            case 'A':
                if (b.ActionType < 0) throw new ErmUnsupportedException("BG:A — this kind of Olden Era action has no H3 action type mapped");
                MapSelector.ReadOnly(c, b.ActionType, 0, Change);
                break;
            case 'N': MapSelector.ReadOnly(c, b.ActionStack, 0, Change); break;
            case 'Q': MapSelector.ReadOnly(c, side, 0, Change); break;
            case 'H': MapSelector.ReadOnly(c, b.Heroes[side], 0, Change); break;
            case 'E': MapSelector.ReadOnly(c, b.ActionTarget, 0, Change); break;
            case 'D':
            case 'S':
            case 'X':
                throw new ErmUnsupportedException("BG:D/S/X — the destination position, the spell and the second position of an action are not mapped yet");
            default:
                throw ErmCall.WrongCommand(c.Letter);
        }
    }
}

/// <summary>!!MF — a stack taking physical damage in !?MF1 (Monsters.cpp ERM_MonFeature).</summary>
public sealed class MfReceiver : ErmReceiverBase
{
    const string Dealt = "MF:E/F — Olden Era reports the damage after it is dealt, so it cannot be changed or cancelled yet";

    public MfReceiver() : base("MF")
    {
        Declare("DNW", CompatLevel.PartiallySupported, "MF:D/N/W — the damage, the stack taking it, the kind of attacker (0); Olden Era reports the damage after it is dealt");
        Declare("EF", CompatLevel.PartiallySupported, "MF:E/F — enabled (1) and the corrected damage read as dealt; changing them is not possible when the damage is reported after it is dealt");
    }

    protected override void Run(ErmCall c)
    {
        var b = c.Need(c.Rt.Services.Game.Battle.GetBattle());
        if (b.DamageStack < 0) throw new ErmRuntimeException("\"!!MF\"-no damage (use it in !?MF1).");
        switch (c.Letter)
        {
            case 'D': MapSelector.ReadOnly(c, b.Damage, 0, Dealt); break;
            case 'F': MapSelector.ReadOnly(c, b.Damage, 0, Dealt); break;
            case 'E': MapSelector.ReadOnly(c, 1, 0, Dealt); break;
            case 'N': MapSelector.ReadOnly(c, b.DamageStack, 0, Dealt); break;
            case 'W': MapSelector.ReadOnly(c, 0, 0, Dealt); break;
            default: throw ErmCall.WrongCommand(c.Letter);
        }
    }
}
