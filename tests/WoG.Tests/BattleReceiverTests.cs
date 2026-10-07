using WoG.Core.Model;
using Xunit;

namespace WoG.Tests;

/// <summary>BA — the battle being set up, as the adapter reports it.</summary>
public class BattleReceiverTests
{
    [Fact]
    public void Ba_reads_heroes_owners_position_and_flags_and_refuses_changes()
    {
        var t = new TestHost().Load(
            "!?FU1;\n" +
            "!!BA:H0/?v1;\n!!BA:H1/?v2;\n!!BA:O?v3/?v4;\n!!BA:P?v5/?v6/?v7;\n" +
            "!!BA:Q?v8;\n!!BA:S?v9;\n!!BA:E?v10;\n!!BA:A?v11;\n" +
            "!?FU2;\n!!BA:Q0;\n" +            // a real change: unsupported, not an error
            "!?FU3;\n!!BA:H2/?v1;\n").Start();  // side out of range
        t.Game.CurrentBattle = new WoGBattle
        {
            Heroes = new[] { 5, -1 }, Owners = new[] { 0, -1 }, Position = new MapPos(10, 12, 0), Quick = true,
        };
        t.Call(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal((5, -2), (t.V(1), t.V(2)));            // no defending hero reads -2, as in WoG
        Assert.Equal((0, -1), (t.V(3), t.V(4)));
        Assert.Equal(new[] { 10, 12, 0 }, new[] { t.V(5), t.V(6), t.V(7) });
        Assert.Equal((1, 0, 0, 0), (t.V(8), t.V(9), t.V(10), t.V(11)));
        t.Call(2);
        Assert.Equal(0, t.ErrorCount);
        Assert.Contains(t.Host.Compat.Entries, e => e.Item == "!!BA:Q");
        t.Call(3);
        Assert.Equal(1, t.ErrorCount);
        t.Game.CurrentBattle = null;
        t.Call(1);                                          // outside a battle: an error
        Assert.True(t.ErrorCount > 1);
    }
}

/// <summary>Loading a game whose WoG state was not saved.</summary>
public class LoadWithoutStateTests
{
    [Fact]
    public void A_save_without_wog_state_gets_a_fresh_state_without_instructions_and_runs_gm0()
    {
        var t = new TestHost().Load("!#VRv1:S5;\n!?GM0;\n!!VRv2:S7;\n").Start();
        Assert.Equal(5, t.V(1));
        t.Host.LoadWithoutSavedState();
        Assert.Equal(0, t.V(1));   // a fresh state; the instructions belong to the start of a map
        Assert.Equal(7, t.V(2));   // !?GM0 ran as for any loaded game
        Assert.True(t.Host.State.InstructionsDone);
    }
}
