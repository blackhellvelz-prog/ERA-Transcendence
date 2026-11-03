using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

// remap-symbols <old interop> <new interop> <wog_symbols.json> [out.json]
//
// An Olden Era update renames the obfuscated types and members (dbx, fdq.cnsj, enb.ckso, …). For every binding of
// the symbol registry that no longer resolves against the new interop assemblies, this walks the binding's path in
// the old assemblies and finds the same types and members in the new ones by structure:
//   * types: refinement rounds over every obfuscated type — a type's features are its kind, base type, interfaces,
//     the types of its declared members, the names of its stable members, and who refers to it (stable types and
//     their stable members); a feature set that is unique on both sides maps the pair, each round makes the next
//     one more precise; whatever is still open is scored (weighted Jaccard) against the open new types;
//   * members: same name and type first (stable names), then the same kind, staticness and signature in
//     declaration order; a member that leads to an obfuscated type maps that type too (path propagation).
// Metadata only: no game code runs. A remapped binding gets the status "remapped" and keeps the old binding in its
// note — it needs the in-game check (WoG Debug selftest) before it becomes "verified" again.
static class SymbolRemap
{
    const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                  BindingFlags.Static | BindingFlags.DeclaredOnly;
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    static readonly Regex ObfTypeName = new("^[a-z]{1,4}$");
    static readonly Regex ObfMemberName = new("^[_<]?[a-z]{2,4}[_>]?(k__BackingField)?$");

    public static int Run(string oldDir, string newDir, string json, string? outPath, string version)
    {
        using var oldSide = new Side(oldDir, true);
        using var newSide = new Side(newDir, false);
        var m = new Matcher(oldSide, newSide);
        Console.WriteLine($"old: {oldSide.Types.Count} types, new: {newSide.Types.Count} types");
        m.Refine();
        Console.WriteLine($"structure: {m.MappedCount} obfuscated types mapped by unique features");

        var doc = JsonNode.Parse(File.ReadAllText(json))!;
        var bindings = doc["Bindings"]!.AsObject();
        var paths = bindings.Select(kv => ((string?)kv.Value!["Type"] ?? "", (string?)kv.Value!["Member"] ?? "")).Where(x => x.Item1.Length > 0).ToList();
        m.PremapClosure(paths);
        // a warm-up pass: the types one binding maps make another binding's member signatures exact
        int before = m.MappedCount;
        foreach (var (type, member) in paths) m.Remap(type, member, out _);
        Console.WriteLine($"warm-up over the bindings: +{m.MappedCount - before} types");
        int same = 0, remapped = 0, failed = 0;
        var report = new StringBuilder();
        foreach (var (key, node) in bindings.ToList())
        {
            var b = node!.AsObject();
            string type = (string?)b["Type"] ?? "", member = (string?)b["Member"] ?? "";
            string status = (string?)b["Status"] ?? "";
            if (type.Length == 0) continue;
            if (newSide.Resolve(type, member, out _) is null && m.SameShape(type, member))
            {
                same++;
                report.AppendLine($"same      {key,-28} {type}.{member}");
                continue;
            }
            var result = m.Remap(type, member, out string why);
            if (result is null)
            {
                failed++;
                report.AppendLine($"FAILED    {key,-28} {type}.{member} — {why}");
                b["Status"] = "broken";
                b["Note"] = $"{b["Note"]} | {version}: no match for {type}.{member} ({why})";
                continue;
            }
            if (result.Value.Type == type && result.Value.Member == member)
            {
                same++;
                report.AppendLine($"same      {key,-28} {type}.{member}  ({why})");
                continue;
            }
            remapped++;
            report.AppendLine($"remapped  {key,-28} {type}.{member} → {result.Value.Type}.{result.Value.Member}  ({why})");
            b["Type"] = result.Value.Type;
            b["Member"] = result.Value.Member;
            b["Status"] = "remapped";
            b["Note"] = $"{b["Note"]} | {version}: remapped from {type}.{member} by structure ({why}); was {status}, check in game";
        }
        Console.Write(report);
        Console.WriteLine($"{same} unchanged, {remapped} remapped, {failed} failed");
        if (outPath != null)
        {
            File.WriteAllText(outPath, doc.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }) + "\n");
            Console.WriteLine("written " + outPath);
        }
        return failed == 0 ? 0 : 2;
    }

    /// <summary>
    /// find-member &lt;interop&gt; &lt;regex&gt; [--type &lt;regex&gt;]: every member of Hex whose name matches, with its type and
    /// signature — for the manual part of the RE after an update (which new type has LevelUp, Init(HeroSkill, Hero)…).
    /// </summary>
    public static int FindMember(string dir, string nameRegex, string? typeRegex)
    {
        using var side = new Side(dir, false);
        var rx = new Regex(nameRegex);
        var tx = typeRegex is null ? null : new Regex(typeRegex);
        foreach (var t in side.Types.OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            if (tx != null && !tx.IsMatch(t.FullName ?? "")) continue;
            MemberInfo[] ms;
            try { ms = t.GetMembers(Declared); } catch { continue; }
            foreach (var mm in ms)
            {
                if (!rx.IsMatch(mm.Name) || mm.Name.StartsWith("NativeMethodInfoPtr_") || mm.Name.StartsWith("NativeFieldInfoPtr_")) continue;
                string sig = mm switch
                {
                    FieldInfo f => "field " + f.FieldType,
                    PropertyInfo p => "prop " + p.PropertyType,
                    MethodBase mb => "method (" + string.Join(", ", mb.GetParameters().Select(p => p.ParameterType.Name)) + ")" +
                                     (mb is MethodInfo mi ? " → " + mi.ReturnType.Name : ""),
                    _ => mm.MemberType.ToString(),
                };
                Console.WriteLine($"{t.FullName}.{mm.Name}  {sig}");
            }
        }
        return 0;
    }

    internal static bool IsObf(Type t) => ObfTypeName.IsMatch(StripArity(t.Name));
    static string StripArity(string n) => n.IndexOf('`') is var i and >= 0 ? n[..i] : n;

    /// <summary>The interop assemblies of one game version, metadata only.</summary>
    internal sealed class Side : IDisposable
    {
        readonly MetadataLoadContext ctx;
        public readonly bool IsOld;
        public readonly Assembly Hex;
        public readonly List<Type> Types;
        public readonly Dictionary<string, Type> ByName = new();

        public Side(string dir, bool isOld)
        {
            IsOld = isOld;
            this.dir = dir;
            // the interop assemblies refer to Il2CppInterop.Runtime and friends: BepInEx/core next to the interop
            // folder, or the installed game's (OLDEN_ERA_DIR)
            var core = new[] { Path.Combine(dir, "..", "core"),
                               Path.Combine(Environment.GetEnvironmentVariable("OLDEN_ERA_DIR") ?? "", "BepInEx", "core") }
                .FirstOrDefault(d => File.Exists(Path.Combine(d, "Il2CppInterop.Runtime.dll")));
            var resolver = new PathAssemblyResolver(Directory.GetFiles(dir, "*.dll")
                .Concat(core is null ? Array.Empty<string>() : Directory.GetFiles(core, "*.dll").Where(f => !File.Exists(Path.Combine(dir, Path.GetFileName(f)))))
                .Concat(Directory.GetFiles(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "*.dll")));
            ctx = new MetadataLoadContext(resolver);
            Hex = ctx.LoadFromAssemblyPath(Path.Combine(dir, "Hex.dll"));
            Type[] types;
            try { types = Hex.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray()!; }
            Types = types.ToList();
            foreach (var t in Types) ByName[t.FullName ?? t.Name] = t;
            if (File.Exists(Path.Combine(dir, "MethodXrefScanCache.db")) && File.Exists(Path.Combine(dir, "MethodAddressToToken.db")))
                Calls = new Xrefs(this, dir);
        }

        public Type? Find(string fullName) => ByName.TryGetValue(fullName, out var t) ? t : null;

        public Xrefs? Calls;
        readonly Dictionary<string, Assembly?> loaded = new();
        readonly string dir;

        /// <summary>An interop assembly of this side by simple name (null when it is not there).</summary>
        public Assembly? Load(string simpleName)
        {
            if (simpleName == "Hex") return Hex;
            if (loaded.TryGetValue(simpleName, out var a)) return a;
            string p = Path.Combine(dir, simpleName + ".dll");
            try { a = File.Exists(p) ? ctx.LoadFromAssemblyPath(p) : null; } catch { a = null; }
            return loaded[simpleName] = a;
        }

        /// <summary>The plugin's resolution (OldenEraSymbols.Resolve): null when the path resolves, else the error.</summary>
        public string? Resolve(string type, string member, out List<MemberInfo> path)
        {
            path = new List<MemberInfo>();
            var current = Find(type);
            if (current is null) return $"type {type} not found";
            foreach (var name in member.Length == 0 ? Array.Empty<string>() : member.Split('.'))
            {
                MemberInfo? m;
                try { m = current.GetMember(name, All).FirstOrDefault(x => x is FieldInfo or PropertyInfo or MethodInfo); }
                catch (Exception e) { return $"{current.FullName}.{name}: {e.GetType().Name}"; }
                if (m is null) return $"member {current.FullName}.{name} not found";
                path.Add(m);
                if (m is MethodInfo) break;
                current = m is FieldInfo f ? f.FieldType : ((PropertyInfo)m).PropertyType;
            }
            return null;
        }

        public void Dispose() => ctx.Dispose();
    }

    /// <summary>
    /// The call graph Il2CppInterop cached when it generated the interop assemblies: MethodXrefScanCache.db (per method
    /// a range of calls it makes and a range of callers, as code addresses) and MethodAddressToToken.db (code address
    /// → method token of an interop assembly). The ranges come from each method's [CachedScanResults] attribute.
    /// Formats: Il2CppInterop.Common.Maps (MethodXrefScanCache, MethodAddressToTokenMapBase).
    /// </summary>
    internal sealed class Xrefs
    {
        const int XrefMagic = 1129860437, TokenMagic = 1297370453;
        readonly Side side;
        readonly byte[] cache;
        readonly long[] addresses;
        readonly int[] values; // token, assembly index
        readonly List<string> assemblies = new();
        readonly Dictionary<string, Dictionary<int, MethodBase>> byToken = new();

        public Xrefs(Side side, string dir)
        {
            this.side = side;
            cache = File.ReadAllBytes(Path.Combine(dir, "MethodXrefScanCache.db"));
            if (BitConverter.ToInt32(cache, 0) != XrefMagic) throw new InvalidDataException("MethodXrefScanCache.db: bad magic");
            var map = File.ReadAllBytes(Path.Combine(dir, "MethodAddressToToken.db"));
            if (BitConverter.ToInt32(map, 0) != TokenMagic) throw new InvalidDataException("MethodAddressToToken.db: bad magic");
            int numAssemblies = BitConverter.ToInt32(map, 8), numMethods = BitConverter.ToInt32(map, 12), dataOffset = BitConverter.ToInt32(map, 16);
            using (var r = new BinaryReader(new MemoryStream(map, 20, map.Length - 20)))
                for (int i = 0; i < numAssemblies; i++) assemblies.Add(r.ReadString().Split(',')[0]);
            addresses = new long[numMethods];
            values = new int[numMethods * 2];
            Buffer.BlockCopy(map, dataOffset, addresses, 0, numMethods * 8);
            Buffer.BlockCopy(map, dataOffset + numMethods * 8, values, 0, numMethods * 8);
        }

        MethodBase? Lookup(long rva)
        {
            int i = Array.BinarySearch(addresses, rva);
            if (i < 0) return null;
            string asm = assemblies[values[i * 2 + 1]];
            if (!byToken.TryGetValue(asm, out var tokens))
            {
                byToken[asm] = tokens = new Dictionary<int, MethodBase>();
                Type[] types = Array.Empty<Type>();
                if (side.Load(asm) is { } a)
                    try { types = a.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray()!; }
                foreach (var t in types)
                {
                    MethodBase[] ms;
                    try { ms = t.GetMethods(Declared).Cast<MethodBase>().Concat(t.GetConstructors(Declared)).ToArray(); } catch { continue; }
                    foreach (var m in ms) tokens[m.MetadataToken] = m;
                }
            }
            return tokens.TryGetValue(values[i * 2], out var mb) ? mb : null;
        }

        readonly Dictionary<(MethodBase, bool), List<MethodBase>> edges = new();

        /// <summary>Methods this one calls (callees = true) or that call it, as interop methods.</summary>
        public List<MethodBase> Edges(MethodBase m, bool callees)
        {
            if (edges.TryGetValue((m, callees), out var known)) return known;
            var list = new List<MethodBase>();
            edges[(m, callees)] = list;
            CustomAttributeData? a;
            try { a = m.GetCustomAttributesData().FirstOrDefault(x => x.AttributeType.Name == "CachedScanResultsAttribute"); }
            catch { return list; }
            if (a is null) return list;
            int Arg(string n) => a.NamedArguments.Where(x => x.MemberName == n).Select(x => Convert.ToInt32(x.TypedValue.Value)).FirstOrDefault();
            int from = Arg(callees ? "XrefRangeStart" : "RefRangeStart"), to = Arg(callees ? "XrefRangeEnd" : "RefRangeEnd");
            for (int i = from; i < to; i++)
            {
                int at = 16 + i * 24; // header (16 bytes); MethodData { long Address; long FoundAt; XrefType Type } = 24 bytes
                if (at + 24 > cache.Length) break;
                if (BitConverter.ToInt32(cache, at + 16) != 1) continue; // XrefType.Method
                if (Lookup(BitConverter.ToInt64(cache, at)) is { } target) list.Add(target);
            }
            return list;
        }
    }

    internal sealed class Matcher
    {
        readonly Side oldS, newS;
        readonly Dictionary<string, string> map = new();     // old full name → new full name (obfuscated types)
        readonly Dictionary<string, string> reverse = new(); // new → old
        readonly HashSet<string> stableNames = new();
        readonly Dictionary<string, double> scores = new();  // old full name → match score (1 = unique features)

        public Matcher(Side o, Side n)
        {
            oldS = o; newS = n;
            // A member name is stable when a non-obfuscated type has a member of that name in both versions.
            foreach (var t in oldS.Types.Where(t => !IsObf(t) && !IsObfNested(t)))
            {
                var nt = newS.Find(t.FullName ?? "");
                if (nt is null) continue;
                var newNames = new HashSet<string>(Members(nt).Select(x => x.Name));
                foreach (var mm in Members(t)) if (newNames.Contains(mm.Name)) stableNames.Add(mm.Name);
            }
        }

        public int MappedCount => map.Count;

        bool IsObfNested(Type t) => t.DeclaringType is { } d && (IsObf(d) || IsObfNested(d));

        /// <summary>
        /// A type whose name does not carry over to the other version: obfuscated, nested in an obfuscated type, or a
        /// readable name that exists on one side only (0.82.07 gave some obfuscated classes their real names:
        /// eah → Hex.Session.Logic.HeroSkill).
        /// </summary>
        bool Movable(Type t, bool isOld)
        {
            if (IsObf(t) || IsObfNested(t)) return true;
            if (t.Assembly != (isOld ? oldS.Hex : newS.Hex)) return false;
            string n = t.FullName ?? "";
            return n.Length > 0 && (isOld ? newS.Find(n) is null : oldS.Find(n) is null);
        }
        bool StableName(string name) => !ObfMemberName.IsMatch(name) || stableNames.Contains(name);

        static IEnumerable<MemberInfo> Members(Type t)
        {
            MemberInfo[] ms;
            try { ms = t.GetMembers(Declared); } catch { yield break; }
            foreach (var m in ms)
            {
                if (m is FieldInfo f && (f.Name.StartsWith("NativeFieldInfoPtr_") || f.Name.StartsWith("NativeMethodInfoPtr_") ||
                                         f.Name.StartsWith("NativeMethod") || f.Name.StartsWith("Il2Cpp"))) continue;
                if (m is MethodInfo mi && mi.IsSpecialName) continue;
                if (m is FieldInfo or PropertyInfo or MethodInfo or ConstructorInfo) yield return m;
            }
        }

        /// <summary>Type name with obfuscated types replaced by their mapped new name ("#x") or "?".</summary>
        string Norm(Type t, bool isOld)
        {
            if (t.IsByRef) return Norm(t.GetElementType()!, isOld) + "&";
            if (t.IsPointer) return Norm(t.GetElementType()!, isOld) + "*";
            if (t.IsArray) return Norm(t.GetElementType()!, isOld) + "[]";
            if (t.IsGenericParameter) return "!" + t.GenericParameterPosition;
            if (t.IsConstructedGenericType)
                return Norm(t.GetGenericTypeDefinition(), isOld) + "<" +
                       string.Join(",", t.GetGenericArguments().Select(a => Norm(a, isOld))) + ">";
            string name = t.FullName ?? t.Name;
            if (t.DeclaringType is { } d && !IsObf(t)) return Norm(d, isOld) + "+" + t.Name;
            if (!Movable(t, isOld)) return name;
            if (isOld) return map.TryGetValue(name, out var n) ? "#" + n : "?";
            return reverse.ContainsKey(name) ? "#" + name : "?";
        }

        string Sig(MemberInfo m, bool isOld) => m switch
        {
            FieldInfo f => $"F{(f.IsStatic ? "s" : "")}:{Norm(f.FieldType, isOld)}",
            PropertyInfo p => $"P{((p.GetMethod ?? p.SetMethod)?.IsStatic == true ? "s" : "")}:{Norm(p.PropertyType, isOld)}",
            MethodInfo mi => $"M{(mi.IsStatic ? "s" : "")}{(mi.IsGenericMethodDefinition ? "`" + mi.GetGenericArguments().Length : "")}:" +
                             $"{Norm(mi.ReturnType, isOld)}({string.Join(",", SafeParams(mi).Select(p => Norm(p.ParameterType, isOld)))})",
            ConstructorInfo c => $"C({string.Join(",", SafeParams(c).Select(p => Norm(p.ParameterType, isOld)))})",
            _ => "",
        };

        static ParameterInfo[] SafeParams(MethodBase m)
        {
            try { return m.GetParameters(); } catch { return Array.Empty<ParameterInfo>(); }
        }

        static string Kind(Type t) =>
            (t.IsEnum ? "enum" : t.IsValueType ? "struct" : t.IsInterface ? "iface" : "class") +
            (t.IsAbstract ? "A" : "") + (t.IsSealed ? "S" : "") + (t.IsGenericTypeDefinition ? "`" + t.GetGenericArguments().Length : "");

        readonly Dictionary<MethodBase, MethodBase> methodMap = new();
        readonly HashSet<MethodBase> methodReverse = new();

        /// <summary>A called or calling method as a feature: its type and its stable name, mapped name or signature.</summary>
        string Describe(MethodBase t, bool isOld)
        {
            string decl = t.DeclaringType is { } d ? Norm(d, isOld) : "";
            string name = StableName(t.Name) ? t.Name
                : isOld && methodMap.TryGetValue(t, out var n) ? "#" + n.Name
                : !isOld && methodReverse.Contains(t) ? "#" + t.Name
                : Sig(t, isOld);
            return decl + "." + name;
        }

        /// <summary>Who the method calls ("c:") and who calls it ("u:"), from the interop call graph.</summary>
        List<string> CallFeatures(MethodBase m, bool isOld)
        {
            var side = isOld ? oldS : newS;
            var f = new List<string>();
            if (side.Calls is null) return f;
            foreach (var c in side.Calls.Edges(m, true)) f.Add("c:" + Describe(c, isOld));
            foreach (var c in side.Calls.Edges(m, false)) f.Add("u:" + Describe(c, isOld));
            return f;
        }

        /// <summary>The calls a type's methods make to stable code (for scoring types whose shape changed).</summary>
        List<string> TypeCallFeatures(Type t, bool isOld)
        {
            var f = new List<string>();
            var side = isOld ? oldS : newS;
            if (side.Calls is null) return f;
            var methods = Members(t).OfType<MethodBase>().ToList();
            var real = RealNames(methods.OfType<MethodInfo>());
            foreach (var m in methods.Where(m => !IsDecoy(m, real)))
                foreach (var c in side.Calls.Edges(m, true))
                    if (c.DeclaringType is { } d && !Movable(d, isOld)) f.Add("tc:" + Describe(c, isOld));
            return f;
        }

        /// <summary>
        /// Maps every obfuscated type the bindings walk through, and the obfuscated types the members of those types
        /// use, so that member signatures compare exactly. Batches: all open new types are scored once per pass; a pair
        /// is taken when it clearly beats the runner-up; each pass sharpens the next one.
        /// </summary>
        public void PremapClosure(IEnumerable<(string Type, string Member)> bindings)
        {
            var need = new HashSet<Type>();
            void Want(Type? t)
            {
                if (t is null) return;
                while (t.HasElementType) t = t.GetElementType()!;
                if (t.IsConstructedGenericType) { foreach (var a in t.GetGenericArguments()) Want(a); Want(t.GetGenericTypeDefinition()); return; }
                if (t.IsGenericParameter) return;
                if (Movable(t, true) && t.Assembly == oldS.Hex) need.Add(t);
                if (t.DeclaringType != null) Want(t.DeclaringType);
            }
            foreach (var (type, member) in bindings)
            {
                if (oldS.Resolve(type, member, out var path) != null) continue;
                var root = oldS.Find(type)!;
                Want(root);
                var decls = new List<Type> { root };
                foreach (var m in path) { if (m.DeclaringType != null) decls.Add(m.DeclaringType); Want(m.DeclaringType); if (m is not MethodInfo) Want(MemberType(m)); }
                foreach (var d in decls)
                    foreach (var mm in Members(d))
                        switch (mm)
                        {
                            case FieldInfo f: Want(f.FieldType); break;
                            case PropertyInfo p: Want(p.PropertyType); break;
                            case MethodBase mb:
                                if (mb is MethodInfo mi) Want(mi.ReturnType);
                                foreach (var p in SafeParams(mb)) Want(p.ParameterType);
                                break;
                        }
            }
            Console.WriteLine($"closure: {need.Count} obfuscated types on the binding paths, {need.Count(t => !map.ContainsKey(t.FullName!))} open");
            for (int pass = 0; pass < 6; pass++)
            {
                var open = need.Where(t => !map.ContainsKey(t.FullName!)).ToList();
                if (open.Count == 0) break;
                var (oldRefs, newRefs) = CachedReferences();
                var kinds = new HashSet<string>(open.Select(Kind));
                var cands = newS.Types.Where(t => Movable(t, false) && !reverse.ContainsKey(t.FullName ?? "") && kinds.Contains(Kind(t)))
                    .Select(t => (t, kind: Kind(t), f: Bag(Features(t, false, newRefs).Concat(TypeCallFeatures(t, false))))).ToList();
                // inverted index over the features that few candidates share: only those candidates get scored
                var df = new Dictionary<string, int>();
                foreach (var c in cands) foreach (var k in c.f.Keys) df[k] = df.TryGetValue(k, out int n) ? n + 1 : 1;
                var index = new Dictionary<string, List<int>>();
                for (int i = 0; i < cands.Count; i++)
                    foreach (var k in cands[i].f.Keys)
                        if (df[k] <= 40) { if (!index.TryGetValue(k, out var l)) index[k] = l = new List<int>(); l.Add(i); }
                var proposals = new List<(Type o, Type n, double s, double next)>();
                foreach (var o in open)
                {
                    var fo = Bag(Features(o, true, oldRefs).Concat(TypeCallFeatures(o, true)));
                    string kind = Kind(o);
                    var pool = new HashSet<int>();
                    foreach (var k in fo.Keys) if (index.TryGetValue(k, out var l)) pool.UnionWith(l);
                    var ranked = pool.Where(i => cands[i].kind == kind).Select(i => (t: cands[i].t, s: Jaccard(fo, cands[i].f)))
                        .OrderByDescending(x => x.s).Take(2).ToList();
                    if (ranked.Count == 0) continue;
                    double second = ranked.Count > 1 ? ranked[1].s : 0;
                    if (ranked[0].s >= 0.45 && ranked[0].s - second >= 0.08) proposals.Add((o, ranked[0].t, ranked[0].s, second));
                }
                int added = 0;
                foreach (var p in proposals.OrderByDescending(p => p.s - p.next))
                {
                    if (map.ContainsKey(p.o.FullName!) || reverse.ContainsKey(p.n.FullName!)) continue;
                    Pair(p.o.FullName!, p.n.FullName!, p.s); added++;
                }
                Console.WriteLine($"  closure pass {pass}: +{added} of {open.Count} open");
                if (added == 0)
                {
                    foreach (var o in open.Take(40)) Console.WriteLine($"    open: {o.FullName} ({Kind(o)}, {Members(o).Count()} members)");
                    break;
                }
            }
        }

        List<string> Features(Type t, bool isOld, Dictionary<string, List<string>> refs)
        {
            var f = new List<string> { "k:" + Kind(t) };
            try { if (t.BaseType != null) f.Add("b:" + Norm(t.BaseType, isOld)); } catch { }
            try { foreach (var i in t.GetInterfaces()) f.Add("i:" + Norm(i, isOld)); } catch { }
            if (t.DeclaringType != null) f.Add("d:" + Norm(t.DeclaringType, isOld));
            var members = Members(t).ToList();
            var real = RealNames(members.OfType<MethodInfo>());
            foreach (var mm in members)
            {
                if (IsDecoy(mm, real)) continue;
                // the interop constructor every class gets
                if (mm is ConstructorInfo c && SafeParams(c) is { Length: 1 } ps && ps[0].ParameterType.Name == "IntPtr") continue;
                string s = Sig(mm, isOld);
                f.Add(s);
                if (StableName(mm.Name)) f.Add(mm.Name + "=" + s);
                if (t.IsEnum && mm is FieldInfo ef && ef.IsStatic)
                    try { f.Add("v:" + ef.GetRawConstantValue()); } catch { }
            }
            if (refs.TryGetValue(t.FullName ?? "", out var r)) f.AddRange(r);
            return f;
        }

        /// <summary>Who refers to each type: "r:declaring type.stable member name:kind" (empty name when obfuscated).</summary>
        Dictionary<string, List<string>> References(Side side)
        {
            var refs = new Dictionary<string, List<string>>();
            void Add(Type? target, string feature)
            {
                if (target is null) return;
                while (target.HasElementType) target = target.GetElementType()!;
                if (target.IsConstructedGenericType)
                {
                    foreach (var a in target.GetGenericArguments()) Add(a, feature + "<>");
                    return;
                }
                if (target.IsGenericParameter || !Movable(target, side.IsOld)) return;
                string k = target.FullName ?? "";
                if (!refs.TryGetValue(k, out var l)) refs[k] = l = new List<string>();
                l.Add(feature);
            }
            foreach (var t in side.Types)
            {
                string owner = Norm(t, side.IsOld);
                foreach (var mm in Members(t))
                {
                    string n = StableName(mm.Name) ? mm.Name : "";
                    switch (mm)
                    {
                        case FieldInfo f: Add(f.FieldType, $"r:{owner}.{n}:F"); break;
                        case PropertyInfo p: Add(p.PropertyType, $"r:{owner}.{n}:P"); break;
                        case MethodBase mb:
                            if (mb is MethodInfo mi) Add(mi.ReturnType, $"r:{owner}.{n}:R");
                            foreach (var p in SafeParams(mb)) Add(p.ParameterType, $"r:{owner}.{n}:A");
                            break;
                    }
                }
            }
            return refs;
        }

        /// <summary>Refinement rounds: a feature set unique among the open types on both sides maps the pair.</summary>
        public void Refine()
        {
            for (int round = 0; round < 8; round++)
            {
                var oldRefs = References(oldS);
                var newRefs = References(newS);
                var oldBuckets = Buckets(oldS, oldRefs, map.Keys);
                var newBuckets = Buckets(newS, newRefs, reverse.Keys);
                int added = 0;
                foreach (var (key, olds) in oldBuckets)
                {
                    if (olds.Count != 1 || !newBuckets.TryGetValue(key, out var news) || news.Count != 1) continue;
                    string o = olds[0].FullName!, n = news[0].FullName!;
                    if (map.ContainsKey(o) || reverse.ContainsKey(n)) continue;
                    map[o] = n; reverse[n] = o; scores[o] = 1; added++;
                }
                Console.WriteLine($"  round {round}: +{added} types");
                if (added == 0) break;
            }
        }

        Dictionary<string, List<Type>> Buckets(Side side, Dictionary<string, List<string>> refs, IEnumerable<string> done)
        {
            var skip = new HashSet<string>(done);
            var b = new Dictionary<string, List<Type>>();
            foreach (var t in side.Types)
            {
                if (!Movable(t, side.IsOld) || skip.Contains(t.FullName ?? "")) continue;
                var f = Features(t, side.IsOld, refs);
                f.Sort(StringComparer.Ordinal);
                string key = string.Join("\n", f);
                if (!b.TryGetValue(key, out var l)) b[key] = l = new List<Type>();
                l.Add(t);
            }
            return b;
        }

        static Dictionary<string, int> Bag(IEnumerable<string> features)
        {
            var d = new Dictionary<string, int>();
            foreach (var f in features) d[f] = d.TryGetValue(f, out int n) ? n + 1 : 1;
            return d;
        }

        static double Jaccard(List<string> a, List<string> b) => Jaccard(Bag(a), Bag(b));

        /// <summary>Weighted Jaccard of two feature multisets: Σ min / Σ max of the counts.</summary>
        static double Jaccard(Dictionary<string, int> a, Dictionary<string, int> b)
        {
            double inter = 0, sa = 0, sb = 0;
            foreach (var (k, x) in a) { sa += x; if (b.TryGetValue(k, out int y)) inter += Math.Min(x, y); }
            foreach (var y in b.Values) sb += y;
            double union = sa + sb - inter;
            return union == 0 ? 0 : inter / union;
        }

        /// <summary>The new type for an old one: itself when not obfuscated, the refined map, else the best score.</summary>
        public Type? MapType(Type oldType, out string why)
        {
            why = "";
            if (oldType.IsConstructedGenericType || oldType.HasElementType) { why = "constructed type"; return null; }
            string name = oldType.FullName ?? "";
            if (!Movable(oldType, true))
            {
                var same = newS.Find(name);
                why = same is null ? $"stable type {name} is gone" : "stable";
                return same;
            }
            if (map.TryGetValue(name, out var mapped)) { why = $"type {name}→{mapped} {scores[name]:0.00}"; return newS.Find(mapped); }
            var (oldRefs, newRefs) = CachedReferences();
            var fo = Features(oldType, true, oldRefs);
            var ranked = newS.Types
                .Where(t => Movable(t, false) && !reverse.ContainsKey(t.FullName ?? "") && Kind(t) == Kind(oldType))
                .Select(t => (t, s: Jaccard(fo, Features(t, false, newRefs))))
                .OrderByDescending(x => x.s).Take(2).ToList();
            if (ranked.Count == 0) { why = $"no open new type of kind {Kind(oldType)}"; return null; }
            double best = ranked[0].s, second = ranked.Count > 1 ? ranked[1].s : 0;
            if (best < 0.5 || best - second < 0.08)
            {
                why = $"type {name}: best {ranked[0].t.FullName} {best:0.00}, next {(ranked.Count > 1 ? ranked[1].t.FullName : "-")} {second:0.00}";
                return null;
            }
            Pair(name, ranked[0].t.FullName!, best);
            why = $"type {name}→{ranked[0].t.FullName} {best:0.00} (next {second:0.00})";
            return ranked[0].t;
        }

        (Dictionary<string, List<string>>, Dictionary<string, List<string>>)? refsCache;
        int refsCacheAt = -1;

        /// <summary>The reference features of both sides; recomputed only after the type map grew.</summary>
        (Dictionary<string, List<string>> Old, Dictionary<string, List<string>> New) CachedReferences()
        {
            if (refsCache is null || refsCacheAt != map.Count)
            {
                refsCache = (References(oldS), References(newS));
                refsCacheAt = map.Count;
            }
            return refsCache.Value;
        }

        void Pair(string o, string n, double score)
        {
            map[o] = n; reverse[n] = o; scores[o] = score;
        }

        /// <summary>True when the binding resolves in both versions to members of the same name and signature.</summary>
        public bool SameShape(string type, string member)
        {
            if (oldS.Resolve(type, member, out var op) is not null || newS.Resolve(type, member, out var np) is not null) return false;
            if (op.Count != np.Count) return false;
            var ot = oldS.Find(type)!;
            if (Movable(ot, true)) return false; // an obfuscated type name that still resolves is a coincidence, not the same type
            for (int i = 0; i < op.Count; i++)
                if (op[i].Name != np[i].Name || Loose(op[i]) != Loose(np[i]) || (Movable(op[i].DeclaringType!, true) && !StableName(op[i].Name)))
                    return false;
            return true;
        }

        string Loose(MemberInfo m) => Regex.Replace(Sig(m, true), @"#[^,()<>\[\]&]+", "?");

        /// <summary>The binding's path in the new version, or null with the reason.</summary>
        public (string Type, string Member)? Remap(string type, string member, out string why)
        {
            var err = oldS.Resolve(type, member, out var path);
            if (err != null) { why = "old: " + err; return null; }
            var oldRoot = oldS.Find(type)!;
            var newRoot = MapType(oldRoot, out var tw);
            var notes = new List<string>();
            if (newRoot is null) { why = tw; return null; }
            if (tw != "stable") notes.Add(tw);
            var segs = new List<string>();
            Type oldCur = oldRoot, newCur = newRoot;
            foreach (var om in path)
            {
                var nm = MapMember(oldCur, newCur, om, out var mw);
                if (nm is null) { why = string.Join("; ", notes.Append(mw)); return null; }
                if (mw.Length > 0) notes.Add(mw);
                segs.Add(nm.Name);
                if (om is MethodInfo) break;
                oldCur = MemberType(om); newCur = MemberType(nm);
                // path propagation: the member's obfuscated type is the old one's counterpart
                if (Movable(oldCur, true) && !map.ContainsKey(oldCur.FullName ?? "") && Movable(newCur, false) && !reverse.ContainsKey(newCur.FullName ?? ""))
                    Pair(oldCur.FullName!, newCur.FullName!, 0.9);
            }
            why = notes.Count == 0 ? "names unchanged" : string.Join("; ", notes);
            return (newRoot.FullName!, string.Join(".", segs));
        }

        /// <summary>The movable type inside a member type (element of an array or list), or the type itself.</summary>
        Type? Unwrap(Type t, bool isOld)
        {
            while (t.HasElementType) t = t.GetElementType()!;
            if (t.IsConstructedGenericType)
                return t.GetGenericArguments().Select(a => Unwrap(a, isOld)).FirstOrDefault(x => x != null && Movable(x, isOld));
            return t.IsGenericParameter ? null : t;
        }

        static readonly Regex ObfLetters = new("^[a-z]{2,5}$");

        /// <summary>Obfuscated names as numbers in the obfuscator's sequence (a, b, …, z, aa, ab, …).</summary>
        static long NameNumber(string n)
        {
            long v = 0;
            foreach (char c in n) v = v * 26 + (c - 'a' + 1);
            return v;
        }

        /// <summary>The members whose obfuscated name has a neighbour in the sequence: the class's own members.</summary>
        static HashSet<string> RealNames(IEnumerable<MemberInfo> members)
        {
            var names = members.Select(m => m.Name).Where(n => ObfLetters.IsMatch(n)).Distinct().ToList();
            var nums = new HashSet<long>(names.Select(NameNumber));
            return new HashSet<string>(names.Where(n => nums.Contains(NameNumber(n) - 1) || nums.Contains(NameNumber(n) + 1)));
        }

        /// <summary>
        /// The partner of <paramref name="om"/> in a longest common subsequence of the two declaration orders (by key),
        /// or null when it is not on it.
        /// </summary>
        static MemberInfo? Align(List<MemberInfo> olds, List<MemberInfo> news, MemberInfo om,
                                 Func<MemberInfo, string> oldKey, Func<MemberInfo, string> newKey)
        {
            var o = olds.OrderBy(x => NameNumber(x.Name)).ToList();
            var n = news.OrderBy(x => NameNumber(x.Name)).ToList();
            var ok = o.Select(oldKey).ToArray();
            var nk = n.Select(newKey).ToArray();
            var dp = new int[o.Count + 1, n.Count + 1];
            for (int i = o.Count - 1; i >= 0; i--)
                for (int j = n.Count - 1; j >= 0; j--)
                    dp[i, j] = ok[i] == nk[j] ? dp[i + 1, j + 1] + 1 : Math.Max(dp[i + 1, j], dp[i, j + 1]);
            for (int i = 0, j = 0; i < o.Count && j < n.Count;)
            {
                if (ok[i] == nk[j] && dp[i, j] == dp[i + 1, j + 1] + 1)
                {
                    if (o[i] == om) return n[j];
                    i++; j++;
                }
                else if (dp[i + 1, j] >= dp[i, j + 1]) { if (o[i] == om) return null; i++; }
                else j++;
            }
            return null;
        }

        /// <summary>A decoy method: an obfuscated name outside the class's consecutive run of real names.</summary>
        bool IsDecoy(MemberInfo m, HashSet<string> real) =>
            m is MethodInfo && ObfLetters.IsMatch(m.Name) && !real.Contains(m.Name) && !stableNames.Contains(m.Name);

        MemberInfo Mapped(MemberInfo o, MemberInfo n)
        {
            if (o is MethodBase om && n is MethodBase nm) { methodMap[om] = nm; methodReverse.Add(nm); }
            return n;
        }

        static Type MemberType(MemberInfo m) => m switch
        {
            FieldInfo f => f.FieldType,
            PropertyInfo p => p.PropertyType,
            MethodInfo mi => mi.ReturnType,
            _ => throw new InvalidOperationException(m.Name),
        };

        MemberInfo? MapMember(Type oldCur, Type newCur, MemberInfo om, out string why)
        {
            why = "";
            // the member may be inherited: look it up on the counterpart of the type that declares it
            var oldDecl = om.DeclaringType!;
            Type? newDecl = oldDecl == oldCur ? newCur : MapType(oldDecl, out _);
            if (newDecl is null) { why = $"declaring type {oldDecl.FullName} not mapped"; return null; }
            string oldSig = Sig(om, true);
            var newMembers = Members(newDecl).Where(x => x.MemberType == om.MemberType).ToList();
            var byName = newMembers.FirstOrDefault(x => x.Name == om.Name && Sig(x, false) == oldSig);
            if (byName != null && (StableName(om.Name) || !Movable(oldDecl, true))) return byName;
            // same signature, in declaration order among the members that do not keep a stable name
            var oldMembers = Members(oldDecl).Where(x => x.MemberType == om.MemberType).ToList();
            var oldStable = new HashSet<string>(oldMembers.Where(x => StableName(x.Name) && newMembers.Any(n => n.Name == x.Name)).Select(x => x.Name));
            var oldOpen = oldMembers.Where(x => !oldStable.Contains(x.Name)).ToList();
            var newOpen = newMembers.Where(x => !oldStable.Contains(x.Name)).ToList();
            // the obfuscator names a class's own members with consecutive names (bmbi…bmdp) and adds decoy methods
            // with random short names (sg, jpn, oka): a real member maps among the real ones only
            var oldReal = RealNames(oldMembers);
            if (oldReal.Contains(om.Name))
            {
                var newReal = RealNames(newMembers);
                oldOpen = oldOpen.Where(x => oldReal.Contains(x.Name)).ToList();
                newOpen = newOpen.Where(x => newReal.Contains(x.Name)).ToList();
            }
            foreach (var strict in new[] { true, false })
            {
                string Key(MemberInfo x, bool isOld) => strict ? Sig(x, isOld) : Regex.Replace(Sig(x, isOld), @"#[^,()<>\[\]&]+|\?", "?");
                string k = Key(om, true);
                var os = oldOpen.Where(x => Key(x, true) == k).ToList();
                var ns = newOpen.Where(x => Key(x, false) == k).ToList();
                if (ns.Count == 0) continue;
                int idx = os.IndexOf(om);
                string what = strict ? "signature" : "shape";
                if (os.Count == 1 && ns.Count == 1)
                {
                    why = $"{om.Name}→{ns[0].Name} unique {what}";
                    return Mapped(om, ns[0]);
                }
                // a field or property of an obfuscated type nobody mapped yet: the candidates' types are few, so the
                // old type is scored against just those
                if (om is FieldInfo or PropertyInfo && Unwrap(MemberType(om), true) is { } ot && Movable(ot, true) && !map.ContainsKey(ot.FullName ?? ""))
                {
                    var (oldRefs, newRefs) = CachedReferences();
                    var fo = Bag(Features(ot, true, oldRefs).Concat(TypeCallFeatures(ot, true)));
                    var ranked = ns.Select(n => (n, t: Unwrap(MemberType(n), false)))
                        .Where(x => x.t != null && Movable(x.t, false) && !reverse.ContainsKey(x.t.FullName ?? ""))
                        .Select(x => (x.n, x.t, s: Jaccard(fo, Bag(Features(x.t!, false, newRefs).Concat(TypeCallFeatures(x.t!, false))))))
                        .OrderByDescending(x => x.s).ToList();
                    if (ranked.Count > 0)
                    {
                        double best = ranked[0].s, second = ranked.Count > 1 ? ranked[1].s : 0;
                        if (best >= 0.3 && best - second >= 0.06)
                        {
                            Pair(ot.FullName!, ranked[0].t!.FullName!, best);
                            why = $"{om.Name}→{ranked[0].n.Name} by its type {ot.Name}→{ranked[0].t!.Name} {best:0.00} (next {second:0.00}) among {ns.Count} same {what}";
                            return Mapped(om, ranked[0].n);
                        }
                    }
                }
                // several candidates: the call graph decides for methods, declaration order backs it up
                if (om is MethodBase omb && oldS.Calls != null && newS.Calls != null)
                {
                    var fo = CallFeatures(omb, true);
                    var ranked = ns.Select(n => (n, s: Jaccard(fo, CallFeatures((MethodBase)n, false))))
                        .OrderByDescending(x => x.s).ToList();
                    double best = ranked[0].s, second = ranked.Count > 1 ? ranked[1].s : 0;
                    var ordinal = os.Count == ns.Count ? ns[idx] : null;
                    if (fo.Count > 0 && best >= 0.3 && best - second >= 0.1)
                    {
                        string agree = ordinal == null ? $"{os.Count} old vs {ns.Count} new" : ordinal == ranked[0].n ? "order agrees" : $"order says {ordinal.Name}";
                        why = $"{om.Name}→{ranked[0].n.Name} calls {best:0.00} (next {second:0.00}, {agree}) among {ns.Count} same {what}";
                        return Mapped(om, ranked[0].n);
                    }
                    if (ordinal != null && (fo.Count == 0 || ranked.First(x => x.n == ordinal).s >= best - 0.0001))
                    {
                        why = $"{om.Name}→{ordinal.Name} #{idx + 1} of {os.Count} same {what} (calls {best:0.00}/{second:0.00} undecided)";
                        return Mapped(om, ordinal);
                    }
                    why = $"{om.Name}: {os.Count} old vs {ns.Count} new members of {what} {k}; calls best {ranked[0].n.Name} {best:0.00}, next {second:0.00}";
                    return null;
                }
                if (os.Count == ns.Count)
                {
                    why = $"{om.Name}→{ns[idx].Name} #{idx + 1} of {os.Count} same {what}";
                    return ns[idx];
                }
                // members were added or removed: align the declaration orders (real names sort in declaration
                // order) as a diff on the signatures; mapped and stable types anchor the alignment
                if (oldReal.Contains(om.Name) && Align(oldOpen, newOpen, om, x => Key(x, true), x => Key(x, false)) is { } aligned)
                {
                    why = $"{om.Name}→{aligned.Name} aligned in declaration order ({oldOpen.Count} old, {newOpen.Count} new {om.MemberType}s; {os.Count} vs {ns.Count} of {what} {k})";
                    return Mapped(om, aligned);
                }
                why = $"{om.Name}: {os.Count} old vs {ns.Count} new members of {what} {k}";
                return null;
            }
            why = $"{om.Name}: no new member of signature {oldSig} in {newDecl.FullName}";
            return null;
        }
    }
}
