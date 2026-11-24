using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ChestCounter;

[SupportedOSPlatform("windows")]
public sealed class DailyChart : Control
{
	private IReadOnlyList<DayChests> _days = Array.Empty<DayChests>();

	public DailyChart()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Theme.Card;
	}

	public void SetDays(IReadOnlyList<DayChests> days)
	{
		_days = days;
		Invalidate();
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
		e.Graphics.Clear(BackColor);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
		if (_days.Count == 0)
		{
			return;
		}
		using Font font = new Font(Font, FontStyle.Bold);
		using Font font2 = new Font(Font, FontStyle.Regular);
		float height = font2.GetHeight(graphics);
		float num = font.GetHeight(graphics) + 2f;
		float num2 = height;
		int num3 = _days.Max((DayChests d) => d.Chests);
		float num4 = (float)Width / (float)_days.Count;
		float num5 = Math.Min(num4 - 6f, height * 1.75f);
		float val = num5 / 7f;
		float num6 = (float)Height - num2 - 4f;
		float num7 = Math.Max(0f, num6 - num - 6f);
		using SolidBrush solidBrush = new SolidBrush(Theme.Blue);
		using SolidBrush solidBrush2 = new SolidBrush(Theme.Cyan);
		using SolidBrush brush = new SolidBrush(Theme.Line);
		using SolidBrush solidBrush3 = new SolidBrush(Theme.Ink);
		using SolidBrush solidBrush4 = new SolidBrush(Theme.Muted);
		using StringFormat format = new StringFormat
		{
			Alignment = StringAlignment.Center,
			LineAlignment = StringAlignment.Far
		};
		using StringFormat format2 = new StringFormat
		{
			Alignment = StringAlignment.Center,
			LineAlignment = StringAlignment.Near
		};
		for (int num8 = 0; num8 < _days.Count; num8++)
		{
			DayChests dayChests = _days[num8];
			bool flag = num8 == _days.Count - 1;
			float num9 = (float)num8 * num4;
			float num10 = num9 + num4 / 2f;
			float num11 = ((num3 > 0) ? ((float)dayChests.Chests / (float)num3 * num7) : 0f);
			if (dayChests.Chests > 0)
			{
				num11 = Math.Max(num11, 4f);
				using GraphicsPath path = Shape.Rounded(new RectangleF(num10 - num5 / 2f, num6 - num11, num5, num11), Math.Min(val, num11 / 2f));
				graphics.FillPath(flag ? solidBrush2 : solidBrush, path);
			}
			else
			{
				graphics.FillRectangle(brush, num10 - num5 / 2f, num6 - 2f, num5, 2f);
			}
			graphics.DrawString(L.N(dayChests.Chests), font, solidBrush3, new RectangleF(num9, num6 - num11 - num - 2f, num4, num), format);
			string text = dayChests.Day.ToString("ddd", L.Culture);
			if (text.Length > 3)
			{
				text = text.Substring(0, 3);
			}
			graphics.DrawString(text, font2, flag ? solidBrush3 : solidBrush4, new RectangleF(num9, num6 + 4f, num4, num2), format2);
		}
	}
}
