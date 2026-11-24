using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ChestCounter;

internal static class ChanceText
{
	public static string Pct(double chance)
	{
		double num = chance * 100.0;
		if (num <= 0.0)
		{
			return "0%";
		}
		string text;
		if (num >= 10.0)
		{
			text = num.ToString("0.0");
		}
		else
		{
			text = ((num >= 1.0) ? num.ToString("0.00") : num.ToString("0.0000"));
		}
		return text + "%";
	}

	public static void Draw(Graphics g, string text, Font font, Rectangle box, Color color, TextFormatFlags flags = TextFormatFlags.Default)
	{
		TextRenderer.DrawText(g, WeeklyChanceText.T(text), font, box, color, flags | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
	}

	public static void Chip(Graphics g, string text, Font font, int right, int top, Color accent)
	{
		text = Fonts.Track(WeeklyChanceText.T(text).ToUpper(L.Culture));
		Size size = TextRenderer.MeasureText(g, text, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
		RectangleF rectangleF = new RectangleF(right - size.Width - 20, top, size.Width + 20, size.Height + 10);
		using GraphicsPath path = Shape.Rounded(rectangleF, rectangleF.Height / 2f);
		using SolidBrush brush = new SolidBrush(Color.FromArgb(34, accent));
		using Pen pen = new Pen(Color.FromArgb(120, accent), 1f);
		g.FillPath(brush, path);
		g.DrawPath(pen, path);
		Draw(g, text, font, Rectangle.Round(rectangleF), accent, TextFormatFlags.HorizontalCenter);
	}

	public static void ChipAt(Graphics g, string text, Font font, int left, int top, Color accent)
	{
		string text2 = Fonts.Track(WeeklyChanceText.T(text).ToUpper(L.Culture));
		Chip(g, text, font, left + TextRenderer.MeasureText(g, text2, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width + 20, top, accent);
	}

	public static void Star(Graphics g, float cx, float cy, float r, Color color)
	{
		PointF[] array = new PointF[8];
		for (int i = 0; i < 8; i++)
		{
			double num = Math.PI / 4.0 * (double)i - Math.PI / 2.0;
			float num2 = ((i % 2 == 0) ? r : (r * 0.26f));
			array[i] = new PointF(cx + (float)(Math.Cos(num) * (double)num2), cy + (float)(Math.Sin(num) * (double)num2));
		}
		using SolidBrush brush = new SolidBrush(color);
		g.FillPolygon(brush, array);
	}
}
