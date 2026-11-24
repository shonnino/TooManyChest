using System;
using System.Collections.Generic;

namespace ChestCounter;

public static class TreasureNames
{
	public const string UnknownPrefix = "Unknown:";

	public const string UnknownGizmo = "Treasure gizmo";

	private const string OldUnknownPrefix = "Inconnu:";

	public static Dictionary<string, string> PrizeNames { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	public static string Friendly(string item)
	{
		if (item.Contains(':'))
		{
			bool flag = item.StartsWith("Unknown:", StringComparison.OrdinalIgnoreCase) || item.StartsWith("Inconnu:", StringComparison.OrdinalIgnoreCase);
			if (PrizeNames.TryGetValue(item, out string value) && !string.IsNullOrWhiteSpace(value))
			{
				return value;
			}
			if (flag)
			{
				string text = item.Substring(item.IndexOf(':') + 1);
				string[] array = new string[2] { "Unknown:", "Inconnu:" };
				foreach (string text2 in array)
				{
					if (PrizeNames.TryGetValue(text2 + text, out string value2) && !string.IsNullOrWhiteSpace(value2))
					{
						return value2;
					}
				}
				return L.Name("Treasure gizmo");
			}
			return L.F("Tesoro desconocido ({0})", "Unknown treasure ({0})", item.Substring(item.IndexOf(':') + 1));
		}
		string text3 = item.ToLowerInvariant();
		if (text3.Contains("flameceleb"))
		{
			return "Golden firework";
		}
		if (text3.Contains("relicextractor"))
		{
			return "Portable extractor";
		}
		if (text3.Contains("relicfinder"))
		{
			return "Spirit locator";
		}
		if (text3.Contains("luckylocator"))
		{
			return "Lucky locator";
		}
		if (text3.Contains("smugglerextractionkey") || text3.Contains("extractionaccelerat"))
		{
			return "Extraction accelerator";
		}
		return item;
	}
}
