using System;
using System.Collections.Generic;
using System.Linq;
using WoG.Core.Model;

namespace WoG.Core.Adapters;

/// <summary>
/// Heroes kept as <see cref="WoGHero"/> records: the heroes of the headless reference engine, and the H3 pool heroes
/// of a game whose engine has no hero for a number (<see cref="PoolHeroAdapter"/>).
/// </summary>
public class HeroRecords : IHeroAdapter
{
    readonly Func<Dictionary<int, WoGHero>>? store;
    readonly Dictionary<int, WoGHero> own = new();

    /// <param name="store">Where the records live (the WoG state of the current game); null = this object.</param>
    public HeroRecords(Func<Dictionary<int, WoGHero>>? store = null) => this.store = store;

    public Dictionary<int, WoGHero> HeroList => store?.Invoke() ?? own;

    /// <summary>Every number 0..155 is a hero (H3); false = only the heroes added (an engine with fewer heroes).</summary>
    public bool AllNumbersExist { get; init; } = true;

    /// <summary>The name a pool hero is made with (H3's HOTRAITS.TXT); null = "Hero&lt;n&gt;".</summary>
    public Func<int, string?>? PoolName { get; set; }

    protected static AdapterResult<T> NoHero<T>(int h) => AdapterResult<T>.Failed($"hero {h} does not exist");

    public WoGHero AddHero(int id, int owner, int heroClass = 0, string name = "")
    {
        var h = new WoGHero { Id = id, Owner = owner, HeroClass = heroClass, Name = name.Length > 0 ? name : $"Hero{id}" };
        HeroList[id] = h;
        return h;
    }

    /// <summary>
    /// Every H3 hero number (0..155) is a hero, as in H3: one not added is in the pool — not owned (-1), not on the
    /// map — and is made on first use; scripts that walk over all heroes (WoG Scripts' adventure cave, enhanced
    /// secondary skills) read it as H3 would.
    /// </summary>
    public bool Exists(int hero) => HeroList.ContainsKey(hero) || AllNumbersExist && hero >= 0 && hero < WoGLimits.HeroCount;

    public bool TryHero(int hero, out WoGHero h)
    {
        var list = HeroList;
        if (list.TryGetValue(hero, out h!)) return true;
        if (!AllNumbersExist || hero < 0 || hero >= WoGLimits.HeroCount) return false;
        h = new WoGHero { Id = hero, Owner = -1, Name = PoolName?.Invoke(hero) ?? $"Hero{hero}", Position = MapPos.None };
        list[hero] = h;
        return true;
    }

    public AdapterResult<int> Get(int hero, HeroStat stat)
    {
        if (!TryHero(hero, out var h)) return NoHero<int>(hero);
        return AdapterResult<int>.Ok(stat switch
        {
            HeroStat.Experience => h.Experience,
            HeroStat.Level => h.Level,
            HeroStat.Attack => h.Primary[0],
            HeroStat.Defence => h.Primary[1],
            HeroStat.Power => h.Primary[2],
            HeroStat.Knowledge => h.Primary[3],
            HeroStat.Mana => h.Mana,
            HeroStat.Movement => h.Movement,
            HeroStat.Owner => h.Owner,
            HeroStat.HeroClass => h.HeroClass,
            _ => 0,
        });
    }

    public AdapterResult Set(int hero, HeroStat stat, int value)
    {
        if (!TryHero(hero, out var h)) return AdapterResult.Failed($"hero {hero} does not exist");
        switch (stat)
        {
            case HeroStat.Experience: h.Experience = value; break;
            case HeroStat.Level: h.Level = value; break;
            case HeroStat.Attack: h.Primary[0] = value; break;
            case HeroStat.Defence: h.Primary[1] = value; break;
            case HeroStat.Power: h.Primary[2] = value; break;
            case HeroStat.Knowledge: h.Primary[3] = value; break;
            case HeroStat.Mana: h.Mana = value; break;
            case HeroStat.Movement: h.Movement = value; break;
            case HeroStat.Owner: h.Owner = value; break;
            case HeroStat.HeroClass: h.HeroClass = value; break;
        }
        return AdapterResult.Ok;
    }

    public AdapterResult<int> GetBase(int hero, HeroStat stat) => Get(hero, stat);

    public AdapterResult<MapPos> GetPosition(int hero) =>
        TryHero(hero, out var h) ? AdapterResult<MapPos>.Ok(h.Position) : NoHero<MapPos>(hero);

    public AdapterResult MoveTo(int hero, MapPos pos, bool withEffect)
    {
        if (!TryHero(hero, out var h)) return AdapterResult.Failed($"hero {hero} does not exist");
        h.Position = pos;
        return AdapterResult.Ok;
    }

    public AdapterResult<int> HeroAt(MapPos pos)
    {
        foreach (var h in HeroList.Values) if (h.Position == pos) return AdapterResult<int>.Ok(h.Id);
        return AdapterResult<int>.Failed($"no hero at {pos}");
    }

    public AdapterResult<int> GetSecondarySkill(int hero, int skill) =>
        TryHero(hero, out var h) ? AdapterResult<int>.Ok(h.SecondarySkills[skill]) : NoHero<int>(hero);

    public AdapterResult SetSecondarySkill(int hero, int skill, int level)
    {
        if (!TryHero(hero, out var h)) return AdapterResult.Failed("no hero");
        h.SecondarySkills[skill] = level;
        // WoG HE:S#/$: a learned skill takes the next display slot (while fewer than 8 are shown); a removed one
        // gives its slot to the skill in the last slot.
        var order = h.SkillOrder;
        int at = order.IndexOf(skill);
        if (level == 0 && at >= 0)
        {
            order[at] = order[^1];
            order.RemoveAt(order.Count - 1);
        }
        else if (level != 0 && at < 0 && order.Count < 8) order.Add(skill);
        return AdapterResult.Ok;
    }

    public AdapterResult<IReadOnlyList<int>> GetSecondarySkillOrder(int hero) =>
        TryHero(hero, out var h) ? AdapterResult<IReadOnlyList<int>>.Ok(h.SkillOrder.ToList()) : NoHero<IReadOnlyList<int>>(hero);

    public AdapterResult SetSecondarySkillOrder(int hero, IReadOnlyList<int> order)
    {
        if (!TryHero(hero, out var h)) return AdapterResult.Failed("no hero");
        h.SkillOrder = order.ToList();
        return AdapterResult.Ok;
    }

    public AdapterResult<bool> HasSpell(int hero, int spell) =>
        TryHero(hero, out var h) ? AdapterResult<bool>.Ok(h.Spells.Contains(spell)) : NoHero<bool>(hero);

    public AdapterResult SetSpell(int hero, int spell, bool known)
    {
        if (!TryHero(hero, out var h)) return AdapterResult.Failed("no hero");
        if (known) h.Spells.Add(spell); else h.Spells.Remove(spell);
        return AdapterResult.Ok;
    }

    public AdapterResult<WoGStack> GetStack(int hero, int slot) =>
        TryHero(hero, out var h) ? AdapterResult<WoGStack>.Ok(h.Army.Slots[slot].Clone()) : NoHero<WoGStack>(hero);

    public AdapterResult SetStack(int hero, int slot, int type, int count)
    {
        if (!TryHero(hero, out var h)) return AdapterResult.Failed("no hero");
        h.Army.Slots[slot].Type = count <= 0 ? -1 : type;
        h.Army.Slots[slot].Count = type < 0 ? 0 : Math.Max(0, count);
        return AdapterResult.Ok;
    }

    /// <summary>Where an artifact is worn ("Format P2"), for HE:A4; null = everything goes to the backpack.</summary>
    public Func<int, int>? ArtifactPosition { get; set; }

    public AdapterResult<int[]> GetArtifacts(int hero)
    {
        if (!TryHero(hero, out var h)) return NoHero<int[]>(hero);
        var all = Enumerable.Repeat(-1, ArtifactSlots.Positions).ToArray();
        Array.Copy(h.Equipped, all, ArtifactSlots.Worn);
        for (int i = 0; i < h.Backpack.Count && i < ArtifactSlots.Backpack; i++) all[ArtifactSlots.Worn + i] = h.Backpack[i];
        return AdapterResult<int[]>.Ok(all);
    }

    public AdapterResult PutArtifact(int hero, int position, int artifact)
    {
        if (!TryHero(hero, out var h)) return AdapterResult.Failed("no hero");
        if (position < ArtifactSlots.Worn)
        {
            if (h.Equipped[position] != -1) return AdapterResult.Failed("position taken");
            h.Equipped[position] = artifact;
            return AdapterResult.Ok;
        }
        int k = position - ArtifactSlots.Worn;
        while (h.Backpack.Count <= k) h.Backpack.Add(-1);
        if (h.Backpack[k] != -1) return AdapterResult.Failed("position taken");
        h.Backpack[k] = artifact;
        return AdapterResult.Ok;
    }

    public AdapterResult RemoveArtifactAt(int hero, int position)
    {
        if (!TryHero(hero, out var h)) return AdapterResult.Failed("no hero");
        if (position < ArtifactSlots.Worn) h.Equipped[position] = -1;
        else if (position - ArtifactSlots.Worn < h.Backpack.Count) h.Backpack[position - ArtifactSlots.Worn] = -1;
        return AdapterResult.Ok;
    }

    public AdapterResult AddToBackpack(int hero, int artifact)
    {
        if (!TryHero(hero, out var h)) return AdapterResult.Failed("no hero");
        int k = h.Backpack.IndexOf(-1);
        if (k >= 0) h.Backpack[k] = artifact;
        else if (h.Backpack.Count < ArtifactSlots.Backpack) h.Backpack.Add(artifact);
        return AdapterResult.Ok;
    }

    public AdapterResult EquipArtifact(int hero, int artifact)
    {
        if (!TryHero(hero, out var h)) return AdapterResult.Failed("no hero");
        int p2 = artifact >= ArtifactSlots.ScrollBase ? 9 : ArtifactPosition?.Invoke(artifact) ?? 0;
        foreach (int slot in ArtifactSlots.ForPosition(p2))
            if (h.Equipped[slot] == -1) { h.Equipped[slot] = artifact; return AdapterResult.Ok; }
        return AddToBackpack(hero, artifact);
    }

    public AdapterResult<string> GetName(int hero) =>
        TryHero(hero, out var h) ? AdapterResult<string>.Ok(h.Name) : NoHero<string>(hero);

    public AdapterResult SetName(int hero, string name)
    {
        if (!TryHero(hero, out var h)) return AdapterResult.Failed("no hero");
        h.Name = name;
        return AdapterResult.Ok;
    }

    public AdapterResult<string> GetBiography(int hero, bool original) =>
        TryHero(hero, out var h) ? AdapterResult<string>.Ok(original ? h.DefaultBiography : h.Biography ?? "") : NoHero<string>(hero);

    public AdapterResult<int[]> GetSpecialty(int hero) =>
        TryHero(hero, out var h) ? AdapterResult<int[]>.Ok((int[])h.Specialty.Clone()) : NoHero<int[]>(hero);

    public AdapterResult SetSpecialty(int hero, int[] record)
    {
        if (!TryHero(hero, out var h)) return AdapterResult.Failed("no hero");
        h.Specialty = (int[])record.Clone();
        return AdapterResult.Ok;
    }

    public AdapterResult<(int Type, int Min, int Max)> GetStartArmy(int hero, int slot) =>
        TryHero(hero, out var h)
            ? AdapterResult<(int, int, int)>.Ok((h.StartArmy[slot][0], h.StartArmy[slot][1], h.StartArmy[slot][2])) : NoHero<(int, int, int)>(hero);

    public AdapterResult SetStartArmy(int hero, int slot, int type, int min, int max)
    {
        if (!TryHero(hero, out var h)) return AdapterResult.Failed("no hero");
        h.StartArmy[slot] = new[] { type, min, max };
        return AdapterResult.Ok;
    }

    public AdapterResult SetBiography(int hero, string text)
    {
        if (!TryHero(hero, out var h)) return AdapterResult.Failed("no hero");
        h.Biography = text;
        return AdapterResult.Ok;
    }

    public AdapterResult<int> ExperienceForLevel(int hero, int level) => AdapterResult<int>.Ok(H3Experience(level));

    /// <summary>H3's experience table (levels 1..12, then each step 1.2 times the last, truncated).</summary>
    static readonly int[] H3Levels = { 0, 1000, 2000, 3200, 4600, 6200, 8000, 10000, 12200, 14700, 17500, 20600 };

    /// <summary>
    /// The total experience of an H3 level: levels 1..12 from the table, then each step 1.2 times the one before
    /// (truncated) — 24320 for 13, 28784 for 14; past int's range, int.MaxValue.
    /// </summary>
    public static int H3Experience(int level)
    {
        if (level < 1) return 0;
        if (level <= H3Levels.Length) return H3Levels[level - 1];
        long total = H3Levels[^1], step = H3Levels[^1] - H3Levels[^2];
        for (int l = H3Levels.Length + 1; l <= level; l++)
        {
            step = (long)(step * 1.2);
            total += step;
            if (total >= int.MaxValue) return int.MaxValue;
        }
        return (int)total;
    }

    public AdapterResult Kill(int hero)
    {
        if (!TryHero(hero, out var h)) return AdapterResult.Failed("no hero");
        h.Alive = false;
        h.Owner = -1;
        return AdapterResult.Ok;
    }
}
