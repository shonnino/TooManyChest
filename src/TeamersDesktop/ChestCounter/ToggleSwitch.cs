using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ChestCounter;

[SupportedOSPlatform("windows")]
public sealed class ToggleSwitch : Control
{
	private bool _checked;

	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public bool Checked
	{
		get
		{
			return _checked;
		}
		set
		{
			if (_checked != value)
			{
				_checked = value;
				Invalidate();
				CheckedChanged?.Invoke(this, EventArgs.Empty);
			}
		}
	}

	public event EventHandler? CheckedChanged;

	public ToggleSwitch()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		Size = new Size(40, 22);
		Cursor = Cursors.Hand;
		TabStop = true;
	}

	protected override void OnClick(EventArgs e)
	{
		Focus();
		Checked = !Checked;
		base.OnClick(e);
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		Keys keyCode = e.KeyCode;
		if ((keyCode == Keys.Return || keyCode == Keys.Space) ? true : false)
		{
			Checked = !Checked;
			e.Handled = true;
		}
		base.OnKeyDown(e);
	}

	protected override void OnGotFocus(EventArgs e)
	{
		Invalidate();
		base.OnGotFocus(e);
	}

	protected override void OnLostFocus(EventArgs e)
	{
		Invalidate();
		base.OnLostFocus(e);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.Clear(Parent?.BackColor ?? Theme.Card);
		RectangleF r = new RectangleF(1f, 1f, Width - 3, Height - 3);
		using (GraphicsPath path = Shape.Rounded(r, r.Height / 2f))
		{
			using SolidBrush brush = new SolidBrush(_checked ? Theme.Accent : Theme.Line);
			graphics.FillPath(brush, path);
			if (Focused)
			{
				using Pen pen = new Pen(Theme.AccentSoft, 1f);
				graphics.DrawPath(pen, path);
			}
		}
		float num = r.Height - 6f;
		float x = (_checked ? (r.Right - num - 3f) : (r.Left + 3f));
		using SolidBrush brush2 = new SolidBrush(_checked ? Theme.AccentInk : Theme.Muted);
		graphics.FillEllipse(brush2, x, r.Top + 3f, num, num);
	}
}
