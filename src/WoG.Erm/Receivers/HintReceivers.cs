using WoG.Core.Compat;
using WoG.Core.Model;
using WoG.Core.State;
using WoG.Erm.Runtime;
using WoG.Erm.Syntax;

namespace WoG.Erm.Receivers;

/// <summary>!!HT — hints of map objects by type and subtype (erm.cpp ERM_HintType); <see cref="MapHints"/> shows them.</summary>
public sealed class HtReceiver : ErmReceiverBase
{
    public const string Note =
        "HT — the hint of objects of a type and subtype (T), by the square's PO:N number (P), the active hero's w variable (W) or a v variable (V), kept with the WoG state in WoG's order and shown in Olden Era's map tooltip in place of the object's name (objects that have one: resource piles; buildings and monsters have none)";

    public HtReceiver() : base("HT")
    {
        Declare("TPWV", CompatLevel.PartiallySupported, Note);
    }

    protected override void Run(ErmCall c)
    {
        if (c.SelectorCount != 2) throw new ErmRuntimeException("\"!!HT:\"-wrong syntax (the command requires 2 parameters).");
        uint type = (uint)c.Selector(0) & 0xFF;
        int sub = c.Selector(1);
        uint subtype = (sub == -1 ? 0xFFu : (uint)sub) & 0xFF;
        uint all = (type << 8) + subtype;
        int val = 0, ind = 0;
        switch (c.Letter)
        {
            case 'T': // Tz (z = 0 deletes)
                MapHints.ChangeTypeHint(c, all + 0x10000000, 0);
                break;
            case 'P': // Pnumber/z: the square's PO:N number 0..15
                c.RequireMin(2);
                if (c.Apply(ref val, 0)) return;
                if (val < 0 || val > 15) throw new ErmRuntimeException("\"!!HT:P\"-Number value out of range (0...15).");
                MapHints.ChangeTypeHint(c, all + 0x20000000 + (((uint)val & 0x0F) << 16), 1);
                break;
            case 'W': // Wind/val/z: w1..w100 of the active hero = 0..32
                c.RequireMin(3);
                if (c.Apply(ref ind, 0)) return;
                if (ind < 1 || ind > 100) throw new ErmRuntimeException("\"!!HT:W\"- W index value out of range (1...100).");
                if (c.Apply(ref val, 1)) return;
                if (val < 0 || val > 32) throw new ErmRuntimeException("\"!!HT:W\"- W value out of range (0...32).");
                MapHints.ChangeTypeHint(c, all + 0x40000000 + (((uint)(ind - 1) & 0x7F) << 16) + (((uint)val & 0x1F) << 23), 2);
                break;
            case 'V': // Vind/val/z: v1..v1000 = 0..3
                c.RequireMin(3);
                if (c.Apply(ref ind, 0)) return;
                if (ind < 1 || ind > 1000) throw new ErmRuntimeException("\"!!HT:V\"- V index value out of range (1...1000).");
                if (c.Apply(ref val, 1)) return;
                if (val < 0 || val > 3) throw new ErmRuntimeException("\"!!HT:V\"- V value out of range (0...3).");
                MapHints.ChangeTypeHint(c, (all + ((((uint)(ind - 1) & 0x3FF) << 16) & 0x3FF0000) + ((((uint)val & 0x3) << 26) & 0x0C000000)) | 0x80000000, 2);
                break;
            default:
                throw ErmCall.WrongCommand(c.Letter);
        }
    }
}

/// <summary>
/// WoG's hints of ERM objects (erm.cpp ObjChangeHint, StrMan::Apply) and the hint the adventure map shows for an object
/// (Era's Hook_ZvsCheckObjHint, then WoG's CheckObjHint and FindObjExt).
/// </summary>
public static class MapHints
{
    /// <summary>ERMOBJECTS: WoG's ERM objects (squares and HT hints together).</summary>
    public const int MaxObjects = 1000;


    /// <summary>
    /// StrMan::Apply on a hint: a string (^text^ or a z variable) is copied as the hint's own text; a number is the z
    /// variable whose text shows (0 removes the hint); '?' gives that number (negative for an own text), a check
    /// compares it. Returns false for get and check.
    /// </summary>
    internal static bool Apply(ErmCall c, ref ErmHint? hint, int i)
    {
        var p = c.P(i);
        int number = hint == null ? 0 : hint.Text != null ? -1 : hint.Z;
        if (p.Mode != ErmParamMode.Set) { c.Apply(ref number, i); return false; }
        bool isText = c.Rt.IsEra ? ErmRuntime.EraIsString(p) : p.Var?.Kind == ErmVarKind.Z || (p.Empty && c.Cmd.Text != null);
        if (isText)
        {
            string text = c.Rt.IsEra ? c.Rt.EraGetText(p)
                : p.Var?.Kind == ErmVarKind.Z ? c.Rt.GetStringRaw(c.Rt.ResolveIndex(p.Var)) : c.Rt.Interpolate(c.Cmd.Text!);
            hint = new ErmHint { Text = text };
            return true;
        }
        int z = number < 0 ? 0 : number;
        c.Apply(ref z, i);
        if (z < 0) throw new ErmRuntimeException("wrong z var index (1...1000+).");
        hint = z == 0 ? null : new ErmHint { Z = z };
        return true;
    }

    /// <summary>ObjChangeHint for an HT code: the slot of the code (a new one takes the first free slot), freed without a hint.</summary>
    internal static void ChangeTypeHint(ErmCall c, uint code, int i)
    {
        var slots = c.Rt.Services.State.TypeHints;
        int at = slots.FindIndex(h => h != null && h.Code == code);
        ErmHint? hint = at >= 0 ? slots[at]!.Hint : null;
        if (!Apply(c, ref hint, i)) return;
        if (hint == null)
        {
            if (at >= 0) slots[at] = null;
            return;
        }
        if (at >= 0) slots[at]!.Hint = hint;
        else
        {
            at = slots.IndexOf(null);
            if (at < 0)
            {
                if (slots.Count + c.Rt.Services.State.Objects.Count >= MaxObjects) throw new ErmRuntimeException("Cannot add more objects");
                slots.Add(null);
                at = slots.Count - 1;
            }
            slots[at] = new ErmTypeHint { Code = code, Hint = hint };
        }
    }

    /// <summary>OB:H — the hint of the object on a square (ObjChangeHint on its position).</summary>
    internal static void ChangeSquareHint(ErmCall c, MapPos pos, int i)
    {
        var objects = c.Rt.Services.State.Objects;
        objects.TryGetValue(pos.Pack(), out var obj);
        var hint = obj?.Hint;
        if (!Apply(c, ref hint, i)) return;
        SetSquareHint(objects, pos, hint);
    }

    /// <summary>The hint of a square; an object left with nothing is deleted (CheckDelObj).</summary>
    internal static void SetSquareHint(System.Collections.Generic.Dictionary<int, ErmObjectState> objects, MapPos pos, ErmHint? hint)
    {
        if (!objects.TryGetValue(pos.Pack(), out var obj))
        {
            if (hint == null) return;
            objects[pos.Pack()] = obj = new ErmObjectState();
        }
        obj.Hint = hint;
        if (obj.Hint == null && obj.DisabledMask == 0) objects.Remove(pos.Pack());
    }

    /// <summary>
    /// The hint the adventure map shows for an object (H3 type and subtype, the object under a hero standing on its
    /// entrance), or null for the game's own: Era's SN:H hint of the square, the type and subtype, the type, the
    /// subtype or any object when the human player sees the square; then WoG's OB:H hint of the square; then the HT
    /// hints in slot order, first of the exact subtype, then of any subtype (P by the square's PO:N number, W by the
    /// active hero's w variable — none without an active hero, V by a v variable, T always).
    /// </summary>
    public static string? ForObject(ErmRuntime rt, MapPos pos, int type, int subtype, bool visible, int activeHero)
    {
        var st = rt.Services.State;
        if (visible && st.Hints.TryGetValue("object", out var era) && pos.X is >= 0 and <= 255 && pos.Y is >= 0 and <= 255)
        {
            if (era.TryGetValue(pos.X | (pos.Y << 8) | (pos.L << 16), out var hint)) return hint;
            if (type is >= -1 and <= 254 && subtype is >= -1 and <= 254)
            {
                int t = type & 0xFF, s = subtype & 0xFF, ts = Era.H3Texts.CodeTypeSubtype;
                foreach (int code in new[] { t | (s << 8) | ts, t | 0xFF00 | ts, (s << 8) | 0xFF | ts, 0xFFFF | ts })
                    if (era.TryGetValue(code, out hint)) return hint;
            }
        }
        if (st.Objects.TryGetValue(pos.Pack(), out var obj) && obj.Hint != null) return Text(rt, obj.Hint);
        var found = FindTypeHint(st, pos, type, subtype, activeHero);
        return found == null ? null : Text(rt, found);
    }

    /// <summary>FindObjExt: the first HT slot that matches, the exact subtype first.</summary>
    static ErmHint? FindTypeHint(WoGGameState st, MapPos pos, int type, int subtype, int activeHero)
    {
        for (int pass = 0; pass < 2; pass++)
        {
            uint all = (((uint)type & 0xFF) << 8) + (pass == 0 ? (uint)subtype & 0xFF : 0xFF);
            foreach (var h in st.TypeHints)
            {
                if (h == null || (h.Code & 0xFFFF) != all) continue;
                switch (h.Code & 0xF0000000)
                {
                    case 0x20000000:
                        int number = st.Squares.TryGetValue(pos.Pack(), out var sq) ? sq.Number : 0;
                        if (number == (h.Code & 0x000F0000) >> 16) return h.Hint;
                        break;
                    case 0x40000000:
                        if (activeHero < 0) continue;
                        uint w = (h.Code & 0x007F0000) >> 16, wval = (h.Code & 0x0F800000) >> 23;
                        if (st.Erm.WFor(activeHero)[w] == wval) return h.Hint;
                        break;
                    case 0x80000000:
                        uint v = (h.Code & 0x03FF0000) >> 16, vval = (h.Code & 0x0C000000) >> 26;
                        if (st.Erm.V[v] == vval) return h.Hint;
                        break;
                    case 0x10000000:
                        return h.Hint;
                }
            }
        }
        return null;
    }

    /// <summary>A hint's text: its own as it is, a z variable's with its codes (ERM2String).</summary>
    static string Text(ErmRuntime rt, ErmHint hint) =>
        hint.Text ?? (rt.IsEra ? rt.InterpolateEra(rt.EraZRaw(hint.Z)) : rt.Interpolate(rt.GetStringRaw(hint.Z)));
}
