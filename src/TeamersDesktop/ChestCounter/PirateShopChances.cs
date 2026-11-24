using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ChestCounter;

public static class PirateShopChances
{
	public const string Key = "pirate-shop-access";

	private static T Read<T>(string path, Func<T> fallback) where T : class
	{
		if (!File.Exists(path))
		{
			return fallback();
		}
		return JsonSerializer.Deserialize<T>(File.ReadAllText(path)) ?? fallback();
	}

	public static ChanceSnapshot Apply(ChanceSnapshot weekly, Store store, Monitor monitor, DateTime now)
	{
		now = ((now.Kind == DateTimeKind.Local) ? now.ToUniversalTime() : DateTime.SpecifyKind(now, DateTimeKind.Utc));
		Config config = monitor.Config;
		if (WeeklyReset.MigratePirateOwnership(config, now))
		{
			store.SaveConfig(config);
		}
		PrizeChance prizeChance = weekly.Prizes.First(p => p.Key == Key);
		var (none, chests) = Calculate(store, config, now, prizeChance.Den, null);
		double liveRate = Math.Clamp(WeeklyChanceModel.BoostFor(weekly.Live, Key) / prizeChance.Den, 0.0, 1.0);
		PrizeChance replacement = prizeChance with
		{
			Name = prizeChance.Name + " (all time)",
			Chance = 1.0 - none,
			Next50 = 1.0 - none * Math.Pow(1.0 - liveRate, 50.0),
			Owned = config.OwnedRewards.GetValueOrDefault(WeeklyReset.OwnedKey(Key, now)),
			AllTimeChests = chests
		};
		return weekly with { Prizes = weekly.Prizes.Select(p => p.Key == Key ? replacement : p).ToList() };
	}

	internal static string HudPercent(Store store, Config config, MatchRecord? live)
	{
		try
		{
			double denominator = WeeklyChanceModel.Table.First(p => p.Key == Key).Den;
			var (none, _) = Calculate(store, config, DateTime.UtcNow, denominator, live);
			return (1.0 - none).ToString("0.0000%", L.Culture);
		}
		catch (IOException) { return "—"; }
		catch (JsonException) { return "—"; }
	}

	private static (double None, int Chests) Calculate(Store store, Config config, DateTime now, double denominator, MatchRecord? live)
	{
		TrackerData trackerData = PersonalReadCache.Load(store);
		string profile = Profiles.Current(trackerData, config)?.Key ?? config.Profile ?? "";
		PersonalLedger personalLedger = Read(Path.Combine(store.Folder, "2xflaw-ledger.json"), () => new PersonalLedger());
		List<TreasureWindow> source = Read(Path.Combine(store.Folder, "2xflaw-odds.json"), PersonalOdds.Seed).Windows.OrderBy((TreasureWindow treasureWindow) => treasureWindow.StartUtc).ToList();
		List<(DateTime? Time, int Count)> events = new List<(DateTime?, int)>();
		Add(null, Profiles.InitialChests(config, profile));
		foreach (MatchRecord item in from m in trackerData.Matches.Concat(personalLedger.Imports.Select((PersonalImport x) => x.Match))
			where Profiles.Key(m) == profile && m.IsBattleRoyale && Profiles.Counted(m, config) && (!m.MatchStartUtc.HasValue || m.MatchStartUtc <= now)
			group m by string.IsNullOrEmpty(m.SessionId) ? $"{m.ReplayFile}|{m.MatchStartUtc:O}" : m.SessionId into g
			select g.First())
		{
			int num = Math.Max(0, Profiles.Adjust(item, config)?.ChestsOpened ?? 0);
			int num2 = Math.Min(num, item.Chests.Count);
			foreach (ChestRecord item2 in item.Chests.Take(num2))
			{
				DateTime? dateTime = item.MatchStartUtc?.AddSeconds(item2.TimeSeconds);
				if (!dateTime.HasValue || dateTime <= now)
				{
					Add(dateTime, 1);
				}
			}
			Add(null, num - num2);
		}
		if (live != null && Profiles.Key(live) == profile && live.IsBattleRoyale)
		{
			int detailCount = Math.Min(live.ChestsOpened, live.Chests.Count);
			foreach (ChestRecord chest in live.Chests.Take(detailCount))
				Add(live.MatchStartUtc?.AddSeconds(chest.TimeSeconds), 1);
			Add(null, live.ChestsOpened - detailCount);
		}
		foreach (PersonalEntry item3 in personalLedger.Entries.Where((PersonalEntry e) =>
		{
			if (e.Profile == profile && e.AtUtc <= now)
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
		}))
		{
			Add(item3.AtUtc, Math.Max(0, item3.Mine));
			if (config.ShowTeammates)
			{
				Add(null, Math.Max(0, item3.Squad));
			}
		}
		double num3 = 1.0;
		foreach (var item4 in events)
		{
			var (dateTime2, _) = item4;
			object w;
			if (dateTime2.HasValue)
			{
				DateTime time = dateTime2.GetValueOrDefault();
				w = source.FirstOrDefault((TreasureWindow treasureWindow) => time >= treasureWindow.StartUtc && time < treasureWindow.EndUtc);
			}
			else
			{
				w = null;
			}
			double num4 = Math.Clamp(WeeklyChanceModel.BoostFor((TreasureWindow?)w, Key) / denominator, 0.0, 1.0);
			num3 *= Math.Pow(1.0 - num4, item4.Count);
		}
		return (num3, events.Sum(e => e.Count));
		void Add(DateTime? item, int count)
		{
			if (count > 0)
			{
				events.Add((item, count));
			}
		}
	}
}
