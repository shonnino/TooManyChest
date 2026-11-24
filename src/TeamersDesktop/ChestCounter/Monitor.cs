using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace ChestCounter;

[SupportedOSPlatform("windows")]
public sealed class Monitor
{
	private readonly object _cfgGate = new object();

	private readonly Store _store;

	private readonly Config _config;

	private readonly TrackerData _data;

	private readonly Tracker _tracker;

	private readonly string _dataDir;

	private readonly AutoResetEvent _wake = new AutoResetEvent(initialState: false);

	private FileSystemWatcher? _watcher;

	private volatile bool _stop;

	private volatile bool _publishRequested;

	private volatile bool _reportRequested;

	private volatile string? _shownKey;

	private volatile string? _shownName;

	private DateTime _nextScan = DateTime.UtcNow;

	private volatile bool _fortniteRunning;

	private DateTime _nextFortniteCheck;

	private volatile UpdateInfo? _update;

	private bool _updateAnnounced;

	private volatile bool _installing;

	private volatile bool _updateOffered;

	private volatile string? _notice;

	private volatile bool _keepOpenRequested;

	private DateTime? _fortniteClosedSince;

	private DateTime? _autoCloseAt;

	private bool _closeRequested;

	private volatile string? _resetBackup;

	public Config Config => _config;

	public bool AutoMode { get; set; }

	public bool RestartAfterUpdate { get; private set; }

	public bool RestartRequested { get; private set; }

	public string? ShownProfileName => _shownName;

	public int InitialChests
	{
		get
		{
			lock (_cfgGate)
			{
				return Profiles.InitialChests(_config, _shownKey);
			}
		}
	}

	public bool OverlayEnabled => OperatingSystem.IsWindows();

	public bool OverlayVisible
	{
		get
		{
			if (OperatingSystem.IsWindows())
			{
				return NativeOverlay.IsVisible;
			}
			return false;
		}
	}

	public event Action<MonitorState>? Changed;

	public event Action? CloseRequested;

	public event Action<UpdateInfo>? UpdateOffered;

	public Monitor(Store store, Config config, TrackerData data, Tracker tracker, string dataDir)
	{
		_store = store;
		_config = config;
		_data = data;
		_tracker = tracker;
		_dataDir = dataDir;
	}

	public void Restart()
	{
		RestartRequested = true;
		CloseRequested?.Invoke();
	}

	public void Start()
	{
		if (OperatingSystem.IsWindows())
		{
			StartOverlay();
		}
		RestartWatcher();
		ObsServer.Start((_config.ObsPort > 0) ? _config.ObsPort : 7373);
		Thread thread = new Thread(Loop);
		thread.IsBackground = true;
		thread.Name = "Vigilancia";
		thread.Start();
		Task.Run(async () =>
		{
			while (true)
			{
				if (_config.UpdateMode != "off" && !_installing)
				{
					UpdateInfo updateInfo = await Updater.CheckAsync();
					UpdateInfo update = _update;
					if (updateInfo != null && (update == null || updateInfo.Version > update.Version))
					{
						_update = updateInfo;
						_updateAnnounced = false;
						RequestPublish();
					}
				}
				await Task.Delay(TimeSpan.FromHours(6));
			}
		});
	}

	public void Stop()
	{
		ObsServer.Stop();
		_stop = true;
		_wake.Set();
		_watcher?.Dispose();
	}

	public void SetNotice(string? text)
	{
		_notice = text;
		RequestPublish();
	}

	public void Change(Action<Config> change)
	{
		lock (_cfgGate)
		{
			change(_config);
			SaveQuietly();
		}
		RequestPublish();
	}

	public void SetProfile(string key)
	{
		Change((Config c) =>
		{
			c.Profile = key;
		});
	}

	public void SetCountExtra(Action<Config> change)
	{
		_reportRequested = true;
		Change(change);
	}

	public void SetCountFrom(DateTime? from)
	{
		_reportRequested = true;
		Change((Config c) =>
		{
			c.CountFrom = from?.Date;
		});
	}

	public IReadOnlyList<(DateTime Day, int Matches)> MatchDays()
	{
		try
		{
			string key = Profiles.Current(_data, _config)?.Key;
			return (from g in (from m in _data.Matches.ToList()
					where (key == null || Profiles.Key(m) == key) && Profiles.IsSupportedVersion(m)
					select m).GroupBy(Profiles.Day)
				orderby g.Key descending
				select (Key: g.Key, g.Count())).ToList();
		}
		catch (InvalidOperationException)
		{
			return Array.Empty<(DateTime, int)>();
		}
	}

	public void SetShowTeammates(bool show)
	{
		_reportRequested = true;
		Change((Config c) =>
		{
			c.ShowTeammates = show;
		});
	}

	public string ResetStats()
	{
		string result = (_resetBackup = Path.Combine(_store.Folder, $"data-backup-{DateTime.Now:yyyyMMdd-HHmmss}.json"));
		_wake.Set();
		return result;
	}

	public void SetInitialChests(int count)
	{
		string key = _shownKey ?? "";
		Change((Config c) =>
		{
			if (count > 0)
			{
				c.InitialChests[key] = count;
			}
			else
			{
				c.InitialChests.Remove(key);
			}
		});
		_reportRequested = true;
		_wake.Set();
	}

	private void WriteReportAtStart()
	{
		ReportWriter.WriteAll(_store, _config, _data);
		if (_config.OpenReportOnStart && !AutoMode && File.Exists(_store.ReportPath))
		{
			try
			{
				Process.Start(new ProcessStartInfo(_store.ReportPath)
				{
					UseShellExecute = true
				})?.Dispose();
			}
			catch
			{
			}
		}
	}

	public void ChangeDemosFolder(string folder)
	{
		Change((Config c) =>
		{
			c.DemosFolder = folder;
		});
		RestartWatcher();
	}

	public bool SetStartWithFortnite(bool on)
	{
		lock (_cfgGate)
		{
			if (!OperatingSystem.IsWindows() || !FortniteWatch.Apply(on, _dataDir))
			{
				return false;
			}
			_config.StartWithFortnite = on;
			SaveQuietly();
		}
		RequestPublish();
		return true;
	}

	public void KeepOpen()
	{
		if (_updateOffered && _update != null && !_installing)
		{
			_updateOffered = false;
			_notice = null;
			InstallUpdate();
		}
		else
		{
			AutoMode = false;
			_keepOpenRequested = true;
			RequestPublish();
		}
	}

	public void ToggleOverlay()
	{
		if (!OperatingSystem.IsWindows())
		{
			return;
		}
		if (NativeOverlay.IsRunning)
		{
			NativeOverlay.Toggle();
			return;
		}
		lock (_cfgGate)
		{
			_config.OverlayVisible = true;
			SaveQuietly();
		}
		StartOverlay();
		RequestPublish();
	}

	public void InstallUpdate()
	{
		UpdateInfo u = _update;
		if (u == null || _installing)
		{
			return;
		}
		_installing = true;
		RequestPublish();
		Task.Run(() =>
		{
			string text = Updater.Install(u);
			_installing = false;
			if (text == null)
			{
				RestartAfterUpdate = true;
				CloseRequested?.Invoke();
			}
			else
			{
				_notice = L.F("No se pudo actualizar: {0}", "Update failed: {0}", text);
				RequestPublish();
			}
		});
	}

	public void RefreshPersonalStatistics()
	{
		_reportRequested = true;
		RequestPublish();
	}

	public void RequestPublish()
	{
		_publishRequested = true;
		_wake.Set();
	}

	private void Loop()
	{
		WriteReportAtStart();
		bool flag = true;
		while (!_stop)
		{
			bool flag2 = flag || _publishRequested;
			flag = false;
			_publishRequested = false;
			string resetBackup = _resetBackup;
			if (resetBackup != null)
			{
				_resetBackup = null;
				try
				{
					_tracker.Reset(resetBackup);
					lock (_cfgGate)
					{
						_config.InitialChests.Clear();
						_config.Profile = null;
						SaveQuietly();
					}
					_reportRequested = true;
					flag2 = true;
				}
				catch (Exception ex)
				{
					ErrorLog.Write("Restablecer estadísticas", ex);
					_notice = L.F("No se pudo restablecer: {0}", "Could not reset: {0}", ex.Message);
				}
			}
			try
			{
				if (DateTime.UtcNow >= _nextScan)
				{
					bool flag3 = _tracker.UpdateLive();
					int num = _tracker.Scan();
					flag2 |= (num > 0) | flag3;
					_nextScan = DateTime.UtcNow.AddSeconds(_tracker.IsFinishing ? 2 : (_tracker.IsLive || _fortniteRunning ? 5 : (_tracker.HasPending ? 15 : ((_watcher != null && !AutoMode) ? 60 : 10))));
				}
				if (AutoMode)
				{
					flag2 |= CheckAutoClose();
				}
			}
			catch (Exception error)
			{
				ErrorLog.Write("Vigilancia", error);
			}
			if (DateTime.UtcNow >= _nextFortniteCheck)
			{
				_nextFortniteCheck = DateTime.UtcNow.AddSeconds(5.0);
				bool flag4 = FortniteWatch.IsFortniteRunning();
				if (flag4 != _fortniteRunning)
				{
					_fortniteRunning = flag4;
					flag2 = true;
				}
			}
			flag2 |= CheckUpdate();
			if (_reportRequested)
			{
				_reportRequested = false;
				ReportWriter.WriteAll(_store, _config, _data);
			}
			if (flag2 || _publishRequested)
			{
				_publishRequested = false;
				Publish();
			}
			_wake.WaitOne(TimeSpan.FromSeconds(1L));
		}
	}

	private void Publish()
	{
		Profile profile = Profiles.Current(_data, _config);
		_shownKey = profile?.Key;
		_shownName = profile?.Name;
		MatchRecord live = ((_tracker.LiveMatch != null && Profiles.Counted(_tracker.LiveMatch, _config)) ? Profiles.Adjust(_tracker.LiveMatch, _config) : null);
		TrackerData matchHistory;
		IReadOnlyCollection<string> historyExtraKinds;
		bool showHistoryTeammates;
		lock (_cfgGate)
		{
			matchHistory = Profiles.View(_data, _config);
			historyExtraKinds = _config.ExtraKinds();
			showHistoryTeammates = _config.ShowTeammates;
		}
		OverlayView overlayView = OverlayModel.Build(matchHistory, live, _tracker.IsLive, Profiles.InitialChests(_data, _config));
		overlayView = overlayView with
		{
			Badge = ((_tracker.IsLive || overlayView.CurrentLive) ? L.T("● PARTIDA EN CURSO", "● TRACKING MATCH") : (_fortniteRunning ? L.T("○ EN EL LOBBY", "○ IN LOBBY") : null))
		};
		ReportWriter.WriteOverlay(_store, overlayView);
		ObsServer.Update(overlayView);
		if (OperatingSystem.IsWindows() && NativeOverlay.IsRunning)
		{
			NativeOverlay.Update(overlayView with
			{
				TokenChance = PirateShopChances.HudPercent(_store, _config, live),
				LiveStatus = _tracker.LiveWaitingForKey ? L.T("Cofres al terminar", "Chests at match end") : null
			});
		}
		string notice = (_installing ? L.T("Descargando la actualización…", "Downloading the update…") : (_notice ?? (_autoCloseAt.HasValue ? L.T("Fortnite está cerrado: el contador se cierra en 30 s.", "Fortnite is closed: the counter closes in 30 s.") : null)));
		MonitorState obj = new MonitorState(overlayView, Profiles.List(_data, _config), Profiles.Current(_data, _config)?.Key, _update, notice, _autoCloseAt.HasValue, BuildStats())
		{
			Team = BuildTeam(),
			MatchHistory = matchHistory.Matches,
			HistoryExtraKinds = historyExtraKinds,
			ShowHistoryTeammates = showHistoryTeammates
		};
		Changed?.Invoke(obj);
	}

	private ChestStats BuildStats()
	{
		List<MatchRecord> matches = Profiles.View(_data, _config).Matches;
		MatchRecord matchRecord = ((_tracker.IsLive && _tracker.LiveMatch != null && Profiles.Counted(_tracker.LiveMatch, _config)) ? Profiles.Adjust(_tracker.LiveMatch, _config) : null);
		DateTime date = DateTime.Now.Date;
		Dictionary<DateTime, int> dictionary = new Dictionary<DateTime, int>();
		foreach (MatchRecord item in matches)
		{
			DateTime date2 = (item.MatchStartUtc ?? item.AnalyzedAtUtc).ToLocalTime().Date;
			dictionary[date2] = dictionary.GetValueOrDefault(date2) + item.ChestsOpened;
		}
		if (matchRecord != null)
		{
			dictionary[date] = dictionary.GetValueOrDefault(date) + matchRecord.ChestsOpened;
		}
		PersonalIntegration.AddManualDays(dictionary, _data, _config);
		List<MatchRecord> list = ((matchRecord != null) ? matches.Append(matchRecord).ToList() : matches);
		double average = ((matches.Count > 0) ? ((double)matches.Sum((MatchRecord m) => m.ChestsOpened) / (double)matches.Count) : 0.0);
		KeyValuePair<DateTime, int>? keyValuePair = ((dictionary.Count > 0) ? new KeyValuePair<DateTime, int>?(dictionary.MaxBy((KeyValuePair<DateTime, int> kv) => kv.Value)) : ((KeyValuePair<DateTime, int>?)null));
		return new ChestStats(matches.Count, average, (list.Count > 0) ? list.Max((MatchRecord m) => m.ChestsOpened) : 0, list.Sum((MatchRecord m) => m.TreasureFinds.Count), dictionary.GetValueOrDefault(date), keyValuePair?.Key, keyValuePair?.Value ?? 0)
		{
			LastDays = BuildLastDays(date, matchRecord),
			HistoryMatches = Profiles.View(_data, _config).Matches.Count,
			LastMatch = BuildLastMatch(),
			HistorySince = ((IEnumerable<MatchRecord>)Profiles.View(_data, _config).Matches).Select((Func<MatchRecord, DateTime?>)((MatchRecord m) => (m.MatchStartUtc ?? m.AnalyzedAtUtc).ToLocalTime().Date)).Min()
		};
	}

	private TeamSummary? BuildTeam()
	{
		if (!_config.ShowTeammates)
		{
			return null;
		}
		IReadOnlyCollection<string> kinds = _config.ExtraKinds();
		MatchRecord matchRecord = ((_tracker.IsLive && _tracker.LiveMatch != null && Profiles.Counted(_tracker.LiveMatch, _config)) ? Profiles.Adjust(_tracker.LiveMatch, _config) : null);
		MatchRecord matchRecord2 = matchRecord ?? Profiles.View(_data, _config).Matches.OrderByDescending((MatchRecord x) => x.MatchStartUtc ?? x.AnalyzedAtUtc).FirstOrDefault();
		if (matchRecord2 == null)
		{
			return null;
		}
		List<TeamMember> mates = (from t in matchRecord2.Teammates ?? new List<TeammateRecord>()
			select new TeamMember(t.Name, MatchRecord.Seen(t, kinds)) into t
			orderby t.Chests descending
			select t).ToList();
		return new TeamSummary(matchRecord != null, matchRecord2.OwnChests, mates, matchRecord2.Teammates == null);
	}

	private LastMatchSummary? BuildLastMatch()
	{
		MatchRecord matchRecord = Profiles.View(_data, _config).Matches.OrderByDescending((MatchRecord x) => x.MatchStartUtc ?? x.AnalyzedAtUtc).FirstOrDefault();
		if (matchRecord == null)
		{
			return null;
		}
		return new LastMatchSummary(matchRecord.ChestsOpened, Enumerable.Count(matchRecord.Chests, (ChestRecord c) => c.Destroyed), matchRecord.TreasureFinds.Count, matchRecord.Placement, matchRecord.TotalPlayers, matchRecord.Eliminations);
	}

	private List<DayChests> BuildLastDays(DateTime today, MatchRecord? live)
	{
		DateTime first = today.AddDays(-6.0);
		Dictionary<DateTime, int> sums = new Dictionary<DateTime, int>();
		foreach (MatchRecord item in Profiles.View(_data, _config).Matches.Append(live).OfType<MatchRecord>())
		{
			DateTime date = (item.MatchStartUtc ?? item.AnalyzedAtUtc).ToLocalTime().Date;
			if (date >= first && date <= today)
			{
				sums[date] = sums.GetValueOrDefault(date) + item.ChestsOpened;
			}
		}
		PersonalIntegration.AddManualDays(sums, _data, _config);
		return (from i in Enumerable.Range(0, 7)
			select first.AddDays(i) into d
			select new DayChests(d, sums.GetValueOrDefault(d))).ToList();
	}

	private bool CheckUpdate()
	{
		UpdateInfo update = _update;
		if (update == null || _updateAnnounced || _installing)
		{
			return false;
		}
		if (_config.UpdateMode == "off")
		{
			return false;
		}
		if (_tracker.IsLive || _tracker.HasPending || _tracker.IsFinishing || AppInfo.ExePath == null)
		{
			return false;
		}
		_updateAnnounced = true;
		if (_config.UpdateMode == "auto")
		{
			InstallUpdate();
			return true;
		}
		_updateOffered = true;
		_notice = L.F("Versión {0} disponible (tienes la {1}): haz clic aquí para instalarla.", "Version {0} is available (you have {1}): click here to install it.", update.Version.ToString(3), AppInfo.VersionText);
		UpdateOffered?.Invoke(update);
		return true;
	}

	private bool CheckAutoClose()
	{
		if (_keepOpenRequested)
		{
			_keepOpenRequested = false;
			_autoCloseAt = null;
			_fortniteClosedSince = null;
			_closeRequested = false;
			return true;
		}
		DateTime utcNow = DateTime.UtcNow;
		if (FortniteWatch.IsFortniteRunning())
		{
			bool hasValue = _autoCloseAt.HasValue;
			_fortniteClosedSince = null;
			_autoCloseAt = null;
			return hasValue;
		}
		DateTime valueOrDefault = _fortniteClosedSince.GetValueOrDefault();
		if (!_fortniteClosedSince.HasValue)
		{
			valueOrDefault = utcNow;
			_fortniteClosedSince = valueOrDefault;
		}
		if ((_tracker.IsLive || _tracker.HasPending || _tracker.IsFinishing) && utcNow - _fortniteClosedSince.Value < TimeSpan.FromMinutes(5L))
		{
			return false;
		}
		if (!_autoCloseAt.HasValue)
		{
			_autoCloseAt = utcNow.AddSeconds(30.0);
			return true;
		}
		valueOrDefault = utcNow;
		DateTime? autoCloseAt = _autoCloseAt;
		if (autoCloseAt.HasValue && valueOrDefault >= autoCloseAt.GetValueOrDefault() && !_closeRequested)
		{
			_closeRequested = true;
			CloseRequested?.Invoke();
		}
		return false;
	}

	private void StartOverlay()
	{
		NativeOverlay.Start(_config.OverlayX, _config.OverlayY, _config.OverlayLocked, (int x, int y) =>
		{
			lock (_cfgGate)
			{
				_config.OverlayX = x;
				_config.OverlayY = y;
				SaveQuietly();
			}
		}, (bool locked) =>
		{
			lock (_cfgGate)
			{
				_config.OverlayLocked = locked;
				SaveQuietly();
			}
		}, _config.OverlayVisible, (bool visible) =>
		{
			lock (_cfgGate)
			{
				_config.OverlayVisible = visible;
				SaveQuietly();
			}
			RequestPublish();
		}, _config.OverlayCompact, (bool compact) =>
		{
			lock (_cfgGate)
			{
				_config.OverlayCompact = compact;
				SaveQuietly();
			}
		}, _config.OverlayOpacity);
	}

	private void RestartWatcher()
	{
		_watcher?.Dispose();
		_watcher = null;
		try
		{
			if (Directory.Exists(_config.DemosFolder))
			{
				_watcher = new FileSystemWatcher(_config.DemosFolder, "*.replay")
				{
					NotifyFilter = (NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite),
					EnableRaisingEvents = true
				};
				_watcher.Created += (object _, FileSystemEventArgs _) =>
				{
					Wake();
				};
				_watcher.Changed += (object _, FileSystemEventArgs _) =>
				{
					Wake();
				};
				_watcher.Renamed += (object _, RenamedEventArgs _) =>
				{
					Wake();
				};
			}
		}
		catch
		{
			_watcher = null;
		}
	}

	private void Wake()
	{
		DateTime dateTime = DateTime.UtcNow.AddSeconds(2.0);
		if (dateTime < _nextScan)
		{
			_nextScan = dateTime;
		}
		_wake.Set();
	}

	private void SaveQuietly()
	{
		try
		{
			_store.SaveConfig(_config);
		}
		catch
		{
		}
	}
}
