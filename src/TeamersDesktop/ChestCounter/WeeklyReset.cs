using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ChestCounter;

public static class WeeklyReset
{
	public static readonly TimeSpan TimeOfDay = TimeSpan.FromHours(14);

	private static DateTime AsUtc(DateTime t)
	{
		if (t.Kind != DateTimeKind.Local)
		{
			return DateTime.SpecifyKind(t, DateTimeKind.Utc);
		}
		return t.ToUniversalTime();
	}

	public static DateTime Start(DateTime now)
	{
		DateTime dateTime = AsUtc(now);
		DateTime dateTime2 = dateTime.Date.AddDays(0 - dateTime.DayOfWeek).Add(TimeOfDay);
		if (!(dateTime2 > dateTime))
		{
			return dateTime2;
		}
		return dateTime2.AddDays(-7.0);
	}

	public static DateTime Next(DateTime now)
	{
		return Start(now).AddDays(7.0);
	}

	public static TimeSpan Remaining(DateTime now)
	{
		return Next(now) - AsUtc(now);
	}

	public static string OwnedKey(string rewardKey, DateTime now)
	{
		if (!(rewardKey == "pirate-shop-access"))
		{
			return $"{rewardKey}@{Start(now):yyyyMMdd}";
		}
		return rewardKey + "@all-time";
	}

	public static bool MigratePirateOwnership(Config config, DateTime now)
	{
		string key = OwnedKey("pirate-shop-access", now);
		if (config.OwnedRewards.ContainsKey(key))
		{
			return false;
		}
		string prefix = "pirate-shop-access@";
		List<KeyValuePair<string, bool>> list = config.OwnedRewards.Where((KeyValuePair<string, bool> pair) => pair.Key.StartsWith(prefix, StringComparison.Ordinal) && DateTime.TryParseExact(pair.Key.Substring(prefix.Length), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result) && result.Date <= AsUtc(now).Date).OrderByDescending((KeyValuePair<string, bool> pair) => pair.Key, StringComparer.Ordinal).ToList();
		if (list.Count == 0)
		{
			return false;
		}
		config.OwnedRewards[key] = list[0].Value;
		return true;
	}
}
