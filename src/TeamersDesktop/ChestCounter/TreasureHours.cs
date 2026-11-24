using System;

namespace ChestCounter;

public static class TreasureHours
{
	public static readonly DateTime UnconfirmedFromUtc = new DateTime(2100, 1, 1, 0, 0, 0, DateTimeKind.Utc);

	public static bool IsUnconfirmed(TreasureWindow w)
	{
		return w.StartUtc >= UnconfirmedFromUtc;
	}

	public static double Boost(TreasureWindow? w, string key)
	{
		if (w == null || IsUnconfirmed(w))
		{
			return 1.0;
		}
		return key switch
		{
			"gold-outfit" => w.Gold,
			"pirate-shop-access" => w.Shop,
			"vehicle-decal" => w.Vehicle,
			_ => 1.0,
		};
	}
}
