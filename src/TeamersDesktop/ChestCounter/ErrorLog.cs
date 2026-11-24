using System;
using System.IO;
using System.Text;

namespace ChestCounter;

public static class ErrorLog
{
	public static string? FilePath { get; set; }

	public static void Write(string what, object error)
	{
		if (FilePath == null)
		{
			return;
		}
		try
		{
			StringBuilder stringBuilder = new StringBuilder();
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(24, 3, stringBuilder2);
			handler.AppendLiteral("==== ");
			handler.AppendFormatted(DateTime.Now, "yyyy-MM-dd HH:mm:ss");
			handler.AppendLiteral(" · ChestCounter ");
			handler.AppendFormatted(AppInfo.VersionText);
			handler.AppendLiteral(" · ");
			handler.AppendFormatted(Environment.OSVersion.VersionString);
			stringBuilder2.AppendLine(ref handler);
			stringBuilder.AppendLine(what);
			stringBuilder.AppendLine(error.ToString());
			stringBuilder.AppendLine();
			File.AppendAllText(FilePath, Anonymize(stringBuilder.ToString()), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			if (new FileInfo(FilePath).Length > 1000000)
			{
				string text = File.ReadAllText(FilePath);
				File.WriteAllText(FilePath, text.Substring(text.Length - 500000), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			}
		}
		catch
		{
		}
	}

	private static string Anonymize(string text)
	{
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		if (folderPath.Length > 3)
		{
			text = text.Replace(folderPath, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
		}
		string userName = Environment.UserName;
		if (userName.Length >= 3)
		{
			text = text.Replace(userName, "<user>", StringComparison.OrdinalIgnoreCase);
		}
		return text;
	}
}
