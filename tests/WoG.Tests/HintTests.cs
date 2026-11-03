using System.IO;
using System.Linq;
using WoG.Core.Model;
using WoG.Core.Save;
using WoG.Erm.Receivers;
using Xunit;

namespace WoG.Tests;

/// <summary>SN:H (AdvErm.SN_H), HT (erm.cpp ERM_HintType), OB:H/B and the hint the map shows (MapHints).</summary>
public class HintTests
{
    static EraTestHost Host(string script)
    {
        var t = new EraTestHost().Script("t.erm", "ZVSE2\n" + script);
        string data = Path.Combine(t.ModDir, "Data");
        File.WriteAllText(Path.Combine(data, "artraits.txt"), H3DataTests.Artraits());
        File.WriteAllText(Path.Combine(data, "sstraits.txt"),
            "\tDescription\r\nName\tBasic\tAdvanced\tExpert\r\nPathfinding\tb\ta\te\r\nArchery\tab\taa\tae\r\n");
        File.WriteAllText(Path.Combine(data, "sptraits.txt"), SpellReceiverTests.Sptraits());
        t.Host.SetEraFolders(null, Path.Combine(t.ModDir, "write"));
        return t.Start();
    }

    [Fact]
    public void Sn_h_texts_are_read_changed_appended_and_deleted()
    {
        // 3 wog - secondary skill text.erm: read an artifact's description, append, write it back
        using var t = Host("!?FU(Go);\n" +
            "!!SN:H^art^/5/1/?(desc:z);\n" +
            "!!SN:H^art^/5/1/^%(desc) +WoG^;\n" +
            "!!SN:H^art^/5/1/?z1;\n" +
            "!!SN:H^art^/5/0/^Axe of WoG^;\n" +
            "!!UN:N0/2/5;\n" +                          // UN:N reads the changed name
            "!!SN:H^secskill^/1/1/^more^;\n" +
            "!!SN:H^secskill^/1/1/d&^ and more^;\n" +   // d& appends to the kept hint
            "!!SN:H^secskill^/1/1/?z3;\n" +
            "!!SN:H^spell^/15/0/?z4;\n" +
            "!?FU(Delete);\n" +
            "!!SN:H^-art^/5/1/^^;\n" +
            "!!SN:H^art^/5/1/?z5;\n");
        t.Call("Go");
        Assert.Equal("", t.ErrorText);
        Assert.Equal(("Description 5 +WoG", "Axe of WoG", "more and more", "Spell15"), (t.Z(1), t.Z(2), t.Z(3), t.Z(4)));
        Assert.Contains(t.Host.Compat.Entries, e => e.Reason == SnReceiver.TextsNotShown);
        var loaded = WoGSaveSerializer.Deserialize(WoGSaveSerializer.Serialize(t.Host.State, "id"), "id");
        Assert.Equal("Axe of WoG", loaded.Hints["art"][5]);
        t.Call("Delete");
        Assert.Equal("Description 5", t.Z(5));
    }

    [Fact]
    public void Sn_h_checks_its_parameters()
    {
        using var t = Host("!?FU(NoSection);\n!!SN:H^nothing^/1/1/^x^;\n!?FU(NoSkill);\n!!SN:H^secskill^/28/0/^x^;\n" +
            "!?FU(TextNotLast);\n!!SN:H^object^/1/2/^x^/5;\n");
        t.Call("NoSection");
        t.Call("NoSkill");
        t.Call("TextNotLast");
        Assert.Equal(3, t.Errors);
    }

    [Fact]
    public void Object_hints_follow_eras_then_wogs_order()
    {
        using var t = Host("!?FU(Go);\n" +
            "!!SN:H^object^/63/18/^Era type^;\n" +
            "!!SN:H^object^/-1/-1/^Era any^;\n" +
            "!!SN:H^object^/10/11/0/^Era square^;\n" +
            "!!SN:H^object^/63/18/?z1;\n" +
            "!!OB12/12/0:H100;\n" +
            "!!HT101/-1:T^chest^;\n");
        t.Call("Go");
        Assert.Equal("Era type", t.Z(1));
        Assert.Empty(t.Host.Compat.Entries);                            // object hints show in the map tooltip
        t.Host.State.Erm.Z[99] = "WoG square %V1";                      // its codes are read when the hint shows
        t.Host.State.Erm.V[0] = 7;
        string? Hint(int x, int type, int sub, bool visible = true) => MapHints.ForObject(t.Erm, new MapPos(x, 11, 0), type, sub, visible, -1);
        Assert.Equal("Era square", MapHints.ForObject(t.Erm, new MapPos(10, 11, 0), 63, 18, true, -1));
        Assert.Equal("Era type", Hint(3, 63, 18));
        Assert.Equal("Era any", Hint(3, 5, 0));                       // any type, any subtype
        Assert.Null(Hint(3, 5, 0, visible: false));                   // Era's hints need a visible square
        Assert.Equal("WoG square 7", MapHints.ForObject(t.Erm, new MapPos(12, 12, 0), 5, 0, false, -1)); // the z var's codes
        Assert.Equal("chest", Hint(3, 101, 4, visible: false));
    }

    [Fact]
    public void Ht_hints_by_number_w_and_v_in_slot_order()
    {
        using var t = Host("!?FU(Go);\n" +
            "!!VRz180:S^not visited^;\n!!VRz181:S^visited^;\n" +
            "!!HT63/18:W21/0/180;\n!!HT63/18:W21/1/181;\n" +           // magic mushrooms: the active hero's w21
            "!!HT63/8:V590/0/400;\n!!VRz400:S^junk^;\n" +              // junk merchant: v590 = 0
            "!!VRz356:S^well^;\n!!HT113/-1:P15/z356;\n" +               // fishing well: the text is copied
            "!!HT63/-1:T^any mushroom^;\n" +
            "!!HT63/18:T?v1;\n" +                                        // no T hint of 63/18: 0
            "!?FU(Change);\n" +
            "!!VRz180:S^changed^;\n!!VRz356:S^not copied^;\n" +
            "!!HT63/18:W21/0/0;\n");                                     // 0 deletes, the slot is freed
        t.Call("Go");
        Assert.Equal("", t.ErrorText);
        Assert.Equal(0, t.V(1));
        var pos = new MapPos(4, 4, 0);
        string? Hint(int type, int sub, int hero) => MapHints.ForObject(t.Erm, pos, type, sub, false, hero);
        Assert.Equal("not visited", Hint(63, 18, 3));
        t.Host.State.Erm.WFor(3)[20] = 1;
        Assert.Equal("visited", Hint(63, 18, 3));
        Assert.Equal("any mushroom", Hint(63, 18, -1));                // no active hero: no W hint
        Assert.Equal("junk", Hint(63, 8, -1));
        Assert.Null(Hint(113, 0, -1));                                  // the square's number is 0, not 15
        t.Host.State.Squares[pos.Pack()] = new WoG.Core.State.PoSquare { Number = 15 };
        Assert.Equal("well", Hint(113, 0, -1));
        t.Host.State.Erm.WFor(3)[20] = 0;
        t.Call("Change");
        Assert.Equal("any mushroom", Hint(63, 18, 3));                  // the W hint for 0 is gone
        Assert.Equal("well", Hint(113, 0, -1));                         // a copied text does not follow z356
        t.Host.State.Erm.WFor(3)[20] = 1;
        Assert.Equal("visited", Hint(63, 18, 3));
        Assert.Null(t.Host.State.TypeHints[0]);                         // the freed slot…
        t.Erm.Diagnostics.Clear();
        var loaded = WoGSaveSerializer.Deserialize(WoGSaveSerializer.Serialize(t.Host.State, "id"), "id");
        Assert.Equal(t.Host.State.TypeHints.Count, loaded.TypeHints.Count);
        Assert.Equal("well", loaded.TypeHints.First(h => h?.Hint.Text != null)!.Hint.Text);
    }

    [Fact]
    public void A_new_ht_hint_takes_the_first_free_slot()
    {
        using var t = Host("!?FU(Go);\n!!VRz1:S^a^;\n!!VRz2:S^b^;\n!!VRz3:S^c^;\n" +
            "!!HT63/-1:V1/0/1;\n!!HT63/-1:V2/0/2;\n!!HT63/-1:V1/0/0;\n!!HT63/-1:V3/0/3;\n");
        t.Call("Go");
        // v1 = v2 = v3 = 0: the first slot (now v3's) wins over v2's
        Assert.Equal("c", MapHints.ForObject(t.Erm, new MapPos(0, 0, 0), 63, 5, false, -1));
    }

    [Fact]
    public void Ob_h_sets_and_b_deletes_a_square_hint()
    {
        using var t = Host("!?FU(Go);\n!!OB5/5/0:H^here^;\n!!OB5/5/0:H?v1;\n!?FU(Del);\n!!OB5/5/0:B;\n");
        t.Call("Go");
        Assert.Equal(-1, t.V(1));                                       // an own text
        Assert.Equal("here", MapHints.ForObject(t.Erm, new MapPos(5, 5, 0), 1, 1, false, -1));
        t.Call("Del");
        Assert.Empty(t.Host.State.Objects);
        Assert.Null(MapHints.ForObject(t.Erm, new MapPos(5, 5, 0), 1, 1, false, -1));
    }
}
