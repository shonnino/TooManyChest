namespace ChestCounter;

public static class WeekendOdds
{
    private static readonly TimeZoneInfo Rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");
    private static readonly Dictionary<string, double> StandardBoosts = new()
    {
        [TreasureCatalog.PirateShop] = 1.5,
        [TreasureCatalog.GoldOutfit] = 7.5,
        [TreasureCatalog.VehicleDecal] = 10,
    };
    public static readonly IReadOnlyList<string> RewardKinds = Array.AsReadOnly(new[]
    {
        TreasureCatalog.PirateShop, TreasureCatalog.GoldOutfit, TreasureCatalog.VehicleDecal,
        TreasureCatalog.PickaxeOrBackBling, TreasureCatalog.JamTrack, TreasureCatalog.LoadingScreen,
    });

    private static DateTime ToUtc(DateTime italy) => TimeZoneInfo.ConvertTimeToUtc(
        DateTime.SpecifyKind(italy, DateTimeKind.Unspecified), Rome);

    public static IReadOnlyList<TreasureHour> Schedule()
    {
        if (TreasureCatalog.Weeks.Count == 0) return [];
        var start = TreasureCatalog.HuntStart;
        var end = TreasureCatalog.Weeks[^1].To;
        var first = TimeZoneInfo.ConvertTimeFromUtc(start, Rome).Date;
        var saturday = first.AddDays(((int)DayOfWeek.Saturday - (int)first.DayOfWeek + 7) % 7);
        var hours = new List<TreasureHour>();
        for (; ToUtc(saturday) < end; saturday = saturday.AddDays(7))
        {
            foreach (var localStart in new[] { saturday.AddHours(20), saturday.AddDays(1).AddHours(3) })
            {
                var from = ToUtc(localStart);
                if (from < start || from >= end) continue;

                // Honour published per-session boosts, while keeping the two requested Rome time slots.
                var published = TreasureCatalog.TreasureHours.FirstOrDefault(h => h.StartUtc == from && h.Confirmed);
                var to = ToUtc(localStart.AddHours(2));
                hours.Add(new TreasureHour(from, to < end ? to : end,
                    published?.Boosts ?? StandardBoosts, published != null));
            }
        }
        return hours;
    }

    public static DateTime NextBoundary(DateTime utc) => Schedule()
        .SelectMany(h => new[] { h.StartUtc, h.EndUtc })
        .Where(t => t > utc).DefaultIfEmpty(DateTime.MaxValue).Min();

    public static IEnumerable<DateTime> ChestTimes(IEnumerable<MatchRecord> matches, DateTime throughUtc)
    {
        foreach (var match in matches)
        {
            if (match.MatchStartUtc is not DateTime recorded) continue;
            var start = recorded.Kind == DateTimeKind.Local ? recorded.ToUniversalTime() : DateTime.SpecifyKind(recorded, DateTimeKind.Utc);
            foreach (var chest in match.Chests)
            {
                if (!float.IsFinite(chest.TimeSeconds) || chest.TimeSeconds < 0) continue;
                var time = start.AddSeconds(chest.TimeSeconds);
                if (time <= throughUtc) yield return time;
            }
        }
    }

    public static double Multiplier(string kind, DateTime utc, IEnumerable<TreasureHour> hours) =>
        hours.FirstOrDefault(h => utc >= h.StartUtc && utc < h.EndUtc)?.Boosts.GetValueOrDefault(kind, 1) ?? 1;

    public static double PerChest(TreasureOdds odds, DateTime utc, IEnumerable<TreasureHour> hours) =>
        Math.Clamp(Multiplier(odds.Kind, utc, hours) / odds.OneIn, 0, 1);

    public static double AtLeastOne(TreasureOdds odds, int total, IReadOnlyCollection<DateTime> times,
        IReadOnlyList<TreasureHour> hours)
    {
        if (total <= 0) return 0;

        var baseLog = double.LogP1(-1.0 / odds.OneIn);
        var logNone = baseLog * total;
        // Manual reductions cannot identify which timestamped chests were removed.
        if (times.Count <= total)
        {
            foreach (var time in times)
                logNone += double.LogP1(-PerChest(odds, time, hours)) - baseLog;
        }
        return -double.ExpM1(logNone);
    }

    public static string CumulativePercent(double probability) => probability >= .9999995 ? "<100%"
        : (probability * 100).ToString("0.####", L.Culture) + "%";

    public static string Percent(double probability) => probability >= .999999995 ? "<100%"
        : (Math.Max(0, probability) * 100).ToString("0.########", L.Culture) + "%";
}
