using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Windows.Forms;

namespace ChestCounter;

[SupportedOSPlatform("windows")]
internal sealed class TreasureHourStrip : UserControl
{
	public sealed class Preferences
	{
		public bool Enabled { get; set; }

		public HashSet<string> Seen { get; set; } = new HashSet<string>();
	}

	private static readonly Dictionary<string, Preferences> Saved = new Dictionary<string, Preferences>(StringComparer.OrdinalIgnoreCase);

	private readonly Store store;

	private readonly Func<bool> animations;

	private readonly string settingsPath;

	private readonly Preferences preferences;

	private readonly Label dot = new Label();

	private readonly Label status = new Label();

	private readonly CheckBox alerts = new CheckBox();

	private readonly Timer clock = new Timer
	{
		Interval = 1000
	};

	private readonly Font textFont = Theme.Text(9f, FontStyle.Bold);

	private readonly Font smallFont = Theme.Text(8f);

	private PersonalOdds? odds;

	private DateTime readAt = DateTime.MinValue;

	private DateTime hideAt = DateTime.MaxValue;

	private NotifyIcon? notification;

	private bool syncing;

	private bool pulse;

	private readonly HashSet<string> attempted = new HashSet<string>();

	public TreasureHourStrip(Store store, Func<bool> animations)
	{
		this.store = store;
		this.animations = animations;
		settingsPath = Path.Combine(store.Folder, "2xflaw-treasure-alerts.json");
		if (!Saved.TryGetValue(settingsPath, out Preferences value))
		{
			try
			{
				value = (File.Exists(settingsPath) ? JsonSerializer.Deserialize<Preferences>(File.ReadAllText(settingsPath)) : null);
			}
			catch (Exception error)
			{
				ErrorLog.Write("Treasure Hour alert preferences", error);
			}
			if (value == null)
			{
				value = new Preferences();
			}
			Preferences preferences = value;
			if (preferences.Seen == null)
			{
				HashSet<string> hashSet = (preferences.Seen = new HashSet<string>());
			}
			Saved[settingsPath] = value;
		}
		this.preferences = value;
		Height = 66;
		Margin = Padding.Empty;
		Padding = new Padding(12, 6, 12, 6);
		BackColor = Theme.Card;
		dot.Dock = DockStyle.Left;
		dot.Width = 22;
		dot.Font = textFont;
		dot.TextAlign = ContentAlignment.MiddleCenter;
		dot.Text = "●";
		status.Dock = DockStyle.Fill;
		status.Font = textFont;
		status.TextAlign = ContentAlignment.MiddleLeft;
		status.AutoEllipsis = true;
		status.UseMnemonic = false;
		alerts.Dock = DockStyle.Right;
		alerts.Width = 144;
		alerts.Font = smallFont;
		alerts.ForeColor = Theme.Ink;
		alerts.Text = ToolText.T("Notify at start");
		alerts.Checked = this.preferences.Enabled;
		Controls.Add(status);
		Controls.Add(dot);
		Controls.Add(alerts);
		alerts.CheckedChanged += (object? _, EventArgs _) =>
		{
			if (!syncing)
			{
				bool enabled = this.preferences.Enabled;
				this.preferences.Enabled = alerts.Checked;
				if (!Save())
				{
					this.preferences.Enabled = enabled;
				}
				UpdateStatus();
			}
		};
		clock.Tick += (object? _, EventArgs _) =>
		{
			UpdateStatus();
		};
		clock.Start();
		UpdateStatus();
	}

	private bool Save()
	{
		try
		{
			string text = settingsPath + ".tmp";
			File.WriteAllText(text, JsonSerializer.Serialize(preferences));
			File.Move(text, settingsPath, overwrite: true);
			return true;
		}
		catch (Exception error)
		{
			ErrorLog.Write("Save Treasure Hour alerts", error);
			return false;
		}
	}

	private static string BoostText(TreasureWindow window)
	{
		List<string> list = new List<string>();
		(string, string)[] array = new (string, string)[3]
		{
			("gold-outfit", "Gold outfit"),
			("pirate-shop-access", "Pirate shop"),
			("vehicle-decal", "Vehicle & decal")
		};
		for (int i = 0; i < array.Length; i++)
		{
			(string, string) tuple = array[i];
			double num = TreasureHours.Boost(window, tuple.Item1);
			if (num > 1.0)
			{
				list.Add(ToolText.T(tuple.Item2) + " " + num.ToString("0.#", L.Culture) + "x");
			}
		}
		return string.Join(" | ", list);
	}

	private void UpdateStatus()
	{
		if (IsDisposed)
		{
			return;
		}
		DateTime now = DateTime.UtcNow;
		if ((now - readAt).TotalSeconds >= 30.0 || now < readAt)
		{
			readAt = now;
			try
			{
				string path = Path.Combine(store.Folder, "2xflaw-odds.json");
				odds = (File.Exists(path) ? JsonSerializer.Deserialize<PersonalOdds>(File.ReadAllText(path)) : PersonalOdds.Seed());
			}
			catch (Exception error)
			{
				odds = null;
				ErrorLog.Write("Treasure Hour schedule", error);
			}
		}
		TreasureWindow treasureWindow = odds?.Windows.Where((TreasureWindow w) => w.EndUtc > w.StartUtc).OrderBy((TreasureWindow w) => w.StartUtc).FirstOrDefault((TreasureWindow w) => now >= w.StartUtc && now < w.EndUtc);
		TreasureWindow treasureWindow2 = odds?.Windows.Where((TreasureWindow w) => w.StartUtc > now && w.EndUtc > w.StartUtc).OrderBy((TreasureWindow w) => w.StartUtc).FirstOrDefault();
		if (notification != null && now >= hideAt)
		{
			notification.Visible = false;
			hideAt = DateTime.MaxValue;
		}
		if (treasureWindow != null && !TreasureHours.IsUnconfirmed(treasureWindow) && preferences.Enabled)
		{
			string item = treasureWindow.StartUtc.ToUniversalTime().ToString("O");
			if (!preferences.Seen.Contains(item) && attempted.Add(item))
			{
				preferences.Seen.Add(item);
				if (Save())
				{
					try
					{
						if (notification == null)
						{
							notification = new NotifyIcon
							{
								Icon = SystemIcons.Information,
								Text = "Chest Counter"
							};
						}
						notification.Visible = true;
						notification.ShowBalloonTip(8000, ToolText.T("Treasure Hour is live"), ToolText.F("{0}. Ends {1:HH:mm} local.", BoostText(treasureWindow), treasureWindow.EndUtc.ToLocalTime()), ToolTipIcon.Info);
						hideAt = now.AddSeconds(12.0);
					}
					catch (Exception error2)
					{
						ErrorLog.Write("Treasure Hour notification", error2);
					}
				}
				else
				{
					preferences.Seen.Remove(item);
				}
			}
		}
		Form form = FindForm();
		if (form == null || (form.Visible && form.WindowState != FormWindowState.Minimized))
		{
			syncing = true;
			alerts.Checked = preferences.Enabled;
			syncing = false;
			bool flag = treasureWindow != null && !TreasureHours.IsUnconfirmed(treasureWindow);
			string text;
			if (treasureWindow == null)
			{
				if (treasureWindow2 == null)
				{
					text = ((odds == null) ? (ToolText.T("Treasure Hour schedule unavailable") + "\n" + ToolText.T("Check your local 2xflaw-odds.json file.")) : (ToolText.T("No upcoming Treasure Hours") + "\n" + ToolText.T("Notifications are optional. Uses your local odds schedule.")));
				}
				else
				{
					text = ToolText.F("Next Treasure Hour: {0:ddd dd MMM HH:mm} local", treasureWindow2.StartUtc.ToLocalTime()) + "\n" + ToolText.T("Notifications are optional. Uses your local odds schedule.");
				}
			}
			else
			{
				TimeSpan timeSpan = treasureWindow.EndUtc - now;
				text = ToolText.F("TREASURE HOUR LIVE - ends in {0:00}:{1:00}:{2:00}", (int)timeSpan.TotalHours, timeSpan.Minutes, timeSpan.Seconds) + "\n" + (flag ? BoostText(treasureWindow) : ToolText.T("Boosts unconfirmed - normal odds apply"));
			}
			if (status.Text != text)
			{
				status.Text = text;
			}
			pulse = !pulse;
			Label label = dot;
			Color foreColor;
			if (flag)
			{
				foreColor = ((animations() && pulse) ? Color.FromArgb(170, 119, 55) : Theme.Amber);
			}
			else
			{
				foreColor = Theme.Muted;
			}
			label.ForeColor = foreColor;
			status.ForeColor = (flag ? Theme.Amber : Theme.Muted);
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			clock.Stop();
			clock.Dispose();
			notification?.Dispose();
		}
		base.Dispose(disposing);
		if (disposing)
		{
			textFont.Dispose();
			smallFont.Dispose();
		}
	}
}
