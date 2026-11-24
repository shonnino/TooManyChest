using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ChestCounter;

[SupportedOSPlatform("windows")]
public sealed class Card : Panel
{
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public int Radius { get; set; } = 16;

	public Card()
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
		e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
		using GraphicsPath path = Shape.Rounded(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), Radius);
		using SolidBrush brush = new SolidBrush(BackColor);
		using Pen pen = new Pen(Theme.CardEdge, 1f);
		e.Graphics.FillPath(brush, path);
		e.Graphics.DrawPath(pen, path);
	}
}
