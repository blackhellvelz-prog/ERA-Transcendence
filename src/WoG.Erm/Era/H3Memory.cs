using System;
using System.Collections.Generic;
using WoG.Core.Model;
using WoG.Core.Services;
using WoG.Erm.Receivers;
using WoG.Erm.Runtime;

namespace WoG.Erm.Era;

/// <summary>
/// UN:C on a different engine (Erm.pas Hook_UN_C → ZvsApply on H3's memory): the H3 addresses scripts are known to
/// use, each read and written by what it means. An address with no known meaning stays unsupported, the address in
/// the reason.
/// </summary>
public sealed class H3Memory
{
    /// <summary>H3's variable holding the spell table's address (0x6854A0 in the executable; WoG and Era move the table).</summary>
    public const int SpellTableVariable = 0x687FA8;
    /// <summary>The virtual "address" of the spell table here: far from H3's real ones (see EraNativeLibrary.VirtualTownStructs).</summary>
    public const int VirtualSpellTable = 0x7D000000;
    public const int SpellSize = 0x88;

    /// <summary>
    /// H3's secondary skill effect tables (4 floats: none, basic, advanced, expert) in the executable's .rdata that
    /// "30 wog - enhanced secondary skills.erm" changes; its own comments name them (SoD: 5/10/15, 5/10/20, 5/10/15 %).
    /// </summary>
    public static readonly (int Address, int Skill, string Name)[] SkillTables =
    {
        (0x63EA08, 23, "Armorer"),
        (0x63EA48, 26, "Resistance"),
        (0x63EA78, 25, "Sorcery"),
    };

    /// <summary>
    /// H3 code and data that scripts read or patch for H3's own sake. Olden Era does not run that code, so a patch has
    /// nothing to change: a read gives the executable's bytes, a write is kept for the session and read back.
    /// </summary>
    public static readonly (int Address, int Size, string What)[] ExecutableBytes =
    {
        (0x4446DB, 4, "the operand of H3's code naming its spell table variable (wogify's FU7779 reads schools through it)"),
        (0x4AA9EF, 2, "H3's object deletion sound (the stdlibs jump over it while they walk the map)"),
        (0x401448, 2, "H3's screen width"),
        (0x40144F, 2, "H3's screen height (the HD mod changes it; towns may be renamed moves its hint by it)"),
        (0x71286D, 4, "the position of the town screen's hint"),
    };

    public const string SpellsNotInGame = "UN:C — a change of H3's spell table is kept with the WoG state like SS, but Olden Era's spells do not change yet";
    public const string SkillsNotInGame = "UN:C — a change of H3's skill effect table (Armorer, Resistance, Sorcery) is kept with the WoG state, but Olden Era's skills do not change yet";

    readonly IWoGServices s;
    /// <summary>Bytes scripts wrote over <see cref="ExecutableBytes"/> this session (H3 patches are made again on game enter).</summary>
    readonly Dictionary<int, byte> patched = new();

    public H3Memory(IWoGServices services) => s = services;

    /// <summary>UN:C(address)/(size)/value or UN:C(address)/(offset)/(size)/value (Hook_UN_C).</summary>
    public void UnC(ErmCall c)
    {
        if (c.Num < 3) throw new ErmRuntimeException("!!UN:C - insufficient parameters");
        for (int i = 0; i <= Math.Min(4, c.Num - 2); i++)
            if (i < c.Num - 1 && c.IsGetOrCheck(i))
                throw new ErmRuntimeException("!!UN:C - GET-syntax is not supported for any parameters, except the last one");
        int address = c.N(0), offset = 0, size = c.N(1), value = 2;
        if (c.Num >= 4) { offset = c.N(1); size = c.N(2); value = 3; }
        if (size is not (4 or 1 or 2 or -1 or -2 or -4))
            throw new ErmRuntimeException("!!UN:C - wrong Size parameter: " + size);
        int at = unchecked(address + offset), width = Math.Abs(size);

        // ZvsApply: a signed size extends the sign, an unsigned one does not
        uint raw = Read(at, width);
        int v = size switch { 1 => (sbyte)raw, -1 => (byte)raw, 2 => (short)raw, -2 => (ushort)raw, _ => (int)raw };
        if (c.Apply(ref v, value)) return;
        Write(at, width, unchecked((uint)v));
    }

    uint Read(int at, int width)
    {
        uint v = 0;
        for (int k = 0; k < width; k++)
        {
            if (!TryReadByte(unchecked(at + k), out byte b)) throw Unknown(at, k);
            v |= (uint)b << (8 * k);
        }
        return v;
    }

    void Write(int at, int width, uint v)
    {
        string? note = null;
        for (int k = 0; k < width; k++)
            if (!Writable(unchecked(at + k))) throw Unknown(at, k);
        for (int k = 0; k < width; k++)
            WriteByte(unchecked(at + k), (byte)(v >> (8 * k)), ref note);
        if (note != null) throw new ErmUnsupportedException(note);
    }

    ErmUnsupportedException Unknown(int at, int k)
    {
        int a = unchecked(at + k);
        string why = Region(a) switch
        {
            Kind.SpellText => "a text pointer of H3's spell table (SS:N/A/D give the texts)",
            Kind.SpellVariable => "H3's spell table variable: the table is not moved here",
            Kind.NoExecutable => "the ERA installation's executable (h3era.exe) was not read",
            _ => "no Olden Era meaning is known for it",
        };
        return new ErmUnsupportedException($"UN:C — H3's memory at {a} (0x{a:X}): {why}");
    }

    enum Kind { None, SpellVariable, Spell, SpellText, Skill, Executable, NoExecutable }

    Kind Region(int a)
    {
        int count = s.H3.SpellTable.Count;
        if (a >= VirtualSpellTable && a < VirtualSpellTable + count * SpellSize)
            return SpellField((a - VirtualSpellTable) % SpellSize & ~3) == null ? Kind.SpellText : Kind.Spell;
        if (a >= SpellTableVariable && a < SpellTableVariable + 4) return count > 0 ? Kind.SpellVariable : Kind.None;
        foreach (var t in SkillTables)
            if (a >= t.Address && a < t.Address + 16) return s.H3.Executable != null ? Kind.Skill : Kind.NoExecutable;
        foreach (var e in ExecutableBytes)
            if (a >= e.Address && a < e.Address + e.Size) return s.H3.Executable != null ? Kind.Executable : Kind.NoExecutable;
        return Kind.None;
    }

    bool Writable(int a) => Region(a) is Kind.Spell or Kind.Skill or Kind.Executable;

    bool TryReadByte(int a, out byte b)
    {
        b = 0;
        switch (Region(a))
        {
            case Kind.SpellVariable:
                b = (byte)(VirtualSpellTable >> (8 * (a - SpellTableVariable)));
                return true;
            case Kind.Spell:
            {
                int o = a - VirtualSpellTable, id = o / SpellSize, field = o % SpellSize;
                b = (byte)(SpellField(field & ~3)!.Value.Get(Spell(id)) >> (8 * (field & 3)));
                return true;
            }
            case Kind.Skill:
            {
                var (table, skill) = SkillAt(a);
                b = (byte)(SkillEffect(skill, table)[(a - table) / 4] >> (8 * ((a - table) & 3)));
                return true;
            }
            case Kind.Executable:
                if (patched.TryGetValue(a, out b)) return true;
                var bytes = s.H3.ReadExecutable(a, 1);
                if (bytes == null) return false;
                b = bytes[0];
                return true;
            default:
                return false;
        }
    }

    void WriteByte(int a, byte b, ref string? note)
    {
        switch (Region(a))
        {
            case Kind.Spell:
            {
                int o = a - VirtualSpellTable, id = o / SpellSize, field = o % SpellSize, shift = 8 * (field & 3);
                var (get, set) = SpellField(field & ~3)!.Value;
                int old = get(Spell(id)), v = (int)(((uint)old & ~(0xFFu << shift)) | ((uint)b << shift));
                if (v == old) return;
                set(SsReceiver.OwnSpell(s, id), v);
                note = SpellsNotInGame;
                return;
            }
            case Kind.Skill:
            {
                var (table, skill) = SkillAt(a);
                int level = (a - table) / 4, shift = 8 * ((a - table) & 3);
                var values = (int[])SkillEffect(skill, table).Clone();
                int v = (int)(((uint)values[level] & ~(0xFFu << shift)) | ((uint)b << shift));
                if (v == values[level]) return;
                values[level] = v;
                // a script that puts the original values back (OnGameLeave) leaves no change behind
                if (values.AsSpan().SequenceEqual(ExecutableEffect(table))) s.State.SkillEffects.Remove(skill);
                else s.State.SkillEffects[skill] = values;
                note = SkillsNotInGame;
                return;
            }
            case Kind.Executable:
                patched[a] = b;
                return;
        }
    }

    WoGSpell Spell(int id) => s.State.SpellOverrides.TryGetValue(id, out var own) ? own : s.H3.SpellTable[id];

    /// <summary>The int fields of WoG's _Spell_ (structs.h) that SS reads and writes; null for the text pointers.</summary>
    static (Func<WoGSpell, int> Get, Action<WoGSpell, int> Set)? SpellField(int o) => o switch
    {
        0x00 => (x => x.Target, (x, v) => x.Target = v),
        0x08 => (x => x.DefIndex, (x, v) => x.DefIndex = v),
        0x0C => (x => x.Flags, (x, v) => x.Flags = v),
        0x18 => (x => x.Level, (x, v) => x.Level = v),
        0x1C => (x => x.Schools, (x, v) => x.Schools = v),
        >= 0x20 and < 0x30 => (x => x.Cost[(o - 0x20) / 4], (x, v) => x.Cost[(o - 0x20) / 4] = v),
        0x30 => (x => x.Power, (x, v) => x.Power = v),
        >= 0x34 and < 0x44 => (x => x.Effect[(o - 0x34) / 4], (x, v) => x.Effect[(o - 0x34) / 4] = v),
        >= 0x44 and < 0x68 => (x => x.Chance[(o - 0x44) / 4], (x, v) => x.Chance[(o - 0x44) / 4] = v),
        >= 0x68 and < 0x78 => (x => x.AiValue[(o - 0x68) / 4], (x, v) => x.AiValue[(o - 0x68) / 4] = v),
        _ => null, // +4 sound, +0x10 name, +0x14 short name, +0x78 descriptions
    };

    static (int Table, int Skill) SkillAt(int a)
    {
        foreach (var t in SkillTables)
            if (a >= t.Address && a < t.Address + 16) return (t.Address, t.Skill);
        throw new InvalidOperationException();
    }

    /// <summary>A skill's effects as float bits: the WoG state's change, else the executable's table.</summary>
    int[] SkillEffect(int skill, int table) =>
        s.State.SkillEffects.TryGetValue(skill, out var own) ? own : ExecutableEffect(table);

    int[] ExecutableEffect(int table)
    {
        var bytes = s.H3.ReadExecutable(table, 16) ?? new byte[16];
        var v = new int[4];
        for (int i = 0; i < 4; i++) v[i] = BitConverter.ToInt32(bytes, 4 * i);
        return v;
    }
}
