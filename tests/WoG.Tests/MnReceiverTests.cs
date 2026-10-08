using WoG.Core.Model;
using Xunit;

namespace WoG.Tests;

/// <summary>!!MN — mines (erm.cpp ERM_Mine).</summary>
public class MnReceiverTests
{
    static TestHost Map(string body)
    {
        var t = new TestHost().Load(body);
        t.Game.Objects[new MapPos(4, 4, 0).Pack()] = new WoGMapObject { Position = new MapPos(4, 4, 0), Type = 53, SubType = 6 };
        t.Game.Objects[new MapPos(8, 8, 0).Pack()] = new WoGMapObject { Position = new MapPos(8, 8, 0), Type = 5, SubType = 1 };
        t.Game.CurrentPlayer = 2;
        return t.Start();
    }

    [Fact]
    public void Owner_resource_and_guards()
    {
        var t = Map("!?FU1;\n!!MN4/4/0:O?v1 O-2 O?v2 O1 O?v3 R?v4;\n!!MN4/4/0:M7/13/40 M7/?v5/?v6 M0/?v7/?v8;\n");
        t.Call(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(new[] { -1, 2, 1, 6, 13, 40, -1, 0 }, new[] { t.V(1), t.V(2), t.V(3), t.V(4), t.V(5), t.V(6), t.V(7), t.V(8) });
    }

    [Theory]
    [InlineData("!!MN8/8/0:O?v1;")]   // not a mine
    [InlineData("!!MN4/4/0:O9;")]     // owner -1..7 (-2 current)
    [InlineData("!!MN4/4/0:R7;")]     // resource 0..6, 100
    [InlineData("!!MN4/4/0:M8/1/1;")] // slot 0..7
    public void Wrong_use_is_an_error(string line)
    {
        var t = Map("!?FU1;\n" + line + "\n");
        t.Call(1);
        Assert.Equal(1, t.ErrorCount);
    }

    [Fact]
    public void Changing_the_resource_is_reported()
    {
        var t = Map("!?FU1;\n!!MN4/4/0:R3;\n!!VRv1:S1;\n");
        t.Call(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Contains(t.Host.Compat.Entries, e => e.Item == "!!MN:R");
    }
}
