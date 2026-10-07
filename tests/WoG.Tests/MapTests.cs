using System.IO;
using System.Linq;
using WoG.Core.Ids;
using WoG.Core.Model;
using Xunit;

namespace WoG.Tests;

/// <summary>Map size and the map object search (UN:X, UN:U) over the adapter's object list.</summary>
public class MapTests
{
    static TestHost WithObjects(string body)
    {
        var t = new TestHost().Load(body);
        t.Game.MapSize = 36;
        t.Game.MapLevels = 1;
        void Add(int x, int y, int l, int type, int sub) =>
            t.Game.Objects[new MapPos(x, y, l).Pack()] = new WoGMapObject { Position = new MapPos(x, y, l), Type = type, SubType = sub };
        Add(10, 5, 0, 53, 6);   // gold mine
        Add(3, 7, 0, 53, 0);    // sawmill
        Add(20, 5, 0, 53, 6);   // gold mine
        Add(1, 2, 1, 53, 6);    // gold mine underground
        Add(4, 4, 0, 98, 0);    // town
        return t.Start();
    }

    [Fact]
    public void Un_x_gets_the_map_size_and_refuses_to_set_it()
    {
        var t = WithObjects("!?FU1;\n!!UN:X?v1/?v2;\n!?FU2;\n!!UN:X5/?v3;\n");
        t.Call(1);
        Assert.Equal(36, t.V(1));
        Assert.Equal(1, t.V(2));
        Assert.Equal(0, t.ErrorCount);
        t.Call(2);
        Assert.Equal(1, t.ErrorCount);
    }

    [Fact]
    public void Un_u_counts_and_finds_objects_in_h3_scan_order()
    {
        var t = WithObjects(
            "!?FU1;\n" +
            "!!UN:U53/-1/?v1;\n" +          // all mines
            "!!UN:U53/6/?v2;\n" +           // gold mines
            "!!UN:U53/6/1/10;\n" +          // first: row 5 left to right, surface first
            "!!UN:U53/6/3/20;\n" +          // third: underground
            "!!UN:U53/-1/2/30;\n");         // second of all mines: (20,5) comes before (3,7)
        t.Call(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(4, t.V(1));
        Assert.Equal(3, t.V(2));
        Assert.Equal(new[] { 10, 5, 0 }, new[] { t.V(10), t.V(11), t.V(12) });
        Assert.Equal(new[] { 1, 2, 1 }, new[] { t.V(20), t.V(21), t.V(22) });
        Assert.Equal(new[] { 20, 5, 0 }, new[] { t.V(30), t.V(31), t.V(32) });
    }

    [Fact]
    public void Un_u_walks_forward_and_backward_from_the_last_found_object()
    {
        var t = WithObjects(
            "!?FU1;\n" +
            "!!VRv1:S-1;\n!!UN:U53/6/-1/1;\n!!VRv4:Sv1;\n" +       // first from the start: (10,5,0)
            "!!UN:U53/6/-1/1;\n!!VRv5:Sv1;\n" +                    // next: (20,5,0)
            "!!UN:U53/6/-1/1;\n!!VRv6:Sv1;\n!!VRv7:Sv3;\n" +       // next: (1,2,1)
            "!!UN:U53/6/-2/1;\n!!VRv8:Sv1;\n" +                    // previous: (20,5,0)
            "!!VRv1:S-2;\n!!UN:U53/6/-2/1;\n!!VRv9:Sv1;\n");       // last from the end: (1,2,1)
        t.Call(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal(10, t.V(4));
        Assert.Equal(20, t.V(5));
        Assert.Equal(1, t.V(6));
        Assert.Equal(1, t.V(7));
        Assert.Equal(20, t.V(8));
        Assert.Equal(1, t.V(9));
    }

    [Fact]
    public void Un_u_reports_wogs_errors()
    {
        var t = WithObjects(
            "!?FU1;\n!!UN:U53/6/5;\n" +          // setting the count
            "!?FU2;\n!!UN:U53/6/0/1;\n" +        // object number 0
            "!?FU3;\n!!UN:U53/6/9/1;\n" +        // no 9th gold mine
            "!?FU4;\n!!VRv1:S1;\n!!VRv2:S2;\n!!VRv3:S1;\n!!UN:U53/6/-1/1;\n" + // nothing after the last one
            "!?FU5;\n!!UN:U53/6/1/9999;\n");     // v index out of range
        for (int f = 1; f <= 5; f++)
        {
            int before = t.ErrorCount;
            t.Call(f);
            Assert.Equal(before + 1, t.ErrorCount);
        }
    }

    [Fact]
    public void Era_un_u_returns_coordinates_in_variables_and_minus_one_when_nothing_is_left()
    {
        using var t = new EraTestHost().Script("t.erm", "ZVSE2\n" +
            "!?FU(Go);\n" +
            "!!VR(x:y):S-1;\n" +
            "!!re i;\n" +
            "  !!UN:U53/6/-1/(x)/(y:y)/(z:y);\n" +       // gem_fixes.erm walks the monsters like this
            "  !!br&(x)<0;\n" +
            "  !!VRv1:+1;\n" +
            "  !!VRv2:+(x);\n" +
            "!!en;\n" +
            "!!UN:U53/6/7/(x)/(y)/(z);\n!!VRv3:S(x);\n" +  // no 7th gold mine: x = -1, no error
            "!!VRv10:S1;\n!!VRv11:S1;\n!!VRv12:S0;\n!!UN:U53/6/-1/10;\n" +
            "!?FU(Bad);\n!!UN:U53/6/1/0;\n").Start();
        t.Game.MapSize = 36;
        void Add(int x, int y, int l) =>
            t.Game.Objects[new MapPos(x, y, l).Pack()] = new WoGMapObject { Position = new MapPos(x, y, l), Type = 53, SubType = 6 };
        Add(10, 5, 0);
        Add(20, 5, 0);
        Add(1, 2, 1);
        t.Call("Go");
        Assert.Equal("", t.ErrorText);
        Assert.Equal(3, t.V(1));
        Assert.Equal(31, t.V(2));
        Assert.Equal(-1, t.V(3));
        Assert.Equal(new[] { 10, 5, 0 }, new[] { t.V(10), t.V(11), t.V(12) });
        t.Call("Bad");
        Assert.Contains("Invalid v-var index", t.ErrorText);
    }

    [Fact]
    public void Ob_and_tr_read_squares_like_h3_mapitems()
    {
        var t = WithObjects(
            "!?FU1;\n" +
            "!!OB10/5/0:T?v1;\n!!OB10/5/0:U?v2;\n" +            // the gold mine's entrance
            "!!TR10/5/0:E?v3;\n!!TR10/5/0:P?v4;\n" +
            "!!OB11/5/0:T?v5;\n!!TR11/5/0:E?v6;\n!!TR11/5/0:P?v7;\n" + // a blocked square of the mine
            "!!TR11/5/0:E?v8/?v9/?v10;\n" +                    // its entrance
            "!!TR0/0/0:T?v11/d/d/d/d/d/d/?v12;\n" +            // an empty grass square
            "!!OB0/0/0:T?v13;\n!!TR0/0/0:P?v14;\n" +
            "!!VRv100:S10;\n!!VRv101:S5;\n!!VRv102:S0;\n!!OB100:T?v15;\n" + // indirect position v100..v102
            "!?FU2;\n!!OB10/5/0:T54;\n");                       // changing a type: unsupported, not an error
        t.Game.Blocked[new MapPos(11, 5, 0).Pack()] = new MapPos(10, 5, 0);
        t.Game.Terrain[new MapPos(0, 0, 0).Pack()] = 2;
        t.Call(1);
        Assert.Equal(0, t.ErrorCount);
        Assert.Equal((53, 6), (t.V(1), t.V(2)));
        Assert.Equal((0, 0), (t.V(3), t.V(4)));            // yellow, not passable
        Assert.Equal(53, t.V(5));
        Assert.Equal((1, 0), (t.V(6), t.V(7)));            // red, not yellow
        Assert.Equal(new[] { 10, 5, 0 }, new[] { t.V(8), t.V(9), t.V(10) });
        Assert.Equal((2, 0), (t.V(11), t.V(12)));
        Assert.Equal((0, 1), (t.V(13), t.V(14)));
        Assert.Equal(53, t.V(15));
        t.Call(2);
        Assert.Equal(0, t.ErrorCount);
        Assert.Contains(t.Host.Compat.Entries, e => e.Item == "!!OB:T");
    }

    [Fact]
    public void Object_type_map_of_the_repository_maps_mines_towns_and_dwellings()
    {
        var path = Path.Combine(RepoRoot(), "Compatibility", "id-maps", "object.json");
        var map = ObjectTypeMap.Load(path);
        Assert.True(map.TryGet("mine_gold", out int t, out int s));
        Assert.Equal((53, 6), (t, s));
        Assert.True(map.TryGet("human_city", out t, out s));
        Assert.Equal((98, 0), (t, s));
        Assert.True(map.TryGet("barracks_human_1", out t, out s));
        Assert.Equal((17, 56), (t, s));                       // Guardhouse (Format CG)
        Assert.True(map.TryGet("custom_windmill", out t, out _));
        Assert.Equal(112, t);
        Assert.True(map.TryGet("alchemy_lab", out t, out _));
        Assert.True(t >= 1000);                               // Olden Era only: mercury etc. into dust
        Assert.False(map.TryGet("pinetree_1", out _, out _)); // scenery is not an ERM object
        Assert.Equal("mine_gold", map.FirstSid(53, 6));
    }

    static string RepoRoot()
    {
        var d = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (d != null && !File.Exists(Path.Combine(d.FullName, "WoGOldenEra.sln"))) d = d.Parent;
        return d!.FullName;
    }
}
