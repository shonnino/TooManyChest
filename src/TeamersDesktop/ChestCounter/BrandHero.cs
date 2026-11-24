using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace ChestCounter;

public sealed class BrandHero : Control
{
	private readonly Image banner;

	private readonly Image avatar;

	private readonly Font nameFont = new Font("Segoe UI", 22f, FontStyle.Bold);

	private readonly Font detailFont = new Font("Segoe UI", 9f, FontStyle.Regular);

	private bool sizing;

	public BrandHero(Image banner, Image avatar)
	{
		this.banner = banner;
		this.avatar = avatar;
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Theme.Card;
	}

	protected override void OnSizeChanged(EventArgs e)
	{
		base.OnSizeChanged(e);
		if (!sizing && banner != null && Width >= 4)
		{
			int num = (int)Math.Ceiling((double)((Width - 2) * banner.Height) / (double)banner.Width) + 112;
			if (Height != num)
			{
				sizing = true;
				Height = num;
				sizing = false;
			}
		}
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
		e.Graphics.Clear(Parent?.BackColor ?? Theme.Bg);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		if (Width < 4 || Height < 4)
		{
			return;
		}
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
		graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
		using GraphicsPath graphicsPath = Shape.Rounded(new RectangleF(1f, 1f, Width - 2, Height - 2), 18f);
		GraphicsState gstate = graphics.Save();
		graphics.SetClip(graphicsPath);
		using (SolidBrush brush = new SolidBrush(Theme.Card))
		{
			graphics.FillRectangle(brush, ClientRectangle);
		}
		float num = Math.Max(1, Height - 112);
		RectangleF rectangleF = new RectangleF(1f, 1f, Width - 2, num);
		float num2 = Math.Min(rectangleF.Width / (float)banner.Width, rectangleF.Height / (float)banner.Height);
		float num3 = (float)banner.Width * num2;
		float num4 = (float)banner.Height * num2;
		RectangleF value = new RectangleF(rectangleF.X + (rectangleF.Width - num3) / 2f, rectangleF.Y + (rectangleF.Height - num4) / 2f, num3, num4);
		using ImageAttributes imageAttributes = new ImageAttributes();
		imageAttributes.SetWrapMode(WrapMode.TileFlipXY);
		graphics.DrawImage(banner, Rectangle.Round(value), 0, 0, banner.Width, banner.Height, GraphicsUnit.Pixel, imageAttributes);
		float num5 = num + 1f;
		using (Pen pen = new Pen(Theme.CardEdge, 1f))
		{
			graphics.DrawLine(pen, 1f, num5, Width - 1, num5);
		}
		RectangleF rect = new RectangleF(24f, num5 + 19f, 72f, 72f);
		using (SolidBrush brush2 = new SolidBrush(Theme.Card))
		{
			graphics.FillEllipse(brush2, rect);
		}
		RectangleF rect2 = new RectangleF(rect.X + 1.5f, rect.Y + 1.5f, rect.Width - 3f, rect.Height - 3f);
		GraphicsState gstate2 = graphics.Save();
		using (GraphicsPath graphicsPath2 = new GraphicsPath())
		{
			graphicsPath2.AddEllipse(rect2);
			graphics.SetClip(graphicsPath2, CombineMode.Intersect);
			float num6 = Math.Max(rect2.Width / (float)avatar.Width, rect2.Height / (float)avatar.Height);
			float num7 = (float)avatar.Width * num6;
			float num8 = (float)avatar.Height * num6;
			graphics.DrawImage(destRect: Rectangle.Round(new RectangleF(rect2.X + (rect2.Width - num7) / 2f, rect2.Y + (rect2.Height - num8) / 2f, num7, num8)), image: avatar, srcX: 0, srcY: 0, srcWidth: avatar.Width, srcHeight: avatar.Height, srcUnit: GraphicsUnit.Pixel, imageAttr: imageAttributes);
		}
		graphics.Restore(gstate2);
		using (Pen pen2 = new Pen(Theme.Accent, 2.5f))
		{
			graphics.DrawEllipse(pen2, rect);
		}
		using (SolidBrush brush3 = new SolidBrush(Theme.Ink))
		{
			graphics.DrawString("2xFlaw", nameFont, brush3, 112f, num5 + 19f);
		}
		using (SolidBrush brush4 = new SolidBrush(Theme.AccentSoft))
		{
			graphics.DrawString("PERSONAL CHEST TRACKER  /  REPLAY ANALYTICS", detailFont, brush4, 114f, num5 + 64f);
		}
		graphics.Restore(gstate);
		using Pen pen3 = new Pen(Theme.CardEdge, 1f);
		graphics.DrawPath(pen3, graphicsPath);
	}

	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);
		if (disposing)
		{
			nameFont.Dispose();
			detailFont.Dispose();
		}
	}
}
