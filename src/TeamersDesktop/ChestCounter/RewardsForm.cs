using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ChestCounter;

[SupportedOSPlatform("windows")]
public sealed class RewardsForm : Form
{
	private const int PictureSize = 168;

	private const int PictureRadius = 16;

	private const int CardWidth = 188;

	private const int CardHeight = 262;

	private const int BarWidth = 280;

	private readonly Monitor _monitor;

	private readonly List<Bitmap> _images = new List<Bitmap>();

	private readonly Timer _clock = new Timer
	{
		Interval = 1000
	};

	private readonly Timer _anim = new Timer
	{
		Interval = 16
	};

	private readonly Label _countdown;

	private readonly Panel _barFill;

	private double _fillNow;

	private double _fillTarget;

	public RewardsForm(Monitor monitor, Store store)
	{
		_monitor = monitor;
		if (WeeklyReset.MigratePirateOwnership(monitor.Config, DateTime.UtcNow))
		{
			store.SaveConfig(monitor.Config);
		}
		Text = AppText.T("Weekly rewards");
		AutoScaleMode = AutoScaleMode.Dpi;
		AutoScaleDimensions = new SizeF(96f, 96f);
		Font = Theme.Text();
		BackColor = Theme.Bg;
		ForeColor = Theme.Ink;
		StartPosition = FormStartPosition.CenterParent;
		ClientSize = new Size(660, 700);
		MinimumSize = new Size(460, 420);
		ShowInTaskbar = false;
		Icon = AppIcon.Load();
		string folder = Path.Combine(store.Folder, "rewards");
		try
		{
			Directory.CreateDirectory(folder);
		}
		catch
		{
		}
		Panel panel = new Panel
		{
			Dock = DockStyle.Top,
			Height = 104,
			BackColor = Theme.Bg
		};
		Label value = new Label
		{
			Text = AppText.T("Weekly rewards"),
			Font = Theme.Text(18f, FontStyle.Bold),
			ForeColor = Theme.Ink,
			AutoSize = true,
			Location = new Point(20, 10),
			BackColor = Theme.Bg
		};
		Label counter = new Label
		{
			Font = Theme.Text(10f, FontStyle.Bold),
			ForeColor = Theme.Mint,
			AutoSize = true,
			Location = new Point(22, 46),
			BackColor = Theme.Bg
		};
		Panel panel2 = new Panel
		{
			Location = new Point(22, 74),
			Size = new Size(280, 6),
			BackColor = Theme.Panel
		};
		_barFill = new Panel
		{
			Location = new Point(0, 0),
			Size = new Size(0, 6),
			BackColor = Theme.Mint
		};
		panel2.Controls.Add(_barFill);
		PillButton pillButton = new PillButton
		{
			Text = ToolText.T("Open pictures folder"),
			Size = new Size(170, 32),
			Radius = 10,
			Font = Theme.Text(8.5f, FontStyle.Bold),
			Anchor = (AnchorStyles.Top | AnchorStyles.Right),
			Location = new Point(470, 16)
		};
		pillButton.Click += (object? _, EventArgs _) =>
		{
			try
			{
				Process.Start(new ProcessStartInfo(folder)
				{
					UseShellExecute = true
				})?.Dispose();
			}
			catch
			{
			}
		};
		_countdown = new Label
		{
			Font = Theme.Text(10f, FontStyle.Bold),
			ForeColor = Theme.Cyan,
			BackColor = Theme.Bg,
			AutoSize = false,
			Size = new Size(280, 20),
			Location = new Point(360, 56),
			Anchor = (AnchorStyles.Top | AnchorStyles.Right),
			TextAlign = ContentAlignment.MiddleRight,
			UseMnemonic = false
		};
		Label value2 = new Label
		{
			Text = ToolText.T("Weekly prizes reset; Pirate Shop is permanent"),
			Font = Theme.Text(8f),
			ForeColor = Theme.Muted,
			BackColor = Theme.Bg,
			AutoSize = false,
			Size = new Size(280, 18),
			Location = new Point(360, 78),
			Anchor = (AnchorStyles.Top | AnchorStyles.Right),
			TextAlign = ContentAlignment.MiddleRight,
			UseMnemonic = false
		};
		panel.Controls.Add(value);
		panel.Controls.Add(counter);
		panel.Controls.Add(panel2);
		panel.Controls.Add(pillButton);
		panel.Controls.Add(_countdown);
		panel.Controls.Add(value2);
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			AutoScroll = true,
			BackColor = Theme.Bg,
			Padding = new Padding(14, 6, 14, 14)
		};
		Controls.Add(flowLayoutPanel);
		Controls.Add(panel);
		Dictionary<string, bool> owned = _monitor.Config.OwnedRewards;
		(string, string)[] all = RewardPictures.All;
		for (int num = 0; num < all.Length; num++)
		{
			(string, string) tuple = all[num];
			string item = tuple.Item1;
			string item2 = tuple.Item2;
			string rewardKey = item;
			Card card = new Card
			{
				Size = new Size(188, 262),
				Radius = 16,
				Margin = new Padding(6)
			};
			Card card2 = new Card
			{
				Location = new Point(10, 10),
				Size = new Size(168, 168),
				Radius = 16,
				BackColor = Theme.Panel
			};
			Bitmap bitmap = RewardPictures.Load(folder, item, 168, 16);
			_images.Add(bitmap);
			card2.Controls.Add(new PictureBox
			{
				Dock = DockStyle.Fill,
				SizeMode = PictureBoxSizeMode.Zoom,
				Image = bitmap,
				BackColor = Theme.Panel
			});
			card.Controls.Add(card2);
			card.Controls.Add(new Label
			{
				Text = WeeklyChanceText.T((item == "pirate-shop-access") ? (item2 + " (all time)") : item2),
				UseMnemonic = false,
				Font = Theme.Text(9.5f, FontStyle.Bold),
				ForeColor = Theme.Ink,
				BackColor = Theme.Card,
				Location = new Point(14, 186),
				Size = new Size(160, 36),
				AutoEllipsis = true
			});
			TickBadge tick = new TickBadge
			{
				Location = new Point(14, 226),
				On = IsOwned(item),
				BackColor = Theme.Card
			};
			Label state = new Label
			{
				Font = Theme.Text(9f, FontStyle.Bold),
				BackColor = Theme.Card,
				Location = new Point(52, 231),
				Size = new Size(122, 20)
			};
			ShowOwned();
			tick.Toggled += (object? _, EventArgs _) =>
			{
				string ownedKey = WeeklyReset.OwnedKey(rewardKey, DateTime.UtcNow);
				_monitor.Change((Config c) =>
				{
					c.OwnedRewards[ownedKey] = tick.On;
				});
				ShowOwned();
				UpdateCounter();
			};
			card.Controls.Add(tick);
			card.Controls.Add(state);
			flowLayoutPanel.Controls.Add(card);
			void ShowOwned()
			{
				state.Text = ToolText.T(tick.On ? "Owned" : "Not owned");
				state.ForeColor = (tick.On ? Theme.Mint : Theme.Muted);
			}
		}
		_anim.Tick += (object? _, EventArgs _) =>
		{
			_fillNow += (_fillTarget - _fillNow) * 0.2;
			if (Math.Abs(_fillTarget - _fillNow) < 0.002)
			{
				_fillNow = _fillTarget;
				_anim.Stop();
			}
			_barFill.Width = (int)Math.Round(280.0 * _fillNow);
		};
		_clock.Tick += (object? _, EventArgs _) =>
		{
			UpdateCountdown();
		};
		UpdateCountdown();
		_clock.Start();
		UpdateCounter();
		bool IsOwned(string key)
		{
			bool value3;
			return owned.TryGetValue(WeeklyReset.OwnedKey(key, DateTime.UtcNow), out value3) & value3;
		}
		void UpdateCounter()
		{
			int num2 = RewardPictures.All.Count(((string Key, string Name) r) => IsOwned(r.Key));
			counter.Text = $"{num2}/{RewardPictures.All.Length} owned";
			_fillTarget = (double)num2 / (double)RewardPictures.All.Length;
			if (!_anim.Enabled)
			{
				_anim.Start();
			}
		}
	}

	private void UpdateCountdown()
	{
		TimeSpan timeSpan = WeeklyReset.Remaining(DateTime.UtcNow);
		if (timeSpan < TimeSpan.Zero)
		{
			timeSpan = TimeSpan.Zero;
		}
		_countdown.Text = ToolText.F("Resets in {0}d {1:00}h {2:00}m {3:00}s", timeSpan.Days, timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);
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
			_anim.Stop();
			_anim.Dispose();
			foreach (Bitmap image in _images)
			{
				image.Dispose();
			}
			_images.Clear();
		}
		base.Dispose(disposing);
	}
}
