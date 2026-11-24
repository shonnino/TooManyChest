using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ChestCounter;

[SupportedOSPlatform("windows")]
public sealed class GlowButton : Button
{
	private readonly Timer _fade = new Timer
	{
		Interval = 16
	};

	private float _hover;

	private float _target;

	private bool _down;

	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public int Radius { get; set; } = 12;

	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public bool ShowSelectedBar { get; set; } = true;

	public GlowButton()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		FlatStyle = FlatStyle.Flat;
		FlatAppearance.BorderSize = 0;
		BackColor = Theme.Panel;
		ForeColor = Theme.Ink;
		Font = Theme.Text(9f, FontStyle.Bold);
		Cursor = Cursors.Hand;
		_fade.Tick += (object? _, EventArgs _) =>
		{
			float num = _target - _hover;
			if (Math.Abs(num) < 0.02f)
			{
				_hover = _target;
				_fade.Stop();
			}
			else
			{
				_hover += num * 0.28f;
			}
			Invalidate();
		};
	}

	private void PaintBehind(Graphics g)
	{
		g.Clear(Parent?.BackColor ?? Theme.Bg);
		if (Parent is IPaintsBehindChildren paintsBehindChildren)
		{
			GraphicsState gstate = g.Save();
			try
			{
				g.TranslateTransform(-Left, -Top);
				paintsBehindChildren.PaintSurface(g);
			}
			finally
			{
				g.Restore(gstate);
			}
		}
	}

	private void FadeTo(float target)
	{
		_target = target;
		if (!_fade.Enabled)
		{
			_fade.Start();
		}
	}

	protected override void OnMouseEnter(EventArgs e)
	{
		FadeTo(1f);
		base.OnMouseEnter(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		_down = false;
		FadeTo(0f);
		base.OnMouseLeave(e);
	}

	protected override void OnMouseDown(MouseEventArgs mevent)
	{
		_down = true;
		Invalidate();
		base.OnMouseDown(mevent);
	}

	protected override void OnMouseUp(MouseEventArgs mevent)
	{
		_down = false;
		Invalidate();
		base.OnMouseUp(mevent);
	}

	protected override void OnPaintBackground(PaintEventArgs pevent)
	{
		PaintBehind(pevent.Graphics);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		if (Width < 4 || Height < 4)
		{
			return;
		}
		Graphics graphics = e.Graphics;
		PaintBehind(graphics);
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		bool flag = ShowSelectedBar && FlatAppearance.BorderColor.ToArgb() == Theme.Accent.ToArgb();
		RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
		Color color = Fx.Mix(Enabled ? BackColor : Fx.Mix(BackColor, Theme.Bg, 0.5f), Color.White, 0.07f * _hover);
		if (_down)
		{
			color = Fx.Mix(color, Color.Black, 0.16f);
		}
		using GraphicsPath path = Shape.Rounded(r, Radius);
		using (LinearGradientBrush brush = new LinearGradientBrush(new RectangleF(0f, 0f, Width, Height), Fx.Mix(color, Color.White, 0.06f), color, 90f))
		{
			graphics.FillPath(brush, path);
		}
		using (Pen pen = new Pen(Fx.Mix(FlatAppearance.BorderColor.IsEmpty ? Theme.CardEdge : FlatAppearance.BorderColor, Theme.Accent, 0.65f * _hover), 1f))
		{
			graphics.DrawPath(pen, path);
		}
		if (flag)
		{
			using GraphicsPath path2 = Shape.Rounded(new RectangleF(7f, (float)Height * 0.25f, 3f, (float)Height * 0.5f), 1.5f);
			using SolidBrush brush2 = new SolidBrush(Theme.Accent);
			graphics.FillPath(brush2, path2);
		}
		TextFormatFlags textFormatFlags = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;
		TextFormatFlags textFormatFlags2 = textFormatFlags;
		TextFormatFlags textFormatFlags3;
		switch (TextAlign)
		{
		case ContentAlignment.TopLeft:
		case ContentAlignment.MiddleLeft:
		case ContentAlignment.BottomLeft:
			textFormatFlags3 = TextFormatFlags.Default;
			break;
		case ContentAlignment.TopRight:
		case ContentAlignment.MiddleRight:
		case ContentAlignment.BottomRight:
			textFormatFlags3 = TextFormatFlags.Right;
			break;
		default:
			textFormatFlags3 = TextFormatFlags.HorizontalCenter;
			break;
		}
		textFormatFlags = textFormatFlags2 | textFormatFlags3;
		Rectangle bounds = new Rectangle(Padding.Left + (flag ? 6 : 0), _down ? 1 : 0, Math.Max(1, Width - Padding.Horizontal - (flag ? 6 : 0)), Height);
		TextRenderer.DrawText(graphics, Text, Font, bounds, Enabled ? ForeColor : Theme.Muted, textFormatFlags);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_fade.Stop();
			_fade.Dispose();
		}
		base.Dispose(disposing);
	}
}
