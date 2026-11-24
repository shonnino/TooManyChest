using System;
using System.Collections.Generic;

namespace ChestCounter;

internal sealed class RankResult
{
	public Dictionary<string, int> Top { get; set; } = new Dictionary<string, int>();

	public Dictionary<string, int> Players { get; set; } = new Dictionary<string, int>();

	public DateTime AtUtc { get; set; }
}
