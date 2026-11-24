using System.Collections.Generic;

namespace ChestCounter;

public sealed class ChestRecord
{
	public float TimeSeconds { get; set; }

	public string Kind { get; set; } = "";

	public string Blueprint { get; set; } = "";

	public bool Destroyed { get; set; }

	public List<string> Items { get; set; } = new List<string>();

	public List<string> TreasureItems { get; set; } = new List<string>();
}
