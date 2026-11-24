using System;
using System.Drawing;
using System.Windows.Forms;

namespace ChestCounter;

public sealed class PersonalOverlay : Form
{
	private readonly Timer timer = new Timer
	{
		Interval = 3000
	};

	public PersonalOverlay(Func<(int total, int last, int week, int? squad)> snapshot)
	{
		PersonalOverlay personalOverlay = this;
		FormBorderStyle = FormBorderStyle.None;
		TopMost = true;
		ShowInTaskbar = false;
		BackColor = Color.Fuchsia;
		TransparencyKey = Color.Fuchsia;
		Size = new Size(390, 130);
		StartPosition = FormStartPosition.Manual;
		Location = new Point(30, 100);
		Label label = new Label
		{
			Dock = DockStyle.Fill,
			ForeColor = Color.FromArgb(255, 102, 178),
			BackColor = Color.Transparent,
			Font = new Font("Segoe UI", 15f, FontStyle.Bold)
		};
		Controls.Add(label);
		ContextMenuStrip contextMenuStrip = new ContextMenuStrip
		{
			Items = {
			{
				ToolText.T("Close"),
				(Image?)null,
				(EventHandler?)((object? _, EventArgs _) =>
				{
					personalOverlay.Close();
				})
			} }
		};
		ContextMenuStrip = contextMenuStrip;
		label.ContextMenuStrip = contextMenuStrip;
		Point origin = default;
		bool drag = false;
		label.MouseDown += (object? _, MouseEventArgs e) =>
		{
			if (e.Button == MouseButtons.Left)
			{
				origin = e.Location;
				drag = true;
			}
		};
		label.MouseMove += (object? _, MouseEventArgs e) =>
		{
			if (drag)
			{
				personalOverlay.Location = new Point(personalOverlay.Left + e.X - origin.X, personalOverlay.Top + e.Y - origin.Y);
			}
		};
		label.MouseUp += (object? _, MouseEventArgs _) =>
		{
			drag = false;
		};
		timer.Tick += (object? _, EventArgs _) =>
		{
			Update();
		};
		FormClosed += (object? _, FormClosedEventArgs _) =>
		{
			personalOverlay.timer.Dispose();
		};
		Update();
		timer.Start();
		void Update()
		{
			try
			{
				var (total, last, week, squad) = snapshot();
				label.Text = ToolText.F("2xFlaw · CHESTS {0:N0}\nPrevious {1}  · Week {2:N0}", total, last, week);
				if (squad.HasValue) label.Text += "\n" + ToolText.F("Squad week ≥ {0:N0}", week + squad.Value);
			}
			catch
			{
				label.Text = ToolText.T("Waiting for saved replay data…");
			}
		}
	}
}
