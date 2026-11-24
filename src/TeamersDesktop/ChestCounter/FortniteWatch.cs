using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using Microsoft.Win32;

namespace ChestCounter;

[SupportedOSPlatform("windows")]
public static class FortniteWatch
{
	private const string RunKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";

	private const string RunValue = "ChestCounter";

	private static readonly string[] GameProcesses;

	public static bool IsFortniteRunning()
	{
		string[] gameProcesses = GameProcesses;
		for (int i = 0; i < gameProcesses.Length; i++)
		{
			Process[] processesByName = Process.GetProcessesByName(gameProcesses[i]);
			bool flag = processesByName.Length != 0;
			Process[] array = processesByName;
			for (int j = 0; j < array.Length; j++)
			{
				array[j].Dispose();
			}
			if (flag)
			{
				return true;
			}
		}
		return false;
	}

	public static bool Apply(bool enabled, string dataDir)
	{
		string exePath = AppInfo.ExePath;
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", writable: true);
			if (enabled)
			{
				if (exePath == null)
				{
					return false;
				}
				registryKey.SetValue("ChestCounter", "\"" + exePath + "\" --standby " + DataArg(dataDir));
				StartStandby(exePath, dataDir);
			}
			else
			{
				registryKey.DeleteValue("ChestCounter", throwOnMissingValue: false);
				StopStandby();
			}
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static string DataArg(string dataDir)
	{
		return "--data \"" + Path.GetFullPath(dataDir).TrimEnd(new char[2] { '\\', '/' }) + "\"";
	}

	public static void StartStandby(string exe, string dataDir)
	{
		if (Mutex.TryOpenExisting("Local\\ChestCounter.Standby", out Mutex result))
		{
			result.Dispose();
			return;
		}
		Process.Start(new ProcessStartInfo(exe, "--standby " + DataArg(dataDir))
		{
			UseShellExecute = false,
			CreateNoWindow = true
		})?.Dispose();
	}

	public static void StopStandby()
	{
		if (EventWaitHandle.TryOpenExisting("Local\\ChestCounter.StandbyStop", out EventWaitHandle result))
		{
			result.Set();
			result.Dispose();
		}
	}

	public static int RunStandby(string dataDir)
	{
		using Mutex mutex = new Mutex(initiallyOwned: false, "Local\\ChestCounter.Standby");
		try
		{
			if (!mutex.WaitOne(0))
			{
				return 0;
			}
		}
		catch (AbandonedMutexException)
		{
		}
		using EventWaitHandle eventWaitHandle = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, "Local\\ChestCounter.StandbyStop");
		try
		{
			Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Idle;
		}
		catch
		{
		}
		try
		{
			EcoQos.Enable();
		}
		catch
		{
		}
		GC.Collect();
		string exePath = AppInfo.ExePath;
		if (exePath == null)
		{
			return 1;
		}
		while (true)
		{
			if (eventWaitHandle.WaitOne(TimeSpan.FromSeconds(5L)))
			{
				return 0;
			}
			if (!IsFortniteRunning())
			{
				continue;
			}
			Config config = new Store(dataDir).LoadConfig();
			if (config != null && !config.StartWithFortnite)
			{
				break;
			}
			if (Mutex.TryOpenExisting(AppInfo.InstanceMutexFor(dataDir), out Mutex result))
			{
				result.Dispose();
			}
			else
			{
				try
				{
					Process.Start(new ProcessStartInfo(exePath, "--auto " + DataArg(dataDir))
					{
						UseShellExecute = false
					})?.Dispose();
				}
				catch
				{
				}
			}
			while (IsFortniteRunning())
			{
				if (eventWaitHandle.WaitOne(TimeSpan.FromSeconds(10L)))
				{
					return 0;
				}
			}
			GC.Collect();
		}
		return 0;
	}

	static FortniteWatch()
	{
		string[] array = new string[1];
		string environmentVariable = Environment.GetEnvironmentVariable("CHESTCOUNTER_GAME_PROCESS");
		array[0] = ((environmentVariable != null && environmentVariable.Length > 0) ? environmentVariable : "FortniteClient-Win64-Shipping");
		GameProcesses = array;
	}
}
