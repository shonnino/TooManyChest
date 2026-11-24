using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ChestCounter;

[SupportedOSPlatform("windows")]
public sealed class PillButton : Button
{
	private bool _hover;

	private Color? _indicator;

	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public Color HoverColor { get; set; } = Theme.PanelHover;

	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public int Radius { get; set; } = 12;

	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public Color? Indicator
	{
		get
		{
			return _indicator;
		}
		set
		{
			if (_indicator != value)
			{
				_indicator = value;
				Invalidate();
			}
		}
	}

	public PillButton()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		FlatStyle = FlatStyle.Flat;
		FlatAppearance.BorderSize = 0;
		BackColor = Theme.Panel;
		ForeColor = Theme.Ink;
		Font = Theme.Text(9f, FontStyle.Bold);
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
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.Clear(Parent?.BackColor ?? Theme.Bg);
		using GraphicsPath path = Shape.Rounded(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), Radius);
		using SolidBrush brush = new SolidBrush(_hover ? HoverColor : BackColor);
		graphics.FillPath(brush, path);
		Color? indicator = _indicator;
		if (indicator.HasValue)
		{
			Color valueOrDefault = indicator.GetValueOrDefault();
			using (Pen pen = new Pen(Color.FromArgb(150, valueOrDefault), 1.2f))
			{
				using GraphicsPath path2 = Shape.Rounded(new RectangleF(1f, 1f, Width - 2, Height - 2), Radius);
				graphics.DrawPath(pen, path2);
			}
			TextFormatFlags textFormatFlags = TextFormatFlags.SingleLine;
			Size size = TextRenderer.MeasureText(graphics, Text, Font, Size, textFormatFlags);
			float num = Math.Max(6f, (float)Font.Height * 0.45f);
			float num2 = num * 0.8f;
			float num3 = ((float)Width - (num + num2 + (float)size.Width)) / 2f;
			using (SolidBrush brush2 = new SolidBrush(valueOrDefault))
			{
				graphics.FillEllipse(brush2, num3, ((float)Height - num) / 2f, num, num);
			}
			Rectangle bounds = new Rectangle((int)(num3 + num + num2), 0, size.Width + 2, Height);
			TextRenderer.DrawText(graphics, Text, Font, bounds, ForeColor, textFormatFlags | TextFormatFlags.VerticalCenter);
		}
		else
		{
			TextRenderer.DrawText(graphics, Text, Font, ClientRectangle, ForeColor, TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
		}
	}
}
