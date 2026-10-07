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

/// <summary>Library functions implemented natively in place of ERM bodies that read H3 memory.</summary>
public class EraNativeLibraryTests
{
    [Fact]
    public void Native_library_functions_replace_their_memory_reading_erm_bodies()
    {
        using var t = new EraTestHost().Script("t.erm", "ZVSE2\n" +
            "!?FU(GetMaxMonsterId);\n!#VA(result:x);\n!!UN:C4855383/4/?(result);\n" + // the ERM body would need H3 memory
            "!?FU(Go);\n" +
            "!!FU(GetMaxMonsterId):P?v1;\n" +
            "!!FU(GetMaxHeroId):P?v12;\n" +
            "!!FU(WOG_PackedCoords):P5/7/1/?v2;\n" +
            "!!FU(WOG_UnPackedCoords):P?v3/?v4/?v5/v2;\n" +
            "!!SN:M-1/4/0/1/?v6;\n!!SN:Vv6/0/10/20/10/30;\n" +
            "!!FU(Array_CountValue):Pv6/10/?v7;\n" +
            "!!FU(Array_IndexOf):Pv6/30/?v8;\n" +
            "!!FU(Array_Slice):Pv6/1/-1/?v9/1;\n" +
            "!!SN:Mv9/?v10;\n" +
            "!!SN:Vv9/0/?v13;\n" +
            "!!FU(Array_Merge):Pv6/v9;\n!!SN:Mv6/?v14;\n" +
            "!!FU(WOG_CheckRandomMap):P?v11;\n").Start();
        t.Game.RandomMap = true;
        t.Call("Go");
        Assert.Equal("", t.ErrorText);
        Assert.Empty(t.Host.Compat.Entries);
        Assert.Equal(196, t.V(1));                                  // 197 WoG 3.58 monsters without a table
        Assert.Equal(155, t.V(12));
        Assert.Equal(5 | 7 << 16 | 0x04000000, t.V(2));
        Assert.Equal(new[] { 5, 7, 1 }, new[] { t.V(3), t.V(4), t.V(5) });
        Assert.Equal((2, 3), (t.V(7), t.V(8)));
        Assert.Equal((2, 20), (t.V(10), t.V(13)));                   // items 1..2 (count -1 skips the last one)
        Assert.Equal(6, t.V(14));
        Assert.Equal(1, t.V(11));
    }
}
