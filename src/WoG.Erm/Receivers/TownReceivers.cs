using WoG.Core.Compat;
using WoG.Core.Model;
using WoG.Erm.Runtime;

namespace WoG.Erm.Receivers;

/// <summary>!!CA — a town (casdem.cpp ERM_Castle).</summary>
public sealed class CaReceiver : ErmReceiverBase
{
    const string Moves = "CA:H/P/T/U — moving heroes into a town, moving a town, changing its type or number: not mapped";
    const string Summon = "CA:M3 — the creature of the summoning portal is not mapped yet";
    const string Battery = "CA:D — the recruitment window with several creatures of the Battery plugin (closed-source ERA DLL)";
    const string Looks = "CA:I — the ruined looks of a town (I-1, I1..I3) are not mapped; I0 (the look of its buildings) is what Olden Era shows";

    public CaReceiver() : base("CA")
    {
        Declare("B", CompatLevel.PartiallySupported,
            "CA:B — buildings by H3 number: dwellings, mage guild, fort/citadel/castle, village/town/city hall, tavern, marketplace, resource silo and grail are Olden Era's buildings (a higher level counts its lower ones as built); the other numbers read as not built and cannot be built; B1/B6 build through the game's construction, free and without using the day's one; B4/B5 allow or forbid; B3…/1 (bonus taken) reads as built; B2 is not possible: Olden Era has no demolition");
        Declare("M", CompatLevel.PartiallySupported,
            "CA:M — M1 creatures to hire (Olden Era keeps one number per dwelling: the row of the building that stands is used), M2 the town's own garrison (creature ids via IdMap), M4 weekly growth (read only, as in WoG); M3 (summoning portal) is not mapped");
        Declare("OGNRS", CompatLevel.PartiallySupported,
            "CA:O/G/N/R/S — owner, mage guild level and spells, name (Olden Era's own, localized), built today, daily gold income; see the matrix for what can be changed");
        Declare("HPTU", CompatLevel.PartiallySupported,
            "CA:H/P/T/U — garrison and visiting hero, position, town type (Olden Era factions as the closest H3 town), town number: read; moving heroes into a town, moving a town, changing its type or number are not mapped");
        Declare("I", CompatLevel.PartiallySupported, Looks);
        Declare("D", CompatLevel.Unsupported, Battery);
    }

    /// <summary>ERM_Castle's selector: CA0/n (town number), CA-1 (the open town), x/y/l or one v index.</summary>
    static int Town(ErmCall c)
    {
        var towns = c.Rt.Services.Game.Towns;
        if (c.SelectorCount == 2)
        {
            if (c.Selector(0) != 0) throw new ErmRuntimeException("\"CA#/#\"-first parameter out of range (0).");
            int n = c.Selector(1);
            if (n < 0 || n >= c.Need(towns.TownCount())) throw new ErmRuntimeException("\"CA$\"-cannot find castle (out of range).");
            return n;
        }
        if (c.SelectorCount == 1 && c.Selector(0) == -1) return c.Need(towns.CurrentTown());
        if (c.SelectorCount is not (1 or 3)) throw new ErmRuntimeException("\"CA???\"-incorrect syntax.");
        var r = towns.TownAt(MapSelector.Resolve(c));
        if (r.Status == Core.Adapters.AdapterStatus.Unsupported) c.Need(r);
        if (!r.IsOk) throw new ErmRuntimeException("\"!!CA\"-not a Castle.");
        return r.Value;
    }

    protected override void Run(ErmCall c)
    {
        int town = Town(c);
        var game = c.Rt.Services.Game;
        var towns = game.Towns;
        switch (c.Letter)
        {
            case 'H': // H0/$ garrison hero, H1/$ visitor
            {
                c.RequireMin(2);
                if (c.N(0) is not (0 or 1)) throw new ErmRuntimeException("\"!!CA:H\"-wrong first parameter (0,1)");
                bool visitor = c.N(0) == 1;
                int cur = c.Need(towns.GetTownHero(town, visitor)), hn = cur;
                if (c.Apply(ref hn, 1) || hn == cur) break;
                if (hn != -1 && (hn < 0 || hn >= WoGLimits.HeroCount)) throw new ErmRuntimeException("\"!!CA:H\"-wrong hero number");
                c.Need(towns.SetTownHero(town, visitor, hn));
                break;
            }
            case 'U': // U$ the town's number
                MapSelector.ReadOnly(c, town, 0, Moves);
                break;
            case 'I': // I$ redraw the town on the map (set only): -1..3
            {
                int i = 0;
                if (c.Apply(ref i, 0)) break;
                i = System.Math.Clamp(i, -1, 3);
                if (i != 0) throw new ErmUnsupportedException(Looks);
                break;
            }
            case 'N': // N^text^, Nz — the name
            {
                string name = c.Need(towns.GetTownName(town)), set = name;
                if (c.ApplyText(ref set, name, 0) || set == name) break;
                c.Need(towns.SetTownName(town, set));
                break;
            }
            case 'P': // P$/$/$ position
            {
                var p = c.Need(towns.GetTownPosition(town));
                MapSelector.ReadOnly(c, p.X, 0, Moves);
                MapSelector.ReadOnly(c, p.Y, 1, Moves);
                MapSelector.ReadOnly(c, p.L, 2, Moves);
                break;
            }
            case 'O': // O$ owner: a set goes through the game's change of owner
            {
                int cur = c.Need(towns.GetTownOwner(town)), owner = cur;
                if (c.Apply(ref owner, 0) || owner == cur) break;
                c.Need(towns.SetTownOwner(town, owner));
                break;
            }
            case 'T': // T$ type
                MapSelector.ReadOnly(c, c.Need(towns.GetTownType(town)), 0, Moves);
                break;
            case 'R': // R$ built this turn (0 may build, 1 may not)
                c.ApplyAdapter(towns.GetBuiltThisTurn(town), v => towns.SetBuiltThisTurn(town, v), 0);
                break;
            case 'G': // mage guild
                Guild(c, towns, town);
                break;
            case 'M': // creatures
                Creatures(c, towns, town);
                break;
            case 'B': // buildings
                Buildings(c, towns, town);
                break;
            case 'S': // S$ income (a set changes nothing, as in WoG)
            {
                c.RequireMin(1);
                int j = c.Need(towns.GetIncome(town));
                c.Apply(ref j, 0);
                break;
            }
            case 'D' when c.IsEra:
                throw new ErmUnsupportedException(Battery);
            default:
                throw new ErmRuntimeException("\"!!CA\"-incorrect command.");
        }
    }

    static void Guild(ErmCall c, Core.Adapters.ITownAdapter towns, int town)
    {
        switch (c.Num)
        {
            case 1: // G$ guild level
                c.ApplyAdapter(towns.GetMageGuildLevel(town), v => towns.SetMageGuildLevel(town, v), 0);
                break;
            case 2: // G$/$ spells at a level
            {
                int level = c.N(0);
                if (level < 0 || level > 4) throw new ErmRuntimeException("\"!!CA:G\"-A level of Magic Guild out of range (0...4).");
                c.ApplyAdapter(towns.GetGuildSpellCount(town, level), v => towns.SetGuildSpellCount(town, level, v), 1);
                break;
            }
            case 3: // G$/$/$ a spell
            {
                int level = c.N(0), slot = c.N(1);
                if (level < 0 || level > 4 || slot < 0 || slot > 5) throw new ErmRuntimeException("wrong parameter");
                c.ApplyAdapter(towns.GetGuildSpell(town, level, slot), v => towns.SetGuildSpell(town, level, slot, v), 2);
                break;
            }
            default:
                throw new ErmRuntimeException("\"!!CA:G\"-incorrect command type (1...3).");
        }
    }

    static void Creatures(ErmCall c, Core.Adapters.ITownAdapter towns, int town)
    {
        switch (c.N(0))
        {
            case 1: // M1/level/$basic/$upgraded creatures to hire (Words)
            {
                c.RequireMin(4);
                int level = c.N(1);
                if (level < 0 || level > 6) throw new ErmRuntimeException("\"!!CA:M\"-level out of range (0...6).");
                for (int row = 0; row < 2; row++)
                {
                    int cur = c.Need(towns.GetAvailable(town, level, row)), v = cur;
                    if (c.Apply(ref v, 2 + row)) continue;
                    v &= 0xFFFF;
                    if (v != cur) c.Need(towns.SetAvailable(town, level, row, v));
                }
                break;
            }
            case 2: // M2/slot/$type/$count the town's garrison
            {
                c.RequireMin(4);
                int slot = c.N(1);
                if (slot < 0 || slot > 6) throw new ErmRuntimeException("\"!!CA:M\"-position out of range (0...6).");
                var st = c.Need(towns.GetGuard(town, slot));
                int type = st.Type, count = st.Count;
                bool getType = c.Apply(ref type, 2), getCount = c.Apply(ref count, 3);
                if ((getType && getCount) || (type == st.Type && count == st.Count)) break;
                c.Need(towns.SetGuard(town, slot, type, count));
                break;
            }
            case 3: // M3/$type/$count the summoning portal
                c.RequireMin(3);
                throw new ErmUnsupportedException(Summon);
            case 4: // M4/level/$ weekly growth (the game's value; a set changes nothing)
            {
                c.RequireMin(3);
                int level = c.N(1);
                if (level < 0 || level > 6) throw new ErmRuntimeException("\"!!CA:M\"-level out of range (0...6).");
                int j = c.Need(towns.GetGrowth(town, level));
                c.Apply(ref j, 2);
                break;
            }
            default:
                throw new ErmRuntimeException("incorrect command type (1...4).");
        }
    }

    static void Buildings(ErmCall c, Core.Adapters.ITownAdapter towns, int town)
    {
        if (c.N(0) != 3 || c.Num != 3) c.RequireMin(2);
        int b = c.N(1);
        if (b < 0 || b >= WoGTown.Buildings) throw new ErmRuntimeException("\"!!CA:B\"-wrong building number (0...43).");
        switch (c.N(0))
        {
            case 1: c.Need(towns.SetBuilt(town, b, true)); break;
            case 2: c.Need(towns.SetBuilt(town, b, false)); break;
            case 3: // B3/b[/0 built, 1 bonus, 2 allowed] → flag 1
            {
                int check = c.Num < 3 ? 0 : c.N(2);
                if (check < 0 || check > 2) throw new ErmRuntimeException("\"!!CA:B3\"-wrong check number (0...2).");
                c.Rt.SetFlag(1, c.Need(towns.GetBuildingFlag(town, b, check)));
                break;
            }
            case 4: c.Need(towns.SetAllowed(town, b, true)); break;
            case 5: c.Need(towns.SetAllowed(town, b, false)); break;
            case 6: c.Need(towns.Build(town, b)); break;
            default: throw new ErmRuntimeException("\"!!CA:B\"-incorrect command type (1...5).");
        }
    }
}
