using System.Collections.Generic;

namespace ChestCounter;

public static class Pretty
{
	public static string Likely => L.T("probable", "likely");

	public static string Playlist(string? playlist)
	{
		if (string.IsNullOrEmpty(playlist))
		{
			return L.T("modo desconocido", "unknown mode");
		}
		string text = playlist.ToLowerInvariant();
		List<string> list = new List<string>();
		if (text.Contains("habanero") || text.Contains("ranked"))
		{
			list.Add(L.T("Clasificatoria", "Ranked"));
		}
		if (text.Contains("nobuild"))
		{
			list.Add(L.T("Cero construcción", "Zero Build"));
		}
		if (text.Contains("solo"))
		{
			list.Add("Solo");
		}
		else if (text.Contains("duo"))
		{
			list.Add(L.T("Dúo", "Duos"));
		}
		else if (text.Contains("trio"))
		{
			list.Add(L.T("Trío", "Trios"));
		}
		else if (text.Contains("squad"))
		{
			list.Add(L.T("Escuadrón", "Squads"));
		}
		if (list.Count <= 0)
		{
			return playlist.Replace("Playlist_", "");
		}
		return string.Join(" · ", list);
	}

	public static string Placement(MatchRecord m)
	{
		int? placement = m.Placement;
		if (placement.HasValue)
		{
			int valueOrDefault = placement.GetValueOrDefault();
			if (valueOrDefault > 0)
			{
				if (valueOrDefault == 1)
				{
					return L.T("¡Victoria Real!", "Victory Royale!");
				}
				string text = L.Ordinal(valueOrDefault);
				placement = m.TotalPlayers;
				if (placement.HasValue)
				{
					int valueOrDefault2 = placement.GetValueOrDefault();
					if (valueOrDefault2 > 0)
					{
						return L.F("{0} de {1}", "{0} of {1}", text, valueOrDefault2);
					}
				}
				return text;
			}
		}
		return L.T("puesto desconocido", "unknown placement");
	}

	public static string Clock(float seconds)
	{
		int num = (int)seconds;
		return $"{num / 60}:{num % 60:00}";
	}

	public static string Obtained(TreasureFind f)
	{
		float? obtainedAtSeconds = f.ObtainedAtSeconds;
		if (obtainedAtSeconds.HasValue)
		{
			float valueOrDefault = obtainedAtSeconds.GetValueOrDefault();
			string text = (f.TimeExact ? (L.T("a las", "at") + " " + Clock(valueOrDefault)) : (L.T("hacia las", "around") + " " + Clock(valueOrDefault)));
			if (!f.SourceCertain)
			{
				return $"{text} · {f.SourceLabel} ({Likely})";
			}
			return text + " · " + f.SourceLabel;
		}
		return L.T("momento y origen desconocidos", "time and source unknown");
	}

	public static string ItemName(string item)
	{
		string text = item;
		string[] array = new string[3] { "WID_", "Athena", "AmmoData" };
		foreach (string oldValue in array)
		{
			text = text.Replace(oldValue, "");
		}
		return text.Replace("_", " ").Trim();
	}
}
