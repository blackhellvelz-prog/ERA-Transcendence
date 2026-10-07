using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WoG.Core.State;
using WoG.Erm.Runtime;
using WoG.Erm.Syntax;

namespace WoG.Erm.Receivers;

/// <summary>
/// SN:F^name^/args... — Era calls an exported function of era.dll (or kernel32/user32, or "dll:function").
/// The port implements the era.dll functions used by the ERA project's scripts (Extern.pas, AdvErm.pas,
/// WogEvo.pas); the result goes to v1 (or e1 with a "." prefix), as CallProc does. Win32 calls answer
/// "nothing found". Anything else is reported as unsupported.
/// </summary>
public static class EraApi
{
    delegate int Fn(ErmCall c, ErmRuntime rt);

    static readonly char[] TrimChars = BuildTrimChars();
    static readonly string ProcessGuid = Guid.NewGuid().ToString("D").ToUpperInvariant();

    static readonly Dictionary<string, Fn> Table = new(StringComparer.Ordinal)
    {
        ["ShowErmError"] = (c, rt) => { rt.ReportError(Str(c, 1)); return 0; },
        ["ExtendArrayLifetime"] = (c, rt) => { rt.ExtendArrayLifetime(Int(c, 1)); return 0; },
        ["IsCommanderId"] = (c, rt) => Int(c, 1) >= 174 && Int(c, 1) <= 191 ? 1 : 0,
        ["IsCampaign"] = (c, rt) => 0,
        ["Hash32"] = (c, rt) => Crc32(Encoding.Latin1.GetBytes(Str(c, 1)), Int(c, 2)),
        ["SplitMix32"] = (c, rt) => SplitMix32(c, rt),
        ["Erm_Substr"] = (c, rt) => rt.CreateTriggerLocalErtPublic(Substr(Str(c, 1), Int(c, 2), Int(c, 3))),
        ["Erm_StrTrim"] = (c, rt) => rt.CreateTriggerLocalErtPublic(Str(c, 1).Trim(TrimChars)),
        ["Erm_StrReplace"] = (c, rt) =>
        {
            string what = Str(c, 2);
            return rt.CreateTriggerLocalErtPublic(what.Length == 0 ? Str(c, 1) : Str(c, 1).Replace(what, Str(c, 3), StringComparison.Ordinal));
        },
        ["Erm_StrPos"] = (c, rt) => StrPos(Str(c, 1), Str(c, 2), Int(c, 3)),
        ["Erm_Interpolate"] = (c, rt) => rt.CreateTriggerLocalErtPublic(rt.InterpolateEra(Str(c, 1))),
        ["Erm_CompareStrings"] = (c, rt) => Math.Sign(string.CompareOrdinal(Str(c, 1), Str(c, 2))),
        ["Erm_IntLog2"] = (c, rt) => IntLog2(Int(c, 1)),
        ["trStatic"] = (c, rt) => rt.CreateTriggerLocalErtPublic(rt.Lang.Tr(Str(c, 1))),
        ["PluginExists"] = (c, rt) => 0,
        ["PcxPngExists"] = (c, rt) => 0,
        ["DisableErmTracking"] = (c, rt) => 0,
        ["EnableErmTracking"] = (c, rt) => 0,
        ["RestoreErmTracking"] = (c, rt) => 0,
        ["ResetErmTracking"] = (c, rt) => 0,
        ["GetProcessGuid"] = (c, rt) => rt.CreateTriggerLocalErtPublic(ProcessGuid),
        // Win32 (kernel32/user32): no H3 window, no files of H3, no other modules
        ["GetKeyState"] = (c, rt) => 0,
        ["GetModuleHandleA"] = (c, rt) => 0,
        ["GetFileAttributesA"] = (c, rt) => -1,
        ["FindFirstFileA"] = (c, rt) => -1,
        ["FindNextFileA"] = (c, rt) => 0,
        ["FindClose"] = (c, rt) => 1,
    };

    static char[] BuildTrimChars()
    {
        var a = new char[32];
        for (int i = 0; i < 32; i++) a[i] = (char)(i + 1);
        return a;
    }

    public static void Call(ErmCall c)
    {
        var rt = c.Rt;
        if (c.Num < 1 || c.IsGet(0) || !ErmRuntime.EraIsString(c.P(0)))
            throw new ErmRuntimeException("Invalid command syntax. Valid syntax is !!SN:F^API function name^/possible parameters...");
        string name = rt.EraGetText(c.P(0));
        if (name.Length == 0) throw new ErmRuntimeException("Cannot call function with empty name");
        bool floatRes = name[0] == '.';
        if (floatRes) name = name.Substring(1);
        int colon = name.IndexOf(':');
        string api = colon >= 0 ? name.Substring(colon + 1) : name;
        if (colon >= 0 && !string.Equals(name.Substring(0, colon), "era", StringComparison.OrdinalIgnoreCase))
            throw new ErmUnsupportedException($"SN:F^{name}^ — ERA plugin function (DLL): different engine");
        if (!Table.TryGetValue(api, out var fn))
            throw new ErmUnsupportedException($"SN:F^{name}^ — Era API function is not ported");
        int result = fn(c, rt);
        if (floatRes) rt.Services.State.Erm.V[0] = result; // e1 for float results is not used by any project script
        else rt.Services.State.Erm.V[0] = result;            // v1
    }

    /// <summary>A service parameter as Era passes it: int value, or the string for string parameters.</summary>
    static int Int(ErmCall c, int i) => i < c.Num ? (c.IsGet(i) ? c.Rt.EraGetInt(c.P(i)) : c.N(i)) : 0;

    static string Str(ErmCall c, int i)
    {
        if (i >= c.Num) return "";
        var p = c.P(i);
        if (ErmRuntime.EraIsString(p))
            return ErmRuntime.EraTypeOf(p) == ErmVarKind.Z ? c.Rt.EraZRaw(c.Rt.EraResolvedIndex(p)) : c.Rt.EraGetText(p);
        // an int holding a string index (local ERT / z)
        return c.Rt.EraZInterpolated(c.N(i));
    }

    static int SplitMix32(ErmCall c, ErmRuntime rt)
    {
        // SplitMix32(var Seed; MinValue, MaxValue): the seed parameter is passed by reference (?var)
        int seed = Int(c, 1);
        int min = Int(c, 2), max = Int(c, 3);
        int result;
        if (min >= max) result = min;
        else if (min == int.MinValue && max == int.MaxValue) result = Mix(ref seed);
        else
        {
            uint range = unchecked((uint)(max - min + 1));
            uint maxUnbiased = uint.MaxValue / range * range - 1;
            uint r = 0;
            for (int k = 0; k <= 100; k++)
            {
                r = unchecked((uint)Mix(ref seed));
                if (r <= maxUnbiased) break;
            }
            result = unchecked(min + (int)(r % range));
        }
        if (c.Num > 1 && c.IsGet(1)) rt.EraSetInt(c.P(1), seed);
        return result;
    }

    static int Mix(ref int seed)
    {
        unchecked
        {
            seed += (int)0x9E3779B9;
            int r = seed ^ (int)((uint)seed >> 15);
            r *= (int)0x85EBCA6B;
            r ^= (int)((uint)r >> 13);
            r *= (int)0xC2B2AE35;
            r ^= (int)((uint)r >> 16);
            return r;
        }
    }

    static string Substr(string s, int offset, int count)
    {
        int len = s.Length;
        if (offset < 0) offset = Math.Max(0, len + offset);
        if (count < 0) count += len;
        if (len > 0 && offset < len && count > 0)
        {
            count = Math.Min(len - offset, count);
            return s.Substring(offset, count);
        }
        return "";
    }

    static int StrPos(string where, string what, int offset)
    {
        if (offset < 0) return -1;
        if (offset != 0 && offset >= where.Length) return -1;
        int p = where.IndexOf(what, offset, StringComparison.Ordinal);
        return p;
    }

    static int IntLog2(int v)
    {
        if (v <= 0) return 0;
        int r = 0;
        while ((1 << r) < v && r < 31) r++;
        return r;
    }

    static int Crc32(byte[] data, int size)
    {
        size = Math.Clamp(size, 0, data.Length);
        uint crc = 0xFFFFFFFF;
        for (int i = 0; i < size; i++)
        {
            crc ^= data[i];
            for (int k = 0; k < 8; k++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return unchecked((int)~crc);
    }
}
