using System.Globalization;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>What a vector icon fills or strokes with: nothing, a color, or the value of <c>currentColor</c> where it is used.</summary>
internal readonly record struct SkUiVectorPaint(SkUiVectorPaintKind Kind, SKColor Color = default)
{
    public static readonly SkUiVectorPaint None = new(SkUiVectorPaintKind.None);
    public static readonly SkUiVectorPaint Current = new(SkUiVectorPaintKind.Current);
}

/// <summary>The kinds of <see cref="SkUiVectorPaint"/>.</summary>
internal enum SkUiVectorPaintKind
{
    None,
    Color,
    Current
}

/// <summary>Reads the values of SVG attributes: lengths, number lists, transforms and colors. Bad values throw <see cref="InvalidDataException"/>.</summary>
internal static class SkUiVectorValues
{
    private const float PixelsPerInch = 96;
    private const int MaxShownValueLength = 40;
    private const int MaxShownFeatureLength = 80;

    /// <summary>The error for a feature of SVG that vector icons do not draw. What it shows of the file is short.</summary>
    public static InvalidDataException Unsupported(string feature) =>
        new($"SVG: {Shortened(feature, MaxShownFeatureLength)} is not supported in vector icons.");

    /// <summary>The error for a value that cannot be read. What it shows of the file is short.</summary>
    public static InvalidDataException Invalid(string name, string value) =>
        new($"SVG: invalid {name} \"{Shortened(value, MaxShownValueLength)}\".");

    private static string Shortened(string text, int length) => text.Length > length ? text[..length] + "…" : text;

    /// <summary>A length in user units: a number, <c>px</c>, <c>pt</c>, <c>pc</c>, <c>in</c>, <c>cm</c> or <c>mm</c>.</summary>
    public static float Length(string value, string name)
    {
        if (!TryLength(value, out var length))
            throw Invalid(name, value);
        return length;
    }

    /// <summary>Reads a length; <c>false</c> for anything else, percentages included.</summary>
    public static bool TryLength(string value, out float length)
    {
        length = 0;
        var text = value.AsSpan().Trim();
        var end = NumberEnd(text, 0);
        if (end == 0 || !TryNumber(text[..end], out var number))
            return false;
        var scale = text[end..] switch
        {
            "" or "px" => 1f,
            "pt" => PixelsPerInch / 72,
            "pc" => PixelsPerInch / 6,
            "in" => PixelsPerInch,
            "cm" => PixelsPerInch / 2.54f,
            "mm" => PixelsPerInch / 25.4f,
            _ => 0f
        };
        length = number * scale;
        return scale > 0 && float.IsFinite(length);
    }

    /// <summary>An opacity: a number or a percentage, limited to 0…1.</summary>
    public static float Opacity(string value, string name)
    {
        var text = value.AsSpan().Trim();
        var percent = text.EndsWith("%");
        if (!TryNumber(percent ? text[..^1] : text, out var number))
            throw Invalid(name, value);
        return Math.Clamp(percent ? number / 100 : number, 0, 1);
    }

    /// <summary>Numbers separated by white space and commas, or by nothing where a sign or a second point starts the next (<c>1-2.5.5</c>).</summary>
    public static float[] Numbers(string value, string name)
    {
        var numbers = new List<float>();
        var text = value.AsSpan();
        var index = 0;
        while (true)
        {
            while (index < text.Length && (char.IsWhiteSpace(text[index]) || text[index] == ','))
                index++;
            if (index == text.Length)
                return numbers.ToArray();
            var end = NumberEnd(text, index);
            if (end == index || !TryNumber(text[index..end], out var number))
                throw Invalid(name, value);
            numbers.Add(number);
            index = end;
        }
    }

    /// <summary>The value of a <c>transform</c> attribute.</summary>
    public static SKMatrix Transform(string value)
    {
        var matrix = SKMatrix.Identity;
        var rest = value.AsSpan().Trim();
        while (!rest.IsEmpty)
        {
            var open = rest.IndexOf('(');
            var close = rest.IndexOf(')');
            if (open <= 0 || close < open)
                throw Invalid(Name, value);
            var function = rest[..open].Trim().ToString();
            var a = Numbers(rest[(open + 1)..close].ToString(), Name);
            matrix = matrix.PreConcat(Function(function, a, value));
            rest = rest[(close + 1)..].TrimStart(" \t\r\n,");
        }
        return matrix;
    }

    /// <summary>A <c>fill</c> or <c>stroke</c> value.</summary>
    public static SkUiVectorPaint Paint(string value, string name)
    {
        var text = value.Trim();
        if (text.Equals("none", StringComparison.OrdinalIgnoreCase))
            return SkUiVectorPaint.None;
        if (text.Equals("currentColor", StringComparison.OrdinalIgnoreCase))
            return SkUiVectorPaint.Current;
        return new SkUiVectorPaint(SkUiVectorPaintKind.Color, Color(text, name));
    }

    /// <summary>A color: <c>#rgb</c>, <c>#rgba</c>, <c>#rrggbb</c>, <c>#rrggbbaa</c>, <c>rgb()</c>, <c>rgba()</c> or a CSS name.</summary>
    public static SKColor Color(string value, string name)
    {
        var text = value.Trim();
        if (text.StartsWith('#'))
            return Hex(text.AsSpan(1)) ?? throw Invalid(name, value);
        if (text.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            return Function(text) ?? throw Invalid(name, value);
        if (SkUiVectorColors.TryGet(text, out var color))
            return color;
        throw Unsupported($"the color \"{text}\"");
    }

    private const string Name = "transform";

    private static SKMatrix Function(string function, float[] a, string value)
    {
        switch (function)
        {
            case "matrix" when a.Length == 6:
                return new SKMatrix(a[0], a[2], a[4], a[1], a[3], a[5], 0, 0, 1);
            case "translate" when a.Length is 1 or 2:
                return SKMatrix.CreateTranslation(a[0], a.Length == 2 ? a[1] : 0);
            case "scale" when a.Length is 1 or 2:
                return SKMatrix.CreateScale(a[0], a.Length == 2 ? a[1] : a[0]);
            case "rotate" when a.Length == 1:
                return SKMatrix.CreateRotationDegrees(a[0]);
            case "rotate" when a.Length == 3:
                return SKMatrix.CreateRotationDegrees(a[0], a[1], a[2]);
            case "skewX" when a.Length == 1:
                return SKMatrix.CreateSkew(MathF.Tan(a[0] * MathF.PI / 180), 0);
            case "skewY" when a.Length == 1:
                return SKMatrix.CreateSkew(0, MathF.Tan(a[0] * MathF.PI / 180));
            default:
                throw Invalid(Name, value);
        }
    }

    private static SKColor? Hex(ReadOnlySpan<char> hex)
    {
        Span<byte> parts = stackalloc byte[4];
        parts[3] = 255;
        var count = hex.Length is 3 or 4 ? hex.Length : hex.Length is 6 or 8 ? hex.Length / 2 : 0;
        if (count == 0)
            return null;
        var width = hex.Length / count;
        for (var part = 0; part < count; part++)
        {
            if (!byte.TryParse(hex.Slice(part * width, width), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var channel))
                return null;
            parts[part] = width == 1 ? (byte)(channel * 17) : channel;
        }
        return new SKColor(parts[0], parts[1], parts[2], parts[3]);
    }

    private static SKColor? Function(string text)
    {
        var open = text.IndexOf('(');
        var close = text.LastIndexOf(')');
        if (open < 0 || close < open)
            return null;
        var parts = text.Substring(open + 1, close - open - 1).Split([' ', ',', '/', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is not (3 or 4))
            return null;
        Span<byte> channels = stackalloc byte[3];
        for (var index = 0; index < 3; index++)
        {
            var part = parts[index].AsSpan();
            var percent = part.EndsWith("%");
            if (!TryNumber(percent ? part[..^1] : part, out var number))
                return null;
            channels[index] = (byte)Math.Round(Math.Clamp(percent ? number * 2.55f : number, 0, 255));
        }
        var alpha = 1f;
        if (parts.Length == 4)
        {
            var part = parts[3].AsSpan();
            var percent = part.EndsWith("%");
            if (!TryNumber(percent ? part[..^1] : part, out alpha))
                return null;
            alpha = Math.Clamp(percent ? alpha / 100 : alpha, 0, 1);
        }
        return new SKColor(channels[0], channels[1], channels[2], (byte)Math.Round(alpha * 255));
    }

    private static bool TryNumber(ReadOnlySpan<char> text, out float number) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && float.IsFinite(number);

    /// <summary>The end of the number that starts at <paramref name="start"/>; <paramref name="start"/> when there is none.</summary>
    private static int NumberEnd(ReadOnlySpan<char> text, int start)
    {
        var index = start;
        if (index < text.Length && text[index] is '+' or '-')
            index++;
        var digits = 0;
        while (index < text.Length && char.IsAsciiDigit(text[index]))
        {
            index++;
            digits++;
        }
        if (index < text.Length && text[index] == '.')
        {
            index++;
            while (index < text.Length && char.IsAsciiDigit(text[index]))
            {
                index++;
                digits++;
            }
        }
        if (digits == 0)
            return start;
        // An exponent, but not the "e" of "em" or "ex".
        if (index + 1 < text.Length && text[index] is 'e' or 'E')
        {
            var exponent = index + 1;
            if (text[exponent] is '+' or '-')
                exponent++;
            if (exponent < text.Length && char.IsAsciiDigit(text[exponent]))
            {
                while (exponent < text.Length && char.IsAsciiDigit(text[exponent]))
                    exponent++;
                index = exponent;
            }
        }
        return index;
    }
}

/// <summary>The CSS named colors, from a table so that nothing is found by reflection (trimming keeps it all).</summary>
internal static class SkUiVectorColors
{
    private static readonly Dictionary<string, uint> Named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["aliceblue"] = 0xF0F8FF, ["antiquewhite"] = 0xFAEBD7, ["aqua"] = 0x00FFFF, ["aquamarine"] = 0x7FFFD4, ["azure"] = 0xF0FFFF,
        ["beige"] = 0xF5F5DC, ["bisque"] = 0xFFE4C4, ["black"] = 0x000000, ["blanchedalmond"] = 0xFFEBCD, ["blue"] = 0x0000FF,
        ["blueviolet"] = 0x8A2BE2, ["brown"] = 0xA52A2A, ["burlywood"] = 0xDEB887, ["cadetblue"] = 0x5F9EA0, ["chartreuse"] = 0x7FFF00,
        ["chocolate"] = 0xD2691E, ["coral"] = 0xFF7F50, ["cornflowerblue"] = 0x6495ED, ["cornsilk"] = 0xFFF8DC, ["crimson"] = 0xDC143C,
        ["cyan"] = 0x00FFFF, ["darkblue"] = 0x00008B, ["darkcyan"] = 0x008B8B, ["darkgoldenrod"] = 0xB8860B, ["darkgray"] = 0xA9A9A9,
        ["darkgreen"] = 0x006400, ["darkgrey"] = 0xA9A9A9, ["darkkhaki"] = 0xBDB76B, ["darkmagenta"] = 0x8B008B, ["darkolivegreen"] = 0x556B2F,
        ["darkorange"] = 0xFF8C00, ["darkorchid"] = 0x9932CC, ["darkred"] = 0x8B0000, ["darksalmon"] = 0xE9967A, ["darkseagreen"] = 0x8FBC8F,
        ["darkslateblue"] = 0x483D8B, ["darkslategray"] = 0x2F4F4F, ["darkslategrey"] = 0x2F4F4F, ["darkturquoise"] = 0x00CED1, ["darkviolet"] = 0x9400D3,
        ["deeppink"] = 0xFF1493, ["deepskyblue"] = 0x00BFFF, ["dimgray"] = 0x696969, ["dimgrey"] = 0x696969, ["dodgerblue"] = 0x1E90FF,
        ["firebrick"] = 0xB22222, ["floralwhite"] = 0xFFFAF0, ["forestgreen"] = 0x228B22, ["fuchsia"] = 0xFF00FF, ["gainsboro"] = 0xDCDCDC,
        ["ghostwhite"] = 0xF8F8FF, ["gold"] = 0xFFD700, ["goldenrod"] = 0xDAA520, ["gray"] = 0x808080, ["green"] = 0x008000,
        ["greenyellow"] = 0xADFF2F, ["grey"] = 0x808080, ["honeydew"] = 0xF0FFF0, ["hotpink"] = 0xFF69B4, ["indianred"] = 0xCD5C5C,
        ["indigo"] = 0x4B0082, ["ivory"] = 0xFFFFF0, ["khaki"] = 0xF0E68C, ["lavender"] = 0xE6E6FA, ["lavenderblush"] = 0xFFF0F5,
        ["lawngreen"] = 0x7CFC00, ["lemonchiffon"] = 0xFFFACD, ["lightblue"] = 0xADD8E6, ["lightcoral"] = 0xF08080, ["lightcyan"] = 0xE0FFFF,
        ["lightgoldenrodyellow"] = 0xFAFAD2, ["lightgray"] = 0xD3D3D3, ["lightgreen"] = 0x90EE90, ["lightgrey"] = 0xD3D3D3, ["lightpink"] = 0xFFB6C1,
        ["lightsalmon"] = 0xFFA07A, ["lightseagreen"] = 0x20B2AA, ["lightskyblue"] = 0x87CEFA, ["lightslategray"] = 0x778899, ["lightslategrey"] = 0x778899,
        ["lightsteelblue"] = 0xB0C4DE, ["lightyellow"] = 0xFFFFE0, ["lime"] = 0x00FF00, ["limegreen"] = 0x32CD32, ["linen"] = 0xFAF0E6,
        ["magenta"] = 0xFF00FF, ["maroon"] = 0x800000, ["mediumaquamarine"] = 0x66CDAA, ["mediumblue"] = 0x0000CD, ["mediumorchid"] = 0xBA55D3,
        ["mediumpurple"] = 0x9370DB, ["mediumseagreen"] = 0x3CB371, ["mediumslateblue"] = 0x7B68EE, ["mediumspringgreen"] = 0x00FA9A, ["mediumturquoise"] = 0x48D1CC,
        ["mediumvioletred"] = 0xC71585, ["midnightblue"] = 0x191970, ["mintcream"] = 0xF5FFFA, ["mistyrose"] = 0xFFE4E1, ["moccasin"] = 0xFFE4B5,
        ["navajowhite"] = 0xFFDEAD, ["navy"] = 0x000080, ["oldlace"] = 0xFDF5E6, ["olive"] = 0x808000, ["olivedrab"] = 0x6B8E23,
        ["orange"] = 0xFFA500, ["orangered"] = 0xFF4500, ["orchid"] = 0xDA70D6, ["palegoldenrod"] = 0xEEE8AA, ["palegreen"] = 0x98FB98,
        ["paleturquoise"] = 0xAFEEEE, ["palevioletred"] = 0xDB7093, ["papayawhip"] = 0xFFEFD5, ["peachpuff"] = 0xFFDAB9, ["peru"] = 0xCD853F,
        ["pink"] = 0xFFC0CB, ["plum"] = 0xDDA0DD, ["powderblue"] = 0xB0E0E6, ["purple"] = 0x800080, ["rebeccapurple"] = 0x663399,
        ["red"] = 0xFF0000, ["rosybrown"] = 0xBC8F8F, ["royalblue"] = 0x4169E1, ["saddlebrown"] = 0x8B4513, ["salmon"] = 0xFA8072,
        ["sandybrown"] = 0xF4A460, ["seagreen"] = 0x2E8B57, ["seashell"] = 0xFFF5EE, ["sienna"] = 0xA0522D, ["silver"] = 0xC0C0C0,
        ["skyblue"] = 0x87CEEB, ["slateblue"] = 0x6A5ACD, ["slategray"] = 0x708090, ["slategrey"] = 0x708090, ["snow"] = 0xFFFAFA,
        ["springgreen"] = 0x00FF7F, ["steelblue"] = 0x4682B4, ["tan"] = 0xD2B48C, ["teal"] = 0x008080, ["thistle"] = 0xD8BFD8,
        ["tomato"] = 0xFF6347, ["turquoise"] = 0x40E0D0, ["violet"] = 0xEE82EE, ["wheat"] = 0xF5DEB3, ["white"] = 0xFFFFFF,
        ["whitesmoke"] = 0xF5F5F5, ["yellow"] = 0xFFFF00, ["yellowgreen"] = 0x9ACD32
    };

    /// <summary>The color called <paramref name="name"/>; <c>transparent</c> has no alpha.</summary>
    public static bool TryGet(string name, out SKColor color)
    {
        if (name.Equals("transparent", StringComparison.OrdinalIgnoreCase))
        {
            color = SKColors.Transparent;
            return true;
        }
        if (Named.TryGetValue(name, out var rgb))
        {
            color = new SKColor((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
            return true;
        }
        color = default;
        return false;
    }
}
