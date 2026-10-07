using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        var key = StatKey(stat);
        if (key == null) return AdapterResult<int>.Unsupported($"hero stat {stat} has no Olden Era equivalent");
        if (!sym.Has(key)) return Missing<int>(key);
        var h = FindHero(hero);
        if (h == null) return AdapterResult<int>.Failed($"hero {hero} does not exist");
        int v = Convert.ToInt32(sym.Read(key, h)) + BaseStat(stat, h);
        return AdapterResult<int>.Ok(stat == HeroStat.Owner ? PlayerOfSide(v) : v);
    }

    /// <summary>
    /// The primary skill a hero type starts with (Olden Era keeps it in the type config; the hero's own block holds
    /// the growth by level). H3 primary skill = type base + growth; bonuses of items and skills (additionalStats)
    /// are not part of it, as artifacts are not part of H3's PSkill.
    /// </summary>
    int BaseStat(HeroStat stat, object hero)
    {
        string? name = stat switch
        {
            HeroStat.Attack => "offence", HeroStat.Defence => "defence",
            HeroStat.Power => "spellPower", HeroStat.Knowledge => "intelligence", _ => null,
        };
        if (name == null || !sym.Has("hero.statsBase")) return 0;
        var block = sym.Read("hero.statsBase", hero);
        return block == null ? 0 : Convert.ToInt32(OldenEraSymbols.ReadMember(block, name));
    }

    public AdapterResult Set(int hero, HeroStat stat, int value)
    {
        var key = StatKey(stat);
        if (key == null) return AdapterResult.Unsupported($"hero stat {stat} has no Olden Era equivalent");
        if (stat == HeroStat.Owner) return AdapterResult.Unsupported("changing a hero's owner is not mapped");
        if (!sym.Has(key)) return Missing(key);
        var h = FindHero(hero);
        if (h == null) return AdapterResult.Failed($"hero {hero} does not exist");
        sym.Write(key, h, value - BaseStat(stat, h));
        return AdapterResult.Ok;
    }

    public AdapterResult<int> GetBase(int hero, HeroStat stat) => Get(hero, stat);
    public AdapterResult<MapPos> GetPosition(int hero) => Missing<MapPos>("hero.position");
    public AdapterResult MoveTo(int hero, MapPos pos, bool withEffect) => Missing("hero.move");
    public AdapterResult<int> HeroAt(MapPos pos) => Missing<int>("hero.position");
    public AdapterResult<int> GetSecondarySkill(int hero, int skill) => Missing<int>("hero.skills");
    public AdapterResult SetSecondarySkill(int hero, int skill, int level) => Missing("hero.skills");
    public AdapterResult<bool> HasSpell(int hero, int spell) => Missing<bool>("hero.spells");
    public AdapterResult SetSpell(int hero, int spell, bool known) => Missing("hero.spells");

    public AdapterResult<WoGStack> GetStack(int hero, int slot)
    {
        if (!sym.Has("hero.army") || !sym.Has("stack.unitSid") || !sym.Has("stack.count") || !sym.Has("stack.slot"))
            return Missing<WoGStack>("hero.army");
        var h = FindHero(hero);
        if (h == null) return AdapterResult<WoGStack>.Failed($"hero {hero} does not exist");
        // OE keeps a list of units, each with its slot position; an empty slot has no unit.
        var st = OldenEraSymbols.Items(sym.Read("hero.army", h))
            .FirstOrDefault(u => u != null && Convert.ToInt32(sym.Read("stack.slot", u)) == slot);
        if (st == null) return AdapterResult<WoGStack>.Ok(new WoGStack());
        string sid = sym.Read("stack.unitSid", st)?.ToString() ?? "";
        int count = Convert.ToInt32(sym.Read("stack.count", st));
        if (!currentIds().TryGetWoG("creature", sid, out int type))
            return AdapterResult<WoGStack>.Unsupported($"Olden Era unit '{sid}' has no WoG creature mapping");
        return AdapterResult<WoGStack>.Ok(new WoGStack { Type = type, Count = count });
    }

    /// <summary>
    /// Writes an army slot. Same creature: the count changes in place; type -1 or count 0 removes the unit; another
    /// creature replaces the unit with a new one (symbol stack.create = the unit type, built with its constructor).
    /// </summary>
    public AdapterResult SetStack(int hero, int slot, int type, int count)
    {
        if (!sym.Has("hero.army") || !sym.Has("stack.unitSid") || !sym.Has("stack.count") || !sym.Has("stack.slot"))
            return Missing("hero.army");
        if (slot < 0 || slot >= WoGLimits.ArmySlots) return AdapterResult.Failed($"army slot {slot} out of range");
        var h = FindHero(hero);
        if (h == null) return AdapterResult.Failed($"hero {hero} does not exist");
        var list = sym.Read("hero.army", h);
        if (list == null) return AdapterResult.Failed($"hero {hero} has no army");
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
    public AdapterResult<int> CountArtifact(int hero, int artifact) => Missing<int>("hero.items");
    public AdapterResult AddArtifact(int hero, int artifact, int slot) => Missing("hero.items");
    public AdapterResult<int> RemoveArtifact(int hero, int artifact, int count) => Missing<int>("hero.items");
    public AdapterResult<string> GetName(int hero) => Missing<string>("hero.name");
    public AdapterResult SetName(int hero, string name) => Missing("hero.name");
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

    bool ICreatureTypeAdapter.Exists(int type) => currentIds().TryGetEngine("creature", type, out _);
    public AdapterResult<int> Get(int type, CreatureStat stat) => Missing<int>("unit.db");
    public AdapterResult Set(int type, CreatureStat stat, int value) => Missing("unit.db");
    public AdapterResult<int> GetCost(int type, int resource) => Missing<int>("unit.db");
    public AdapterResult SetCost(int type, int resource, int value) => Missing("unit.db");

    // ---- map / towns ------------------------------------------------------------------------

    public AdapterResult<(int type, int subtype)> GetObjectAt(MapPos pos) => Missing<(int, int)>("map.objects");
    public AdapterResult<int> GetObjectOwner(MapPos pos) => Missing<int>("map.objects");
    public AdapterResult SetObjectOwner(MapPos pos, int owner) => Missing("map.objects");
    public AdapterResult<int> TownAt(MapPos pos) => Missing<int>("town.list");
    public AdapterResult<int> GetMageGuildLevel(int town) => Missing<int>("town.buildings");
    public AdapterResult<bool> IsBuilt(int town, int building) => Missing<bool>("town.buildings");
    public AdapterResult SetBuilt(int town, int building, bool built) => Missing("town.buildings");

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

    public bool InBattle => false;
    public AdapterResult<int> GetHero(int side) => Missing<int>("battle.start");
    public AdapterResult<int> StackCount() => Missing<int>("battle.start");
    public AdapterResult<int> GetStack(int stackIndex, BattleStackStat stat) => Missing<int>("battle.stacks");
    public AdapterResult SetStack(int stackIndex, BattleStackStat stat, int value) => Missing("battle.stacks");
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
