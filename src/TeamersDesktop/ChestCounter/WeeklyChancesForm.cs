using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ChestCounter;

[SupportedOSPlatform("windows")]
public sealed class WeeklyChancesForm : Form
{
	private const int HeaderHeight = 190;

	private readonly TreasureHourStrip _eventStrip;

	private readonly Monitor _monitor;

	private readonly Store _store;

	private readonly string _folder;

	private readonly Dictionary<string, Bitmap> _images = new Dictionary<string, Bitmap>();

	private readonly Timer _clock = new Timer
	{
		Interval = 1000
	};

	private readonly Timer _data = new Timer
	{
		Interval = 15000
	};

	private readonly Timer _anim = new Timer
	{
		Interval = 16
	};

	private readonly Timer _sparkle = new Timer
	{
		Interval = 50
	};

	private readonly Label _countdown;

	private readonly Label _hour;

	private readonly Panel _body;

	private readonly GoalPanel _goal = new GoalPanel();

	private readonly PlannerPanel _planner = new PlannerPanel();

	private readonly OddsTable _table = new OddsTable();

	private readonly Label _note;

	private readonly ClaimAllBanner _claim;

	private readonly GlowButton _auto;

	private DateTime _weekStart;

	private string? _pick;

	private int _plan = 100;

	private ChanceSnapshot? _snap;

	private string PickPath => Path.Combine(_store.Folder, "2xflaw-goal.txt");

	public WeeklyChancesForm(Monitor monitor, Store store)
	{
		_monitor = monitor;
		_store = store;
		_eventStrip = new TreasureHourStrip(store, () => _monitor.Config.Animations);
		_folder = Path.Combine(store.Folder, "rewards");
		Text = WeeklyChanceMore.T("Weekly chances");
		AutoScaleMode = AutoScaleMode.Dpi;
		AutoScaleDimensions = new SizeF(96f, 96f);
		Font = Theme.Text();
		BackColor = Theme.Bg;
		ForeColor = Theme.Ink;
		StartPosition = FormStartPosition.CenterParent;
		ClientSize = new Size(900, 900);
		MinimumSize = new Size(880, 560);
		Icon = AppIcon.Load();
		Panel panel = new Panel
		{
			Dock = DockStyle.Top,
			Height = 190,
			BackColor = Theme.Bg
		};
		panel.Controls.Add(new Label
		{
			Text = WeeklyChanceMore.T("WEEKLY CHANCES"),
			Font = Fonts.Display(26f),
			ForeColor = Theme.Ink,
			BackColor = Theme.Bg,
			AutoSize = false,
			Size = new Size(500, 48),
			Location = new Point(20, 4),
			TextAlign = ContentAlignment.MiddleLeft,
			UseMnemonic = false
		});
		panel.Controls.Add(new Label
		{
			Text = WeeklyChanceMore.T("Your odds of having found each prize this week. Click a prize to aim for it."),
			Font = Theme.Text(),
			ForeColor = Theme.Muted,
			BackColor = Theme.Bg,
			AutoSize = false,
			Size = new Size(560, 22),
			Location = new Point(22, 56),
			TextAlign = ContentAlignment.MiddleLeft,
			UseMnemonic = false
		});
		_auto = new GlowButton
		{
			Text = WeeklyChanceMore.T("Auto: next closest"),
			Size = new Size(190, 34),
			Radius = 11,
			Font = Theme.Text(8.5f, FontStyle.Bold),
			Location = new Point(22, 84)
		};
		_auto.Click += (object? _, EventArgs _) =>
		{
			_pick = null;
			SavePick(null);
			Apply();
		};
		_countdown = new Label
		{
			Font = Fonts.Display(16f),
			ForeColor = Theme.Cyan,
			BackColor = Theme.Bg,
			AutoSize = false,
			Size = new Size(420, 34),
			Location = new Point(460, 8),
			Anchor = (AnchorStyles.Top | AnchorStyles.Right),
			TextAlign = ContentAlignment.MiddleRight,
			UseMnemonic = false
		};
		Label value = new Label
		{
			Text = WeeklyChanceMore.T("Resets every Sunday at 14:00 GMT"),
			Font = Theme.Text(8f),
			ForeColor = Theme.Muted,
			BackColor = Theme.Bg,
			AutoSize = false,
			Size = new Size(420, 20),
			Location = new Point(460, 44),
			Anchor = (AnchorStyles.Top | AnchorStyles.Right),
			TextAlign = ContentAlignment.MiddleRight,
			UseMnemonic = false
		};
		_hour = new Label
		{
			Font = Theme.Text(9f, FontStyle.Bold),
			ForeColor = Theme.Muted,
			BackColor = Theme.Bg,
			AutoSize = false,
			Size = new Size(560, 24),
			Location = new Point(320, 88),
			Anchor = (AnchorStyles.Top | AnchorStyles.Right),
			TextAlign = ContentAlignment.MiddleRight,
			UseMnemonic = false
		};
		panel.Controls.Add(_auto);
		panel.Controls.Add(_countdown);
		panel.Controls.Add(value);
		panel.Controls.Add(_hour);
		_eventStrip.Dock = DockStyle.Bottom;
		panel.Controls.Add(_eventStrip);
		_eventStrip.BringToFront();
		_note = new Label
		{
			Font = Theme.Text(8f),
			ForeColor = Theme.Muted,
			BackColor = Theme.Bg,
			AutoSize = false,
			UseMnemonic = false
		};
		_claim = new ClaimAllBanner(_folder);
		_body = new Panel
		{
			Dock = DockStyle.Fill,
			AutoScroll = true,
			BackColor = Theme.Bg
		};
		_body.Controls.Add(_goal);
		_body.Controls.Add(_planner);
		_body.Controls.Add(_table);
		_body.Controls.Add(_claim);
		_body.Controls.Add(_note);
		Controls.Add(_body);
		Controls.Add(panel);
		_body.Resize += (object? _, EventArgs _) =>
		{
			DoLayout();
		};
		_table.Picked += (string key) =>
		{
			_pick = key;
			SavePick(key);
			Apply();
		};
		_planner.Chosen += (int n) =>
		{
			_plan = n;
			Apply();
		};
		_pick = LoadPick();
		_weekStart = WeeklyReset.Start(DateTime.UtcNow);
		_anim.Tick += (object? _, EventArgs _) =>
		{
			if (!_monitor.Config.Animations)
			{
				_goal.Snap();
				_table.Snap();
				_planner.Snap();
				_anim.Stop();
			}
			else
			{
				bool flag = _goal.Step();
				bool flag2 = _table.Step();
				bool flag3 = _planner.Step();
				if (!flag && !flag2 && !flag3)
				{
					_anim.Stop();
				}
			}
		};
		_sparkle.Tick += (object? _, EventArgs _) =>
		{
			if (_goal.Sparkle && Visible && WindowState != FormWindowState.Minimized)
			{
				_goal.Invalidate();
			}
		};
		_clock.Tick += (object? _, EventArgs _) =>
		{
			DateTime utcNow = DateTime.UtcNow;
			UpdateCountdown();
			_goal.Sparkle = _monitor.Config.Animations;
			_sparkle.Enabled = _monitor.Config.Animations && Visible && WindowState != FormWindowState.Minimized;
			if (!_monitor.Config.Animations && _anim.Enabled)
			{
				_goal.Snap();
				_table.Snap();
				_planner.Snap();
				_anim.Stop();
			}
			bool flag = WeeklyReset.Start(utcNow) != _weekStart;
			bool flag2 = _snap != null && ((_snap.Live != null && utcNow >= _snap.Live.EndUtc) || (_snap.Next != null && utcNow >= _snap.Next.StartUtc));
			if (flag | flag2)
			{
				_weekStart = WeeklyReset.Start(utcNow);
				RefreshData();
			}
		};
		_data.Tick += (object? _, EventArgs _) =>
		{
			RefreshData();
		};
		Activated += (object? _, EventArgs _) =>
		{
			RefreshData();
		};
		UpdateCountdown();
		RefreshData();
		_clock.Start();
		_data.Start();
		if (_monitor.Config.Animations)
		{
			_sparkle.Start();
		}
	}

	private string? LoadPick()
	{
		try
		{
			string text = (File.Exists(PickPath) ? File.ReadAllText(PickPath).Trim() : "");
			return (text.Length == 0) ? null : text;
		}
		catch
		{
			return null;
		}
	}

	private void SavePick(string? key)
	{
		try
		{
			File.WriteAllText(PickPath, key ?? "");
		}
		catch
		{
		}
	}

	private static string Range(TreasureWindow w)
	{
		return WeeklyChanceMore.F("{0:ddd d MMM HH:mm}–{1:HH:mm} GMT", w.StartUtc, w.EndUtc) + (TreasureHours.IsUnconfirmed(w) ? WeeklyChanceMore.T("  (final · boosts not announced yet)") : "");
	}

	private void UpdateCountdown()
	{
		DateTime utcNow = DateTime.UtcNow;
		TimeSpan timeSpan = WeeklyReset.Remaining(utcNow);
		if (timeSpan < TimeSpan.Zero)
		{
			timeSpan = TimeSpan.Zero;
		}
		_countdown.Text = WeeklyChanceMore.F("RESETS IN {0}D {1:00}H {2:00}M {3:00}S", timeSpan.Days, timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);
		TreasureWindow treasureWindow = _snap?.Live;
		if (treasureWindow != null && utcNow < treasureWindow.EndUtc)
		{
			TimeSpan timeSpan2 = treasureWindow.EndUtc - utcNow;
			_hour.Text = WeeklyChanceMore.F("TREASURE HOUR LIVE  ·  ends in {0:00}:{1:00}:{2:00}", (int)timeSpan2.TotalHours, timeSpan2.Minutes, timeSpan2.Seconds) + (_snap.LiveUnknown ? WeeklyChanceMore.T("  ·  boosts not announced") : "");
			_hour.ForeColor = (_snap.LiveUnknown ? Theme.Muted : Theme.Amber);
			return;
		}
		TreasureWindow treasureWindow2 = _snap?.Next;
		if (treasureWindow2 != null)
		{
			TimeSpan timeSpan3 = treasureWindow2.StartUtc - utcNow;
			_hour.Text = WeeklyChanceMore.F("Next Treasure Hour in {0}d {1:00}h {2:00}m {3:00}s", timeSpan3.Days, timeSpan3.Hours, timeSpan3.Minutes, timeSpan3.Seconds) + (_snap.NextUnknown ? WeeklyChanceMore.T("  ·  final, boosts not announced") : "");
			_hour.ForeColor = (_snap.NextUnknown ? Theme.Muted : Theme.Cyan);
		}
		else
		{
			_hour.Text = WeeklyChanceMore.T("No more Treasure Hours");
			_hour.ForeColor = Theme.Muted;
		}
	}

	private void RefreshData()
	{
		try
		{
			_snap = WeeklyChanceModel.Compute(_store, _monitor, DateTime.UtcNow);
		}
		catch (Exception error)
		{
			ErrorLog.Write("Weekly chances", error);
			return;
		}
		Apply();
		UpdateCountdown();
	}

	private Bitmap Picture(string key, int size, int radius)
	{
		string key2 = $"{key}|{size}";
		if (!_images.TryGetValue(key2, out var value))
		{
			value = RewardPictures.Load(_folder, key, size, radius);
			_images[key2] = value;
		}
		return value;
	}

	private void Apply()
	{
		ChanceSnapshot snap = _snap;
		if ((object)snap != null)
		{
			_goal.Sparkle = _monitor.Config.Animations;
			List<PrizeChance> source = snap.Prizes.Where((PrizeChance p) => !p.Owned).ToList();
			List<PrizeChance> source2 = (from p in source
				where p.Key != "gizmo"
				orderby p.Chance descending, p.Den
				select p).Concat(source.Where((PrizeChance p) => p.Key == "gizmo")).ToList();
			List<PrizeChance> source3 = source2.Where((PrizeChance p) => p.Key != "gizmo").ToList();
			PrizeChance prizeChance = ((_pick != null) ? source3.FirstOrDefault((PrizeChance p) => p.Key == _pick) : null);
			bool flag = prizeChance == null;
			if ((object)prizeChance == null)
			{
				prizeChance = source3.FirstOrDefault();
			}
			Dictionary<string, double> previous = _table.Rows.ToDictionary((OddsRow r) => r.Prize.Key, (OddsRow r) => r.Fill);
			_table.SetRows(source2.Select((PrizeChance p) => new OddsRow
			{
				Prize = p,
				Image = Picture(p.Key, 40, 10),
				Target = p.Chance,
				Fill = (previous.TryGetValue(p.Key, out var value) ? value : 0.0),
				Ahead = WeeklyChanceModel.After(p.Chance, p.Den, p.Boost, _plan)
			}).ToList(), prizeChance?.Key, _plan);
			if (prizeChance == null)
			{
				_goal.ShowMessage(WeeklyChanceMore.T("Everything is ticked as owned. Fresh prizes arrive at the reset."));
				_planner.Visible = false;
			}
			else
			{
				string liveText = ((snap.Live != null && prizeChance.Boost > 1.0) ? WeeklyChanceMore.F("TREASURE HOUR  {0:0.#}×", prizeChance.Boost) : null);
				_goal.ShowGoal(new GoalPanel.Vm(prizeChance.Name, flag ? "NEXT CLOSEST" : "YOUR PICK", flag, prizeChance.Chance, prizeChance.Den, prizeChance.Boost, prizeChance.AllTimeChests ?? snap.Chests, WeeklyChanceModel.ChestsToHalf(prizeChance.Chance, prizeChance.Den), WeeklyChanceModel.After(prizeChance.Chance, prizeChance.Den, prizeChance.Boost, _plan) - prizeChance.Chance, WeeklyChanceModel.After(prizeChance.Chance, prizeChance.Den, 1.0, _plan) - prizeChance.Chance, _plan, Picture(prizeChance.Key, 190, 16), liveText));
				TreasureWindow treasureWindow = snap.Live ?? snap.Next;
				bool unknown = treasureWindow != null && TreasureHours.IsUnconfirmed(treasureWindow);
				string status;
				if (snap.Live != null)
				{
					status = "LIVE NOW";
				}
				else
				{
					status = ((snap.Next != null) ? "NEXT TREASURE HOUR" : "NO MORE TREASURE HOURS");
				}
				string windowText = ((treasureWindow == null) ? "" : Range(treasureWindow));
				_planner.Visible = true;
				_planner.SetState(new PlannerPanel.Vm(status, snap.Live != null, windowText, prizeChance.Name, prizeChance.Chance, prizeChance.Den, WeeklyChanceModel.BoostFor(treasureWindow, prizeChance.Key), unknown), _plan);
			}
			int hidden = Enumerable.Count(snap.Prizes, (PrizeChance p) => p.Owned);
			_note.Text = WeeklyChanceMore.Note(hidden);
			bool flag2 = (_pick == null) | flag;
			_auto.BackColor = (flag2 ? Theme.Accent : Theme.Panel);
			_auto.ForeColor = (flag2 ? Theme.AccentInk : Theme.Ink);
			_auto.FlatAppearance.BorderColor = (flag2 ? Theme.Accent : Theme.CardEdge);
			_auto.Invalidate();
			DoLayout();
			if (!_monitor.Config.Animations)
			{
				_goal.Snap();
				_table.Snap();
				_planner.Snap();
				_anim.Stop();
			}
			else if (!_anim.Enabled)
			{
				_anim.Start();
			}
		}
	}

	private void DoLayout()
	{
		int width = Math.Max(640, _body.ClientSize.Width - 40);
		Point autoScrollPosition = _body.AutoScrollPosition;
		_goal.SetBounds(20 + autoScrollPosition.X, 8 + autoScrollPosition.Y, width, 244);
		int y = _goal.Bottom + 12;
		if (_planner.Visible)
		{
			_planner.SetBounds(20, y, width, 134);
			y = _planner.Bottom + 12;
		}
		_table.SetBounds(20, y, width, _table.WantedHeight);
		_claim.SetBounds(20, _table.Bottom + 12, width, _claim.HeightFor(width));
		_note.SetBounds(20, _claim.Bottom + 12, width, 130);
	}

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		Theme.Chrome(Handle);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_clock.Stop();
			_clock.Dispose();
			_data.Stop();
			_data.Dispose();
			_anim.Stop();
			_anim.Dispose();
			_sparkle.Stop();
			_sparkle.Dispose();
			foreach (Bitmap value in _images.Values)
			{
				value.Dispose();
			}
			_images.Clear();
		}
		base.Dispose(disposing);
	}
}
