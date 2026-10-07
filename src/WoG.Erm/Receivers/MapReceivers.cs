using WoG.Core.Compat;
using WoG.Core.Model;
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
