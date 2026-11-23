using System.Runtime.InteropServices;

namespace ChestCounter;

internal static class Brand
{
    public const string Name = "toomanychests";
    public const string Executable = Name + ".exe";
    public static readonly Lazy<string> BodyFontFace = new(() =>
        Font("Heading Bold", "secondary.ttf", "ttf", "truetype", "unicode-range:U+0000-002F,U+003A-10FFFF;") +
        Font("Chest Digits", "digits.ttf", "ttf", "truetype", "unicode-range:U+0030-0039;"));

    public static readonly Lazy<string> FontFace = new(() => BodyFontFace.Value +
        Font("Heading Now", "interface.ttf", "ttf", "truetype", "unicode-range:U+0000-0024,U+0026-002A,U+002C-002F,U+003A-10FFFF;") +
        Font("Wordmark", "wordmark.ttf", "ttf", "truetype"));

    public static readonly Lazy<string> IconSrc = new(() => DataUri("brand.png", "image/png"));
    public static readonly Lazy<Bitmap> ChestImage = new(() =>
    {
        using var stream = Resource("brand.png");
        using var source = new Bitmap(stream);
        return new Bitmap(source);
    });
    public static readonly Lazy<string> DashboardCss = new(() =>
    {
        using var reader = new StreamReader(Resource("dashboard.css"));
        return reader.ReadToEnd();
    });

    public static readonly Lazy<Icon> Icon = new(() =>
    {
        using var stream = Resource("brand.ico");
        return new Icon(stream, 32, 32);
    });

    public static readonly Lazy<string> OverlayFont = new(() => RegisterFont("secondary.ttf", "Barlow Condensed"));
    public static readonly Lazy<string> OverlayDigits = new(() => RegisterFont("digits.ttf", "Chest Digits"));

    private static string RegisterFont(string resource, string family)
    {
        using var stream = Resource(resource);
        using var font = new MemoryStream();
        stream.CopyTo(font);
        var bytes = font.ToArray();
        return AddFontMemResourceEx(bytes, (uint)bytes.Length, IntPtr.Zero, out _) != IntPtr.Zero
            ? family
            : "Segoe UI";
    }

    private static Stream Resource(string name) =>
        typeof(Brand).Assembly.GetManifestResourceStream(name)
        ?? throw new InvalidOperationException($"Missing brand resource {name}");

    private static string DataUri(string name, string mime)
    {
        using var source = Resource(name);
        using var buffer = new MemoryStream();
        source.CopyTo(buffer);
        return $"data:{mime};base64,{Convert.ToBase64String(buffer.ToArray())}";
    }

    private static string Font(string family, string name, string kind, string format, string descriptors = "") =>
        $"@font-face{{font-family:'{family}';src:url({DataUri(name, "font/" + kind)}) format('{format}');{descriptors}font-weight:100 900;font-display:swap}}";

    [DllImport("gdi32.dll")]
    private static extern IntPtr AddFontMemResourceEx(byte[] data, uint size, IntPtr reserved, out uint count);
}
