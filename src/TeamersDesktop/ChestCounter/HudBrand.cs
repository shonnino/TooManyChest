using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace ChestCounter;

internal static class HudBrand
{
    public static readonly Lazy<string> OverlayFont = new(() => RegisterFont("BarlowCondensed-Bold.ttf", "Barlow Condensed"));
    public static readonly Lazy<string> OverlayDigits = new(() => RegisterFont("ChestDigits.ttf", "Chest Digits"));
    public static readonly Lazy<Bitmap> ChestImage = new(() =>
    {
        using var stream = Resource("toomanychests.png");
        using var bitmap = new Bitmap(stream);
        return new Bitmap(bitmap);
    });
    public static readonly Lazy<Icon> Icon = new(() =>
    {
        using var stream = Resource("toomanychests.ico");
        return new Icon(stream, 32, 32);
    });

    private static Stream Resource(string name) => typeof(HudBrand).Assembly.GetManifestResourceStream("ChestCounter.Hud." + name)
        ?? throw new InvalidOperationException($"Missing HUD asset {name}.");

    private static string RegisterFont(string name, string family)
    {
        using var resource = Resource(name);
        using var buffer = new MemoryStream();
        resource.CopyTo(buffer);
        var bytes = buffer.ToArray();
        return AddFontMemResourceEx(bytes, (uint)bytes.Length, IntPtr.Zero, out _) != IntPtr.Zero ? family : "Segoe UI";
    }

    [DllImport("gdi32.dll")]
    private static extern IntPtr AddFontMemResourceEx(byte[] data, uint size, IntPtr reserved, out uint count);
}
