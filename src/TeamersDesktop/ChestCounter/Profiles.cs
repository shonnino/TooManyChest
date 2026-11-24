using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace ChestCounter;

public static class Profiles
{
	public const int MinFortniteMajorVersion = 43;

	private static readonly DateTime SupportedSince = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

	private static readonly Regex VersionPattern = new Regex("(?:Release-|v|\\b)(\\d+)\\.(\\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	public static int? ParseFortniteMajorVersion(string? version)
	{
		if (string.IsNullOrWhiteSpace(version))
		{
			return null;
		}
		Match match = VersionPattern.Match(version);
		if (match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var result))
		{
			return result;
		}
		return null;
	}

	public static bool IsSupportedVersion(MatchRecord? m)
	{
		if (m == null)
		{
			return false;
		}
		int? num = ParseFortniteMajorVersion(m.GameVersion);
		if (num.HasValue)
		{
			return num.Value >= 43;
		}
		DateTime dateTime = m.MatchStartUtc ?? m.AnalyzedAtUtc;
		if (dateTime != default(DateTime) && dateTime < SupportedSince)
		{
			return false;
		}
		return true;
	}

	public static string Key(MatchRecord m)
	{
		string? text;
		if (string.IsNullOrEmpty(m.EpicId))
		{
			text = m.PlayerName;
			if (text == null)
			{
				return "";
			}
		}
		else
		{
			text = m.EpicId;
		}
		return text;
	}

	public static List<Profile> List(TrackerData d, Config? c = null)
	{
		return (from p in PersonalIntegration.Recorded(d).Matches.GroupBy(Key).Select((IGrouping<string, MatchRecord> g) =>
			{
				List<MatchRecord> list = g.Where((MatchRecord m2) => (c != null) ? Counted(m2, c) : IsSupportedVersion(m2)).ToList();
				if (list.Count == 0)
				{
					return (Profile)null;
				}
				MatchRecord m = list.OrderByDescending(When).First();
				string text = (from matchRecord in list.OrderByDescending(When)
					select matchRecord.PlayerName).FirstOrDefault((string n) => !string.IsNullOrWhiteSpace(n));
				int chests = list.Sum((MatchRecord matchRecord) => ((c != null) ? Adjust(matchRecord, c) : matchRecord).ChestsOpened);
				return new Profile(g.Key, text ?? L.T("Cuenta desconocida", "Unknown account"), list.Count, chests, When(m));
			})
			where p != null
			select (p) into p
			orderby p.LastPlayed descending
			select p).ToList();
	}

	private static DateTime When(MatchRecord m)
	{
		return m.MatchStartUtc ?? m.AnalyzedAtUtc;
	}

	public static Profile? Current(TrackerData d, Config c)
	{
		List<Profile> source = List(d, c);
		return source.FirstOrDefault((Profile p) => p.Key == c.Profile) ?? source.FirstOrDefault();
	}

	public static TrackerData View(TrackerData d, Config c)
	{
		Profile current = Current(d, c);
		return new TrackerData
		{
			Version = d.Version,
			AnalyzerVersion = d.AnalyzerVersion,
			CreatedUtc = d.CreatedUtc,
			Matches = (from m in RecordedForProfile(d, c, current?.Key)
				select Adjust(m, c)).ToList(),
			ProcessedFiles = d.ProcessedFiles
		};
	}

	internal static IEnumerable<MatchRecord> RecordedForProfile(TrackerData data, Config config, string? profile) =>
		PersonalIntegration.Recorded(data).Matches.Where(match => (profile == null || Key(match) == profile) && Counted(match, config));

	public static DateTime Day(MatchRecord m)
	{
		return When(m).ToLocalTime().Date;
	}

	public static bool Counted(MatchRecord m, Config c)
	{
		if (IsSupportedVersion(m))
		{
			DateTime? countFrom = c.CountFrom;
			if (countFrom.HasValue)
			{
				DateTime valueOrDefault = countFrom.GetValueOrDefault();
				return Day(m) >= valueOrDefault.Date;
			}
			return true;
		}
		return false;
	}

	public static MatchRecord? Adjust(MatchRecord? m, Config c)
	{
		return m?.WithExtras(c.ExtraKinds(), c.ShowTeammates);
	}

	public static int InitialChests(Config c, string? key)
	{
		if (c.InitialChests == null || !c.InitialChests.TryGetValue(key ?? "", out var value))
		{
			return 0;
		}
		return Math.Max(0, value);
	}

	public static int InitialChests(TrackerData d, Config c)
	{
		return InitialChests(c, Current(d, c)?.Key) + PersonalIntegration.ManualTotal(Current(d, c)?.Key, c);
	}

	public static string Describe(Profile p)
	{
		return $"{p.Name}  ({L.Count(p.Matches, "partida", "partidas", "match", "matches")}, {L.Count(p.Chests, "cofre", "cofres", "chest", "chests")})";
	}
}
