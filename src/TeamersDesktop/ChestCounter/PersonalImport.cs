namespace ChestCounter;

public sealed class PersonalImport
{
	public string Hash { get; set; } = "";

	public MatchRecord Match { get; set; } = new MatchRecord();
}
