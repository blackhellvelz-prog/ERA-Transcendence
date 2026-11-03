using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;
using WoG.Core.Adapters;

namespace WoG.Core.Threading;

/// <summary>
/// An adapter method that runs on the caller's thread even on the scripts' worker (it waits for the player itself,
/// through <see cref="ScriptFibers.TryWait{T}"/>); every other adapter call of a script runs on the game thread.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class OnCallerThreadAttribute : Attribute
{
}

/// <summary>
/// The engine adapter as scripts see it when they run on the <see cref="ScriptFibers"/> worker: each call is carried
/// to the game thread (engines are single-threaded: IL2CPP objects, Unity's interface), the results come back as
/// they are.
/// </summary>
public sealed class GameThreadAdapter : IGameAdapter
{
    readonly IGameAdapter target;

    public GameThreadAdapter(IGameAdapter target, ScriptFibers fibers)
    {
        this.target = target;
        Heroes = GameThreadProxy<IHeroAdapter>.Wrap(target.Heroes, fibers);
        Players = GameThreadProxy<IPlayerAdapter>.Wrap(target.Players, fibers);
        Creatures = GameThreadProxy<ICreatureTypeAdapter>.Wrap(target.Creatures, fibers);
        Map = GameThreadProxy<IMapAdapter>.Wrap(target.Map, fibers);
        Towns = GameThreadProxy<ITownAdapter>.Wrap(target.Towns, fibers);
        Ui = GameThreadProxy<IUiAdapter>.Wrap(target.Ui, fibers);
        Battle = GameThreadProxy<IBattleAdapter>.Wrap(target.Battle, fibers);
        Clock = GameThreadProxy<IGameClock>.Wrap(target.Clock, fibers);
    }

    /// <summary>The adapter behind (for code on the game thread that needs its own type).</summary>
    public IGameAdapter Target => target;

    public string EngineName => target.EngineName;
    public IHeroAdapter Heroes { get; }
    public IPlayerAdapter Players { get; }
    public ICreatureTypeAdapter Creatures { get; }
    public IMapAdapter Map { get; }
    public ITownAdapter Towns { get; }
    public IUiAdapter Ui { get; }
    public IBattleAdapter Battle { get; }
    public IGameClock Clock { get; }
}

/// <summary>Carries the calls of one adapter interface to the game thread (see <see cref="GameThreadAdapter"/>).</summary>
public class GameThreadProxy<T> : DispatchProxy where T : class
{
    static readonly ConcurrentDictionary<MethodInfo, bool> callerThread = new();
    T target = null!;
    ScriptFibers fibers = null!;

    public static T Wrap(T target, ScriptFibers fibers)
    {
        var proxy = Create<T, GameThreadProxy<T>>();
        var self = (GameThreadProxy<T>)(object)proxy;
        self.target = target;
        self.fibers = fibers;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method == null) return null;
        if (fibers.OnWorker && !callerThread.GetOrAdd(method, m => m.GetCustomAttribute<OnCallerThreadAttribute>() != null))
            return fibers.OnGameThread(() => Call(method, args));
        return Call(method, args);
    }

    object? Call(MethodInfo method, object?[]? args)
    {
        try { return method.Invoke(target, args); }
        catch (TargetInvocationException e) when (e.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            throw;
        }
    }
}
