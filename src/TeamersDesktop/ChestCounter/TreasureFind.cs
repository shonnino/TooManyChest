using System.Collections.Generic;

namespace ChestCounter;

public sealed class TreasureFind
{
	public string DisplayName
	{
		get
		{
			if (PrizeId == null)
			{
				return L.Name(Item);
			}
			return TreasureNames.Friendly(PrizeId);
		}
	}

	public string Item { get; set; } = "";

	public string RawItem { get; set; } = "";

	public string? PrizeId { get; set; }

	public bool FromGoldenChest { get; set; }

	public bool FromPrizeHandler { get; set; }

	public bool TimeExact { get; set; }

	public bool SourceDestroyed { get; set; }

	public string SourceLabel
	{
		get
		{
			if (!SourceDestroyed)
			{
				return L.Name(SourceKind);
			}
			return L.Name(SourceKind) + " (" + L.T("destruido", "destroyed") + ")";
		}
	}

	public float? ObtainedAtSeconds { get; set; }

	public string SourceKind { get; set; } = "";

	public string SourceBlueprint { get; set; } = "";

	public bool SourceIsChest { get; set; } = true;

	public bool SourceCertain { get; set; }

	public List<string> AlsoPossible { get; set; } = new List<string>();
}
