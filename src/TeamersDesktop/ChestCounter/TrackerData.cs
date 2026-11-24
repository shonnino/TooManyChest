using System;
using System.Collections.Generic;
using System.Linq;

namespace ChestCounter;

public sealed class TrackerData
{
	public int Version { get; set; } = 1;

	public int AnalyzerVersion { get; set; } = 1;

	public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

	public List<MatchRecord> Matches { get; set; } = new List<MatchRecord>();

	public Dictionary<string, string> ProcessedFiles { get; set; } = new Dictionary<string, string>();

	public bool UpgradeNames()
	{
		bool changed = false;
		foreach (MatchRecord match in Matches)
		{
			if (match.NotTrackedOwner != null)
			{
				match.NotTrackedOwner = Up(match.NotTrackedOwner);
			}
			foreach (ChestRecord chest in match.Chests)
			{
				chest.Kind = Up(chest.Kind);
				UpList(chest.Items);
				UpList(chest.TreasureItems);
			}
			foreach (TreasureFind treasureFind in match.TreasureFinds)
			{
				treasureFind.Item = Up(treasureFind.Item);
				treasureFind.SourceKind = Up(treasureFind.SourceKind);
				UpList(treasureFind.AlsoPossible);
			}
			if (!match.OtherInteractions.Keys.Any((string k) => L.Upgrade(k) != k))
			{
				continue;
			}
			Dictionary<string, int> dictionary = new Dictionary<string, int>();
			foreach (KeyValuePair<string, int> otherInteraction in match.OtherInteractions)
			{
				string key = Up(otherInteraction.Key);
				dictionary[key] = dictionary.GetValueOrDefault(key) + otherInteraction.Value;
			}
			match.OtherInteractions = dictionary;
		}
		foreach (string item in (from kv in ProcessedFiles
			where kv.Value == "autre mode"
			select kv.Key).ToList())
		{
			ProcessedFiles[item] = "other mode";
			changed = true;
		}
		return changed;
		string Up(string value)
		{
			string text = L.Upgrade(value);
			changed |= (object)text != value && text != value;
			return text;
		}
		void UpList(List<string> list)
		{
			for (int i = 0; i < list.Count; i++)
			{
				list[i] = Up(list[i]);
			}
		}
	}
}
