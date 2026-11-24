using System;
using System.Collections.Generic;

namespace ChestCounter;

public sealed class PersonalOdds
{
	public string Source { get; set; } = "https://fortnite.bot/treasure";

	public DateTime CapturedUtc { get; set; } = new DateTime(2026, 11, 14, 1, 34, 0, DateTimeKind.Utc);

	public string Status { get; set; } = "Dated snapshot. Event times are entered by you and the rates are checked against the source. Nothing is fetched automatically.";

	public List<TreasureWindow> Windows { get; set; } = new List<TreasureWindow>();

	public static PersonalOdds Seed()
	{
		PersonalOdds personalOdds = new PersonalOdds();
		int[] array = new int[3] { 14, 21, 28 };
		foreach (int num in array)
		{
			DateTime startUtc = new DateTime(2026, 11, num, 19, 0, 0, DateTimeKind.Utc);
			personalOdds.Windows.Add(new TreasureWindow
			{
				StartUtc = startUtc,
				EndUtc = startUtc.AddHours(2.0)
			});
			startUtc = new DateTime(2026, 11, num + 1, 2, 0, 0, DateTimeKind.Utc);
			personalOdds.Windows.Add(new TreasureWindow
			{
				StartUtc = startUtc,
				EndUtc = startUtc.AddHours(2.0)
			});
		}
		return personalOdds;
	}
}
