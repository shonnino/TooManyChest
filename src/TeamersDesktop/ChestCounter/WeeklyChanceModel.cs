using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ChestCounter;

public static class WeeklyChanceModel
{
	public const int Lookahead = 50;

	public static readonly (string Key, string Name, double Den)[] Table = new (string, string, double)[7]
	{
		("gold-outfit", "Weekly Gold Outfit", 1562500.0),
		("pirate-shop-access", "Pirate Shop Access", 137500.0),
		("vehicle-decal", "Weekly Vehicle & Decal", 7813.0),
		("pickaxe-back-bling", "Weekly Pickaxe or Back Bling", 568.0),
		("jam-track", "Weekly Jam Track", 391.0),
		("loading-screen", "Weekly Loading Screen", 272.0),
		("gizmo", "Gizmo (Any)", 127.0)
	};

	private static T? Read<T>(string path)
	{
		try
		{
			return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path)) : default(T);
		}
		catch
		{
			return default;
		}
	}

	public static double BoostFor(TreasureWindow? w, string key)
	{
		return TreasureHours.Boost(w, key);
	}

	public static ChanceSnapshot Compute(Store store, Monitor monitor, DateTime now)
	{
		TrackerData trackerData = PersonalReadCache.Load(store);
		string key = Profiles.Current(trackerData, monitor.Config)?.Key ?? "";
		PersonalLedger personalLedger = Read<PersonalLedger>(Path.Combine(store.Folder, "2xflaw-ledger.json")) ?? new PersonalLedger();
		PersonalOdds? obj = Read<PersonalOdds>(Path.Combine(store.Folder, "2xflaw-odds.json")) ?? PersonalOdds.Seed();
		DateTime start = WeeklyReset.Start(now);
		DateTime end = start.AddDays(7.0);
		List<TreasureWindow> source = obj.Windows.OrderBy((TreasureWindow treasureWindow3) => treasureWindow3.StartUtc).ToList();
		List<MatchRecord> list = (from matchRecord in trackerData.Matches.Concat(personalLedger.Imports.Select((PersonalImport x) => x.Match))
			where Profiles.Key(matchRecord) == key && matchRecord.IsBattleRoyale
			group matchRecord by string.IsNullOrEmpty(matchRecord.SessionId) ? $"{matchRecord.ReplayFile}|{matchRecord.MatchStartUtc:O}" : matchRecord.SessionId into g
			select g.First()).ToList().Where((MatchRecord matchRecord) =>
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
		foreach (PersonalEntry item4 in personalLedger.Entries.Where((PersonalEntry le) => le.Profile == key && le.AtUtc >= start && le.AtUtc <= now))
		{
			list2.Add((item4.AtUtc, item4.Mine));
		}
		TreasureWindow treasureWindow = source.FirstOrDefault((TreasureWindow treasureWindow3) => now >= treasureWindow3.StartUtc && now < treasureWindow3.EndUtc);
		TreasureWindow treasureWindow2 = (from treasureWindow3 in source
			where treasureWindow3.StartUtc > now
			orderby treasureWindow3.StartUtc
			select treasureWindow3).FirstOrDefault();
		Dictionary<string, bool> ownedRewards = monitor.Config.OwnedRewards;
		List<PrizeChance> list3 = new List<PrizeChance>();
		(string, string, double)[] table = Table;
		bool value = default;
		for (int num = 0; num < table.Length; num++)
		{
			(string, string, double) tuple = table[num];
			string item = tuple.Item1;
			string item2 = tuple.Item2;
			double item3 = tuple.Item3;
			double num2 = 1.0;
			foreach (var item5 in list2)
			{
				var (dateTime, _) = item5;
				object obj2;
				if (dateTime.HasValue)
				{
					DateTime t = dateTime.GetValueOrDefault();
					obj2 = source.FirstOrDefault((TreasureWindow x) => t >= x.StartUtc && t < x.EndUtc);
				}
				else
				{
					obj2 = null;
				}
				TreasureWindow w = (TreasureWindow)obj2;
				num2 *= Math.Pow(1.0 - Math.Clamp(BoostFor(w, item) / item3, 0.0, 1.0), item5.Item2);
			}
			double chance = 1.0 - num2;
			double num3 = Math.Clamp(BoostFor(treasureWindow, item) / item3, 0.0, 1.0);
			double next = 1.0 - num2 * Math.Pow(1.0 - num3, 50.0);
			bool owned = (item != "gizmo" && ownedRewards.TryGetValue(WeeklyReset.OwnedKey(item, now), out value)) & value;
			list3.Add(new PrizeChance(item, item2, item3, BoostFor(treasureWindow, item), chance, next, owned));
		}
		return PirateShopChances.Apply(new ChanceSnapshot(list3, list2.Sum(((DateTime? time, int count) x) => x.count), treasureWindow, treasureWindow2, start, treasureWindow != null && TreasureHours.IsUnconfirmed(treasureWindow), treasureWindow2 != null && TreasureHours.IsUnconfirmed(treasureWindow2)), store, monitor, now);
	}

	public static double ChestsToHalf(double chance, double den)
	{
		if (chance >= 0.5)
		{
			return 0.0;
		}
		double num = 1.0 / den;
		return Math.Log(0.5 / (1.0 - chance)) / Math.Log(1.0 - num);
	}

	public static double After(double chance, double den, double boost, int chests)
	{
		return 1.0 - (1.0 - chance) * Math.Pow(1.0 - Math.Clamp(boost / den, 0.0, 1.0), chests);
	}
}
