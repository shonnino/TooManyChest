using System;

namespace ChestCounter;

public sealed class ProgressBar
{
	private readonly string _label;

	private readonly int _total;

	private int _lastLength;

	public ProgressBar(string label, int total)
	{
		_label = label;
		_total = Math.Max(1, total);
	}

	public void Update(int done, string? detail = null)
	{
		int num = (int)Math.Round((double)(28 * Math.Clamp(done, 0, _total)) / (double)_total);
		string text = new string('█', num) + new string('░', 28 - num);
		string value = $"{done} / {_total}";
		if (!ConsoleUi.Interactive)
		{
			return;
		}
		int num2;
		try
		{
			num2 = Console.WindowWidth;
		}
		catch
		{
			num2 = 80;
		}
		string text2 = $"  {_label}  {text}  {value}";
		if (!string.IsNullOrEmpty(detail))
		{
			int num3 = num2 - text2.Length - 4;
			if (num3 > 8)
			{
				string text3 = text2;
				string text4;
				if (detail.Length <= num3)
				{
					text4 = detail;
				}
				else
				{
					int num4 = num3 - 1;
					text4 = "…" + detail.Substring(detail.Length - num4);
				}
				text2 = text3 + "  " + text4;
			}
		}
		Console.Write("\r");
		Console.ForegroundColor = ConsoleColor.Cyan;
		Console.Write("  " + _label + "  ");
		Console.ForegroundColor = ConsoleColor.Yellow;
		Console.Write(text);
		Console.ForegroundColor = ConsoleColor.Gray;
		Console.Write(text2.Substring(4 + _label.Length + text.Length));
		Console.ResetColor();
		if (text2.Length < _lastLength)
		{
			Console.Write(new string(' ', _lastLength - text2.Length));
		}
		_lastLength = text2.Length;
	}

	public void Clear()
	{
		if (ConsoleUi.Interactive && _lastLength != 0)
		{
			Console.Write("\r" + new string(' ', _lastLength) + "\r");
			_lastLength = 0;
		}
	}
}
