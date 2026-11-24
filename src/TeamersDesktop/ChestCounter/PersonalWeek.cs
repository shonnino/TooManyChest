using System;
using System.Linq;

namespace ChestCounter;

public static class PersonalWeek
{
	public static int PersonalCount(MatchRecord match, DateTime startUtc, DateTime endUtc)
	{
		DateTime? matchStartUtc = match.MatchStartUtc;
		if (matchStartUtc.HasValue)
		{
			DateTime time = matchStartUtc.GetValueOrDefault();
			if (match.Chests.Count > 0)
			{
				return Enumerable.Count(match.Chests, (ChestRecord c) => time.AddSeconds(c.TimeSeconds) >= startUtc && time.AddSeconds(c.TimeSeconds) < endUtc);
			}
			DateTime dateTime = time.AddSeconds(match.DurationSeconds.GetValueOrDefault());
			if (!(time >= startUtc) || !(time < endUtc) || !(dateTime < endUtc))
			{
				return 0;
			}
			return match.ChestsOpened;
		}
		return 0;
	}
}
