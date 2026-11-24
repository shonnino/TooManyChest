using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ChestCounter;

[SupportedOSPlatform("windows")]
internal sealed class PlannerPanel : Control
{
	public sealed record Vm(string Status, bool Live, string WindowText, string PrizeName, double Chance, double Den, double Boost, bool Unknown);

	public static readonly int[] Options = new int[5] { 25, 50, 100, 200, 400 };

	private const int ChipW = 54;

	private const int ChipGap = 6;

	private readonly Font _label = Fonts.Body(7.5f, FontStyle.Bold);

	private readonly Font _chip = Theme.Text(9f, FontStyle.Bold);

	private readonly Font _value = Fonts.Display(24f);

	private readonly Font _sub = Theme.Text();

	private readonly Font _arrow = Theme.Text(16f, FontStyle.Bold);

	private Vm? _vm;

	private int _n = 100;

	private int _hover = -1;

	private double _normalShown;

	private double _normalTarget;

	private double _boostShown;

	private double _boostTarget;

	public event Action<int>? Chosen;

	public PlannerPanel()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		Height = 134;
	}

	public void SetState(Vm? vm, int chests)
	{
		_vm = vm;
		_n = chests;
		if (vm != null)
		{
			_normalTarget = WeeklyChanceModel.After(vm.Chance, vm.Den, 1.0, chests);
			_boostTarget = WeeklyChanceModel.After(vm.Chance, vm.Den, vm.Boost, chests);
		}
		Invalidate();
	}

	public void Snap()
	{
		_normalShown = _normalTarget;
		_boostShown = _boostTarget;
		Invalidate();
	}

	public bool Step()
	{
		bool moving = false;
		Ease(ref _normalShown, _normalTarget);
		Ease(ref _boostShown, _boostTarget);
		Invalidate();
		return moving;
		void Ease(ref double shown, double target)
		{
			double num = target - shown;
			if (Math.Abs(num) < 2E-05)
			{
				shown = target;
			}
			else
			{
				shown += num * 0.16;
				moving = true;
			}
		}
	}

	private static Rectangle ChipRect(int i)
	{
		return new Rectangle(20 + i * 60, 88, 54, 30);
	}

	private int ChipAt(Point p)
	{
		for (int i = 0; i < Options.Length; i++)
		{
			if (ChipRect(i).Contains(p))
			{
				return i;
			}
		}
		return -1;
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		int num = ChipAt(e.Location);
		if (num != _hover)
		{
			_hover = num;
			Cursor = ((num >= 0) ? Cursors.Hand : Cursors.Default);
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
			int num = ChipAt(e.Location);
			if (num >= 0)
			{
				Chosen?.Invoke(Options[num]);
			}
		}
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
		e.Graphics.Clear(Parent?.BackColor ?? Theme.Bg);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		if (Width < 300 || _vm == null)
		{
			return;
		}
		Vm vm = _vm;
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		Color color = (vm.Live ? Theme.Amber : Theme.Cyan);
		using (GraphicsPath path = Shape.Rounded(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 18f))
		{
			using SolidBrush brush = new SolidBrush(Theme.Card);
			using Pen pen = new Pen(Theme.CardEdge, 1f);
			graphics.FillPath(brush, path);
			graphics.DrawPath(pen, path);
		}
		ChanceText.ChipAt(graphics, vm.Status, _label, 20, 14, color);
		ChanceText.Draw(graphics, vm.WindowText, _sub, new Rectangle(20, 14, Width - 44, 28), Theme.Muted, TextFormatFlags.Right);
		ChanceText.Draw(graphics, Fonts.Track(WeeklyChanceText.T("IF YOU OPEN THIS MANY CHESTS").ToUpper(L.Culture)), _label, new Rectangle(20, 60, 340, 22), Theme.Muted);
		for (int i = 0; i < Options.Length; i++)
		{
			Rectangle rectangle = ChipRect(i);
			bool flag = Options[i] == _n;
			using GraphicsPath path2 = Shape.Rounded(rectangle, 10f);
			Color color2;
			if (flag)
			{
				color2 = Color.FromArgb(46, color);
			}
			else
			{
				color2 = ((i == _hover) ? Theme.PanelHover : Theme.Panel);
			}
			using SolidBrush brush2 = new SolidBrush(color2);
			using Pen pen2 = new Pen(flag ? color : Theme.CardEdge, flag ? 1.5f : 1f);
			graphics.FillPath(brush2, path2);
			graphics.DrawPath(pen2, path2);
			ChanceText.Draw(graphics, Options[i].ToString("N0"), _chip, rectangle, flag ? color : Theme.Ink, TextFormatFlags.HorizontalCenter);
		}
		int num = Width - 24 - 190;
		int num2 = num - 190 - 26;
		if (num2 < 20 + Options.Length * 60 + 10)
		{
			return;
		}
		ChanceText.Draw(graphics, Fonts.Track(WeeklyChanceText.T("NORMAL RATE").ToUpper(L.Culture)), _label, new Rectangle(num2, 52, 190, 18), Theme.Muted);
		ChanceText.Draw(graphics, ChanceText.Pct(_normalShown), _value, new Rectangle(num2, 70, 190, 32), Theme.Ink);
		ChanceText.Draw(graphics, "+" + ChanceText.Pct(_normalTarget - vm.Chance), _sub, new Rectangle(num2, 102, 190, 18), Theme.Muted);
		if (vm.Boost > 1.0)
		{
			ChanceText.Draw(graphics, "→", _arrow, new Rectangle(num2 + 190, 66, 26, 36), Theme.Muted, TextFormatFlags.HorizontalCenter);
			ChanceText.Draw(graphics, Fonts.Track(WeeklyChanceMore.F("TREASURE HOUR {0:0.#}×", vm.Boost)), _label, new Rectangle(num, 52, 190, 18), Theme.Amber);
			ChanceText.Draw(graphics, ChanceText.Pct(_boostShown), _value, new Rectangle(num, 70, 190, 32), Theme.Amber);
			ChanceText.Draw(graphics, "+" + ChanceText.Pct(_boostTarget - vm.Chance), _sub, new Rectangle(num, 102, 190, 18), Theme.Amber);
		}
		else if (vm.Unknown)
		{
			RectangleF rectangleF = new RectangleF(num, 62f, 44f, 44f);
			using (GraphicsPath path3 = Shape.Rounded(rectangleF, 12f))
			{
				using SolidBrush brush3 = new SolidBrush(Color.FromArgb(58, 51, 69));
				graphics.FillPath(brush3, path3);
			}
			ChanceText.Draw(graphics, "?", _value, Rectangle.Round(rectangleF), Color.FromArgb(196, 186, 209), TextFormatFlags.HorizontalCenter);
			ChanceText.Draw(graphics, "Boosts not announced yet", _sub, new Rectangle(num + 56, 62, 150, 44), Theme.Muted);
		}
		else
		{
			ChanceText.Draw(graphics, "No Treasure Hour boost for this prize", _sub, new Rectangle(num - 40, 70, 230, 32), Theme.Muted);
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_label.Dispose();
			_chip.Dispose();
			_value.Dispose();
			_sub.Dispose();
			_arrow.Dispose();
		}
		base.Dispose(disposing);
	}
}
