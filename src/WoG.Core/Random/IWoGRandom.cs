namespace WoG.Core.Random;

public interface IWoGRandom
{
    /// <summary>Uniform integer in [min, max] inclusive (WoG Random(min,max)).</summary>
    int Next(int min, int max);
    int Seed { get; set; }
}

/// <summary>
/// The MSVC CRT linear congruential generator that H3 (a VC++ 6 program) uses for rand():
/// seed = seed * 214013 + 2531011; rand = (seed >> 16) &amp; 0x7FFF. WoG's Random(min,max) is built on the
/// game's generator; the exact call sequence of the original is not reproduced (UNVERIFIED), so this is
/// the same generator family with deterministic seeding for tests and replays.
/// </summary>
public sealed class MsvcRandom : IWoGRandom
{
    uint state;

    public MsvcRandom(int seed = 1) { state = unchecked((uint)seed); }

    public int Seed
    {
        get => unchecked((int)state);
        set => state = unchecked((uint)value);
    }

    public int Rand()
    {
        state = unchecked(state * 214013u + 2531011u);
        return (int)((state >> 16) & 0x7FFF);
    }

    public int Next(int min, int max)
    {
        if (max <= min) return min;
        long span = (long)max - min + 1;
        // Two draws give 30 bits — enough for every range ERM uses.
        long r = ((long)Rand() << 15) | (long)Rand();
        return (int)(min + r % span);
    }
}
