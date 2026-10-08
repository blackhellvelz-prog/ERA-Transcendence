using WoG.Core.Model;
using Xunit;

namespace WoG.Tests;

/// <summary>!!OW — the player commands H O N T V W (erm.cpp ERM_Owner).</summary>
public class OwReceiverTests
{
    static TestHost Game(string body)
    {
        var t = new TestHost().Load(body);
        t.Game.AddHero(7, owner: 1);
        t.Game.AddHero(3, owner: 1);
        t.Game.AddHero(5, owner: 0);
        t.Game.AddTown(0, 0, new MapPos(1, 1, 0), owner: 1);
        t.Game.AddTown(1, 1, new MapPos(5, 1, 0), owner: 0);
        t.Game.AddTown(2, 2, new MapPos(9, 1, 0), owner: 1);
        t.Game.Tavern[1] = (20, 21);
        return t.Start();
    }

    [Fact]
    public void H_stores_the_players_heroes_by_number()
    {
        var t = Game("!?FU1;\n!!OW:H1/100;\n!!OW:H1/200/0;\n!!OW:H1/201/2;\n!!VRv202:S-5;\n!!OW:H1/202/3;\n");
        t.Call(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(new[] { 2, 3, 7 }, new[] { t.V(100), t.V(101), t.V(102) });
        Assert.Equal(2, t.V(200));
        Assert.Equal(7, t.V(201));
        Assert.Equal(-5, t.V(202)); // no third hero: v unchanged
    }

    [Fact]
    public void O_reads_the_hero_list_and_W_N_the_towns()
    {
        var t = Game("!?FU1;\n!!OW:O1/1/?v1 O1/5/?v2;\n!!OW:O1/?v3/?v4/?v5/?v6/?v7/?v8/?v9/?v10/?v11;\n" +
                     "!!OW:W1/?v12 W1/1/?v13 W1/2/?v14;\n!!OW:N1/0/?v15;\n");
        t.Call(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(new[] { 3, -1, 2, 7, 3, -1 }, new[] { t.V(1), t.V(2), t.V(3), t.V(4), t.V(5), t.V(6) });
        Assert.Equal(new[] { 2, 2, -1, 0 }, new[] { t.V(12), t.V(13), t.V(14), t.V(15) });
    }

    [Fact]
    public void T_reads_the_team_and_V_the_tavern()
    {
        var t = Game("!?FU1;\n!!OW:T1/?v1 V1/?v2/?v3;\n!!OW:V1/30/d;\n!!OW:V1/?v4/?v5;\n");
        t.Game.Teams[1] = 0;
        t.Call(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(new[] { 0, 20, 21, 30, 21 }, new[] { t.V(1), t.V(2), t.V(3), t.V(4), t.V(5) });
    }

    [Theory]
    [InlineData("!!OW:H8/1;")]   // owner 0..7
    [InlineData("!!OW:H1/0;")]   // v index 1..10000
    [InlineData("!!OW:W1/48/?v1;")]
    [InlineData("!!OW:T1;")]     // exactly 2 parameters
    public void Wrong_parameters_are_errors(string line)
    {
        var t = Game("!?FU1;\n" + line + "\n");
        t.Call(1);
        Assert.Equal(1, t.ErrorCount);
    }
}
