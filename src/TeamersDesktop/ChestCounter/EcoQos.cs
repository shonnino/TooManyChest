using System.Runtime.InteropServices;

namespace ChestCounter;

internal static class EcoQos
{
	private struct PROCESS_POWER_THROTTLING_STATE
	{
		public uint Version;

		public uint ControlMask;

		public uint StateMask;
	}

	private const int ProcessPowerThrottling = 4;

	private const uint PROCESS_POWER_THROTTLING_CURRENT_VERSION = 1u;

	private const uint PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 1u;

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool SetProcessInformation(nint process, int infoClass, ref PROCESS_POWER_THROTTLING_STATE info, uint size);

	[DllImport("kernel32.dll")]
	private static extern nint GetCurrentProcess();

	public static void Enable()
	{
		PROCESS_POWER_THROTTLING_STATE info = new PROCESS_POWER_THROTTLING_STATE
		{
			Version = 1u,
			ControlMask = 1u,
			StateMask = 1u
		};
		SetProcessInformation(GetCurrentProcess(), 4, ref info, (uint)Marshal.SizeOf<PROCESS_POWER_THROTTLING_STATE>());
	}
}
