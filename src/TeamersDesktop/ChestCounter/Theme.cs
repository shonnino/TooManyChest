using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ChestCounter;

[SupportedOSPlatform("windows")]
internal static class Theme
{
	public static readonly Color Bg = Color.FromArgb(14, 9, 18);

	public static readonly Color Card = Color.FromArgb(25, 15, 30);

	public static readonly Color CardEdge = Color.FromArgb(69, 28, 60);

	public static readonly Color Panel = Color.FromArgb(43, 23, 45);

	public static readonly Color PanelHover = Color.FromArgb(70, 28, 61);

	public static readonly Color Line = Color.FromArgb(83, 39, 72);

	public static readonly Color Ink = Color.FromArgb(249, 236, 246);

	public static readonly Color Muted = Color.FromArgb(183, 155, 178);

	public static readonly Color Cyan = Color.FromArgb(208, 0, 128);

	public static readonly Color Amber = Color.FromArgb(255, 127, 180);

	public static readonly Color Mint = Color.FromArgb(61, 220, 151);

	public static readonly Color Accent = Color.FromArgb(208, 0, 128);

	public static readonly Color AccentHover = Color.FromArgb(240, 41, 154);

	public static readonly Color AccentInk = Color.FromArgb(255, 244, 251);

	public static readonly Color AccentSoft = Color.FromArgb(255, 164, 212);

	public static readonly Color Blue = Color.FromArgb(160, 0, 96);

	public static readonly Color Rose = Color.FromArgb(255, 101, 183);

	public static readonly Color Coral = Color.FromArgb(255, 99, 99);

	public static Color Gold => Amber;

	public static Color Green => Mint;

	public static Color Red => Coral;

	public static Font Text(float size = 9f, FontStyle style = FontStyle.Regular)
	{
		return Fonts.Body(size, style);
	}

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

	public static void Chrome(nint hwnd)
	{
		try
		{
			int value = 1;
			DwmSetWindowAttribute(hwnd, 20, ref value, 4);
			DwmSetWindowAttribute(hwnd, 19, ref value, 4);
			int value2 = 2;
			DwmSetWindowAttribute(hwnd, 33, ref value2, 4);
		}
		catch
		{
		}
	}

	public static void Style(Button b)
	{
		b.FlatStyle = FlatStyle.Flat;
		b.BackColor = Panel;
		b.ForeColor = Ink;
		b.Font = Text();
		b.Cursor = Cursors.Hand;
		b.FlatAppearance.BorderColor = Line;
		b.FlatAppearance.MouseOverBackColor = PanelHover;
		b.FlatAppearance.MouseDownBackColor = Line;
	}
}
