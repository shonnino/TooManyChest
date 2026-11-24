using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ChestCounter;

public static class PersonalIntegration
{
	private static readonly object Gate = new object();

	private static string folder = Store.DefaultFolder();

	private static PersonalLedger cached = new PersonalLedger();

	private static DateTime stamp = DateTime.MinValue;

	private static long bytes = -1L;

	public static void Bind(string path)
	{
		lock (Gate)
		{
			if (folder != path)
			{
				folder = path;
				stamp = DateTime.MinValue;
				bytes = -1L;
				cached = new PersonalLedger();
			}
		}
		OnlineSync.Start(path);
		PersonalCloud.Start(path);
		if (OperatingSystem.IsWindows())
		{
			OnlineEntry.Install();
		}
	}

	private static PersonalLedger Ledger()
	{
		lock (Gate)
		{
			try
			{
				FileInfo fileInfo = new FileInfo(Path.Combine(folder, "2xflaw-ledger.json"));
				if (!fileInfo.Exists)
				{
					cached = new PersonalLedger();
					stamp = DateTime.MinValue;
					bytes = -1L;
					return cached;
				}
				if (fileInfo.LastWriteTimeUtc == stamp && fileInfo.Length == bytes)
				{
					return cached;
				}
				using FileStream utf8Json = File.Open(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
				PersonalLedger personalLedger = JsonSerializer.Deserialize<PersonalLedger>(utf8Json);
				if (personalLedger != null)
				{
					cached = personalLedger;
					stamp = fileInfo.LastWriteTimeUtc;
					bytes = fileInfo.Length;
				}
			}
			catch (Exception error)
			{
				ErrorLog.Write("Personal ledger projection; retaining last valid snapshot", error);
			}
			return cached;
		}
	}

	public static TrackerData Recorded(TrackerData original)
	{
		List<PersonalImport> imports = Ledger().Imports;
		if (imports.Count == 0)
		{
			return TeammateReplayImports.Project(original, folder);
		}
		List<MatchRecord> matches = (from m in original.Matches.Concat(imports.Select((PersonalImport x) => x.Match))
			group m by string.IsNullOrWhiteSpace(m.SessionId) ? $"fallback:{Profiles.Key(m)}|{m.MatchStartUtc:O}|{Path.GetFileName(m.ReplayFile)}" : ("session:" + m.SessionId) into g
			select g.First()).ToList();
		return TeammateReplayImports.Project(new TrackerData
		{
			Version = original.Version,
			AnalyzerVersion = original.AnalyzerVersion,
			CreatedUtc = original.CreatedUtc,
			Matches = matches,
			ProcessedFiles = original.ProcessedFiles
		}, folder);
	}

	public static List<PersonalEntry> Manual(string? profile, Config config)
	{
		return Ledger().Entries.Where((PersonalEntry e) =>
		{
			if (e.Profile == (profile ?? "") && e.AtUtc <= DateTime.UtcNow)
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
		}).ToList();
	}

	public static int ManualTotal(string? profile, Config config)
	{
		return Manual(profile, config).Sum((PersonalEntry e) => Math.Max(0, e.Mine) + (config.ShowTeammates ? Math.Max(0, e.Squad) : 0));
	}

	public static void AddManualDays(Dictionary<DateTime, int> days, TrackerData data, Config config)
	{
		foreach (PersonalEntry item in Manual(Profiles.Current(data, config)?.Key, config))
		{
			DateTime date = item.AtUtc.ToLocalTime().Date;
			days[date] = days.GetValueOrDefault(date) + Math.Max(0, item.Mine) + (config.ShowTeammates ? Math.Max(0, item.Squad) : 0);
		}
	}
}
