using System.Text;

namespace WoG.Erm.Era;

/// <summary>
/// Era reads scripts as raw ANSI bytes. Modern Era mods ship UTF-8 files (comments, translation keys),
/// classic WoG files are cp1251. A strictly valid UTF-8 file is decoded as UTF-8, anything else as cp1251.
/// </summary>
public static class EraText
{
    static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static string Decode(byte[] bytes)
    {
        int offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        bool ascii = true;
        for (int i = offset; i < bytes.Length; i++) if (bytes[i] >= 0x80) { ascii = false; break; }
        if (ascii) return Encoding.ASCII.GetString(bytes, offset, bytes.Length - offset);
        try
        {
            return StrictUtf8.GetString(bytes, offset, bytes.Length - offset);
        }
        catch (DecoderFallbackException)
        {
            return Syntax.ErmParser.DecodeFile(bytes);
        }
    }
}
