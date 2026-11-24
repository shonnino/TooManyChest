using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;

namespace ChestCounter;

[SupportedOSPlatform("windows")]
public static class RewardPictures
{
	public static readonly (string Key, string Name)[] All = new (string, string)[6]
	{
		("gold-outfit", "Weekly Gold Outfit"),
		("pirate-shop-access", "Pirate Shop Access"),
		("vehicle-decal", "Weekly Vehicle & Decal"),
		("pickaxe-back-bling", "Weekly Pickaxe or Back Bling"),
		("jam-track", "Weekly Jam Track"),
		("loading-screen", "Weekly Loading Screen")
	};

	public static Bitmap Load(string folder, string key, int size, int radius)
	{
		string[] array = new string[3] { ".png", ".jpg", ".jpeg" };
		foreach (string text in array)
		{
			string path = Path.Combine(folder, key + text);
			if (File.Exists(path))
			{
				try
				{
					return Render(File.ReadAllBytes(path), size, radius);
				}
				catch
				{
				}
			}
		}
		try
		{
			Assembly assembly = typeof(RewardPictures).Assembly;
			string text2 = assembly.GetManifestResourceNames().FirstOrDefault((string n) => n.EndsWith("." + key + ".png", StringComparison.OrdinalIgnoreCase) || n.EndsWith("." + key + ".jpg", StringComparison.OrdinalIgnoreCase));
			if (text2 != null)
			{
				using Stream stream = assembly.GetManifestResourceStream(text2);
				if (stream != null)
				{
					using (MemoryStream memoryStream = new MemoryStream())
					{
						stream.CopyTo(memoryStream);
						return Render(memoryStream.ToArray(), size, radius);
					}
				}
			}
		}
		catch
		{
		}
		return Placeholder(size, radius);
	}

	private static Bitmap Placeholder(int size, int radius)
	{
		int num = size * 2;
		Bitmap bitmap = new Bitmap(num, num);
		using Graphics graphics = Graphics.FromImage(bitmap);
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
		using (GraphicsPath path = Shape.Rounded(new RectangleF(0f, 0f, num, num), radius * 2))
		{
			using SolidBrush brush = new SolidBrush(Theme.Panel);
			graphics.FillPath(brush, path);
		}
		using Font font = new Font("Segoe UI", (float)num * 0.42f, FontStyle.Regular, GraphicsUnit.Pixel);
		using SolidBrush brush2 = new SolidBrush(Theme.Line);
		StringFormat format = new StringFormat
		{
			Alignment = StringAlignment.Center,
			LineAlignment = StringAlignment.Center
		};
		graphics.DrawString("★", font, brush2, new RectangleF(0f, 0f, num, num), format);
		return bitmap;
	}

	private static Bitmap Render(byte[] data, int size, int radius)
	{
		using Image original = Image.FromStream(new MemoryStream(data));
		using Bitmap bitmap = new Bitmap(original);
		int num = size * 2;
		bool flag = bitmap.GetPixel(0, 0).A == byte.MaxValue && bitmap.GetPixel(bitmap.Width - 1, 0).A == byte.MaxValue && bitmap.GetPixel(0, bitmap.Height - 1).A == byte.MaxValue && bitmap.GetPixel(bitmap.Width - 1, bitmap.Height - 1).A == byte.MaxValue;
		Bitmap bitmap2 = new Bitmap(num, num);
		using Graphics graphics = Graphics.FromImage(bitmap2);
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
		graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
		if (flag)
		{
			int num2 = Math.Min(bitmap.Width, bitmap.Height);
			RectangleF srcRect = new RectangleF((float)(bitmap.Width - num2) / 2f, (float)(bitmap.Height - num2) / 2f, num2, num2);
			using GraphicsPath clip = Shape.Rounded(new RectangleF(0f, 0f, num, num), radius * 2);
			graphics.SetClip(clip);
			graphics.DrawImage(bitmap, new RectangleF(0f, 0f, num, num), srcRect, GraphicsUnit.Pixel);
			return bitmap2;
		}
		double num3 = Math.Min((double)num / (double)bitmap.Width, (double)num / (double)bitmap.Height);
		int num4 = (int)((double)bitmap.Width * num3);
		int num5 = (int)((double)bitmap.Height * num3);
		graphics.DrawImage(bitmap, (num - num4) / 2, (num - num5) / 2, num4, num5);
		return bitmap2;
	}
}
