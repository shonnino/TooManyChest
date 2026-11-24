using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ChestCounter;

internal sealed class ChestStatsPanel : Control
{
	private sealed record Row(DateTime When, int Opened, int Broken, int Mates, int RankMates, int? Seconds);

	private sealed record Data(List<Row> Rows);

	private sealed record Bar(DateTime When, int You, int Broken, int Mates, int? Seconds = null)
	{
		public int Total => You + Mates;

		public double PerMinute
		{
			get
			{
				int? seconds = Seconds;
				if (!seconds.HasValue || seconds.GetValueOrDefault() <= 0)
				{
					return 0.0;
				}
				return (double)Total * 60.0 / (double)Seconds.Value;
			}
		}
	}

	private readonly Monitor _monitor;

	private readonly Store _store;

	private Data _data = new Data(new List<Row>());

	private RankResult? _rank;

	private bool _showTeammates;
	private bool _preferTeammates;
	private bool WithMates => _showTeammates && _preferTeammates;

	private bool _modeChosen;

	private bool _perDay;

	private string _signature = "";

	private int _loading;

	private int _hover = -1;

	private const int NoFocus = -1;

	private const int FocusYou = 0;

	private const int FocusMates = 1;

	private const int FocusAverage = 2;

	private const int FocusBest = 3;

	private const int FocusRate = 4;

	private int _focus = -1;

	private int _hoverTile = -1;

	private int _hoverLink = -1;

	private readonly Rectangle[] _tileBoxes = new Rectangle[5];

	private readonly Rectangle[] _linkBoxes = new Rectangle[5];

	private readonly ToolTip _tip = new ToolTip
	{
		InitialDelay = 400,
		ShowAlways = true
	};

	private const string LeaderboardUrl = "https://chestcounter.io/leaderboard";

	private readonly List<(Rectangle Box, Action Click)> _hits = new List<(Rectangle, Action)>();

	private readonly List<(float X, float Width)> _columns = new List<(float, float)>();

	private Rectangle _plot;

	private readonly Font _tileValue = Fonts.Display(24f);

	private readonly Font _tileLabel = Theme.Text(8.5f, FontStyle.Bold);

	private readonly Font _small = Theme.Text(8.5f);

	private readonly Font _smallBold = Theme.Text(8.5f, FontStyle.Bold);

	private readonly Font _pill = Theme.Text(8.5f, FontStyle.Bold);

	private readonly Font _title = Fonts.Display(14f);

	private readonly Font _icon = new Font("Segoe UI Emoji", 10f);

	private static readonly Color[] TileColours = new Color[5]
	{
		Theme.Accent,
		Color.FromArgb(176, 92, 160),
		Theme.AccentSoft,
		Theme.Amber,
		Theme.Mint
	};

	private static readonly string[] Icons = new string[5] { "\ud83d\udc64", "\ud83d\udc65", "\ud83c\udfaf", "\ud83c\udfc6", "⏱" };

	private static readonly Color IconGrey = Color.FromArgb(128, 112, 124);

	private static readonly Color YouBar = Theme.Accent;

	private static readonly Color BrokenBar = Theme.AccentSoft;

	private static readonly Color MateBar = Color.FromArgb(120, 72, 112);

	private static readonly Color MateFocusBar = Color.FromArgb(196, 124, 182);

	private static readonly Dictionary<(Font, char), int> CharWidths = new Dictionary<(Font, char), int>();

	private bool HasEmojiFont => string.Equals(_icon.Name, "Segoe UI Emoji", StringComparison.OrdinalIgnoreCase);

	private float S => (float)DeviceDpi / 96f;

	public ChestStatsPanel(Monitor monitor, Store store)
	{
		_monitor = monitor;
		_store = store;
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Theme.Bg;
		Dock = DockStyle.Top;
		Height = 560;
		Margin = new Padding(0, 12, 0, 12);
		Cursor = Cursors.Default;
		try
		{
			_rank = Rankings.Cached(_store.Folder);
		}
		catch (Exception error)
		{
			ErrorLog.Write("Player rankings", error);
		}
	}

	private int P(float v)
	{
		return (int)Math.Round(v * S);
	}

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		Height = P(560f);
	}

	protected override void OnDpiChangedAfterParent(EventArgs e)
	{
		base.OnDpiChangedAfterParent(e);
		Height = P(560f);
	}

	public void Refresh(MonitorState s)
	{
		Config c = _monitor.Config;
		if (_showTeammates != s.ShowHistoryTeammates)
		{
			_showTeammates = s.ShowHistoryTeammates;
			Invalidate();
		}
		string text = $"{s.CurrentKey}|{s.Stats.HistoryMatches}|{s.Stats.LastMatch?.Chests}|{string.Join(",", c.ExtraKinds())}|{c.CountFrom:O}|{TeammateReplayImports.Revision}";
		if (text == _signature)
		{
			if (_data.Rows.Count > 0)
			{
				Rank(_data);
			}
		}
		else
		{
			if (Interlocked.Exchange(ref _loading, 1) == 1)
			{
				return;
			}
			_signature = text;
			Task.Run(() =>
			{
				try
				{
					IReadOnlyCollection<string> kinds = c.ExtraKinds();
					return new Data((from m in Profiles.View(_store.LoadData(), c).Matches
						where m.IsBattleRoyale
						orderby m.MatchStartUtc ?? m.AnalyzedAtUtc
						select m).Select((MatchRecord m) =>
					{
						int num = Math.Max(0, m.OwnChests);
						int num2 = Math.Min(num, Enumerable.Count(m.Chests, (ChestRecord x) => x.Destroyed));
						DateTime when = (m.MatchStartUtc ?? m.AnalyzedAtUtc).ToLocalTime();
						int opened = num - num2;
						int mates = Math.Max(0, m.TeammatesSeen(kinds));
						int? durationSeconds = m.DurationSeconds;
						return new Row(when, opened, num2, mates, m.ReplayTeammatesSeen(kinds), (durationSeconds.HasValue && durationSeconds.GetValueOrDefault() > 0) ? m.DurationSeconds : ((int?)null));
					}).ToList());
				}
				catch (Exception error)
				{
					ErrorLog.Write("Dashboard chest statistics", error);
					return (Data)null;
				}
			}).ContinueWith((Task<Data> t) =>
			{
				Interlocked.Exchange(ref _loading, 0);
				Data d = t.Result;
				if ((object)d != null)
				{
					OnUi(() =>
					{
						_data = d;
						if (!_modeChosen)
						{
							_preferTeammates = d.Rows.Any((Row r) => r.Mates > 0);
						}
						_hover = -1;
						Invalidate();
						Rank(d);
					});
				}
			});
		}
	}

	private void OnUi(Action action)
	{
		if (IsDisposed || !IsHandleCreated)
		{
			return;
		}
		try
		{
			BeginInvoke(action);
		}
		catch (InvalidOperationException)
		{
		}
	}

	private static RankFigures Figures(List<Row> rows)
	{
		List<Row> source = rows.Where((Row r) => r.Seconds.HasValue).ToList();
		double num = (double)source.Sum((Row r) => r.Seconds.Value) / 60.0;
		return new RankFigures(rows.Count, ((IEnumerable<Row>)rows).Sum((Func<Row, int>)You), rows.Sum((Row r) => r.Mates), (rows.Count > 0) ? ((IEnumerable<Row>)rows).Average((Func<Row, int>)You) : 0.0, (rows.Count > 0) ? rows.Average((Row r) => You(r) + r.Mates) : 0.0, (rows.Count > 0) ? ((IEnumerable<Row>)rows).Max((Func<Row, int>)You) : 0, (rows.Count > 0) ? rows.Max((Row r) => You(r) + r.Mates) : 0, (num > 0.0) ? new double?((double)((IEnumerable<Row>)source).Sum((Func<Row, int>)You) / num) : ((double?)null), (num > 0.0) ? new double?((double)source.Sum((Row r) => You(r) + r.Mates) / num) : ((double?)null));
		static int You(Row r)
		{
			return r.Opened + r.Broken;
		}
	}

	private void Rank(Data d)
	{
		string folder = _store.Folder;
		RankFigures f = Figures(d.Rows.Select(row => row with { Mates = row.RankMates }).ToList());
		if (!Rankings.Due(folder, f))
		{
			return;
		}
		Rankings.Fetch(folder, f).ContinueWith((Task<RankResult?> t) =>
		{
			if (t.Status == TaskStatus.RanToCompletion)
			{
				RankResult r = t.Result;
				if (r != null)
				{
					OnUi(() =>
					{
						_rank = r;
						Invalidate();
					});
				}
			}
		});
	}

	private void ToggleRankings()
	{
		bool flag = !Rankings.Enabled(_store.Folder);
		Rankings.SetEnabled(_store.Folder, flag);
		_rank = null;
		if (flag && _data.Rows.Count > 0)
		{
			Rank(_data);
		}
	}

	private (int Top, int Players)? RankOf(string key)
	{
		if (_rank == null || !_rank.Top.TryGetValue(key, out var value))
		{
			return null;
		}
		_rank.Players.TryGetValue(key, out var value2);
		if (value2 < 2)
		{
			return null;
		}
		return (Math.Clamp(value, 1, 100), value2);
	}

	private static Color RankColour(int top)
	{
		return FromHsl(120.0 * (double)(100 - Math.Clamp(top, 1, 100)) / 99.0, 0.78, 0.55);
	}

	private static Color FromHsl(double h, double s, double l)
	{
		double num = (1.0 - Math.Abs(2.0 * l - 1.0)) * s;
		double num2 = num * (1.0 - Math.Abs(h / 60.0 % 2.0 - 1.0));
		double num3 = l - num / 2.0;
		double num4;
		double num5;
		double num6;
		if (!(h < 60.0))
		{
			(num4, num5, num6) = ((h < 120.0) ? (num2, num, 0.0) : (0.0, num, num2));
		}
		else
		{
			double num7 = num2;
			num6 = 0.0;
			num5 = num7;
			num4 = num;
		}
		return Color.FromArgb((int)Math.Round((num4 + num3) * 255.0), (int)Math.Round((num5 + num3) * 255.0), (int)Math.Round((num6 + num3) * 255.0));
	}

	private int Total(Row r)
	{
		return r.Opened + r.Broken + (WithMates ? r.Mates : 0);
	}

	protected override void OnMouseClick(MouseEventArgs e)
	{
		base.OnMouseClick(e);
		foreach (var (rectangle, action) in _hits)
		{
			if (rectangle.Contains(e.Location))
			{
				action();
				_hover = -1;
				Invalidate();
				break;
			}
		}
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		bool flag = _hits.Any(((Rectangle Box, Action Click) h) => h.Box.Contains(e.Location));
		if (Cursor == Cursors.Hand != flag)
		{
			Cursor = (flag ? Cursors.Hand : Cursors.Default);
		}
		int num = Array.FindIndex(_tileBoxes, (Rectangle b) => b.Contains(e.Location));
		int num2 = Array.FindIndex(_linkBoxes, (Rectangle b) => !b.IsEmpty && b.Contains(e.Location));
		if (num2 != _hoverLink)
		{
			_tip.SetToolTip(this, (num2 >= 0) ? StatsText.T("See the leaderboard on chestcounter.io") : null);
		}
		if (num != _hoverTile || num2 != _hoverLink)
		{
			_hoverTile = num;
			_hoverLink = num2;
			Invalidate();
		}
		int num3 = -1;
		if (_plot.Contains(e.Location))
		{
			for (int num4 = 0; num4 < _columns.Count; num4++)
			{
				if ((float)e.X >= _columns[num4].X && (float)e.X < _columns[num4].X + _columns[num4].Width)
				{
					num3 = num4;
					break;
				}
			}
		}
		if (num3 != _hover)
		{
			_hover = num3;
			Invalidate();
		}
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		base.OnMouseLeave(e);
		if (_hoverLink != -1)
		{
			_tip.SetToolTip(this, null);
		}
		if (_hover != -1 || _hoverTile != -1 || _hoverLink != -1)
		{
			_hover = -1;
			_hoverTile = -1;
			_hoverLink = -1;
			Invalidate();
		}
	}

	private void SelectTile(int i)
	{
		if (i == 1 && !_showTeammates) return;
		if (_focus == i)
		{
			_focus = -1;
			return;
		}
		_focus = i;
		bool flag;
		switch (i)
		{
		case 0:
			_preferTeammates = false;
			_modeChosen = true;
			return;
		case 1:
			_preferTeammates = true;
			_modeChosen = true;
			return;
		case 2:
		case 3:
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			_perDay = false;
		}
	}

	private static void OpenLeaderboard()
	{
		try
		{
			Process.Start(new ProcessStartInfo("https://chestcounter.io/leaderboard")
			{
				UseShellExecute = true
			})?.Dispose();
		}
		catch (Exception error)
		{
			ErrorLog.Write("Open leaderboard", error);
		}
	}

	private static GraphicsPath Round(Rectangle r, int radius)
	{
		return Round(r, radius, bottom: true);
	}

	private static GraphicsPath Round(RectangleF r, float radius, bool bottom)
	{
		GraphicsPath graphicsPath = new GraphicsPath();
		float num = Math.Max(1f, Math.Min(radius * 2f, Math.Min(r.Width, r.Height)));
		graphicsPath.AddArc(r.X, r.Y, num, num, 180f, 90f);
		graphicsPath.AddArc(r.Right - num, r.Y, num, num, 270f, 90f);
		if (bottom)
		{
			graphicsPath.AddArc(r.Right - num, r.Bottom - num, num, num, 0f, 90f);
			graphicsPath.AddArc(r.X, r.Bottom - num, num, num, 90f, 90f);
		}
		else
		{
			graphicsPath.AddLine(r.Right, r.Bottom, r.X, r.Bottom);
		}
		graphicsPath.CloseFigure();
		return graphicsPath;
	}

	private new static void Text(Graphics g, string text, Font font, Rectangle box, Color colour, TextFormatFlags extra = TextFormatFlags.Default)
	{
		TextRenderer.DrawText(g, text, font, box, colour, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | extra);
	}

	private static int Measure(Graphics g, string text, Font font)
	{
		return TextRenderer.MeasureText(g, text, font, Size.Empty, TextFormatFlags.NoPadding).Width;
	}

	private static int CharWidth(Graphics g, Font font, char c)
	{
		if (!CharWidths.TryGetValue((font, c), out var value))
		{
			value = TextRenderer.MeasureText(g, c.ToString(), font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;
			CharWidths[(font, c)] = value;
		}
		return value;
	}

	private static float Em(Graphics g, Font font)
	{
		return font.SizeInPoints * g.DpiY / 72f;
	}

	private static int LetterGap(Graphics g, Font font)
	{
		return Math.Max(1, (int)Math.Round(Em(g, font) * 0.09f));
	}

	private static int WordGap(Graphics g, Font font)
	{
		return Math.Max(3, (int)Math.Round(Em(g, font) * 0.42f));
	}

	private static string Upper(string text)
	{
		return text.ToUpper(L.Culture).Trim();
	}

	private static int CapsWidthRaw(Graphics g, string s, Font font)
	{
		int num = 0;
		int num2 = LetterGap(g, font);
		int num3 = WordGap(g, font);
		for (int i = 0; i < s.Length; i++)
		{
			if (char.IsWhiteSpace(s[i]))
			{
				num += num3;
				continue;
			}
			num += CharWidth(g, font, s[i]);
			if (i < s.Length - 1 && !char.IsWhiteSpace(s[i + 1]))
			{
				num += num2;
			}
		}
		return num;
	}

	private static int CapsWidth(Graphics g, string text, Font font)
	{
		return CapsWidthRaw(g, Upper(text), font);
	}

	private static void Caps(Graphics g, string text, Font font, Rectangle box, Color colour)
	{
		string text2 = Upper(text);
		if (CapsWidthRaw(g, text2, font) > box.Width)
		{
			while (text2.Length > 1 && CapsWidthRaw(g, text2 + "…", font) > box.Width)
			{
				string text3 = text2;
				text2 = text3.Substring(0, text3.Length - 1).TrimEnd();
			}
			text2 += "…";
		}
		int num = box.X;
		int num2 = LetterGap(g, font);
		int num3 = WordGap(g, font);
		for (int i = 0; i < text2.Length; i++)
		{
			if (char.IsWhiteSpace(text2[i]))
			{
				num += num3;
				continue;
			}
			int num4 = CharWidth(g, font, text2[i]);
			TextRenderer.DrawText(g, text2[i].ToString(), font, new Rectangle(num, box.Y, num4 + 2, box.Height), colour, TextFormatFlags.NoClipping | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
			num += num4 + ((i < text2.Length - 1 && !char.IsWhiteSpace(text2[i + 1])) ? num2 : 0);
		}
	}

	private int Pill(Graphics g, int x, int y, string text, string? badge, bool on, Action click)
	{
		int num = CapsWidth(g, text, _pill) + P(24f);
		int num2 = ((badge != null) ? (Measure(g, badge, _small) + P(14f)) : 0);
		Rectangle rectangle = new Rectangle(x, y, num + num2 + ((badge != null) ? P(6f) : 0), P(32f));
		using (GraphicsPath path = Round(rectangle, P(8f)))
		{
			using SolidBrush brush = new SolidBrush(on ? Theme.Accent : Theme.Panel);
			g.FillPath(brush, path);
		}
		Caps(g, text, _pill, new Rectangle(x + P(12f), y, num, rectangle.Height), on ? Theme.AccentInk : Theme.Muted);
		if (badge != null)
		{
			Rectangle rectangle2 = new Rectangle(x + num + P(2f), y + P(7f), num2, P(18f));
			using (GraphicsPath path2 = Round(rectangle2, P(5f)))
			{
				using SolidBrush brush2 = new SolidBrush(on ? Theme.AccentHover : Theme.Card);
				g.FillPath(brush2, path2);
			}
			Text(g, badge, _small, rectangle2, on ? Theme.AccentInk : Theme.Muted, TextFormatFlags.HorizontalCenter);
		}
		_hits.Add((rectangle, click));
		return rectangle.Right;
	}

	private static void Star(Graphics g, float cx, float cy, float r)
	{
		PointF[] array = new PointF[10];
		for (int i = 0; i < 10; i++)
		{
			double num = -Math.PI / 2.0 + (double)i * Math.PI / 5.0;
			double num2 = ((i % 2 == 0) ? ((double)r) : ((double)r * 0.45));
			array[i] = new PointF(cx + (float)(num2 * Math.Cos(num)), cy + (float)(num2 * Math.Sin(num)));
		}
		using SolidBrush brush = new SolidBrush(Theme.Amber);
		g.FillPolygon(brush, array);
	}

	private static double NiceStep(double max)
	{
		double num = Math.Max(0.2, max) / 4.0;
		double num2 = Math.Pow(10.0, Math.Floor(Math.Log10(num)));
		double[] array = new double[5] { 1.0, 2.0, 2.5, 5.0, 10.0 };
		foreach (double num3 in array)
		{
			if (num <= num3 * num2 + 1E-09)
			{
				return num3 * num2;
			}
		}
		return 10.0 * num2;
	}

	private static int NiceStep(int max)
	{
		double num = (double)Math.Max(1, max) / 4.0;
		double num2 = Math.Pow(10.0, Math.Floor(Math.Log10(num)));
		double[] array = new double[5] { 1.0, 2.0, 2.5, 5.0, 10.0 };
		foreach (double num3 in array)
		{
			if (num <= num3 * num2)
			{
				return Math.Max(1, (int)Math.Ceiling(num3 * num2));
			}
		}
		return Math.Max(1, (int)Math.Ceiling(10.0 * num2));
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.Clear(BackColor);
		_hits.Clear();
		_columns.Clear();
		_plot = Rectangle.Empty;
		List<Row> rows = _data.Rows;
		int num = rows.Sum((Row r) => r.Opened);
		int num2 = rows.Sum((Row r) => r.Broken);
		int num3 = num + num2;
		int num4 = rows.Sum((Row r) => r.Mates);
		CultureInfo culture = L.Culture;
		int num5 = Pill(graphics, 0, 0, StatsText.T("Just you"), num3.ToString("N0", culture), !WithMates, () =>
		{
			_preferTeammates = false;
			_modeChosen = true;
			if (_focus == 1)
			{
				_focus = -1;
			}
		});
		if (_showTeammates) Pill(graphics, num5 + P(8f), 0, StatsText.T("With teammates"), (num3 + num4).ToString("N0", culture), WithMates, () =>
		{
			_preferTeammates = true;
			_modeChosen = true;
			if (_focus == 0)
			{
				_focus = -1;
			}
		});
		DrawRankingsToggle(graphics);
		int num6 = P(46f);
		int num7 = P(12f);
		int num8 = P(150f);
		int[] visibleTiles = _showTeammates ? [0, 1, 2, 3, 4] : [0, 2, 3, 4];
		int num10 = (Width - num7 * (visibleTiles.Length - 1)) / visibleTiles.Length;
		double num11 = ((rows.Count > 0) ? ((IEnumerable<Row>)rows).Average((Func<Row, int>)Total) : 0.0);
		Row row = ((rows.Count > 0) ? rows.MaxBy(Total) : null);
		List<Row> source = rows.Where((Row r) => r.Seconds.HasValue).ToList();
		double num12 = (double)source.Sum((Row r) => r.Seconds.Value) / 60.0;
		double num13 = ((num12 > 0.0) ? ((double)((IEnumerable<Row>)source).Sum((Func<Row, int>)Total) / num12) : 0.0);
		string text = (WithMates ? "All" : "");
		(string, string, string, bool, string)[] array = new (string, string, string, bool, string)[5]
		{
			(num3.ToString("N0", culture), StatsText.T("By you"), StatsText.F("{0:N0} opened · {1:N0} broken", num, num2), false, "total"),
			(num4.ToString("N0", culture), StatsText.T("Teammate chests"), L.T("según los replays disponibles", "from available replays"), !WithMates, "mates"),
			(num11.ToString("0.0", culture), StatsText.T("Per match"), StatsText.T("average"), false, "avg" + text),
			(((!(row == null)) ? Total(row) : 0).ToString("N0", culture), StatsText.T("Best match"), (row == null) ? "—" : row.When.ToString(L.En ? "d MMM yyyy" : "d", culture), false, "best" + text),
			(num13.ToString("0.0", culture), StatsText.T("Per minute"), StatsText.T("of match time"), false, "pm" + text)
		};
		Array.Clear(_tileBoxes);
		Array.Clear(_linkBoxes);
		for (int position = 0; position < visibleTiles.Length; position++)
		{
			int tile = visibleTiles[position];
			DrawTile(graphics, new Rectangle(position * (num10 + num7), num6, num10, num8), tile, array[tile], (tile == 0 && num3 > 0) ? new double?((double)num / (double)num3) : ((double?)null));
		}
		if (_focus == 1 && !WithMates)
		{
			_focus = -1;
		}
		DrawChart(graphics, new Rectangle(0, num6 + num8 + P(14f), Width - 1, Height - (num6 + num8 + P(14f)) - 1), rows);
	}

	private void DrawRankingsToggle(Graphics g)
	{
		bool flag = Rankings.Enabled(_store.Folder);
		int num = _rank?.Players.Values.DefaultIfEmpty(0).Max() ?? 0;
		string text;
		if (flag)
		{
			if (_rank == null)
			{
				text = StatsText.T("Connecting…");
			}
			else
			{
				text = ((num < 2) ? StatsText.T("Not enough players yet") : StatsText.F("{0:N0} players", num));
			}
		}
		else
		{
			text = StatsText.T("Off");
		}
		string text2 = StatsText.T("Player rankings");
		int num2 = CapsWidth(g, text2, _pill);
		int num3 = Measure(g, text, _small);
		int num4 = num2 + num3 + P(46f);
		int num5 = P(32f);
		Rectangle rectangle = new Rectangle(Width - num4 - 1, 0, num4, num5);
		using (GraphicsPath path = Round(rectangle, P(8f)))
		{
			using SolidBrush brush = new SolidBrush(Theme.Card);
			g.FillPath(brush, path);
			using Pen pen = new Pen(flag ? Theme.Accent : Theme.CardEdge);
			g.DrawPath(pen, path);
		}
		Rectangle rect = new Rectangle(rectangle.X + P(12f), rectangle.Y + num5 / 2 - P(4f), P(8f), P(8f));
		using (SolidBrush brush2 = new SolidBrush(flag ? Theme.Mint : Theme.Muted))
		{
			g.FillEllipse(brush2, rect);
		}
		Caps(g, text2, _pill, new Rectangle(rect.Right + P(8f), rectangle.Y, num2 + P(4f), num5), flag ? Theme.Ink : Theme.Muted);
		Text(g, text, _small, new Rectangle(rect.Right + P(8f) + num2 + P(10f), rectangle.Y, num3 + P(4f), num5), Theme.Muted);
		_hits.Add((rectangle, ToggleRankings));
	}

	private void DrawTile(Graphics g, Rectangle box, int i, (string Value, string Label, string Foot, bool Dim, string Key) t, double? openedShare)
	{
		_ = L.Culture;
		(int, int)? tuple = RankOf(t.Key);
		_tileBoxes[i] = box;
		_linkBoxes[i] = Rectangle.Empty;
		bool flag = _focus == i;
		bool flag2 = _hoverTile == i;
		if (flag2 && !flag)
		{
			box.Offset(0, -P(2f));
		}
		Color color = ((!tuple.HasValue) ? TileColours[i] : RankColour(tuple.GetValueOrDefault().Item1));
		Color color2 = (t.Dim ? Color.FromArgb(90, color) : color);
		using (GraphicsPath graphicsPath = Round(box, P(10f)))
		{
			using (SolidBrush brush = new SolidBrush(Theme.Card))
			{
				g.FillPath(brush, graphicsPath);
			}
			g.SetClip(graphicsPath);
			using (LinearGradientBrush brush2 = new LinearGradientBrush(box, Color.FromArgb(t.Dim ? 14 : 44, color), Color.FromArgb(0, color), 90f))
			{
				g.FillRectangle(brush2, box);
			}
			using (SolidBrush brush3 = new SolidBrush(color2))
			{
				g.FillRectangle(brush3, box.X, box.Y, box.Width, P(3f));
			}
			g.ResetClip();
			Color color3;
			if (flag)
			{
				color3 = Theme.AccentSoft;
			}
			else if (tuple.HasValue && !t.Dim)
			{
				color3 = Color.FromArgb(flag2 ? 200 : 110, color);
			}
			else
			{
				color3 = (flag2 ? Theme.Accent : Theme.CardEdge);
			}
			using Pen pen = new Pen(color3, (!flag) ? 1 : Math.Max(2, P(2f)));
			g.DrawPath(pen, graphicsPath);
		}
		int num = P(14f);
		int width = box.Width - num * 2;
		Text(g, t.Value, _tileValue, new Rectangle(box.X + num, box.Y + P(12f), width, P(42f)), t.Dim ? Theme.Muted : color2);
		int num2 = box.X + num;
		if (HasEmojiFont)
		{
			Rectangle bounds = new Rectangle(box.X + num - P(1f), box.Y + P(56f), P(20f), P(20f));
			TextRenderer.DrawText(g, Icons[i], _icon, bounds, IconGrey, TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
			num2 = bounds.Right + P(5f);
		}
		Caps(g, t.Label, _tileLabel, new Rectangle(num2, box.Y + P(56f), box.Right - num - num2, P(20f)), Theme.Ink);
		Text(g, t.Foot, _small, new Rectangle(box.X + num, box.Y + P(80f), width, P(18f)), Theme.Muted);
		Rectangle box2 = new Rectangle(box.X + num, box.Y + P(104f), width, P(18f));
		if (tuple.HasValue)
		{
			(int, int) valueOrDefault = tuple.GetValueOrDefault();
			string text = StatsText.F("You're in the top {0}%", valueOrDefault.Item1);
			Color color4 = (t.Dim ? Theme.Muted : color);
			Text(g, text, _smallBold, box2, color4);
			int width2 = Math.Min(box2.Width, Measure(g, text, _smallBold));
			Rectangle rectangle = new Rectangle(box2.X, box2.Y, width2, box2.Height);
			if (_hoverLink == i)
			{
				using Pen pen2 = new Pen(color4, Math.Max(1, P(1f)));
				g.DrawLine(pen2, rectangle.X, rectangle.Bottom - P(2f), rectangle.Right, rectangle.Bottom - P(2f));
			}
			if (flag2 && !flag)
			{
				rectangle.Offset(0, P(2f));
			}
			_linkBoxes[i] = rectangle;
			_hits.Add((rectangle, OpenLeaderboard));
		}
		else if (Rankings.Enabled(_store.Folder) && _rank != null)
		{
			Text(g, StatsText.T("Not enough players yet"), _small, box2, Theme.Muted);
		}
		if (openedShare.HasValue)
		{
			double valueOrDefault2 = openedShare.GetValueOrDefault();
			Rectangle r = new Rectangle(box.X + num, box.Bottom - P(16f), width, P(5f));
			int num3 = (int)((double)r.Width * valueOrDefault2);
			using (GraphicsPath clip = Round(r, P(2f)))
			{
				g.SetClip(clip);
			}
			using (SolidBrush brush4 = new SolidBrush(YouBar))
			{
				g.FillRectangle(brush4, r.X, r.Y, num3, r.Height);
			}
			using (SolidBrush brush5 = new SolidBrush(BrokenBar))
			{
				g.FillRectangle(brush5, r.X + num3, r.Y, r.Width - num3, r.Height);
			}
			g.ResetClip();
		}
		int tileIndex = i;
		_hits.Add((_tileBoxes[i], () =>
		{
			SelectTile(tileIndex);
		}));
	}

	private void DrawChart(Graphics g, Rectangle card, List<Row> rows)
	{
		CultureInfo culture = L.Culture;
		bool rate = _focus == 4;
		using (GraphicsPath path = Round(card, P(12f)))
		{
			using SolidBrush brush = new SolidBrush(Theme.Card);
			g.FillPath(brush, path);
			using Pen pen = new Pen(Theme.CardEdge);
			g.DrawPath(pen, path);
		}
		int num = card.X + P(18f);
		string source;
		if (rate)
		{
			source = "Chests per minute";
		}
		else
		{
			source = (_perDay ? "Chests per day" : "Chests per match");
		}
		Caps(g, StatsText.T(source), _title, new Rectangle(num, card.Y + P(12f), card.Width / 2, P(30f)), Theme.Ink);
		int num2 = card.Right - P(18f);
		int num3 = CapsWidth(g, StatsText.T("Per day"), _pill) + P(24f);
		int num4 = CapsWidth(g, StatsText.T("Per match"), _pill) + P(24f);
		Pill(g, num2 - num3 - num4 - P(8f), card.Y + P(12f), StatsText.T("Per match"), null, !_perDay, () =>
		{
			_perDay = false;
		});
		Pill(g, num2 - num3, card.Y + P(12f), StatsText.T("Per day"), null, _perDay, () =>
		{
			_perDay = true;
			int focus = _focus;
			if ((uint)(focus - 2) <= 1u)
			{
				_focus = -1;
			}
		});
		List<Row> source2 = (rate ? rows.Where((Row r) =>
		{
			int? seconds = r.Seconds;
			return seconds.HasValue && seconds.GetValueOrDefault() > 0;
		}).ToList() : rows);
		List<Bar> series = (_perDay ? (from r in source2
			group r by r.When.Date into gr
			orderby gr.Key
			select new Bar(gr.Key, gr.Sum((Row r) => r.Opened + r.Broken), gr.Sum((Row r) => r.Broken), WithMates ? gr.Sum((Row r) => r.Mates) : 0, rate ? new int?(gr.Sum((Row r) => r.Seconds.GetValueOrDefault())) : ((int?)null))).ToList() : source2.Select((Row r) => new Bar(r.When, r.Opened + r.Broken, r.Broken, WithMates ? r.Mates : 0, r.Seconds)).ToList());
		double num5;
		if (series.Count != 0)
		{
			num5 = (rate ? ((double)series.Sum((Bar b) => b.Total) * 60.0 / (double)Math.Max(1, series.Sum((Bar b) => b.Seconds.GetValueOrDefault()))) : series.Average((Bar b) => b.Total));
		}
		else
		{
			num5 = 0.0;
		}
		string text;
		if (series.Count != 0)
		{
			if (rate)
			{
				text = StatsText.F("Oldest to newest, only matches with a known length. The line marks your average of {0:0.00} per minute.", num5);
			}
			else if (_focus == 2)
			{
				text = StatsText.F("Bright bars are the matches that beat your average of {0:0.0}.", num5);
			}
			else
			{
				text = ((_focus == 3) ? StatsText.T("Your best match is pinned. Click the tile again to unpin it.") : StatsText.F(_perDay ? "Oldest to newest. The line marks your average of {0:0.0} per day." : "Oldest to newest. The line marks your average of {0:0.0} per match.", num5));
			}
		}
		else
		{
			text = StatsText.T(rate ? "No matches with a known length yet." : "No matches yet. They appear here once a replay has been analysed.");
		}
		Text(g, text, _small, new Rectangle(num, card.Y + P(46f), card.Width - P(36f), P(18f)), Theme.Muted);
		int num6 = card.Y + P(68f);
		int num7 = num;
		(string, Color)[] array = new (string, Color)[3]
		{
			("Opened", YouBar),
			("Broken", BrokenBar),
			("Teammates", (_focus == 1) ? MateFocusBar : MateBar)
		};
		for (int num8 = 0; num8 < array.Length; num8++)
		{
			var (text2, color) = array[num8];
			if (text2 == "Teammates" && !WithMates)
			{
				continue;
			}
			using (GraphicsPath path2 = Round(new Rectangle(num7, num6 + P(4f), P(10f), P(10f)), P(3f)))
			{
				using SolidBrush brush2 = new SolidBrush(color);
				g.FillPath(brush2, path2);
			}
			string text3 = StatsText.T(text2);
			Text(g, text3, _small, new Rectangle(num7 + P(15f), num6, P(160f), P(18f)), Theme.Ink);
			num7 += P(15f) + Measure(g, text3, _small) + P(16f);
		}
		string text4 = (rate ? StatsText.F("Avg {0:0.00}", num5) : StatsText.F("Avg {0:0.0}", num5));
		int num9 = Measure(g, text4, _smallBold) + P(14f);
		Rectangle plot = new Rectangle(num + P(34f), num6 + P(32f), card.Width - P(70f) - num9 - P(8f), card.Bottom - num6 - P(58f));
		if (series.Count == 0 || plot.Width < 40 || plot.Height < 40)
		{
			return;
		}
		int num10 = Math.Max(1, plot.Width / Math.Max(1, P(9f)));
		int num11 = ((_focus == 3 && !rate && !_perDay) ? series.FindIndex((Bar b) => b.Total == series.Max((Bar x) => x.Total)) : (-1));
		int num12 = Math.Max(0, series.Count - num10);
		if (num11 >= 0 && num11 < num12)
		{
			num12 = Math.Max(0, Math.Min(series.Count - num10, num11 - num10 / 2));
		}
		List<Bar> shown = series.Skip(num12).Take(num10).ToList();
		if (num11 >= 0)
		{
			num11 -= num12;
		}
		if (series.Count > shown.Count)
		{
			Text(g, (num12 + shown.Count == series.Count) ? StatsText.F("Showing the latest {0:N0} of {1:N0}", shown.Count, series.Count) : StatsText.F("Showing {0:N0} of {1:N0}", shown.Count, series.Count), _small, new Rectangle(card.Right - P(300f), num6, P(282f), P(18f)), Theme.Muted, TextFormatFlags.Right);
		}
		double num13 = Math.Max(rate ? 0.05 : 1.0, ((IEnumerable<Bar>)shown).Max((Func<Bar, double>)V));
		double num14 = (rate ? NiceStep(num13) : ((double)NiceStep((int)Math.Ceiling(num13))));
		double num15 = Math.Max(num14, Math.Ceiling(num13 / num14 - 1E-09) * num14);
		_plot = plot;
		using (Pen pen2 = new Pen(Color.FromArgb(90, Theme.Line))
		{
			DashStyle = DashStyle.Dot
		})
		{
			using Pen pen3 = new Pen(Theme.Line);
			int num16 = (int)Math.Round(num15 / num14);
			for (int num17 = 0; num17 <= num16; num17++)
			{
				double num18 = (double)num17 * num14;
				int num19 = plot.Bottom - (int)Math.Round((double)plot.Height * (num18 / num15));
				g.DrawLine((num17 == 0) ? pen3 : pen2, plot.X, num19, plot.Right, num19);
				Text(g, rate ? num18.ToString("0.##", culture) : num18.ToString("N0", culture), _small, new Rectangle(num - P(4f), num19 - P(9f), P(34f), P(18f)), Theme.Muted, TextFormatFlags.Right);
			}
		}
		float num20 = (float)plot.Width / (float)shown.Count;
		float num21 = Math.Min(Math.Max(2f, num20 * 0.66f), P(46f));
		float radius = Math.Min(num21 / 2f, P(4f));
		int num22 = shown.FindIndex((Bar t) => V(t) == ((IEnumerable<Bar>)shown).Max((Func<Bar, double>)V));
		int num23 = ((_hover >= 0) ? _hover : num11);
		string text5 = null;
		int num24 = -100000;
		for (int num25 = 0; num25 < shown.Count; num25++)
		{
			Bar bar = shown[num25];
			float num26 = (float)plot.X + (float)num25 * num20;
			_columns.Add((num26, num20));
			if (num25 == num23)
			{
				using SolidBrush brush3 = new SolidBrush(Color.FromArgb(70, Theme.PanelHover));
				g.FillRectangle(brush3, num26, plot.Y, num20, plot.Height);
			}
			float num27 = num26 + (num20 - num21) / 2f;
			double num28;
			if (rate)
			{
				num28 = ((bar.Total > 0) ? (bar.PerMinute / (double)bar.Total) : 0.0);
			}
			else
			{
				num28 = 1.0;
			}
			float num29 = (float)((double)plot.Height * ((double)bar.You * num28 / num15));
			float num30 = (float)((double)plot.Height * ((double)bar.Broken * num28 / num15));
			float num31 = (float)((double)plot.Height * ((double)bar.Mates * num28 / num15));
			float num32 = num29 + num31;
			if (num32 >= 0.5f)
			{
				using GraphicsPath clip = Round(new RectangleF(num27, (float)plot.Bottom - num32, num21, num32), radius, bottom: false);
				GraphicsState gstate = g.Save();
				g.SetClip(clip);
				bool num33;
				if (_focus == 2)
				{
					num33 = ((_hover == -1) ? (V(bar) > num5) : (num25 == _hover));
				}
				else
				{
					if (num23 == -1)
					{
						goto IL_0c53;
					}
					num33 = num23 == num25;
				}
				if (num33)
				{
					goto IL_0c53;
				}
				int num34 = ((_focus == 2) ? 80 : 150);
				goto IL_0c58;
				IL_0c53:
				num34 = 255;
				goto IL_0c58;
				IL_0c58:
				int num35 = num34;
				int alpha = ((_focus == 1 && num23 != num25) ? Math.Min(num35, 110) : num35);
				using (LinearGradientBrush brush4 = new LinearGradientBrush(new RectangleF(num27, plot.Y, num21, plot.Height + 1), Color.FromArgb(alpha, ControlPaint.Light(YouBar, 0.25f)), Color.FromArgb(alpha, YouBar), 90f))
				{
					g.FillRectangle(brush4, num27, (float)plot.Bottom - num29 + num30, num21, num29 - num30 + 1f);
				}
				if (num30 > 0f)
				{
					using SolidBrush brush5 = new SolidBrush(Color.FromArgb(alpha, BrokenBar));
					g.FillRectangle(brush5, num27, (float)plot.Bottom - num29, num21, num30);
				}
				if (num31 > 0f)
				{
					using SolidBrush brush6 = new SolidBrush(Color.FromArgb(num35, (_focus == 1) ? MateFocusBar : MateBar));
					g.FillRectangle(brush6, num27, (float)plot.Bottom - num32, num21, num31);
				}
				g.Restore(gstate);
			}
			if (num25 == num22)
			{
				Star(g, num26 + num20 / 2f, (float)plot.Bottom - num32 - (float)P(10f), P(6f));
			}
			string text6 = bar.When.ToString((L.Code == "ja") ? "M/d" : "d MMM", culture);
			if (text6 != text5)
			{
				int num36 = Measure(g, text6, _small);
				int num37 = Math.Min((int)num27, plot.Right - num36);
				if (num37 - num24 > P(14f))
				{
					Text(g, text6, _small, new Rectangle(num37, plot.Bottom + P(6f), num36 + P(4f), P(16f)), Theme.Muted);
					text5 = text6;
					num24 = num37 + num36;
				}
			}
		}
		int num38 = plot.Bottom - (int)Math.Round((double)plot.Height * (num5 / num15));
		float width = ((_focus == 2) ? Math.Max(2, P(2.5f)) : Math.Max(1, P(1.5f)));
		using (Pen pen4 = new Pen(Theme.Amber, width)
		{
			DashStyle = DashStyle.Dash
		})
		{
			g.DrawLine(pen4, plot.X, num38, plot.Right, num38);
		}
		Rectangle rectangle = new Rectangle(plot.Right + P(6f), num38 - P(9f), num9, P(18f));
		using (GraphicsPath path3 = Round(rectangle, P(8f)))
		{
			using SolidBrush brush7 = new SolidBrush(Theme.Amber);
			g.FillPath(brush7, path3);
		}
		Text(g, text4, _smallBold, rectangle, Theme.Bg, TextFormatFlags.HorizontalCenter);
		if (num23 >= 0 && num23 < shown.Count)
		{
			DrawTooltip(g, shown[num23], plot, (float)plot.X + (float)num23 * num20 + num20 / 2f, rate);
		}
		double V(Bar b)
		{
			if (!rate)
			{
				return b.Total;
			}
			return b.PerMinute;
		}
	}

	private static string Duration(int seconds)
	{
		TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);
		if (timeSpan.TotalHours >= 1.0)
		{
			return $"{(int)timeSpan.TotalHours}:{timeSpan.Minutes:00}:{timeSpan.Seconds:00}";
		}
		return $"{timeSpan.Minutes}:{timeSpan.Seconds:00}";
	}

	private void DrawTooltip(Graphics g, Bar s, Rectangle plot, float centreX, bool rate)
	{
		CultureInfo culture = L.Culture;
		string text = (_perDay ? s.When.ToString(L.En ? "ddd d MMM yyyy" : "D", culture) : s.When.ToString(L.En ? "ddd d MMM yyyy, HH:mm" : "g", culture));
		List<(string, string, Color)> list = (rate ? new List<(string, string, Color)>
		{
			(StatsText.T("Chests"), s.Total.ToString("N0", culture), YouBar),
			(StatsText.T("Match time"), Duration(s.Seconds.GetValueOrDefault()), Theme.Muted),
			(StatsText.T("Per minute"), s.PerMinute.ToString("0.00", culture), Color.Empty)
		} : new List<(string, string, Color)>
		{
			(StatsText.T("Opened"), (s.You - s.Broken).ToString("N0", culture), YouBar),
			(StatsText.T("Broken"), s.Broken.ToString("N0", culture), BrokenBar)
		});
		if (!rate)
		{
			if (WithMates)
			{
				list.Add((StatsText.T("Teammates"), s.Mates.ToString("N0", culture), MateBar));
			}
			list.Add((StatsText.T("Total"), s.Total.ToString("N0", culture), Color.Empty));
		}
		int num = P(18f);
		int num2 = Math.Max(Measure(g, text, _smallBold) + P(24f), P(170f));
		int height = P(30f) + list.Count * num + P(6f);
		int num3 = (int)centreX + P(12f);
		if (num3 + num2 > plot.Right)
		{
			num3 = (int)centreX - P(12f) - num2;
		}
		num3 = Math.Max(plot.X, num3);
		Rectangle r = new Rectangle(num3, plot.Y + P(4f), num2, height);
		using (GraphicsPath path = Round(r, P(8f)))
		{
			using SolidBrush brush = new SolidBrush(Color.FromArgb(245, Theme.Bg));
			g.FillPath(brush, path);
			using Pen pen = new Pen(Theme.Accent);
			g.DrawPath(pen, path);
		}
		Text(g, text, _smallBold, new Rectangle(r.X + P(12f), r.Y + P(8f), num2 - P(24f), P(18f)), Theme.Ink);
		int num4 = r.Y + P(30f);
		foreach (var (text2, text3, color) in list)
		{
			if (color != Color.Empty)
			{
				using SolidBrush brush2 = new SolidBrush(color);
				g.FillRectangle(brush2, r.X + P(12f), num4 + P(5f), P(8f), P(8f));
			}
			Font font = ((color == Color.Empty) ? _smallBold : _small);
			Text(g, text2, font, new Rectangle(r.X + P(26f), num4, num2 - P(80f), num), (color == Color.Empty) ? Theme.Ink : Theme.Muted);
			Text(g, text3, font, new Rectangle(r.X + P(12f), num4, num2 - P(24f), num), Theme.Ink, TextFormatFlags.Right);
			num4 += num;
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_tileValue.Dispose();
			_tileLabel.Dispose();
			_small.Dispose();
			_smallBold.Dispose();
			_pill.Dispose();
			_title.Dispose();
			_icon.Dispose();
			_tip.Dispose();
		}
		base.Dispose(disposing);
	}
}
