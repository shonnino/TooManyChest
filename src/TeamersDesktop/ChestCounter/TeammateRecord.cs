using System.Collections.Generic;

namespace ChestCounter;

public sealed class TeammateRecord
{
	public string Name { get; set; } = "";

	public string? EpicId { get; set; }

	public int ChestsSeen { get; set; }

	public Dictionary<string, int> Kinds { get; set; } = new Dictionary<string, int>();
}
