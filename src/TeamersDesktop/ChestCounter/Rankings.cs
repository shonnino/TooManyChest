using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ChestCounter;

internal static class Rankings
{
	private sealed class State
	{
		public string Id { get; set; } = "";

		public bool Enabled { get; set; } = true;

		public string? Sent { get; set; }

		public RankResult? Last { get; set; }
	}

	private static readonly HttpClient Http = new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(10L)
	};

	private static readonly SemaphoreSlim Busy = new SemaphoreSlim(1, 1);

	private static readonly object Gate = new object();

	private static readonly TimeSpan Fresh = TimeSpan.FromMinutes(30L);

	private static State? _state;

	private static string _path = "";

	private static State Load(string folder)
	{
		lock (Gate)
		{
			if (_state != null)
			{
				return _state;
			}
			_path = Path.Combine(folder, "2xflaw-rankings.json");
			try
			{
				_state = (File.Exists(_path) ? JsonSerializer.Deserialize<State>(File.ReadAllText(_path)) : null);
			}
			catch (Exception error)
			{
				ErrorLog.Write("Player rankings", error);
			}
			if (_state == null)
			{
				_state = new State();
			}
			if (_state.Id.Length != 32)
			{
				_state.Id = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
				Save();
			}
			return _state;
		}
	}

	private static void Save()
	{
		try
		{
			File.WriteAllText(_path + ".tmp", JsonSerializer.Serialize(_state));
			File.Move(_path + ".tmp", _path, overwrite: true);
		}
		catch (Exception error)
		{
			ErrorLog.Write("Player rankings", error);
		}
	}

	public static bool Enabled(string folder)
	{
		return Load(folder).Enabled;
	}

	public static void SetEnabled(string folder, bool on)
	{
		State state = Load(folder);
		lock (Gate)
		{
			state.Enabled = on;
			state.Sent = null;
			if (!on)
			{
				state.Last = null;
			}
			Save();
		}
		if (!on)
		{
			Forget(state.Id);
		}
	}

	private static async Task Forget(string id)
	{
		try
		{
			OnlineSettings onlineSettings = OnlineSync.Load();
			using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Delete, onlineSettings.ApiBase.TrimEnd('/') + "/api/benchmarks")
			{
				Content = JsonContent.Create(new { id })
			};
			if (!string.IsNullOrEmpty(onlineSettings.Token) && onlineSettings.Token.Contains('.'))
			{
				req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", onlineSettings.Token);
			}
			using (await Http.SendAsync(req))
			{
			}
		}
		catch (Exception ex) when ((ex is HttpRequestException || ex is TaskCanceledException || ex is NotSupportedException || ex is InvalidOperationException) ? true : false)
		{
		}
	}

	public static RankResult? Cached(string folder)
	{
		State state = Load(folder);
		if (state == null || !state.Enabled)
		{
			return null;
		}
		return state.Last;
	}

	public static bool Due(string folder, RankFigures f)
	{
		State state = Load(folder);
		if (state.Enabled)
		{
			if (!(state.Sent != Key(f)) && state.Last != null)
			{
				return DateTime.UtcNow - state.Last.AtUtc > Fresh;
			}
			return true;
		}
		return false;
	}

	private static string Key(RankFigures f)
	{
		return JsonSerializer.Serialize(f);
	}

	public static async Task<RankResult?> Fetch(string folder, RankFigures f)
	{
		State s = Load(folder);
		bool flag = !s.Enabled;
		if (!flag)
		{
			flag = !(await Busy.WaitAsync(0));
		}
		if (flag)
		{
			return null;
		}
		try
		{
			OnlineSettings onlineSettings = OnlineSync.Load();
			string id = s.Id;
			int matches = f.Matches;
			int total = f.Total;
			int mates = f.Mates;
			double avg = Math.Round(f.Avg, 3);
			double avgAll = Math.Round(f.AvgAll, 3);
			int best = f.Best;
			int bestAll = f.BestAll;
			double? pm = f.Pm;
			double? pm2;
			if (pm.HasValue)
			{
				double valueOrDefault = pm.GetValueOrDefault();
				pm2 = Math.Round(valueOrDefault, 3);
			}
			else
			{
				pm2 = null;
			}
			pm = f.PmAll;
			double? pmAll;
			if (pm.HasValue)
			{
				double valueOrDefault2 = pm.GetValueOrDefault();
				pmAll = Math.Round(valueOrDefault2, 3);
			}
			else
			{
				pmAll = null;
			}
			var inputValue = new
			{
				id = id,
				matches = matches,
				total = total,
				mates = mates,
				avg = avg,
				avgAll = avgAll,
				best = best,
				bestAll = bestAll,
				pm = pm2,
				pmAll = pmAll
			};
			using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, onlineSettings.ApiBase.TrimEnd('/') + "/api/benchmarks")
			{
				Content = JsonContent.Create(inputValue)
			};
			if (!string.IsNullOrEmpty(onlineSettings.Token) && onlineSettings.Token.Contains('.'))
			{
				req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", onlineSettings.Token);
			}
			using HttpResponseMessage resp = await Http.SendAsync(req);
			if (!resp.IsSuccessStatusCode)
			{
				return null;
			}
			RankResult rankResult = await resp.Content.ReadFromJsonAsync<RankResult>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
			if (rankResult == null)
			{
				return null;
			}
			rankResult.AtUtc = DateTime.UtcNow;
			lock (Gate)
			{
				if (!s.Enabled)
				{
					return null;
				}
				s.Last = rankResult;
				s.Sent = Key(f);
				Save();
			}
			return rankResult;
		}
		catch (Exception ex) when ((ex is HttpRequestException || ex is TaskCanceledException || ex is JsonException || ex is NotSupportedException) ? true : false)
		{
			return null;
		}
		finally
		{
			Busy.Release();
		}
	}
}
