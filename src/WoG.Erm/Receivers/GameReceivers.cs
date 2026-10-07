using System;
using System.Collections.Generic;
using WoG.Core.Adapters;
using WoG.Core.Compat;
using WoG.Core.H3Data;
using WoG.Core.Model;
using WoG.Core.Options;
using WoG.Erm.Runtime;
using WoG.Erm.Syntax;

namespace WoG.Erm.Receivers;

/// <summary>!!IF — inline IF of ProcessMes (messages, questions, flags, w-hero).</summary>
public sealed class IfReceiver : ErmReceiverBase
{
    public IfReceiver() : base("IF")
    {
        Declare("VASRW", CompatLevel.FullySupported);
        Declare("MQ", CompatLevel.PartiallySupported, "text messages and yes/no questions; variants with pictures need custom UI");
        Declare("TPEXBFGDNL", CompatLevel.Unsupported, "special WoG dialogs (pictures, sphinx, checkboxes, multiple choice) need a custom UI layer");
    }

    protected override void Run(ErmCall c)
    {
        var rt = c.Rt;
        var ui = rt.Services.Game.Ui;
        switch (c.Letter)
        {
            case 'M':
                if (c.Num == 3)
                {
                    int type = 1;
                    c.Apply(ref type, 1);
                    string text = c.Text(2);
                    int result;
                    if (type == 1) { c.Need(ui.ShowMessage(text)); result = 0; }
                    else if (type == 2) result = c.Need(ui.AskYesNo(text)) ? 1 : 0;
                    else throw new ErmUnsupportedException($"IF:M message type {type} needs custom UI");
                    c.Apply(ref result, 0);
                }
                else if (c.Num == 2)
                {
                    if (c.N(0) != 1) throw new ErmRuntimeException("\"IF:M\"-wrong syntax (M1/$).");
                    c.Need(ui.ShowMessage(rt.GetString(c.N(1))));
                }
                else
                {
                    var p = c.P(0);
                    string text = p.Var?.Kind == WoG.Core.State.ErmVarKind.Z ? rt.GetString(c.N(0)) : rt.Interpolate(c.Cmd.Text ?? "");
                    c.Need(ui.ShowMessage(text));
                }
                break;
            case 'Q':
            {
                if (c.N(0) < 1 || c.N(0) > 1000) throw new ErmRuntimeException("\"IF:Q\"-wrong flag number (1...1000).");
                if (c.Num > 2) throw new ErmUnsupportedException("IF:Q with pictures needs custom UI");
                string text = c.Num == 2 ? c.Text(1) : rt.Interpolate(c.Cmd.Text ?? "");
                rt.SetFlag(c.N(0), c.Need(ui.AskYesNo(text)));
                break;
            }
            case 'V':
                if (c.Num < 2) throw new ErmRuntimeException("\"IF:V\"-wrong syntax.");
                if (c.N(0) < 1 || c.N(0) > 1000) throw new ErmRuntimeException("\"IF:V\"-wrong flag index.");
                rt.SetFlag(c.N(0), c.N(1) != 0);
                break;
            case 'A':
            case 'S':
            case 'R':
            {
                // Decimal digits of the number are flags 1..10 (leftmost = flag 1).
                int n = c.N(0);
                for (int i = 9; i >= 0; i--, n /= 10)
                {
                    bool fl = (n & 1) != 0;
                    if (c.Letter == 'S' && !fl) continue;
                    if (c.Letter == 'R') { if (!fl) continue; fl = false; }
                    rt.SetFlag(i + 1, fl);
                }
                break;
            }
            case 'W':
            {
                int h = c.N(0);
                if (h == -1)
                {
                    h = rt.Context.Hero;
                    if (h < 0) throw new ErmRuntimeException("\"IF:W\"-cannot find hero.");
                }
                if (h < 0 || h >= WoGLimits.HeroCount) throw new ErmRuntimeException("\"IF:W\"-hero number out of range.");
                rt.Services.State.Erm.CurrentWHero = h;
                break;
            }
            default:
                throw ErmCall.WrongCommand(c.Letter);
        }
    }
}

/// <summary>!!UN — ERM_Universal (option access implemented; the rest is engine-specific).</summary>
public sealed class UnReceiver : ErmReceiverBase
{
    public UnReceiver() : base("UN")
    {
        Declare("P", CompatLevel.FullySupported);
        Declare("A", CompatLevel.PartiallySupported,
            "UN:A — artifact types come from the ERA installation's artraits.txt and are kept per game; Olden Era items are not linked to them yet, so changes do not affect the game's items, and the map ban does not affect map generation");
        Declare("V", CompatLevel.FullySupported, "UN:V — WoG/ERM versions of the dialect (ERA 400/3931, WoG 358/281); a single-player game: no network, no cheat tracking (0)");
        Declare("C", CompatLevel.Unsupported, "UN:C writes to H3 memory addresses — impossible on a different engine");
        Declare("BDEFGHIJKLMNOQRSTUWXYZ", CompatLevel.Unsupported, "UN map/object/global commands are not mapped yet");
    }

    protected override void Run(ErmCall c)
    {
        switch (c.Letter)
        {
            case 'P': Options(c); break;
            case 'A': Artifact(c); break;
            case 'V': Versions(c); break;
            default: throw ErmCall.WrongCommand(c.Letter);
        }
    }

    static void Options(ErmCall c)
    {
        var opts = c.Rt.Services.State.Options;
        if (c.Num < 2)
        {
            int v0 = opts.Get(0);
            if (!c.Apply(ref v0, 0)) opts.Set(0, v0);
            return;
        }
        int idx = c.N(0);
        if (idx < 0 || idx >= WoGOptionIds.Count) throw new ErmRuntimeException("\"!!UN:P\"-wrong first parameter.");
        int v = opts.Get(idx);
        bool isCheck = c.Apply(ref v, 1);
        if (!isCheck) opts.Set(idx, v);
        if ((idx == WoGOptionIds.NoNPC || idx == WoGOptionIds.NPC2Hire) && !isCheck)
            c.Rt.Services.Commanders?.ApplyOptions();
    }

    /// <summary>UN:A (ERM_Universal case 'A'): map ban, artifact setup fields, combination table.</summary>
    static void Artifact(ErmCall c)
    {
        var s = c.Rt.Services;
        c.RequireMin(2);
        if (c.Num == 2)
        {
            // ArtDisabled / ArtDisabledSet
            int art = c.N(0);
            if (art < 0 || art >= s.ArtifactCount()) throw new ErmRuntimeException("\"ArtDisabled\" wrong Artifact number.");
            int v = s.State.BannedArtifacts.Contains(art) ? 1 : 0;
            if (c.Apply(ref v, 1)) return;
            if (v != 0) s.State.BannedArtifacts.Add(art); else s.State.BannedArtifacts.Remove(art);
            return;
        }
        if (!s.HasArtifactTable())
            throw new ErmUnsupportedException("UN:A needs the artifact table of the ERA installation (artraits.txt), and none was found");
        if (c.Num == 3)
        {
            int id = -1;
            c.Apply(ref id, 0);
            if (id < 0 || id >= s.ArtifactCount()) throw new ErmRuntimeException("\"!!UN:A\"-wrong artifact number (internal).");
            var a = s.Artifact(id)!;
            int what = 0;
            c.Apply(ref what, 1);
            switch (what)
            {
                case 1: Field(c, a.Cost, v => s.EditArtifact(id).Cost = v); break;
                case 2: Field(c, a.Position, v => s.EditArtifact(id).Position = v); break;
                case 3: Field(c, a.Type, v => s.EditArtifact(id).Type = v); break;
                case 4: Field(c, a.SuperN, v => s.EditArtifact(id).SuperN = v); break;
                case 5: Field(c, a.PartOfSuperN, v => s.EditArtifact(id).PartOfSuperN = v); break;
                case 7: Field(c, a.Disable, v => s.EditArtifact(id).Disable = (byte)v); break;
                case 8: Field(c, a.NewSpell, v => s.EditArtifact(id).NewSpell = (byte)v); break;
                case 9: Text(c, a.Name, s.H3.Artifacts[id].Name, t => s.EditArtifact(id).Name = t); break;
                case 10: Text(c, a.Description, s.H3.Artifacts[id].Description, t => s.EditArtifact(id).Description = t); break;
                case 11: Text(c, a.PickUpText, s.H3.Artifacts[id].PickUpText, t => s.EditArtifact(id).PickUpText = t); break;
                default: throw new ErmRuntimeException("\"UN:A\"-wrong syntax (A$/$/$).");
            }
            return;
        }
        // Num >= 4: combination table entry
        int index = -1;
        c.Apply(ref index, 0);
        if (index < 0 || index >= ArtifactTable.ComboSlots) throw new ErmRuntimeException("\"!!UN:A\"-wrong combo artifact index.");
        int combo = s.Combo(index)[0];
        if (c.Apply(ref combo, 1)) return;
        if (combo < 0 || combo >= s.ArtifactCount()) throw new ErmRuntimeException("\"!!UN:A\"-wrong combo artifact number.");
        var parts = new List<int>();
        for (int i = 2; i < c.Num; i++)
        {
            int part = -1;
            c.Apply(ref part, i);
            parts.Add(part);
        }
        c.Rt.SetFlag(1, s.BuildUpCombo(combo, index, parts));
    }

    static void Field(ErmCall c, int current, Action<int> write)
    {
        int v = current;
        if (!c.Apply(ref v, 2)) write(v);
    }

    static void Text(ErmCall c, string current, string original, Action<string> write)
    {
        string t = current;
        if (!c.ApplyText(ref t, original, 2)) write(t);
    }

    /// <summary>UN:V: WoG and ERM versions, then humans / network / game type / cheat menu / cheats used.</summary>
    static void Versions(ErmCall c)
    {
        c.RequireMin(2);
        for (int i = 0; i < Math.Min(c.Num, 7); i++)
            if (!c.IsGetOrCheck(i) && !(c.IsEra && c.P(i).Empty)) throw new ErmRuntimeException("\"!!UN:V\"-try to set a version or a game parameter.");
        var dialect = c.Rt.Options.Dialect;
        int wog = dialect == ErmDialect.Era ? 400 : dialect == ErmDialect.Wog359Alpha ? 359 : 358;
        int erm = dialect == ErmDialect.Era ? EraErmVersion : dialect == ErmDialect.Wog359Alpha ? 307 : 281;
        c.Apply(ref wog, 0);
        c.Apply(ref erm, 1);
        if (c.Num <= 2) return;
        c.RequireMin(5);
        int humans = 0;
        var players = c.Rt.Services.Game.Players;
        for (int p = 0; p < WoGLimits.PlayerCount; p++)
            if (players.IsHuman(p) is { Status: AdapterStatus.Ok, Value: true }) humans++;
        int moreHumans = humans > 1 ? 2 : 1, network = 0, gameType = humans > 1 ? 3 : 0; // 3 = hot seat
        c.Apply(ref moreHumans, 2);
        c.Apply(ref network, 3);
        c.Apply(ref gameType, 4);
        if (c.Num <= 5) return;
        c.RequireMin(7);
        int cheatMenu = 0, cheated = 0;
        c.Apply(ref cheatMenu, 5);
        c.Apply(ref cheated, 6);
    }

    /// <summary>Era's ERA_VERSION_INT (GameExt.pas) of the Era version this port follows (3.9.31).</summary>
    public const int EraErmVersion = 3931;
}

/// <summary>Hero selection shared by HE/CO/EX: #, -1 current, -10/-20 battle sides, x/y/l.</summary>
internal static class HeroSelector
{
    public static int Resolve(ErmCall c)
    {
        var rt = c.Rt;
        if (c.SelectorCount == 3)
            return c.Need(rt.Services.Game.Heroes.HeroAt(new MapPos(c.Selector(0), c.Selector(1), c.Selector(2))));
        int h = c.Selector(0);
        switch (h)
        {
            case -1:
                if (rt.Context.Hero < 0) throw new ErmRuntimeException("no current hero");
                return rt.Context.Hero;
            case -10:
                if (rt.Context.BattleAttacker < 0) throw new ErmRuntimeException("no attacking hero");
                return rt.Context.BattleAttacker;
            case -20:
                if (rt.Context.BattleDefender < 0) throw new ErmRuntimeException("no defending hero");
                return rt.Context.BattleDefender;
        }
        if (h < 0 || h >= WoGLimits.HeroCount) throw new ErmRuntimeException($"hero number out of range ({h})");
        if (!rt.Services.Game.Heroes.Exists(h)) throw new ErmRuntimeException($"hero {h} does not exist");
        return h;
    }
}

/// <summary>!!HE — the hero receiver (inline HE of ProcessMes). Implemented subset per the matrix.</summary>
public sealed class HeReceiver : ErmReceiverBase
{
    public HeReceiver() : base("HE")
    {
        Declare("EFIWMONPK", CompatLevel.PartiallySupported, "values go through the adapter; OE primary stats differ (see the matrix)");
        Declare("SAC", CompatLevel.PartiallySupported, "ids via IdMap; display-slot forms are not supported");
        Declare("BDGHLRTUVXY", CompatLevel.Unsupported, "not mapped yet");
    }

    protected override void Run(ErmCall c)
    {
        var heroes = c.Rt.Services.Game.Heroes;
        int h = HeroSelector.Resolve(c);
        switch (c.Letter)
        {
            case 'E':
                c.ApplyAdapter(heroes.Get(h, HeroStat.Experience), v => heroes.Set(h, HeroStat.Experience, v), 0);
                if (c.Num >= 2) // E$exp/$level[/1 = no redraw]
                    c.ApplyAdapter(heroes.Get(h, HeroStat.Level), v => heroes.Set(h, HeroStat.Level, v), 1);
                break;
            case 'F':
            {
                c.RequireMin(4);
                bool baseOnly = c.Num >= 5 && c.N(4) == 1;
                var stats = new[] { HeroStat.Attack, HeroStat.Defence, HeroStat.Power, HeroStat.Knowledge };
                for (int i = 0; i < 4; i++)
                {
                    var st = stats[i];
                    if (baseOnly)
                    {
                        int v = c.Need(heroes.GetBase(h, st));
                        c.Apply(ref v, i); // set is accepted but does nothing (help)
                    }
                    else c.ApplyAdapter(heroes.Get(h, st), v => heroes.Set(h, st, v), i);
                }
                break;
            }
            case 'I':
                c.ApplyAdapter(heroes.Get(h, HeroStat.Mana), v => heroes.Set(h, HeroStat.Mana, v), 0);
                break;
            case 'W':
                c.ApplyAdapter(heroes.Get(h, HeroStat.Movement), v => heroes.Set(h, HeroStat.Movement, v), 0);
                break;
            case 'O':
                c.ApplyAdapter(heroes.Get(h, HeroStat.Owner), v => heroes.Set(h, HeroStat.Owner, v), 0);
                break;
            case 'N':
            {
                int v = h;
                c.Apply(ref v, 0); // set is ignored ("don't try to set the hero's number")
                break;
            }
            case 'K':
                c.Need(heroes.Kill(h));
                break;
            case 'P':
            {
                c.RequireMin(3);
                var pos = c.Need(heroes.GetPosition(h));
                int x = pos.X, y = pos.Y, l = pos.L;
                bool g = c.Apply(ref x, 0) | c.Apply(ref y, 1) | c.Apply(ref l, 2);
                if (g) break;
                bool effect = c.Num > 3 && c.N(3) == 1;
                c.Need(heroes.MoveTo(h, new MapPos(x, y, l), effect));
                break;
            }
            case 'S':
            {
                if (c.Num == 3) throw new ErmUnsupportedException("HE:S display-slot syntax needs the hero screen layer");
                c.RequireExactly(2);
                int skill = c.N(0);
                if (skill < 0 || skill >= WoGLimits.SecondarySkillCount) throw new ErmRuntimeException("wrong secondary skill number");
                c.ApplyAdapter(heroes.GetSecondarySkill(h, skill), v => heroes.SetSecondarySkill(h, skill, v), 1);
                break;
            }
            case 'M':
            {
                c.RequireExactly(2);
                int spell = c.N(0);
                int v = c.Need(heroes.HasSpell(h, spell)) ? 1 : 0;
                if (c.Apply(ref v, 1)) break;
                c.Need(heroes.SetSpell(h, spell, v != 0));
                break;
            }
            case 'A':
                Artifacts(c, heroes, h);
                break;
            case 'C':
                Creatures(c, heroes, h);
                break;
            default:
                throw ErmCall.WrongCommand(c.Letter);
        }
    }

    static void Artifacts(ErmCall c, IHeroAdapter heroes, int h)
    {
        if (c.Num == 1)
        {
            int a = c.N(0);
            if (a >= 0) c.Need(heroes.AddArtifact(h, a, -1));
            else c.Need(heroes.RemoveArtifact(h, -a, 1));
            return;
        }
        switch (c.N(0))
        {
            case 1: // A1/art/slot — equip; flag 1 = 0 when the slot is busy
            {
                c.RequireMin(3);
                var r = heroes.AddArtifact(h, c.N(1), c.N(2));
                if (r.Status == AdapterStatus.Failed) { c.Rt.SetFlag(1, false); break; }
                c.Need(r);
                break;
            }
            case 2: // A2/art/?n/?m — count (the port does not distinguish equipped/backpack: m = 0)
            {
                c.RequireMin(3);
                int n = c.Need(heroes.CountArtifact(h, c.N(1)));
                c.Apply(ref n, 2);
                if (c.Num > 3) { int m = 0; c.Apply(ref m, 3); }
                break;
            }
            case 3: // A3/art/n/m — remove copies
                c.RequireMin(3);
                c.Need(heroes.RemoveArtifact(h, c.N(1), c.N(2)));
                break;
            case 4:
                c.RequireMin(2);
                c.Need(heroes.AddArtifact(h, c.N(1), -1));
                break;
            default:
                throw new ErmUnsupportedException($"HE:A{c.N(0)} not mapped");
        }
    }

    static void Creatures(ErmCall c, IHeroAdapter heroes, int h)
    {
        switch (c.N(0))
        {
            case 0: // C0/slot/$type/$num(/$exp)
            {
                c.RequireMin(4);
                int slot = c.N(1);
                if (slot < 0 || slot >= WoGLimits.ArmySlots) throw new ErmRuntimeException("wrong slot number");
                var st = c.Need(heroes.GetStack(h, slot));
                int type = st.Type, num = st.Count;
                bool g1 = c.Apply(ref type, 2), g2 = c.Apply(ref num, 3);
                if (!g1 || !g2) c.Need(heroes.SetStack(h, slot, type, num));
                if (c.Num >= 5)
                {
                    var sx = c.Rt.Services.StackExperience ?? throw new ErmUnsupportedException("stack experience module disabled");
                    var rec = sx.GetOrCreate(StackLocation.Hero(h, slot), type, num);
                    int exp = rec.Expo;
                    int mode = c.Num >= 6 ? c.N(5) : 0;
                    if (mode >= 10 && c.IsGet(4)) exp = sx.GetRank(type, exp);
                    if (!c.Apply(ref exp, 4)) rec.Expo = mode >= 10 ? sx.RankExp(type, exp) : exp;
                }
                break;
            }
            case 1: // C1/type/$type/$num — every stack of a type
            {
                c.RequireMin(4);
                int from = c.N(1);
                for (int s = 0; s < WoGLimits.ArmySlots; s++)
                {
                    var st = c.Need(heroes.GetStack(h, s));
                    if (st.Type != from || st.Count <= 0) continue;
                    int type = st.Type, num = st.Count;
                    bool g1 = c.Apply(ref type, 2), g2 = c.Apply(ref num, 3);
                    if (g1 && g2) continue;
                    if (type == -1 || num == 0) c.Need(heroes.SetStack(h, s, -1, 0));
                    else c.Need(heroes.SetStack(h, s, type, num));
                }
                break;
            }
            case 2: // C2/type/num/ask — add a stack (merge with same type, else first empty slot)
            {
                c.RequireMin(3);
                int type = c.N(1), num = c.N(2);
                int empty = -1;
                for (int s = 0; s < WoGLimits.ArmySlots; s++)
                {
                    var st = c.Need(heroes.GetStack(h, s));
                    if (!st.IsEmpty && st.Type == type) { c.Need(heroes.SetStack(h, s, type, st.Count + num)); return; }
                    if (st.IsEmpty && empty < 0) empty = s;
                }
                if (empty < 0) throw new ErmUnsupportedException("HE:C2 with a full army needs the army-choice dialog");
                c.Need(heroes.SetStack(h, empty, type, num));
                break;
            }
            default:
                throw new ErmUnsupportedException("HE:C choice-of-armies syntax needs custom UI");
        }
    }
}

/// <summary>!!OW — ERM_Owner (resources, current player, active hero, AI flag).</summary>
public sealed class OwReceiver : ErmReceiverBase
{
    public OwReceiver() : base("OW")
    {
        Declare("RCAIG", CompatLevel.PartiallySupported, "resource ids via IdMap");
        Declare("DTHKOVNWS", CompatLevel.Unsupported, "not mapped yet");
    }

    protected override void Run(ErmCall c)
    {
        var players = c.Rt.Services.Game.Players;
        int Owner(int p) => p == -1 ? players.CurrentPlayer : p;
        switch (c.Letter)
        {
            case 'R':
            {
                c.RequireMin(3);
                int p = Owner(c.N(0)), res = c.N(1);
                if (res < 0 || res >= WoGLimits.ResourceCount) throw new ErmRuntimeException("wrong resource number");
                c.ApplyAdapter(players.GetResource(p, res), v => players.SetResource(p, res, v), 2);
                break;
            }
            case 'C':
            {
                int v = players.CurrentPlayer;
                if (!c.IsGetOrCheck(0)) throw new ErmRuntimeException("\"!!OW:C\"-you can only get or check the current player.");
                c.Apply(ref v, 0);
                break;
            }
            case 'A':
            {
                c.RequireMin(2);
                int v = c.Need(players.GetActiveHero(Owner(c.N(0))));
                if (!c.Apply(ref v, 1)) throw new ErmUnsupportedException("setting the active hero is not mapped");
                break;
            }
            case 'I':
            {
                c.RequireMin(2);
                int v = c.Need(players.IsHuman(Owner(c.N(0)))) ? 0 : 1;
                if (!c.Apply(ref v, 1)) throw new ErmUnsupportedException("changing AI/human control is not mapped");
                if (c.Num > 2) { int dead = c.Need(players.IsAlive(Owner(c.N(0)))) ? 0 : 1; c.Apply(ref dead, 2); }
                break;
            }
            case 'G':
            {
                c.RequireMin(2);
                int v = c.Need(players.IsLocal(Owner(c.N(0)))) ? 1 : 0;
                c.Apply(ref v, 1);
                break;
            }
            default:
                throw ErmCall.WrongCommand(c.Letter);
        }
    }
}

/// <summary>!!MA — creature type attributes (ERM_MonAtr), routed to the creature-type adapter.</summary>
public sealed class MaReceiver : ErmReceiverBase
{
    public MaReceiver() : base("MA")
    {
        Declare("ADPSMENFIGRHVLOUXBC", CompatLevel.PartiallySupported, "the engine's stat model differs (initiative/speed, no shots)");
    }

    protected override void Run(ErmCall c)
    {
        var cr = c.Rt.Services.Game.Creatures;
        if (c.Letter == 'C')
        {
            c.RequireMin(3);
            int type = c.N(0), res = c.N(1);
            c.ApplyAdapter(cr.GetCost(type, res), v => cr.SetCost(type, res, v), 2);
            return;
        }
        CreatureStat stat = c.Letter switch
        {
            'A' => CreatureStat.Attack,
            'D' => CreatureStat.Defence,
            'P' => CreatureStat.HitPoints,
            'S' => CreatureStat.Speed,
            'M' => CreatureStat.DamageLow,
            'E' => CreatureStat.DamageHigh,
            'N' => CreatureStat.Shots,
            'F' => CreatureStat.FightValue,
            'I' => CreatureStat.AiValue,
            'G' => CreatureStat.Growth,
            'R' => CreatureStat.HordeGrowth,
            'H' => CreatureStat.AdvMapHigh,
            'V' => CreatureStat.AdvMapLow,
            'L' => CreatureStat.Level,
            'O' => CreatureStat.Town,
            'U' => CreatureStat.UpgradeTo,
            'X' => CreatureStat.Flags,
            'B' => CreatureStat.Casts,
            _ => throw ErmCall.WrongCommand(c.Letter),
        };
        c.RequireMin(2);
        int t = c.N(0);
        if (!cr.Exists(t)) throw new ErmRuntimeException($"wrong monster type {t}");
        c.ApplyAdapter(cr.Get(t, stat), v => cr.Set(t, stat, v), 1);
    }
}
