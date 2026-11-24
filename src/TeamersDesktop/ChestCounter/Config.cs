using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ChestCounter;

public sealed class Config
{
	[JsonPropertyName("UiLanguage")]
	public string? Language { get; set; }

	public string? Profile { get; set; }

	public string DemosFolder { get; set; } = "";

	public bool StartWithFortnite { get; set; }

	[JsonPropertyName("UpdateModeV2")]
	public string UpdateMode { get; set; } = "auto";

	[JsonPropertyName("OpenReportAtStartup")]
	public bool OpenReportOnStart { get; set; }

	public int? OverlayX { get; set; }

	public int? OverlayY { get; set; }

	public bool OverlayLocked { get; set; }

	public bool OverlayVisible { get; set; }

	public bool OverlayCompact { get; set; }

	public int OverlayOpacity { get; set; } = 100;

	public int ChestGoal { get; set; }

	public bool Animations { get; set; } = true;

	public bool ShowBanner { get; set; } = true;

	public Dictionary<string, bool> OwnedRewards { get; set; } = new Dictionary<string, bool>();

	public bool LiveUpdate { get; set; } = true;

	public int LiveIntervalSeconds { get; set; } = 30;

	public int ObsPort { get; set; } = 7373;

	public Dictionary<string, int> InitialChests { get; set; } = new Dictionary<string, int>();

	public bool CountAmmoBoxes { get; set; }

	public bool CountFoodBoxes { get; set; }

	public bool CountLootCannons { get; set; }

	public bool ShowTeammates { get; set; } = true;

	public DateTime? CountFrom { get; set; }

	// Retained for existing personal cloud sync configurations.
	public bool CountTeammateChests { get; set; }

	public IReadOnlyCollection<string> ExtraKinds()
	{
		List<string> list = new List<string>(3);
		if (CountAmmoBoxes)
		{
			list.Add("Ammo box");
		}
		if (CountFoodBoxes)
		{
			list.Add("Food box");
		}
		if (CountLootCannons)
		{
			list.Add("Loot cannon");
		}
		return list;
	}
}
