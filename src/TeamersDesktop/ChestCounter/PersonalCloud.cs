using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ChestCounter;

public static class PersonalCloud
{
	public sealed class Options
	{
		public string Source { get; set; } = Guid.NewGuid().ToString("N");

		public string? Consent { get; set; }
	}

	private static string folder = "";

	private static Options options = new Options();

	private static readonly SemaphoreSlim Busy = new SemaphoreSlim(1, 1);

	private static readonly HttpClient Http = new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(30L)
	};

	private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	private static System.Threading.Timer? timer;

	private static bool installed;

	private static string Hash(string value)
	{
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
	}

	public static void Start(string path)
	{
		if (timer != null)
		{
			return;
		}
		folder = path;
		try
		{
			options = JsonSerializer.Deserialize<Options>(File.ReadAllText(Path.Combine(folder, "2xflaw-personal-cloud.json"))) ?? new Options();
		}
		catch
		{
			options = new Options();
		}
		timer = new System.Threading.Timer(async (object? _) =>
		{
			await Sync(history: false);
		}, null, TimeSpan.FromSeconds(15L), TimeSpan.FromMinutes(1L));
		if (installed || !OperatingSystem.IsWindows())
		{
			return;
		}
		installed = true;
		Application.Idle += (object? _, EventArgs _) =>
		{
			OnlineForm[] array = ((IEnumerable)Application.OpenForms).OfType<OnlineForm>().ToArray();
			foreach (OnlineForm form in array)
			{
				FlowLayoutPanel flowLayoutPanel = form.Controls.OfType<FlowLayoutPanel>().FirstOrDefault();
				if (flowLayoutPanel != null && !flowLayoutPanel.Controls.OfType<Button>().Any((Button b) => object.Equals(b.Tag, "personal-cloud")))
				{
					flowLayoutPanel.AutoScroll = true;
					Button button = new Button
					{
						AutoSize = true,
						Tag = "personal-cloud",
						Text = "Sync my full personal history (including manual additions)"
					};
					Button button2 = new Button
					{
						AutoSize = true,
						Text = "Stop syncing personal history"
					};
					Label status = new Label
					{
						AutoSize = false,
						Width = 440,
						Height = 60,
						Text = "Optional. Manual entries and imported replays are kept out of leaderboard rankings. Syncing makes this PC the source of your personal history."
					};
					flowLayoutPanel.Controls.Add(button);
					flowLayoutPanel.Controls.Add(button2);
					flowLayoutPanel.Controls.Add(status);
					flowLayoutPanel.Controls.SetChildIndex(button, 0);
					flowLayoutPanel.Controls.SetChildIndex(button2, 1);
					flowLayoutPanel.Controls.SetChildIndex(status, 2);
					button.Click += async (object? obj2, EventArgs e) =>
					{
						OnlineSettings onlineSettings = OnlineSync.Load();
						if (string.IsNullOrEmpty(onlineSettings.Token) || !onlineSettings.Enabled)
						{
							status.Text = "Link this PC and turn on sync first.";
						}
						else if (MessageBox.Show(form, "Upload all your eligible replay history, imported replay statistics and manual chest amounts? Notes, names, Epic IDs and replay files are not uploaded. This replaces your starting total on the website rather than adding to it. Future edits and deletions sync while this is on.", "Sync personal history", MessageBoxButtons.YesNo) == DialogResult.Yes)
						{
							options.Consent = Hash(onlineSettings.Token);
							Save();
							button.Enabled = false;
							status.Text = "Syncing history…";
							string text = await Sync(history: true);
							if (!form.IsDisposed)
							{
								button.Enabled = true;
								status.Text = text ?? "History synced. Refresh My stats on the website. Changes now sync every minute while sync is on.";
							}
						}
					};
					button2.Click += (object? obj2, EventArgs e) =>
					{
						options.Consent = null;
						Save();
						status.Text = "Personal-history uploads have stopped. The last snapshot stays on the website until you clear it there.";
					};
				}
			}
		};
	}

	private static void Save()
	{
		Directory.CreateDirectory(folder);
		string text = Path.Combine(folder, "2xflaw-personal-cloud.json");
		File.WriteAllText(text + ".tmp", JsonSerializer.Serialize(options));
		File.Move(text + ".tmp", text, overwrite: true);
	}

	private static T? Read<T>(string name)
	{
		return JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(folder, name)));
	}

	private static async Task Send(string route, object payload, string token, string api)
	{
		OnlineSettings onlineSettings = OnlineSync.Load();
		if (!onlineSettings.Enabled || onlineSettings.Token != token || options.Consent != Hash(token))
		{
			throw new InvalidOperationException("Personal sync was stopped or the linked profile changed.");
		}
		using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, api.TrimEnd('/') + route)
		{
			Content = JsonContent.Create(payload, null, Json)
		};
		req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		using HttpResponseMessage resp = await Http.SendAsync(req);
		if (!resp.IsSuccessStatusCode)
		{
			throw new InvalidOperationException("Personal sync: " + await resp.Content.ReadAsStringAsync());
		}
	}

	private static async Task<string?> Sync(bool history)
	{
		if (!(await Busy.WaitAsync(0)))
		{
			return history ? "A sync is already running. Please try again shortly." : null;
		}
		try
		{
			OnlineSettings s = OnlineSync.Load();
			if (!s.Enabled || string.IsNullOrEmpty(s.Token) || options.Consent != Hash(s.Token))
			{
				return history ? "Turn on sync and approve the personal-history upload first." : null;
			}
			TrackerData trackerData = Read<TrackerData>("data.json") ?? throw new InvalidOperationException("No replay history available.");
			Config config = (File.Exists(Path.Combine(folder, "config.json")) ? (Read<Config>("config.json") ?? new Config()) : new Config());
			PersonalLedger personalLedger = (File.Exists(Path.Combine(folder, "2xflaw-ledger.json")) ? (Read<PersonalLedger>("2xflaw-ledger.json") ?? throw new InvalidOperationException("Cannot read manual ledger.")) : new PersonalLedger());
			string key = Profiles.Current(trackerData, config)?.Key ?? throw new InvalidOperationException("Select a profile in the app first.");
			DateTime now = DateTime.UtcNow;
			List<(MatchRecord, string)> source = (from x in (from m in trackerData.Matches.Where(Eligible)
					select (match: m, origin: "replay")).Concat(from m in personalLedger.Imports.Select((PersonalImport i) => i.Match).Where(Eligible)
					select (match: m, origin: "imported"))
				group x by Id(x.match) into g
				select g.First()).ToList();
			var matches = (from x in source.Select(((MatchRecord match, string origin) x) => new
				{
					id = Hash(Id(x.match)),
					startedAt = x.match.MatchStartUtc?.ToUniversalTime().ToString("O"),
					mode = (x.match.Playlist ?? ""),
					origin = x.origin,
					opened = Math.Max(0, x.match.ChestsOpened - Enumerable.Count(x.match.Chests, (ChestRecord c) => c.Destroyed)),
					destroyed = Enumerable.Count(x.match.Chests, (ChestRecord c) => c.Destroyed),
					team = (config.CountTeammateChests ? Math.Max(0, x.match.Teammates?.Sum((TeammateRecord t) => t.ChestsSeen) ?? 0) : 0)
				})
				orderby x.id
				select x).ToArray();
			var entries = (from e in personalLedger.Entries.Where((PersonalEntry e) =>
				{
					if (e.Profile == key && e.AtUtc.ToUniversalTime() <= now)
					{
						DateTime? countFrom = config.CountFrom;
						if (countFrom.HasValue)
						{
							DateTime valueOrDefault = countFrom.GetValueOrDefault();
							return e.AtUtc.ToLocalTime().Date >= valueOrDefault.Date;
						}
						return true;
					}
					return false;
				})
				select new
				{
					id = e.Id.ToString(),
					at = e.AtUtc.ToUniversalTime().ToString("O"),
					amount = Math.Max(0, e.Mine) + (config.CountTeammateChests ? Math.Max(0, e.Squad) : 0)
				} into e
				orderby e.id
				select e).ToArray();
			if (matches.Length > 10000 || entries.Length > 10000)
			{
				throw new InvalidOperationException("Personal history exceeds the 10,000-record safety limit; nothing was truncated or replaced.");
			}
			if (history)
			{
				List<MatchRecord> source2 = (from m in trackerData.Matches.Where(Eligible)
					where m.MatchStartUtc.HasValue && !string.IsNullOrEmpty(m.SessionId) && m.SessionId.Length <= 80
					group m by m.SessionId into g
					select g.First()).ToList();
				foreach (MatchRecord[] item in source2.Chunk(50))
				{
					var matches2 = item.Select((MatchRecord m) => new
					{
						sessionId = m.SessionId,
						startedAt = m.MatchStartUtc.Value.ToUniversalTime().ToString("O"),
						mode = (m.Playlist ?? ""),
						opened = Math.Max(0, m.ChestsOpened - Enumerable.Count(m.Chests, (ChestRecord c) => c.Destroyed)),
						destroyed = Enumerable.Count(m.Chests, (ChestRecord c) => c.Destroyed),
						chestTypes = (from c in m.Chests
							group c by c.Kind).ToDictionary((IGrouping<string, ChestRecord> g) => g.Key, (IGrouping<string, ChestRecord> g) => g.Count()),
						chestTimes = m.Chests.Select((ChestRecord c) => (int)c.TimeSeconds).ToArray()
					}).ToArray();
					await Send("/api/matches", new
					{
						matches = matches2
					}, s.Token, s.ApiBase);
					await Task.Delay(250);
				}
			}
			await Send("/api/personal/snapshot", new
			{
				source = options.Source,
				scope = Hash(key),
				matches = matches,
				entries = entries
			}, s.Token, s.ApiBase);
			return null;
			bool Eligible(MatchRecord m)
			{
				if (m.IsBattleRoyale && Profiles.Key(m) == key && (!m.MatchStartUtc.HasValue || m.MatchStartUtc.Value.ToUniversalTime() <= now))
				{
					DateTime? countFrom = config.CountFrom;
					if (countFrom.HasValue)
					{
						DateTime valueOrDefault = countFrom.GetValueOrDefault();
						countFrom = m.MatchStartUtc;
						if (countFrom.HasValue)
						{
							return countFrom.GetValueOrDefault().ToLocalTime().Date >= valueOrDefault.Date;
						}
						return false;
					}
					return true;
				}
				return false;
			}
			string Id(MatchRecord m)
			{
				if (string.IsNullOrWhiteSpace(m.SessionId))
				{
					return $"fallback:{key}|{m.MatchStartUtc:O}|{Path.GetFileName(m.ReplayFile)}";
				}
				return "session:" + m.SessionId;
			}
		}
		catch (Exception ex)
		{
			ErrorLog.Write("Personal cloud sync", ex);
			return ex.Message;
		}
		finally
		{
			Busy.Release();
		}
	}
}
