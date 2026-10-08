using System.Collections.Generic;
using WoG.Core.Model;

namespace WoG.Core.Adapters;

/// <summary>
/// The heroes of an engine with fewer heroes than H3's 156 numbers (Olden Era). A number the engine has a hero for is
/// that hero; any other number 0..155 is an H3 pool hero — not owned (-1), not on the map, kept as a record in the WoG
/// state — because in H3 every hero exists all the time and scripts walk over all of them (WoG Scripts' adventure cave,
/// enhanced secondary skills). A pool hero never enters play: the engine has no such hero to hire.
/// </summary>
public sealed class PoolHeroAdapter : IHeroAdapter
{
    readonly IHeroAdapter game;

    public PoolHeroAdapter(IHeroAdapter game, HeroRecords pool)
    {
        this.game = game;
        Pool = pool;
    }

    public HeroRecords Pool { get; }

    IHeroAdapter Of(int hero) => game.Exists(hero) ? game : Pool;

    /// <summary>True when the number is an engine hero, false for a pool hero.</summary>
    public bool InGame(int hero) => game.Exists(hero);

    public bool Exists(int hero) => game.Exists(hero) || Pool.Exists(hero);
    public AdapterResult<int> Get(int hero, HeroStat stat) => Of(hero).Get(hero, stat);
    public AdapterResult Set(int hero, HeroStat stat, int value) => Of(hero).Set(hero, stat, value);
    public AdapterResult<int> GetBase(int hero, HeroStat stat) => Of(hero).GetBase(hero, stat);
    public AdapterResult<MapPos> GetPosition(int hero) => Of(hero).GetPosition(hero);
    public AdapterResult MoveTo(int hero, MapPos pos, bool withEffect) => Of(hero).MoveTo(hero, pos, withEffect);
    /// <summary>Pool heroes are not on the map.</summary>
    public AdapterResult<int> HeroAt(MapPos pos) => game.HeroAt(pos);
    public AdapterResult<int> GetSecondarySkill(int hero, int skill) => Of(hero).GetSecondarySkill(hero, skill);
    public AdapterResult SetSecondarySkill(int hero, int skill, int level) => Of(hero).SetSecondarySkill(hero, skill, level);
    public AdapterResult<IReadOnlyList<int>> GetSecondarySkillOrder(int hero) => Of(hero).GetSecondarySkillOrder(hero);
    public AdapterResult SetSecondarySkillOrder(int hero, IReadOnlyList<int> order) => Of(hero).SetSecondarySkillOrder(hero, order);
    public AdapterResult<bool> HasSpell(int hero, int spell) => Of(hero).HasSpell(hero, spell);
    public AdapterResult SetSpell(int hero, int spell, bool known) => Of(hero).SetSpell(hero, spell, known);
    public AdapterResult<WoGStack> GetStack(int hero, int slot) => Of(hero).GetStack(hero, slot);
    public AdapterResult SetStack(int hero, int slot, int type, int count) => Of(hero).SetStack(hero, slot, type, count);
    public AdapterResult<int[]> GetArtifacts(int hero) => Of(hero).GetArtifacts(hero);
    public AdapterResult PutArtifact(int hero, int position, int artifact) => Of(hero).PutArtifact(hero, position, artifact);
    public AdapterResult RemoveArtifactAt(int hero, int position) => Of(hero).RemoveArtifactAt(hero, position);
    public AdapterResult AddToBackpack(int hero, int artifact) => Of(hero).AddToBackpack(hero, artifact);
    public AdapterResult EquipArtifact(int hero, int artifact) => Of(hero).EquipArtifact(hero, artifact);
    public AdapterResult<string> GetName(int hero) => Of(hero).GetName(hero);
    public AdapterResult SetName(int hero, string name) => Of(hero).SetName(hero, name);
    public AdapterResult<string> GetBiography(int hero, bool original) => Of(hero).GetBiography(hero, original);
    public AdapterResult SetBiography(int hero, string text) => Of(hero).SetBiography(hero, text);
    public AdapterResult<int[]> GetSpecialty(int hero) => Of(hero).GetSpecialty(hero);
    public AdapterResult SetSpecialty(int hero, int[] record) => Of(hero).SetSpecialty(hero, record);
    public AdapterResult<(int Type, int Min, int Max)> GetStartArmy(int hero, int slot) => Of(hero).GetStartArmy(hero, slot);
    public AdapterResult SetStartArmy(int hero, int slot, int type, int min, int max) => Of(hero).SetStartArmy(hero, slot, type, min, max);
    public AdapterResult Kill(int hero) => Of(hero).Kill(hero);
}
