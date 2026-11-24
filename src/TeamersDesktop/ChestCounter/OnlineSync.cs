using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ChestCounter;

public static class OnlineSync
{
	private static readonly object Gate = new object();

	private static readonly HttpClient Http = new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(10L)
	};

	private static readonly JsonSerializerOptions Camel = new JsonSerializerOptions
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	private static readonly JsonSerializerOptions Pretty = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	private static string folder = "";

	private static Thread? worker;

	private static volatile bool wake;

	private static string FilePath => Path.Combine(folder, "2xflaw-online.json");

	public static bool IsLinked => !string.IsNullOrEmpty(Load().Token);

	public static void Start(string path)
	{
		lock (Gate)
		{
			folder = path;
		}
		if (File.Exists(FilePath) && Load().Enabled)
		{
			EnsureThread();
		}
	}

	private static void EnsureThread()
	{
		lock (Gate)
		{
			if (worker == null)
			{
				worker = new Thread(Loop)
				{
					IsBackground = true,
					Name = "Online sync",
					Priority = ThreadPriority.BelowNormal
				};
				worker.Start();
			}
		}
	}

	public static OnlineSettings Load()
	{
		lock (Gate)
		{
			try
			{
				return File.Exists(FilePath) ? (JsonSerializer.Deserialize<OnlineSettings>(File.ReadAllText(FilePath)) ?? new OnlineSettings()) : new OnlineSettings();
			}
			catch
			{
				return new OnlineSettings();
			}
		}
	}

	private static void Save(OnlineSettings s)
	{
		lock (Gate)
		{
			Directory.CreateDirectory(folder);
			string text = FilePath + ".tmp";
			File.WriteAllText(text, JsonSerializer.Serialize(s, Pretty));
			File.Move(text, FilePath, overwrite: true);
		}
	}

	private static void Update(Action<OnlineSettings> change)
	{
		lock (Gate)
		{
			OnlineSettings onlineSettings = Load();
			change(onlineSettings);
			Save(onlineSettings);
		}
	}

	public static void SetEnabled(bool on)
	{
		Update((OnlineSettings s) =>
		{
			s.Enabled = on && !string.IsNullOrEmpty(s.Token);
		});
		if (on)
		{
			EnsureThread();
			wake = true;
		}
	}

	public static void Unlink()
	{
		OnlineSettings onlineSettings = Load();
		string token = onlineSettings.Token;
		string baseUrl = onlineSettings.ApiBase.TrimEnd('/');
		Update((OnlineSettings x) =>
		{
			x.Enabled = false;
			x.Token = null;
			x.Synced.Clear();
		});
		if (!string.IsNullOrEmpty(token))
		{
			RevokeAsync(baseUrl, token);
		}
	}

	private static async Task RevokeAsync(string baseUrl, string token)
	{
		try
		{
			using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, baseUrl + "/api/pc/unlink");
			req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
			using (await Http.SendAsync(req))
			{
			}
		}
		catch (Exception error)
		{
			ErrorLog.Write("Online unlink", error);
		}
	}

	private static string ErrorOf(JsonElement json, HttpResponseMessage resp)
	{
		if (json.ValueKind != JsonValueKind.Object || !json.TryGetProperty("error", out var value))
		{
			return $"HTTP {(int)resp.StatusCode}";
		}
		return value.GetString() ?? $"HTTP {(int)resp.StatusCode}";
	}

	private static void SaveToken(string? token, bool includeHistory)
	{
		Update((OnlineSettings x) =>
		{
			x.Token = token;
			x.Enabled = true;
			x.SinceUtc = (includeHistory ? ((DateTime?)null) : new DateTime?(DateTime.UtcNow));
			x.Synced.Clear();
			x.LastError = null;
		});
		EnsureThread();
		wake = true;
	}

	private static void OpenBrowser(string url, string apiBase)
	{
		if (!url.StartsWith(apiBase + "/", StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("Refusing to open an unexpected address.");
		}
		Process.Start(new ProcessStartInfo(url)
		{
			UseShellExecute = true
		});
	}

	public static async Task<string?> LinkAsync(string code, bool includeHistory)
	{
		_ = 1;
		try
		{
			OnlineSettings onlineSettings = Load();
			using HttpResponseMessage resp = await Http.PostAsJsonAsync(onlineSettings.ApiBase.TrimEnd('/') + "/api/link/claim", new
			{
				code = code.Trim(),
				optIn = true
			}, Camel);
			JsonElement json = await resp.Content.ReadFromJsonAsync<JsonElement>();
			if (!resp.IsSuccessStatusCode)
			{
				return ErrorOf(json, resp);
			}
			SaveToken(json.GetProperty("token").GetString(), includeHistory);
			return null;
		}
		catch (Exception ex)
		{
			ErrorLog.Write("Online link", ex);
			return ex.Message;
		}
	}

	public static async Task<string?> StartLinkAsync(bool includeHistory)
	{
		_ = 4;
		try
		{
			string baseUrl = Load().ApiBase.TrimEnd('/');
			using HttpResponseMessage startResp = await Http.PostAsync(baseUrl + "/api/pc/pair/start", null);
			JsonElement json = await startResp.Content.ReadFromJsonAsync<JsonElement>();
			if (!startResp.IsSuccessStatusCode)
			{
				return ErrorOf(json, startResp);
			}
			string code = json.GetProperty("code").GetString();
			string secret = json.GetProperty("secret").GetString();
			OpenBrowser(baseUrl + "/link.html?code=" + Uri.EscapeDataString(code), baseUrl);
			DateTime deadline = DateTime.UtcNow.AddMinutes(10.0);
			while (DateTime.UtcNow < deadline)
			{
				await Task.Delay(2000);
				using HttpResponseMessage pollResp = await Http.PostAsJsonAsync(baseUrl + "/api/pc/pair/poll", new { code, secret }, Camel);
				JsonElement json2 = await pollResp.Content.ReadFromJsonAsync<JsonElement>();
				if (!pollResp.IsSuccessStatusCode)
				{
					return ErrorOf(json2, pollResp);
				}
				string text = (json2.TryGetProperty("status", out var value) ? value.GetString() : null);
				if (text == "linked")
				{
					SaveToken(json2.GetProperty("token").GetString(), includeHistory);
					return null;
				}
				if (text == "expired")
				{
					return L.T("La solicitud caducó. Inténtalo de nuevo.", "The link request expired. Try again.");
				}
			}
			return L.T("Tiempo agotado. Inténtalo de nuevo.", "Linking timed out. Try again.");
		}
		catch (Exception ex)
		{
			ErrorLog.Write("Online auto link", ex);
			return ex.Message;
		}
	}

	public static async Task<string?> OpenWebAsync(string path)
	{
		_ = 1;
		try
		{
			OnlineSettings onlineSettings = Load();
			if (string.IsNullOrEmpty(onlineSettings.Token))
			{
				return L.T("Este PC no está vinculado todavía.", "This PC is not linked yet.");
			}
			string baseUrl = onlineSettings.ApiBase.TrimEnd('/');
			using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, baseUrl + "/api/pc/web-session")
			{
				Content = JsonContent.Create(new
				{
					next = path
				}, null, Camel)
			};
			req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", onlineSettings.Token);
			using HttpResponseMessage resp = await Http.SendAsync(req);
			JsonElement json = await resp.Content.ReadFromJsonAsync<JsonElement>();
			if (!resp.IsSuccessStatusCode)
			{
				return (resp.StatusCode == HttpStatusCode.Unauthorized) ? L.T("Este PC fue desvinculado en la web. Vincúlalo de nuevo.", "This PC was unlinked on the website. Link it again.") : ErrorOf(json, resp);
			}
			OpenBrowser(json.GetProperty("url").GetString(), baseUrl);
			return null;
		}
		catch (Exception ex)
		{
			ErrorLog.Write("Online open web", ex);
			return ex.Message;
		}
	}

	private static void Loop()
	{
		DateTime dateTime = DateTime.UtcNow.AddSeconds(20.0);
		while (true)
		{
			Thread.Sleep(2000);
			if (!(DateTime.UtcNow < dateTime) || wake)
			{
				wake = false;
				try
				{
					OnlineSettings onlineSettings = Load();
					dateTime = DateTime.UtcNow.Add((onlineSettings.Enabled && !string.IsNullOrEmpty(onlineSettings.Token)) ? SyncOnce(onlineSettings) : TimeSpan.FromSeconds(30L));
				}
				catch (Exception error)
				{
					ErrorLog.Write("Online sync", error);
					dateTime = DateTime.UtcNow.AddMinutes(5.0);
				}
			}
		}
	}

	private static TimeSpan SyncOnce(OnlineSettings s)
	{
		TimeSpan timeSpan = TimeSpan.FromSeconds(60L);
		string path = Path.Combine(folder, "data.json");
		string path2 = Path.Combine(folder, "config.json");
		if (!File.Exists(path))
		{
			return timeSpan;
		}
		TrackerData trackerData = JsonSerializer.Deserialize<TrackerData>(File.ReadAllText(path));
		if (trackerData == null)
		{
			return timeSpan;
		}
		Config c = (File.Exists(path2) ? JsonSerializer.Deserialize<Config>(File.ReadAllText(path2)) : null) ?? new Config();
		string key = Profiles.Current(trackerData, c)?.Key;
		HashSet<string> done = new HashSet<string>(s.Synced);
		DateTime since = s.SinceUtc ?? DateTime.MinValue;
		List<MatchRecord> list = (from m in trackerData.Matches.Where((MatchRecord m) =>
			{
				if (m.IsBattleRoyale)
				{
					DateTime? matchStartUtc = m.MatchStartUtc;
					if (matchStartUtc.HasValue && matchStartUtc.GetValueOrDefault().ToUniversalTime() >= since && !string.IsNullOrEmpty(m.SessionId) && !done.Contains(m.SessionId))
					{
						if (key != null)
						{
							return Profiles.Key(m) == key;
						}
						return true;
					}
				}
				return false;
			})
			orderby m.MatchStartUtc
			select m).Take(50).ToList();
		bool flag = false;
		if (list.Count == 0 && s.ExtrasVersion < 1)
		{
			DateTime cursor = s.ExtrasCursorUtc ?? DateTime.MinValue;
			list = (from m in trackerData.Matches.Where((MatchRecord m) =>
				{
					if (m.IsBattleRoyale)
					{
						DateTime? matchStartUtc = m.MatchStartUtc;
						if (matchStartUtc.HasValue)
						{
							DateTime valueOrDefault = matchStartUtc.GetValueOrDefault();
							if (valueOrDefault.ToUniversalTime() >= since && valueOrDefault.ToUniversalTime() > cursor && !string.IsNullOrEmpty(m.SessionId) && done.Contains(m.SessionId))
							{
								if (key != null)
								{
									return Profiles.Key(m) == key;
								}
								return true;
							}
						}
					}
					return false;
				})
				orderby m.MatchStartUtc
				select m).Take(50).ToList();
			if (list.Count == 0)
			{
				Update((OnlineSettings x) =>
				{
					x.ExtrasVersion = 1;
					x.ExtrasCursorUtc = null;
				});
				return timeSpan;
			}
			flag = true;
		}
		if (list.Count == 0)
		{
			return timeSpan;
		}
		var matches = list.Select((MatchRecord m) =>
		{
			int num = Enumerable.Count(m.Chests, (ChestRecord chestRecord) => chestRecord.Destroyed);
			string sessionId = m.SessionId;
			string startedAt = m.MatchStartUtc.Value.ToUniversalTime().ToString("O");
			string? mode = m.Playlist ?? "";
			int opened = Math.Max(0, m.ChestsOpened - num);
			Dictionary<string, int> chestTypes = (from chestRecord in m.Chests
				group chestRecord by chestRecord.Kind).ToDictionary((IGrouping<string, ChestRecord> g) => g.Key, (IGrouping<string, ChestRecord> g) => g.Count());
			int[] chestTimes = m.Chests.Select((ChestRecord chestRecord) => (int)chestRecord.TimeSeconds).ToArray();
			int? mates = m.Teammates?.Sum((TeammateRecord t) => Math.Max(0, t.ChestsSeen));
			int? durationSeconds = m.DurationSeconds;
			int? durationS;
			if (durationSeconds.HasValue)
			{
				int valueOrDefault = durationSeconds.GetValueOrDefault();
				if (valueOrDefault > 0 && valueOrDefault < 7200)
				{
					durationS = m.DurationSeconds;
					goto IL_0184;
				}
			}
			durationS = null;
			goto IL_0184;
			IL_0184:
			return new
			{
				sessionId = sessionId,
				startedAt = startedAt,
				mode = mode,
				opened = opened,
				destroyed = num,
				chestTypes = chestTypes,
				chestTimes = chestTimes,
				mates = mates,
				durationS = durationS
			};
		}).ToList();
		using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, s.ApiBase.TrimEnd('/') + "/api/matches")
		{
			Content = JsonContent.Create(new { matches }, null, Camel)
		};
		httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.Token);
		HttpResponseMessage resp = Http.Send(httpRequestMessage);
		try
		{
			if (resp.StatusCode == HttpStatusCode.Unauthorized)
			{
				Update((OnlineSettings x) =>
				{
					x.Enabled = false;
					x.Token = null;
					x.LastError = "This PC was unlinked on the website.";
				});
				return TimeSpan.FromMinutes(5L);
			}
			if (!resp.IsSuccessStatusCode)
			{
				Update((OnlineSettings x) =>
				{
					x.LastError = $"Server answered {(int)resp.StatusCode}";
				});
				return TimeSpan.FromMinutes(5L);
			}
			if (flag)
			{
				List<MatchRecord> list2 = list;
				DateTime last = list2[list2.Count - 1].MatchStartUtc.Value.ToUniversalTime();
				Update((OnlineSettings x) =>
				{
					x.ExtrasCursorUtc = last;
					x.LastSyncUtc = DateTime.UtcNow;
					x.LastError = null;
				});
				return (list.Count == 50) ? TimeSpan.FromSeconds(5L) : timeSpan;
			}
			List<string> ids = list.Select((MatchRecord m) => m.SessionId).ToList();
			Update((OnlineSettings x) =>
			{
				x.Synced.AddRange(ids);
				if (x.Synced.Count > 5000)
				{
					x.Synced.RemoveRange(0, x.Synced.Count - 5000);
				}
				x.LastSyncUtc = DateTime.UtcNow;
				x.LastError = null;
			});
			return (list.Count == 50) ? TimeSpan.FromSeconds(5L) : timeSpan;
		}
		finally
		{
			if (resp != null)
			{
				((IDisposable)resp).Dispose();
			}
		}
	}
}
