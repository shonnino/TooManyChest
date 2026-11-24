using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ChestCounter;

[SupportedOSPlatform("windows")]
public static class NativeConsole
{
	private const int STD_OUTPUT_HANDLE = -11;

	private const int ATTACH_PARENT_PROCESS = -1;

	private const uint FILE_TYPE_DISK = 1u;

	private const uint FILE_TYPE_PIPE = 3u;

	private const int SW_SHOWMINNOACTIVE = 7;

	private const int SW_HIDE = 0;

	public static void Ensure(bool attachToParent)
	{
		if (GetConsoleWindow() == IntPtr.Zero && !IsRedirected(-11) && (!attachToParent || !AttachConsole(-1)))
		{
			AllocConsole();
		}
	}

	public static void Hide()
	{
		nint consoleWindow = GetConsoleWindow();
		if (consoleWindow != IntPtr.Zero)
		{
			ShowWindow(consoleWindow, 0);
		}
	}

	public static void Minimize()
	{
		nint consoleWindow = GetConsoleWindow();
		if (consoleWindow != IntPtr.Zero)
		{
			ShowWindow(consoleWindow, 7);
		}
	}

	private static bool IsRedirected(int std)
	{
		nint stdHandle = GetStdHandle(std);
		if (stdHandle == IntPtr.Zero || stdHandle == new IntPtr(-1))
		{
			return false;
		}
		uint fileType = GetFileType(stdHandle);
		if (fileType == 1 || fileType == 3)
		{
			return true;
		}
		return false;
	}

	[DllImport("kernel32.dll")]
	private static extern nint GetConsoleWindow();

	[DllImport("kernel32.dll")]
	private static extern bool AllocConsole();

	[DllImport("kernel32.dll")]
	private static extern bool AttachConsole(int processId);

	[DllImport("kernel32.dll")]
	private static extern nint GetStdHandle(int std);

	[DllImport("kernel32.dll")]
	private static extern uint GetFileType(nint handle);

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(nint hwnd, int cmd);
}
