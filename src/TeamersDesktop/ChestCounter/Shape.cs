using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ChestCounter;

internal static class Shape
{
	public static GraphicsPath Rounded(RectangleF r, float radius)
	{
		float num = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
		GraphicsPath graphicsPath = new GraphicsPath();
		graphicsPath.AddArc(r.X, r.Y, num, num, 180f, 90f);
		graphicsPath.AddArc(r.Right - num, r.Y, num, num, 270f, 90f);
		graphicsPath.AddArc(r.Right - num, r.Bottom - num, num, num, 0f, 90f);
		graphicsPath.AddArc(r.X, r.Bottom - num, num, num, 90f, 90f);
		graphicsPath.CloseFigure();
		return graphicsPath;
	}
}
