using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ChestCounter;

[SupportedOSPlatform("windows")]
internal sealed class GoalPanel : Control
{
	public sealed record Vm(string Name, string Tag, bool Auto, double Chance, double Den, double Boost, int Chests, double ChestsToHalf, double GainNow, double GainNormal, int Ahead, Bitmap Image, string? LiveText);

	private static readonly (float X, float Y, float Size, float Phase, float Speed)[] Sparks = new (float, float, float, float, float)[7]
	{
		(0.06f, 0.1f, 8f, 0f, 1.1f),
		(0.95f, 0.18f, 10f, 1.7f, 0.9f),
		(0.5f, 0f, 6f, 3.1f, 1.3f),
		(1f, 0.62f, 8f, 4.2f, 1f),
		(0.02f, 0.72f, 9f, 2.4f, 1.2f),
		(0.84f, 1f, 6f, 5f, 0.8f),
		(0.3f, 1f, 5f, 0.9f, 1.4f)
	};

	private readonly Font _tag = Fonts.Body(7.5f, FontStyle.Bold);

	private readonly Font _title = Fonts.Display(22f);

	private readonly Font _big = Fonts.Display(38f);

	private readonly Font _sub = Theme.Text();

	private readonly Font _subBold = Theme.Text(9f, FontStyle.Bold);

	private readonly Font _small = Theme.Text(7.5f);

	private Vm? _vm;

	private string? _message;

	private double _shown;

	private double _target;

	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public bool Sparkle { get; set; } = true;

	public GoalPanel()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		Height = 244;
	}

	public void ShowGoal(Vm vm)
	{
		_vm = vm;
		_message = null;
		_target = vm.Chance;
		Invalidate();
	}

	public void ShowMessage(string message)
	{
		_vm = null;
		_message = message;
		Invalidate();
	}

	public void Snap()
	{
		_shown = _target;
		Invalidate();
	}

	public bool Step()
	{
		double num = _target - _shown;
		if (Math.Abs(num) < 2E-05)
		{
			if (_shown != _target)
			{
				_shown = _target;
				Invalidate();
			}
			return false;
		}
		_shown += num * 0.14;
		Invalidate();
		return true;
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
		e.Graphics.Clear(Parent?.BackColor ?? Theme.Bg);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		if (Width < 120)
		{
			return;
		}
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
		graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
		Vm? vm = _vm;
		bool flag = (object)vm != null && vm.Boost > 1.0;
		Color color = (flag ? Theme.Amber : Theme.Cyan);
		double num = (double)Environment.TickCount64 / 1000.0;
		using (GraphicsPath graphicsPath = Shape.Rounded(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 20f))
		{
			using SolidBrush brush = new SolidBrush(Theme.Card);
			graphics.FillPath(brush, graphicsPath);
			graphics.SetClip(graphicsPath);
			using (LinearGradientBrush brush2 = new LinearGradientBrush(new Rectangle(0, 0, Width, Height), Color.FromArgb(flag ? 56 : 38, color), Color.FromArgb(0, color), LinearGradientMode.ForwardDiagonal))
			{
				graphics.FillRectangle(brush2, 0, 0, Width, Height);
			}
			graphics.ResetClip();
			using Pen pen = new Pen(Theme.CardEdge, 1f);
			graphics.DrawPath(pen, graphicsPath);
		}
		if (_vm == null)
		{
			ChanceText.Draw(graphics, _message ?? "", _title, new Rectangle(24, 0, Width - 48, Height), Theme.Mint, TextFormatFlags.HorizontalCenter);
			return;
		}
		Vm vm2 = _vm;
		bool flag2 = vm2.Name.EndsWith(" (all time)", StringComparison.Ordinal);
		Rectangle rect = new Rectangle(22, 24, 190, 190);
		graphics.DrawImage(vm2.Image, rect);
		using (GraphicsPath path = Shape.Rounded(new RectangleF(rect.X, rect.Y, rect.Width, rect.Height), 16f))
		{
			using Pen pen2 = new Pen(Color.FromArgb(150, color), 2f);
			graphics.DrawPath(pen2, path);
		}
		if (Sparkle)
		{
			(float, float, float, float, float)[] sparks = Sparks;
			for (int i = 0; i < sparks.Length; i++)
			{
				(float, float, float, float, float) tuple = sparks[i];
				float item = tuple.Item1;
				float item2 = tuple.Item2;
				float item3 = tuple.Item3;
				float item4 = tuple.Item4;
				float item5 = tuple.Item5;
				double num2 = Math.Sin(num * (double)item5 * 2.2 + (double)item4);
				double num3 = ((num2 > 0.0) ? (num2 * num2) : 0.0);
				if (!(num3 < 0.02))
				{
					Color color2 = Color.FromArgb((int)(40.0 + 215.0 * num3), flag ? Theme.Amber : Color.White);
					ChanceText.Star(graphics, (float)rect.X + item * (float)rect.Width, (float)rect.Y + item2 * (float)rect.Height, (float)((double)item3 * (0.55 + 0.45 * num3)), color2);
				}
			}
		}
		int num4 = rect.Right + 26;
		int num5 = Width - 24;
		ChanceText.ChipAt(graphics, vm2.Tag, _tag, num4, 22, vm2.Auto ? Theme.Cyan : Theme.Amber);
		if (vm2.LiveText != null)
		{
			ChanceText.Chip(graphics, vm2.LiveText, _tag, num5, 22, Theme.Amber);
		}
		ChanceText.Draw(graphics, vm2.Name.ToUpperInvariant(), _title, new Rectangle(num4, 50, num5 - num4, 34), Theme.Ink);
		ChanceText.Draw(graphics, ChanceText.Pct(_shown), _big, new Rectangle(num4, 84, num5 - num4, 54), color);
		ChanceText.Draw(graphics, flag2 ? "cumulative chance all time" : "cumulative chance this week", _sub, new Rectangle(num4, 138, num5 - num4, 18), Theme.Muted);
		ChanceText.Draw(graphics, flag2 ? WeeklyChanceMore.F("1 in {0:N0} per chest   ·   {1:N0} chests all time", vm2.Den, vm2.Chests) : WeeklyChanceMore.F("1 in {0:N0} per chest   ·   {1:N0} chests opened this week", vm2.Den, vm2.Chests), _sub, new Rectangle(num4, 158, num5 - num4, 18), Theme.Ink);
		ChanceText.Draw(graphics, (vm2.ChestsToHalf <= 0.0) ? "Past the 50% mark" : WeeklyChanceMore.F("About {0:N0} more chests to reach 50%", Math.Ceiling(vm2.ChestsToHalf)), _subBold, new Rectangle(num4, 178, num5 - num4, 18), (vm2.ChestsToHalf <= 0.0) ? Theme.Mint : Theme.Muted);
		string text = ((vm2.Boost > 1.0) ? WeeklyChanceMore.F("If you open {0:N0} more chests: +{1} now (Treasure Hour) vs +{2} normally", vm2.Ahead, ChanceText.Pct(vm2.GainNow), ChanceText.Pct(vm2.GainNormal)) : WeeklyChanceMore.F("If you open {0:N0} more chests: +{1}", vm2.Ahead, ChanceText.Pct(vm2.GainNormal)));
		ChanceText.Draw(graphics, text, _sub, new Rectangle(num4, 138, num5 - num4, 18), (vm2.Boost > 1.0) ? Theme.Amber : Theme.Muted, TextFormatFlags.Right);
		int num6 = num4 + 7;
		int num7 = num5 - 8;
		RectangleF r = new RectangleF(num6, 206f, num7 - num6, 10f);
		using (GraphicsPath path2 = Shape.Rounded(r, 5f))
		{
			using SolidBrush brush3 = new SolidBrush(Theme.Panel);
			graphics.FillPath(brush3, path2);
		}
		double num8 = Math.Clamp(_shown, 0.0, 1.0);
		float num9 = (float)((double)r.Width * num8);
		if (num8 > 0.0005 && num9 > 0.5f)
		{
			RectangleF rectangleF = new RectangleF(r.X, r.Y, Math.Max(num9, 10f), r.Height);
			using GraphicsPath graphicsPath2 = Shape.Rounded(rectangleF, 5f);
			using (LinearGradientBrush brush4 = new LinearGradientBrush(rectangleF, Theme.Cyan, Theme.Mint, LinearGradientMode.Horizontal))
			{
				graphics.FillPath(brush4, graphicsPath2);
			}
			if (Sparkle && rectangleF.Width > 24f)
			{
				float num10 = (float)(num % 2.6 / 2.6);
				float x = rectangleF.X - 40f + num10 * (rectangleF.Width + 80f);
				graphics.SetClip(graphicsPath2);
				LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new RectangleF(x, rectangleF.Y, 40f, rectangleF.Height), Color.FromArgb(0, Color.White), Color.FromArgb(0, Color.White), LinearGradientMode.Horizontal);
				linearGradientBrush.InterpolationColors = new ColorBlend
				{
					Positions = new float[3] { 0f, 0.5f, 1f },
					Colors = new Color[3]
					{
						Color.FromArgb(0, Color.White),
						Color.FromArgb(110, Color.White),
						Color.FromArgb(0, Color.White)
					}
				};
				using LinearGradientBrush brush5 = linearGradientBrush;
				graphics.FillRectangle(brush5, x, rectangleF.Y, 40f, rectangleF.Height);
				graphics.ResetClip();
			}
		}
		(double, string)[] array = new (double, string)[5]
		{
			(0.0, "0"),
			(0.25, "25%"),
			(0.5, "50%"),
			(0.75, "75%"),
			(1.0, "100%")
		};
		for (int i = 0; i < array.Length; i++)
		{
			(double, string) tuple2 = array[i];
			double item6 = tuple2.Item1;
			string item7 = tuple2.Item2;
			float num11 = r.X + (float)((double)r.Width * item6);
			bool flag3 = num8 >= item6 - 1E-09 && item6 > 0.0;
			RectangleF rect2 = new RectangleF(num11 - 7f, r.Y + r.Height / 2f - 7f, 14f, 14f);
			using (SolidBrush brush6 = new SolidBrush(flag3 ? Theme.Mint : Theme.Card))
			{
				using Pen pen3 = new Pen(flag3 ? Theme.Mint : Theme.Line, 2f);
				graphics.FillEllipse(brush6, rect2);
				graphics.DrawEllipse(pen3, rect2);
			}
			ChanceText.Draw(graphics, item7, _small, new Rectangle((int)num11 - 30, 222, 60, 14), flag3 ? Theme.Mint : Theme.Muted, TextFormatFlags.HorizontalCenter);
		}
		if (!(num8 > 0.003) || !(num8 < 1.0))
		{
			return;
		}
		float num12 = r.X + (float)((double)r.Width * num8);
		RectangleF rect3 = new RectangleF(num12 - 8f, r.Y + r.Height / 2f - 8f, 16f, 16f);
		using SolidBrush brush7 = new SolidBrush(Color.FromArgb(70, color));
		graphics.FillEllipse(brush7, rect3.X - 4f, rect3.Y - 4f, rect3.Width + 8f, rect3.Height + 8f);
		using SolidBrush brush8 = new SolidBrush(Theme.Ink);
		graphics.FillEllipse(brush8, rect3);
		using Pen pen4 = new Pen(color, 2.5f);
		graphics.DrawEllipse(pen4, rect3);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_tag.Dispose();
			_title.Dispose();
			_big.Dispose();
			_sub.Dispose();
			_subBold.Dispose();
			_small.Dispose();
		}
		base.Dispose(disposing);
	}
}
