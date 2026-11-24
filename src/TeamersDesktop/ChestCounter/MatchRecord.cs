using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace ChestCounter;

public sealed class MatchRecord
{
	public string SessionId { get; set; } = "";

	public string ReplayFile { get; set; } = "";

	public DateTime AnalyzedAtUtc { get; set; }

	public DateTime? MatchStartUtc { get; set; }

	public int? DurationSeconds { get; set; }

	public string? Playlist { get; set; }

	public string? GameVersion { get; set; }

	public string? PlayerName { get; set; }

	public string? EpicId { get; set; }

	public bool TrackedByUsername { get; set; }

	public string? NotTrackedOwner { get; set; }

	public bool IsReplayOwner { get; set; }

	public int? Placement { get; set; }

	public int? TotalPlayers { get; set; }

	public int? Eliminations { get; set; }

	public int ChestsOpened { get; set; }

	public int AmmoBoxesOpened { get; set; }

	public int OtherContainersOpened { get; set; }

	public Dictionary<string, int> OtherInteractions { get; set; } = new Dictionary<string, int>();

	public int AnalyzerVersion { get; set; } = 1;

	public bool IsBattleRoyale { get; set; } = true;

	public List<ChestRecord> Chests { get; set; } = new List<ChestRecord>();

	public List<TreasureFind> TreasureFinds { get; set; } = new List<TreasureFind>();

	public List<TeammateRecord>? Teammates { get; set; }

	private List<TeammateRecord>? originalTeammates;

	internal MatchRecord WithTeammateReplays(List<TeammateRecord> teammates)
	{
		var match = (MatchRecord)MemberwiseClone();
		match.originalTeammates = originalTeammates ?? Teammates;
		match.Teammates = teammates;
		return match;
	}

	internal int ReplayTeammatesSeen(IReadOnlyCollection<string> kinds) =>
		(originalTeammates ?? Teammates)?.Sum(mate => Seen(mate, kinds)) ?? 0;

	[JsonIgnore]
	public int TeammatesAdded { get; private set; }

	[JsonIgnore]
	public int OwnChests => ChestsOpened - TeammatesAdded;

	public int Interactions(string kind)
	{
		if (OtherInteractions.Count <= 0)
		{
			if (!(kind == "Ammo box"))
			{
				return 0;
			}
			return AmmoBoxesOpened;
		}
		return OtherInteractions.GetValueOrDefault(kind);
	}

	public static int Seen(TeammateRecord t, IReadOnlyCollection<string> kinds)
	{
		return t.ChestsSeen + kinds.Sum((string k) => t.Kinds.GetValueOrDefault(k));
	}

	public int TeammatesSeen(IReadOnlyCollection<string> kinds)
	{
		return Teammates?.Sum((TeammateRecord t) => Seen(t, kinds)) ?? 0;
	}

	public MatchRecord WithExtras(IReadOnlyCollection<string> kinds, bool addTeammates = false)
	{
		int num = ((IEnumerable<string>)kinds).Sum((Func<string, int>)Interactions);
		int num2 = (addTeammates ? TeammatesSeen(kinds) : 0);
		if (num == 0 && num2 == 0)
		{
			return this;
		}
		MatchRecord matchRecord = (MatchRecord)MemberwiseClone();
		matchRecord.ChestsOpened = ChestsOpened + num + num2;
		matchRecord.TeammatesAdded = num2;
		return matchRecord;
	}
}
