using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ChestCounter;

[SupportedOSPlatform("windows")]
public sealed class SettingsForm : Form
{
	private static readonly string[] UpdateLabels = new string[3]
	{
		L.T("Preguntar", "Ask"),
		L.T("Automáticas", "Automatic"),
		L.T("Nunca", "Never")
	};

	private static readonly string[] UpdateModes = new string[3] { "prompt", "auto", "off" };

	private static readonly string[] UpdateHints = new string[3]
	{
		L.T("Te pregunta antes de instalar una versión nueva", "Asks before installing a new version"),
		L.T("Instala las versiones nuevas entre partidas", "Installs new versions between matches"),
		L.T("No busca versiones nuevas", "Never checks for new versions")
	};

	private const int LeftX = 15;

	private const int LeftW = 267;

	private const int RightX = 297;

	private const int RightW = 280;

	private const int MaxInitial = 1000000;

	private const int DefaultUpdateMode = 1;

	private readonly Monitor _monitor;

	private readonly ToggleSwitch _fortnite;

	private readonly ToggleSwitch _report;

	private readonly PillButton[] _updates = new PillButton[UpdateModes.Length];

	private readonly TextBox _folder;

	private readonly TextBox _initial;

	private Label? _updateHint;

	private bool _loading;

	public SettingsForm(Monitor monitor, Store store)
	{
		_monitor = monitor;
		Config config = monitor.Config;
		Text = L.T("Ajustes", "Settings");
		AutoScaleMode = AutoScaleMode.Dpi;
		AutoScaleDimensions = new SizeF(96f, 96f);
		Font = Theme.Text();
		BackColor = Theme.Bg;
		ForeColor = Theme.Ink;
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MaximizeBox = false;
		MinimizeBox = false;
		ShowInTaskbar = false;
		StartPosition = FormStartPosition.CenterParent;
		ClientSize = new Size(592, 690);
		Icon = AppIcon.Load();
		Lbl(this, L.T("AJUSTES", "SETTINGS"), 8.5f, FontStyle.Bold, Theme.Cyan, new Point(15, 14), new Size(200, 18));
		Lbl(this, L.F("Versión {0}", "Version {0}", AppInfo.VersionText), 8f, FontStyle.Regular, Theme.Muted, new Point(477, 13), new Size(100, 18), ContentAlignment.TopRight);
		AddLanguagePicker(config.Language, new Point(297, 9));
		Card card = AddCard(15, 40, 267, 170, 16);
		CardTitle(card, L.T("Opciones", "Options"));
		_fortnite = AddToggleRow(card, L.T("Abrir con Fortnite", "Open with Fortnite"), L.T("Cuenta tus partidas sin pensar en ello", "Counts your matches on its own"), 24, config.StartWithFortnite);
		_fortnite.CheckedChanged += (object? _, EventArgs _) =>
		{
			if (!_loading && !_monitor.SetStartWithFortnite(_fortnite.Checked))
			{
				_loading = true;
				_fortnite.Checked = !_fortnite.Checked;
				_loading = false;
				MessageBox.Show(this, L.T("No se puede desde aquí: abre el contador con ChestCounter.exe.", "Not possible from here: open the counter with ChestCounter.exe."), L.T("Ajustes", "Settings"), MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			}
		};
		_report = AddToggleRow(card, L.T("Abrir el informe al iniciar", "Open the report at startup"), null, 64, config.OpenReportOnStart);
		_report.CheckedChanged += (object? _, EventArgs _) =>
		{
			if (!_loading)
			{
				_monitor.Change((Config c) =>
				{
					c.OpenReportOnStart = _report.Checked;
				});
			}
		};
		ToggleSwitch mates = AddToggleRow(card, L.T("Compañeros en el informe", "Teammates in the report"), L.T("Incluye sus cofres en tus totales", "Include their chests in your totals"), 94, config.ShowTeammates);
		mates.CheckedChanged += (object? _, EventArgs _) =>
		{
			if (!_loading)
			{
				if (mates.Checked && !InfoDialog.Confirm(this, L.T("Compañeros en el informe", "Teammates in the report"), L.T("El informe, el historial y el overlay incluirán los cofres de tus compañeros.", "The report, match history and overlay will include your teammates’ chests."), new InfoPoint[2]
				{
					ExactPoint(),
					ApproxPoint()
				}, L.T("Activar", "Turn on"), L.T("Cancelar", "Cancel")))
				{
					Revert(mates);
				}
				else
				{
					_monitor.SetShowTeammates(mates.Checked);
				}
			}
		};
		Card card2 = AddCard(15, 219, 267, 116, 16);
		CardTitle(card2, L.T("Contar también como cofre", "Also count as a chest"));
		ToggleSwitch ammo = AddToggleRow(card2, L.T("Cajas de munición", "Ammo boxes"), null, 26, config.CountAmmoBoxes);
		ammo.CheckedChanged += (object? _, EventArgs _) =>
		{
			if (!_loading)
			{
				_monitor.SetCountExtra((Config c) =>
				{
					c.CountAmmoBoxes = ammo.Checked;
				});
			}
		};
		ToggleSwitch food = AddToggleRow(card2, L.T("Cajas de comida", "Food boxes"), null, 56, config.CountFoodBoxes);
		food.CheckedChanged += (object? _, EventArgs _) =>
		{
			if (!_loading)
			{
				_monitor.SetCountExtra((Config c) =>
				{
					c.CountFoodBoxes = food.Checked;
				});
			}
		};
		ToggleSwitch cannons = AddToggleRow(card2, L.T("Cañones de botín", "Loot cannons"), null, 86, config.CountLootCannons);
		cannons.CheckedChanged += (object? _, EventArgs _) =>
		{
			if (!_loading)
			{
				_monitor.SetCountExtra((Config c) =>
				{
					c.CountLootCannons = cannons.Checked;
				});
			}
		};
		Card card3 = AddCard(297, 169, 280, 94, 14);
		CardTitle(card3, L.T("Actualizaciones", "Updates"));
		int num = 80;
		for (int num2 = 0; num2 < UpdateModes.Length; num2++)
		{
			int index = num2;
			PillButton pillButton = new PillButton
			{
				Text = UpdateLabels[num2],
				Location = new Point(13 + num2 * (num + 6), 38),
				Size = new Size(num, 32),
				Radius = 10,
				Font = Theme.Text(8.5f, FontStyle.Bold)
			};
			pillButton.Click += (object? _, EventArgs _) =>
			{
				SelectUpdateMode(index, save: true);
			};
			card3.Controls.Add(pillButton);
			_updates[num2] = pillButton;
		}
		_updateHint = Lbl(card3, UpdateHints[1], 7.5f, FontStyle.Regular, Theme.Muted, new Point(13, 74), new Size(254, 16));
		int num3 = Array.IndexOf(UpdateModes, config.UpdateMode);
		SelectUpdateMode((num3 < 0) ? 1 : num3, save: false);
		Card card4 = AddCard(15, 344, 267, 94, 14);
		CardTitle(card4, L.T("Carpeta de replays", "Replays folder"));
		_folder = AddField(card4, new Point(13, 26), 169, config.DemosFolder);
		PillButton pillButton2 = new PillButton
		{
			Text = L.T("Cambiar", "Change"),
			Location = new Point(188, 26),
			Size = new Size(66, 30),
			Radius = 10,
			Font = Theme.Text(8.5f, FontStyle.Bold)
		};
		pillButton2.Click += (object? _, EventArgs _) =>
		{
			ChooseFolder();
		};
		card4.Controls.Add(pillButton2);
		Lbl(card4, L.T("Tus datos se guardan en ", "Your data is saved in ") + store.Folder, 7.5f, FontStyle.Regular, Theme.Muted, new Point(13, 62), new Size(241, 28));
		Card card5 = AddCard(297, 40, 280, 120, 16);
		CardTitle(card5, L.T("Cofres antes del contador", "Chests before the counter"));
		_initial = new TextBox
		{
			BorderStyle = BorderStyle.None,
			BackColor = Theme.Card,
			ForeColor = Theme.Amber,
			Font = Theme.Text(24f, FontStyle.Bold),
			Location = new Point(15, 36),
			Width = 160,
			MaxLength = 7,
			Text = Math.Min(1000000, monitor.InitialChests).ToString(L.Culture)
		};
		card5.Controls.Add(_initial);
		PillButton pillButton3 = new PillButton
		{
			Text = "+",
			Location = new Point(233, 32),
			Size = new Size(34, 28),
			Radius = 9,
			Font = Theme.Text(11f, FontStyle.Bold)
		};
		PillButton pillButton4 = new PillButton
		{
			Text = "−",
			Location = new Point(233, 64),
			Size = new Size(34, 28),
			Radius = 9,
			Font = Theme.Text(11f, FontStyle.Bold)
		};
		pillButton3.Click += (object? _, EventArgs _) =>
		{
			StepInitial(10);
		};
		pillButton4.Click += (object? _, EventArgs _) =>
		{
			StepInitial(-10);
		};
		card5.Controls.Add(pillButton3);
		card5.Controls.Add(pillButton4);
		_initial.KeyPress += (object? _, KeyPressEventArgs e) =>
		{
			e.Handled = !char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar);
		};
		_initial.TextChanged += (object? _, EventArgs _) =>
		{
			if (!_loading)
			{
				_monitor.SetInitialChests(ParseInitial());
			}
		};
		string shownProfileName = monitor.ShownProfileName;
		Lbl(card5, (shownProfileName != null) ? L.F("estimado · se suma al total de {0}", "estimate · added to {0}'s total", shownProfileName) : L.T("estimado · se suma a tu total", "estimate · added to your total"), 8f, FontStyle.Regular, Theme.Muted, new Point(16, 88), new Size(210, 16));
		Card card6 = AddCard(297, 272, 280, 94, 14);
		CardTitle(card6, L.T("Overlay para OBS", "Overlay for OBS"));
		string obsUrl = ObsServer.Url;
		Card card7 = new Card
		{
			Location = new Point(197, 6),
			Size = new Size(70, 18),
			Radius = 9,
			BackColor = Theme.Panel
		};
		Lbl(card7, (obsUrl != null) ? L.T("● ACTIVO", "● ON") : L.T("● NO", "● OFF"), 7f, FontStyle.Bold, (obsUrl != null) ? Theme.Mint : Theme.Red, new Point(0, 0), new Size(70, 18), ContentAlignment.MiddleCenter).BackColor = Color.Transparent;
		card6.Controls.Add(card7);
		TextBox obs = AddField(card6, new Point(13, 41), 182, obsUrl ?? L.T("Puerto ocupado", "Port in use"));
		PillButton copy = new PillButton
		{
			Text = L.T("Copiar", "Copy"),
			Location = new Point(201, 41),
			Size = new Size(66, 30),
			Radius = 10,
			Font = Theme.Text(8.5f, FontStyle.Bold),
			Enabled = (obsUrl != null)
		};
		copy.Click += (object? _, EventArgs _) =>
		{
			try
			{
				Clipboard.SetText(obsUrl);
				copy.Text = "✓";
			}
			catch
			{
				obs.Focus();
				obs.SelectAll();
			}
		};
		card6.Controls.Add(copy);
		Card parent = AddCard(297, 375, 280, 55, 12);
		Lbl(parent, L.T("Atajos del overlay", "Overlay shortcuts"), 7.5f, FontStyle.Regular, Theme.Muted, new Point(13, 7), new Size(254, 17));
		Lbl(parent, L.T("Ctrl+Mayús+O mostrar, L fijar, K compacto", "Ctrl+Shift+O show, L lock, K compact"), 8f, FontStyle.Bold, Theme.Ink, new Point(13, 27), new Size(254, 18));
		PillButton pillButton5 = new PillButton
		{
			Text = L.T("Restablecer estadísticas…", "Reset statistics…"),
			Location = new Point(15, 447),
			Size = new Size(267, 32),
			Radius = 10,
			Font = Theme.Text(8.5f, FontStyle.Bold),
			ForeColor = Theme.Red
		};
		pillButton5.Click += (object? _, EventArgs _) =>
		{
			ResetStats();
		};
		Controls.Add(pillButton5);
		PillButton pillButton6 = new PillButton
		{
			Text = L.T("Cerrar", "Close"),
			Location = new Point(297, 439),
			Size = new Size(280, 40),
			Radius = 12,
			BackColor = Theme.Accent,
			HoverColor = Theme.AccentHover,
			ForeColor = Theme.AccentInk,
			DialogResult = DialogResult.OK
		};
		Controls.Add(pillButton6);
		Card look = AddCard(15, 490, 562, 136, 14);
		CardTitle(look, L.T("Overlay y objetivo", "Overlay & goal"));
		int apOpacity = Math.Clamp(config.OverlayOpacity, 40, 100);
		int apGoal = Math.Max(0, config.ChestGoal);
		ApStepRow(L.T("Opacidad del overlay", "Overlay opacity"), 30, () => apOpacity + "%", (int d) =>
		{
			apOpacity = Math.Clamp(apOpacity + d * 10, 40, 100);
			_monitor.Change((Config c) =>
			{
				c.OverlayOpacity = apOpacity;
			});
			NativeOverlay.SetOpacity(apOpacity);
		});
		ApStepRow(L.T("Objetivo de cofres", "Chest goal"), 62, () => (apGoal != 0) ? apGoal.ToString("N0", L.Culture) : L.T("Sin objetivo", "Off"), (int d) =>
		{
			apGoal = Math.Clamp(apGoal + d * 100, 0, 100000);
			_monitor.Change((Config c) =>
			{
				c.ChestGoal = apGoal;
			});
		});
		ToggleSwitch apCompact = AddToggleRow(look, L.T("Overlay compacto", "Compact overlay"), L.T("Solo total y partida actual", "Total and match counter only"), 94, config.OverlayCompact);
		apCompact.CheckedChanged += (object? _, EventArgs _) =>
		{
			if (!_loading)
			{
				_monitor.Change((Config c) =>
				{
					c.OverlayCompact = apCompact.Checked;
				});
				NativeOverlay.SetCompact(apCompact.Checked);
			}
		};
		PillButton pillButton7 = new PillButton
		{
			Text = L.T("Restablecer posición", "Reset position"),
			Location = new Point(335, 94),
			Size = new Size(150, 28),
			Radius = 9,
			Font = Theme.Text(8.5f, FontStyle.Bold)
		};
		pillButton7.Click += (object? _, EventArgs _) =>
		{
			NativeOverlay.ResetPosition();
		};
		look.Controls.Add(pillButton7);
		AddLinks(AddCard(15, 636, 562, 44, 12), L.T("Hecho por", "Made by"), "2xFlaw", "https://github.com/2xFlaw/ChestCounter", Array.Empty<(string, string, string)>());
		FitTexts(this);
		AcceptButton = pillButton6;
		CancelButton = pillButton6;
		ActiveControl = pillButton6;
		void ApStepRow(string title, int top, Func<string> text, Action<int> step)
		{
			Lbl(look, title, 9.5f, FontStyle.Bold, Theme.Ink, new Point(13, top + 4), new Size(200, 18));
			Label apValue = Lbl(look, text(), 9.5f, FontStyle.Bold, Theme.Cyan, new Point(345, top + 4), new Size(120, 18), ContentAlignment.MiddleRight);
			PillButton pillButton8 = new PillButton
			{
				Text = "−",
				Location = new Point(475, top),
				Size = new Size(34, 28),
				Radius = 9,
				Font = Theme.Text(11f, FontStyle.Bold)
			};
			PillButton pillButton9 = new PillButton
			{
				Text = "+",
				Location = new Point(515, top),
				Size = new Size(34, 28),
				Radius = 9,
				Font = Theme.Text(11f, FontStyle.Bold)
			};
			pillButton8.Click += (object? _, EventArgs _) =>
			{
				step(-1);
				apValue.Text = text();
			};
			pillButton9.Click += (object? _, EventArgs _) =>
			{
				step(1);
				apValue.Text = text();
			};
			look.Controls.Add(pillButton8);
			look.Controls.Add(pillButton9);
		}
	}

	private void AddLanguagePicker(string? current, Point at)
	{
		string chosen = ((current != null && L.Languages.Any(((string Code, string Name) l) => l.Code == current)) ? current : null);
		PillButton button = new PillButton
		{
			Text = NameOf(chosen) + "  ▾",
			Location = at,
			Size = new Size(172, 26),
			Radius = 9,
			Font = Theme.Text(8.5f, FontStyle.Bold)
		};
		ContextMenuStrip menu = new ContextMenuStrip
		{
			Renderer = new DarkMenuRenderer(),
			ShowImageMargin = false,
			ShowCheckMargin = false,
			BackColor = Theme.Panel,
			Font = Theme.Text(),
			Padding = new Padding(4)
		};
		AddItem(null);
		menu.Items.Add(new ToolStripSeparator());
		foreach (var language in L.Languages)
		{
			string item = language.Code;
			AddItem(item);
		}
		button.ContextMenuStrip = menu;
		button.Click += (object? _, EventArgs _) =>
		{
			menu.Show(button, new Point(0, button.Height + 4));
		};
		Controls.Add(button);
		void AddItem(string? code)
		{
			ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem(((code == chosen) ? "✓   " : "      ") + NameOf(code))
			{
				ForeColor = ((code == chosen) ? Theme.Cyan : Theme.Ink),
				Padding = new Padding(4, 3, 18, 3)
			};
			toolStripMenuItem.Click += (object? _, EventArgs _) =>
			{
				Choose(code);
			};
			menu.Items.Add(toolStripMenuItem);
		}
		void Choose(string? code)
		{
			if (!(code == chosen))
			{
				chosen = code;
				button.Text = NameOf(code) + "  ▾";
				foreach (ToolStripMenuItem item2 in menu.Items.OfType<ToolStripMenuItem>())
				{
					bool flag = item2.Text?.TrimStart(new char[2] { '✓', ' ' }) == NameOf(code);
					item2.Text = (flag ? "✓   " : "      ") + item2.Text?.TrimStart(new char[2] { '✓', ' ' });
					item2.ForeColor = (flag ? Theme.Cyan : Theme.Ink);
				}
				_monitor.Change((Config c) =>
				{
					c.Language = code;
				});
				if (InfoDialog.Confirm(this, L.T("Idioma", "Language"), L.T("El nuevo idioma se aplica al reiniciar el contador. Tu historial y tus ajustes se conservan.", "The new language applies when the counter restarts. Your history and settings are kept."), Array.Empty<InfoPoint>(), L.T("Reiniciar ahora", "Restart now"), L.T("Más tarde", "Later")))
				{
					Close();
					_monitor.Restart();
				}
			}
		}
		static string NameOf(string? code)
		{
			if (code != null)
			{
				return L.Languages.First(((string Code, string Name) l) => l.Code == code).Name;
			}
			return L.F("Sistema ({0})", "System ({0})", L.Languages.FirstOrDefault(((string Code, string Name) l) => l.Code == L.SystemCode).Name);
		}
	}

	private static void FitTexts(Control parent)
	{
		foreach (Control control in parent.Controls)
		{
			FitTexts(control);
			if (control is Label label)
			{
				if (label.AutoEllipsis)
				{
					goto IL_0042;
				}
			}
			else if (control is PillButton)
			{
				goto IL_0042;
			}
			bool flag = false;
			goto IL_004a;
			IL_0042:
			flag = true;
			goto IL_004a;
			IL_004a:
			if (!flag || string.IsNullOrEmpty(control.Text))
			{
				continue;
			}
			Font font = control.Font;
			if (!(control is Label) || !((double)control.Height > (double)font.Height * 1.8))
			{
				int num = control.Width - ((control is PillButton) ? 12 : 2);
				while (font.Size > 6.5f && TextRenderer.MeasureText(control.Text, font).Width > num)
				{
					font = new Font(font.FontFamily, font.Size - 0.25f, font.Style);
				}
				if (font != control.Font)
				{
					control.Font = font;
				}
			}
		}
	}

	private static InfoPoint ExactPoint()
	{
		return new InfoPoint("✓", Theme.Mint, L.T("Exacto", "Exact"), L.T("Con quién juegas, partidas juntos y victorias.", "Who you play with, matches together and wins."));
	}

	private static InfoPoint ApproxPoint()
	{
		return new InfoPoint("≈", Theme.Amber, L.T("Cofres en las repeticiones", "Chests in replays"), L.T("Tu repetición puede mostrar solo parte de los cofres de un compañero. Importa su repetición de la misma partida en el historial para usar su recuento personal.", "Your replay may show only some of a teammate’s chests. Import their replay of the same match in Match History to use their personal count."));
	}

	private void Revert(ToggleSwitch toggle)
	{
		_loading = true;
		toggle.Checked = !toggle.Checked;
		_loading = false;
	}

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		Theme.Chrome(Handle);
	}

	private void SelectUpdateMode(int index, bool save)
	{
		for (int i = 0; i < _updates.Length; i++)
		{
			bool flag = i == index;
			_updates[i].BackColor = (flag ? Theme.Accent : Theme.Panel);
			_updates[i].HoverColor = (flag ? Theme.AccentHover : Theme.PanelHover);
			_updates[i].ForeColor = (flag ? Theme.AccentInk : Theme.Ink);
			_updates[i].Invalidate();
		}
		if (_updateHint != null)
		{
			_updateHint.Text = UpdateHints[index];
		}
		if (save)
		{
			string chosen = UpdateModes[index];
			_monitor.Change((Config c) =>
			{
				c.UpdateMode = chosen;
			});
		}
	}

	private int ParseInitial()
	{
		if (!int.TryParse(_initial.Text, NumberStyles.None, L.Culture, out var result))
		{
			return 0;
		}
		return Math.Clamp(result, 0, 1000000);
	}

	private void StepInitial(int delta)
	{
		int num = Math.Clamp(ParseInitial() + delta, 0, 1000000);
		_initial.Text = num.ToString(L.Culture);
	}

	private void ChooseFolder()
	{
		using FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog
		{
			Description = L.T("Carpeta de replays de Fortnite", "Fortnite replays folder"),
			UseDescriptionForTitle = true,
			SelectedPath = (Directory.Exists(_folder.Text) ? _folder.Text : "")
		};
		if (folderBrowserDialog.ShowDialog(this) == DialogResult.OK)
		{
			_monitor.ChangeDemosFolder(folderBrowserDialog.SelectedPath);
			_folder.Text = folderBrowserDialog.SelectedPath;
		}
	}

	private Card AddCard(int x, int y, int w, int h, int radius)
	{
		Card card = new Card
		{
			Location = new Point(x, y),
			Size = new Size(w, h),
			Radius = radius,
			BackColor = Theme.Card
		};
		Controls.Add(card);
		return card;
	}

	private static void AddLinks(Card footer, string title, string webText, string webUrl, (string Tip, string Url, string Logo)[] networks)
	{
		Font font = Theme.Text(8f);
		int width = TextRenderer.MeasureText(title, font).Width;
		Lbl(footer, title, 8f, FontStyle.Regular, Theme.Muted, new Point(13, 2), new Size(width, footer.Height - 4), ContentAlignment.MiddleLeft);
		Font font2 = Theme.Text(9f, FontStyle.Bold);
		string text = webText + "  ↗";
		LinkLabel linkLabel = new LinkLabel
		{
			Text = text,
			Font = font2,
			LinkColor = Theme.Cyan,
			ActiveLinkColor = Theme.AccentHover,
			VisitedLinkColor = Theme.Cyan,
			LinkBehavior = LinkBehavior.HoverUnderline,
			BackColor = footer.BackColor,
			Location = new Point(13 + width + 4, 2),
			Size = new Size(TextRenderer.MeasureText(text, font2).Width + 4, footer.Height - 4),
			TextAlign = ContentAlignment.MiddleLeft,
			LinkArea = new LinkArea(0, text.Length),
			Cursor = Cursors.Hand
		};
		linkLabel.LinkClicked += (object? _, LinkLabelLinkClickedEventArgs _) =>
		{
			OpenUrl(webUrl);
		};
		footer.Controls.Add(linkLabel);
		ToolTip toolTip = new ToolTip();
		toolTip.SetToolTip(linkLabel, webUrl);
		int y = (footer.Height - 30) / 2;
		int num = footer.Width - 7 - (networks.Length * 30 + (networks.Length - 1) * 6);
		for (int num2 = 0; num2 < networks.Length; num2++)
		{
			(string, string, string) tuple = networks[num2];
			string url = tuple.Item2;
			LogoButton logoButton = new LogoButton
			{
				Logo = LoadLogo(tuple.Item3),
				Location = new Point(num, y),
				Size = new Size(30, 30),
				AccessibleName = tuple.Item1
			};
			logoButton.Click += (object? _, EventArgs _) =>
			{
				OpenUrl(url);
			};
			toolTip.SetToolTip(logoButton, tuple.Item1);
			footer.Controls.Add(logoButton);
			num += 36;
		}
	}

	private static Image? LoadLogo(string name)
	{
		using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ChestCounter.Assets." + name + ".png");
		if (stream == null)
		{
			return null;
		}
		MemoryStream memoryStream = new MemoryStream();
		stream.CopyTo(memoryStream);
		return new Bitmap(new MemoryStream(memoryStream.ToArray()));
	}

	private static void OpenUrl(string url)
	{
		try
		{
			Process.Start(new ProcessStartInfo(url)
			{
				UseShellExecute = true
			})?.Dispose();
		}
		catch
		{
		}
	}

	private static void CardTitle(Card card, string text)
	{
		Lbl(card, text, 8f, FontStyle.Regular, Theme.Muted, new Point(13, 7), new Size(card.Width - 100, 15));
	}

	private void ResetStats()
	{
		if (MessageBox.Show(this, L.T("Se borrarán todas tus partidas, cofres y tesoros guardados, y los cofres de antes del contador. El contador empezará de cero con tu próxima partida (los replays que ya tienes no se vuelven a contar).\n\nSe guarda una copia de seguridad en la carpeta de tus datos.\n\n¿Restablecer las estadísticas?", "All your saved matches, chests and treasures will be deleted, as well as the chests before the counter. The counter starts from zero with your next match (the replays you already have are not counted again).\n\nA backup copy is saved in your data folder.\n\nReset the statistics?"), L.T("Restablecer estadísticas", "Reset statistics"), MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
		{
			string path = _monitor.ResetStats();
			_loading = true;
			_initial.Text = "0";
			_loading = false;
			MessageBox.Show(this, L.F("Estadísticas restablecidas. Copia de seguridad: {0}", "Statistics reset. Backup copy: {0}", Path.GetFileName(path)), L.T("Restablecer estadísticas", "Reset statistics"), MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}
	}

	private static ToggleSwitch AddToggleRow(Card card, string text, string? hint, int top, bool value)
	{
		ToggleSwitch toggle = new ToggleSwitch
		{
			Location = new Point(card.Width - 13 - 40, (hint != null) ? (top + 5) : (top - 2)),
			Checked = value
		};
		Label label = Lbl(card, text, 9.5f, FontStyle.Bold, Theme.Ink, new Point(13, top), new Size(card.Width - 26 - 48, 18));
		label.Cursor = Cursors.Hand;
		label.Click += (object? _, EventArgs _) =>
		{
			toggle.Checked = !toggle.Checked;
		};
		if (hint != null)
		{
			Label label2 = Lbl(card, hint, 7.5f, FontStyle.Regular, Theme.Muted, new Point(13, top + 18), new Size(card.Width - 26 - 48, 14));
			label2.Cursor = Cursors.Hand;
			label2.Click += (object? _, EventArgs _) =>
			{
				toggle.Checked = !toggle.Checked;
			};
		}
		card.Controls.Add(toggle);
		return toggle;
	}

	private static TextBox AddField(Card card, Point at, int width, string text)
	{
		Card card2 = new Card
		{
			Location = at,
			Size = new Size(width, 30),
			Radius = 10,
			BackColor = Theme.Panel
		};
		TextBox textBox = new TextBox
		{
			BorderStyle = BorderStyle.None,
			BackColor = Theme.Panel,
			ForeColor = Theme.Ink,
			Font = Theme.Text(),
			ReadOnly = true,
			Text = text,
			Width = width - 20
		};
		textBox.Location = new Point(10, (card2.Height - textBox.Height) / 2);
		card2.Controls.Add(textBox);
		card.Controls.Add(card2);
		return textBox;
	}

	private static Label Lbl(Control parent, string text, float size, FontStyle style, Color color, Point at, Size sz, ContentAlignment align = ContentAlignment.TopLeft)
	{
		Label label = new Label
		{
			Text = text,
			Font = Theme.Text(size, style),
			ForeColor = color,
			BackColor = parent.BackColor,
			Location = at,
			Size = sz,
			TextAlign = align,
			AutoEllipsis = true
		};
		parent.Controls.Add(label);
		return label;
	}
}
