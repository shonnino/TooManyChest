using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ChestCounter;

[SupportedOSPlatform("windows")]
internal sealed class OddsTable : Control
{
	private const int HeaderH = 36;

	private const int RowH = 62;

	private readonly Font _head = Fonts.Body(7.5f, FontStyle.Bold);

	private readonly Font _name = Theme.Text(10f, FontStyle.Bold);

	private readonly Font _sub = Theme.Text(8.5f);

	private readonly Font _pct = Fonts.Display(16f);

	private List<OddsRow> _rows = new List<OddsRow>();

	private string? _selected;

	private int _hover = -1;

	private int _ahead = 100;

	public IReadOnlyList<OddsRow> Rows => _rows;

	public int WantedHeight => 36 + _rows.Count * 62 + 10;

	public event Action<string>? Picked;

	public OddsTable()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
	}

	public void SetRows(List<OddsRow> rows, string? selected, int ahead)
	{
		_rows = rows;
		_selected = selected;
		_ahead = ahead;
		Invalidate();
	}

	public void Snap()
	{
		foreach (OddsRow row in _rows)
		{
			row.Fill = row.Target;
		}
		Invalidate();
	}

	public bool Step()
	{
		bool result = false;
		foreach (OddsRow row in _rows)
		{
			double num = row.Target - row.Fill;
			if (Math.Abs(num) < 0.0004)
			{
				row.Fill = row.Target;
				continue;
			}
			row.Fill += num * 0.12;
			result = true;
		}
		Invalidate();
		return result;
	}

	private int RowAt(Point p)
	{
		int num = (p.Y - 36) / 62;
		if (p.Y < 36 || num < 0 || num >= _rows.Count)
		{
			return -1;
		}
		return num;
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		int num = RowAt(e.Location);
		if (num != _hover)
		{
			_hover = num;
			Cursor = ((num >= 0 && _rows[num].Prize.Key != "gizmo") ? Cursors.Hand : Cursors.Default);
			Invalidate();
		}
		base.OnMouseMove(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		_hover = -1;
		Invalidate();
		base.OnMouseLeave(e);
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		base.OnMouseUp(e);
		if (e.Button == MouseButtons.Left)
		{
			int num = RowAt(e.Location);
			if (num >= 0 && _rows[num].Prize.Key != "gizmo")
			{
				Picked?.Invoke(_rows[num].Prize.Key);
			}
		}
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
		e.Graphics.Clear(Parent?.BackColor ?? Theme.Bg);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		if (Width < 200)
		{
			return;
		}
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
		graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
		using (GraphicsPath path = Shape.Rounded(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 18f))
		{
			using SolidBrush brush = new SolidBrush(Theme.Card);
			using Pen pen = new Pen(Theme.CardEdge, 1f);
			graphics.FillPath(brush, path);
			graphics.DrawPath(pen, path);
		}
		int num = 310;
		int num2 = Width - 150;
		ChanceText.Draw(graphics, Fonts.Track(WeeklyChanceMore.T("PRIZE")), _head, new Rectangle(20, 0, 200, 36), Theme.Muted);
		ChanceText.Draw(graphics, Fonts.Track(WeeklyChanceMore.F("CUMULATIVE ODDS THIS WEEK  ·  DASHED = IF YOU OPEN {0:N0} CHESTS", _ahead)), _head, new Rectangle(num, 0, Math.Max(40, num2 - num), 36), Theme.Muted);
		ChanceText.Draw(graphics, Fonts.Track(WeeklyChanceMore.T("CHANCE")), _head, new Rectangle(Width - 130, 0, 112, 36), Theme.Muted, TextFormatFlags.Right);
		for (int i = 0; i < _rows.Count; i++)
		{
			OddsRow oddsRow = _rows[i];
			int num3 = 36 + i * 62;
			bool flag = oddsRow.Prize.Key == _selected;
			bool flag2 = oddsRow.Prize.Boost > 1.0;
			bool flag3 = flag || i == _hover;
			RectangleF r = new RectangleF(8f, num3 + 3, Width - 16, 56f);
			if (flag3)
			{
				using GraphicsPath path2 = Shape.Rounded(r, 12f);
				using SolidBrush brush2 = new SolidBrush(flag ? Color.FromArgb(30, Theme.Cyan) : Theme.PanelHover);
				graphics.FillPath(brush2, path2);
				if (flag)
				{
					using Pen pen2 = new Pen(Color.FromArgb(110, Theme.Cyan), 1.2f);
					graphics.DrawPath(pen2, path2);
				}
			}
			else if (i > 0)
			{
				using Pen pen3 = new Pen(Theme.CardEdge, 1f);
				graphics.DrawLine(pen3, 24, num3, Width - 24, num3);
			}
			graphics.DrawImage(oddsRow.Image, new Rectangle(20, num3 + 11, 40, 40));
			int num4 = 74;
			ChanceText.Draw(graphics, oddsRow.Prize.Name, _name, new Rectangle(num4, num3 + 10, num - num4 - 12, 22), Theme.Ink);
			string text = ((oddsRow.Prize.Key == "gizmo") ? WeeklyChanceMore.F("1 in {0:N0} per chest  ·  can repeat", oddsRow.Prize.Den) : WeeklyChanceMore.F("1 in {0:N0} per chest", oddsRow.Prize.Den));
			ChanceText.Draw(graphics, text, _sub, new Rectangle(num4, num3 + 32, num - num4 - 12, 18), Theme.Muted);
			if (flag2)
			{
				int width = TextRenderer.MeasureText(graphics, text, _sub, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;
				ChanceText.Draw(graphics, WeeklyChanceMore.F("  ·  {0:0.#}× now", oddsRow.Prize.Boost), _sub, new Rectangle(num4 + width, num3 + 32, 80, 18), Theme.Amber);
			}
			RectangleF r2 = new RectangleF(num, (float)num3 + 31f - 4f, Math.Max(40, num2 - num), 8f);
			using (GraphicsPath path3 = Shape.Rounded(r2, 4f))
			{
				using SolidBrush brush3 = new SolidBrush(Theme.Panel);
				graphics.FillPath(brush3, path3);
			}
			double num5 = Math.Clamp(oddsRow.Fill + (oddsRow.Ahead - oddsRow.Prize.Chance), 0.0, 1.0);
			if (flag3 && num5 > oddsRow.Fill + 0.0005)
			{
				float x = r2.X + (float)((double)r2.Width * oddsRow.Fill);
				float num6 = (float)((double)r2.Width * (num5 - oddsRow.Fill));
				if (num6 >= 2f)
				{
					RectangleF r3 = new RectangleF(x, r2.Y, num6, r2.Height);
					Color baseColor = (flag2 ? Theme.Amber : Theme.Cyan);
					using GraphicsPath path4 = Shape.Rounded(r3, 4f);
					using SolidBrush brush4 = new SolidBrush(Color.FromArgb(flag2 ? 90 : 50, baseColor));
					using Pen pen4 = new Pen(Color.FromArgb(210, baseColor), 1f)
					{
						DashStyle = DashStyle.Dash
					};
					graphics.FillPath(brush4, path4);
					graphics.DrawPath(pen4, path4);
				}
			}
			float num7 = (float)((double)r2.Width * Math.Clamp(oddsRow.Fill, 0.0, 1.0));
			if (oddsRow.Fill > 0.0005 && num7 > 0.5f)
			{
				RectangleF rectangleF = new RectangleF(r2.X, r2.Y, Math.Max(num7, 8f), r2.Height);
				using GraphicsPath path5 = Shape.Rounded(rectangleF, 4f);
				using LinearGradientBrush brush5 = new LinearGradientBrush(rectangleF, Theme.Cyan, Theme.Mint, LinearGradientMode.Horizontal);
				graphics.FillPath(brush5, path5);
			}
			Rectangle box = (flag3 ? new Rectangle(Width - 130, num3 + 8, 112, 28) : new Rectangle(Width - 130, num3, 112, 62));
			ChanceText.Draw(graphics, ChanceText.Pct(oddsRow.Fill), _pct, box, flag ? Theme.Cyan : Theme.Ink, TextFormatFlags.Right);
			if (flag3)
			{
				ChanceText.Draw(graphics, "→ " + ChanceText.Pct(oddsRow.Ahead), _sub, new Rectangle(Width - 130, num3 + 36, 112, 16), flag2 ? Theme.Amber : Theme.Muted, TextFormatFlags.Right);
			}
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_head.Dispose();
			_name.Dispose();
			_sub.Dispose();
			_pct.Dispose();
		}
		base.Dispose(disposing);
	}
}
