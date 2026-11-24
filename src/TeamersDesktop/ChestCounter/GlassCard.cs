using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ChestCounter;

[SupportedOSPlatform("windows")]
public sealed class GlassCard : Panel
{
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public int Radius { get; set; } = 16;

	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public Color? AccentColor { get; set; }

	public GlassCard()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Theme.Card;
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
		e.Graphics.Clear(Parent?.BackColor ?? Theme.Bg);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		if (Width < 6 || Height < 6)
		{
			return;
		}
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		using GraphicsPath path = Shape.Rounded(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), Radius);
		using (LinearGradientBrush brush = new LinearGradientBrush(new RectangleF(0f, 0f, Width, Height), Fx.Mix(BackColor, Color.White, 0.05f), BackColor, 90f))
		{
			graphics.FillPath(brush, path);
		}
		using (Pen pen = new Pen(Theme.CardEdge, 1f))
		{
			graphics.DrawPath(pen, path);
		}
		if (Width > Radius * 2 + 8)
		{
			using Pen pen2 = new Pen(Color.FromArgb(46, 255, 255, 255), 1f);
			graphics.DrawLine(pen2, Radius, 1, Width - Radius, 1);
		}
		Color? accentColor = AccentColor;
		if (!accentColor.HasValue)
		{
			return;
		}
		Color valueOrDefault = accentColor.GetValueOrDefault();
		using GraphicsPath path2 = Shape.Rounded(new RectangleF(16f, 12f, 26f, 3f), 1.5f);
		using SolidBrush brush2 = new SolidBrush(valueOrDefault);
		graphics.FillPath(brush2, path2);
	}
}
