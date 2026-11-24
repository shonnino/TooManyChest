using System;

namespace ChestCounter;

public static class ConsoleUi
{
	private static bool _identified;

	public static bool Interactive { get; } = !Console.IsInputRedirected && !Console.IsOutputRedirected;

	public static void Text(string text, ConsoleColor? color = null)
	{
		if (!_identified)
		{
			Console.WriteLine("2xFlaw's Chest Counter | Replay tools | Weekly reset: Sunday 09:00 GMT");
			_identified = true;
		}
		if (color.HasValue)
		{
			ConsoleColor valueOrDefault = color.GetValueOrDefault();
			Console.ForegroundColor = valueOrDefault;
		}
		string[] array = text.Split('\n');
		foreach (string text2 in array)
		{
			Console.WriteLine("  " + text2);
		}
		if (color.HasValue)
		{
			Console.ResetColor();
		}
	}

	public static void Hint(string text)
	{
		Text(text, ConsoleColor.DarkGray);
	}
}
