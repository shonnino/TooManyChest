using System;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace ChestCounter;

[SupportedOSPlatform("windows")]
internal static class Fonts
{
	private static readonly PrivateFontCollection Names = new PrivateFontCollection();

	private static string? _body;

	private static string? _display;

	private static FontStyle _displayStyle = FontStyle.Regular;

	private static bool _loaded;

	public static bool HasBody => _body != null;

	public static bool HasDisplay => _display != null;

	[DllImport("gdi32.dll")]
	private static extern nint AddFontMemResourceEx(nint pbFont, uint cbFont, nint pdv, ref uint pcFonts);

	public static void Load()
	{
		if (_loaded)
		{
			return;
		}
		_loaded = true;
		try
		{
			Assembly assembly = typeof(Fonts).Assembly;
			foreach (string item3 in from n in assembly.GetManifestResourceNames()
				where n.StartsWith("ChestCounter.Assets.fonts.", StringComparison.OrdinalIgnoreCase) && n.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)
				select n)
			{
				using Stream stream = assembly.GetManifestResourceStream(item3);
				if (stream != null)
				{
					byte[] array = new byte[stream.Length];
					stream.ReadExactly(array);
					nint num = Marshal.AllocCoTaskMem(array.Length);
					Marshal.Copy(array, 0, num, array.Length);
					uint pcFonts = 0u;
					AddFontMemResourceEx(num, (uint)array.Length, IntPtr.Zero, ref pcFonts);
					Names.AddMemoryFont(num, array.Length);
				}
			}
			_body = Check(Names.Families.FirstOrDefault((FontFamily f) => f.Name.StartsWith("Inter", StringComparison.OrdinalIgnoreCase))?.Name, FontStyle.Regular);
			_display = Check(Names.Families.FirstOrDefault((FontFamily f) => f.Name.StartsWith("Anton", StringComparison.OrdinalIgnoreCase))?.Name, FontStyle.Regular);
			FontFamily fontFamily = Names.Families.FirstOrDefault((FontFamily f) => f.Name.StartsWith("Burbank", StringComparison.OrdinalIgnoreCase));
			if (fontFamily != null)
			{
				FontStyle[] array2 = new FontStyle[2]
				{
					FontStyle.Bold,
					FontStyle.Regular
				};
				foreach (FontStyle fontStyle in array2)
				{
					string text = Check(fontFamily.Name, fontStyle);
					if (text != null)
					{
						_display = text;
						_displayStyle = fontStyle;
						break;
					}
				}
			}
		}
		catch
		{
			_body = null;
			_display = null;
		}
		(string, FontStyle)[] array3 = new (string, FontStyle)[3]
		{
			("Burbank Big Condensed", FontStyle.Bold),
			("Burbank Big Regular", FontStyle.Bold),
			("Burbank Big Regular Bold", FontStyle.Regular)
		};
		for (int num2 = 0; num2 < array3.Length; num2++)
		{
			(string, FontStyle) tuple = array3[num2];
			string item = tuple.Item1;
			FontStyle item2 = tuple.Item2;
			string text2 = Check(item, item2);
			if (text2 != null)
			{
				_display = text2;
				_displayStyle = item2;
				break;
			}
		}
	}

	private static string? Check(string? name, FontStyle style)
	{
		if (name == null)
		{
			return null;
		}
		try
		{
			using Font font = new Font(name, 10f, style);
			return string.Equals(font.Name, name, StringComparison.OrdinalIgnoreCase) ? name : null;
		}
		catch
		{
			return null;
		}
	}

	public static Font Body(float size, FontStyle style = FontStyle.Regular)
	{
		return new Font(_body ?? "Segoe UI", size, style);
	}

	public static Font Display(float size)
	{
		if (_display == null)
		{
			return new Font("Segoe UI", size * 0.92f, FontStyle.Bold);
		}
		return new Font(_display, size, _displayStyle);
	}

	public static string Track(string text)
	{
		if (_body == null || text.Length < 2)
		{
			return text;
		}
		StringBuilder stringBuilder = new StringBuilder(text.Length * 2);
		for (int i = 0; i < text.Length; i++)
		{
			stringBuilder.Append(text[i]);
			if (i < text.Length - 1 && text[i] != ' ' && text[i + 1] != ' ')
			{
				stringBuilder.Append('\u200a');
			}
		}
		return stringBuilder.ToString();
	}
}
