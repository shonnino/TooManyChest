using System;
using System.Collections.Generic;

namespace ChestCounter;

public sealed record MonitorState(OverlayView View, IReadOnlyList<Profile> Profiles, string? CurrentKey, UpdateInfo? Update, string? Notice, bool AutoCloseOffer, ChestStats Stats)
{
	public TeamSummary? Team { get; init; }
	internal IReadOnlyList<MatchRecord> MatchHistory { get; init; } = Array.Empty<MatchRecord>();
	internal IReadOnlyCollection<string> HistoryExtraKinds { get; init; } = Array.Empty<string>();

	internal bool ShowHistoryTeammates { get; init; } = true;
}
