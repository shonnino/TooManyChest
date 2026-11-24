using System;
using System.Collections.Generic;

namespace ChestCounter;

public sealed record ChestStats(int Matches, double Average, int BestMatch, int Treasures, int Today, DateTime? BestDay, int BestDayChests)
{
	public IReadOnlyList<DayChests> LastDays { get; init; } = Array.Empty<DayChests>();

	public int HistoryMatches { get; init; }

	public DateTime? HistorySince { get; init; }

	public LastMatchSummary? LastMatch { get; init; }

	public static readonly ChestStats Empty = new ChestStats(0, 0.0, 0, 0, 0, null, 0);
}
