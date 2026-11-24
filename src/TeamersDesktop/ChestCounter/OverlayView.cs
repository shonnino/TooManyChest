using System.Collections.Generic;
using System.Linq;

namespace ChestCounter;

public sealed record OverlayView(string Total, string? Current, string CurrentCaption, bool CurrentLive, string? Badge, IReadOnlyList<OverlayRow> Rows)
{
	public string Treasures { get; init; } = "0";
	public string Session { get; init; } = "0";
	public string? Last { get; init; }
	public string? Squad { get; init; }
	public string TokenChance { get; init; } = "—";
	public string? LiveStatus { get; init; }

	internal bool SameContent(OverlayView other) => Equals(other with { Rows = Rows }) && Rows.SequenceEqual(other.Rows);
}
