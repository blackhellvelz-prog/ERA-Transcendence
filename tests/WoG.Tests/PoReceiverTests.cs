using System.IO;
using Xunit;

namespace WoG.Tests;

/// <summary>!!PO — WoG's own data of map squares (erm.cpp ERM_Position: _Square_ bit fields and _Square2_).</summary>
public class PoReceiverTests
{
    static TestHost Map(string body)
    {
        var t = new TestHost().Load(body);
        t.Game.MapSize = 10;
        t.Game.MapLevels = 1; // with an underground: 10 x 10 x 2 squares
        return t.Start();
    }

    [Fact]
    public void Start_values_and_bit_fields_keep_only_their_bits()
    {
        var t = Map(
            "!?FU1;\n" +
            "!!PO1/2/0:H?v1 O?v2 N?v3 T?v4 S?v5;\n" +
            "!!PO1/2/0:H300 O9 N17 T256 S511;\n" +   // 300 & 255, 9 as a signed 4-bit value, 17 & 15, ...
            "!!PO1/2/0:H?v6 O?v7 N?v8 T?v9 S?v10;\n" +
            "!!PO1/2/0:Hd5 H?v11;\n");
        t.Call(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(new[] { 255, -1, 0, 0, 0 }, new[] { t.V(1), t.V(2), t.V(3), t.V(4), t.V(5) });
        Assert.Equal(new[] { 44, -7, 1, 0, 255 }, new[] { t.V(6), t.V(7), t.V(8), t.V(9), t.V(10) });
        Assert.Equal(49, t.V(11));
    }

    [Fact]
    public void V_and_B_hold_shorts_and_longs()
    {
        var t = Map(
            "!?FU1;\n" +
            "!!VRv5:S1; !!VRv6:S1; !!VRv7:S1;\n" +   // the square (1, 1, 1) by v5
            "!!PO5:V3/40000 V3/?v1 B1/-123456789 B1/?v2 V0/?v3;\n");
        t.Call(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(-25536, t.V(1)); // 40000 as a short
        Assert.Equal(-123456789, t.V(2));
        Assert.Equal(0, t.V(3));
    }

    [Fact]
    public void C_counts_matching_squares_into_v1_including_untouched_ones()
    {
        var t = Map(
            "!?FU1;\n" +
            "!!PO0/0/0:T5 N3;\n!!PO1/0/1:T5;\n!!PO2/0/0:T6;\n" +
            "!!PO0/0/0:C5/-1/-1/-1/-1; !!VRv2:Sv1;\n" +
            "!!PO0/0/0:C5/-1/-1/-1/3; !!VRv3:Sv1;\n" +
            "!!PO0/0/0:C-1/-1/255/-1/-1; !!VRv4:Sv1;\n" +   // every square has the start hero 255
            "!!PO0/0/0:C0/-1/-1/-1/-1; !!VRv5:Sv1;\n");
        t.Call(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(new[] { 2, 1, 200, 197 }, new[] { t.V(2), t.V(3), t.V(4), t.V(5) });
    }

    [Fact]
    public void Wrong_positions_and_indexes_are_errors()
    {
        var t = Map(
            "!?FU1;\n!!PO10/0/0:N1;\n" +
            "!?FU2;\n!!PO0/0/2:N1;\n" +
            "!?FU3;\n!!PO0/0/0:V4/1;\n" +
            "!?FU4;\n!!PO0/0/0:B?v1/1;\n");
        for (int f = 1; f <= 4; f++)
        {
            t.Call(f);
            Assert.Equal(f, t.ErrorCount);
        }
    }

    [Fact]
    public void Square_data_is_saved_and_a_new_game_starts_clean()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wog-po-" + Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try
        {
            var t = Map("!?FU1;\n!!PO3/4/0:T9 B0/77;\n!?FU2;\n!!PO3/4/0:T?v1 B0/?v2;\n");
            t.Call(1);
            var save = Path.Combine(dir, "s.wogsave");
            t.Host.SaveTo(save, "slot");
            t.Host.StartNewGame();
            t.Call(2);
            Assert.Equal((0, 0), (t.V(1), t.V(2)));
            t.Host.LoadFrom(save, "slot");
            t.Call(2);
            Assert.Equal((9, 77), (t.V(1), t.V(2)));
        }
        finally { Directory.Delete(dir, true); }
    }
}
