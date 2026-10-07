using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace WoG.Core.Ids;

/// <summary>
/// Engine map object id (sid) → H3 object type/subtype (ERM "Format OB"), from Compatibility/id-maps/object.json.
/// One-way: several engine objects may stand for one H3 type; placing an H3 type uses its first sid. Engine-only
/// objects have types from 1000 up; objects not in the file (scenery) are not map objects for ERM.
/// </summary>
public sealed class ObjectTypeMap
{
    readonly Dictionary<string, (int Type, int Subtype)> bySid = new(StringComparer.Ordinal);
    readonly Dictionary<(int, int), string> firstSid = new();

    public int Count => bySid.Count;

    sealed class Row
    {
        public string? engine { get; set; }
        public int type { get; set; }
        public int subtype { get; set; }
    }

    public static ObjectTypeMap Load(string path)
    {
        var map = new ObjectTypeMap();
        foreach (var r in JsonSerializer.Deserialize<List<Row>>(File.ReadAllText(path)) ?? new())
            if (!string.IsNullOrEmpty(r.engine)) map.Add(r.engine!, r.type, r.subtype);
        return map;
    }

    public void Add(string sid, int type, int subtype)
    {
        bySid[sid] = (type, subtype);
        firstSid.TryAdd((type, subtype), sid);
    }

    public bool TryGet(string sid, out int type, out int subtype)
    {
        if (bySid.TryGetValue(sid, out var t)) { (type, subtype) = t; return true; }
        type = subtype = -1;
        return false;
    }

    public string? FirstSid(int type, int subtype) => firstSid.TryGetValue((type, subtype), out var s) ? s : null;
}
