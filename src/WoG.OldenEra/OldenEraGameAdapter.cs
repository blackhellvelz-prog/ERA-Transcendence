using System;
using System.Collections;
using System.Collections.Generic;
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

    /// <summary>Finds the OE hero object whose WoG id (via IdMap "hero") matches.</summary>
    object? FindHero(int hero)
    {
        if (!sym.Has("hero.list") || !sym.Has("hero.id")) return null;
        if (!currentIds().TryGetEngine("hero", hero, out var sid)) return null;
        foreach (var h in OldenEraSymbols.Items(sym.Read("hero.list", Root())))
            if (h != null && Equals(sym.Read("hero.id", h)?.ToString(), sid)) return h;
        return null;
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
        if (h == null) return AdapterResult<int>.Failed($"hero {hero} not found (or not in IdMap)");
        return AdapterResult<int>.Ok(Convert.ToInt32(sym.Read(key, h)));
    }

    public AdapterResult Set(int hero, HeroStat stat, int value)
    {
        var key = StatKey(stat);
        if (key == null) return AdapterResult.Unsupported($"hero stat {stat} has no Olden Era equivalent");
        if (!sym.Has(key)) return Missing(key);
        var h = FindHero(hero);
        if (h == null) return AdapterResult.Failed($"hero {hero} not found (or not in IdMap)");
        sym.Write(key, h, value);
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
        if (!sym.Has("hero.army") || !sym.Has("stack.unitSid") || !sym.Has("stack.count")) return Missing<WoGStack>("hero.army");
        var h = FindHero(hero);
        if (h == null) return AdapterResult<WoGStack>.Failed($"hero {hero} not found");
        var army = OldenEraSymbols.Items(sym.Read("hero.army", h));
        if (slot >= army.Count) return AdapterResult<WoGStack>.Ok(new WoGStack());
        var st = army[slot];
        if (st == null) return AdapterResult<WoGStack>.Ok(new WoGStack());
        string sid = sym.Read("stack.unitSid", st)?.ToString() ?? "";
        int count = Convert.ToInt32(sym.Read("stack.count", st));
        if (!currentIds().TryGetWoG("creature", sid, out int type))
            return AdapterResult<WoGStack>.Unsupported($"Olden Era unit '{sid}' has no WoG creature mapping");
        return AdapterResult<WoGStack>.Ok(new WoGStack { Type = type, Count = count });
    }

    public AdapterResult SetStack(int hero, int slot, int type, int count) => Missing("stack.create");
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
        sym.Write("resource.value", r.Value, value);
        return AdapterResult.Ok;
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
    public AdapterResult<int> GetActiveHero(int player) => Missing<int>("player.activeHero");
    public AdapterResult<IReadOnlyList<int>> GetHeroes(int player) => Missing<IReadOnlyList<int>>("player.heroes");

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
