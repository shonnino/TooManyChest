using System;
using System.Collections.Generic;

namespace ChestCounter;

public sealed class OnlineSettings
{
	public bool Enabled { get; set; }

	public string? Token { get; set; }

	public string ApiBase { get; set; } = "https://chestcounter.io";

	public DateTime? SinceUtc { get; set; }

	public List<string> Synced { get; set; } = new List<string>();

	public DateTime? LastSyncUtc { get; set; }

	public string? LastError { get; set; }

	public int ExtrasVersion { get; set; }

	public DateTime? ExtrasCursorUtc { get; set; }
}
