using WoG.Core.Visual;
using WoG.Erm.Runtime;
using WoG.Erm.Syntax;
using WoG.Headless;
using WoG.Host;

namespace WoG.Tests;

/// <summary>A WoG host over the headless reference engine, with helpers to run ERM snippets.</summary>
public sealed class TestHost
{
    public HeadlessGame Game { get; } = new();
    public WoGHost Host { get; }
    public ErmRuntime Erm => Host.Erm!;

    public TestHost(ErmDialect dialect = ErmDialect.Wog358, bool bugs = false, WoGModules? modules = null)
    {
        Host = new WoGHost(Game, new VisualResolver(new NoAssets()), modules,
            new ErmRuntimeOptions { Dialect = dialect, ReproduceKnownBugs = bugs });
    }

    /// <summary>Loads a script as a new game (instructions run).</summary>
    public TestHost Load(string body, string name = "test.erm")
    {
        Host.AddScript(name, "ZVSE\n" + body);
        return this;
    }

    public TestHost Start()
    {
        Host.StartNewGame();
        return this;
    }

    /// <summary>Calls !?FU# with arguments.</summary>
    public int[] Call(int fn, params int[] args) => Erm.CallFunction(fn, args);

    public int V(int i) => Host.State.Erm.V[i - 1];
    public void SetV(int i, int value) => Host.State.Erm.V[i - 1] = value;
    public bool F(int i) => Host.State.Erm.Flags[i - 1];
    public string Z(int i) => Host.State.Erm.Z[i - 1];
    public int ErrorCount => Erm.Diagnostics.FindAll(d => d.Severity == ErmSeverity.Error).Count;
}
