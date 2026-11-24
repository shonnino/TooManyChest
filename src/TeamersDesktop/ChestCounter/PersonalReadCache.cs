using System;
using System.IO;

namespace ChestCounter;

public static class PersonalReadCache
{
	private static readonly object Gate = new object();

	private static string? path;

	private static DateTime stamp;

	private static long length = -1L;

	private static TrackerData? cached;

	public static TrackerData Load(Store store)
	{
		lock (Gate)
		{
			FileInfo fileInfo = new FileInfo(store.DataPath);
			DateTime dateTime = (fileInfo.Exists ? fileInfo.LastWriteTimeUtc : DateTime.MinValue);
			long num = (fileInfo.Exists ? fileInfo.Length : 0);
			if (cached != null && path == store.DataPath && stamp == dateTime && length == num)
			{
				return cached;
			}
			TrackerData? result = (cached = store.LoadData());
			path = store.DataPath;
			stamp = dateTime;
			length = num;
			return result;
		}
	}
}
