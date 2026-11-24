using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ChestCounter;

public static class AppInfo
{
	public const string Repo = "2xFlaw/ChestCounter";

	public const string StandbyMutex = "Local\\ChestCounter.Standby";

	public const string StandbyStopEvent = "Local\\ChestCounter.StandbyStop";

	public static Version Version { get; } = typeof(AppInfo).Assembly.GetName().Version ?? new Version(1, 0, 0);

	public static string VersionText => $"{Version.Major}.{Version.Minor}.{Math.Max(0, Version.Build)}";

	public static string RepoUrl => "https://github.com/2xFlaw/ChestCounter";

	public static string IssuesUrl => RepoUrl + "/issues";

	public static string? ExePath
	{
		get
		{
			string processPath = Environment.ProcessPath;
			if (processPath == null || !Path.GetFileName(processPath).Equals("ChestCounter.exe", StringComparison.OrdinalIgnoreCase))
			{
				return null;
			}
			return processPath;
		}
	}

	public static string InstanceMutexFor(string dataDir)
	{
		byte[] inArray = SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(dataDir).TrimEnd(new char[2] { '\\', '/' }).ToLowerInvariant()));
		return "Local\\ChestCounter.Instance." + Convert.ToHexString(inArray, 0, 8);
	}
}
