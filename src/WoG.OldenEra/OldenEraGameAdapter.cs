using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using WoG.Core.Adapters;
using WoG.Core.Ids;
using WoG.Core.Model;

namespace WoG.OldenEra;

/// <summary>
/// IGameAdapter for Olden Era. Every operation first checks that the game symbols it needs are bound
/// and verified (<see cref="OldenEraSymbols.Has"/>); otherwise it returns Unsupported with the missing
/// symbol named, so the ERM runtime records it instead of pretending (project rule 6).
///
/// Mapping decisions (Compatibility/Compatibility_Matrix.md):
///  * H3 primary skills → OE hero stats: Attack→offence, Defence→defence, Power→spellPower,
///    Knowledge→intelligence (OE luck/moral are separate stats with no H3 primary-skill equivalent);
///  * ids (creatures, artifacts, skills, resources) go through <see cref="IdMap"/>; unmapped → Unsupported.
/// </summary>
public sealed class OldenEraGameAdapter : IGameAdapter, IHeroAdapter, IPlayerAdapter, ICreatureTypeAdapter, IMapAdapter,
    ITownAdapter, IUiAdapter, IBattleAdapter, IGameClock
{
    readonly OldenEraSymbols sym;
    readonly Func<IdMap> currentIds;

    public OldenEraGameAdapter(OldenEraSymbols symbols, Func<IdMap> ids)
    {
        sym = symbols;
        currentIds = ids; // read lazily: the host that owns the IdMap is created after the adapter
    }

    public string EngineName => "Heroes of Might and Magic: Olden Era";
    public IHeroAdapter Heroes => this;
    public IPlayerAdapter Players => this;
    public ICreatureTypeAdapter Creatures => this;
    public IMapAdapter Map => this;
    public ITownAdapter Towns => this;
    public IUiAdapter Ui => this;
    public IBattleAdapter Battle => this;
    public IGameClock Clock => this;

    static AdapterResult<T> Missing<T>(string key) => AdapterResult<T>.Unsupported($"[UNVERIFIED] Olden Era symbol '{key}' is not bound/verified");
    static AdapterResult Missing(string key) => AdapterResult.Unsupported($"[UNVERIFIED] Olden Era symbol '{key}' is not bound/verified");

    /// <summary>The session state object, or null outside a game (main menu) or when unbound.</summary>
    public object? Root() => sym.Has("game.root") ? sym.Read("game.root", null) : null;

    // ---- hero numbers -----------------------------------------------------------------------
    // WoG addresses heroes by number 0..155 (HERNUM: commanders, w-variables and range checks depend on it), and in
    // H3 every hero exists all the time. Olden Era keeps all its hero types in one list (177 in 0.81.04, id = type
    // index); in a game a hero is in play (on the map, in a garrison or prison, in reserve, dead), in the hire pool,
    // or not part of the game at all (status Unknown: campaign heroes and the like). Heroes in play, then pool
    // heroes, get a WoG number for the whole game: their OE id when that number is free, otherwise the lowest free
    // one; when all are taken, a pool hero gives its number up to a hero entering play. The table lives in the WoG
    // state (IdMap "heroSlot": number -> "id:<OE id>") and is saved with it. H3 scripts that name a specific H3 hero
    // by number act on whatever hero holds that number: Olden Era has no H3 heroes (see the compatibility matrix).

    const string SlotDomain = "heroSlot";
    // Hex.Session.Data.EHeroStatus: Unknown = 0 (not in this game), InPool = 4 (hire pool).
    const int StatusUnknown = 0, StatusInPool = 4;

    Dictionary<int, object>? heroByNumber;
    long heroCacheTime;

    int HeroEngineId(object hero) => Convert.ToInt32(sym.Read("hero.id", hero));
    int HeroStatus(object hero) => sym.Has("hero.status") ? Convert.ToInt32(sym.Read("hero.status", hero)) : -1;

    /// <summary>Number → hero for the heroes of this game, refreshed at most every 100 ms (ERM loops call it a lot).</summary>
    Dictionary<int, object> HeroTable()
    {
        long now = Environment.TickCount64;
        if (heroByNumber != null && now - heroCacheTime < 100) return heroByNumber;
        heroCacheTime = now;
        var table = new Dictionary<int, object>();
        heroByNumber = table;
        if (!sym.Has("hero.list") || !sym.Has("hero.id") || Root() is not { } root) return table;
        var ids = currentIds();
        var all = OldenEraSymbols.Items(sym.Read("hero.list", root)).Where(h => h != null).Select(h => h!)
            .Select(h => (hero: h, id: HeroEngineId(h), status: HeroStatus(h)))
            .Where(x => x.status != StatusUnknown).ToList();
        var byKey = all.ToDictionary(x => "id:" + x.id, x => x);
        var inPlay = all.Where(x => x.status != StatusInPool).ToList();
        var pool = all.Where(x => x.status == StatusInPool).ToList();
        // Numbers already given in this game stay; a number of a hero that left the game is free again.
        var owner = new Dictionary<int, string>();
        foreach (var (n, key) in ids.Forward.TryGetValue(SlotDomain, out var f) ? f : new Dictionary<int, string>())
            if (byKey.ContainsKey(key)) owner[n] = key;
        var numbered = owner.Values.ToHashSet();
        int Free(int preferred)
        {
            if (preferred >= 0 && preferred < WoGLimits.HeroCount && !owner.ContainsKey(preferred)) return preferred;
            for (int n = 0; n < WoGLimits.HeroCount; n++) if (!owner.ContainsKey(n)) return n;
            return -1;
        }
        foreach (var x in inPlay.Concat(pool))
        {
            string key = "id:" + x.id;
            if (numbered.Contains(key)) continue;
            int n = Free(x.id);
            if (n < 0 && x.status != StatusInPool)
            {
                // All 156 numbers taken: a pool hero gives its number to the hero entering play.
                var victim = owner.FirstOrDefault(kv => byKey[kv.Value].status == StatusInPool);
                if (victim.Value != null) { n = victim.Key; numbered.Remove(victim.Value); }
            }
            if (n < 0) continue;
            owner[n] = key;
            numbered.Add(key);
            ids.Set(SlotDomain, n, key);
        }
        foreach (var (n, key) in owner) table[n] = byKey[key].hero;
        return table;
    }

    /// <summary>The WoG number of an Olden Era hero id; -1 when the hero is not part of this game.</summary>
    public int HeroNumber(int engineId)
    {
        HeroTable();
        return currentIds().TryGetWoG(SlotDomain, "id:" + engineId, out int n) ? n : -1;
    }

    /// <summary>The OE hero object that holds WoG hero number <paramref name="hero"/>.</summary>
    public object? FindHero(int hero) => HeroTable().TryGetValue(hero, out var h) ? h : null;

    /// <summary>WoG player number of an OE side id (the side's position in the side array).</summary>
    int PlayerOfSide(int sideId)
    {
        if (!sym.Has("player.id")) return sideId;
        var all = PlayerObjects();
        for (int i = 0; i < all.Count; i++)
            if (all[i] != null && Convert.ToInt32(sym.Read("player.id", all[i])) == sideId) return i;
        return -1;
    }

    static string? StatKey(HeroStat s) => s switch
    {
        HeroStat.Experience => "hero.experience",
        HeroStat.Level => "hero.level",
        HeroStat.Attack => "hero.offence",
        HeroStat.Defence => "hero.defence",
        HeroStat.Power => "hero.spellPower",
        HeroStat.Knowledge => "hero.intelligence",
        HeroStat.Mana => "hero.mana",
        HeroStat.Movement => "hero.movement",
        HeroStat.Owner => "hero.owner",
        _ => null,
    };

    // ---- heroes -----------------------------------------------------------------------------

    public bool Exists(int hero) => FindHero(hero) != null;

    public AdapterResult<int> Get(int hero, HeroStat stat)
    {
        var own = GetBase(hero, stat);
        if (own.Status != AdapterStatus.Ok || PrimaryName(stat) == null) return own;
        // H3's primary skills include the bonuses of worn artifacts (equipping adds them; WoG's HE:A1 does it too):
        // the total of the hero's logic, as the hero panel shows it [V-game: Crown of the Supreme Magi, knowledge 1 → 5].
        var h = FindHero(hero)!;
        return TotalStat(stat, h) is int total ? AdapterResult<int>.Ok(total) : own;
    }

    static string? PrimaryName(HeroStat stat) => stat switch
    {
        HeroStat.Attack => "offence", HeroStat.Defence => "defence",
        HeroStat.Power => "spellPower", HeroStat.Knowledge => "intelligence", _ => null,
    };

    /// <summary>The primary skill with every bonus (items, skills) from the logic of a hero on the map.</summary>
    int? TotalStat(HeroStat stat, object hero)
    {
        if (PrimaryName(stat) is not { } name || !sym.Has("herologic.stats") || HeroLogic(hero) is not { } l) return null;
        return sym.Read("herologic.stats", l) is { } block ? Convert.ToInt32(OldenEraSymbols.ReadMember(block, name)) : null;
    }

    /// <summary>
    /// The primary skill a hero type starts with (Olden Era keeps it in the type config; the hero's own block holds
    /// the growth by level): type base + growth is the hero's own value, without the bonuses of items.
    /// </summary>
    int BaseStat(HeroStat stat, object hero)
    {
        string? name = PrimaryName(stat);
        if (name == null || !sym.Has("hero.statsBase")) return 0;
        var block = sym.Read("hero.statsBase", hero);
        return block == null ? 0 : Convert.ToInt32(OldenEraSymbols.ReadMember(block, name));
    }

    public AdapterResult Set(int hero, HeroStat stat, int value)
    {
        if (stat == HeroStat.HeroClass)
            return GetHeroClass(hero) is { Status: AdapterStatus.Ok } cls && cls.Value == value ? AdapterResult.Ok
                : AdapterResult.Unsupported("an Olden Era hero's class comes with its type and cannot be changed");
        var key = StatKey(stat);
        if (key == null) return AdapterResult.Unsupported($"hero stat {stat} has no Olden Era equivalent");
        if (stat == HeroStat.Owner) return AdapterResult.Unsupported("changing a hero's owner is not mapped");
        if (!sym.Has(key)) return Missing(key);
        var h = FindHero(hero);
        if (h == null) return AdapterResult.Failed($"hero {hero} does not exist");
        if (stat == HeroStat.Experience && sym.Has("herologic.experience") && sym.Has("experience.add") && HeroLogic(h) is { } xl)
        {
            int gain = value - Convert.ToInt32(sym.Read(key, h));
            if (gain > 0)
            {
                // WoG's HE:E calls AddExp after setting it: the game's own gain (dze.barm(gain, true)) levels the hero up
                // and opens its level-up window [V-game: +10 at 1000 → level 2, the skill choice was shown]
                OldenEraSymbols.Call(sym.Read("herologic.experience", xl)!, sym.MemberOf("experience.add")!.Name, gain, true);
                return AdapterResult.Ok;
            }
        }
        // a new total changes the growth by the difference (the bonuses of items stay theirs)
        int current = Get(hero, stat) is { Status: AdapterStatus.Ok } t ? t.Value : 0;
        int growth = Convert.ToInt32(sym.Read(key, h));
        sym.Write(key, h, PrimaryName(stat) != null ? growth + value - current : value - BaseStat(stat, h));
        // the hero logic's totals follow only after a recalculation (Logic.Hero.baql) [V-game: attack 2 → 3]
        if (PrimaryName(stat) != null && sym.Has("herologic.logic") && sym.Has("herologic.recalc")
            && HeroLogic(h) is { } l && sym.Read("herologic.logic", l) is { } logic)
            OldenEraSymbols.Call(logic, sym.MemberOf("herologic.recalc")!.Name);
        return AdapterResult.Ok;
    }

    /// <summary>Without the bonuses of items (HE:F…/1): the type's base plus the growth by level.</summary>
    public AdapterResult<int> GetBase(int hero, HeroStat stat)
    {
        if (stat == HeroStat.HeroClass) return GetHeroClass(hero);
        var key = StatKey(stat);
        if (key == null) return AdapterResult<int>.Unsupported($"hero stat {stat} has no Olden Era equivalent");
        if (!sym.Has(key)) return Missing<int>(key);
        var h = FindHero(hero);
        if (h == null) return AdapterResult<int>.Failed($"hero {hero} does not exist");
        int v = Convert.ToInt32(sym.Read(key, h)) + BaseStat(stat, h);
        return AdapterResult<int>.Ok(stat == HeroStat.Owner ? PlayerOfSide(v) : v);
    }
    public AdapterResult<MapPos> GetPosition(int hero)
    {
        if (!sym.Has("hero.position")) return Missing<MapPos>("hero.position");
        if (MapDims() is not var (sx, sz)) return AdapterResult<MapPos>.Failed("no game session");
        var h = FindHero(hero);
        if (h == null) return AdapterResult<MapPos>.Failed($"hero {hero} does not exist");
        return AdapterResult<MapPos>.Ok(NodeToPos(Convert.ToInt32(sym.Read("hero.position", h)), sx, sz));
    }

    public AdapterResult MoveTo(int hero, MapPos pos, bool withEffect) => Missing("hero.move");

    public AdapterResult<int> HeroAt(MapPos pos)
    {
        if (!sym.Has("hero.position")) return Missing<int>("hero.position");
        if (MapDims() is not var (sx, sz)) return AdapterResult<int>.Failed("no game session");
        int node = PosToNode(pos, sx, sz);
        foreach (var (n, h) in HeroTable())
            if (node >= 0 && Convert.ToInt32(sym.Read("hero.position", h)) == node) return AdapterResult<int>.Ok(n);
        return AdapterResult<int>.Failed($"no hero at {pos}");
    }
    // Secondary skills: Hero.skills (HeroSkills).list of HeroSkill {sid, level 1..3} [V-game: a new hero has
    // skill_logistic 1 and its faction skill]; H3 skills map by effect (id-maps/skill.json), levels 1..3 as in H3.
    object? SkillEntry(object hero, string sid)
    {
        foreach (var s in OldenEraSymbols.Items(sym.Read("hero.skills", hero)))
            if (s != null && sym.Read("skill.sid", s) as string == sid) return s;
        return null;
    }

    public AdapterResult<int> GetSecondarySkill(int hero, int skill)
    {
        if (!sym.Has("hero.skills") || !sym.Has("skill.sid") || !sym.Has("skill.level")) return Missing<int>("hero.skills");
        var h = FindHero(hero);
        if (h == null) return AdapterResult<int>.Failed($"hero {hero} does not exist");
        // A skill Olden Era does not have is a skill no hero has learned.
        if (!currentIds().TryGetEngine("skill", skill, out var sid)) return AdapterResult<int>.Ok(0);
        var e = SkillEntry(h, sid);
        return AdapterResult<int>.Ok(e == null ? 0 : Convert.ToInt32(sym.Read("skill.level", e)));
    }

    public AdapterResult SetSecondarySkill(int hero, int skill, int level)
    {
        var cur = GetSecondarySkill(hero, skill);
        if (cur.Status != AdapterStatus.Ok) return cur.AsPlain();
        if (level < 0 || level > 3) return AdapterResult.Failed("wrong skill level (0...3)");
        if (level == cur.Value) return AdapterResult.Ok;
        if (!currentIds().TryGetEngine("skill", skill, out _))
            return AdapterResult.Unsupported($"H3 secondary skill {skill} has no Olden Era equivalent (id-maps/skill.json)");
        if (level < cur.Value)
            return AdapterResult.Unsupported("lowering or removing a learned Olden Era skill is not mapped (its bonuses stay applied)");
        currentIds().TryGetEngine("skill", skill, out var sid);
        var h = FindHero(hero)!;
        // A skill's bonuses live in its skill logic (eah, one per applied skill of a hero on the map); the data
        // entry alone changes nothing until the game builds the logic (it does so for every entry on load).
        var logic = SkillLogic(h);
        if (cur.Value == 0)
        {
            if (!sym.Has("hero.skillsAdd")) return Missing("hero.skillsAdd");
            // HeroSkills.bjfv(sid, level): the data entry; eaj.bazp(entry): builds and applies its logic.
            var entry = OldenEraSymbols.Call(sym.Read("hero.skillsHolder", h)!, sym.MemberOf("hero.skillsAdd")!.Name, sid, level);
            if (logic != null && entry != null && sym.Has("skills.learn"))
                OldenEraSymbols.Call(logic, sym.MemberOf("skills.learn")!.Name, entry);
            OfferSubSkills(hero, h, sid, 1, level);
            return AdapterResult.Ok;
        }
        if (!sym.Has("skilllogic.levelUp")) return Missing("skilllogic.levelUp");
        var own = logic == null ? null : OldenEraSymbols.Items(sym.Read("skilllogic.list", logic))
            .FirstOrDefault(e => e != null && sym.Read("skilllogic.sid", e) as string == sid);
        if (own == null)
            return AdapterResult.Unsupported("raising a skill of a hero that is not on the map (no skill logic to level up)");
        // eah.LevelUp(): the game's level-up of a learned skill (data level and bonuses), one level per call.
        for (int n = cur.Value; n < level; n++) OldenEraSymbols.Call(own, sym.MemberOf("skilllogic.levelUp")!.Name);
        OfferSubSkills(hero, h, sid, cur.Value, level);
        return AdapterResult.Ok;
    }

    // Sub-skills: reaching level 2 or 3 of a skill the player picks one of that level's three sub-skills
    // (DB skills.json parametersPerLevel[].subSkills; HeroSkill.subSkills with status Inactive / PermanentActive).
    // A level given by a script brings the same choice: a level-up in the hero's pool with only the sub-skill stage,
    // registered with the hero's level-up logic and shown in the game's own level-up window [V-game: skill_luck 2
    // offered sub_skill_luck_1..3, the pick became PermanentActive, the hero kept his level and experience].
    // Heroes of an AI player, and heroes without map logic, get one of the choices at once (eah.bayw).
    void OfferSubSkills(int hero, object h, string sid, int from, int to)
    {
        if (!sym.Has("hero.levelUpPool") || !sym.Has("levelups.add") || SkillEntry(h, sid) is not { } entry) return;
        var logic = HeroLogic(h);
        var levelUps = logic != null && sym.Has("herologic.levelUps") ? sym.Read("herologic.levelUps", logic) : null;
        bool ai = levelUps == null || !(Get(hero, HeroStat.Owner) is { Status: AdapterStatus.Ok } o && IsHuman(o.Value) is { Status: AdapterStatus.Ok, Value: true });
        bool offered = false;
        for (int level = Math.Max(from + 1, 2); level <= to; level++)
        {
            var choices = OldenEraSymbols.Items(OldenEraSymbols.ReadMember(entry, "subSkills")).Where(s => s != null).Select(s => s!)
                .Where(s => Convert.ToInt32(OldenEraSymbols.ReadMember(s, "level")) == level).ToList();
            if (choices.Count == 0 || choices.Any(s => OldenEraSymbols.ReadMember(s, "status")!.ToString() != "Inactive"))
                continue; // no sub-skills at this level, or one is picked already
            if (ai)
            {
                var own = SkillLogic(h) is { } skills ? OldenEraSymbols.Items(sym.Read("skilllogic.list", skills))
                    .FirstOrDefault(e => e != null && sym.Read("skilllogic.sid", e) as string == sid) : null;
                if (own != null && sym.Has("skilllogic.pickSub"))
                {
                    string pick = (string)OldenEraSymbols.ReadMember(choices[Random.Shared.Next(choices.Count)], "sid")!;
                    OldenEraSymbols.Call(own, sym.MemberOf("skilllogic.pickSub")!.Name, pick);
                }
                continue;
            }
            // DataLevelUpPool.bjhn(false): a new level-up; only its sub-skill stage is active
            var levelUp = OldenEraSymbols.Call(sym.Read("hero.levelUpPool", h)!, sym.MemberOf("levelups.add")!.Name, false)!;
            OldenEraSymbols.WriteMember(OldenEraSymbols.ReadMember(levelUp, "skillStage")!, "isActive", false);
            OldenEraSymbols.WriteMember(OldenEraSymbols.ReadMember(levelUp, "alternativeSubSkillStage")!, "isActive", false);
            var stage = OldenEraSymbols.ReadMember(levelUp, "subSkillStage")!;
            OldenEraSymbols.WriteMember(stage, "isActive", true);
            OldenEraSymbols.WriteMember(stage, "skillSid", sid);
            OldenEraSymbols.WriteMember(stage, "skillLevel", level);
            // dzm.baue(levelUp, false): its logic (LevelUp, the stage queue)
            OldenEraSymbols.Call(levelUps!, sym.MemberOf("levelups.register")!.Name, levelUp, false);
            offered = true;
        }
        if (offered && sym.Has("levelups.show")) OldenEraSymbols.Call(levelUps!, sym.MemberOf("levelups.show")!.Name);
    }

    /// <summary>The sub-skills of a hero's skills, for WoG Debug: sid, level, status.</summary>
    public string DescribeSubSkills(int hero)
    {
        var h = FindHero(hero);
        if (h == null || !sym.Has("hero.skills")) return $"hero {hero}: not found";
        var sb = new System.Text.StringBuilder($"hero {hero} sub-skills:\n");
        foreach (var s in OldenEraSymbols.Items(sym.Read("hero.skills", h)))
        {
            if (s == null) continue;
            sb.Append($"  {sym.Read("skill.sid", s)} {sym.Read("skill.level", s)}: ");
            sb.Append(string.Join(", ", OldenEraSymbols.Items(OldenEraSymbols.ReadMember(s, "subSkills")).Where(x => x != null)
                .Select(x => $"{OldenEraSymbols.ReadMember(x!, "sid")} (ур. {OldenEraSymbols.ReadMember(x!, "level")}, {OldenEraSymbols.ReadMember(x!, "status")})")));
            sb.Append('\n');
        }
        return sb.ToString();
    }

    // The hero screen lists the skills in learning order; H3 scripts see only the skills that have an H3 number.
    public AdapterResult<IReadOnlyList<int>> GetSecondarySkillOrder(int hero)
    {
        if (!sym.Has("hero.skills") || !sym.Has("skill.sid")) return Missing<IReadOnlyList<int>>("hero.skills");
        var h = FindHero(hero);
        if (h == null) return AdapterResult<IReadOnlyList<int>>.Failed($"hero {hero} does not exist");
        var ids = currentIds();
        var order = new List<int>();
        foreach (var s in OldenEraSymbols.Items(sym.Read("hero.skills", h)))
            if (s != null && sym.Read("skill.sid", s) is string sid && ids.TryGetWoG("skill", sid, out int n)
                && n < WoGLimits.SecondarySkillCount && order.Count < 8)
                order.Add(n);
        return AdapterResult<IReadOnlyList<int>>.Ok(order);
    }

    public AdapterResult SetSecondarySkillOrder(int hero, IReadOnlyList<int> order)
    {
        var cur = GetSecondarySkillOrder(hero);
        if (cur.Status != AdapterStatus.Ok) return cur.AsPlain();
        return cur.Value!.SequenceEqual(order) ? AdapterResult.Ok
            : AdapterResult.Unsupported("the Olden Era hero screen shows skills in learning order; reordering or hiding them is not mapped");
    }

    /// <summary>The skills logic (eaj) of a hero on the map, null for heroes without map logic (hire pool).</summary>
    object? SkillLogic(object hero) =>
        sym.Has("herologic.skills") && sym.Has("skilllogic.list") && HeroLogic(hero) is { } l ? sym.Read("herologic.skills", l) : null;

    /// <summary>The logic object (fdq) of a hero on the map; null for heroes without map logic (hire pool).</summary>
    object? HeroLogic(object hero)
    {
        if (!sym.Has("world.heroLogics") || !sym.Has("visitor.hero")) return null;
        int id = HeroEngineId(hero);
        foreach (var l in OldenEraSymbols.Items(sym.Read("world.heroLogics", null)))
            if (l != null && sym.Read("visitor.hero", l) is { } d && HeroEngineId(d) == id) return l;
        return null;
    }
    // Spells: Hero.magics.list of MagicData {sidConfig, level, isLearned} plus the magic logic of a hero on the map
    // (eaa, one dzx per spell); H3 spells map by effect (id-maps/spell.json). A specialist knows a spell as its
    // "_special" variant, which eaa.baxy names (learning the base sid gives the variant by itself).
    object? MagicLogic(object hero) =>
        sym.Has("herologic.magics") && HeroLogic(hero) is { } l ? sym.Read("herologic.magics", l) : null;

    string? SpellVariant(object? logic, string sid) =>
        logic != null && sym.Has("magics.variant") ? OldenEraSymbols.Call(logic, sym.MemberOf("magics.variant")!.Name, sid) as string : null;

    public AdapterResult<bool> HasSpell(int hero, int spell)
    {
        if (!sym.Has("hero.magics") || !sym.Has("magic.sid") || !sym.Has("magic.learned")) return Missing<bool>("hero.magics");
        var h = FindHero(hero);
        if (h == null) return AdapterResult<bool>.Failed($"hero {hero} does not exist");
        // A spell Olden Era does not have is a spell no hero knows.
        if (!currentIds().TryGetEngine("spell", spell, out var sid)) return AdapterResult<bool>.Ok(false);
        var variant = SpellVariant(MagicLogic(h), sid);
        foreach (var m in OldenEraSymbols.Items(sym.Read("hero.magics", h)))
            if (m != null && sym.Read("magic.sid", m) is string s && (s == sid || s == variant)
                && Convert.ToBoolean(sym.Read("magic.learned", m)))
                return AdapterResult<bool>.Ok(true);
        return AdapterResult<bool>.Ok(false);
    }

    public AdapterResult SetSpell(int hero, int spell, bool known)
    {
        var cur = HasSpell(hero, spell);
        if (cur.Status != AdapterStatus.Ok) return cur.AsPlain();
        if (cur.Value == known) return AdapterResult.Ok;
        if (!currentIds().TryGetEngine("spell", spell, out var sid))
            return AdapterResult.Unsupported($"H3 spell {spell} has no Olden Era equivalent (id-maps/spell.json)");
        var logic = MagicLogic(FindHero(hero)!);
        if (logic == null)
            return AdapterResult.Unsupported("changing the spells of a hero that is not on the map is not mapped (no magic logic)");
        if (known)
        {
            if (!sym.Has("magics.learn")) return Missing("magics.learn");
            // eaa.baxs(sid, false): the data entry and its logic; a specialist gets the "_special" variant
            OldenEraSymbols.Call(logic, sym.MemberOf("magics.learn")!.Name, sid, false);
            return AdapterResult.Ok;
        }
        if (!sym.Has("magics.forget") || !sym.Has("magiclogic.list") || !sym.Has("magiclogic.sid")) return Missing("magics.forget");
        var variant = SpellVariant(logic, sid);
        var own = OldenEraSymbols.Items(sym.Read("magiclogic.list", logic))
            .FirstOrDefault(e => e != null && sym.Read("magiclogic.sid", e) is string s && (s == sid || s == variant));
        if (own == null) return AdapterResult.Unsupported("the spell is in the hero's data but has no magic logic to remove");
        // eaa.bayb(dzx): removes the spell's logic and its data entry
        OldenEraSymbols.Call(logic, sym.MemberOf("magics.forget")!.Name, own);
        return AdapterResult.Ok;
    }

    static readonly string[] StackKeys = { "stack.unitSid", "stack.count", "stack.slot" };

    public AdapterResult<WoGStack> GetStack(int hero, int slot)
    {
        if (!sym.Has("hero.army") || StackKeys.Any(k => !sym.Has(k))) return Missing<WoGStack>("hero.army");
        var h = FindHero(hero);
        if (h == null) return AdapterResult<WoGStack>.Failed($"hero {hero} does not exist");
        return ReadStack(sym.Read("hero.army", h), slot);
    }

    /// <summary>
    /// Writes an army slot. Same creature: the count changes in place; type -1 or count 0 removes the unit; another
    /// creature replaces the unit with a new one (symbol stack.create = the unit type, built with its constructor).
    /// </summary>
    public AdapterResult SetStack(int hero, int slot, int type, int count)
    {
        if (!sym.Has("hero.army") || StackKeys.Any(k => !sym.Has(k))) return Missing("hero.army");
        if (slot < 0 || slot >= WoGLimits.ArmySlots) return AdapterResult.Failed($"army slot {slot} out of range");
        var h = FindHero(hero);
        if (h == null) return AdapterResult.Failed($"hero {hero} does not exist");
        var list = sym.Read("hero.army", h);
        return list == null ? AdapterResult.Failed($"hero {hero} has no army") : WriteStack(list, slot, type, count);
    }

    // An army (a hero's party, a town's garrison) is a list of units, each with its slot position; an empty slot
    // has no unit.
    AdapterResult<WoGStack> ReadStack(object? army, int slot)
    {
        var st = OldenEraSymbols.Items(army).FirstOrDefault(u => u != null && Convert.ToInt32(sym.Read("stack.slot", u)) == slot);
        if (st == null) return AdapterResult<WoGStack>.Ok(new WoGStack());
        string sid = sym.Read("stack.unitSid", st)?.ToString() ?? "";
        int count = Convert.ToInt32(sym.Read("stack.count", st));
        if (!currentIds().TryGetWoG("creature", sid, out int type))
            return AdapterResult<WoGStack>.Unsupported($"Olden Era unit '{sid}' has no WoG creature mapping");
        return AdapterResult<WoGStack>.Ok(new WoGStack { Type = type, Count = count });
    }

    AdapterResult WriteStack(object list, int slot, int type, int count)
    {
        var existing = OldenEraSymbols.Items(list)
            .FirstOrDefault(u => u != null && Convert.ToInt32(sym.Read("stack.slot", u)) == slot);
        if (type < 0 || count <= 0)
        {
            if (existing != null) OldenEraSymbols.Call(list, "Remove", existing);
            return AdapterResult.Ok;
        }
        if (!currentIds().TryGetEngine("creature", type, out var sid))
            return AdapterResult.Unsupported($"creature {type} has no Olden Era unit (IdMap \"creature\")");
        if (existing != null && sym.Read("stack.unitSid", existing)?.ToString() == sid)
        {
            sym.Write("stack.count", existing, count);
            return AdapterResult.Ok;
        }
        if (!sym.Has("stack.create")) return Missing("stack.create");
        var unit = Activator.CreateInstance(sym.TypeOf("stack.create"))!;
        sym.Write("stack.unitSid", unit, sid);
        sym.Write("stack.count", unit, count);
        sym.Write("stack.slot", unit, slot);
        if (existing != null) OldenEraSymbols.Call(list, "Remove", existing);
        OldenEraSymbols.Call(list, "Add", unit);
        return AdapterResult.Ok;
    }
    // Artifacts: the doll (Hero.slots) and the backpack (Hero.inventory) are item containers whose slots (SlotInfo:
    // type, items = item ids, -1 empty) point into Data.items (DataItem: id, configSid). Changes go through the item
    // logic of a hero on the map (eas: bbci adds to the backpack, Move puts on the doll, bbck removes) [V-game]. Move
    // does not check the slot type [V-game: it put a spyglass on the head], so the adapter does.
    // H3 worn positions → Olden Era doll slots: the neck is the belt (Olden Era's dragon and Angelic Alliance sets
    // carry a sash where H3 has a necklace), H3's right hand (weapons) is Olden Era's LEFT_HAND and the left hand
    // (shields) RIGHT_HAND; misc 1..4 are the four ITEM_SLOTs, misc 5 the UNIQUE_SLOT.
    static readonly (string type, int index)?[] DollSlots =
    {
        ("HEAD", 0), ("BACK", 0), ("BELT", 0), ("LEFT_HAND", 0), ("RIGHT_HAND", 0), ("ARMOR", 0), ("RING", 0),
        ("RING", 1), ("BOOTS", 0), ("ITEM_SLOT", 0), ("ITEM_SLOT", 1), ("ITEM_SLOT", 2), ("ITEM_SLOT", 3),
        null, null, null, null, null, ("UNIQUE_SLOT", 0),
    };
    static readonly string[] ScrollPrefixes = { "magic_scroll_artifact_", "enchanted_magic_scroll_artifact_", "mythic_magic_scroll_artifact_" };
    /// <summary>The number of an Olden Era item that artifact.json does not list (an item added by a game update).</summary>
    public const int UnknownItem = 499;

    Dictionary<string, List<int>> SlotItems(object container)
    {
        var d = new Dictionary<string, List<int>>();
        foreach (var slot in OldenEraSymbols.Items(sym.Read("container.slots", container)))
            if (slot != null)
                d[sym.Read("slot.type", slot)!.ToString()!] = OldenEraSymbols.Items(sym.Read("slot.items", slot)).Select(Convert.ToInt32).ToList();
        return d;
    }

    List<int> BackpackItems(object hero) =>
        SlotItems(sym.Read("hero.backpack", hero)!).Values.SelectMany(x => x).Where(x => x >= 0).ToList();

    int ArtifactOf(string sid)
    {
        var ids = currentIds();
        foreach (var prefix in ScrollPrefixes)
            if (sid.StartsWith(prefix, StringComparison.Ordinal))
                return ids.TryGetWoG("spell", sid[prefix.Length..], out int n) ? ArtifactSlots.ScrollBase + n : UnknownItem;
        return ids.TryGetWoG("artifact", sid, out int a) ? a : UnknownItem;
    }

    string? SidOf(int artifact)
    {
        var ids = currentIds();
        if (artifact >= ArtifactSlots.ScrollBase)
            return ids.TryGetEngine("spell", artifact - ArtifactSlots.ScrollBase, out var spell) ? ScrollPrefixes[0] + spell : null;
        return ids.TryGetEngine("artifact", artifact, out var sid) ? sid : null;
    }

    object? ItemData(int id)
    {
        foreach (var it in OldenEraSymbols.Items(sym.Read("item.list", Root())))
            if (it != null && Convert.ToInt32(sym.Read("item.id", it)) == id) return it;
        return null;
    }

    /// <summary>The doll and backpack logic (eas) of a hero on the map; null for heroes without map logic.</summary>
    (object doll, object pack)? ItemLogic(object hero)
    {
        if (!sym.Has("herologic.doll") || !sym.Has("herologic.backpack") || HeroLogic(hero) is not { } l) return null;
        return sym.Read("herologic.doll", l) is { } doll && sym.Read("herologic.backpack", l) is { } pack ? (doll, pack) : null;
    }

    /// <summary>Calls a bound game method; text arguments for enum parameters are enum value names.</summary>
    object? CallGame(object target, string key, params object?[] args)
    {
        var m = (System.Reflection.MethodInfo)sym.MemberOf(key)!;
        var ps = m.GetParameters();
        var call = new object?[ps.Length];
        for (int i = 0; i < ps.Length; i++)
            call[i] = args[i] is string s && ps[i].ParameterType.IsEnum ? Enum.Parse(ps[i].ParameterType, s) : args[i];
        return m.Invoke(target, call);
    }

    bool HasItemSymbols() => sym.Has("hero.doll") && sym.Has("hero.backpack") && sym.Has("container.slots")
        && sym.Has("slot.type") && sym.Has("slot.items") && sym.Has("item.list") && sym.Has("item.id") && sym.Has("item.sid");

    public AdapterResult<int[]> GetArtifacts(int hero)
    {
        if (!HasItemSymbols()) return Missing<int[]>("hero.doll");
        var h = FindHero(hero);
        if (h == null) return AdapterResult<int[]>.Failed($"hero {hero} does not exist");
        var sids = new Dictionary<int, string>();
        foreach (var it in OldenEraSymbols.Items(sym.Read("item.list", Root())))
            if (it != null) sids[Convert.ToInt32(sym.Read("item.id", it))] = sym.Read("item.sid", it) as string ?? "";
        int Code(int id) => id >= 0 && sids.TryGetValue(id, out var s) ? ArtifactOf(s) : -1;
        var all = Enumerable.Repeat(-1, ArtifactSlots.Positions).ToArray();
        var doll = SlotItems(sym.Read("hero.doll", h)!);
        for (int p = 0; p < ArtifactSlots.Worn; p++)
            if (DollSlots[p] is { } slot && doll.TryGetValue(slot.type, out var list) && slot.index < list.Count)
                all[p] = Code(list[slot.index]);
        all[ArtifactSlots.SpellBook] = 0; // Olden Era heroes always have a spellbook
        var pack = BackpackItems(h);
        for (int k = 0; k < pack.Count && k < ArtifactSlots.Backpack; k++) all[ArtifactSlots.Worn + k] = Code(pack[k]);
        return AdapterResult<int[]>.Ok(all);
    }

    /// <summary>Adds an item to the backpack; its index there, or an error.</summary>
    AdapterResult<int> AddItem(object hero, int artifact, (object doll, object pack) logic)
    {
        var sid = SidOf(artifact);
        if (sid == null) return AdapterResult<int>.Unsupported($"artifact {artifact} has no Olden Era equivalent (id-maps/artifact.json)");
        if (!sym.Has("items.add")) return Missing<int>("items.add");
        // eas.bbci(sid, Real, 1): a new item at the end of the backpack
        if (CallGame(logic.pack, "items.add", sid, "Real", 1) is not true)
            return AdapterResult<int>.Failed($"the game did not add {sid}");
        return AdapterResult<int>.Ok(BackpackItems(hero).Count - 1);
    }

    AdapterResult<(object hero, (object doll, object pack) logic)> ItemTarget(int hero)
    {
        if (!HasItemSymbols()) return Missing<(object, (object, object))>("hero.doll");
        var h = FindHero(hero);
        if (h == null) return AdapterResult<(object, (object, object))>.Failed($"hero {hero} does not exist");
        var logic = ItemLogic(h);
        if (logic == null)
            return AdapterResult<(object, (object, object))>.Unsupported("changing the artifacts of a hero that is not on the map is not mapped (no item logic)");
        return AdapterResult<(object, (object, object))>.Ok((h, logic.Value));
    }

    public AdapterResult AddToBackpack(int hero, int artifact)
    {
        var t = ItemTarget(hero);
        if (t.Status != AdapterStatus.Ok) return t.AsPlain();
        if (BackpackItems(t.Value.hero).Count >= ArtifactSlots.Backpack) return AdapterResult.Ok; // full, as in H3
        return AddItem(t.Value.hero, artifact, t.Value.logic).AsPlain();
    }

    /// <summary>Moves the backpack item at index k onto a doll slot when its type fits; otherwise removes it again.</summary>
    AdapterResult Wear(object hero, (object doll, object pack) logic, int k, (string type, int index) slot, out bool fits)
    {
        fits = false;
        var item = ItemData(BackpackItems(hero)[k]);
        string? type = item != null && sym.Has("item.slotType") ? sym.Read("item.slotType", item)?.ToString() : null;
        if (type != slot.type) return AdapterResult.Ok;
        fits = true;
        if (!sym.Has("items.move")) return Missing("items.move");
        // eas.Move(toType, toIndex, toContainer, fromType, fromIndex, Real), called on the backpack
        return CallGame(logic.pack, "items.move", slot.type, slot.index, logic.doll, "ANY", k, "Real") is true
            ? AdapterResult.Ok : AdapterResult.Failed("the game did not move the item");
    }

    void DropFromBackpack((object doll, object pack) logic, int k) => CallGame(logic.pack, "items.remove", "ANY", k, false, "Real");

    public AdapterResult PutArtifact(int hero, int position, int artifact)
    {
        // Olden Era's backpack has no gaps: an empty backpack position means "at the end".
        if (position >= ArtifactSlots.Worn) return AddToBackpack(hero, artifact);
        if (DollSlots[position] is not { } slot)
            return AdapterResult.Unsupported(position == ArtifactSlots.SpellBook
                ? "Olden Era heroes always have a spellbook" : "Olden Era has no war machines");
        var t = ItemTarget(hero);
        if (t.Status != AdapterStatus.Ok) return t.AsPlain();
        var (h, logic) = t.Value;
        var added = AddItem(h, artifact, logic);
        if (added.Status != AdapterStatus.Ok) return added.AsPlain();
        var r = Wear(h, logic, added.Value, slot, out bool fits);
        if (fits && r.Status == AdapterStatus.Ok) return r;
        DropFromBackpack(logic, added.Value);
        return fits ? r : AdapterResult.Unsupported($"{SidOf(artifact)} does not fit the {slot.type} slot (Olden Era items fit only their own slot type)");
    }

    public AdapterResult RemoveArtifactAt(int hero, int position)
    {
        var t = ItemTarget(hero);
        if (t.Status != AdapterStatus.Ok) return t.AsPlain();
        var (h, logic) = t.Value;
        if (!sym.Has("items.remove")) return Missing("items.remove");
        if (position >= ArtifactSlots.Worn)
        {
            if (position - ArtifactSlots.Worn < BackpackItems(h).Count) DropFromBackpack(logic, position - ArtifactSlots.Worn);
            return AdapterResult.Ok;
        }
        if (DollSlots[position] is not { } slot)
            return position == ArtifactSlots.SpellBook
                ? AdapterResult.Unsupported("Olden Era heroes always have a spellbook") : AdapterResult.Ok; // no war machines
        var doll = SlotItems(sym.Read("hero.doll", h)!);
        if (doll.TryGetValue(slot.type, out var list) && slot.index < list.Count && list[slot.index] >= 0)
            // eas.bbck(type, index, false, Real) on the doll: the item is gone (also from Data.items)
            CallGame(logic.doll, "items.remove", slot.type, slot.index, false, "Real");
        return AdapterResult.Ok;
    }

    public AdapterResult EquipArtifact(int hero, int artifact)
    {
        var t = ItemTarget(hero);
        if (t.Status != AdapterStatus.Ok) return t.AsPlain();
        var (h, logic) = t.Value;
        var added = AddItem(h, artifact, logic);
        if (added.Status != AdapterStatus.Ok) return added.AsPlain();
        var doll = SlotItems(sym.Read("hero.doll", h)!);
        foreach (var s in DollSlots)
            if (s is { } slot && doll.TryGetValue(slot.type, out var list) && slot.index < list.Count && list[slot.index] < 0)
            {
                var r = Wear(h, logic, added.Value, slot, out bool fits);
                if (fits) return r;
            }
        return AdapterResult.Ok; // no free slot of its type: it stays in the backpack
    }
    // H3 hero classes (Format HC) are two per town: might, then magic. An Olden Era hero type has a faction and a
    // might/magic kind (HeroConfig.fraction / classType) [V-data: human_hero_1 human might].
    AdapterResult<int> GetHeroClass(int hero)
    {
        if (!sym.Has("hero.typeConfig") || !sym.Has("heroconfig.fraction") || !sym.Has("heroconfig.classType")) return Missing<int>("hero.typeConfig");
        if (FindHero(hero) is not { } h || sym.Read("hero.typeConfig", h) is not { } cfg) return AdapterResult<int>.Failed($"hero {hero} does not exist");
        string faction = sym.Read("heroconfig.fraction", cfg)?.ToString() ?? "";
        if (!TownOfFraction.TryGetValue(faction, out int town)) return AdapterResult<int>.Unsupported($"Olden Era faction '{faction}' has no H3 town");
        bool magic = string.Equals(sym.Read("heroconfig.classType", cfg)?.ToString(), "magic", StringComparison.OrdinalIgnoreCase);
        return AdapterResult<int>.Ok(town * 2 + (magic ? 1 : 0));
    }

    // A hero's name and biography are the texts of its type's localization keys (HeroConfig: human_hero_1 → «Истр»,
    // human_hero_1_description) [V-game]. A hero type is in a game once, so a new name or biography is the text of that
    // key for the session: the game shows it everywhere it names the hero. The original texts are given back when the
    // session ends; the new ones are kept in the WoG state (IdMap "heroName" / "heroBio" by WoG hero number) and set
    // again when a game is loaded.
    const string HeroNameDomain = "heroName", HeroBioDomain = "heroBio";

    AdapterResult<string> HeroTextKey(int hero, string key)
    {
        if (!sym.Has("hero.typeConfig") || !sym.Has(key)) return Missing<string>(key);
        if (FindHero(hero) is not { } h || sym.Read("hero.typeConfig", h) is not { } cfg) return AdapterResult<string>.Failed($"hero {hero} does not exist");
        return sym.Read(key, cfg) is string k && k.Length > 0 ? AdapterResult<string>.Ok(k) : AdapterResult<string>.Failed($"hero {hero} has no {key}");
    }

    public AdapterResult<string> GetName(int hero)
    {
        var key = HeroTextKey(hero, "heroconfig.nameKey");
        return key.IsOk ? AdapterResult<string>.Ok(Localize(key.Value) ?? key.Value) : key;
    }

    public AdapterResult SetName(int hero, string name)
    {
        var key = HeroTextKey(hero, "heroconfig.nameKey");
        if (!key.IsOk) return key.AsPlain();
        if (!OverrideText(key.Value, name)) return Missing("loc.entries");
        currentIds().Set(HeroNameDomain, hero, name);
        return AdapterResult.Ok;
    }

    public AdapterResult<string> GetBiography(int hero, bool original)
    {
        var key = HeroTextKey(hero, "heroconfig.bioKey");
        if (!key.IsOk) return key;
        if (original) return AdapterResult<string>.Ok(OriginalText(key.Value) ?? "");
        return AdapterResult<string>.Ok(currentIds().TryGetEngine(HeroBioDomain, hero, out var bio) ? bio : "");
    }

    public AdapterResult SetBiography(int hero, string text)
    {
        var key = HeroTextKey(hero, "heroconfig.bioKey");
        if (!key.IsOk) return key.AsPlain();
        if (!OverrideText(key.Value, text)) return Missing("loc.entries");
        currentIds().Set(HeroBioDomain, hero, text);
        return AdapterResult.Ok;
    }

    // Olden Era specializations are sets of bonuses (DB/heroes_specializations; SpecializationConfig.bonuses: type,
    // parameters) [V-data: 108 of the six factions: 28 creatures, 23 spells, 9 resources, 48 others]. The closest H3
    // specialty: a spell it improves (heroMagicReplace [base, special]) → 3, a creature it grows (cityUnitsIncrement
    // [unit]) → 1, a resource it brings (sideRes [resource, amount]) → 2, a hero stat that is an H3 skill's effect → 0;
    // the rest (magic schools, battle abilities, energy, immunities) have no H3 specialty.
    static readonly (string Stat, int Skill)[] SkillOfStat =
    {
        ("movementPerBonus", 2), ("viewRadius", 3), ("diplomacyEfficiencyPerBonus", 4), ("moral", 6), ("luck", 9),
        ("necromancyPerBonus", 12), ("tacticsPlacementSize", 19), ("expPerBonus", 21), ("manaBonusPercent", 24),
        ("magicAttackPerBonus", 25),
    };

    public AdapterResult<int[]> GetSpecialty(int hero)
    {
        if (!sym.Has("hero.specialization") || !sym.Has("spec.bonuses") || !sym.Has("bonus.type") || !sym.Has("bonus.parameters"))
            return Missing<int[]>("hero.specialization");
        if (FindHero(hero) is not { } h) return AdapterResult<int[]>.Failed($"hero {hero} does not exist");
        if (sym.Read("hero.specialization", h) is not { } spec) return AdapterResult<int[]>.Unsupported("the hero has no specialization");
        var bonuses = OldenEraSymbols.Items(sym.Read("spec.bonuses", spec)).Where(b => b != null)
            .Select(b => (Type: sym.Read("bonus.type", b!)?.ToString() ?? "",
                          Params: OldenEraSymbols.Items(sym.Read("bonus.parameters", b!)).Select(x => x?.ToString() ?? "").ToList()))
            .ToList();
        var ids = currentIds();
        int[]? Record(int type, string domain, string sid) =>
            ids.TryGetWoG(domain, sid, out int n) ? new[] { type, n, 0, 0, 0, 0, 0 } : null;
        foreach (var (type, prm) in bonuses)
            if (prm.Count > 0 && (type == "heroMagicReplace" ? Record(3, "spell", prm[0])
                    : type.StartsWith("cityUnitsIncrement", StringComparison.Ordinal) ? Record(1, "creature", prm[0])
                    : type == "sideRes" ? Record(2, "resource", prm[0]) : null) is { } found)
                return AdapterResult<int[]>.Ok(found);
        foreach (var (type, prm) in bonuses)
            if (type == "heroStat" && prm.Count > 0 && SkillOfStat.FirstOrDefault(s => s.Stat == prm[0]) is { Stat: not null } m)
                return AdapterResult<int[]>.Ok(new[] { 0, m.Skill, 0, 0, 0, 0, 0 });
        string first = bonuses.Count > 0 ? $"{bonuses[0].Type} {string.Join(" ", bonuses[0].Params)}" : "none";
        return AdapterResult<int[]>.Unsupported($"the hero's Olden Era specialization ({first}) has no H3 specialty");
    }

    public AdapterResult SetSpecialty(int hero, int[] record) =>
        GetSpecialty(hero) is { Status: AdapterStatus.Ok } cur && cur.Value.SequenceEqual(record) ? AdapterResult.Ok
            : AdapterResult.Unsupported("an Olden Era hero's specialization comes with its type and cannot be changed");

    // HE:H — a hero type's start squad (HeroConfig.startSquad: {sid, min, max} per slot) [V-game: human_hero_1 esquire
    // 14-20, crossbowman 10-14, griffin 5-7]. The config lives for the whole run of the game, so the original squad is
    // given back when a session ends; a squad WoG set is kept in the WoG state (IdMap "heroArmy": hero * 3 + slot →
    // "type/min/max") and set again when a game is loaded. Which hires use it is the game's affair [UNVERIFIED: not
    // hired in game yet].
    const string HeroArmyDomain = "heroArmy";
    readonly Dictionary<object, object?> originalSquads = new(ReferenceEqualityComparer.Instance);

    AdapterResult<(object Config, List<object?> Slots)> StartSquad(int hero)
    {
        if (!sym.Has("hero.typeConfig") || !sym.Has("heroconfig.startSquad") || !sym.Has("squadslot.sid") || !sym.Has("squadslot.min") || !sym.Has("squadslot.max"))
            return Missing<(object, List<object?>)>("heroconfig.startSquad");
        if (FindHero(hero) is not { } h || sym.Read("hero.typeConfig", h) is not { } cfg) return AdapterResult<(object, List<object?>)>.Failed($"hero {hero} does not exist");
        return AdapterResult<(object, List<object?>)>.Ok((cfg, OldenEraSymbols.Items(sym.Read("heroconfig.startSquad", cfg)).ToList()));
    }

    public AdapterResult<(int Type, int Min, int Max)> GetStartArmy(int hero, int slot)
    {
        var sq = StartSquad(hero);
        if (!sq.IsOk) return sq.Error<(int, int, int)>();
        if (slot >= sq.Value.Slots.Count || sq.Value.Slots[slot] is not { } e) return AdapterResult<(int, int, int)>.Ok((-1, 0, 0));
        string sid = sym.Read("squadslot.sid", e) as string ?? "";
        if (!currentIds().TryGetWoG("creature", sid, out int type))
            return AdapterResult<(int, int, int)>.Unsupported($"Olden Era unit '{sid}' has no WoG creature mapping");
        return AdapterResult<(int, int, int)>.Ok((type, Convert.ToInt32(sym.Read("squadslot.min", e)), Convert.ToInt32(sym.Read("squadslot.max", e))));
    }

    public AdapterResult SetStartArmy(int hero, int slot, int type, int min, int max)
    {
        var sq = StartSquad(hero);
        if (!sq.IsOk) return sq.AsPlain();
        string? sid = null;
        if (type >= 0 && !currentIds().TryGetEngine("creature", type, out sid))
            return AdapterResult.Unsupported($"creature {type} has no Olden Era unit (IdMap \"creature\")");
        var (cfg, slots) = sq.Value;
        if (!originalSquads.ContainsKey(cfg)) originalSquads[cfg] = sym.Read("heroconfig.startSquad", cfg);
        while (slots.Count <= slot) slots.Add(null);
        if (sid == null) slots[slot] = null;
        else
        {
            // a new entry: the game's own entries stay as they are, shared with the original squad
            var e = Activator.CreateInstance(sym.TypeOf("squadslot.sid"))!;
            sym.Write("squadslot.sid", e, sid);
            sym.Write("squadslot.min", e, min);
            sym.Write("squadslot.max", e, max);
            slots[slot] = e;
        }
        WriteSquad(cfg, slots.Where(x => x != null).ToList());
        currentIds().Set(HeroArmyDomain, hero * 3 + slot, $"{type}/{min}/{max}");
        return AdapterResult.Ok;
    }

    void WriteSquad(object cfg, List<object?> slots)
    {
        var arrayType = sym.TypeOf("heroconfig.startSquad").GetProperty("startSquad")?.PropertyType
                        ?? throw new InvalidOperationException("HeroConfig.startSquad has no array type");
        var array = Activator.CreateInstance(arrayType, (long)slots.Count)!;
        for (int i = 0; i < slots.Count; i++) OldenEraSymbols.Call(array, "set_Item", i, slots[i]);
        sym.Write("heroconfig.startSquad", cfg, array);
    }

    // Localization texts WoG changed for this session, with the game's own text to give back (null: a key WoG added).
    readonly Dictionary<string, string?> originalTexts = new();

    bool OverrideText(string key, string text)
    {
        if (!originalTexts.ContainsKey(key)) originalTexts[key] = Localize(key);
        return RegisterText(key, text);
    }

    string? OriginalText(string key) => originalTexts.TryGetValue(key, out var t) ? t : Localize(key);

    /// <summary>
    /// A session starts: the game's own texts come back, then the names and biographies WoG gave in this game (its
    /// WoG state) are set again.
    /// </summary>
    public void ApplySavedTexts()
    {
        foreach (var (key, text) in originalTexts)
            if (text != null) RegisterText(key, text);
        originalTexts.Clear();
        foreach (var (cfg, squad) in originalSquads) sym.Write("heroconfig.startSquad", cfg, squad);
        originalSquads.Clear();
        if (currentIds().Forward.TryGetValue(HeroArmyDomain, out var armies))
            foreach (var (key, value) in armies.ToList())
                if (value.Split('/') is { Length: 3 } v)
                    SetStartArmy(key / 3, key % 3, int.Parse(v[0]), int.Parse(v[1]), int.Parse(v[2]));
        RestoreTownNames();
        foreach (var (domain, set) in new (string, Func<int, string, AdapterResult>)[] { (HeroNameDomain, SetName), (HeroBioDomain, SetBiography) })
            if (currentIds().Forward.TryGetValue(domain, out var saved))
                foreach (var (hero, text) in saved.ToList()) set(hero, text);
    }
    public AdapterResult Kill(int hero) => Missing("hero.kill");

    // ---- players ----------------------------------------------------------------------------

    // A WoG player number is the index of the side in the game's side array (OE has no fixed 8 colours).

    /// <summary>
    /// The player whose day is being processed. Set by the plugin around the per-player day events (OE starts a
    /// day for all sides at once, H3 per player); outside them the local player is the current one.
    /// </summary>
    public int? CurrentOverride { get; set; }

    public int CurrentPlayer =>
        CurrentOverride ?? (sym.Has("player.local") ? Convert.ToInt32(sym.Read("player.local", Root())) : 0);

    /// <summary>All players (sides) in the game's order; empty when the symbols are missing or no game runs.</summary>
    public IReadOnlyList<object?> PlayerObjects() =>
        sym.Has("game.root") && sym.Has("player.list") ? OldenEraSymbols.Items(sym.Read("player.list", Root())) : Array.Empty<object?>();

    object? FindPlayer(int player)
    {
        var all = PlayerObjects();
        return player >= 0 && player < all.Count ? all[player] : null;
    }

    /// <summary>The game's resource object for a WoG resource id (IdMap "resource" → member of the resource heap).</summary>
    AdapterResult<object> ResourceObject(int player, int resource)
    {
        if (!sym.Has("game.root") || !sym.Has("player.list")) return Missing<object>("player.list");
        if (!sym.Has("player.resources") || !sym.Has("resource.value")) return Missing<object>("player.resources");
        if (!currentIds().TryGetEngine("resource", resource, out var sid))
            return AdapterResult<object>.Unsupported($"resource {resource} has no Olden Era equivalent (IdMap \"resource\")");
        var p = FindPlayer(player);
        if (p == null) return AdapterResult<object>.Failed($"player {player} does not exist");
        var heap = sym.Read("player.resources", p);
        if (heap == null) return AdapterResult<object>.Failed($"player {player} has no resources");
        var r = OldenEraSymbols.ReadMember(heap, sid);
        return r == null ? AdapterResult<object>.Failed($"resource '{sid}' is null") : AdapterResult<object>.Ok(r);
    }

    static AdapterResult<T> NotOk<T, U>(AdapterResult<U> r) =>
        r.Status == AdapterStatus.Unsupported ? AdapterResult<T>.Unsupported(r.Reason ?? "") : AdapterResult<T>.Failed(r.Reason ?? "");

    public AdapterResult<int> GetResource(int player, int resource)
    {
        var r = ResourceObject(player, resource);
        return r.IsOk ? AdapterResult<int>.Ok(Convert.ToInt32(sym.Read("resource.value", r.Value))) : NotOk<int, object>(r);
    }

    public AdapterResult SetResource(int player, int resource, int value)
    {
        var r = ResourceObject(player, resource);
        if (!r.IsOk) return r.AsPlain();
        int current = Convert.ToInt32(sym.Read("resource.value", r.Value));
        if (value == current) return AdapterResult.Ok;
        // The game's own "add resource" updates the top bar, quests and statistics; a plain write is the fallback.
        if (AddResourceTheGameWay(player, resource, value - current) &&
            Convert.ToInt32(sym.Read("resource.value", r.Value)) == value)
            return AdapterResult.Ok;
        sym.Write("resource.value", r.Value, value);
        return AdapterResult.Ok;
    }

    /// <summary>Gains go through the game's "add", losses through its "spend" (add ignores negative amounts).</summary>
    bool AddResourceTheGameWay(int player, int resource, int delta)
    {
        string key = delta > 0 ? "resource.add" : "resource.spend";
        if (!sym.Has("player.logic") || !sym.Has("player.resourceLogic") || !sym.Has(key)) return false;
        if (!currentIds().TryGetEngine("resource", resource, out var sid)) return false;
        var logic = OldenEraSymbols.Items(sym.Read("player.logic", null));
        if (player < 0 || player >= logic.Count || logic[player] == null) return false;
        var component = sym.Read("player.resourceLogic", logic[player]);
        if (component == null || sym.MemberOf(key) is not System.Reflection.MethodInfo method) return false;
        method.Invoke(component, new object[] { sid, Math.Abs(delta) });
        return true;
    }

    public AdapterResult<bool> IsHuman(int player)
    {
        if (!sym.Has("player.isHuman")) return Missing<bool>("player.isHuman");
        var p = FindPlayer(player);
        if (p == null) return AdapterResult<bool>.Failed($"player {player} does not exist");
        return AdapterResult<bool>.Ok(Convert.ToInt32(sym.Read("player.isHuman", p)) == 0);
    }

    public AdapterResult<bool> IsLocal(int player)
    {
        if (!sym.Has("player.local")) return Missing<bool>("player.local");
        if (Root() is not { } root) return AdapterResult<bool>.Failed("no game session");
        return AdapterResult<bool>.Ok(Convert.ToInt32(sym.Read("player.local", root)) == player);
    }

    public AdapterResult<bool> IsAlive(int player)
    {
        if (!sym.Has("player.alive")) return Missing<bool>("player.alive");
        var p = FindPlayer(player);
        if (p == null) return AdapterResult<bool>.Ok(false);
        return AdapterResult<bool>.Ok(Convert.ToInt32(sym.Read("player.alive", p)) == 0);
    }
    public AdapterResult<int> GetActiveHero(int player)
    {
        if (!sym.Has("player.activeHero") || !sym.Has("hero.id")) return Missing<int>("player.activeHero");
        var p = FindPlayer(player);
        if (p == null) return AdapterResult<int>.Failed($"player {player} does not exist");
        int id = Convert.ToInt32(sym.Read("player.activeHero", p));
        // OE may remember a last selected hero the player no longer has; only a hero of this player counts.
        if (id < 0 || GetHeroes(player) is not { IsOk: true } mine) return AdapterResult<int>.Ok(-1);
        int n = HeroNumber(id);
        return AdapterResult<int>.Ok(mine.Value.Contains(n) ? n : -1);
    }

    public AdapterResult<int> GetActiveTown(int player) => Missing<int>("player.activeTown");

    // The tavern of a side offers two heroes (Side.heroesHirePool.heroes: Olden Era hero ids) [V-game: 118, 70].
    public AdapterResult<(int Left, int Right)> GetTavernHeroes(int player)
    {
        if (!sym.Has("player.tavern")) return Missing<(int, int)>("player.tavern");
        if (FindPlayer(player) is not { } p) return AdapterResult<(int, int)>.Failed($"player {player} does not exist");
        var ids = OldenEraSymbols.Items(sym.Read("player.tavern", p)).Select(x => x == null ? -1 : HeroNumber(Convert.ToInt32(x))).ToList();
        return AdapterResult<(int, int)>.Ok((ids.Count > 0 ? ids[0] : -1, ids.Count > 1 ? ids[1] : -1));
    }

    public AdapterResult SetTavernHeroes(int player, int left, int right) =>
        AdapterResult.Unsupported("OW:V — choosing the heroes of the tavern is not mapped yet");

    public AdapterResult<IReadOnlyList<int>> GetHeroes(int player)
    {
        if (!sym.Has("player.heroes") || !sym.Has("hero.id")) return Missing<IReadOnlyList<int>>("player.heroes");
        var p = FindPlayer(player);
        if (p == null) return AdapterResult<IReadOnlyList<int>>.Failed($"player {player} does not exist");
        IReadOnlyList<int> list = OldenEraSymbols.Items(sym.Read("player.heroes", p)).Where(x => x != null)
            .Select(x => HeroNumber(Convert.ToInt32(x))).Where(n => n >= 0).ToList();
        return AdapterResult<IReadOnlyList<int>>.Ok(list);
    }

    // ---- creature types (static data lives in Core.zip; runtime edits need the unit DB symbol) ----

    // ---- creature types ---------------------------------------------------------------------
    // An Olden Era unit type is a global config (UnitLogicConfig) shared by every unit of that type; a session Unit
    // resolves it from its sid (Unit.ctzt) [V-game 0.81.04: esquire → offence 4, defence 4, hp 12, damage 2-3,
    // speed 4, initiative 5, tier 1, fraction human, cost 85 gold]. Changing it changes the type for the whole game, which
    // is what MA does. H3 stats map by meaning: Attack → offence, Defence → defence, Hit Points → hp, Speed → speed,
    // damage → damageMin/Max, Level → tier − 1, Town → the faction's H3 town, Upgrade → upgradeSid, Fight/AI value →
    // squadValue. Shots, casts, growth and adventure-map counts have no Olden Era unit stat.

    readonly Dictionary<string, object?> unitConfigs = new();

    object? UnitConfig(int type)
    {
        if (!sym.Has("unit.config") || !sym.Has("stack.create") || !sym.Has("stack.unitSid")) return null;
        if (!currentIds().TryGetEngine("creature", type, out var sid)) return null;
        if (unitConfigs.TryGetValue(sid, out var cached)) return cached;
        object? config = null;
        try
        {
            var unit = Activator.CreateInstance(sym.TypeOf("stack.create"))!;
            sym.Write("stack.unitSid", unit, sid);
            config = sym.Read("unit.config", unit);
        }
        catch (Exception) { config = null; }
        unitConfigs[sid] = config;
        return config;
    }

    static readonly Dictionary<CreatureStat, string> UnitStatField = new()
    {
        [CreatureStat.Attack] = "offence", [CreatureStat.Defence] = "defence", [CreatureStat.HitPoints] = "hp",
        [CreatureStat.Speed] = "speed", [CreatureStat.DamageLow] = "damageMin", [CreatureStat.DamageHigh] = "damageMax",
    };

    static readonly Dictionary<string, int> TownOfFraction = new()
    {
        ["human"] = 0, ["nature"] = 1, ["demon"] = 3, ["undead"] = 4, ["dungeon"] = 5, ["unfrozen"] = 8,
    };

    bool ICreatureTypeAdapter.Exists(int type) => UnitConfig(type) != null;

    public AdapterResult<int> Get(int type, CreatureStat stat)
    {
        if (UnitConfig(type) is not { } cfg) return AdapterResult<int>.Failed($"creature {type} has no Olden Era unit");
        if (UnitStatField.TryGetValue(stat, out var field))
            return AdapterResult<int>.Ok(Convert.ToInt32(OldenEraSymbols.ReadMember(OldenEraSymbols.ReadMember(cfg, "stats")!, field)));
        switch (stat)
        {
            case CreatureStat.Level:
                return AdapterResult<int>.Ok(Convert.ToInt32(OldenEraSymbols.ReadMember(cfg, "tier")) - 1);
            case CreatureStat.Town:
                return AdapterResult<int>.Ok(OldenEraSymbols.ReadMember(cfg, "fraction") is string f && TownOfFraction.TryGetValue(f, out int t) ? t : -1);
            case CreatureStat.UpgradeTo:
                return AdapterResult<int>.Ok(OldenEraSymbols.ReadMember(cfg, "upgradeSid") is string up && currentIds().TryGetWoG("creature", up, out int n) ? n : -1);
            case CreatureStat.FightValue:
            case CreatureStat.AiValue:
                return AdapterResult<int>.Ok(Convert.ToInt32(OldenEraSymbols.ReadMember(cfg, "squadValue")));
            default:
                return AdapterResult<int>.Unsupported($"Olden Era units have no {stat}");
        }
    }

    public AdapterResult Set(int type, CreatureStat stat, int value)
    {
        if (UnitConfig(type) is not { } cfg) return AdapterResult.Failed($"creature {type} has no Olden Era unit");
        if (UnitStatField.TryGetValue(stat, out var field))
        {
            OldenEraSymbols.WriteMember(OldenEraSymbols.ReadMember(cfg, "stats")!, field, value);
            return AdapterResult.Ok;
        }
        if (stat is CreatureStat.FightValue or CreatureStat.AiValue)
        {
            OldenEraSymbols.WriteMember(cfg, "squadValue", value);
            return AdapterResult.Ok;
        }
        return AdapterResult.Unsupported($"changing {stat} of an Olden Era unit type is not mapped");
    }

    object? CostEntry(object cfg, int resource)
    {
        if (!currentIds().TryGetEngine("resource", resource, out var name)) return null;
        var cost = OldenEraSymbols.ReadMember(cfg, "unitCost");
        if (cost == null) return null;
        foreach (var e in OldenEraSymbols.Items(OldenEraSymbols.ReadMember(cost, "costResArray")))
            if (e != null && OldenEraSymbols.ReadMember(e, "name") as string == name) return e;
        return null;
    }

    public AdapterResult<int> GetCost(int type, int resource)
    {
        if (UnitConfig(type) is not { } cfg) return AdapterResult<int>.Failed($"creature {type} has no Olden Era unit");
        var e = CostEntry(cfg, resource);
        return AdapterResult<int>.Ok(e == null ? 0 : Convert.ToInt32(OldenEraSymbols.ReadMember(e, "cost")));
    }

    public AdapterResult SetCost(int type, int resource, int value)
    {
        if (UnitConfig(type) is not { } cfg) return AdapterResult.Failed($"creature {type} has no Olden Era unit");
        var e = CostEntry(cfg, resource);
        if (e == null)
            return value == 0 ? AdapterResult.Ok : AdapterResult.Unsupported("adding a resource to an Olden Era unit's cost is not mapped");
        OldenEraSymbols.WriteMember(e, "cost", value);
        return AdapterResult.Ok;
    }


    // ---- map / towns ------------------------------------------------------------------------

    // Olden Era's map is a grid of nodes (node = x + z·sizeX) with z growing to the north and no underground; H3
    // counts y from the top, so y = sizeZ − 1 − z [V-game: a hero standing at its town's south gate has the smaller
    // z]. An Olden Era object may have several entrance nodes; it stands at the first one in H3 scan order, so it is
    // counted once. Scenery and objects without a row in id-maps/object.json are not objects for ERM.

    /// <summary>Olden Era object sid → H3 type/subtype (id-maps/object.json); null when the file is missing.</summary>
    public ObjectTypeMap? ObjectTypes { get; set; }

    static readonly string[] MapKeys = { "map.root", "map.sizeX", "map.sizeZ" };
    static readonly string[] ObjectKeys = { "map.objects", "mapobj.id", "mapobj.node", "mapobj.entrances", "mapobj.sid" };

    object? MapRoot() => sym.Has("map.root") ? sym.Read("map.root", null) : null;

    (int sx, int sz)? MapDims()
    {
        if (MapKeys.Any(k => !sym.Has(k)) || MapRoot() is not { } map) return null;
        return (Convert.ToInt32(sym.Read("map.sizeX", map)), Convert.ToInt32(sym.Read("map.sizeZ", map)));
    }

    static MapPos NodeToPos(int node, int sx, int sz) => node < 0 ? MapPos.None : new(node % sx, sz - 1 - node / sx, 0);

    static int PosToNode(MapPos p, int sx, int sz) =>
        p.L != 0 || p.X < 0 || p.X >= sx || p.Y < 0 || p.Y >= sz ? -1 : p.X + (sz - 1 - p.Y) * sx;

    public AdapterResult<bool> IsRandomMap()
    {
        if (!sym.Has("map.data") || !sym.Has("map.generatorChecksum")) return Missing<bool>("map.generatorChecksum");
        if (MapRoot() is not { } map || sym.Read("map.data", map) is not { } data) return AdapterResult<bool>.Failed("no game session");
        return AdapterResult<bool>.Ok(sym.Read("map.generatorChecksum", data) is string s && s.Length > 0);
    }

    // The AI difficulty chosen when the game was started (StartInfo.settings.AiDifficulty) [V-game: 1 for "low"];
    // its scale is taken as H3's 0..4 [UNVERIFIED].
    public AdapterResult<int> GetDifficulty()
    {
        if (!sym.Has("game.difficulty")) return Missing<int>("game.difficulty");
        return sym.Read("game.difficulty", null) is { } v ? AdapterResult<int>.Ok(Convert.ToInt32(v)) : AdapterResult<int>.Failed("no game session");
    }

    public AdapterResult SetDifficulty(int level)
    {
        if (!sym.Has("game.difficulty")) return Missing("game.difficulty");
        sym.Write("game.difficulty", null, level);
        return AdapterResult.Ok;
    }

    /// <summary>Players of one Olden Era alliance (Data.alliances.list[].sides) share the lowest player number among them.</summary>
    public AdapterResult<int> GetTeam(int player)
    {
        if (!sym.Has("alliance.list") || !sym.Has("alliance.sides") || !sym.Has("player.id")) return Missing<int>("alliance.list");
        if (Root() is not { } root) return AdapterResult<int>.Failed("no game session");
        var sides = PlayerObjects();
        if (player < 0 || player >= sides.Count || sides[player] == null) return AdapterResult<int>.Failed($"player {player} is not in the game");
        int side = Convert.ToInt32(sym.Read("player.id", sides[player]));
        foreach (var alliance in OldenEraSymbols.Items(sym.Read("alliance.list", root)))
        {
            if (alliance == null) continue;
            var members = OldenEraSymbols.Items(sym.Read("alliance.sides", alliance)).Select(Convert.ToInt32).ToList();
            if (members.Contains(side)) return AdapterResult<int>.Ok(members.Select(PlayerOfSide).Where(p => p >= 0).DefaultIfEmpty(player).Min());
        }
        return AdapterResult<int>.Ok(player);
    }

    public AdapterResult<(int Size, int Levels)> GetSize()
    {
        if (MapKeys.FirstOrDefault(k => !sym.Has(k)) is { } missing) return Missing<(int, int)>(missing);
        if (MapDims() is not var (sx, sz)) return AdapterResult<(int, int)>.Failed("no game session");
        return AdapterResult<(int, int)>.Ok((Math.Max(sx, sz), 0));
    }

    /// <summary>Counts game frames (the plugin's frame hook); one ERM call runs within one frame, so map data read
    /// once per frame is current for the whole call.</summary>
    public static long Frame;

    /// <summary>The map as ERM sees it, read once per frame (scripts scan every square in one call).</summary>
    sealed class MapSnapshot
    {
        public long Frame, Time;
        public int SizeX, SizeZ;
        public List<WoGMapObject> Objects = new();
        public Dictionary<int, WoGMapObject> ById = new();   // Olden Era map object id → object
        public int[] ObjectAt = Array.Empty<int>();   // node → index in Objects, -1 none
        public bool[] Entrance = Array.Empty<bool>(); // node is an object's ERM position (yellow square)
        public bool[] Blocked = Array.Empty<bool>();  // node is covered by any object, scenery included (red square)
        public byte[] Land = Array.Empty<byte>();
        public byte[] Road = Array.Empty<byte>();
    }

    MapSnapshot? snapshot;

    AdapterResult<MapSnapshot> Snapshot()
    {
        if (MapKeys.Concat(ObjectKeys).FirstOrDefault(k => !sym.Has(k)) is { } missing) return Missing<MapSnapshot>(missing);
        if (ObjectTypes == null) return AdapterResult<MapSnapshot>.Unsupported("the object type map (id-maps/object.json) is not installed");
        if (MapDims() is not var (sx, sz) || MapRoot() is not { } map) return AdapterResult<MapSnapshot>.Failed("no game session");
        long now = Environment.TickCount64;
        if (snapshot != null && ((Frame > 0 && snapshot.Frame == Frame) || now - snapshot.Time < 250)) return AdapterResult<MapSnapshot>.Ok(snapshot);

        var snap = new MapSnapshot { Frame = Frame, Time = now, SizeX = sx, SizeZ = sz };
        int nodes = sx * sz;
        snap.ObjectAt = Enumerable.Repeat(-1, nodes).ToArray();
        snap.Entrance = new bool[nodes];
        snap.Blocked = new bool[nodes];
        snap.Land = new byte[nodes];
        snap.Road = new byte[nodes];
        ReadTerrain(map, snap);

        var owners = new Dictionary<int, int>();
        if (sym.Has("object.list") && sym.Has("object.mapId") && sym.Has("object.owner") && Root() is { } root)
            foreach (var o in OldenEraSymbols.Items(sym.Read("object.list", root)))
                if (o != null) owners[Convert.ToInt32(sym.Read("object.mapId", o))] = Convert.ToInt32(sym.Read("object.owner", o));

        var covered = new List<(WoGMapObject Obj, List<int> Nodes)>();
        var found = new List<(object Obj, string Sid, int Type, int Subtype, List<int> Blocked)>();
        foreach (var o in OldenEraSymbols.Items(sym.Read("map.objects", map)))
        {
            if (o == null) continue;
            var blocked = sym.Has("mapobj.blocked")
                ? OldenEraSymbols.Items(sym.Read("mapobj.blocked", o)).Select(Convert.ToInt32).Where(n => n >= 0 && n < nodes).Distinct().ToList()
                : new List<int>();
            foreach (int n in blocked) snap.Blocked[n] = true;
            if (sym.Read("mapobj.sid", o) is not string sid || !ObjectTypes.TryGet(sid, out int type, out int subtype)) continue;
            found.Add((o, sid, type, subtype, blocked));
        }
        // A pick-up (resource, chest, artifact, prison) occupies one node with entrances all around: as in H3 a hero
        // steps onto it, so that node is its position. A building stands at its first entrance in H3 scan order that
        // no pick-up lies on: in H3 a square holds one object, while an Olden Era map can put a resource pile on a
        // mine's entrance [V-game: resource_gold and mine_gold both at 67/6/0 of the test map].
        var taken = new HashSet<int>(found.Where(f => f.Blocked.Count == 1).Select(f => f.Blocked[0]));
        foreach (var (o, sid, type, subtype, blocked) in found)
        {
            var pos = MapPos.None;
            if (blocked.Count == 1) pos = NodeToPos(blocked[0], sx, sz);
            else
            {
                var entrances = OldenEraSymbols.Items(sym.Read("mapobj.entrances", o)).Select(Convert.ToInt32)
                    .Select(n => (Node: n, Pos: NodeToPos(n, sx, sz))).OrderBy(e => e.Pos.Y).ThenBy(e => e.Pos.X).ToList();
                var free = entrances.Where(e => !taken.Contains(e.Node)).DefaultIfEmpty(entrances.FirstOrDefault()).First();
                if (entrances.Count > 0)
                {
                    pos = free.Pos;
                    taken.Add(free.Node);
                }
            }
            if (pos.IsNone) pos = NodeToPos(Convert.ToInt32(sym.Read("mapobj.node", o)), sx, sz);
            int id = Convert.ToInt32(sym.Read("mapobj.id", o));
            int owner = owners.TryGetValue(id, out int side) && side >= 0 ? PlayerOfSide(side) : -1;
            var obj = new WoGMapObject { Position = pos, Type = type, SubType = subtype, Owner = owner, Sid = sid };
            snap.Objects.Add(obj);
            snap.ById[id] = obj;
            covered.Add((obj, blocked));
        }
        AddMonsters(snap.Objects, sx, sz);
        AddHeroes(snap.Objects, sx, sz);
        snap.Objects.Sort((a, b) => a.Position.Y != b.Position.Y ? a.Position.Y.CompareTo(b.Position.Y) : a.Position.X.CompareTo(b.Position.X));
        var index = new Dictionary<WoGMapObject, int>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < snap.Objects.Count; i++) index[snap.Objects[i]] = i;
        foreach (var (obj, blocked) in covered)
            foreach (int n in blocked) if (snap.ObjectAt[n] < 0) snap.ObjectAt[n] = index[obj];
        for (int i = 0; i < snap.Objects.Count; i++)
        {
            int n = PosToNode(snap.Objects[i].Position, sx, sz);
            if (n < 0 || snap.Objects[i].Type == 34) continue; // a hero stands on a square; it does not make it an entrance
            snap.ObjectAt[n] = i;
            snap.Entrance[n] = true;
            snap.Blocked[n] = true;
        }
        snapshot = snap;
        return AdapterResult<MapSnapshot>.Ok(snap);
    }

    // Olden Era biomes (DB/map/tiles/tiles.json ids) as the H3 terrain of the town whose faction lives there (Format
    // TR): grass 1 → grass, sand 2 → sand, deathland 3 → dirt (Necropolis), snow 4 → snow, autumn 5 → grass (Rampart),
    // lava 6 → lava, dirt 7 → dirt. Water (DB/map/waters) → water, its lava kind → lava.
    static readonly byte[] LandOfTile = { 2, 2, 1, 0, 3, 2, 7, 0 };

    void ReadTerrain(object map, MapSnapshot snap)
    {
        if (!sym.Has("map.data") || sym.Read("map.data", map) is not { } data) { Array.Fill(snap.Land, (byte)2); return; }
        byte[] Bytes(string key) => sym.Has(key) && sym.Read(key, data) is { } arr ? OldenEraSymbols.Items(arr).Select(Convert.ToByte).ToArray() : Array.Empty<byte>();
        var tiles = Bytes("map.tiles");
        var water = Bytes("map.water");
        var roads = Bytes("map.roads");
        for (int n = 0; n < snap.Land.Length; n++)
        {
            int w = n < water.Length ? water[n] : 0;
            int t = n < tiles.Length ? tiles[n] : 1;
            snap.Land[n] = w != 0 ? (byte)(w == 6 ? 7 : 8) : t < LandOfTile.Length ? LandOfTile[t] : (byte)2;
            snap.Road[n] = n < roads.Length ? roads[n] : (byte)0;
        }
    }

    /// <summary>The map objects ERM can see (one snapshot per frame).</summary>
    public AdapterResult<IReadOnlyList<WoGMapObject>> GetObjects()
    {
        var s = Snapshot();
        return s.Status == AdapterStatus.Ok ? AdapterResult<IReadOnlyList<WoGMapObject>>.Ok(s.Value.Objects) : s.Error<IReadOnlyList<WoGMapObject>>();
    }

    public AdapterResult<MapSquare> GetSquare(MapPos pos)
    {
        var r = Snapshot();
        if (r.Status != AdapterStatus.Ok) return r.Error<MapSquare>();
        var s = r.Value;
        int n = PosToNode(pos, s.SizeX, s.SizeZ);
        if (n < 0) return AdapterResult<MapSquare>.Failed($"square {pos} is outside the map");
        int i = s.ObjectAt[n];
        var o = i >= 0 ? s.Objects[i] : null;
        return AdapterResult<MapSquare>.Ok(new MapSquare(o?.Type ?? 0, o?.SubType ?? 0, s.Entrance[n], s.Blocked[n], s.Land[n], s.Road[n],
            o?.Position ?? MapPos.None));
    }

    static readonly string[] SquadKeys = { "squad.list", "squad.node", "squad.units", "squad.released", "squadunit.sid" };

    /// <summary>
    /// Wandering monsters: an Olden Era squad (several unit stacks on one node) is H3's monster object (type 54); its
    /// subtype is the creature number (creature.json) of its first unit. Defeated squads stay in the list as released.
    /// </summary>
    void AddMonsters(List<WoGMapObject> list, int sx, int sz)
    {
        if (SquadKeys.Any(k => !sym.Has(k)) || Root() is not { } root) return;
        var ids = currentIds();
        foreach (var s in OldenEraSymbols.Items(sym.Read("squad.list", root)))
        {
            if (s == null || Convert.ToBoolean(sym.Read("squad.released", s))) continue;
            var pos = NodeToPos(Convert.ToInt32(sym.Read("squad.node", s)), sx, sz);
            if (pos.IsNone) continue;
            var first = OldenEraSymbols.Items(sym.Read("squad.units", s)).FirstOrDefault(u => u != null);
            string? unit = first == null ? null : sym.Read("squadunit.sid", first) as string;
            int creature = unit != null && ids.TryGetWoG("creature", unit, out int n) ? n : -1;
            list.Add(new WoGMapObject { Position = pos, Type = 54, SubType = creature, Sid = unit });
        }
    }

    /// <summary>Heroes on the map are objects of type 34 with their WoG number as subtype (Format OB).</summary>
    void AddHeroes(List<WoGMapObject> list, int sx, int sz)
    {
        if (!sym.Has("hero.position")) return;
        foreach (var (n, h) in HeroTable())
        {
            var pos = NodeToPos(Convert.ToInt32(sym.Read("hero.position", h)), sx, sz);
            if (pos.IsNone) continue;
            int owner = sym.Has("hero.owner") ? PlayerOfSide(Convert.ToInt32(sym.Read("hero.owner", h))) : -1;
            list.Add(new WoGMapObject { Position = pos, Type = 34, SubType = n, Owner = owner, Sid = "hero" });
        }
    }

    /// <summary>
    /// The WoG view of a visit: the object's logic (fnt) and the hero's logic (fdq) → visiting hero, its owner, the
    /// object's ERM position and H3 type/subtype. Null when the object is not an ERM object or the hero is unknown.
    /// </summary>
    public WoG.Core.Events.WoGEvent? DescribeVisit(object objectLogic, object? heroLogic, out string why)
    {
        why = "";
        if (!sym.Has("visit.mapObject") || !sym.Has("visitor.hero")) { why = "visit symbols not verified"; return null; }
        if (heroLogic == null) { why = "no hero logic argument"; return null; }
        var mapObj = sym.Read("visit.mapObject", MethodTraceReal(objectLogic));
        var hero = sym.Read("visitor.hero", heroLogic);
        if (mapObj == null || hero == null) { why = $"map object {(mapObj == null ? "null" : "ok")}, hero {(hero == null ? "null" : "ok")}"; return null; }
        int id = Convert.ToInt32(sym.Read("mapobj.id", mapObj));
        var snap = Snapshot();
        if (snap.Status != AdapterStatus.Ok) { why = "map: " + snap.Reason; return null; }
        if (!snap.Value.ById.TryGetValue(id, out var obj)) { why = $"map object {id} ({sym.Read("mapobj.sid", mapObj)}) is not an ERM object"; return null; }
        int number = HeroNumber(HeroEngineId(hero));
        int owner = sym.Has("hero.owner") ? PlayerOfSide(Convert.ToInt32(sym.Read("hero.owner", hero))) : CurrentPlayer;
        return new WoG.Core.Events.WoGEvent
        {
            Player = owner, Hero = number, Position = obj.Position, ObjectType = obj.Type, ObjectSubType = obj.SubType,
        };
    }

    /// <summary>A member of a game event argument through a symbol; null when it cannot be read.</summary>
    public object? EventValue(object? arg, string key)
    {
        if (arg == null || !sym.Has(key)) return null;
        try { return sym.Read(key, MethodTraceReal(arg)); }
        catch (Exception) { return null; }
    }

    /// <summary>The checksum of the save the current session was loaded from; null for a new game.</summary>
    public string? LoadedSaveHash()
    {
        if (!sym.Has("session.startInfo") || !sym.Has("startInfo.load") || !sym.Has("startInfo.hash")) return null;
        var info = sym.Read("session.startInfo", null);
        if (info == null || Convert.ToInt32(sym.Read("startInfo.load", info)) != 1) return null; // ELoad.LoadSave
        return sym.Read("startInfo.hash", info) as string;
    }

    /// <summary>An int member of a game event argument through a symbol; -1 when it cannot be read.</summary>
    public int EventInt(object? arg, string key)
    {
        if (arg == null || !sym.Has(key)) return -1;
        try { return Convert.ToInt32(sym.Read(key, MethodTraceReal(arg))); }
        catch (Exception) { return -1; }
    }

    /// <summary>The WoG player of a battle side (side id; -1 or unknown: the player whose turn it is) and that player's active hero.</summary>
    public (int Player, int Hero) BattleParticipant(int side)
    {
        int player = side >= 0 ? PlayerOfSide(side) : -1;
        if (player < 0) player = CurrentPlayer;
        var hero = GetActiveHero(player);
        return (player, hero.Status == AdapterStatus.Ok ? hero.Value : -1);
    }

    /// <summary>
    /// The hero a game event is about: the first member of the event argument that is a hero's logic (fdq) or a session
    /// hero; with its owner and map position. Hero −1 when the event names no hero of this game.
    /// </summary>
    public (int Hero, int Player, MapPos Pos) EventHero(object? arg)
    {
        if (arg == null || !sym.Has("visitor.hero")) return (-1, -1, MapPos.None);
        var real = MethodTraceReal(arg);
        object? hero = null;
        foreach (var p in real.GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
        {
            if (p.GetIndexParameters().Length > 0) continue;
            string type = p.PropertyType.FullName ?? "";
            if (type == "fdq") { var v = p.GetValue(real); hero = v == null ? null : sym.Read("visitor.hero", v); }
            else if (type == "Hex.Session.Data.Hero") hero = p.GetValue(real);
            if (hero != null) break;
        }
        if (hero == null) return (-1, -1, MapPos.None);
        int number = HeroNumber(HeroEngineId(hero));
        int player = sym.Has("hero.owner") ? PlayerOfSide(Convert.ToInt32(sym.Read("hero.owner", hero))) : -1;
        var pos = number >= 0 && GetPosition(number) is { Status: AdapterStatus.Ok } r ? r.Value : MapPos.None;
        return (number, player, pos);
    }

    /// <summary>A game object re-wrapped as its runtime class (Harmony hands over the declared base type).</summary>
    public static Func<object, object> MethodTraceReal = o => o;

    AdapterResult<WoGMapObject> ObjectAt(MapPos pos)
    {
        var all = GetObjects();
        if (all.Status != AdapterStatus.Ok) return all.Error<WoGMapObject>();
        // An Olden Era squad can stand on the entrance of what it guards (a mine) [V-game: MN at a mine's position found
        // the squad]: the building is the object of the square, as in H3, where a square has one object.
        var o = all.Value.Where(x => x.Position == pos).OrderBy(x => x.Type is 54 or 34 ? 1 : 0).FirstOrDefault();
        return o == null ? AdapterResult<WoGMapObject>.Failed($"no object at {pos}") : AdapterResult<WoGMapObject>.Ok(o);
    }

    public AdapterResult<(int type, int subtype)> GetObjectAt(MapPos pos)
    {
        var o = ObjectAt(pos);
        return o.Status == AdapterStatus.Ok ? AdapterResult<(int, int)>.Ok((o.Value.Type, o.Value.SubType)) : o.Error<(int, int)>();
    }

    public AdapterResult<int> GetObjectOwner(MapPos pos)
    {
        var o = ObjectAt(pos);
        return o.Status == AdapterStatus.Ok ? AdapterResult<int>.Ok(o.Value.Owner) : o.Error<int>();
    }

    /// <summary>The map object whose ERM position is <paramref name="pos"/> and its Olden Era map object id.</summary>
    AdapterResult<int> MapIdAt(MapPos pos)
    {
        var snap = Snapshot();
        if (!snap.IsOk) return snap.Error<int>();
        foreach (var (id, o) in snap.Value.ById)
            if (o.Position == pos) return AdapterResult<int>.Ok(id);
        return AdapterResult<int>.Failed("no object");
    }

    object? SessionObject(int mapId) =>
        OldenEraSymbols.Items(sym.Read("object.list", Root())).FirstOrDefault(o => o != null && Convert.ToInt32(sym.Read("object.mapId", o)) == mapId);

    // The game's change of owner of a map object (fnt.bmiq(side id), -1 neutral) [V-game on a city].
    public AdapterResult SetObjectOwner(MapPos pos, int owner)
    {
        if (!sym.Has("world.objectLogics") || !sym.Has("object.setOwner")) return Missing("object.setOwner");
        var id = MapIdAt(pos);
        if (!id.IsOk) return id.AsPlain();
        int side = -1;
        if (owner >= 0)
        {
            if (!sym.Has("player.id") || FindPlayer(owner) is not { } p) return AdapterResult.Failed($"player {owner} is not in the game");
            side = Convert.ToInt32(sym.Read("player.id", p));
        }
        var all = sym.Read("world.objectLogics", null);
        if (all == null || !Convert.ToBoolean(OldenEraSymbols.Call(all, "ContainsKey", id.Value))) return AdapterResult.Failed("the object has no map logic");
        OldenEraSymbols.Call(MethodTrace.Real(OldenEraSymbols.Call(all, "get_Item", id.Value)!), sym.MemberOf("object.setOwner")!.Name, side);
        snapshot = null; // the owners of the map snapshot are read again
        return AdapterResult.Ok;
    }

    // An object's own guards are its session object's party (DataObject.garnisonParty) [V-game: empty for the mines of
    // the test map; Olden Era guards mines with squads on the map]. Setting them is not mapped.
    public AdapterResult<WoGStack> GetObjectGuard(MapPos pos, int slot)
    {
        if (!sym.Has("object.list") || !sym.Has("object.mapId") || !sym.Has("town.garrison") || !StackKeys.All(sym.Has)) return Missing<WoGStack>("town.garrison");
        var id = MapIdAt(pos);
        if (!id.IsOk) return id.Error<WoGStack>();
        if (SessionObject(id.Value) is not { } o) return AdapterResult<WoGStack>.Ok(new WoGStack());
        return slot >= WoGLimits.ArmySlots ? AdapterResult<WoGStack>.Ok(new WoGStack()) : ReadStack(sym.Read("town.garrison", MethodTrace.Real(o)), slot);
    }

    public AdapterResult SetObjectGuard(MapPos pos, int slot, int type, int count) =>
        AdapterResult.Unsupported("the guards of a map object are not mapped: Olden Era guards mines with squads on the map");

    // ---- towns ------------------------------------------------------------------------------
    // Olden Era's cities are session objects (Data.objects: ObjCity) with their buildings (BuildingsData: one
    // BuildingData per building of the faction, sid + level + isConstructed + bansPerLevel), dwellings (BuildingHire:
    // HiredUnitSet currentAmount / weeklyIncrement), garrison (garnisonParty) and heroes (garnisonHeroId /
    // visitorHeroId = Olden Era hero ids) [V-game: peeked in a skirmish]. WoG numbers towns 0..n-1 in the order of
    // that list (H3: the order of the map file). The map object of a city (idMapObject) gives its ERM position.

    static readonly string[] TownKeys = { "object.list", "town.class", "object.mapId", "object.owner", "town.buildings", "building.sid", "building.level", "building.built" };

    List<object>? towns;
    long townCacheTime;

    /// <summary>The cities of this game in WoG numbering, refreshed at most every 100 ms.</summary>
    List<object> TownTable()
    {
        long now = Environment.TickCount64;
        if (towns != null && now - townCacheTime < 100) return towns;
        townCacheTime = now;
        towns = new List<object>();
        if (Root() is not { } root) return towns;
        var cls = sym.TypeOf("town.class");
        foreach (var o in OldenEraSymbols.Items(sym.Read("object.list", root)))
            if (o != null && MethodTrace.Real(o) is { } real && cls.IsInstanceOfType(real)) towns.Add(real);
        return towns;
    }

    AdapterResult<object> City(int town)
    {
        if (TownKeys.FirstOrDefault(k => !sym.Has(k)) is { } missing) return Missing<object>(missing);
        var all = TownTable();
        return town >= 0 && town < all.Count ? AdapterResult<object>.Ok(all[town]) : AdapterResult<object>.Failed($"town {town} does not exist");
    }

    AdapterResult<T> FromCity<T>(int town, Func<object, AdapterResult<T>> read)
    {
        var c = City(town);
        return c.IsOk ? read(c.Value) : c.Error<T>();
    }

    AdapterResult ChangeCity(int town, Func<object, AdapterResult> write)
    {
        var c = City(town);
        return c.IsOk ? write(c.Value) : c.AsPlain();
    }

    public AdapterResult<int> TownCount() =>
        TownKeys.FirstOrDefault(k => !sym.Has(k)) is { } missing ? Missing<int>(missing) : AdapterResult<int>.Ok(TownTable().Count);

    public AdapterResult<int> TownAt(MapPos pos)
    {
        if (TownKeys.FirstOrDefault(k => !sym.Has(k)) is { } missing) return Missing<int>(missing);
        var snap = Snapshot();
        if (!snap.IsOk) return snap.Error<int>();
        var all = TownTable();
        for (int i = 0; i < all.Count; i++)
            if (snap.Value.ById.TryGetValue(Convert.ToInt32(sym.Read("object.mapId", all[i])), out var o) && o.Position == pos)
                return AdapterResult<int>.Ok(i);
        return AdapterResult<int>.Failed("no town");
    }

    public AdapterResult<int> CurrentTown() => Missing<int>("town.current");

    public AdapterResult<MapPos> GetTownPosition(int town) => FromCity(town, c =>
    {
        var snap = Snapshot();
        if (!snap.IsOk) return snap.Error<MapPos>();
        return snap.Value.ById.TryGetValue(Convert.ToInt32(sym.Read("object.mapId", c)), out var o)
            ? AdapterResult<MapPos>.Ok(o.Position) : AdapterResult<MapPos>.Failed("the town has no map object");
    });

    public AdapterResult<int> GetTownOwner(int town) => FromCity(town, c =>
    {
        int side = Convert.ToInt32(sym.Read("object.owner", c));
        return AdapterResult<int>.Ok(side >= 0 ? PlayerOfSide(side) : -1);
    });

    // The game's change of owner of a map object (fnt.bmiq(side id), -1 neutral) [V-game: a neutral city given to
    // side 0 joined the player's town list, its view radius opened, the daily income grew].
    public AdapterResult SetTownOwner(int town, int owner) => ChangeCity(town, c =>
    {
        if (!sym.Has("world.objectLogics") || !sym.Has("object.setOwner")) return Missing("object.setOwner");
        int side = -1;
        if (owner >= 0)
        {
            if (!sym.Has("player.id") || FindPlayer(owner) is not { } p) return AdapterResult.Failed($"player {owner} is not in the game");
            side = Convert.ToInt32(sym.Read("player.id", p));
        }
        if (CityLogicObject(c) is not { } logic) return AdapterResult.Failed("the town has no map logic");
        OldenEraSymbols.Call(logic, sym.MemberOf("object.setOwner")!.Name, side);
        snapshot = null; // the owners of the map snapshot are read again
        return AdapterResult.Ok;
    });

    // The faction of a city (its config sid "<faction>_city") as the closest H3 town (same table as creatures).
    public AdapterResult<int> GetTownType(int town) => FromCity(town, c =>
    {
        if (!sym.Has("object.sid")) return Missing<int>("object.sid");
        string sid = sym.Read("object.sid", c) as string ?? "";
        string faction = sid.EndsWith("_city") ? sid[..^5] : sid;
        return TownOfFraction.TryGetValue(faction, out int t) ? AdapterResult<int>.Ok(t)
            : AdapterResult<int>.Unsupported($"Olden Era faction '{faction}' has no H3 town");
    });

    /// <summary>Olden Era's localized text of a key (it.iap); null when the game has none for it.</summary>
    string? Localize(string key)
    {
        if (!sym.Has("loc.text") || sym.MemberOf("loc.text") is not MethodInfo m) return null;
        var text = m.Invoke(null, new object[] { key }) as string;
        return text == null || text.StartsWith("LOC:", StringComparison.Ordinal) ? null : text;
    }

    // A city's name is a localization key (human_city_name_16 → "Кузня Сердца") [V-game].
    public AdapterResult<string> GetTownName(int town) => FromCity(town, c =>
    {
        if (!sym.Has("town.name")) return Missing<string>("town.name");
        string key = sym.Read("town.name", c) as string ?? "";
        if (Localize(key) is { } text) return AdapterResult<string>.Ok(text);
        return AdapterResult<string>.Ok(currentIds().TryGetEngine(TownNameDomain, Convert.ToInt32(sym.Read("object.mapId", c)), out var name)
            && RegisterText(key, name) ? name : key);
    });

    // A new name gets a localization key of its own (wog_town_<map object id>) in the game's table (it.btdt.cafu:
    // key → {key, text, args}), which the town shows wherever the game names it. The table is not saved: the name is
    // kept in the WoG state (IdMap "townName") and its key registered again when a game is loaded.
    const string TownNameDomain = "townName", TownNameKey = "wog_town_";

    public AdapterResult SetTownName(int town, string name) => ChangeCity(town, c =>
    {
        if (!sym.Has("town.name")) return Missing("town.name");
        int id = Convert.ToInt32(sym.Read("object.mapId", c));
        string key = TownNameKey + id;
        if (!RegisterText(key, name)) return Missing("loc.entries");
        currentIds().Set(TownNameDomain, id, name);
        sym.Write("town.name", c, key);
        return AdapterResult.Ok;
    });

    /// <summary>Registers the names WoG gave towns again (after a load: the localization table starts without them).</summary>
    public void RestoreTownNames()
    {
        if (!TownKeys.All(sym.Has) || !sym.Has("town.name")) return;
        foreach (var c in TownTable())
            if (sym.Read("town.name", c) is string key && key.StartsWith(TownNameKey, StringComparison.Ordinal)
                && currentIds().TryGetEngine(TownNameDomain, Convert.ToInt32(sym.Read("object.mapId", c)), out var name))
                RegisterText(key, name);
    }

    bool RegisterText(string key, string text)
    {
        if (!sym.Has("loc.entries") || !sym.Has("locentry.key") || !sym.Has("locentry.text") || !sym.Has("locentry.args")) return false;
        if (sym.Read("loc.entries", null) is not { } table) return false;
        object entry;
        if (Convert.ToBoolean(OldenEraSymbols.Call(table, "ContainsKey", key))) entry = OldenEraSymbols.Call(table, "get_Item", key)!;
        else
        {
            entry = Activator.CreateInstance(sym.TypeOf("locentry.key"))!;
            sym.Write("locentry.key", entry, key);
            sym.Write("locentry.args", entry, new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray(0));
            OldenEraSymbols.Call(table, "set_Item", key, entry);
        }
        sym.Write("locentry.text", entry, text);
        return true;
    }

    public AdapterResult<int> GetTownHero(int town, bool visitor) => FromCity(town, c =>
    {
        string key = visitor ? "town.visitorHero" : "town.garrisonHero";
        if (!sym.Has(key)) return Missing<int>(key);
        int id = Convert.ToInt32(sym.Read(key, c));
        return AdapterResult<int>.Ok(id < 0 ? -1 : HeroNumber(id));
    });

    public AdapterResult SetTownHero(int town, bool visitor, int hero) =>
        AdapterResult.Unsupported("moving a hero into a town or out of it is not mapped yet");

    // H3 buildings (ERM help, building list) as Olden Era buildings and the level that makes them built. Olden Era
    // levels are cumulative (a city hall is a main building of level 3), as WoG scripts expect of H3: they check the
    // fort before the citadel, the basic dwelling before the upgraded one (ERA Scripts option 724, Tobyn's scripts).
    static (string Sid, int Level)? OeBuilding(int b) => b switch
    {
        >= 0 and <= 4 => ("Build_Magic_Guild", b + 1),
        5 => ("Build_Tavern", 1),
        >= 7 and <= 9 => ("Build_Wall", b - 6),
        >= 10 and <= 12 => ("Build_Main", b - 9),
        14 => ("Build_Market", 1),
        15 => ("Build_Resource_Depot", 1),
        26 => (Grail, 1),
        >= 30 and <= 36 => ($"Build_Tier_{b - 29}", 1),
        >= 37 and <= 43 => ($"Build_Tier_{b - 36}", 2),
        _ => null,
    };

    const string Grail = "<grail>";
    // The grail building of each faction (DB/objects_logic/cities/*.json "graals") [V-data, 0.81.04].
    static readonly HashSet<string> GrailSids = new()
    {
        "Build_Golden_Calf", "Build_Spring_of_Life", "Build_Eternal_Flame", "Build_Gaping_Maw", "Build_Spy_Network", "Build_Frigid_Firmament",
    };

    object? BuildingOf(object city, string sid) =>
        OldenEraSymbols.Items(sym.Read("town.buildings", city))
            .FirstOrDefault(b => b != null && sym.Read("building.sid", b) is string s && (sid == Grail ? GrailSids.Contains(s) : s == sid));

    int BuiltLevel(object building) =>
        Convert.ToBoolean(sym.Read("building.built", building)) ? Convert.ToInt32(sym.Read("building.level", building)) : 0;

    /// <summary>How many levels a building has in this town (one ban flag per level: a human guild has 4).</summary>
    int Levels(object building) => sym.Has("building.bans") ? OldenEraSymbols.Items(sym.Read("building.bans", building)).Count : int.MaxValue;

    public AdapterResult<int> GetMageGuildLevel(int town) => FromCity(town, c =>
        AdapterResult<int>.Ok(BuildingOf(c, "Build_Magic_Guild") is { } g ? BuiltLevel(g) : 0));

    public AdapterResult SetMageGuildLevel(int town, int level) =>
        AdapterResult.Unsupported("CA:G1 — Olden Era's guild level is its building: build it with CA:B1/B6");

    // The spells a mage guild level offers: the city logic's list (fmr.bmdd(level 1..5) -> spell sids) [V-game: 7
    // spells at level 1 of a guild of level 1, none at level 2]. Changing them is not mapped.
    AdapterResult<IReadOnlyList<object?>> GuildSpells(object city, int level)
    {
        if (!sym.Has("world.objectLogics") || !sym.Has("citylogic.guildSpells")) return Missing<IReadOnlyList<object?>>("citylogic.guildSpells");
        if (CityLogicObject(city) is not { } logic) return AdapterResult<IReadOnlyList<object?>>.Failed("the town has no map logic");
        return AdapterResult<IReadOnlyList<object?>>.Ok(OldenEraSymbols.Items(OldenEraSymbols.Call(logic, sym.MemberOf("citylogic.guildSpells")!.Name, level + 1)));
    }

    const string GuildSpellChange = "CA:G2/G3 — changing the spells of an Olden Era mage guild is not mapped yet";

    public AdapterResult<int> GetGuildSpellCount(int town, int level) => FromCity(town, c =>
    {
        var spells = GuildSpells(c, level);
        return spells.IsOk ? AdapterResult<int>.Ok(spells.Value.Count) : spells.Error<int>();
    });

    public AdapterResult SetGuildSpellCount(int town, int level, int count) => AdapterResult.Unsupported(GuildSpellChange);

    public AdapterResult<int> GetGuildSpell(int town, int level, int slot) => FromCity(town, c =>
    {
        var spells = GuildSpells(c, level);
        if (!spells.IsOk) return spells.Error<int>();
        if (slot >= spells.Value.Count) return AdapterResult<int>.Ok(-1);
        string sid = spells.Value[slot] as string ?? "";
        return currentIds().TryGetWoG("spell", sid, out int spell) ? AdapterResult<int>.Ok(spell)
            : AdapterResult<int>.Unsupported($"Olden Era spell '{sid}' has no H3 number (id-maps/spell.json)");
    });

    public AdapterResult SetGuildSpell(int town, int level, int slot, int spell) => AdapterResult.Unsupported(GuildSpellChange);

    public AdapterResult<bool> GetBuildingFlag(int town, int building, int check) => FromCity(town, c =>
    {
        if (OeBuilding(building) is not var (sid, level) || BuildingOf(c, sid) is not { } b) return AdapterResult<bool>.Ok(false);
        if (check != 2) return AdapterResult<bool>.Ok(BuiltLevel(b) >= level); // the bonus of a building is its being built
        if (!sym.Has("building.bans")) return Missing<bool>("building.bans");
        var bans = OldenEraSymbols.Items(sym.Read("building.bans", b));
        return AdapterResult<bool>.Ok(level - 1 < bans.Count && !Convert.ToBoolean(bans[level - 1]));
    });

    public AdapterResult SetAllowed(int town, int building, bool allowed) => ChangeCity(town, c =>
    {
        if (!sym.Has("building.bans")) return Missing("building.bans");
        if (OeBuilding(building) is not var (sid, level) || BuildingOf(c, sid) is not { } b)
            return AdapterResult.Unsupported($"building {building} has no Olden Era building in this town");
        var bans = sym.Read("building.bans", b);
        if (bans == null || level - 1 >= OldenEraSymbols.Items(bans).Count) return AdapterResult.Failed("the building has no ban for that level");
        OldenEraSymbols.Call(bans, "set_Item", level - 1, !allowed);
        return AdapterResult.Ok;
    });

    // B1 and B6 build through the game's own construction, one level at a time, without its cost and keeping the
    // day's construction (WoG's B6 restores BuiltThisTurn; B1 never touches it).
    public AdapterResult SetBuilt(int town, int building, bool built) =>
        built ? Build(town, building) : Demolish(town, building);

    public AdapterResult Build(int town, int building) => ChangeCity(town, c =>
    {
        if (!sym.Has("world.objectLogics") || !sym.Has("citylogic.buildings") || !sym.Has("buildings.construct")) return Missing("buildings.construct");
        if (OeBuilding(building) is not var (sid, level) || BuildingOf(c, sid) is not { } b)
            return AdapterResult.Unsupported($"building {building} has no Olden Era building in this town");
        var logic = CityLogic(c);
        if (logic == null) return AdapterResult.Failed("the town has no map logic");
        sid = (string)sym.Read("building.sid", b)!;
        if (level > Levels(b)) return AdapterResult.Unsupported($"{sid} of this Olden Era town has {Levels(b)} levels, not {level}");
        // egz.bdkr is the player's construction: one per day, paid by the owner [V-game: Build_Wall 1 took 2500 gold
        // and 5 ore, todaysConstructionsCount 0 -> 1; a second level the same day was refused; the grail, which costs
        // a "graal" resource, was refused]. WoG's construction is free and keeps the day's one: the count is cleared
        // before each level, the owner can afford anything meanwhile, and both are restored afterwards.
        int today = sym.Has("town.builtToday") ? Convert.ToInt32(sym.Read("town.builtToday", c)) : -1;
        int side = Convert.ToInt32(sym.Read("object.owner", c)), owner = side >= 0 ? PlayerOfSide(side) : -1;
        var before = Resources(owner);
        int built = BuiltLevel(b);
        try
        {
            foreach (var (r, was) in before) sym.Write("resource.value", r, was + 1_000_000);
            for (int l = built + 1; l <= level; l++)
            {
                if (today >= 0) sym.Write("town.builtToday", c, 0);
                OldenEraSymbols.Call(logic, sym.MemberOf("buildings.construct")!.Name, sid, l);
                if (BuiltLevel(b) < l) return AdapterResult.Failed($"the game does not let {sid} reach level {l} yet (what it needs is not built)");
            }
        }
        finally
        {
            if (today >= 0) sym.Write("town.builtToday", c, today);
            foreach (var (r, was) in before) sym.Write("resource.value", r, was);
        }
        return AdapterResult.Ok;
    });

    /// <summary>All resource objects of a player with their amounts (the heap holds more than H3's seven).</summary>
    List<(object Resource, int Value)> Resources(int player)
    {
        var all = new List<(object, int)>();
        if (!sym.Has("player.resources") || !sym.Has("resource.value") || FindPlayer(player) is not { } p) return all;
        if (sym.Read("player.resources", p) is not { } heap) return all;
        var resType = sym.TypeOf("resource.value");
        foreach (var prop in heap.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            if (prop.PropertyType == resType && prop.GetValue(heap) is { } r)
                all.Add((r, Convert.ToInt32(sym.Read("resource.value", r))));
        return all;
    }

    // B2: Olden Era has no demolition of buildings.
    AdapterResult Demolish(int town, int building) => ChangeCity(town, c =>
    {
        if (OeBuilding(building) is not var (sid, level) || BuildingOf(c, sid) is not { } b || BuiltLevel(b) < level) return AdapterResult.Ok;
        return AdapterResult.Unsupported("CA:B2 — Olden Era has no demolition of buildings");
    });

    /// <summary>The map logic of a city (fmr), from the object logics by map object id.</summary>
    object? CityLogicObject(object city)
    {
        var all = sym.Read("world.objectLogics", null);
        int id = Convert.ToInt32(sym.Read("object.mapId", city));
        if (all == null || !Convert.ToBoolean(OldenEraSymbols.Call(all, "ContainsKey", id))) return null;
        return OldenEraSymbols.Call(all, "get_Item", id) is { } l ? MethodTrace.Real(l) : null;
    }

    /// <summary>The buildings logic of a city (egz).</summary>
    object? CityLogic(object city) => CityLogicObject(city) is { } l ? sym.Read("citylogic.buildings", l) : null;

    public AdapterResult<int> GetBuiltThisTurn(int town) => FromCity(town, c =>
        sym.Has("town.builtToday") ? AdapterResult<int>.Ok(Convert.ToInt32(sym.Read("town.builtToday", c)) > 0 ? 1 : 0) : Missing<int>("town.builtToday"));

    public AdapterResult SetBuiltThisTurn(int town, int value) => ChangeCity(town, c =>
    {
        if (!sym.Has("town.builtToday")) return Missing("town.builtToday");
        sym.Write("town.builtToday", c, Math.Max(0, value));
        return AdapterResult.Ok;
    });

    // Dwellings: Build_Tier_<level+1> (level 2 = upgraded) hires from one HiredUnitSet whose currentAmount serves both
    // its basic and upgraded creatures; WoG's row of the building that stands is that number.
    AdapterResult<(object Set, bool Upgraded)> Dwelling(object city, int level)
    {
        if (!sym.Has("town.hires") || !sym.Has("hire.sets") || !sym.Has("unitset.amount")) return Missing<(object, bool)>("town.hires");
        string sid = $"Build_Tier_{level + 1}";
        foreach (var h in OldenEraSymbols.Items(sym.Read("town.hires", city)))
        {
            if (h == null || sym.Read("building.sid", h) as string != sid) continue;
            var set = OldenEraSymbols.Items(sym.Read("hire.sets", h)).FirstOrDefault(x => x != null);
            return set == null ? AdapterResult<(object, bool)>.Failed("the dwelling has no creatures")
                : AdapterResult<(object, bool)>.Ok((set, BuiltLevel(h) >= 2));
        }
        return AdapterResult<(object, bool)>.Failed($"the town has no dwelling of level {level}");
    }

    public AdapterResult<int> GetAvailable(int town, int level, int row) => FromCity(town, c =>
    {
        var d = Dwelling(c, level);
        return d.IsOk ? AdapterResult<int>.Ok(Convert.ToInt32(sym.Read("unitset.amount", d.Value.Set))) : d.Error<int>();
    });

    public AdapterResult SetAvailable(int town, int level, int row, int count) => ChangeCity(town, c =>
    {
        var d = Dwelling(c, level);
        if (!d.IsOk) return d.AsPlain();
        if ((row == 1) == d.Value.Upgraded) sym.Write("unitset.amount", d.Value.Set, count);
        return AdapterResult.Ok;
    });

    public AdapterResult<int> GetGrowth(int town, int level) => FromCity(town, c =>
    {
        if (!sym.Has("unitset.growth")) return Missing<int>("unitset.growth");
        var d = Dwelling(c, level);
        return d.IsOk ? AdapterResult<int>.Ok(Convert.ToInt32(sym.Read("unitset.growth", d.Value.Set))) : d.Error<int>();
    });

    public AdapterResult<WoGStack> GetGuard(int town, int slot) => FromCity(town, c =>
        sym.Has("town.garrison") && StackKeys.All(sym.Has) ? ReadStack(sym.Read("town.garrison", c), slot) : Missing<WoGStack>("town.garrison"));

    public AdapterResult SetGuard(int town, int slot, int type, int count) => ChangeCity(town, c =>
    {
        if (!sym.Has("town.garrison") || !StackKeys.All(sym.Has)) return Missing("town.garrison");
        var list = sym.Read("town.garrison", c);
        return list == null ? AdapterResult.Failed("the town has no garrison") : WriteStack(list, slot, type, count);
    });

    // The city logic's daily income by resource (fmr.bmcz -> {gold 1250, gemstones 1, dust 5}) [V-game]; WoG's S is the gold.
    public AdapterResult<int> GetIncome(int town) => FromCity(town, c =>
    {
        if (!sym.Has("world.objectLogics") || !sym.Has("citylogic.income")) return Missing<int>("citylogic.income");
        if (CityLogicObject(c) is not { } logic) return AdapterResult<int>.Failed("the town has no map logic");
        var income = OldenEraSymbols.Call(logic, sym.MemberOf("citylogic.income")!.Name);
        var gold = income == null ? null : OldenEraSymbols.Pairs(income)?.FirstOrDefault(x => x.Key as string == "gold").Value;
        return AdapterResult<int>.Ok(gold == null ? 0 : Convert.ToInt32(gold));
    });

    // ---- UI ---------------------------------------------------------------------------------

    public Func<string, bool>? ShowMessageImpl { get; set; }
    public Func<string, bool?>? AskImpl { get; set; }

    public AdapterResult ShowMessage(string text) =>
        ShowMessageImpl != null && ShowMessageImpl(text) ? AdapterResult.Ok : Missing("ui.message");

    public AdapterResult<bool> AskYesNo(string text)
    {
        var r = AskImpl?.Invoke(text);
        return r.HasValue ? AdapterResult<bool>.Ok(r.Value) : Missing<bool>("ui.question");
    }

    // ---- battle -----------------------------------------------------------------------------

    /// <summary>The battle the event bus reported starting; null outside battles.</summary>
    public WoGBattle? CurrentBattle { get; private set; }

    public bool InBattle => CurrentBattle != null;

    public AdapterResult<WoGBattle> GetBattle() =>
        CurrentBattle != null ? AdapterResult<WoGBattle>.Ok(CurrentBattle) : AdapterResult<WoGBattle>.Failed("not in battle");

    /// <summary>
    /// Records a starting battle: the attacker is the active hero of the battle side's player (the player whose turn it
    /// is when the side is unknown); the defender is the monster squad on a square next to the attacker, neutral and
    /// without a hero. Hero-vs-hero and town battles are not told apart yet [UNVERIFIED].
    /// </summary>
    public WoGBattle BeginBattle(int side, bool quick)
    {
        var (player, hero) = BattleParticipant(side);
        var b = new WoGBattle { Quick = quick, Owners = new[] { player, -1 }, Heroes = new[] { hero, -1 } };
        var at = hero >= 0 && GetPosition(hero) is { Status: AdapterStatus.Ok } p ? p.Value : MapPos.None;
        b.Position = at;
        if (!at.IsNone && GetObjects() is { Status: AdapterStatus.Ok } objs)
        {
            var squad = objs.Value.FirstOrDefault(o => o.Type == 54 && o.Position.L == at.L
                && Math.Abs(o.Position.X - at.X) <= 1 && Math.Abs(o.Position.Y - at.Y) <= 1);
            if (squad != null) b.Position = squad.Position;
        }
        CurrentBattle = b;
        return b;
    }

    public void EndBattle() => CurrentBattle = null;

    public AdapterResult<int> GetHero(int side) =>
        CurrentBattle != null && side is 0 or 1 ? AdapterResult<int>.Ok(CurrentBattle.Heroes[side]) : AdapterResult<int>.Failed("not in battle");
    // ---- battle stacks ----------------------------------------------------------------------
    // The battle being fought is the battle controller's logic (static elb.ckia → .ckic: eor); its field objects
    // (eor.clgt.ckzg) are the units of both sides in the order they were placed, attacker slots first; eor.clhe.current
    // is the unit whose turn it is [V-game]. A unit: sid (FieldObject.ckyo), side (ckys.clxl: 0 attacker, 1 defender),
    // the army stack it came from (cmwm: stacks = count at the start, slotPos), its battle data (cmwp: ctxq = count,
    // fullStacks = creatures besides the top one, cnbx = hit points of the top one), its totals (stats) and its battle
    // modifier (cmws), which every recalculation of the totals adds [V-game: griffins' modifier +11 attack → the total
    // 9 → 20 after their next action; writing the total alone was recalculated back to 9].
    // WoG numbers the stacks of a side 0..20 (the defender's from 21) in army-slot order; a number stays with its unit
    // for the whole battle.
    static readonly string[] BattleKeys = { "battle.logic", "battle.objects", "bunit.class", "bunit.sid", "bunit.side", "bunit.data", "bunit.army", "bunit.stats", "bunit.mods" };
    readonly Dictionary<IntPtr, int> stackNumbers = new();
    IntPtr numberedBattle;

    /// <summary>The units of the battle being fought by WoG stack number.</summary>
    AdapterResult<Dictionary<int, object>> BattleUnits()
    {
        if (BattleKeys.FirstOrDefault(k => !sym.Has(k)) is { } missing) return Missing<Dictionary<int, object>>(missing);
        if (sym.Read("battle.logic", null) is not Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase logic)
            return AdapterResult<Dictionary<int, object>>.Failed("not in battle");
        if (logic.Pointer != numberedBattle) { stackNumbers.Clear(); numberedBattle = logic.Pointer; }
        var cls = sym.TypeOf("bunit.class");
        var units = OldenEraSymbols.Items(sym.Read("battle.objects", logic)).Where(o => o != null).Select(o => MethodTrace.Real(o!))
            .Where(cls.IsInstanceOfType).Cast<Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase>().ToList();
        // new units get the next numbers of their side, in army-slot order (summoned ones, slot -1, last)
        foreach (var u in units.Where(u => !stackNumbers.ContainsKey(u.Pointer))
                     .OrderBy(u => Convert.ToInt32(sym.Read("bunit.side", u)))
                     .ThenBy(u => Convert.ToInt32(OldenEraSymbols.ReadMember(sym.Read("bunit.army", u)!, "slotPos")) is var s && s >= 0 ? s : 99))
        {
            int side = Convert.ToInt32(sym.Read("bunit.side", u)) == 0 ? 0 : 1;
            int n = side * 21;
            while (stackNumbers.ContainsValue(n) && n < side * 21 + 20) n++;
            stackNumbers[u.Pointer] = n;
        }
        var byNumber = new Dictionary<int, object>();
        foreach (var u in units) byNumber[stackNumbers[u.Pointer]] = u;
        return AdapterResult<Dictionary<int, object>>.Ok(byNumber);
    }

    public AdapterResult<int> StackCount()
    {
        var units = BattleUnits();
        return units.IsOk ? AdapterResult<int>.Ok(units.Value.Count) : units.Error<int>();
    }

    public AdapterResult<int> CurrentStack()
    {
        var units = BattleUnits();
        if (!units.IsOk) return units.Error<int>();
        if (!sym.Has("battle.current")) return Missing<int>("battle.current");
        if (sym.Read("battle.current", sym.Read("battle.logic", null)) is not Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase cur)
            return AdapterResult<int>.Ok(-1);
        return AdapterResult<int>.Ok(stackNumbers.TryGetValue(cur.Pointer, out int n) ? n : -1);
    }

    // BattleStackStat → the field of the unit's totals and modifier (UnitStat)
    static string? UnitStatName(BattleStackStat stat) => stat switch
    {
        BattleStackStat.Attack => "offence", BattleStackStat.Defence => "defence", BattleStackStat.HitPoints => "hp",
        BattleStackStat.Speed => "speed", BattleStackStat.DamageLow => "damageMin", BattleStackStat.DamageHigh => "damageMax",
        _ => null,
    };

    const string StackKept = "this stat of an Olden Era battle unit is not mapped (or cannot be changed)";

    public AdapterResult<int> GetStack(int stackIndex, BattleStackStat stat)
    {
        var units = BattleUnits();
        if (!units.IsOk) return units.Error<int>();
        if (!units.Value.TryGetValue(stackIndex, out var u)) return AdapterResult<int>.Failed($"no battle stack {stackIndex}");
        var data = sym.Read("bunit.data", u)!;
        var stats = sym.Read("bunit.stats", u)!;
        int Total(string name) => Convert.ToInt32(OldenEraSymbols.ReadMember(stats, name));
        switch (stat)
        {
            case BattleStackStat.Type:
            {
                string sid = sym.Read("bunit.sid", u) as string ?? "";
                return currentIds().TryGetWoG("creature", sid, out int type) ? AdapterResult<int>.Ok(type)
                    : AdapterResult<int>.Unsupported($"Olden Era unit '{sid}' has no WoG creature mapping");
            }
            case BattleStackStat.Count: return AdapterResult<int>.Ok(Convert.ToInt32(OldenEraSymbols.ReadMember(data, "ctxq")));
            case BattleStackStat.CountAtStart: return AdapterResult<int>.Ok(Convert.ToInt32(OldenEraSymbols.ReadMember(sym.Read("bunit.army", u)!, "stacks")));
            case BattleStackStat.HitPointsLost: return AdapterResult<int>.Ok(Total("hp") - Convert.ToInt32(OldenEraSymbols.ReadMember(data, "cnbx")));
            case BattleStackStat.Side: return AdapterResult<int>.Ok(Convert.ToInt32(sym.Read("bunit.side", u)) == 0 ? 0 : 1);
            case BattleStackStat.ArmySlot: return AdapterResult<int>.Ok(Convert.ToInt32(OldenEraSymbols.ReadMember(sym.Read("bunit.army", u)!, "slotPos")));
        }
        return UnitStatName(stat) is { } field ? AdapterResult<int>.Ok(Total(field)) : AdapterResult<int>.Unsupported(StackKept);
    }

    public AdapterResult SetStack(int stackIndex, BattleStackStat stat, int value)
    {
        var units = BattleUnits();
        if (!units.IsOk) return units.AsPlain();
        if (!units.Value.TryGetValue(stackIndex, out var u)) return AdapterResult.Failed($"no battle stack {stackIndex}");
        var data = sym.Read("bunit.data", u)!;
        var stats = sym.Read("bunit.stats", u)!;
        switch (stat)
        {
            case BattleStackStat.Count:
                // the creatures besides the top one [V-game: fullStacks 6 → 19 showed 20 griffins on the field and in
                // the turn queue]; removing a stack is not mapped
                if (value < 1) return AdapterResult.Unsupported("removing a battle stack (BM:N0) is not mapped yet");
                OldenEraSymbols.WriteMember(data, "fullStacks", value - 1);
                return AdapterResult.Ok;
            case BattleStackStat.HitPointsLost:
            {
                int hp = Convert.ToInt32(OldenEraSymbols.ReadMember(stats, "hp"));
                OldenEraSymbols.WriteMember(data, "cnbx", Math.Clamp(hp - value, 1, Math.Max(1, hp)));
                return AdapterResult.Ok;
            }
        }
        if (UnitStatName(stat) is not { } field) return AdapterResult.Unsupported(StackKept);
        // the battle modifier keeps the change through the game's recalculations; the total shows it at once
        var mods = sym.Read("bunit.mods", u)!;
        int delta = value - Convert.ToInt32(OldenEraSymbols.ReadMember(stats, field));
        OldenEraSymbols.WriteMember(mods, field, Convert.ToInt32(OldenEraSymbols.ReadMember(mods, field)) + delta);
        OldenEraSymbols.WriteMember(stats, field, value);
        return AdapterResult.Ok;
    }
    public AdapterResult ApplyBuff(int stackIndex, string buffId) => Missing("buff.apply");
    public AdapterResult<int> SummonUnit(int side, string unitKey, int count) => Missing<int>("battle.summon");

    // ---- clock ------------------------------------------------------------------------------

    int ClockValue(string key, Func<int> fallback) =>
        sym.Has(key) && Root() is { } root && sym.Read(key, root) is { } v ? Convert.ToInt32(v) : fallback();

    public int AbsoluteDay => ClockValue("game.day", () => 1);
    public int DayOfWeek => ClockValue("game.dayOfWeek", () => (AbsoluteDay - 1) % 7 + 1);
    public int Week => ClockValue("game.week", () => (AbsoluteDay - 1) / 7 % 4 + 1);
    public int Month => ClockValue("game.month", () => (AbsoluteDay - 1) / 28 + 1);
}
