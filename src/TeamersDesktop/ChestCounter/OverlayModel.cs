using System;
using System.Collections.Generic;
using System.Linq;

namespace ChestCounter;

public static class OverlayModel
{
	public const string TreasureIcon = "\ud83d\udc8e";

	public static string LastMatchCaption => L.T("anterior", "previous");
	public static string TokenLabel => L.T("Ficha pirata", "Pirate token");
	public static string SquadLabel => L.T("Equipo", "Squad");

	public static OverlayView Build(TrackerData d, MatchRecord? live, bool inProgress, int initialChests = 0)
	{
		int num = live?.ChestsOpened ?? 0;
		int num2 = live?.TreasureFinds.Count ?? 0;
		int n = d.Matches.Sum((MatchRecord m) => m.ChestsOpened) + num + Math.Max(0, initialChests);
		List<MatchRecord> list = d.Matches.Where(ReportWriter.IsThisSession).ToList();
		List<OverlayRow> list2 = new List<OverlayRow>();
		string detail = null;
		string value = "—";
		if (list.Count > 0)
		{
			value = Math.Round((double)list.Sum((MatchRecord m) => m.ChestsOpened) / (double)list.Count, MidpointRounding.AwayFromZero).ToString("0", L.Culture);
			detail = $"{L.Count(list.Count, "partida", "partidas", "match", "matches")} · {L.T("media", "avg.")} {value}";
		}
		list2.Add(new OverlayRow(L.T("Sesión", "Session"), L.N(list.Sum((MatchRecord m) => m.ChestsOpened) + num), Treasure: false, detail));
		list2.Add(new OverlayRow(L.T("Media", "Avg."), value));
		MatchRecord matchRecord = d.Matches.OrderByDescending((MatchRecord m) => m.MatchStartUtc ?? m.AnalyzedAtUtc).FirstOrDefault();
		if (matchRecord != null && ((live != null) | inProgress))
		{
			list2.Add(new OverlayRow(L.T("Anterior", "Previous"), L.N(matchRecord.ChestsOpened)));
		}
		int n2 = d.Matches.Sum((MatchRecord m) => m.TreasureFinds.Count) + num2;
		list2.Add(new OverlayRow(L.T("Tesoros", "Treasures"), "\ud83d\udc8e " + L.N(n2), Treasure: true));
		string? last = matchRecord != null ? L.N(matchRecord.ChestsOpened) : null;
		var view = live != null
			? new OverlayView(L.N(n), L.N(live.ChestsOpened), L.T("actual", "current"), true, L.T("● EN DIRECTO", "● LIVE"), list2)
			: inProgress
				? new OverlayView(L.N(n), "…", L.T("en curso", "in progress"), true, null, list2)
				: new OverlayView(L.N(n), last, LastMatchCaption, false, null, list2);
		return view with { Last = last, Treasures = L.N(n2), Session = list2[0].Value };
	}
}
