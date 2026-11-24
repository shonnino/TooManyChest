using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ChestCounter;

[SupportedOSPlatform("windows")]
public sealed class MainForm : Form
{
	private sealed record HistoryRow(string Date, string Mode, int CountedTotal, int Personal, int TeammatesSeen,
		int Treasures, int? Place, int? Eliminations);

	private List<HistoryRow> displayedHistory = new();
	private bool importingTeammateReplay;

	private readonly Monitor monitor;

	private readonly TreasureHourStrip treasureHour;

	private readonly Store store;
	private readonly TeamersClient teamers;
	private readonly System.Windows.Forms.Timer teamersTimer = new() { Interval = 25000 };
	private readonly HashSet<string> notifiedTeamersInvites = new();
	private NotifyIcon? teamersNotification;
	private TeamersForm? teamersForm;

	private ChestStatsPanel? chestStats;

	private readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();

	private readonly Panel content = new Panel
	{
		Dock = DockStyle.Fill,
		Padding = new Padding(20),
		AutoScroll = true
	};

	private readonly Label total = new Label();

	private readonly Label current = new Label();

	private readonly Label today = new Label();

	private readonly Label average = new Label();

	private readonly Label best = new Label();

	private readonly Label treasures = new Label();

	private readonly Label last = new Label();

	private readonly Label team = new Label();

	private readonly Label notice = new Label();

	private readonly Label profileName = new Label();

	private readonly Label reset = new Label();

	private readonly Label performance = new Label();

	private readonly Label status = new Label();

	private readonly ModernSelect profiles = new ModernSelect
	{
		Width = 250,
		Height = 44
	};

	private readonly DailyChart chart = new DailyChart
	{
		Dock = DockStyle.Fill
	};

	private readonly Button overlay = new Button();

	private readonly Label historyEmpty = new Label
	{
		Dock = DockStyle.Top,
		Height = 40,
		TextAlign = ContentAlignment.MiddleLeft,
		ForeColor = Theme.Muted,
		Visible = false,
		Text = MainActionText.T("No matches yet. They appear here once a replay has been analysed.")
	};

	private readonly Card goalCard = new Card
	{
		Dock = DockStyle.Top,
		Height = 76,
		Radius = 14,
		Padding = new Padding(16, 10, 16, 12),
		Margin = new Padding(6, 0, 6, 12),
		Visible = false
	};

	private readonly Label goalText = new Label();

	private readonly Panel goalTrack = new Panel();

	private readonly Panel goalFill = new Panel();

	private readonly System.Windows.Forms.Timer anim = new System.Windows.Forms.Timer
	{
		Interval = 33
	};

	private int animFrom;

	private int animTo;

	private DateTime animStart;

	private string animText = "";

	private bool pulse;

	private bool perfLoading;

	private Control? hero;

	private ToggleSwitch? animToggle;

	private ToggleSwitch? bannerToggle;

	private readonly PillButton[] livePills = new PillButton[4];

	private static readonly int[] LiveSeconds = new int[4] { 0, 10, 30, 60 };

	private readonly System.Windows.Forms.Timer clock = new System.Windows.Forms.Timer
	{
		Interval = 1000
	};

	private readonly Image banner;

	private readonly Image avatar;

	private readonly Dictionary<string, Panel> pages = new Dictionary<string, Panel>();

	private readonly List<Button> navigation = new List<Button>();

	private string active = "Dashboard";

	private string profileSignature = "";

	private string chartSignature = "";

	private bool selecting;

	private MonitorState? state;

	private MonitorState? pendingState;

	private int queued;

	private PersonalTools? tools;

	private readonly DataGridView history = new DataGridView
	{
		Dock = DockStyle.Fill,
		ReadOnly = true,
		AllowUserToAddRows = false,
		AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
		RowHeadersVisible = false,
		BackgroundColor = Theme.Card,
		BorderStyle = BorderStyle.None,
		EnableHeadersVisualStyles = false
	};

	private WeeklyChancesForm? chances;

	private Font F(float size, FontStyle style = FontStyle.Regular)
	{
		string key = $"{size}:{style}";
		if (!fonts.TryGetValue(key, out var value))
		{
			value = (fonts[key] = Fonts.Body(size, style));
		}
		return value;
	}

	private Label TextLabel(string text, float size = 10f, Color? colour = null)
	{
		text = PerformanceText.T(MainActionText.T(AppText.T(text)));
		string text2 = text;
		bool flag = text.Any(char.IsLetter) && text == text.ToUpperInvariant();
		Font font;
		if (size >= 20f)
		{
			text2 = ((text == "2xFlaw") ? text : text.ToUpperInvariant());
			font = Fonts.Display(size * 1.1f);
		}
		else if (flag && size <= 10f)
		{
			text2 = Fonts.Track(text);
			font = F(Math.Min(size, 9f), FontStyle.Bold);
		}
		else
		{
			font = F(size);
		}
		return new Label
		{
			Text = text2,
			AutoSize = true,
			ForeColor = (colour ?? Theme.Ink),
			Font = font,
			Margin = new Padding(0, 0, 0, 9)
		};
	}

	private Button ActionButton(string text, Action click)
	{
		GlowButton glowButton = new GlowButton();
		glowButton.Text = MainActionText.T(AppText.T(text));
		glowButton.AutoSize = true;
		glowButton.MinimumSize = new Size(120, 40);
		glowButton.BackColor = Theme.Panel;
		glowButton.ForeColor = Theme.Ink;
		glowButton.Font = F(10f, FontStyle.Bold);
		glowButton.Margin = new Padding(0, 0, 8, 8);
		glowButton.Padding = new Padding(12, 4, 12, 4);
		glowButton.FlatAppearance.BorderColor = Theme.CardEdge;
		glowButton.Click += (object? _, EventArgs _) =>
		{
			click();
		};
		return glowButton;
	}

	private Panel Page(string name)
	{
		Panel panel = new Panel
		{
			Dock = DockStyle.Fill,
			BackColor = Theme.Bg,
			Visible = false,
			AutoScroll = true
		};
		pages[name] = panel;
		content.Controls.Add(panel);
		return panel;
	}

	private Panel Metric(string caption, Label number, Color colour)
	{
		GlassCard glassCard = new GlassCard
		{
			Dock = DockStyle.Fill,
			Radius = 16,
			Padding = new Padding(16, 24, 14, 12),
			Margin = new Padding(6),
			AccentColor = colour
		};
		number.Dock = DockStyle.Fill;
		number.ForeColor = colour;
		number.Font = Fonts.Display(28f);
		number.Text = "—";
		number.TextAlign = ContentAlignment.MiddleLeft;
		number.BackColor = Color.Transparent;
		number.Padding = Padding.Empty;
		Label label = TextLabel(caption.ToUpperInvariant(), 8.5f, Theme.Muted);
		label.AutoSize = false;
		label.Height = 22;
		label.Dock = DockStyle.Top;
		label.BackColor = Color.Transparent;
		label.Padding = Padding.Empty;
		glassCard.Controls.Add(number);
		glassCard.Controls.Add(label);
		return glassCard;
	}

	private void OpenTools()
	{
		try
		{
			if (tools == null || tools.IsDisposed)
			{
				tools = new PersonalTools(store, monitor);
				tools.Show(this);
				return;
			}
			if (tools.WindowState == FormWindowState.Minimized)
			{
				tools.WindowState = FormWindowState.Normal;
			}
			tools.Activate();
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, ex.Message, MainStatusText.T("Weekly tools error"));
		}
	}

	private void OpenChances()
	{
		try
		{
			if (chances == null || chances.IsDisposed)
			{
				chances = new WeeklyChancesForm(monitor, store);
				chances.Show(this);
				return;
			}
			if (chances.WindowState == FormWindowState.Minimized)
			{
				chances.WindowState = FormWindowState.Normal;
			}
			chances.Activate();
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, ex.Message, MainStatusText.T("Weekly chances error"));
		}
	}

	public MainForm(Monitor monitor, Store store)
	{
		MainForm mainForm = this;
		this.monitor = monitor;
		this.store = store;
		teamers = new TeamersClient(store, monitor.Config);
		teamers.Changed += UpdateTeamersNotifications;
		teamersTimer.Tick += async (_, _) => await teamers.RefreshAsync();
		treasureHour = new TreasureHourStrip(store, () => monitor.Config.Animations);
		banner = PersonalBrand.Banner();
		avatar = PersonalBrand.Avatar();
		Text = "2xFlaw's Chest Counter";
		Size = new Size(1160, 850);
		MinimumSize = new Size(960, 720);
		StartPosition = FormStartPosition.CenterScreen;
		BackColor = Theme.Bg;
		ForeColor = Theme.Ink;
		Font = F(10f);
		Icon = AppIcon.Load();
		AutoScaleMode = AutoScaleMode.Dpi;
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 2,
			RowCount = 2,
			Margin = Padding.Empty
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200f));
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		Controls.Add(tableLayoutPanel);
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 66f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		treasureHour.Dock = DockStyle.Fill;
		tableLayoutPanel.Controls.Add(treasureHour, 0, 0);
		tableLayoutPanel.SetColumnSpan(treasureHour, 2);
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false,
			BackColor = Theme.Card,
			Padding = new Padding(17, 25, 12, 16)
		};
		tableLayoutPanel.Controls.Add(flowLayoutPanel, 0, 1);
		tableLayoutPanel.Controls.Add(content, 1, 1);
		flowLayoutPanel.Controls.Add(TextLabel("2xFlaw", 22f, Theme.Accent));
		flowLayoutPanel.Controls.Add(TextLabel("CHEST COUNTER", 9f, Theme.Muted));
		string[] array = ["Dashboard", "Match history", "Teamers", "Weekly chances", "Weekly rewards", "Overlay", "Settings", "Performance"];
		foreach (string text in array)
		{
			string name = text;
			Button button = ActionButton(text, () =>
			{
				mainForm.Navigate(name);
			});
			button.Tag = name;
			button.Width = 164;
			button.Height = 44;
			button.AutoSize = false;
			button.TextAlign = ContentAlignment.MiddleLeft;
			button.Padding = new Padding(18, 0, 8, 0);
			navigation.Add(button);
			flowLayoutPanel.Controls.Add(button);
		}
		flowLayoutPanel.Controls.Add(TextLabel("F8  Weekly tools", 9f, Theme.Muted));
		flowLayoutPanel.Controls.Add(TextLabel("Personal source build\nOriginal parser retained", 8f, Theme.Muted));
		BuildDashboard();
		BuildHistory();
		BuildPerformance();
		profiles.BackColor = Theme.Panel;
		profiles.ForeColor = Theme.Ink;
		profiles.Font = F(10f);
		profiles.SelectedIndexChanged += (object? _, EventArgs _) =>
		{
			if (!mainForm.selecting && mainForm.profiles.SelectedItem is Profile profile)
			{
				monitor.SetProfile(profile.Key);
			}
		};
		notice.ForeColor = Theme.AccentSoft;
		notice.Font = F(10f);
		notice.AutoSize = true;
		notice.MaximumSize = new Size(800, 0);
		notice.Click += (object? _, EventArgs _) =>
		{
			monitor.KeepOpen();
		};
		KeyPreview = true;
		KeyDown += (object? _, KeyEventArgs e) =>
		{
			if (e.KeyCode == Keys.F8)
			{
				mainForm.OpenTools();
				e.Handled = true;
			}
		};
		clock.Tick += (object? _, EventArgs _) =>
		{
			mainForm.Tick();
		};
		clock.Start();
		anim.Tick += (object? _, EventArgs _) =>
		{
			mainForm.AnimateTick();
		};
		monitor.Changed += OnChanged;
		Shown += async (object? _, EventArgs _) =>
		{
			if (monitor.AutoMode)
			{
				mainForm.WindowState = FormWindowState.Minimized;
			}
			teamersTimer.Start();
			await teamers.RefreshAsync();
		};
		Navigate("Dashboard");
	}

	private void BuildDashboard()
	{
		Panel panel = Page("Dashboard");
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Top,
			AutoSize = true,
			ColumnCount = 1,
			Padding = Padding.Empty
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		panel.Controls.Add(tableLayoutPanel);
		hero = new BrandHero(banner, avatar)
		{
			Dock = DockStyle.Top,
			Height = 185,
			Margin = new Padding(0, 0, 0, 14)
		};
		hero.Visible = monitor.Config.ShowBanner;
		tableLayoutPanel.Controls.Add(hero);
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Top,
			AutoSize = true,
			WrapContents = true
		};
		Label label = TextLabel("Your dashboard", 22f);
		label.Margin = new Padding(0, 0, 20, 0);
		profiles.Margin = new Padding(0, Math.Max(0, (label.GetPreferredSize(Size.Empty).Height - profiles.Height) / 2), 0, 0);
		flowLayoutPanel.Controls.Add(label);
		flowLayoutPanel.Controls.Add(profiles);
		tableLayoutPanel.Controls.Add(flowLayoutPanel);
		status.AutoSize = true;
		status.Font = F(10f, FontStyle.Bold);
		status.Margin = new Padding(0, 0, 0, 6);
		tableLayoutPanel.Controls.Add(status);
		profileName.AutoSize = true;
		profileName.ForeColor = Theme.Muted;
		tableLayoutPanel.Controls.Add(profileName);
		chestStats = new ChestStatsPanel(monitor, store);
		tableLayoutPanel.Controls.Add(chestStats);
		TableLayoutPanel tableLayoutPanel2 = new TableLayoutPanel
		{
			Dock = DockStyle.Top,
			Height = 255,
			ColumnCount = 3,
			RowCount = 2,
			Margin = new Padding(0, 12, 0, 12)
		};
		for (int i = 0; i < 3; i++)
		{
			tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
		}
		for (int j = 0; j < 2; j++)
		{
			tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
		}
		tableLayoutPanel2.Controls.Add(Metric("Total personal / configured", total, Theme.Accent), 0, 0);
		tableLayoutPanel2.Controls.Add(Metric("Current / previous match", current, Theme.Ink), 1, 0);
		tableLayoutPanel2.Controls.Add(Metric("Today", today, Theme.AccentSoft), 2, 0);
		tableLayoutPanel2.Controls.Add(Metric("Average per match", average, Theme.Ink), 0, 1);
		tableLayoutPanel2.Controls.Add(Metric("Best match", best, Theme.Rose), 1, 1);
		tableLayoutPanel2.Controls.Add(Metric("Treasures", treasures, Theme.Accent), 2, 1);
		tableLayoutPanel.Controls.Add(tableLayoutPanel2);
		BuildGoalCard();
		tableLayoutPanel.Controls.Add(goalCard);
		FlowLayoutPanel flowLayoutPanel2 = new FlowLayoutPanel
		{
			Dock = DockStyle.Top,
			AutoSize = true,
			BackColor = Theme.Card,
			Padding = new Padding(14),
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false
		};
		last.AutoSize = true;
		last.ForeColor = Theme.Ink;
		last.Font = F(11f, FontStyle.Bold);
		team.AutoSize = true;
		team.ForeColor = Theme.Muted;
		team.MaximumSize = new Size(780, 0);
		flowLayoutPanel2.Controls.Add(last);
		flowLayoutPanel2.Controls.Add(team);
		tableLayoutPanel.Controls.Add(flowLayoutPanel2);
		tableLayoutPanel.Controls.Add(TextLabel("LAST 7 DAYS", 10f, Theme.Muted));
		tableLayoutPanel.Controls.Add(new Panel
		{
			Dock = DockStyle.Top,
			Height = 155,
			BackColor = Theme.Card,
			Controls = { (Control?)chart },
			Margin = new Padding(0, 10, 0, 10)
		});
		FlowLayoutPanel flowLayoutPanel3 = new FlowLayoutPanel
		{
			Dock = DockStyle.Top,
			AutoSize = true
		};
		Button button = ActionButton("Toggle game overlay", () =>
		{
			monitor.ToggleOverlay();
		});
		button.Name = "OverlayAction";
		flowLayoutPanel3.Controls.Add(button);
		flowLayoutPanel3.Controls.Add(ActionButton("Compact overlay (Ctrl+Shift+K)", () =>
		{
			NativeOverlay.ToggleCompact();
		}));
		flowLayoutPanel3.Controls.Add(ActionButton("Weekly / import / manual", OpenTools));
		flowLayoutPanel3.Controls.Add(ActionButton("Weekly report", () =>
		{
			WeeklyReport.Open(store, monitor, this);
		}));
		flowLayoutPanel3.Controls.Add(ActionButton("Open classic report", () =>
		{
			try
			{
				Process.Start(new ProcessStartInfo(store.ReportPath)
				{
					UseShellExecute = true
				});
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, ex.Message);
			}
		}));
		tableLayoutPanel.Controls.Add(flowLayoutPanel3);
		reset.AutoSize = true;
		reset.ForeColor = Theme.Muted;
		tableLayoutPanel.Controls.Add(reset);
		tableLayoutPanel.Controls.Add(notice);
		tableLayoutPanel.Controls.Add(TextLabel("Imports are integrated. Manual additions affect totals and daily charts, not match averages. Edit them in Weekly tools.", 9f, Theme.Muted));
	}

	private void BuildHistory()
	{
		Panel panel = Page("Match history");
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Top,
			Height = 60
		};
		flowLayoutPanel.Controls.Add(TextLabel("Match history", 22f));
		flowLayoutPanel.Controls.Add(ActionButton("Refresh", () =>
		{
			monitor.RequestPublish();
		}));
		flowLayoutPanel.Controls.Add(ActionButton(L.Code == "it" ? "Importa replay compagno" : "Import teammate replay",
			() => _ = ImportTeammateReplay()));
		history.DefaultCellStyle.BackColor = Theme.Card;
		history.DefaultCellStyle.ForeColor = Theme.Ink;
		history.DefaultCellStyle.SelectionBackColor = Theme.PanelHover;
		history.DefaultCellStyle.SelectionForeColor = Theme.Ink;
		history.ColumnHeadersDefaultCellStyle.BackColor = Theme.Panel;
		history.ColumnHeadersDefaultCellStyle.ForeColor = Theme.AccentSoft;
		history.GridColor = Theme.CardEdge;
		history.RowTemplate.Height = 32;
		historyEmpty.Font = F(10f);
		panel.Controls.Add(history);
		panel.Controls.Add(historyEmpty);
		panel.Controls.Add(flowLayoutPanel);
	}

	private void BuildPerformance()
	{
		Panel panel = Page("Performance");
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false,
			AutoScroll = true
		};
		panel.Controls.Add(flowLayoutPanel);
		flowLayoutPanel.Controls.Add(TextLabel("Performance & behaviour", 22f));
		performance.AutoSize = true;
		performance.Font = F(15f, FontStyle.Bold);
		performance.ForeColor = Theme.Accent;
		flowLayoutPanel.Controls.Add(performance);
		Label label = TextLabel("Native Windows interface, with no embedded browser engine.\n\n• The dashboard and open match history update from the tracker.\n• The countdown pauses while this window is hidden or minimised.\n• Images and fonts are loaded once and reused.\n\nMemory use rises briefly while replays are analysed.", 11f, Theme.Muted);
		label.MaximumSize = new Size(720, 0);
		flowLayoutPanel.Controls.Add(label);
		BuildPerformanceOptions(flowLayoutPanel);
	}

	private void Navigate(string name)
	{
		switch (name)
		{
		case "Teamers":
			OpenTeamers();
			return;
		case "Weekly chances":
			OpenChances();
			return;
		case "Weekly rewards":
		{
			using RewardsForm rewardsForm = new RewardsForm(monitor, store);
			rewardsForm.ShowDialog(this);
			return;
		}
		case "Overlay":
			monitor.ToggleOverlay();
			return;
		case "Settings":
		{
			using SettingsForm settingsForm = new SettingsForm(monitor, store);
			settingsForm.ShowDialog(this);
			return;
		}
		}
		active = name;
		foreach (KeyValuePair<string, Panel> page in pages)
		{
			page.Value.Visible = page.Key == name;
			if (page.Key == name)
			{
				page.Value.BringToFront();
			}
		}
		foreach (Button item in navigation)
		{
			bool flag = string.Equals(item.Tag as string, name, StringComparison.Ordinal);
			item.BackColor = (flag ? Theme.PanelHover : Theme.Panel);
			item.ForeColor = (flag ? Theme.AccentSoft : Theme.Ink);
			item.FlatAppearance.BorderColor = (flag ? Theme.Accent : Theme.CardEdge);
		}
		if (name == "Match history" && state is { } currentState)
		{
			RenderHistory(currentState);
		}
		Tick();
	}

	private void OpenTeamers()
	{
		if (teamersForm == null || teamersForm.IsDisposed)
		{
			teamersForm = new TeamersForm(teamers);
			teamersForm.Show(this);
		}
		else teamersForm.Activate();
	}

	private void UpdateTeamersNotifications()
	{
		if (IsDisposed) return;
		var invitations = teamers.State?.Invitations ?? Array.Empty<TeamersInvite>();
		var button = navigation.FirstOrDefault(item => Equals(item.Tag, "Teamers"));
		if (button != null) button.Text = invitations.Length == 0 ? "Teamers" : $"Teamers ({invitations.Length})";
		var received = invitations.Where(invite => notifiedTeamersInvites.Add(invite.Id)).ToArray();
		if (received.Length == 0) return;
		if (teamersNotification == null)
		{
			teamersNotification = new NotifyIcon { Icon = AppIcon.Load() ?? SystemIcons.Information, Text = "Teamers", Visible = true };
			teamersNotification.BalloonTipClicked += (_, _) => OpenTeamers();
		}
		teamersNotification.ShowBalloonTip(5000, "Teamers",
			TeamersClient.Text($"Invito da {received[0].FromUsername} per {received[0].TeamName}",
				$"Invitation from {received[0].FromUsername} to {received[0].TeamName}"), ToolTipIcon.Info);
	}

	private void RenderHistory(MonitorState currentState)
	{
		var rows = currentState.MatchHistory
			.OrderByDescending(m => m.MatchStartUtc ?? m.AnalyzedAtUtc)
			.Take(500)
			.Select(m => new HistoryRow(
				(m.MatchStartUtc ?? m.AnalyzedAtUtc).ToLocalTime().ToString("dd MMM HH:mm", L.Culture),
				m.Playlist ?? "Unknown", m.ChestsOpened, m.OwnChests,
				m.TeammatesSeen(currentState.HistoryExtraKinds), m.TreasureFinds.Count, m.Placement, m.Eliminations))
			.ToList();
		if (history.DataSource != null && displayedHistory.SequenceEqual(rows))
		{
			history.Columns[nameof(HistoryRow.TeammatesSeen)].Visible = currentState.ShowHistoryTeammates;
			return;
		}

		displayedHistory = rows;
		history.DataSource = rows;
		historyEmpty.Visible = rows.Count == 0;
		foreach (DataGridViewColumn column in history.Columns)
		{
			column.HeaderText = MainStatusText.HistoryHeader(column.DataPropertyName);
			if (column.DataPropertyName == nameof(HistoryRow.CountedTotal))
				column.DefaultCellStyle.Font = F(10f, FontStyle.Bold);
			if (column.DataPropertyName == nameof(HistoryRow.TeammatesSeen))
				column.Visible = currentState.ShowHistoryTeammates;
		}
	}

	private async Task ImportTeammateReplay()
	{
		if (importingTeammateReplay) return;
		string title = L.Code == "it" ? "Importa replay compagno" : "Import teammate replay";
		using var dialog = new OpenFileDialog { Title = title, Filter = "Fortnite replay|*.replay" };
		if (dialog.ShowDialog(this) != DialogResult.OK) return;

		importingTeammateReplay = true;
		try
		{
			string? profile = state?.CurrentKey ?? monitor.Config.Profile;
			string path = dialog.FileName;
			var teammate = await Task.Run(() => TeammateReplayImports.ImportReplay(store, path, profile));
			monitor.RefreshPersonalStatistics();
			if (IsDisposed) return;
			int count = MatchRecord.Seen(teammate, monitor.Config.ExtraKinds());
			string message = L.Code == "it"
				? $"Replay collegato alla partita. Conteggio dal replay del compagno: {count:N0}."
				: $"Replay linked to the match. Count from the teammate's replay: {count:N0}.";
			MessageBox.Show(this, message, title);
		}
		catch (Exception error)
		{
			if (!IsDisposed) MessageBox.Show(this, error.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
		}
		finally { importingTeammateReplay = false; }
	}

	private void OnChanged(MonitorState s)
	{
		pendingState = s;
		if (IsDisposed || !IsHandleCreated || Interlocked.Exchange(ref queued, 1) != 0)
		{
			return;
		}
		try
		{
			BeginInvoke(() =>
			{
				Interlocked.Exchange(ref queued, 0);
				if (!IsDisposed)
				{
					MonitorState monitorState = pendingState;
					if ((object)monitorState != null)
					{
						Render(monitorState);
					}
				}
			});
		}
		catch (InvalidOperationException)
		{
			Interlocked.Exchange(ref queued, 0);
		}
	}

	private static void Set(Label l, string text)
	{
		if (l.Text != text)
		{
			l.Text = text;
		}
	}

	private void Render(MonitorState s)
	{
		state = s;
		if (active == "Match history") RenderHistory(s);
		chestStats?.Refresh(s);
		UpdateStatus(s);
		UpdateGoal(s);
		SetTotal(s.View.Total);
		Set(current, s.View.Current ?? "—");
		Set(today, s.Stats.Today.ToString("N0"));
		Set(average, s.Stats.Average.ToString("0.0"));
		Set(best, s.Stats.BestMatch.ToString("N0"));
		Set(treasures, s.Stats.Treasures.ToString("N0"));
		Set(profileName, DashboardText.F("{0:N0} saved matches · {1} · {2}", s.Stats.HistoryMatches, L.Culture.TextInfo.ToTitleCase(s.View.CurrentCaption), monitor.OverlayVisible ? MainStatusText.T("Overlay on") : MainStatusText.T("Overlay off")));
		Label l = last;
		LastMatchSummary lastMatch = s.Stats.LastMatch;
		Set(l, ((object)lastMatch != null) ? DashboardText.F("Last completed match   {0} chests · {1} treasures · {2} eliminations", lastMatch.Chests, lastMatch.Treasures, lastMatch.Eliminations?.ToString(L.Culture) ?? "—") : MainStatusText.T("Waiting for your first completed replay"));
		Label l2 = team;
		TeamSummary teamSummary = s.Team;
		Set(l2, ((object)teamSummary != null) ? (DashboardText.F("Observed squad ≥ {0}   |   You {1}   |   ", teamSummary.Total, teamSummary.Mine) + string.Join("  ·  ", teamSummary.Mates.Select((TeamMember m) => $"{m.Name} ≥ {m.Chests}"))) : MainStatusText.T("Squad data: enable teammate tracking in Settings. Counts are limited to replay visibility."));
		Set(notice, s.Notice ?? (s.AutoCloseOffer ? MainStatusText.T("Closing with Fortnite — click here to keep open.") : ""));
		string text = string.Join("|", s.Profiles.Select((Profile p) => p.Key + ":" + p.Name));
		if (text != profileSignature)
		{
			profileSignature = text;
			selecting = true;
			profiles.DisplayMember = "Name";
			profiles.DataSource = s.Profiles.ToList();
			selecting = false;
		}
		selecting = true;
		foreach (Profile item in profiles.Items.Cast<Profile>())
		{
			if (item.Key == s.CurrentKey)
			{
				profiles.SelectedItem = item;
				break;
			}
		}
		selecting = false;
		string text2 = string.Join("|", s.Stats.LastDays.Select((DayChests d) => $"{d.Day.Ticks}:{d.Chests}"));
		if (text2 != chartSignature)
		{
			chartSignature = text2;
			chart.SetDays(s.Stats.LastDays);
		}
	}

	private void BuildGoalCard()
	{
		goalText.AutoSize = false;
		goalText.Dock = DockStyle.Top;
		goalText.Height = 28;
		goalText.Font = F(10f, FontStyle.Bold);
		goalText.ForeColor = Theme.Ink;
		goalText.BackColor = Theme.Card;
		goalTrack.Dock = DockStyle.Bottom;
		goalTrack.Height = 12;
		goalTrack.BackColor = Theme.Panel;
		goalFill.Dock = DockStyle.Left;
		goalFill.Width = 0;
		goalFill.BackColor = Theme.Accent;
		goalTrack.Controls.Add(goalFill);
		goalCard.Controls.Add(goalTrack);
		goalCard.Controls.Add(goalText);
	}

	private void UpdateGoal(MonitorState s)
	{
		int chestGoal = monitor.Config.ChestGoal;
		if (chestGoal <= 0 || !int.TryParse(s.View.Total, NumberStyles.AllowThousands, L.Culture, out var result))
		{
			if (goalCard.Visible)
			{
				goalCard.Visible = false;
			}
			return;
		}
		if (!goalCard.Visible)
		{
			goalCard.Visible = true;
		}
		Set(goalText, (result >= chestGoal) ? DashboardText.F("Goal reached  {0:N0} / {1:N0} chests", result, chestGoal) : DashboardText.F("Chest goal   {0:N0} / {1:N0}   ·   {2:N0} to go", result, chestGoal, chestGoal - result));
		int num = (int)((double)goalTrack.Width * Math.Min(1.0, (double)result / (double)chestGoal));
		if (goalFill.Width != num)
		{
			goalFill.Width = num;
		}
	}

	private void UpdateStatus(MonitorState s)
	{
		string text;
		Color color;
		if (!Directory.Exists(monitor.Config.DemosFolder))
		{
			text = MainStatusText.T("● Replay folder not found, choose it in Settings");
			color = Theme.Coral;
		}
		else if (s.View.CurrentLive)
		{
			text = MainStatusText.T("● Match in progress, counting live");
			color = Theme.Mint;
		}
		else if (s.Stats.HistoryMatches == 0)
		{
			text = MainStatusText.T("● Ready, play a match and it will appear here");
			color = Theme.AccentSoft;
		}
		else
		{
			text = MainStatusText.T("● Ready, watching your replays");
			color = Theme.Mint;
		}
		Set(status, text);
		if (status.ForeColor != color)
		{
			status.ForeColor = color;
		}
	}

	private void SetTotal(string text)
	{
		CultureInfo culture = L.Culture;
		if (!monitor.Config.Animations || !Visible || WindowState == FormWindowState.Minimized || !int.TryParse(total.Text, NumberStyles.AllowThousands, culture, out var result) || !int.TryParse(text, NumberStyles.AllowThousands, culture, out var result2) || result2 <= result || result2 - result > 5000)
		{
			anim.Stop();
			Set(total, text);
			return;
		}
		animFrom = result;
		animTo = result2;
		animText = text;
		animStart = DateTime.UtcNow;
		anim.Start();
	}

	private void AnimateTick()
	{
		double num = Math.Min(1.0, (DateTime.UtcNow - animStart).TotalMilliseconds / 450.0);
		if (num >= 1.0)
		{
			anim.Stop();
			Set(total, animText);
		}
		else
		{
			double num2 = 1.0 - Math.Pow(1.0 - num, 3.0);
			Set(total, ((int)((double)animFrom + (double)(animTo - animFrom) * num2)).ToString("N0", L.Culture));
		}
	}

	private void SelectLive(int index, bool save)
	{
		for (int i = 0; i < livePills.Length; i++)
		{
			bool flag = i == index;
			livePills[i].BackColor = (flag ? Theme.Accent : Theme.Panel);
			livePills[i].HoverColor = (flag ? Theme.AccentHover : Theme.PanelHover);
			livePills[i].ForeColor = (flag ? Theme.AccentInk : Theme.Ink);
			livePills[i].Invalidate();
		}
		if (!save)
		{
			return;
		}
		monitor.Change((Config c) =>
		{
			c.LiveUpdate = index != 0;
			if (index != 0)
			{
				c.LiveIntervalSeconds = LiveSeconds[index];
			}
		});
	}

	private void ApplyPreset(int preset)
	{
		bool animations = preset != 0;
		int live = preset switch
		{
			1 => 2,
			0 => 3,
			_ => 1,
		};
		perfLoading = true;
		if (animToggle != null)
		{
			animToggle.Checked = animations;
		}
		if (bannerToggle != null)
		{
			bannerToggle.Checked = animations;
		}
		perfLoading = false;
		monitor.Change((Config c) =>
		{
			c.Animations = animations;
			c.ShowBanner = animations;
			c.LiveUpdate = true;
			c.LiveIntervalSeconds = LiveSeconds[live];
		});
		if (hero != null)
		{
			hero.Visible = animations;
		}
		SelectLive(live, save: false);
	}

	private void BuildPerformanceOptions(FlowLayoutPanel col)
	{
		Config config = monitor.Config;
		col.Controls.Add(TextLabel("VISUAL & PERFORMANCE OPTIONS", 9f, Theme.Muted));
		Card card = new Card
		{
			Size = new Size(700, 336),
			Radius = 14,
			Margin = new Padding(0, 0, 0, 12)
		};
		Mk("Smooth animations", 10f, FontStyle.Bold, Theme.Ink, 16, 14, 540, 22);
		Mk("Numbers count up and the live dot pulses. A short timer runs only while something is changing.", 8.5f, FontStyle.Regular, Theme.Muted, 16, 36, 620, 20);
		animToggle = new ToggleSwitch
		{
			Location = new Point(644, 18),
			Checked = config.Animations
		};
		animToggle.CheckedChanged += (object? _, EventArgs _) =>
		{
			if (!perfLoading)
			{
				monitor.Change((Config c) =>
				{
					c.Animations = animToggle.Checked;
				});
			}
		};
		card.Controls.Add(animToggle);
		Mk("Show banner", 10f, FontStyle.Bold, Theme.Ink, 16, 74, 540, 22);
		Mk("Hide the dashboard banner to draw slightly less.", 8.5f, FontStyle.Regular, Theme.Muted, 16, 96, 620, 20);
		bannerToggle = new ToggleSwitch
		{
			Location = new Point(644, 78),
			Checked = config.ShowBanner
		};
		bannerToggle.CheckedChanged += (object? _, EventArgs _) =>
		{
			if (!perfLoading)
			{
				monitor.Change((Config c) =>
				{
					c.ShowBanner = bannerToggle.Checked;
				});
				if (hero != null)
				{
					hero.Visible = bannerToggle.Checked;
				}
			}
		};
		card.Controls.Add(bannerToggle);
		Mk("Live match reading", 10f, FontStyle.Bold, Theme.Ink, 16, 134, 540, 22);
		Mk("How often the replay in progress is re-read. Faster gives fresher numbers but uses more disk and CPU.", 8.5f, FontStyle.Regular, Theme.Muted, 16, 156, 660, 20);
		string[] array = new string[4] { "Off", "Fast · 10 s", "Normal · 30 s", "Saver · 60 s" };
		for (int num = 0; num < livePills.Length; num++)
		{
			int index = num;
			PillButton pillButton = new PillButton
			{
				Text = PerformanceText.T(array[num]),
				Location = new Point(16 + num * 158, 184),
				Size = new Size(150, 32),
				Radius = 10,
				Font = Theme.Text(8.5f, FontStyle.Bold)
			};
			pillButton.Click += (object? _, EventArgs _) =>
			{
				SelectLive(index, save: true);
			};
			card.Controls.Add(pillButton);
			livePills[num] = pillButton;
		}
		SelectLive(config.LiveUpdate ? ((config.LiveIntervalSeconds <= 10) ? 1 : ((config.LiveIntervalSeconds <= 30) ? 2 : 3)) : 0, save: false);
		Mk("Quick presets", 10f, FontStyle.Bold, Theme.Ink, 16, 238, 540, 22);
		Mk("Applies animations, banner and live reading speed in one click.", 8.5f, FontStyle.Regular, Theme.Muted, 16, 260, 620, 20);
		string[] array2 = new string[3] { "Efficient", "Balanced", "Visual" };
		for (int num2 = 0; num2 < array2.Length; num2++)
		{
			int preset = num2;
			PillButton pillButton2 = new PillButton
			{
				Text = PerformanceText.T(array2[num2]),
				Location = new Point(16 + num2 * 158, 288),
				Size = new Size(150, 32),
				Radius = 10,
				Font = Theme.Text(8.5f, FontStyle.Bold)
			};
			pillButton2.Click += (object? _, EventArgs _) =>
			{
				ApplyPreset(preset);
			};
			card.Controls.Add(pillButton2);
		}
		col.Controls.Add(card);
		Label Mk(string text, float size, FontStyle style, Color colour, int x, int y, int w, int h)
		{
			Label label = new Label
			{
				Text = PerformanceText.T(text),
				Font = F(size, style),
				ForeColor = colour,
				BackColor = Theme.Card,
				Location = new Point(x, y),
				Size = new Size(w, h),
				AutoEllipsis = true
			};
			card.Controls.Add(label);
			return label;
		}
	}

	private void Tick()
	{
		if (!Visible || WindowState == FormWindowState.Minimized)
		{
			return;
		}
		pulse = !pulse;
		if (monitor.Config.Animations)
		{
			MonitorState? monitorState = state;
			if ((object)monitorState != null && monitorState.View.CurrentLive)
			{
				status.ForeColor = (pulse ? Theme.Mint : Color.FromArgb(36, 140, 96));
			}
		}
		if (active == "Dashboard")
		{
			DateTime utcNow = DateTime.UtcNow;
			DateTime dateTime = PersonalTools.Week(utcNow).AddDays(7.0);
			TimeSpan timeSpan = dateTime - utcNow;
			Set(reset, DashboardText.F("Weekly reset: {0:ddd dd MMM HH:mm} local · {1}d {2:00}h {3:00}m {4:00}s", dateTime.ToLocalTime(), timeSpan.Days, timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds));
		}
		if (!(active == "Performance"))
		{
			return;
		}
		using Process process = Process.GetCurrentProcess();
		Set(performance, PerformanceText.F("Memory in use: {0:0.0} MB   |   .NET managed memory: {1:0.0} MB", (double)process.WorkingSet64 / 1048576.0, (double)GC.GetTotalMemory(forceFullCollection: false) / 1048576.0));
	}

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		Theme.Chrome(Handle);
		MonitorState monitorState = pendingState;
		if ((object)monitorState != null)
		{
			Render(monitorState);
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			clock.Stop();
			clock.Dispose();
			anim.Stop();
			anim.Dispose();
			teamersTimer.Dispose();
			teamersNotification?.Dispose();
			teamers.Changed -= UpdateTeamersNotifications;
			monitor.Changed -= OnChanged;
		}
		base.Dispose(disposing);
		if (!disposing)
		{
			return;
		}
		banner.Dispose();
		avatar.Dispose();
		foreach (Font value in fonts.Values)
		{
			value.Dispose();
		}
		fonts.Clear();
	}
}
