using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using WoG.Host;

namespace WoG.OldenEra;

/// <summary>
/// The WoG state of a game travels with Olden Era's saves [V-game 0.81.04]. A save is gzip-compressed text that
/// starts with a 32-hex-digit checksum; after that save is loaded the session's StartInfo.fileHashSum holds the same
/// checksum. So the state is written when the game reports a save (event MapSaved, with the save's path relative to
/// the user's folder under Application.persistentDataPath) to BepInEx/config/WoG/saves/&lt;checksum&gt;.wog.json, and
/// read back when a session starts from a save. Nothing is written into the game's save folder.
/// </summary>
internal static class SaveSync
{
    public static string Dir { get; set; } = "";

    static string? persistent;

    static string PersistentDataPath()
    {
        if (persistent != null) return persistent;
        try
        {
            var app = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEngine.Application")).FirstOrDefault(t => t != null);
            persistent = app?.GetProperty("persistentDataPath", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as string;
        }
        catch (Exception) { persistent = null; }
        persistent ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "Unfrozen", "HeroesOldenEra");
        return persistent;
    }

    /// <summary>The file a save path names: relative to a user's folder (persistentDataPath/users/&lt;user&gt;); the newest match.</summary>
    public static string? Resolve(string path)
    {
        if (Path.IsPathRooted(path)) return File.Exists(path) ? path : null;
        string root = PersistentDataPath();
        var candidates = new List<string> { Path.Combine(root, path) };
        string users = Path.Combine(root, "users");
        if (Directory.Exists(users)) candidates.AddRange(Directory.GetDirectories(users).Select(u => Path.Combine(u, path)));
        return candidates.Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
    }

    /// <summary>The checksum an Olden Era save starts with.</summary>
    public static string? SaveHash(string file)
    {
        using var gz = new GZipStream(File.OpenRead(file), CompressionMode.Decompress);
        var buf = new byte[64];
        int n = 0, r;
        while (n < buf.Length && (r = gz.Read(buf, n, buf.Length - n)) > 0) n += r;
        var m = Regex.Match(Encoding.ASCII.GetString(buf, 0, n), "[0-9a-f]{32}");
        return m.Success ? m.Value : null;
    }

    public static string StateFile(string hash) => Path.Combine(Dir, hash + ".wog.json");

    sealed record Pending(string Path, string Snapshot, DateTime At);

    static readonly List<Pending> pending = new();

    /// <summary>
    /// MapSaved: the WoG state is taken now (before-save triggers included); the save file is written by the game a
    /// little later [V-game: it does not exist yet when the event is raised], so the state file follows in <see cref="Poll"/>.
    /// </summary>
    public static void OnSaved(WoGHost host, string savePath) =>
        pending.Add(new Pending(savePath, host.SaveSnapshot(), DateTime.UtcNow));

    /// <summary>Writes the state of saves whose file is complete; gives up on a save after 30 seconds.</summary>
    public static void Poll()
    {
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            var p = pending[i];
            string? hash = null, file = null;
            try
            {
                file = Resolve(p.Path);
                if (file != null && File.GetLastWriteTimeUtc(file) >= p.At.AddSeconds(-5)) hash = SaveHash(file);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { hash = null; }
            if (hash == null)
            {
                if (DateTime.UtcNow - p.At > TimeSpan.FromSeconds(30))
                {
                    pending.RemoveAt(i);
                    WoGPlugin.L?.LogWarning("WoG: the saved game did not appear, its WoG state is not kept: " + p.Path);
                }
                continue;
            }
            pending.RemoveAt(i);
            Directory.CreateDirectory(Dir);
            WoGHost.WriteSnapshot(StateFile(hash), p.Snapshot, hash);
            WoGPlugin.L?.LogInfo($"WoG: state saved with {System.IO.Path.GetFileName(file)} ({hash})");
        }
    }
}
