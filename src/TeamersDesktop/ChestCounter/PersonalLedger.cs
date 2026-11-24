using System.Collections.Generic;

namespace ChestCounter;

public sealed class PersonalLedger
{
	public List<PersonalEntry> Entries { get; set; } = new List<PersonalEntry>();

	public List<PersonalImport> Imports { get; set; } = new List<PersonalImport>();

	public Dictionary<string, List<string>> Claimed { get; set; } = new Dictionary<string, List<string>>();
}
