using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace ChestCounter;

public static class Png
{
	private static readonly uint[] Crc = Enumerable.Range(0, 256).Select((int n) =>
	{
		uint num = (uint)n;
		for (int i = 0; i < 8; i++)
		{
			num = (((num & 1) != 0) ? (0xEDB88320u ^ (num >> 1)) : (num >> 1));
		}
		return num;
	}).ToArray();

	public static void Write(string path, int width, int height, byte[] bgra)
	{
		MemoryStream memoryStream = new MemoryStream();
		for (int i = 0; i < height; i++)
		{
			memoryStream.WriteByte(0);
			for (int j = 0; j < width; j++)
			{
				int num = (i * width + j) * 4;
				memoryStream.WriteByte(bgra[num + 2]);
				memoryStream.WriteByte(bgra[num + 1]);
				memoryStream.WriteByte(bgra[num]);
			}
		}
		MemoryStream memoryStream2 = new MemoryStream();
		using (ZLibStream destination = new ZLibStream(memoryStream2, CompressionLevel.Optimal, leaveOpen: true))
		{
			memoryStream.Position = 0L;
			memoryStream.CopyTo(destination);
		}
		using FileStream fileStream = File.Create(path);
		fileStream.Write(new byte[8] { 137, 80, 78, 71, 13, 10, 26, 10 });
		byte[] array = new byte[13];
		BE(array, 0, (uint)width);
		BE(array, 4, (uint)height);
		array[8] = 8;
		array[9] = 2;
		Chunk(fileStream, "IHDR", array);
		Chunk(fileStream, "IDAT", memoryStream2.ToArray());
		Chunk(fileStream, "IEND", Array.Empty<byte>());
	}

	private static void BE(byte[] b, int o, uint v)
	{
		b[o] = (byte)(v >> 24);
		b[o + 1] = (byte)(v >> 16);
		b[o + 2] = (byte)(v >> 8);
		b[o + 3] = (byte)v;
	}

	private static void Chunk(Stream s, string type, byte[] data)
	{
		byte[] array = new byte[8];
		BE(array, 0, (uint)data.Length);
		for (int i = 0; i < 4; i++)
		{
			array[4 + i] = (byte)type[i];
		}
		s.Write(array);
		s.Write(data);
		uint num = uint.MaxValue;
		foreach (byte item in array.AsSpan(4).ToArray().Concat(data))
		{
			num = Crc[(num ^ item) & 0xFF] ^ (num >> 8);
		}
		byte[] array2 = new byte[4];
		BE(array2, 0, num ^ 0xFFFFFFFFu);
		s.Write(array2);
	}
}
