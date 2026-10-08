using System;
using WoG.Core.Compat;
using WoG.Core.H3Data;
using WoG.Core.Model;
using WoG.Erm.Runtime;

namespace WoG.Erm.Receivers;

/// <summary>
/// !!SS — the spell receiver (spell.cpp ERM_Spell, WoG 3.58 TE and ERA): the H3 spell table of the installation
/// (sptraits.txt; target, animation and flags from its executable). A change is kept with the WoG state, as WoG keeps
/// it with the saved game (SaveSpells), and read back; Olden Era's own spells do not change yet.
/// </summary>
public sealed class SsReceiver : ErmReceiverBase
{
    const string NotInGame = "SS — the change is kept with the WoG state and read back, but Olden Era's spells do not change yet";
    const string NoExe = "SS:O/X/F — the spell table of the ERA executable was not read (h3era.exe)";

    public SsReceiver() : base("SS")
    {
        Declare("LSCPEHI", CompatLevel.PartiallySupported,
            "SS — the H3 spell table of the ERA installation (sptraits.txt): level, schools, costs, power, effects, guild chances, AI values; a change is kept with the WoG state and read back, Olden Era's spells do not change yet");
        Declare("OXF", CompatLevel.PartiallySupported,
            "SS:O/X/F — target, animation and flags from the spell table of the ERA executable (h3era.exe); a change is kept and read back, Olden Era's spells do not change yet");
        Declare("NADW", CompatLevel.PartiallySupported,
            "SS:N/A/D/W — the z variable of a text a script gave (0 = the original), as WoG 3.58; Olden Era shows its own spell texts");
    }

    protected override void Run(ErmCall c)
    {
        var s = c.Rt.Services;
        int id = c.Selector(0);
        if (id < 0 || id >= H3Tables.SpellCount) throw new ErmRuntimeException("\"SS\"- incorrect spell index.");
        var table = s.H3.SpellTable;
        if (id >= table.Count) throw new ErmUnsupportedException("SS — the ERA installation's sptraits.txt was not read");
        var spell = s.State.SpellOverrides.TryGetValue(id, out var own) ? own : table[id];
        bool changed = false;

        // Apply on one field; a set that changes the value goes into the WoG state's own copy of the spell.
        void Field(Func<WoGSpell, int> get, Action<WoGSpell, int> set, int param)
        {
            int v = get(spell);
            if (c.Apply(ref v, param) || v == get(spell)) return;
            if (!s.State.SpellOverrides.TryGetValue(id, out var copy))
                s.State.SpellOverrides[id] = copy = table[id].Clone();
            set(copy, v);
            spell = copy;
            changed = true;
        }

        int Index(int max, string cmd)
        {
            c.RequireMin(2);
            int k = c.N(0);
            if (k < 0 || k > max) throw new ErmRuntimeException($"\"SS:{cmd}\"- wrong index.");
            return k;
        }

        void NeedExe()
        {
            if (!spell.FromExecutable) throw new ErmUnsupportedException(NoExe);
        }

        switch (c.Letter)
        {
            case 'O': NeedExe(); Field(x => x.Target, (x, v) => x.Target = v, 0); break;
            case 'X': NeedExe(); Field(x => x.DefIndex, (x, v) => x.DefIndex = v, 0); break;
            case 'F': NeedExe(); Field(x => x.Flags, (x, v) => x.Flags = v, 0); break;
            case 'L': Field(x => x.Level, (x, v) => x.Level = v, 0); break;
            case 'S': Field(x => x.Schools, (x, v) => x.Schools = v, 0); break;
            case 'P': Field(x => x.Power, (x, v) => x.Power = v, 0); break;
            case 'C': { int k = Index(3, "C"); Field(x => x.Cost[k], (x, v) => x.Cost[k] = v, 1); break; }
            case 'E': { int k = Index(3, "E"); Field(x => x.Effect[k], (x, v) => x.Effect[k] = v, 1); break; }
            case 'H': { int k = Index(8, "H"); Field(x => x.Chance[k], (x, v) => x.Chance[k] = v, 1); break; }
            case 'I': { int k = Index(3, "I"); Field(x => x.AiValue[k], (x, v) => x.AiValue[k] = v, 1); break; }
            // texts: WoG 3.58 keeps the z variable a script set (ZVars[spell][0..6]) and gives it back; 0 = none
            case 'N': Text(0, 0); break;
            case 'A': Text(1, 0); break;
            case 'D': { int k = Index(3, "D"); Text(2 + k, 1); break; }
            case 'W': Text(6, 0); break;
            default: throw ErmCall.WrongCommand(c.Letter);
        }
        if (changed) throw new ErmUnsupportedException(NotInGame);
        return;

        void Text(int slot, int param)
        {
            int v = spell.TextVars[slot];
            if (c.Apply(ref v, param) || v == spell.TextVars[slot]) return;
            if (!s.State.SpellOverrides.TryGetValue(id, out var copy))
                s.State.SpellOverrides[id] = copy = table[id].Clone();
            copy.TextVars[slot] = v;
            spell = copy;
            changed = true;
        }
    }
}
