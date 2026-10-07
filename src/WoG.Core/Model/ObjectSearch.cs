using System.Collections.Generic;
using System.Linq;

namespace WoG.Core.Model;

/// <summary>
/// WoG's map object search (erm.cpp CalcObjects, FindObjects, FindNextObjects, IsObjectType) over the objects the
/// adapter reports. H3 scans the squares x fastest, then y, then the level; an object counts at its entrance square,
/// and subtype −1 matches any subtype.
/// </summary>
public sealed class ObjectSearch
{
    readonly int size, levels;
    readonly List<(int Index, WoGMapObject Obj)> objects;

    /// <param name="levels">H3's GetMapLevels: 0 = surface only, 1 = with underground.</param>
    public ObjectSearch(int size, int levels, IEnumerable<WoGMapObject> all)
    {
        this.size = size;
        this.levels = levels;
        objects = all.Where(o => !o.Position.IsNone)
            .Select(o => (Index(o.Position.X, o.Position.Y, o.Position.L), o))
            .OrderBy(t => t.Item1).ToList();
    }

    int Index(int x, int y, int l) => x + (y + l * size) * size;
    int Last => size * size * (levels + 1) - 1;
    MapPos Pos(int index) => new(index % size, index / size % size, index / (size * size));

    static bool Matches(WoGMapObject o, int type, int subtype) => o.Type == type && (subtype == -1 || o.SubType == subtype);

    /// <summary>CalcObjects: how many objects of the type/subtype the map has.</summary>
    public int Count(int type, int subtype) => objects.Count(t => Matches(t.Obj, type, subtype));

    /// <summary>FindObjects: the number-th (1-based) object of the type in scan order, or null.</summary>
    public MapPos? Find(int type, int subtype, int number)
    {
        if (number < 1) return null;
        var hit = objects.Where(t => Matches(t.Obj, type, subtype)).Skip(number - 1).FirstOrDefault();
        return hit.Obj == null ? null : hit.Obj.Position;
    }

    /// <summary>
    /// FindNextObjects: from the square after (x, y, l) forward (direction −1) or the one before it backward (−2);
    /// x = −1 starts at the first square, x = −2 at the last one (that square included).
    /// </summary>
    public MapPos? FindNext(int type, int subtype, int x, int y, int l, int direction)
    {
        bool backward = direction == -2;
        int start = x == -1 ? 0 : x == -2 ? Last : Index(x, y, l) + (backward ? -1 : 1);
        if (start < 0 || start > Last) return null;
        var matching = objects.Where(t => Matches(t.Obj, type, subtype));
        var hit = backward
            ? matching.LastOrDefault(t => t.Index <= start)
            : matching.FirstOrDefault(t => t.Index >= start);
        return hit.Obj == null ? null : Pos(hit.Index);
    }
}
