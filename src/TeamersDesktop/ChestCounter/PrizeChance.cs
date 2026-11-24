namespace ChestCounter;

public sealed record PrizeChance(string Key, string Name, double Den, double Boost, double Chance, double Next50, bool Owned)
{
	public int? AllTimeChests { get; init; }
}
