using Xunit;

namespace WoG.Tests;

/// <summary>Receiver commands Era changed (hooks in Erm.pas).</summary>
public class EraReceiverTests
{
    [Fact]
    public void Era_ow_c_gives_the_current_and_the_local_human_player_and_ignores_other_parameters()
    {
        using var t = new EraTestHost().Script("t.erm", "ZVSE2\n!?FU(Go);\n!!OW:Cd/?(cur:y);\n!!VRv1:S(cur);\n!!OW:C?v2/?v3;\n").Start();
        t.Game.CurrentPlayer = 2;
        t.Game.Local[0] = false;
        t.Game.Local[1] = true;
        t.Call("Go");
        Assert.Equal("", t.ErrorText);
        Assert.Equal(1, t.V(1));                 // the second parameter is the human player at this PC
        Assert.Equal((2, 1), (t.V(2), t.V(3)));
    }
}
