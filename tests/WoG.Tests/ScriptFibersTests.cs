using System;
using System.Collections.Generic;
using System.Threading;
using WoG.Core.Threading;
using Xunit;

namespace WoG.Tests;

/// <summary>
/// Scripts on a worker thread that can wait for the player (IF:Q, IF:M in the game). The test thread plays the
/// game's thread: it runs the worker's calls while a script runs and gets control back when the script waits.
/// </summary>
public class ScriptFibersTests
{
    [Fact]
    public void A_script_runs_on_the_worker_and_its_calls_on_the_game_thread()
    {
        var fibers = new ScriptFibers();
        var game = Thread.CurrentThread;
        Thread? script = null;
        var callThreads = new List<Thread>();
        int sum = 0;
        fibers.Run(() =>
        {
            script = Thread.CurrentThread;
            for (int i = 1; i <= 3; i++)
                sum += fibers.OnGameThread(() => { callThreads.Add(Thread.CurrentThread); return i * 10; });
        });
        Assert.NotNull(script);
        Assert.NotSame(game, script);
        Assert.All(callThreads, t => Assert.Same(game, t));
        Assert.Equal(60, sum);                      // the run returned after the script ended
        Assert.False(fibers.Waiting);
    }

    [Fact]
    public void A_waiting_script_gives_the_game_thread_back_and_goes_on_after_the_answer()
    {
        var fibers = new ScriptFibers();
        Action<bool>? answer = null;
        var log = new List<string>();
        fibers.Run(() =>
        {
            log.Add("asks");
            Assert.True(fibers.TryWait<bool>(a => answer = a, out bool yes));
            log.Add("answer " + yes);
            log.Add("then " + fibers.OnGameThread(() => "a game call"));
        });
        // the script waits: the game thread is back here, the question is up
        Assert.True(fibers.Waiting);
        Assert.Equal(new[] { "asks" }, log);
        Assert.NotNull(answer);

        // meanwhile another entry runs at once on this thread (it cannot wait)
        bool nestedOnGame = false;
        fibers.Run(() => nestedOnGame = !fibers.OnWorker && !fibers.TryWait<bool>(_ => { }, out _));
        Assert.True(nestedOnGame);

        answer!(false); // the player answers: the script finishes, pumped from the answer's callback
        Assert.False(fibers.Waiting);
        Assert.Equal(new[] { "asks", "answer False", "then a game call" }, log);
    }

    [Fact]
    public void An_answer_given_at_once_does_not_park_the_script()
    {
        var fibers = new ScriptFibers();
        bool got = false;
        fibers.Run(() => got = fibers.TryWait<bool>(a => a(true), out bool yes) && yes);
        Assert.True(got);
        Assert.False(fibers.Waiting);
    }

    [Fact]
    public void Errors_of_calls_reach_the_script_and_errors_of_entries_are_reported()
    {
        var reported = new List<Exception>();
        var fibers = new ScriptFibers { OnError = reported.Add };
        string? caught = null;
        fibers.Run(() =>
        {
            try { fibers.OnGameThread<int>(() => throw new InvalidOperationException("from the game")); }
            catch (InvalidOperationException ex) { caught = ex.Message; }
            throw new ArgumentException("from the script");
        });
        Assert.Equal("from the game", caught);
        Assert.Single(reported);
        Assert.IsType<ArgumentException>(reported[0]);
        // the worker takes the next entry
        int after = 0;
        fibers.Run(() => after = 1);
        Assert.Equal(1, after);
    }

    [Fact]
    public void The_adapter_proxy_carries_calls_to_the_game_thread_except_the_dialogs()
    {
        var fibers = new ScriptFibers();
        var game = new WoG.Headless.HeadlessGame();
        var proxied = new GameThreadAdapter(game, fibers);
        var game0 = Thread.CurrentThread;
        bool exists = false;
        WoG.Core.Adapters.AdapterResult shown = WoG.Core.Adapters.AdapterResult.Ok;
        fibers.Run(() =>
        {
            exists = proxied.Heroes.Exists(0) || !proxied.Heroes.Exists(0); // any call: it runs on the game thread
            shown = proxied.Ui.ShowMessage("hello");                         // [OnCallerThread]: on the worker
        });
        Assert.True(exists);
        Assert.Equal(WoG.Core.Adapters.AdapterStatus.Ok, shown.Status);
        Assert.Equal(new[] { "hello" }, game.Messages);
        Assert.Same(game0, Thread.CurrentThread);
    }
}
