using System;

namespace ChestCounter;

public sealed class TreasureWindow
{
	public DateTime StartUtc { get; set; }

	public DateTime EndUtc { get; set; }

	public double Gold { get; set; } = 7.5;

	public double Shop { get; set; } = 1.5;

	public double Vehicle { get; set; } = 10.0;
}
