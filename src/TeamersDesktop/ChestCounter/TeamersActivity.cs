using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace ChestCounter;

internal sealed record TeamersPoint(long At, int Opened, int Broken, int Extra)
{
    [JsonIgnore] public int Total => Opened + Broken + Extra;

    [JsonIgnore] public DateTime LocalTime => DateTimeOffset.FromUnixTimeMilliseconds(At).LocalDateTime;
}

internal sealed record TeamersActivity(int Matches, int Opened, int Broken, int Extra, double? Average,
    int? Best, long? BestAt, double? PerMinute, int? Last, int? Treasures, TeamersPoint[] Recent)
{
    public static TeamersActivity FromMatches(IEnumerable<MatchRecord> source, int options)
    {
        var entries = source.Select(match =>
        {
            var when = (match.MatchStartUtc ?? match.AnalyzedAtUtc).ToUniversalTime();
            var details = TeamersMatchActivity.FromMatch(match);
            int extra = TeamersClient.ChestCounts.FromMatch(match).Total(options) - match.OwnChests;
            var point = new TeamersPoint(new DateTimeOffset(when).ToUnixTimeMilliseconds(),
                match.OwnChests - details.Broken, details.Broken, extra);
            return (Point: point, Details: details);
        }).Where(entry => entry.Point.At >= 0).OrderBy(entry => entry.Point.At).ToArray();
        var points = entries.Select(entry => entry.Point).ToArray();
        var best = points.OrderByDescending(point => point.Total).ThenByDescending(point => point.At).FirstOrDefault();
        int timedChests = entries.Where(entry => entry.Details.DurationSeconds is > 0).Sum(entry => entry.Point.Total);
        long seconds = entries.Sum(entry => (long)Math.Max(0, entry.Details.DurationSeconds ?? 0));
        return new TeamersActivity(points.Length, points.Sum(point => point.Opened), points.Sum(point => point.Broken),
            points.Sum(point => point.Extra), points.Length == 0 ? null : points.Average(point => point.Total),
            best?.Total, best?.At, seconds > 0 ? timedChests * 60.0 / seconds : null, points.LastOrDefault()?.Total,
            entries.Sum(entry => entry.Details.Treasures), points.TakeLast(100).ToArray());
    }
}

internal sealed record TeamersMatchActivity(int Broken, int? DurationSeconds, int Treasures)
{
    public static TeamersMatchActivity FromMatch(MatchRecord match) =>
        new(Math.Min(match.OwnChests, match.Chests.Count(chest => chest.Destroyed)), match.DurationSeconds, match.TreasureFinds.Count);
}
