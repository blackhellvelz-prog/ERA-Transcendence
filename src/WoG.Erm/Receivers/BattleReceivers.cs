using WoG.Core.Compat;
using WoG.Erm.Runtime;

namespace WoG.Erm.Receivers;

/// <summary>!!BA — the battle being set up or fought (Monsters.cpp ERM_Battle: the G2B_* globals of the battle).</summary>
public sealed class BaReceiver : ErmReceiverBase
{
    const string Note = "BA:H/O/P/Q/S/E/A — the battle as the adapter saw it start: attacker = the active hero of the player whose turn it is, defender = the monster squad next to it (hero-vs-hero and town battles are not told apart yet); read only";
    const string Change = "BA — changing the heroes, position or quick-battle flag of a battle: not mapped yet";

    public BaReceiver() : base("BA")
    {
        Declare("HOPQSEA", CompatLevel.PartiallySupported, Note);
        Declare("M", CompatLevel.Unsupported, "BA:M — the armies of the battle sides are not mapped yet");
        Declare("DB", CompatLevel.Unsupported, "BA:D/B — cancelling a battle and its background are not mapped yet");
    }

    protected override void Run(ErmCall c)
    {
        var b = c.Need(c.Rt.Services.Game.Battle.GetBattle());
        switch (c.Letter)
        {
            case 'H':
            {
                c.RequireMin(2);
                int side = c.N(0);
                if (side < 0 || side > 1) throw new ErmRuntimeException("\"!!BA:H\"-side out of range (0,1).");
                int hero = b.Heroes[side] >= 0 ? b.Heroes[side] : -2; // WoG: -2 when the side has no hero
                MapSelector.ReadOnly(c, hero, 1, Change);
                break;
            }
            case 'O':
            {
                c.RequireMin(2);
                int a = b.Owners[0], d = b.Owners[1];
                c.Apply(ref a, 0); // WoG applies to copies: a "set" changes nothing
                c.Apply(ref d, 1);
                break;
            }
            case 'P':
            {
                c.RequireMin(3);
                MapSelector.ReadOnly(c, b.Position.X, 0, Change);
                MapSelector.ReadOnly(c, b.Position.Y, 1, Change);
                MapSelector.ReadOnly(c, b.Position.L, 2, Change);
                break;
            }
            case 'Q':
                c.RequireMin(1);
                MapSelector.ReadOnly(c, b.Quick ? 1 : 0, 0, Change);
                break;
            case 'S':
            {
                c.RequireMin(1);
                int v = b.Siege ? 1 : 0;
                c.Apply(ref v, 0);
                break;
            }
            case 'E':
            {
                int v = 0; // single-player: never a human-vs-human network battle
                c.Apply(ref v, 0);
                break;
            }
            case 'A':
            {
                c.RequireMin(1);
                int v = b.CompleteAi ? 1 : 0;
                c.Apply(ref v, 0);
                break;
            }
            case 'M':
                throw new ErmUnsupportedException("BA:M — the armies of the battle sides are not mapped yet");
            case 'D':
            case 'B':
                throw new ErmUnsupportedException("BA:D/B — cancelling a battle and its background are not mapped yet");
            default:
                throw ErmCall.WrongCommand(c.Letter);
        }
    }
}
