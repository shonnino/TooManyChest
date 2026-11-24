using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;

namespace ChestCounter;

public sealed class Tracker
{
	private sealed record Outcome(MatchRecord? Match = null, bool Updated = false, string? Skipped = null, string? Error = null, List<string>? NewPrizeIds = null);

	private static readonly TimeSpan QuietPeriod = TimeSpan.FromSeconds(10L);

	private static readonly TimeSpan FinalizedQuietPeriod = TimeSpan.FromSeconds(4L);

	private static readonly TimeSpan OldReplayAge = TimeSpan.FromMinutes(1L);

	private static readonly TimeSpan ParseTimeout = TimeSpan.FromMinutes(3L);

	private readonly Store _store;

	private readonly Config _config;

	private readonly TrackerData _data;

	private readonly Dictionary<string, long> _lastSeenSize = new Dictionary<string, long>();

	private string? _liveFile;

	private long _liveSize = -1L;
	private DateTime _liveWriteUtc;
	private string? _observedReplay;
	private long _observedLength;
	private DateTime _observedActivityUtc;

	private DateTime _liveParsedUtc = DateTime.MinValue;

	public TrackerData Data => _data;

	public bool HasPending { get; private set; }

	public bool IsFinishing { get; private set; }

	public bool NewProfilesFound { get; set; }

	public MatchRecord? LiveMatch { get; private set; }

	public bool IsLive => _liveFile != null;
	public bool LiveWaitingForKey { get; private set; }

	public Tracker(Store store, Config config, TrackerData data)
	{
		_store = store;
		_config = config;
		_data = data;
		foreach (string item in (from kv in _data.ProcessedFiles
			where kv.Value.StartsWith("autre joueur", StringComparison.Ordinal)
			select kv.Key).ToList())
		{
			_data.ProcessedFiles.Remove(item);
		}
		if (_data.UpgradeNames())
		{
			try
			{
				_store.SaveData(_data);
			}
			catch
			{
			}
		}
	}

	private static void ReleaseMemory()
	{
		GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
		GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
		GC.WaitForPendingFinalizers();
	}

	public static string FileKey(FileInfo f)
	{
		return $"{f.Name}|{f.Length}|{f.LastWriteTimeUtc.Ticks}";
	}

	public IEnumerable<FileInfo> ReplayFiles()
	{
		DirectoryInfo directoryInfo = new DirectoryInfo(_config.DemosFolder);
		if (!directoryInfo.Exists)
		{
			return Enumerable.Empty<FileInfo>();
		}
		return from f in directoryInfo.EnumerateFiles("*.replay", SearchOption.TopDirectoryOnly)
			orderby f.LastWriteTimeUtc
			select f;
	}

	public IEnumerable<FileInfo> UnprocessedFiles()
	{
		return from f in ReplayFiles()
			where !_data.ProcessedFiles.ContainsKey(FileKey(f))
			select f;
	}

	public int BacklogCount()
	{
		return UnprocessedFiles().Count((FileInfo f) => ReplayHeaderProbe.IsLive(f.FullName) != true);
	}

	public void Reset(string backupPath)
	{
		if (File.Exists(_store.DataPath))
		{
			File.Copy(_store.DataPath, backupPath, overwrite: true);
		}
		_data.Matches.Clear();
		_data.ProcessedFiles.Clear();
		foreach (FileInfo item in from f in ReplayFiles()
			where ReplayHeaderProbe.IsLive(f.FullName) != true
			select f)
		{
			_data.ProcessedFiles[FileKey(item)] = "skipped";
		}
		_store.SaveData(_data);
	}

	public void MarkSkipped(IEnumerable<FileInfo> files)
	{
		foreach (FileInfo file in files)
		{
			_data.ProcessedFiles[FileKey(file)] = "skipped";
		}
		_store.SaveData(_data);
	}

	public int Scan(bool ignoreQuietPeriod = false)
	{
		HasPending = false;
		IsFinishing = false;
		List<FileInfo> list = new List<FileInfo>();
		foreach (FileInfo item in UnprocessedFiles().ToList())
		{
			item.Refresh();
			if (item.Exists)
			{
				if (!IsReady(item, ignoreQuietPeriod))
				{
					HasPending = true;
				}
				else
				{
					list.Add(item);
				}
			}
		}
		if (list.Count == 0)
		{
			return 0;
		}
		bool flag = list.Count >= 2;
		int count = Profiles.List(_data).Count;
		ProgressBar progressBar = (flag ? new ProgressBar(L.T("Analizando replays", "Analyzing replays"), list.Count) : null);
		List<Outcome> list2 = new List<Outcome>();
		for (int i = 0; i < list.Count; i++)
		{
			FileInfo fileInfo = list[i];
			if (flag)
			{
				progressBar.Update(i, fileInfo.Name);
				ReportWriter.Progress = new ScanProgress(i, list.Count);
				ReportWriter.WriteAll(_store, _config, _data);
			}
			else
			{
				Console.ForegroundColor = ConsoleColor.DarkGray;
				string text = ((double)(fileInfo.Length / 1024) / 1024.0).ToString("F1", L.Culture);
				Console.WriteLine((DateTime.UtcNow - fileInfo.LastWriteTimeUtc < TimeSpan.FromMinutes(2L)) ? L.F("[{0:HH:mm:ss}] Partida terminada: analizando el replay ({1} MB)…", "[{0:HH:mm:ss}] Match over: analyzing the replay ({1} MB)…", DateTime.Now, text) : L.F("[{0:HH:mm:ss}] Analizando {1} ({2} MB)…", "[{0:HH:mm:ss}] Analyzing {1} ({2} MB)…", DateTime.Now, fileInfo.Name, text));
				Console.ResetColor();
			}
			Outcome outcome = Process(fileInfo);
			list2.Add(outcome);
			if (!flag)
			{
				PrintOutcome(outcome);
			}
		}
		if (flag)
		{
			progressBar.Update(list.Count);
			progressBar.Clear();
			ReportWriter.Progress = null;
			PrintBatchSummary(list2);
			if (count < 2 && Profiles.List(_data).Count >= 2)
			{
				NewProfilesFound = true;
			}
		}
		int num = Enumerable.Count(list2, (Outcome o) => o.Match != null);
		if ((num > 0) | flag)
		{
			ReportWriter.WriteAll(_store, _config, _data);
		}
		return num;
	}

	private bool IsReady(FileInfo file, bool ignoreQuietPeriod)
	{
		bool? flag = ReplayHeaderProbe.IsLive(file.FullName);
		if (flag == true)
		{
			return false;
		}
		TimeSpan timeSpan = DateTime.UtcNow - file.LastWriteTimeUtc;
		if (flag == false && (ignoreQuietPeriod || timeSpan > OldReplayAge))
		{
			return true;
		}
		bool flag2 = _lastSeenSize.TryGetValue(file.FullName, out var value) && value == file.Length;
		_lastSeenSize[file.FullName] = file.Length;
		if (!flag.HasValue)
		{
			if (flag2)
			{
				return timeSpan > TimeSpan.FromMinutes(2L);
			}
			return false;
		}
		if (!ReplayHeaderProbe.KeyMissing(file.FullName))
		{
			IsFinishing = true;
			if (flag2)
			{
				return timeSpan >= FinalizedQuietPeriod;
			}
			return false;
		}
		if (flag2)
		{
			return timeSpan > QuietPeriod;
		}
		return false;
	}

	private Outcome Process(FileInfo file)
	{
		string key = FileKey(file);
		MatchRecord record;
		try
		{
			record = Analyze(file, ParseTimeout);
		}
		catch (Exception ex)
		{
			string text = (ex as AggregateException)?.InnerException?.Message ?? ex.Message;
			ErrorLog.Write($"Unreadable replay: {file.FullName} ({file.Length} bytes, {file.LastWriteTime:yyyy-MM-dd HH:mm:ss})", ex);
			_data.ProcessedFiles[key] = "error: " + text;
			_store.SaveData(_data);
			return new Outcome(null, Updated: false, null, file.Name + " : " + text);
		}
		record.ReplayFile = file.Name;
		if (!record.IsBattleRoyale)
		{
			_data.Matches.RemoveAll((MatchRecord m) => m.SessionId == record.SessionId);
			_data.ProcessedFiles[key] = "other mode";
			_store.SaveData(_data);
			return new Outcome(null, Updated: false, L.T("no es una partida Battle Royale (creativo / otro modo)", "not a Battle Royale match (Creative / other mode)"));
		}
		if (!Profiles.IsSupportedVersion(record))
		{
			_data.Matches.RemoveAll((MatchRecord m) => m.SessionId == record.SessionId);
			_data.ProcessedFiles[key] = "older version";
			_store.SaveData(_data);
			return new Outcome(null, Updated: false, L.T("versión anterior a v43 (Capítulo 7 Mini Temporada)", "version prior to v43 (Chapter 7 Mini Season)"));
		}
		MatchRecord matchRecord = _data.Matches.FirstOrDefault((MatchRecord m) => m.SessionId == record.SessionId);
		if (matchRecord != null && matchRecord.AnalyzerVersion >= record.AnalyzerVersion)
		{
			_data.ProcessedFiles[key] = record.SessionId;
			_store.SaveData(_data);
			return new Outcome(null, Updated: false, L.T("partida ya contada (misma partida, archivo renombrado)", "match already counted (same match, renamed file)"));
		}
		if (matchRecord != null)
		{
			record.AnalyzedAtUtc = matchRecord.AnalyzedAtUtc;
			_data.Matches.Remove(matchRecord);
		}
		_data.Matches.Add(record);
		_data.ProcessedFiles[key] = record.SessionId;
		_store.SaveData(_data);
		if (_liveFile == file.FullName)
		{
			ClearLive();
		}
		if (string.IsNullOrEmpty(_config.Profile))
		{
			_config.Profile = Profiles.Key(record);
			if (_config.InitialChests.Remove("", out var value) && !_config.InitialChests.ContainsKey(_config.Profile))
			{
				_config.InitialChests[_config.Profile] = value;
			}
			try
			{
				_store.SaveConfig(_config);
			}
			catch
			{
			}
		}
		List<string> newPrizeIds = _store.RegisterPrizeIds(from f in record.TreasureFinds
			where f.PrizeId != null
			select f.PrizeId);
		return new Outcome(record, matchRecord != null, null, null, newPrizeIds);
	}

	private void PrintOutcome(Outcome o)
	{
		if (o.Error != null)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine(L.F("   No se puede leer este replay: {0}", "   Cannot read this replay: {0}", o.Error));
			Console.ResetColor();
			PrintErrorLogHint();
			return;
		}
		if (o.Skipped != null)
		{
			Console.WriteLine($"   {L.T("Ignorado", "Skipped")}: {o.Skipped}.");
			return;
		}
		MatchRecord match = o.Match;
		PrintMatch(match, o.Updated);
		Profile profile = Profiles.Current(_data, _config);
		if (profile != null && Profiles.Key(match) != profile.Key)
		{
			Console.ForegroundColor = ConsoleColor.DarkYellow;
			string text = match.PlayerName ?? L.T("cuenta desconocida", "unknown account");
			Console.WriteLine(L.F("   Partida jugada en otra cuenta ({0}): cuenta para su propio perfil. Perfil mostrado: {1}.", "   Match played on another account ({0}): it counts for its own profile. Profile shown: {1}.", text, profile.Name));
			Console.ResetColor();
		}
		PrintNewPrizeIds(o.NewPrizeIds);
	}

	private void PrintNewPrizeIds(List<string>? ids)
	{
		ids = ids?.Where((string id) => !id.StartsWith("Unknown:", StringComparison.OrdinalIgnoreCase)).ToList();
		if (ids != null && ids.Count > 0)
		{
			Console.ForegroundColor = ConsoleColor.DarkYellow;
			Console.WriteLine(L.F("   Tesoro(s) por nombrar en {0}: {1}", "   Treasure(s) to name in {0}: {1}", _store.PrizeNamesPath, string.Join(", ", ids)));
			Console.ResetColor();
		}
	}

	private void PrintErrorLogHint()
	{
		Console.ForegroundColor = ConsoleColor.DarkGray;
		Console.WriteLine(L.F("   Detalles en {0}. Suele deberse a una actualización de Fortnite: notifícalo en {1}", "   Details in {0}. Usually caused by a Fortnite update: report it at {1}", _store.ErrorLogPath, AppInfo.IssuesUrl));
		Console.ResetColor();
	}

	private void PrintBatchSummary(List<Outcome> outcomes)
	{
		List<MatchRecord> list = (from o in outcomes
			where o.Match != null
			select o.Match).ToList();
		int num = Enumerable.Count(outcomes, (Outcome o) => o.Skipped != null);
		List<string> list2 = (from o in outcomes
			where o.Error != null
			select o.Error).ToList();
		Console.ForegroundColor = ConsoleColor.Green;
		Console.Write("  ✔ " + L.Count(outcomes.Count, "replay analizado", "replays analizados", "replay analyzed", "replays analyzed"));
		Console.ResetColor();
		Console.Write(": ");
		Console.ForegroundColor = ConsoleColor.Yellow;
		Console.Write(L.Count(list.Count, "partida", "partidas", "match", "matches") + ", " + L.Count(list.Sum((MatchRecord m) => m.ChestsOpened), "cofre", "cofres", "chest", "chests"));
		int num2 = list.Sum((MatchRecord m) => m.TreasureFinds.Count);
		if (num2 > 0)
		{
			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.Write(", ★ " + L.Count(num2, "tesoro", "tesoros", "treasure", "treasures"));
		}
		Console.ResetColor();
		Console.WriteLine();
		if (num > 0)
		{
			Console.ForegroundColor = ConsoleColor.DarkGray;
			Console.WriteLine(L.F("    {0} ignorado(s): versiones anteriores, modos creativos o partidas ya contadas.", "    {0} skipped: older versions, Creative modes or matches already counted.", num));
			Console.ResetColor();
		}
		if (list2.Count > 0)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine(L.F("    {0} replay(s) ilegible(s):", "    {0} unreadable replay(s):", list2.Count));
			foreach (string item in list2.Take(5))
			{
				Console.WriteLine("      " + item);
			}
			Console.ResetColor();
			PrintErrorLogHint();
		}
		PrintNewPrizeIds(outcomes.Where((Outcome o) => o.NewPrizeIds != null).SelectMany((Outcome o) => o.NewPrizeIds).ToList());
	}

	private MatchRecord Analyze(FileInfo file, TimeSpan timeout)
	{
		byte[] bytes;
		using (FileStream fileStream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
		{
			bytes = new byte[fileStream.Length];
			fileStream.ReadExactly(bytes);
		}
		MatchRecord result = null;
		Exception failure = null;
		Thread thread = new Thread(() =>
		{
			try
			{
				using MemoryStream stream = new MemoryStream(bytes, writable: false);
				result = new ReplayAnalyzer().Analyze(stream, file.Name, "");
			}
			catch (Exception ex)
			{
				failure = ex;
			}
		});
		thread.IsBackground = true;
		thread.Priority = ThreadPriority.Lowest;
		thread.Name = "ReplayAnalysis";
		thread.Start();
		if (!thread.Join(timeout))
		{
			throw new TimeoutException(L.T("análisis demasiado largo", "analysis took too long"));
		}
		bytes = Array.Empty<byte>();
		ReleaseMemory();
		if (failure != null)
		{
			ExceptionDispatchInfo.Capture(failure).Throw();
		}
		return result;
	}

	private void ClearLive()
	{
		_liveFile = null;
		_liveSize = -1L;
		_liveWriteUtc = default;
		_liveParsedUtc = DateTime.MinValue;
		LiveWaitingForKey = false;
		LiveMatch = null;
	}

	public bool UpdateLive()
	{
		var now = DateTime.UtcNow;
		FileInfo? fileInfo = UnprocessedFiles()
			.Where(f => f.FullName == _liveFile || now - f.LastWriteTimeUtc < TimeSpan.FromHours(1) || now - f.CreationTimeUtc < TimeSpan.FromHours(1))
			.OrderByDescending(f => f.LastWriteTimeUtc)
			.FirstOrDefault(f => ReplayHeaderProbe.IsLive(f.FullName) == true);
		if (fileInfo != null)
		{
			// While Fortnite holds the writer open, Windows can defer updating LastWriteTime.
			if (fileInfo.FullName != _observedReplay)
			{
				_observedReplay = fileInfo.FullName;
				_observedLength = fileInfo.Length;
				_observedActivityUtc = fileInfo.LastWriteTimeUtc;
			}
			else if (fileInfo.Length != _observedLength)
			{
				_observedLength = fileInfo.Length;
				_observedActivityUtc = now;
			}
			if (now - _observedActivityUtc > TimeSpan.FromMinutes(5) && now - fileInfo.LastWriteTimeUtc > TimeSpan.FromMinutes(5))
				fileInfo = null;
		}
		if (fileInfo == null)
		{
			if (_liveFile != null && (!File.Exists(_liveFile) || ReplayHeaderProbe.IsLive(_liveFile) == false || now - _observedActivityUtc > TimeSpan.FromMinutes(5)))
			{
				ClearLive();
				return true;
			}
			return false;
		}
		bool flag = false;
		if (fileInfo.FullName != _liveFile)
		{
			ClearLive();
			_liveFile = fileInfo.FullName;
			flag = true;
			Console.ForegroundColor = ConsoleColor.DarkGray;
			Console.WriteLine(L.F("[{0:HH:mm:ss}] Partida en curso… (sus cofres se contarán al final)", "[{0:HH:mm:ss}] Match in progress… (its chests will be counted at the end)", DateTime.Now));
			Console.ResetColor();
		}
		if (!_config.LiveUpdate)
		{
			bool wasReading = LiveMatch != null || LiveWaitingForKey;
			LiveMatch = null;
			LiveWaitingForKey = false;
			_liveSize = -1L;
			return flag || wasReading;
		}
		bool waiting = ReplayHeaderProbe.KeyMissing(fileInfo.FullName);
		flag |= waiting != LiveWaitingForKey;
		LiveWaitingForKey = waiting;
		if (waiting) return flag;
		TimeSpan timeSpan = TimeSpan.FromSeconds(Math.Max(10, _config.LiveIntervalSeconds));
		long length = fileInfo.Length;
		DateTime modified = fileInfo.LastWriteTimeUtc;
		if ((length == _liveSize && modified == _liveWriteUtc) || now - _liveParsedUtc < timeSpan)
		{
			return flag;
		}
		MatchRecord liveMatch;
		try
		{
			liveMatch = Analyze(fileInfo, TimeSpan.FromSeconds(60L));
		}
		catch (Exception value)
		{
			_liveParsedUtc = DateTime.UtcNow;
			string environmentVariable = Environment.GetEnvironmentVariable("CHESTCOUNTER_DEBUG");
			if (environmentVariable != null && environmentVariable.Length > 0)
			{
				Console.Error.WriteLine($"DBG live parse failed: {value}");
			}
			return flag;
		}
		_liveSize = length;
		_liveWriteUtc = modified;
		_liveParsedUtc = DateTime.UtcNow;
		MatchRecord liveMatch2 = LiveMatch;
		LiveMatch = liveMatch.IsBattleRoyale && Profiles.IsSupportedVersion(liveMatch) ? liveMatch : null;
		int num;
		if (liveMatch2?.ChestsOpened == LiveMatch?.ChestsOpened && liveMatch2?.TreasureFinds.Count == LiveMatch?.TreasureFinds.Count)
		{
			num = ((liveMatch2 == null != (LiveMatch == null)) ? 1 : 0);
			if (num == 0)
			{
				goto IL_03ed;
			}
		}
		else
		{
			num = 1;
		}
		if (LiveMatch != null)
		{
			Console.ForegroundColor = ConsoleColor.DarkGray;
			Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {L.T("Partida en curso", "Match in progress")}: {L.Count(LiveMatch.ChestsOpened, "cofre", "cofres", "chest", "chests")}" + ((LiveMatch.TreasureFinds.Count > 0) ? (", " + L.Count(LiveMatch.TreasureFinds.Count, "tesoro", "tesoros", "treasure", "treasures")) : ""));
			Console.ResetColor();
		}
		goto IL_03ed;
		IL_03ed:
		return (byte)((uint)num | (flag ? 1u : 0u)) != 0;
	}

	public bool UpgradeIfNeeded()
	{
		if (_data.AnalyzerVersion >= 13)
		{
			return false;
		}
		bool result = _data.Matches.Count > 0;
		foreach (string item in (from kv in _data.ProcessedFiles
			where kv.Value != "skipped" && kv.Value != "other mode" && kv.Value != "older version"
			select kv.Key).ToList())
		{
			_data.ProcessedFiles.Remove(item);
		}
		_data.AnalyzerVersion = 13;
		_store.SaveData(_data);
		return result;
	}

	private void PrintMatch(MatchRecord m, bool updated)
	{
		m = Profiles.Adjust(m, _config);
		int n = _data.Matches.Where((MatchRecord x) => Profiles.Key(x) == Profiles.Key(m) && Profiles.Counted(x, _config)).Sum((MatchRecord x) => Profiles.Adjust(x, _config).ChestsOpened) + Profiles.InitialChests(_config, Profiles.Key(m));
		DateTime d = (m.MatchStartUtc ?? m.AnalyzedAtUtc).ToLocalTime();
		Console.ForegroundColor = ConsoleColor.Green;
		Console.Write($"   {(updated ? "↻" : "✔")} {L.T("Partida del", "Match of")} {L.DateTimeShort(d)}");
		Console.ResetColor();
		Console.Write($" ({Pretty.Playlist(m.Playlist)}, {Pretty.Placement(m)}): ");
		Console.ForegroundColor = ConsoleColor.Yellow;
		int num = Enumerable.Count(m.Chests, (ChestRecord c) => c.Destroyed);
		Console.Write(L.Count(m.ChestsOpened, "cofre", "cofres", "chest", "chests") + ((num > 0) ? L.F(" ({0} destruido(s))", " ({0} destroyed)", num) : ""));
		Console.ResetColor();
		Console.WriteLine("  →  total: " + L.N(n));
		foreach (TreasureFind treasureFind in m.TreasureFinds)
		{
			Console.ForegroundColor = (treasureFind.FromGoldenChest ? ConsoleColor.Yellow : ConsoleColor.Cyan);
			Console.WriteLine($"   {(treasureFind.FromGoldenChest ? "★★" : "★")} {L.T("Tesoro obtenido: ", "Treasure obtained: ")}{treasureFind.DisplayName} ({Pretty.Obtained(treasureFind)})");
			if (!treasureFind.SourceIsChest)
			{
				Console.ForegroundColor = ConsoleColor.Cyan;
				Console.WriteLine(L.F("   ⚑ Origen inusual: {0}{1} [{2}] → anotado en el informe", "   ⚑ Unusual source: {0}{1} [{2}] → noted in the report", treasureFind.SourceLabel, treasureFind.SourceCertain ? "" : L.T(" (probable)", " (likely)"), treasureFind.SourceBlueprint));
			}
			Console.ResetColor();
		}
	}

	public void PrintSummary()
	{
		Profile profile = Profiles.Current(_data, _config);
		List<MatchRecord> matches = Profiles.View(_data, _config).Matches;
		int num = matches.Sum((MatchRecord m) => m.ChestsOpened);
		int n = num + Profiles.InitialChests(_data, _config);
		int n2 = matches.Sum((MatchRecord m) => m.TreasureFinds.Count);
		StringBuilder stringBuilder = new StringBuilder();
		if (profile != null)
		{
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(7, 1, stringBuilder2);
			handler.AppendFormatted(profile.Name);
			handler.AppendLiteral("   |   ");
			stringBuilder2.Append(ref handler);
		}
		stringBuilder.Append(L.F("Partidas: {0}   |   Cofres: {1}   |   Tesoros: {2}", "Matches: {0}   |   Chests: {1}   |   Treasures: {2}", L.N(matches.Count), L.N(n), L.N(n2)));
		if (matches.Count > 0)
		{
			string text = ((double)num / (double)matches.Count).ToString("F1", L.Culture);
			stringBuilder.Append(L.F("   |   Media: {0} / partida", "   |   Average: {0} / match", text));
		}
		Console.ForegroundColor = ConsoleColor.Cyan;
		Console.WriteLine("  " + stringBuilder);
		Console.ResetColor();
	}
}
