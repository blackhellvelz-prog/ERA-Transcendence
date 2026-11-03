using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace WoG.Core.Threading;

/// <summary>
/// Script entries (ERM triggers, timers, functions) run on one worker thread, so a script can wait for the player the
/// way H3's modal dialogs held it while the game keeps its own thread. Everything a script asks of the game runs on
/// the game's thread: while a script runs, the game thread executes the worker's calls (<see cref="OnGameThread{T}"/>)
/// and gets its own work back when the script ends or waits for an answer (<see cref="TryWait{T}"/>); the answer
/// resumes the script, pumped from the answer's callback.
/// <para>
/// One script waits at a time. An entry that arrives meanwhile (the next player's day while a script of this one's
/// asks) waits too: it runs after the waiting script ends, in the order the entries came, as nothing else ran under
/// H3's modal dialogs. One started from inside a call the game thread runs for the worker (a game event raised by a
/// script's own change) runs at once on the calling thread, nested as before; it cannot wait for the player.
/// </para>
/// <para>
/// A script makes thousands of calls (a new map's scripts: tens of thousands), so a hand-over first spins briefly
/// (both threads are busy with the same script) and blocks only after <see cref="SpinMicroseconds"/>.
/// </para>
/// </summary>
public sealed class ScriptFibers
{
    const int SpinMicroseconds = 200;
    static readonly long SpinTicks = Stopwatch.Frequency * SpinMicroseconds / 1_000_000;

    readonly object gate = new();
    readonly Thread worker;

    Action? entry;                       // handed to the worker
    bool running;                        // game thread only: an entry is on the worker (running, calling or waiting)
    volatile bool entryDone;
    Exception? entryError;               // published by entryDone
    Func<object?>? call;                 // a call the worker asks the game thread to run (Interlocked)
    volatile bool callDone;
    object? callResult;                  // published by callDone
    Exception? callError;
    volatile bool waiting;               // the worker waits for an answer; the game thread is free
    readonly Queue<Action> queued = new(); // game thread only: entries that came while a script waits

    /// <summary>An entry ended with an exception (reported on the game thread).</summary>
    public Action<Exception>? OnError { get; set; }

    public ScriptFibers()
    {
        worker = new Thread(WorkerLoop) { IsBackground = true, Name = "WoG scripts" };
        worker.Start();
    }

    /// <summary>True on the scripts' worker thread: the caller may wait for the player.</summary>
    public bool OnWorker => Thread.CurrentThread == worker;

    /// <summary>A script waits for the player's answer.</summary>
    public bool Waiting => waiting;

    /// <summary>Wakes the other thread if it blocked (the spinning one sees the change by itself).</summary>
    void Signal()
    {
        lock (gate) Monitor.PulseAll(gate);
    }

    /// <summary>Waits until <paramref name="ready"/>: spins for a moment, then blocks until a <see cref="Signal"/>.</summary>
    void Await(Func<bool> ready)
    {
        if (ready()) return;
        long until = Stopwatch.GetTimestamp() + SpinTicks;
        var spin = new SpinWait();
        while (Stopwatch.GetTimestamp() < until)
        {
            if (ready()) return;
            spin.SpinOnce(-1); // never Thread.Sleep(1): that would cost a millisecond per hand-over
        }
        lock (gate)
            while (!ready()) Monitor.Wait(gate, 50); // the timeout covers a signal sent between the check and the wait
    }

    /// <summary>
    /// Runs a script entry: on the worker, the calling (game) thread executing its calls until it ends or waits.
    /// While a script waits for the player, the entry waits its turn; nested in a running script (or on the worker
    /// itself) it runs at once on this thread.
    /// </summary>
    public void Run(Action action)
    {
        if (OnWorker)
        {
            action();
            return;
        }
        if (running)
        {
            if (waiting) queued.Enqueue(action);
            else action(); // a game event raised inside a call the game thread runs for the worker
            return;
        }
        Start(action);
        Pump();
    }

    /// <summary>The number of entries waiting for a script that waits for the player.</summary>
    public int Queued => queued.Count;

    void Start(Action action)
    {
        running = true;
        entryDone = false;
        Volatile.Write(ref entry, action);
        Signal();
    }

    /// <summary>
    /// From the worker: runs <paramref name="f"/> on the game thread and returns its result (its exception is thrown
    /// here). Any other thread runs it at once.
    /// </summary>
    public T OnGameThread<T>(Func<T> f)
    {
        if (!OnWorker) return f();
        callDone = false;
        Volatile.Write(ref call, () => f());
        Signal();
        Await(() => callDone);
        var (result, error) = (callResult, callError);
        (callResult, callError) = (null, null);
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
        return (T)result!;
    }

    sealed class Answer<T>
    {
        public volatile bool Done;
        public T Value = default!;
    }

    /// <summary>
    /// From the worker: starts a request on the game thread (a dialog, given the callback that answers it) and
    /// waits — the game thread is free meanwhile — until it is answered. False (nothing started) when the caller is
    /// not on the worker and so cannot wait.
    /// </summary>
    public bool TryWait<T>(Action<Action<T>> start, out T answer)
    {
        answer = default!;
        if (!OnWorker) return false;
        var slot = new Answer<T>();
        OnGameThread<object?>(() =>
        {
            start(value => Resume(slot, value));
            return null;
        });
        if (!slot.Done)
        {
            // Answered later, on the game thread, after its pump has seen this flag and returned (a callback can come
            // only from the game's frames): Resume clears the flag and pumps again.
            waiting = true;
            Signal();
            lock (gate)
                while (!slot.Done) Monitor.Wait(gate);
        }
        answer = slot.Value;
        return true;
    }

    /// <summary>On the game thread (a dialog's callback): the answer; the parked script goes on, pumped from here.</summary>
    void Resume<T>(Answer<T> slot, T value)
    {
        if (slot.Done) return;
        slot.Value = value;
        bool pump = waiting; // false: answered at once, inside the request — the worker never parked
        waiting = false;
        slot.Done = true;
        Signal();
        if (pump) Pump();
    }

    /// <summary>The game thread: runs the worker's calls until its entry ends or waits for an answer.</summary>
    void Pump()
    {
        while (true)
        {
            Await(() => Volatile.Read(ref call) != null || entryDone || waiting);
            var c = Interlocked.Exchange(ref call, null);
            if (c != null)
            {
                try { callResult = c(); }
                catch (Exception ex) { callError = ex; }
                callDone = true;
                Signal();
                continue;
            }
            if (entryDone)
            {
                running = false;
                entryDone = false;
                var failed = entryError;
                entryError = null;
                if (failed != null) OnError?.Invoke(failed);
                if (queued.Count == 0) return;
                Start(queued.Dequeue()); // the next entry that came while this one waited
                continue;
            }
            if (waiting) return; // the script waits for the player: the game goes on
        }
    }

    void WorkerLoop()
    {
        while (true)
        {
            Await(() => Volatile.Read(ref entry) != null);
            var a = Interlocked.Exchange(ref entry, null)!;
            Exception? error = null;
            try { a(); }
            catch (Exception ex) { error = ex; }
            entryError = error;
            entryDone = true;
            Signal();
        }
    }
}
