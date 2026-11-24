using System;

namespace ChestCounter;

public sealed class PersonalEntry
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public DateTime AtUtc { get; set; } = DateTime.UtcNow;

	public string Profile { get; set; } = "";

	public int Mine { get; set; }

	public int Squad { get; set; }

	public string Note { get; set; } = "";
}
