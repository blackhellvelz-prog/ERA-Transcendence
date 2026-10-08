using System.Linq;
using WoG.Core.Compat;
using WoG.Core.Model;
using WoG.Core.State;
using WoG.Erm.Runtime;

namespace WoG.Erm.Receivers;

/// <summary>Map squares addressed like WoG's GetDinMixPos: x/y/l, or one number = index of v[i], v[i+1], v[i+2].</summary>
internal static class MapSelector
{
    public static MapPos Resolve(ErmCall c)
    {
        switch (c.SelectorCount)
        {
            case 3:
                return new MapPos(c.Selector(0), c.Selector(1), c.Selector(2));
            case 1:
            {
                int ind = c.Selector(0);
                if (ind < 1 || ind > 9998) throw new ErmRuntimeException("Index of var for Dinamic position is out of range (1...9998).");
                var v = c.Rt.Services.State.Erm.V;
                return new MapPos(v[ind - 1], v[ind], v[ind + 1]);
            }
            default:
                throw new ErmRuntimeException("wrong number of parameters");
        }
    }

    /// <summary>
    /// Apply() on a value the target engine cannot change: get and check work; a "set" that leaves the value as it is
    /// (such as a bare "d") is accepted; a real change is unsupported.
    /// </summary>
    public static void ReadOnly(ErmCall c, int value, int i, string what)
    {
        int v = value;
        if (!c.Apply(ref v, i) && v != value) throw new ErmUnsupportedException(what);
    }
}

/// <summary>!!OB — the generic object receiver (erm.cpp ERM_SetObject).</summary>
public sealed class ObReceiver : ErmReceiverBase
{
    const string ChangeType = "OB:T/U — changing the type of an object: Olden Era objects keep their own type";

    public ObReceiver() : base("OB")
    {
        Declare("TU", CompatLevel.PartiallySupported, "OB:T/U — the type and subtype of the object on a square (Format OB via id-maps/object.json); they cannot be changed");
        Declare("C", CompatLevel.Unsupported, "OB:C — the control word is H3's object setup data: different engine");
        Declare("DERSMHB", CompatLevel.Unsupported, "OB:D/E/R/S/M/H/B — disabling objects, auto-answers and hints need the object visit hook (not verified yet)");
    }

    protected override void Run(ErmCall c)
    {
        var pos = MapSelector.Resolve(c);
        var game = c.Rt.Services.Game;
        switch (c.Letter)
        {
            case 'T':
            case 'U':
            {
                var sq = c.Need(game.Map.GetSquare(pos));
                int type = sq.ObjectType, sub = sq.ObjectSubtype;
                // A hero on the square is the object there (OB:T#/1 asks for the object under it).
                bool under = c.Letter == 'T' && c.Num >= 2 && c.N(1) == 1;
                if (!under && game.Heroes.HeroAt(pos) is { Status: Core.Adapters.AdapterStatus.Ok } h)
                    (type, sub) = (34, h.Value);
                MapSelector.ReadOnly(c, c.Letter == 'T' ? type : sub, 0, ChangeType);
                break;
            }
            case 'C':
                throw new ErmUnsupportedException("OB:C — the control word is H3's object setup data: different engine");
            default:
                if ("DERSMHB".IndexOf(c.Letter) >= 0)
                    throw new ErmUnsupportedException("OB:D/E/R/S/M/H/B — disabling objects, auto-answers and hints need the object visit hook (not verified yet)");
                throw ErmCall.WrongCommand(c.Letter);
        }
    }
}

/// <summary>!!TR — the terrain receiver (erm.cpp ERM_Terrain).</summary>
public sealed class TrReceiver : ErmReceiverBase
{
    const string Change = "TR:T/P/E — changing terrain, passability or entrances: Olden Era's map is not changed by ERM yet";

    public TrReceiver() : base("TR")
    {
        Declare("TPE", CompatLevel.PartiallySupported,
            "TR:T/P/E — terrain (Olden Era biomes as the H3 terrain of the matching town), road, blocked (red) and entrance (yellow) squares; read only; rivers are 0");
        Declare("G", CompatLevel.Unsupported, "TR:G — H3 terrain overlays (magic plains, cursed ground…) have no Olden Era equivalent mapped");
        Declare("V", CompatLevel.Unsupported, "TR:V — square visibility (fog of war) is not mapped yet");
    }

    protected override void Run(ErmCall c)
    {
        var pos = MapSelector.Resolve(c);
        var map = c.Rt.Services.Game.Map;
        switch (c.Letter)
        {
            case 'T':
            {
                c.RequireExactly(8);
                var sq = c.Need(map.GetSquare(pos));
                int attrib = (sq.Blocked ? 0x01 : 0) | (sq.Entrance ? 0x10 : 0);
                int[] values = { sq.Land, 0, 0, 0, sq.Road, 0, 0, attrib };
                for (int i = 0; i < 8; i++) MapSelector.ReadOnly(c, values[i], i, Change);
                break;
            }
            case 'P':
            {
                c.RequireExactly(1);
                var sq = c.Need(map.GetSquare(pos));
                MapSelector.ReadOnly(c, sq.Blocked ? 0 : 1, 0, Change);
                break;
            }
            case 'E':
            {
                var sq = c.Need(map.GetSquare(pos));
                if (c.Num == 3)
                {
                    // GetObjectEntrance: the entrance of the object covering the square
                    var e = sq.ObjectEntrance.IsNone ? pos : sq.ObjectEntrance;
                    int x = e.X, y = e.Y, l = e.L;
                    c.Apply(ref x, 0);
                    c.Apply(ref y, 1);
                    c.Apply(ref l, 2);
                    break;
                }
                c.RequireExactly(1);
                MapSelector.ReadOnly(c, sq.Entrance ? 0 : 1, 0, Change);
                break;
            }
            case 'G':
                throw new ErmUnsupportedException("TR:G — H3 terrain overlays (magic plains, cursed ground…) have no Olden Era equivalent mapped");
            case 'V':
                throw new ErmUnsupportedException("TR:V — square visibility (fog of war) is not mapped yet");
            default:
                throw ErmCall.WrongCommand(c.Letter);
        }
    }
}

/// <summary>!!PO — WoG's own data of a map square (erm.cpp ERM_Position), kept in the WoG state.</summary>
public sealed class PoReceiver : ErmReceiverBase
{
    public PoReceiver() : base("PO")
    {
        Declare("HONTSCVB", CompatLevel.FullySupported, "PO — WoG data of a map square, kept in the WoG state and saved with it");
    }

    protected override void Run(ErmCall c)
    {
        var pos = MapSelector.Resolve(c);
        var (size, levels) = c.Need(c.Rt.Services.Game.Map.GetSize());
        if (pos.X < 0 || pos.X >= size) throw new ErmRuntimeException("\"!!PO\"-wrong position (x).");
        if (pos.Y < 0 || pos.Y >= size) throw new ErmRuntimeException("\"!!PO\"-wrong position (y).");
        if (pos.L < 0 || pos.L > levels) throw new ErmRuntimeException("\"!!PO\"-wrong position (l).");
        var squares = c.Rt.Services.State.Squares;
        int key = pos.Pack();
        var sq = squares.TryGetValue(key, out var found) ? found : new PoSquare();
        // The bit fields of _Square_ keep only their low bits.
        int Field(int value, int bits, bool signed)
        {
            int mask = (1 << bits) - 1, v = value & mask;
            return signed && v > mask >> 1 ? v - (mask + 1) : v;
        }
        int Bits(int value, int i, int bits, bool signed = false)
        {
            c.RequireMin(1);
            c.Apply(ref value, i);
            return Field(value, bits, signed);
        }
        switch (c.Letter)
        {
            case 'H': sq.Hero = Bits(sq.Hero, 0, 8); break;
            case 'O': sq.Owner = Bits(sq.Owner, 0, 4, signed: true); break;
            case 'N': sq.Number = Bits(sq.Number, 0, 4); break;
            case 'T': sq.NumberT = Bits(sq.NumberT, 0, 8); break;
            case 'S': sq.NumberS = Bits(sq.NumberS, 0, 8); break;
            case 'C': // Ct/st/h/o/n (-1 = any): how many squares match, into v1
            {
                c.RequireMin(5);
                var want = new[] { c.N(0), c.N(1), c.N(2), c.N(3), c.N(4) };
                bool Match(PoSquare q) =>
                    (want[0] == -1 || q.NumberT == want[0]) && (want[1] == -1 || q.NumberS == want[1])
                    && (want[2] == -1 || q.Hero == want[2]) && (want[3] == -1 || q.Owner == want[3])
                    && (want[4] == -1 || q.Number == want[4]);
                int total = size * size * (levels + 1);
                int stored = squares.Count, matched = squares.Values.Count(Match);
                if (Match(new PoSquare())) matched += total - stored; // the squares nobody touched
                c.Rt.Services.State.Erm.V[0] = matched;
                return;
            }
            case 'V': // V#/$ — four shorts
            case 'B': // B#/$ — two longs
            {
                c.RequireMin(2);
                string name = c.Letter == 'V' ? "\"!!PO:V\"" : "\"!!PO:B\"";
                if (c.IsGetOrCheck(0)) throw new ErmRuntimeException(name + "-you cannot use get or check syntax for the first argument.");
                int i = c.N(0), n = c.Letter == 'V' ? 4 : 2;
                if (i < 0 || i >= n) throw new ErmRuntimeException(name + $"-wrong index (0...{n - 1}).");
                if (c.Letter == 'V') { int v = sq.S[i]; c.Apply(ref v, 1); sq.S[i] = (short)v; }
                else { int v = sq.L[i]; c.Apply(ref v, 1); sq.L[i] = v; }
                break;
            }
            default:
                throw ErmCall.WrongCommand(c.Letter);
        }
        if (sq.IsStart()) squares.Remove(key);
        else squares[key] = sq;
    }
}

/// <summary>!!MN — a mine or a lighthouse (erm.cpp ERM_Mine).</summary>
public sealed class MnReceiver : ErmReceiverBase
{
    const string Resource = "MN:R — changing what a mine produces: an Olden Era mine is its own object type";

    public MnReceiver() : base("MN")
    {
        Declare("O", CompatLevel.PartiallySupported, "MN:O — the owner of a mine; setting it is the game's change of owner (flag, income)");
        Declare("R", CompatLevel.PartiallySupported, "MN:R — the resource a mine produces (id-maps/object.json); it cannot be changed");
        Declare("M", CompatLevel.PartiallySupported, "MN:M — the guards a mine keeps itself; an Olden Era mine has none (it is guarded by squads on the map), so they read as empty and cannot be set");
    }

    protected override void Run(ErmCall c)
    {
        var pos = MapSelector.Resolve(c);
        var game = c.Rt.Services.Game;
        var obj = game.Map.GetObjectAt(pos);
        if (obj.Status == Core.Adapters.AdapterStatus.Unsupported) c.Need(obj);
        if (!obj.IsOk || obj.Value.type is not (53 or 42)) throw new ErmRuntimeException("\"!!MN:\"-not a mine.");
        switch (c.Letter)
        {
            case 'O': // O$ owner (-2 = the current player), O$/1 without redrawing
            {
                int cur = c.Need(game.Map.GetObjectOwner(pos)), owner = cur;
                if (!c.IsGetOrCheck(0))
                {
                    // ERM_Mine puts the resolved owner into the parameter before Apply
                    if (c.N(0) == -2) owner = game.Players.CurrentPlayer;
                    else if (c.N(0) < -1 || c.N(0) > 7) throw new ErmRuntimeException("\"!!MN:O\"-Owner out of range (-1...7).");
                    else c.Apply(ref owner, 0);
                }
                else c.Apply(ref owner, 0);
                if (owner != cur) c.Need(game.Map.SetObjectOwner(pos, owner));
                break;
            }
            case 'R': // R$ resource (0..6, 100)
            {
                if (!c.IsGetOrCheck(0) && (c.N(0) < 0 || c.N(0) > 6) && c.N(0) != 100)
                    throw new ErmRuntimeException("\"!!MN:R\"-Resource type out of range (0...6,100).");
                MapSelector.ReadOnly(c, obj.Value.subtype, 0, Resource);
                break;
            }
            case 'M': // M#/$type/$count guards
            {
                c.RequireMin(3);
                int slot = c.N(0);
                if (slot < 0 || slot > 7) throw new ErmRuntimeException("\"!!MN:M\"-wrong slot number (0...7).");
                var st = c.Need(game.Map.GetObjectGuard(pos, slot));
                int type = st.Type, count = st.Count;
                bool get = c.Apply(ref type, 1) & c.Apply(ref count, 2);
                if (!get && (type != st.Type || count != st.Count)) c.Need(game.Map.SetObjectGuard(pos, slot, type, count));
                break;
            }
            default:
                throw new ErmRuntimeException("wrong command");
        }
    }
}
