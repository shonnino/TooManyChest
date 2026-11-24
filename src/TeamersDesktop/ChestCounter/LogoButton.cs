using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ChestCounter;

[SupportedOSPlatform("windows")]
public sealed class LogoButton : Control
{
	private bool _hover;

	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public Image? Logo { get; set; }

	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public int Radius { get; set; } = 9;

	public LogoButton()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Theme.Panel;
		Cursor = Cursors.Hand;
	}

	protected override void OnMouseEnter(EventArgs e)
	{
		_hover = true;
		Invalidate();
		base.OnMouseEnter(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		_hover = false;
		Invalidate();
		base.OnMouseLeave(e);
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
		e.Graphics.Clear(Parent?.BackColor ?? Theme.Card);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		using (GraphicsPath path = Shape.Rounded(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), Radius))
		{
			using SolidBrush brush = new SolidBrush(_hover ? Theme.PanelHover : BackColor);
			graphics.FillPath(brush, path);
		}
		if (Logo == null)
		{
			return;
		}
		int num = (int)Math.Round((double)Math.Min(Width, Height) * 0.55);
		Rectangle destRect = new Rectangle((Width - num) / 2, (Height - num) / 2, num, num);
		graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
		graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
		graphics.CompositingQuality = CompositingQuality.HighQuality;
		using ImageAttributes imageAttributes = new ImageAttributes();
		imageAttributes.SetWrapMode(WrapMode.TileFlipXY);
		imageAttributes.SetColorMatrix(new ColorMatrix
		{
			Matrix33 = (_hover ? 1f : 0.78f)
		});
		graphics.DrawImage(Logo, destRect, 0, 0, Logo.Width, Logo.Height, GraphicsUnit.Pixel, imageAttributes);
	}
}
