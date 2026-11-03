using System.Linq;
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

    /// <summary>
    /// Array_Fill and Array_Revert (stdlib.erm) pass the array's address to Era's Erm_FillInt32Array /
    /// Erm_RevertInt32Array; natively: the same defaults, clamping and steps.
    /// </summary>
    [Fact]
    public void Array_fill_and_revert_work_on_the_array_without_its_address()
    {
        using var t = new EraTestHost().Script("t.erm", "ZVSE2\n" +
            "!?FU(Array_Fill);\n!#VA(list:x);\n!!SN:M(list)/?(n:y)/d/d/?(addr:y);\n" + // the ERM body needs the address
            "!?FU(Go);\n" +
            "!!SN:M-1/6/0/1/?v1;\n" +
            "!!FU(Array_Fill):Pv1/100/2;\n" +                 // 100 102 104 106 108 110
            "!!SN:Vv1/0/?v2/?v3/?v4/?v5/?v6/?v7;\n" +
            "!!FU(Array_Fill):Pv1/7/0/4/99;\n" +              // items 4..5 (the end clamped) = 7
            "!!FU(Array_Revert):Pv1/-3/3;\n" +                 // items 0..3 reversed (the start clamped)
            "!!SN:Vv1/0/?v8/?v9/?v10/?v11/?v12/?v13;\n" +
            "!!FU(Array_Revert):Pv1/5/5;\n" +                  // a single item: nothing
            "!!SN:M-1/0/0/1/?v14;\n" +
            "!!FU(Array_Fill):Pv14/1/1;\n" +                   // an empty array: nothing
            "!!FU(Array_Fill):Pv1/2147483647/1/0/1;\n" +       // Delphi integers wrap around
            "!!SN:Vv1/0/?v15/?v16;\n").Start();
        t.Call("Go");
        Assert.Equal("", t.ErrorText);
        Assert.Empty(t.Host.Compat.Entries);
        Assert.Equal(new[] { 100, 102, 104, 106, 108, 110 }, Enumerable.Range(2, 6).Select(t.V));
        Assert.Equal(new[] { 106, 104, 102, 100, 7, 7 }, Enumerable.Range(8, 6).Select(t.V));
        Assert.Equal((int.MaxValue, int.MinValue), (t.V(15), t.V(16)));
    }

    /// <summary>
    /// Game Enhancement Mod reads H3's town structure by address and warns the player when it gets 0 ("the game may
    /// fall at any time"); natively: a virtual non-zero address, and the spell ban from UN:J0.
    /// </summary>
    [Fact]
    public void Gem_town_functions_do_not_warn_and_read_the_spell_ban()
    {
        using var t = new EraTestHost().Script("t.erm", "ZVSE2\n" +
            "!?FU(gem_Town_GetStruct);\n!#VA(townId:x) (buildId:x);\n!!UN:C6919480/4/?(game:y);\n" +
            "!!IF&(buildId)<43200000:M^{FU(gem_Town_GetStruct): Attention!}^;\n" +
            "!?FU(Go);\n" +
            "!!FU(gem_Town_GetStruct):P3/?v1;\n" +
            "!!UN:J0/17/1;\n" +
            "!!FU(Town_IsSpellBanned):P3/17/?v2;\n" +
            "!!FU(Town_IsSpellBanned):P3/18/?v3;\n").Start();
        t.Call("Go");
        Assert.Equal("", t.ErrorText);
        Assert.Empty(t.Host.Compat.Entries);
        Assert.Empty(t.Game.Messages);
        Assert.Equal(WoG.Erm.Era.EraNativeLibrary.VirtualTownStructs + 3 * 360, t.V(1));
        Assert.Equal((1, 0), (t.V(2), t.V(3)));
    }

    /// <summary>Era Erm Framework's artifact functions read H3's hero structure (HE:Z, UN:C, SN:E): here the adapter.</summary>
    [Fact]
    public void Era_artifact_functions_work_on_the_hero_through_the_adapter()
    {
        using var t = new EraTestHost().Script("t.erm", "ZVSE2\n!?FU(Go);\n" +
            "!!FU(GetArtAtSlot):P0/3/?v1/?v2;\n" +          // right hand: artifact 7
            "!!FU(GetArtAtSlot):P0/19/?v3/?v4;\n" +         // backpack: a scroll of spell 17
            "!!FU(GetArtAtSlot):P0/4/?v5/?v6;\n" +          // empty
            "!!FU(ChangeArtModAtSlot):P0/19/20;\n" +
            "!!FU(GetArtAtSlot):P0/19/?v7/?v8;\n" +
            "!!FU(UnequipArtFromSlot):P0/3;\n" +
            "!!FU(GetArtAtSlot):P0/3/?v9;\n" +
            "!!FU(EquipArtToSlot):P0/7/-1/4/?v10;\n" +      // 7 does not fit the left hand
            "!!FU(EquipArtToSlot):P0/7/-1/-1/?v11;\n" +     // the first slot it fits
            "!!FU(GetArtAtSlot):P0/3/?v12;\n" +
            "!!FU(AddArtToHero):P0/7/-1/?v13;\n" +          // the right hand is taken: the backpack
            "!!FU(GetArtAtSlot):P0/20/?v14;\n" +
            "!!FU(GetHeroPrimarySkillsWithoutArts):P0/?v15/?v16/?v17/?v18;\n").Start();
        var h = t.Game.AddHero(0, 0);
        h.Equipped[3] = 7;
        h.Backpack.Add(1001 + 17);
        h.Primary = new[] { 4, 3, 2, 1 };
        t.Host.State.ArtifactOverrides[7] = new WoG.Core.Model.WoGArtifact { Id = 7, Position = 4 }; // Format P2 4: right hand
        t.Game.ArtifactPosition = a => a == 7 ? 4 : 0;
        t.Call("Go");
        Assert.Equal("", t.ErrorText);
        Assert.Equal((7, -1, 1, 17, -1, -1), (t.V(1), t.V(2), t.V(3), t.V(4), t.V(5), t.V(6)));
        Assert.Equal((1, 20), (t.V(7), t.V(8)));
        Assert.Equal(-1, t.V(9));
        Assert.Equal((0, 1, 7), (t.V(10), t.V(11), t.V(12)));
        Assert.Equal((1, 7), (t.V(13), t.V(14)));
        Assert.Equal((4, 3, 2, 1), (t.V(15), t.V(16), t.V(17), t.V(18)));
    }

    [Fact]
    public void Era_hero_structure_address_is_reported_unsupported()
    {
        using var t = new EraTestHost().Script("t.erm", "ZVSE2\n!?FU(Go);\n!!HE0:Z?v1;\n").Start();
        t.Game.AddHero(0, 0);
        t.Call("Go");
        Assert.Equal("", t.ErrorText);
        Assert.Contains(t.Host.Compat.Entries, e => e.Item.Contains("HE:Z") || e.Reason.Contains("HE:Z"));
    }
}

/// <summary>WoG's mithril (OW:R resource 7) and Era's TR:T with fewer parameters.</summary>
public class EraResourceAndTerrainTests
{
    [Fact]
    public void Ow_r_keeps_WoG_mithril_per_player_and_saves_it()
    {
        using var t = new EraTestHost().Script("t.erm", "ZVSE2\n!?FU(Go);\n!!OW:R1/7/d5;\n!!OW:R1/7/?v1;\n!!OW:R0/7/?v2;\n").Start();
        t.Call("Go");
        Assert.Equal("", t.ErrorText);
        Assert.Equal((5, 0), (t.V(1), t.V(2)));
        var loaded = WoG.Core.Save.WoGSaveSerializer.Deserialize(WoG.Core.Save.WoGSaveSerializer.Serialize(t.Host.State, "id"), "id");
        Assert.Equal(5, loaded.Mithril[1]);
    }

    [Fact]
    public void H3_experience_table_continues_by_one_point_two()
    {
        var levels = new[] { 0, 1, 2, 12, 13, 14, 20 }.Select(WoG.Core.Adapters.HeroRecords.H3Experience).ToArray();
        Assert.Equal(new[] { 0, 0, 1000, 20600, 24320, 28784, 81961 }, levels);
        Assert.Equal(int.MaxValue, WoG.Core.Adapters.HeroRecords.H3Experience(200));
    }

    [Fact]
    public void Un_j1_keeps_the_level_limit_and_gives_the_experience_of_a_level()
    {
        using var t = new EraTestHost().Script("t.erm", "ZVSE2\n!?FU(Go);\n" +
            "!!UN:J1/?v1/?v2;\n" +                                // no limit: 0, its experience 0
            "!!FU(WOG_GetExpRequirementOfLevel):P2/?v3;\n" +
            "!!FU(WOG_GetExpRequirementOfLevel):P13/?v4;\n" +
            "!!FU(WOG_GetExpRequirementOfLevel):P0/?v5;\n" +
            "!!UN:J1/14/?v6;\n!!UN:J1/?v7/d;\n").Start();
        t.Call("Go");
        Assert.Equal("", t.ErrorText);
        Assert.Equal((0, 0, 1000, 24320, 0), (t.V(1), t.V(2), t.V(3), t.V(4), t.V(5)));
        Assert.Equal((28784, 14), (t.V(6), t.V(7)));
        Assert.Equal(14, t.Host.State.LevelLimit);
        Assert.Contains(t.Host.Compat.Entries, e => e.Reason.Contains("level limit"));   // not enforced yet
    }

    [Fact]
    public void Era_tr_t_takes_any_number_of_parameters()
    {
        using var t = new EraTestHost().Script("t.erm", "ZVSE2\n!?FU(Go);\n!!TR1/2/0:T?v1/?v2;\n!!TR1/2/0:T?v3;\n").Start();
        t.Game.Terrain[new WoG.Core.Model.MapPos(1, 2, 0).Pack()] = 3;
        t.Call("Go");
        Assert.Equal("", t.ErrorText);
        Assert.Equal((3, 0, 3), (t.V(1), t.V(2), t.V(3)));
    }
}
