using System.IO;
using System.Linq;

namespace ChestCounter;

public static class ReplayHeaderProbe
{
	private const uint FileMagic = 480436863u;

	public static bool KeyMissing(string path)
	{
		try
		{
			using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
			using BinaryReader binaryReader = new BinaryReader(fileStream);
			if (binaryReader.ReadUInt32() != 480436863)
			{
				return true;
			}
			uint version = binaryReader.ReadUInt32();
			if (version >= 7)
			{
				int num = binaryReader.ReadInt32();
				if (num < 0 || num > 64)
				{
					return true;
				}
				fileStream.Seek((long)num * 20L, SeekOrigin.Current);
			}
			fileStream.Seek(12L, SeekOrigin.Current);
			int num2 = binaryReader.ReadInt32();
			fileStream.Seek((num2 < 0) ? (-2L * (long)num2) : num2, SeekOrigin.Current);
			binaryReader.ReadUInt32();
			if (version >= 3) binaryReader.ReadInt64();
			if (version >= 2) binaryReader.ReadUInt32();
			if (version < 6) return false;
			if (binaryReader.ReadUInt32() == 0)
			{
				return false;
			}
			int num3 = binaryReader.ReadInt32();
			if (num3 <= 0 || num3 > 256)
			{
				return true;
			}
			byte[] key = binaryReader.ReadBytes(num3);
			return key.Length != num3 || key.All(b => b == 0);
		}
		catch
		{
			return true;
		}
	}

	public static bool? IsLive(string path)
	{
		try
		{
			using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
			using BinaryReader binaryReader = new BinaryReader(fileStream);
			if (binaryReader.ReadUInt32() != 480436863)
			{
				return null;
			}
			if (binaryReader.ReadUInt32() >= 7)
			{
				int num = binaryReader.ReadInt32();
				if (num < 0 || num > 64)
				{
					return null;
				}
				fileStream.Seek((long)num * 20L, SeekOrigin.Current);
			}
			binaryReader.ReadUInt32();
			binaryReader.ReadUInt32();
			binaryReader.ReadUInt32();
			int num2 = binaryReader.ReadInt32();
			if (num2 < 0)
			{
				fileStream.Seek(-2L * (long)num2, SeekOrigin.Current);
			}
			else
			{
				fileStream.Seek(num2, SeekOrigin.Current);
			}
			return binaryReader.ReadUInt32() != 0;
		}
		catch
		{
			return null;
		}
	}
}
