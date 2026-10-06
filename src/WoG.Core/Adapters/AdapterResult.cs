namespace WoG.Core.Adapters;

public enum AdapterStatus
{
    /// <summary>The operation was performed on the real game state.</summary>
    Ok,
    /// <summary>The target engine cannot do this (or the capability is not verified yet).</summary>
    Unsupported,
    /// <summary>Supported, but this call failed (bad id, no such hero, …).</summary>
    Failed,
}

/// <summary>
/// Result of every adapter call. Callers must not treat Unsupported as success: the ERM runtime records
/// it in the compatibility report instead of pretending the command worked.
/// </summary>
public readonly struct AdapterResult
{
    public AdapterStatus Status { get; }
    public string? Reason { get; }

    AdapterResult(AdapterStatus s, string? reason) { Status = s; Reason = reason; }

    public bool IsOk => Status == AdapterStatus.Ok;
    public static AdapterResult Ok => new(AdapterStatus.Ok, null);
    public static AdapterResult Unsupported(string reason) => new(AdapterStatus.Unsupported, reason);
    public static AdapterResult Failed(string reason) => new(AdapterStatus.Failed, reason);
    public override string ToString() => IsOk ? "Ok" : $"{Status}: {Reason}";
}

public readonly struct AdapterResult<T>
{
    public AdapterStatus Status { get; }
    public T Value { get; }
    public string? Reason { get; }

    AdapterResult(AdapterStatus s, T value, string? reason) { Status = s; Value = value; Reason = reason; }

    public bool IsOk => Status == AdapterStatus.Ok;
    public static AdapterResult<T> Ok(T value) => new(AdapterStatus.Ok, value, null);
    public static AdapterResult<T> Unsupported(string reason) => new(AdapterStatus.Unsupported, default!, reason);
    public static AdapterResult<T> Failed(string reason) => new(AdapterStatus.Failed, default!, reason);

    public AdapterResult AsPlain() => Status switch
    {
        AdapterStatus.Ok => AdapterResult.Ok,
        AdapterStatus.Unsupported => AdapterResult.Unsupported(Reason ?? ""),
        _ => AdapterResult.Failed(Reason ?? ""),
    };

    public override string ToString() => IsOk ? $"Ok({Value})" : $"{Status}: {Reason}";
}
