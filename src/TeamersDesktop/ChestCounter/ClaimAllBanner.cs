using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ChestCounter;

[SupportedOSPlatform("windows")]
internal sealed class ClaimAllBanner : Control, IPaintsBehindChildren
{
	private const int Pad = 18;

	private const int IconSize = 60;

	private const int Gap = 16;

	private const int ButtonWidth = 210;

	private const int ButtonHeight = 40;

	private readonly Font _tag = Fonts.Body(7.5f, FontStyle.Bold);

	private readonly Font _title = Theme.Text(11.5f, FontStyle.Bold);

	private readonly Font _sub = Theme.Text(8.5f);

	private readonly GlowButton _button;

	private readonly ToolTip _tip = new ToolTip();

	private Bitmap? _icon;

	private const TextFormatFlags Wrap = TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl | TextFormatFlags.WordBreak;

	private const TextFormatFlags Line = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;

	private static string Title => ClaimAllText.T("Already own the Pirate Shop Access Token? Claim the whole Item Shop with one button!");

	private static string Sub => ClaimAllText.T("Free for Chrome, Edge, Firefox and Safari. Never spends V-Bucks.");

	private static string Caption => Fonts.Track(ClaimAllText.T("Pirate Shop Claim All").ToUpper(L.Culture));

	public ClaimAllBanner(string rewardsFolder)
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Theme.Bg;
		Cursor = Cursors.Hand;
		try
		{
			_icon = RewardPictures.Load(rewardsFolder, "pirate-shop-access", 60, 14);
		}
		catch
		{
			_icon = null;
		}
		_button = new GlowButton
		{
			Text = ClaimAllText.T("Claim All"),
			Size = new Size(210, 40),
			Radius = 12,
			Font = Theme.Text(9.5f, FontStyle.Bold),
			BackColor = Theme.Accent,
			ForeColor = Theme.AccentInk,
			UseMnemonic = false,
			ShowSelectedBar = false
		};
		_button.FlatAppearance.BorderColor = Theme.Accent;
		_button.Click += (object? _, EventArgs _) =>
		{
			Open();
		};
		Controls.Add(_button);
		Click += (object? _, EventArgs _) =>
		{
			Open();
		};
		string caption = ClaimAllText.T("Opens chestcounter.io/claim in your browser");
		_tip.SetToolTip(this, caption);
		_tip.SetToolTip(_button, caption);
		Height = HeightFor(760);
	}

	private int TextWidth(int width)
	{
		return Math.Max(200, width - 36 - 60 - 16 - 210 - 16);
	}

	public int HeightFor(int width)
	{
		int width2 = TextWidth(width);
		int height = TextRenderer.MeasureText(Caption, _tag, new Size(width2, 100), TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Height;
		int height2 = TextRenderer.MeasureText(Title, _title, new Size(width2, 400), TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl | TextFormatFlags.WordBreak).Height;
		int height3 = TextRenderer.MeasureText(Sub, _sub, new Size(width2, 200), TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl | TextFormatFlags.WordBreak).Height;
		return Math.Max(96, 18 + height + 4 + height2 + 4 + height3 + 18);
	}

	protected override void OnLayout(LayoutEventArgs e)
	{
		base.OnLayout(e);
		_button.Location = new Point(Width - 18 - 210, (Height - 40) / 2);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.Clear(Theme.Bg);
		PaintSurface(graphics);
		if (_icon != null)
		{
			graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
			graphics.DrawImage(_icon, new Rectangle(18, (Height - 60) / 2, 60, 60));
		}
		int x = 94;
		int width = TextWidth(Width);
		int height = TextRenderer.MeasureText(Caption, _tag, new Size(width, 100), TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Height;
		int height2 = TextRenderer.MeasureText(Title, _title, new Size(width, 400), TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl | TextFormatFlags.WordBreak).Height;
		int height3 = TextRenderer.MeasureText(Sub, _sub, new Size(width, 200), TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl | TextFormatFlags.WordBreak).Height;
		int num = (Height - (height + 4 + height2 + 4 + height3)) / 2;
		TextRenderer.DrawText(graphics, Caption, _tag, new Rectangle(x, num, width, height), Theme.Amber, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
		num += height + 4;
		TextRenderer.DrawText(graphics, Title, _title, new Rectangle(x, num, width, height2), Theme.Ink, TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl | TextFormatFlags.WordBreak);
		num += height2 + 4;
		TextRenderer.DrawText(graphics, Sub, _sub, new Rectangle(x, num, width, height3), Theme.Muted, TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl | TextFormatFlags.WordBreak);
	}

	public void PaintSurface(Graphics g)
	{
		SmoothingMode smoothingMode = g.SmoothingMode;
		g.SmoothingMode = SmoothingMode.AntiAlias;
		RectangleF rectangleF = new RectangleF(0.5f, 0.5f, (float)Width - 1.5f, (float)Height - 1.5f);
		using (GraphicsPath path = Shape.Rounded(rectangleF, 16f))
		{
			using LinearGradientBrush brush = new LinearGradientBrush(rectangleF, Color.FromArgb(46, 16, 44), Color.FromArgb(24, 14, 32), LinearGradientMode.Horizontal);
			g.FillPath(brush, path);
			using LinearGradientBrush brush2 = new LinearGradientBrush(rectangleF, Color.FromArgb(70, 208, 0, 128), Color.FromArgb(0, 208, 0, 128), LinearGradientMode.Horizontal);
			g.FillPath(brush2, path);
			using Pen pen = new Pen(Theme.CardEdge, 1f);
			g.DrawPath(pen, path);
		}
		g.SmoothingMode = smoothingMode;
	}

	private void Open()
	{
		try
		{
			Process.Start(new ProcessStartInfo(ClaimAllText.PageUrl)
			{
				UseShellExecute = true
			})?.Dispose();
		}
		catch (Exception ex)
		{
			MessageBox.Show(FindForm(), ex.Message, "Chest Counter");
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_icon?.Dispose();
			_tag.Dispose();
			_title.Dispose();
			_sub.Dispose();
			_tip.Dispose();
		}
		base.Dispose(disposing);
	}
}
