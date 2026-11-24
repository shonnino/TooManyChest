using System;
using System.Drawing;
using System.Runtime.Versioning;

namespace ChestCounter;

[SupportedOSPlatform("windows")]
internal static class AppIcon
{
	public static Icon? Load()
	{
		try
		{
			string processPath = Environment.ProcessPath;
			return (processPath != null) ? Icon.ExtractAssociatedIcon(processPath) : null;
		}
		catch
		{
			return null;
		}
	}
}
