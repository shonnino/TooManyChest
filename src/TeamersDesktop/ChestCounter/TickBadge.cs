using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ChestCounter;

[SupportedOSPlatform("windows")]
public sealed class TickBadge : Control
{
	private bool _on;

	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public bool On
	{
		get
		{
			return _on;
		}
		set
		{
			if (_on != value)
			{
				_on = value;
				Invalidate();
			}
		}
	}

	public event EventHandler? Toggled;

	public TickBadge()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		Size = new Size(30, 30);
		Cursor = Cursors.Hand;
	}

	protected override void OnClick(EventArgs e)
	{
		_on = !_on;
		Invalidate();
		Toggled?.Invoke(this, EventArgs.Empty);
		base.OnClick(e);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.Clear(Parent?.BackColor ?? Theme.Card);
		RectangleF rect = new RectangleF(1.5f, 1.5f, Width - 3, Height - 3);
		if (_on)
		{
			using (SolidBrush brush = new SolidBrush(Theme.Mint))
			{
				graphics.FillEllipse(brush, rect);
				using Pen pen = new Pen(Color.FromArgb(10, 40, 25), 2.6f)
				{
					StartCap = LineCap.Round,
					EndCap = LineCap.Round,
					LineJoin = LineJoin.Round
				};
				graphics.DrawLines(pen, new PointF[3]
				{
					new PointF((float)Width * 0.28f, (float)Height * 0.52f),
					new PointF((float)Width * 0.44f, (float)Height * 0.68f),
					new PointF((float)Width * 0.73f, (float)Height * 0.34f)
				});
				return;
			}
		}
		using Pen pen2 = new Pen(Theme.Line, 2f);
		graphics.DrawEllipse(pen2, rect);
	}
}
