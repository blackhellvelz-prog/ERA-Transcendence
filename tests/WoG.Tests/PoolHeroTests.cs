using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WoG.Core.Adapters;
using WoG.Core.H3Data;
using WoG.Core.Model;
using WoG.Core.Save;
using WoG.Core.Visual;
using WoG.Erm.Runtime;
using WoG.Headless;
using WoG.Host;
using Xunit;

namespace WoG.Tests;

/// <summary>
/// An engine with fewer heroes than H3's 156 numbers (Olden Era): the other numbers are H3 pool heroes kept in the WoG
/// state (<see cref="PoolHeroAdapter"/>).
/// </summary>
public class PoolHeroTests
{
    /// <summary>The headless engine with its heroes limited to the ones added, and the pool over the host's state.</summary>
    sealed class FewHeroesGame : IGameAdapter
    {
        public readonly HeadlessGame Inner = new();
        public readonly HeroRecords Engine = new() { AllNumbersExist = false };
        public PoolHeroAdapter? Pool;
        public string EngineName => "few heroes";
        public IHeroAdapter Heroes => Pool!;
        public IPlayerAdapter Players => Inner;
        public ICreatureTypeAdapter Creatures => Inner;
        public IMapAdapter Map => Inner;
        public ITownAdapter Towns => Inner;
        public IUiAdapter Ui => Inner;
        public IBattleAdapter Battle => Inner;
        public IGameClock Clock => Inner;
    }

    static (FewHeroesGame Game, WoGHost Host) Make()
    {
        var game = new FewHeroesGame();
        WoGHost? host = null;
        game.Pool = new PoolHeroAdapter(game.Engine, new HeroRecords(() => host!.State.PoolHeroes)
        {
            PoolName = n => n == 21 ? "Сэр Кристиан" : null,
        });
        host = new WoGHost(game, new VisualResolver(new NoAssets()), null, new ErmRuntimeOptions());
        game.Engine.AddHero(0, 0, name: "Engine0").Position = new MapPos(1, 1, 0);
        game.Engine.AddHero(5, 1, name: "Engine5").Position = new MapPos(2, 2, 0);
        return (game, host);
    }

    [Fact]
    public void A_number_without_an_engine_hero_is_a_pool_hero()
    {
        var (game, host) = Make();
        var heroes = game.Heroes;
        Assert.True(heroes.Exists(21));
        Assert.False(heroes.Exists(156));
        Assert.Equal(-1, heroes.Get(21, HeroStat.Owner).Value);
        Assert.True(heroes.GetPosition(21).Value.IsNone);
        Assert.Equal("Сэр Кристиан", heroes.GetName(21).Value);
        Assert.Equal("Hero22", heroes.GetName(22).Value);
        Assert.Equal("Engine5", heroes.GetName(5).Value); // an engine hero is the engine's
        Assert.True(heroes.SetSecondarySkill(21, 3, 2).IsOk);
        Assert.Equal(2, host.State.PoolHeroes[21].SecondarySkills[3]); // kept in the WoG state
        Assert.False(host.State.PoolHeroes.ContainsKey(5));
        Assert.False(heroes.HeroAt(MapPos.None).IsOk); // pool heroes are not on the map
    }

    [Fact]
    public void Scripts_walk_over_all_156_heroes()
    {
        var (_, host) = Make();
        host.AddScript("t.erm", "ZVSE\n!?PI;\n!!DO1/0/155/1:P;\n!?FU1;\n!!HEx16:O?y1;\n!!VRv1&y1=-1:+1;\n!!VRv2&y1>-1:+1;\n");
        host.StartNewGame();
        Assert.DoesNotContain(host.Erm!.Diagnostics, d => d.Severity == WoG.Erm.Syntax.ErmSeverity.Error);
        Assert.Equal((154, 2), (host.State.Erm.V[0], host.State.Erm.V[1]));
    }

    [Fact]
    public void Pool_heroes_are_saved_with_the_WoG_state()
    {
        var (game, host) = Make();
        game.Heroes.SetSpell(30, 17, true);
        game.Heroes.SetName(30, "Renamed");
        var loaded = WoGSaveSerializer.Deserialize(WoGSaveSerializer.Serialize(host.State, "id"), "id");
        Assert.Contains(17, loaded.PoolHeroes[30].Spells);
        Assert.Equal("Renamed", loaded.PoolHeroes[30].Name);
        Assert.True(loaded.PoolHeroes[30].Position.IsNone);
    }

    [Fact]
    public void Hotraits_names_follow_the_two_header_rows()
    {
        const string text = "Hero Traits\r\nName\tLow1\tHigh1\tArmy1\r\nOrrin\t10\t20\tPikemen\r\nValeska\t10\t20\tPikemen\r\n";
        Assert.Equal(new[] { "Orrin", "Valeska" }, H3Tables.ParseHeroNames(text));
    }

    /// <summary>The user's ERA installation (ERA_GAME_DIR): a name for every H3 hero number.</summary>
    [Fact]
    public void The_installation_names_every_H3_hero()
    {
        string? dir = Environment.GetEnvironmentVariable("ERA_GAME_DIR");
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
        var mods = new[] { "WoG Rus", "WoG" }.Select(m => Path.Combine(dir, "Mods", m)).ToArray();
        var names = H3Tables.Load(new EraVfs(dir, mods)).HeroNames;
        Assert.True(names.Count >= 156, $"{names.Count} names: {string.Join(", ", names.Take(5))}");
        Assert.All(names.Take(156), n => Assert.False(string.IsNullOrWhiteSpace(n)));
    }
}
