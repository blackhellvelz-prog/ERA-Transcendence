using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WoG.Core.State;

namespace WoG.Core.Save;

/// <summary>Raised when a WoG save blob cannot be trusted (wrong schema, checksum, identity).</summary>
public sealed class WoGSaveException : Exception
{
    public WoGSaveException(string message) : base(message) { }
}

/// <summary>
/// Versioned, checksummed JSON persistence of <see cref="WoGGameState"/>. Written as a side-car file next
/// to the engine's own save (OldenEra_ReverseEngineering/05_Save_System.md).
/// </summary>
public static class WoGSaveSerializer
{
    sealed class Envelope
    {
        public string format { get; set; } = "wog-oe-save";
        public int schema { get; set; }
        public string identity { get; set; } = "";
        public string sha256 { get; set; } = "";
        public string payload { get; set; } = "";
    }

    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = false,
        IncludeFields = false,
    };

    public static string Serialize(WoGGameState state, string identity)
    {
        string payload = JsonSerializer.Serialize(state, Json);
        var env = new Envelope
        {
            schema = WoGGameState.SchemaVersion,
            identity = identity,
            sha256 = Hash(payload),
            payload = payload,
        };
        return JsonSerializer.Serialize(env, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Restores state; throws <see cref="WoGSaveException"/> on any integrity problem.</summary>
    public static WoGGameState Deserialize(string text, string? expectedIdentity = null)
    {
        var env = JsonSerializer.Deserialize<Envelope>(text) ?? throw new WoGSaveException("empty WoG save");
        if (env.format != "wog-oe-save") throw new WoGSaveException("not a WoG save blob");
        if (env.schema != WoGGameState.SchemaVersion)
            throw new WoGSaveException($"WoG save schema {env.schema}, expected {WoGGameState.SchemaVersion}");
        if (Hash(env.payload) != env.sha256) throw new WoGSaveException("WoG save checksum mismatch");
        if (expectedIdentity != null && env.identity != expectedIdentity)
            throw new WoGSaveException($"WoG save belongs to '{env.identity}', not '{expectedIdentity}'");
        var state = JsonSerializer.Deserialize<WoGGameState>(env.payload, Json)
                    ?? throw new WoGSaveException("WoG save payload is empty");
        state.Ids.RebuildReverse();
        return state;
    }

    public static void WriteFile(string path, WoGGameState state, string identity)
    {
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, Serialize(state, identity), Encoding.UTF8);
        if (File.Exists(path)) File.Delete(path);
        File.Move(tmp, path);
    }

    public static WoGGameState ReadFile(string path, string? expectedIdentity = null) =>
        Deserialize(File.ReadAllText(path, Encoding.UTF8), expectedIdentity);

    static string Hash(string s)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(s)));
    }
}
