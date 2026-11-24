using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ChestCounter;

public sealed class PersonalTools : Form
{
	private sealed record ClaimItem(string Key)
	{
		public override string ToString()
		{
			return WeeklyChanceText.T(Key);
		}
	}

	private readonly Store store;

	private readonly Monitor monitor;

	private readonly PersonalLedger ledger;

	private readonly PersonalOdds odds;

	private readonly DataGridView grid = new DataGridView
	{
		Dock = DockStyle.Fill,
		AutoGenerateColumns = true,
		AllowUserToAddRows = false,
		ReadOnly = true,
		AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
	};

	private readonly Label status = new Label
	{
		AutoSize = true,
		ForeColor = Color.White,
		Padding = new Padding(8)
	};

	private readonly ComboBox profiles = new ComboBox
	{
		Width = 220,
		DropDownStyle = ComboBoxStyle.DropDownList
	};

	private readonly DateTimePicker when = new DateTimePicker
	{
		Width = 185,
		Format = DateTimePickerFormat.Custom,
		CustomFormat = "yyyy-MM-dd HH:mm"
	};

	private readonly NumericUpDown mine = new NumericUpDown
	{
		Maximum = 1000000m,
		Width = 85
	};

	private readonly NumericUpDown squad = new NumericUpDown
	{
		Maximum = 1000000m,
		Width = 85
	};

	private readonly TextBox note = new TextBox
	{
		Width = 200,
		PlaceholderText = ToolText.T("Note / source")
	};

	private readonly CheckedListBox claimed = new CheckedListBox
	{
		Width = 230,
		Height = 115,
		CheckOnClick = true
	};

	private static readonly string[] Names = new string[7] { "Weekly Gold Outfit", "Pirate Shop Access", "Weekly Vehicle & Decal", "Weekly Pickaxe or Back Bling", "Weekly Jam Track", "Weekly Loading Screen", "Any gizmo" };

	private static readonly double[] Den = new double[7] { 1562500.0, 137500.0, 7813.0, 568.0, 391.0, 272.0, 127.0 };

	private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	private string LedgerPath => Path.Combine(store.Folder, "2xflaw-ledger.json");

	private string OddsPath => Path.Combine(store.Folder, "2xflaw-odds.json");

	private string Key => (profiles.SelectedItem as Profile)?.Key ?? "";

	private string ClaimKey => Key + "|" + Week(DateTime.UtcNow).ToString("O");

	public PersonalTools(Store store, Monitor monitor)
	{
		PersonalTools personalTools = this;
		this.store = store;
		this.monitor = monitor;
		ledger = Read<PersonalLedger>(LedgerPath) ?? new PersonalLedger();
		odds = Read<PersonalOdds>(OddsPath) ?? PersonalOdds.Seed();
		if (!File.Exists(OddsPath))
		{
			Save(OddsPath, odds);
		}
		Text = ToolText.T("2xFlaw's Chest Counter - Weekly tools");
		Size = new Size(1120, 720);
		BackColor = Theme.Bg;
		ForeColor = Theme.Ink;
		Font = Theme.Text(10f);
		FlowLayoutPanel top = new FlowLayoutPanel
		{
			Dock = DockStyle.Top,
			Height = 150,
			AutoScroll = true,
			Padding = new Padding(8)
		};
		profiles.DisplayMember = "Name";
		profiles.DataSource = Profiles.List(PersonalReadCache.Load(store), monitor.Config);
		top.Controls.Add(profiles);
		Caption("Local date/time");
		top.Controls.Add(when);
		Caption("Your chests");
		top.Controls.Add(mine);
		Caption("Teammates only");
		top.Controls.Add(squad);
		top.Controls.Add(note);
		Button("Add dated entry", () =>
		{
			personalTools.ledger.Entries.Add(new PersonalEntry
			{
				Profile = personalTools.Key,
				AtUtc = personalTools.when.Value.ToUniversalTime(),
				Mine = (int)personalTools.mine.Value,
				Squad = (int)personalTools.squad.Value,
				Note = personalTools.note.Text
			});
			personalTools.Persist();
		});
		Button("Update selected entry", () =>
		{
			if (personalTools.grid.CurrentRow?.DataBoundItem is PersonalEntry personalEntry)
			{
				personalEntry.AtUtc = personalTools.when.Value.ToUniversalTime();
				personalEntry.Mine = (int)personalTools.mine.Value;
				personalEntry.Squad = (int)personalTools.squad.Value;
				personalEntry.Note = personalTools.note.Text;
				personalTools.Persist();
			}
		});
		Button("Use current week", () =>
		{
			personalTools.when.Value = Week(DateTime.UtcNow).AddMinutes(1.0).ToLocalTime();
		});
		Button("Delete selected entry", () =>
		{
			if (personalTools.grid.CurrentRow?.DataBoundItem is PersonalEntry item && MessageBox.Show(personalTools, ToolText.T("Delete selected manual entry?"), ToolText.T("Confirm"), MessageBoxButtons.YesNo) == DialogResult.Yes)
			{
				personalTools.ledger.Entries.Remove(item);
				personalTools.Persist();
			}
		});
		Button("Import replay(s)", () =>
		{
			using OpenFileDialog openFileDialog = new OpenFileDialog
			{
				Filter = "Fortnite replay|*.replay",
				Multiselect = true
			};
			if (openFileDialog.ShowDialog() == DialogResult.OK)
			{
				personalTools.Import(openFileDialog.FileNames);
			}
		});
		Button("Weekly report / chances", () =>
		{
			WeeklyReport.Open(store, monitor, personalTools);
		});
		Button("Transparent overlay", () =>
		{
			new PersonalOverlay(personalTools.Snapshot).Show(personalTools);
		});
		Button("Open odds JSON", () =>
		{
			Process.Start(new ProcessStartInfo(personalTools.OddsPath)
			{
				UseShellExecute = true
			});
		});
		Button("Save claimed prizes", () =>
		{
			personalTools.ledger.Claimed[personalTools.ClaimKey] = (from ClaimItem i in personalTools.claimed.CheckedItems
				select i.Key).ToList();
			personalTools.Persist();
		});
		foreach (string item2 in Names.Take(6))
		{
			claimed.Items.Add(new ClaimItem(item2));
		}
		top.Controls.Add(claimed);
		Controls.Add(grid);
		Controls.Add(top);
		Controls.Add(new Panel
		{
			Dock = DockStyle.Bottom,
			Height = 42,
			Controls = { (Control?)status }
		});
		profiles.SelectedIndexChanged += (object? _, EventArgs _) =>
		{
			personalTools.RefreshEntries();
		};
		AllowDrop = true;
		DragEnter += (object? _, DragEventArgs e) =>
		{
			IDataObject? data = e.Data;
			if (data != null && data.GetDataPresent(DataFormats.FileDrop))
			{
				e.Effect = DragDropEffects.Copy;
			}
		};
		DragDrop += (object? _, DragEventArgs e) =>
		{
			if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths)
			{
				_ = personalTools.Import(paths);
			}
		};
		grid.DataBindingComplete += (object? _, DataGridViewBindingCompleteEventArgs _) =>
		{
			foreach (DataGridViewColumn column in personalTools.grid.Columns)
			{
				switch (column.DataPropertyName)
				{
				case "Id":
					column.Visible = false;
					break;
				case "AtUtc":
					column.HeaderText = ToolText.T("Date (UTC)");
					break;
				case "Profile":
					column.HeaderText = ToolText.T("Profile");
					break;
				case "Mine":
					column.HeaderText = ToolText.T("Your chests");
					break;
				case "Squad":
					column.HeaderText = ToolText.T("Teammates only");
					break;
				case "Note":
					column.HeaderText = ToolText.T("Note");
					break;
				}
			}
		};
		grid.SelectionChanged += (object? _, EventArgs _) =>
		{
			if (personalTools.grid.CurrentRow?.DataBoundItem is PersonalEntry personalEntry)
			{
				personalTools.when.Value = personalEntry.AtUtc.ToLocalTime();
				personalTools.mine.Value = Math.Clamp(personalEntry.Mine, 0, 1000000);
				personalTools.squad.Value = Math.Clamp(personalEntry.Squad, 0, 1000000);
				personalTools.note.Text = personalEntry.Note;
			}
		};
		RefreshEntries();
		void Button(string text, Action action)
		{
			Button button = new Button
			{
				Text = ToolText.T(text),
				AutoSize = true
			};
			Theme.Style(button);
			button.Click += (object? _, EventArgs _) =>
			{
				action();
			};
			top.Controls.Add(button);
		}
		void Caption(string text)
		{
			top.Controls.Add(new Label
			{
				Text = ToolText.T(text),
				AutoSize = true,
				Padding = new Padding(4, 8, 0, 0)
			});
		}
	}

	private static T? Read<T>(string path)
	{
		if (!File.Exists(path))
		{
			return default;
		}
		return JsonSerializer.Deserialize<T>(File.ReadAllText(path));
	}

	private static void Save<T>(string path, T value)
	{
		if (File.Exists(path))
		{
			File.Copy(path, path + ".bak", overwrite: true);
		}
		File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(value, Json));
		File.Move(path + ".tmp", path, overwrite: true);
	}

	private void Persist()
	{
		Save(LedgerPath, ledger);
		monitor.RefreshPersonalStatistics();
		RefreshEntries();
	}

	private void RefreshEntries()
	{
		grid.DataSource = (from e in ledger.Entries
			where e.Profile == Key
			orderby e.AtUtc descending
			select e).ToList();
		List<string> list = ledger.Claimed.GetValueOrDefault(ClaimKey) ?? new List<string>();
		for (int num = 0; num < claimed.Items.Count; num++)
		{
			claimed.SetItemChecked(num, list.Contains(((ClaimItem)claimed.Items[num]).Key));
		}
		status.Text = ToolText.T("Manual additions feed dashboard totals and daily charts. Dates below are UTC. Drop .replay files here.");
	}

	public static DateTime Week(DateTime utc)
	{
		return WeeklyReset.Start(utc);
	}

	private List<MatchRecord> Matches()
	{
		return (from m in PersonalReadCache.Load(store).Matches.Concat(ledger.Imports.Select((PersonalImport x) => x.Match))
			where Profiles.Key(m) == Key && m.IsBattleRoyale
			group m by string.IsNullOrEmpty(m.SessionId) ? $"{m.ReplayFile}|{m.MatchStartUtc:O}" : m.SessionId into g
			select g.First()).ToList();
	}

	private async Task Import(string[] paths)
	{
		foreach (string path in paths.Where((string p) => Path.GetExtension(p).Equals(".replay", StringComparison.OrdinalIgnoreCase)))
		{
			status.Text = ToolText.F("Parsing {0} - please wait", Path.GetFileName(path));
			try
			{
				PersonalImport result = await Task.Run(() =>
				{
					using FileStream fileStream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
					string hash = Convert.ToHexString(SHA256.HashData(fileStream));
					fileStream.Position = 0L;
					MatchRecord match = new ReplayAnalyzer().Analyze(fileStream, path, null);
					return new PersonalImport
					{
						Hash = hash,
						Match = match
					};
				});
				if (ledger.Imports.Any((PersonalImport x) => x.Hash == result.Hash) || (!string.IsNullOrEmpty(result.Match.SessionId) && PersonalReadCache.Load(store).Matches.Any((MatchRecord m) => m.SessionId == result.Match.SessionId)))
				{
					status.Text = ToolText.F("Duplicate skipped: {0}", Path.GetFileName(path));
				}
				else
				{
					ledger.Imports.Add(result);
					Save(LedgerPath, ledger);
					monitor.RefreshPersonalStatistics();
					status.Text = ToolText.F("Imported {0}: {1} personal chests. Stored in ledger and projected into dashboard totals.", result.Match.PlayerName, result.Match.ChestsOpened);
				}
				string key = Key;
				profiles.DataSource = Profiles.List(new TrackerData
				{
					Matches = PersonalReadCache.Load(store).Matches.Concat(ledger.Imports.Select((PersonalImport x) => x.Match)).ToList()
				});
				foreach (Profile item in profiles.Items.Cast<Profile>())
				{
					if (item.Key == key)
					{
						profiles.SelectedItem = item;
						break;
					}
				}
			}
			catch (Exception ex)
			{
				status.Text = ToolText.F("Import failed: {0}", ex.Message);
				ErrorLog.Write("Personal replay import", ex);
			}
		}
	}

	private (int total, int last, int week, int? squad) Snapshot()
	{
		List<MatchRecord> source = Matches();
		DateTime start = Week(DateTime.UtcNow);
		List<PersonalEntry> source2 = ledger.Entries.Where((PersonalEntry e) => e.Profile == Key).ToList();
		var now = DateTime.UtcNow;
		var weeklyEntries = source2.Where(entry => entry.AtUtc >= start && entry.AtUtc < now).ToList();
		int week = source.Sum(match => PersonalWeek.PersonalCount(match, start, now)) + weeklyEntries.Sum(entry => entry.Mine);
		int? squad = monitor.Config.ShowTeammates
			? source.Where(match => match.MatchStartUtc >= start).Sum(match => match.Teammates?.Sum(teammate => teammate.ChestsSeen) ?? 0)
				+ weeklyEntries.Sum(entry => entry.Squad)
			: null;
		return (source.Sum(match => match.ChestsOpened) + source2.Sum(entry => entry.Mine),
			source.OrderByDescending(match => match.MatchStartUtc).FirstOrDefault()?.ChestsOpened ?? 0, week, squad);
	}

	private void Report()
	{
		try
		{
			PersonalOdds personalOdds = Read<PersonalOdds>(OddsPath) ?? odds;
			DateTime now = DateTime.UtcNow;
			DateTime start = Week(now);
			DateTime end = start.AddDays(7.0);
			List<MatchRecord> source = Matches();
			List<MatchRecord> list = source.Where((MatchRecord matchRecord) =>
			{
				DateTime? matchStartUtc = matchRecord.MatchStartUtc;
				if (matchStartUtc.HasValue)
				{
					DateTime valueOrDefault = matchStartUtc.GetValueOrDefault();
					if (valueOrDefault < end)
					{
						return valueOrDefault.AddSeconds(matchRecord.DurationSeconds.GetValueOrDefault()) >= start;
					}
				}
				return false;
			}).ToList();
			List<(DateTime?, int)> list2 = new List<(DateTime?, int)>();
			foreach (MatchRecord m in list)
			{
				if (m.Chests.Count > 0)
				{
					list2.AddRange(from c in m.Chests
						select ((DateTime?, int))(m.MatchStartUtc.Value.AddSeconds(c.TimeSeconds), 1) into e
						where e.Item1 >= start && e.Item1 < end
						select e);
				}
				else
				{
					list2.Add((null, PersonalWeek.PersonalCount(m, start, end)));
				}
			}
			List<PersonalEntry> source2 = ledger.Entries.Where((PersonalEntry e) => e.Profile == Key && e.AtUtc >= start && e.AtUtc <= now).ToList();
			list2.AddRange(source2.Select((PersonalEntry e) => ((DateTime?, int Mine))(e.AtUtc, Mine: e.Mine)));
			int num = list2.Sum(((DateTime? time, int count) x) => x.count);
			int num2 = list.Sum((MatchRecord matchRecord) => matchRecord.Teammates?.Sum((TeammateRecord teammateRecord) => teammateRecord.ChestsSeen) ?? 0) + source2.Sum((PersonalEntry e) => e.Squad);
			StringBuilder stringBuilder = new StringBuilder("<!doctype html><meta charset='utf-8'><meta name='viewport' content='width=device-width'><title>2xFlaw's Chest Counter</title><style>body{background:#0b0e14;color:#e6eaf2;font:16px Segoe UI,sans-serif;max-width:1150px;margin:30px auto;padding:15px}h1,h2,a{color:#ff66b2}table{width:100%;border-collapse:collapse;background:#151a24}td,th{padding:12px;text-align:left;border-bottom:1px solid #343d50}.card{background:#151a24;border-radius:14px;padding:20px;margin:16px 0}.muted{color:#b0bacb}.banner{width:100%;border-radius:16px}strong{color:#ff80b8}</style>");
			stringBuilder.Append("<img class='banner' src='2xflaw-banner.jpg' alt='2xFlaw'><h1>2xFlaw's Chest Counter</h1>");
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder3 = stringBuilder2;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(75, 2, stringBuilder2);
			handler.AppendLiteral("<p>Profile: ");
			handler.AppendFormatted(H((profiles.SelectedItem as Profile)?.Name));
			handler.AppendLiteral(" | Generated ");
			handler.AppendFormatted(H(now.ToLocalTime()));
			handler.AppendLiteral(". Reopen the report to refresh the statistics.</p>");
			stringBuilder3.Append(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder4 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(315, 6, stringBuilder2);
			handler.AppendLiteral("<div class='card'><strong>");
			handler.AppendFormatted(num, "N0");
			handler.AppendLiteral("</strong> of your chests this week | <strong>≥ ");
			handler.AppendFormatted(num2, "N0");
			handler.AppendLiteral("</strong> teammate chests (seen or added by hand) | Team ≥ ");
			handler.AppendFormatted(num + num2, "N0");
			handler.AppendLiteral("<p>Added by hand: ");
			handler.AppendFormatted(source2.Sum((PersonalEntry e) => e.Mine), "N0");
			handler.AppendLiteral(". Teammates are only partly visible in replays, so their counts are a minimum.</p><p>Weekly reset: <span data-date='");
			handler.AppendFormatted(end, "O");
			handler.AppendLiteral("'></span> — <span data-count='");
			handler.AppendFormatted(end, "O");
			handler.AppendLiteral("'></span></p></div>");
			stringBuilder4.Append(ref handler);
			stringBuilder.Append("<h2>Your chances</h2><p>The estimated chance of at least one drop, treating each chest as an independent try. It isn't a guarantee or the odds for your next chest. Claimed prizes are ticked by hand per profile and week, so check which rewards are on offer each week. Squad eligibility and exact teammate opening times are unknown.</p><table><tr><th>Reward</th><th>Next chest now</th><th>Personal this week</th><th>Squad illustration</th><th>Status</th></tr>");
			TreasureWindow w = personalOdds.Windows.FirstOrDefault((TreasureWindow treasureWindow) => now >= treasureWindow.StartUtc && now < treasureWindow.EndUtc);
			List<string> list3 = ledger.Claimed.GetValueOrDefault(ClaimKey) ?? new List<string>();
			int i;
			for (i = 0; i < Names.Length; i++)
			{
				double num3 = 1.0;
				foreach (var item in list2)
				{
					var (dateTime, _) = item;
					object obj;
					if (dateTime.HasValue)
					{
						DateTime t = dateTime.GetValueOrDefault();
						obj = personalOdds.Windows.FirstOrDefault((TreasureWindow treasureWindow) => t >= treasureWindow.StartUtc && t < treasureWindow.EndUtc);
					}
					else
					{
						obj = null;
					}
					TreasureWindow w2 = (TreasureWindow)obj;
					num3 *= Math.Pow(1.0 - Math.Clamp(Boost(w2) / Den[i], 0.0, 1.0), item.Item2);
				}
				double num4 = (1.0 - num3) * 100.0;
				double value = (1.0 - num3 * Math.Pow(1.0 - 1.0 / Den[i], num2)) * 100.0;
				bool flag = list3.Contains(Names[i]);
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder5 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(98, 8, stringBuilder2);
				handler.AppendLiteral("<tr><td>");
				handler.AppendFormatted(H(Names[i]));
				handler.AppendLiteral("</td><td data-den='");
				handler.AppendFormatted(Den[i]);
				handler.AppendLiteral("' data-index='");
				handler.AppendFormatted(i);
				handler.AppendLiteral("' data-claimed='");
				handler.AppendFormatted(flag.ToString().ToLowerInvariant());
				handler.AppendLiteral("'>");
				handler.AppendFormatted(flag ? "Claimed" : ("1 in " + (Den[i] / Boost(w)).ToString("N1")));
				handler.AppendLiteral("</td><td>");
				handler.AppendFormatted(flag ? "Claimed" : (num4.ToString("0.####") + "%"));
				handler.AppendLiteral("</td><td>");
				handler.AppendFormatted(value, "0.####");
				handler.AppendLiteral("%*</td><td>");
				handler.AppendFormatted(flag ? "User-confirmed claimed" : "Not confirmed claimed");
				handler.AppendLiteral("</td></tr>");
				stringBuilder5.Append(ref handler);
			}
			stringBuilder.Append("</table><p class='muted'>*Squad illustration combines your timestamp-adjusted probability with teammates at BASE rates, assuming all remain eligible. It is not a verified squad reward probability. Missing personal chest timestamps use base odds. Manual entries use their entered time. Only confirmed normal-match eligible chests should be included.</p>");
			stringBuilder.Append("<h2>Treasure Hours</h2><div id='event'></div><table><tr><th>Starts (your time)</th><th>Ends</th><th>Gold / Shop / Vehicle</th></tr>");
			foreach (TreasureWindow item2 in personalOdds.Windows.OrderBy((TreasureWindow treasureWindow) => treasureWindow.StartUtc))
			{
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder6 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(71, 5, stringBuilder2);
				handler.AppendLiteral("<tr><td data-date='");
				handler.AppendFormatted(item2.StartUtc, "O");
				handler.AppendLiteral("'></td><td data-date='");
				handler.AppendFormatted(item2.EndUtc, "O");
				handler.AppendLiteral("'></td><td>");
				handler.AppendFormatted(item2.Gold);
				handler.AppendLiteral("× / ");
				handler.AppendFormatted(item2.Shop);
				handler.AppendLiteral("× / ");
				handler.AppendFormatted(item2.Vehicle);
				handler.AppendLiteral("×</td></tr>");
				stringBuilder6.Append(ref handler);
			}
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder7 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(190, 4, stringBuilder2);
			handler.AppendLiteral("</table><p>Source: <a href='");
			handler.AppendFormatted(H(personalOdds.Source));
			handler.AppendLiteral("'>Fortnite.bot</a> | Captured: <span data-date='");
			handler.AppendFormatted(personalOdds.CapturedUtc, "O");
			handler.AppendLiteral("'></span><br>");
			handler.AppendFormatted(H(personalOdds.Status));
			handler.AppendLiteral("<br>Cache age: ");
			handler.AppendFormatted((now - personalOdds.CapturedUtc).TotalHours, "0.0");
			handler.AppendLiteral(" hours. Treat as stale after 24 hours; countdowns use cached confirmed times only.</p>");
			stringBuilder7.Append(ref handler);
			stringBuilder.Append("<h2>Personal treasures and team evidence</h2><p>The current replay model stores personal treasure finds and teammate chest counts, not reliably attributed teammate treasure finds. Team treasure ownership is unavailable, not zero.</p><table><tr><th>Match</th><th>Personal chests</th><th>Teammates observed</th><th>Personal treasures</th></tr>");
			foreach (MatchRecord item3 in source.OrderByDescending((MatchRecord matchRecord) => matchRecord.MatchStartUtc).Take(100))
			{
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder8 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(51, 5, stringBuilder2);
				handler.AppendLiteral("<tr><td>");
				handler.AppendFormatted(H(item3.MatchStartUtc?.ToLocalTime()));
				handler.AppendLiteral("</td><td>");
				handler.AppendFormatted(item3.ChestsOpened);
				handler.AppendLiteral("</td><td>≥ ");
				handler.AppendFormatted(item3.Teammates?.Sum((TeammateRecord teammateRecord) => teammateRecord.ChestsSeen) ?? 0);
				handler.AppendLiteral("<br>");
				handler.AppendFormatted(H(string.Join(", ", item3.Teammates?.Select((TeammateRecord teammateRecord) => $"{teammateRecord.Name}: {teammateRecord.ChestsSeen}") ?? Enumerable.Empty<string>())));
				handler.AppendLiteral("</td><td>");
				handler.AppendFormatted(H(string.Join(", ", item3.TreasureFinds.Select((TreasureFind treasureFind) => treasureFind.DisplayName))));
				handler.AppendLiteral("</td></tr>");
				stringBuilder8.Append(ref handler);
			}
			stringBuilder.Append("</table><h2>Manual history</h2><table><tr><th>Local time</th><th>Personal</th><th>Teammates only</th><th>Note</th></tr>");
			foreach (PersonalEntry item4 in from e in ledger.Entries
				where e.Profile == Key
				orderby e.AtUtc descending
				select e)
			{
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder9 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(45, 4, stringBuilder2);
				handler.AppendLiteral("<tr><td>");
				handler.AppendFormatted(H(item4.AtUtc.ToLocalTime()));
				handler.AppendLiteral("</td><td>");
				handler.AppendFormatted(item4.Mine);
				handler.AppendLiteral("</td><td>");
				handler.AppendFormatted(item4.Squad);
				handler.AppendLiteral("</td><td>");
				handler.AppendFormatted(H(item4.Note));
				handler.AppendLiteral("</td></tr>");
				stringBuilder9.Append(ref handler);
			}
			stringBuilder.Append("</table><script>const windows=" + JsonSerializer.Serialize(personalOdds.Windows) + ";function duration(ms){let s=Math.max(0,Math.floor(ms/1000));return Math.floor(s/86400)+'d '+Math.floor(s%86400/3600)+'h '+Math.floor(s%3600/60)+'m '+s%60+'s'}function tick(){const n=Date.now();document.querySelectorAll('[data-date]').forEach(e=>e.textContent=new Date(e.dataset.date).toLocaleString());document.querySelectorAll('[data-count]').forEach(e=>e.textContent=duration(new Date(e.dataset.count)-n));const a=windows.find(w=>n>=Date.parse(w.StartUtc)&&n<Date.parse(w.EndUtc)),next=windows.find(w=>Date.parse(w.StartUtc)>n);document.querySelectorAll('[data-den]').forEach(e=>{let i=+e.dataset.index,m=!a?1:i===0?a.Gold:i===1?a.Shop:i===2?a.Vehicle:1;e.textContent=e.dataset.claimed==='true'?'Claimed':'1 in '+(+e.dataset.den/m).toLocaleString(undefined,{maximumFractionDigits:1});});document.getElementById('event').textContent=a?'ACTIVE — ends in '+duration(Date.parse(a.EndUtc)-n):next?'Next session starts in '+duration(Date.parse(next.StartUtc)-n):'No confirmed upcoming event in cached schedule';}tick();setInterval(tick,1000);</script>");
			using (Stream stream = typeof(PersonalTools).Assembly.GetManifestResourceStream("ChestCounter.Assets.2xflaw-banner.jpg"))
			{
				if (stream != null)
				{
					using FileStream destination = File.Create(Path.Combine(store.Folder, "2xflaw-banner.jpg"));
					stream.CopyTo(destination);
				}
			}
			string text = Path.Combine(store.Folder, "2xflaw-report.html");
			File.WriteAllText(text, stringBuilder.ToString(), Encoding.UTF8);
			Process.Start(new ProcessStartInfo(text)
			{
				UseShellExecute = true
			});
			double Boost(TreasureWindow? treasureWindow)
			{
				if (treasureWindow != null)
				{
					if (i != 0)
					{
						if (i != 1)
						{
							if (i != 2)
							{
								return 1.0;
							}
							return treasureWindow.Vehicle;
						}
						return treasureWindow.Shop;
					}
					return treasureWindow.Gold;
				}
				return 1.0;
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, ex.Message, ToolText.T("Report error"));
		}
		static string H(object? x)
		{
			return WebUtility.HtmlEncode(x?.ToString() ?? "");
		}
	}
}
