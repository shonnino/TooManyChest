using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ChestCounter;

[SupportedOSPlatform("windows")]
public sealed class InfoDialog : Form
{
	private const int W = 440;

	private const int Pad = 20;

	private const int TextX = 72;

	private InfoDialog(string title, string intro, IReadOnlyList<InfoPoint> points, string ok, string? cancel, bool owned)
	{
		Text = title;
		AutoScaleMode = AutoScaleMode.Dpi;
		AutoScaleDimensions = new SizeF(96f, 96f);
		Font = Theme.Text();
		BackColor = Theme.Bg;
		ForeColor = Theme.Ink;
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MaximizeBox = false;
		MinimizeBox = false;
		ShowInTaskbar = !owned;
		StartPosition = ((!owned) ? FormStartPosition.CenterScreen : FormStartPosition.CenterParent);
		Icon = AppIcon.Load();
		Card card = new Card
		{
			Location = new Point(20, 20),
			Size = new Size(40, 40),
			Radius = 20,
			BackColor = Theme.Panel
		};
		card.Controls.Add(new Label
		{
			Text = "i",
			Font = new Font("Georgia", 15f, FontStyle.Bold | FontStyle.Italic),
			ForeColor = Theme.Cyan,
			BackColor = Color.Transparent,
			Location = new Point(0, 0),
			Size = new Size(40, 40),
			TextAlign = ContentAlignment.MiddleCenter
		});
		Controls.Add(card);
		int y = 19;
		y = AddText(this, title, Theme.Text(12f, FontStyle.Bold), Theme.Ink, 72, y, 348) + 4;
		y = AddText(this, intro, Theme.Text(), Theme.Muted, 72, y, 348);
		y = Math.Max(y, 60) + 16;
		Card card2 = new Card
		{
			Location = new Point(20, y),
			Radius = 14,
			BackColor = Theme.Card,
			Visible = (points.Count > 0)
		};
		int num = 400;
		int num2 = 14;
		foreach (InfoPoint point in points)
		{
			Card card3 = new Card
			{
				Location = new Point(14, num2),
				Size = new Size(26, 26),
				Radius = 13,
				BackColor = Theme.Panel
			};
			card3.Controls.Add(new Label
			{
				Text = point.Glyph,
				Font = Theme.Text(10f, FontStyle.Bold),
				ForeColor = point.Color,
				BackColor = Color.Transparent,
				Location = new Point(0, 0),
				Size = new Size(26, 26),
				TextAlign = ContentAlignment.MiddleCenter
			});
			card2.Controls.Add(card3);
			int y2 = AddText(card2, point.Title, Theme.Text(9f, FontStyle.Bold), point.Color, 52, num2 + 1, num - 52 - 14);
			y2 = AddText(card2, point.Text, Theme.Text(8.75f), Theme.Ink, 52, y2, num - 52 - 14);
			num2 = Math.Max(y2, num2 + 26) + 12;
		}
		card2.Size = new Size(num, num2 + 2);
		Controls.Add(card2);
		y += ((points.Count > 0) ? (card2.Height + 18) : 6);
		PillButton pillButton = new PillButton
		{
			Text = ok,
			Size = new Size(130, 36),
			Location = new Point(290, y),
			Radius = 11,
			BackColor = Theme.Accent,
			HoverColor = Theme.AccentHover,
			ForeColor = Theme.AccentInk,
			DialogResult = DialogResult.OK
		};
		PillButton pillButton2 = new PillButton
		{
			Text = (cancel ?? ""),
			Size = new Size(150, 36),
			Location = new Point(132, y),
			Radius = 11,
			DialogResult = DialogResult.Cancel,
			Visible = (cancel != null)
		};
		Controls.Add(pillButton);
		Controls.Add(pillButton2);
		AcceptButton = pillButton;
		CancelButton = ((cancel != null) ? pillButton2 : pillButton);
		ActiveControl = pillButton;
		ClientSize = new Size(440, y + 36 + 20);
	}

	public static bool Confirm(IWin32Window? owner, string title, string intro, IReadOnlyList<InfoPoint> points, string ok, string? cancel)
	{
		using InfoDialog infoDialog = new InfoDialog(title, intro, points, ok, cancel, owner != null);
		return ((owner != null) ? infoDialog.ShowDialog(owner) : infoDialog.ShowDialog()) == DialogResult.OK;
	}

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		Theme.Chrome(Handle);
	}

	private int AddText(Control parent, string text, Font font, Color color, int x, int y, int width)
	{
		float num = (float)DeviceDpi / 96f;
		int num2 = (int)Math.Ceiling((float)TextRenderer.MeasureText(text, font, new Size((int)((float)width * num), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height / num) + 2;
		parent.Controls.Add(new Label
		{
			Text = text,
			Font = font,
			ForeColor = color,
			BackColor = parent.BackColor,
			Location = new Point(x, y),
			Size = new Size(width, num2)
		});
		return y + num2;
	}
}
