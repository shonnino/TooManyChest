using System.Collections.Generic;

namespace ChestCounter;

public static class ChestKinds
{
	public const string Normal = "Chest";

	public const string Rare = "Rare chest";

	public const string Buried = "Buried chest";

	public const string Extraction = "Extraction / relic chest";

	public const string PosterChest = "Spirit poster chest";

	public const string Other = "Other chest";

	public const string Cannon = "Loot cannon";

	public const string AmmoBox = "Ammo box";

	public const string FoodBox = "Food box";

	public const string OtherContainer = "Other container";

	private static readonly HashSet<string> ChestSet = new HashSet<string> { "Chest", "Rare chest", "Buried chest", "Extraction / relic chest", "Spirit poster chest", "Other chest" };

	public static bool IsChest(string kind)
	{
		return ChestSet.Contains(kind);
	}

	public static string Classify(string blueprint)
	{
		string text = blueprint.ToLowerInvariant();
		if (text.Contains("lootcannon") || text.Contains("cannon"))
		{
			return "Loot cannon";
		}
		if (text.Contains("ammo"))
		{
			return "Ammo box";
		}
		if (text.Contains("foodbox") || text.Contains("food_box"))
		{
			return "Food box";
		}
		if (text.Contains("rarechest") || text.Contains("legendary") || text.Contains("rare_chest"))
		{
			return "Rare chest";
		}
		if (text.Contains("buried"))
		{
			return "Buried chest";
		}
		if (text.Contains("spriteposter") || text.Contains("booty"))
		{
			return "Spirit poster chest";
		}
		if (text.Contains("relic") || text.Contains("extraction"))
		{
			return "Extraction / relic chest";
		}
		if (text.Contains("tiered_chest") || text.Contains("normalchest") || text.Contains("standardchest"))
		{
			return "Chest";
		}
		if (!text.Contains("chest"))
		{
			return "Other container";
		}
		return "Other chest";
	}
}
