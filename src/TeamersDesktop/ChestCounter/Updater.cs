using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ChestCounter;

public static class Updater
{
	private const string AssetName = "ChestCounter.exe";

	private static readonly string LatestUrl = "https://api.github.com/repos/shonnino/TooManyChest/releases/latest";

	private static HttpClient Client(TimeSpan timeout)
	{
		HttpClient httpClient = new HttpClient();
		httpClient.Timeout = timeout;
		httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ChestCounter/" + AppInfo.VersionText);
		httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
		return httpClient;
	}

	public static async Task<UpdateInfo?> CheckAsync()
	{
		try
		{
			using HttpClient http = Client(TimeSpan.FromSeconds(20L));
			using JsonDocument jsonDocument = JsonDocument.Parse(await http.GetStringAsync(LatestUrl));
			JsonElement rootElement = jsonDocument.RootElement;
			if (rootElement.TryGetProperty("draft", out var value) && value.ValueKind == JsonValueKind.True)
			{
				return null;
			}
			if (rootElement.TryGetProperty("prerelease", out var value2) && value2.ValueKind == JsonValueKind.True)
			{
				return null;
			}
			string tag = rootElement.GetProperty("tag_name").GetString() ?? "";
			if (!TryParse(tag, out Version version) || version <= Current())
			{
				return null;
			}
			foreach (JsonElement item in rootElement.GetProperty("assets").EnumerateArray())
			{
				if (!string.Equals(item.GetProperty("name").GetString(), "ChestCounter.exe", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				string text = item.GetProperty("browser_download_url").GetString();
				if (string.IsNullOrEmpty(text) || !text.StartsWith("https://github.com/2xFlaw/ChestCounter/", StringComparison.OrdinalIgnoreCase))
				{
					return null;
				}
				long size = ((item.TryGetProperty("size", out var value3) && value3.TryGetInt64(out var value4)) ? value4 : 0);
				string sha = null;
				if (item.TryGetProperty("digest", out var value5))
				{
					string text2 = value5.GetString();
					if (text2 != null && text2.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
					{
						sha = text2.Substring(7).Trim().ToLowerInvariant();
					}
				}
				string pageUrl = (rootElement.TryGetProperty("html_url", out var value6) ? (value6.GetString() ?? (AppInfo.RepoUrl + "/releases")) : (AppInfo.RepoUrl + "/releases"));
				return new UpdateInfo(version, tag, text, size, sha, pageUrl);
			}
		}
		catch (Exception ex)
		{
			ErrorLog.Write("Update check", ex.GetType().Name + ": " + ex.Message + ((ex.InnerException != null) ? (" / " + ex.GetBaseException().Message) : ""));
		}
		return null;
	}

	public static string? Install(UpdateInfo update)
	{
		string exePath = AppInfo.ExePath;
		if (exePath == null)
		{
			return L.T("renombra el programa a ChestCounter.exe para poder actualizarlo", "rename the program to ChestCounter.exe so it can update itself");
		}
		string directoryName = Path.GetDirectoryName(exePath);
		string text = Path.Combine(directoryName, "ChestCounter.new.exe");
		string text2 = Path.Combine(directoryName, "ChestCounter.old.exe");
		try
		{
			TryDelete(text);
			using (HttpClient httpClient = Client(TimeSpan.FromMinutes(10L)))
			{
				using HttpResponseMessage httpResponseMessage = httpClient.GetAsync(update.AssetUrl, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
				httpResponseMessage.EnsureSuccessStatusCode();
				using Stream stream = httpResponseMessage.Content.ReadAsStream();
				using FileStream destination = new FileStream(text, FileMode.Create, FileAccess.Write, FileShare.None);
				stream.CopyTo(destination);
			}
			long length = new FileInfo(text).Length;
			if (length < 1000000 || (update.Size > 0 && length != update.Size))
			{
				TryDelete(text);
				return L.T("la descarga está incompleta", "the download is incomplete");
			}
			if (update.Sha256 != null)
			{
				using FileStream fileStream = File.OpenRead(text);
				if (Convert.ToHexString(SHA256.HashData(fileStream)).ToLowerInvariant() != update.Sha256)
				{
					fileStream.Dispose();
					TryDelete(text);
					return L.T("la descarga no coincide con la publicada", "the download does not match the release");
				}
			}
			TryDelete(text2);
			if (File.Exists(text2))
			{
				text2 = Path.Combine(directoryName, $"ChestCounter.old-{DateTime.UtcNow:yyyyMMddHHmmss}.exe");
			}
			File.Move(exePath, text2);
			try
			{
				File.Move(text, exePath);
			}
			catch
			{
				File.Move(text2, exePath);
				throw;
			}
			return null;
		}
		catch (Exception ex)
		{
			TryDelete(text);
			ErrorLog.Write("Update install", ex);
			return ex.Message;
		}
	}

	public static void CleanupOld()
	{
		string exePath = AppInfo.ExePath;
		if (exePath == null)
		{
			return;
		}
		string dir = Path.GetDirectoryName(exePath);
		Thread thread = new Thread(() =>
		{
			for (int i = 0; i < 15; i++)
			{
				int num = 0;
				try
				{
					foreach (string item in Directory.EnumerateFiles(dir, "ChestCounter.old*.exe").Append(Path.Combine(dir, "ChestCounter.new.exe")))
					{
						if (File.Exists(item) && !TryDelete(item))
						{
							num++;
						}
					}
				}
				catch
				{
					break;
				}
				if (num == 0)
				{
					break;
				}
				Thread.Sleep(1000);
			}
		});
		thread.IsBackground = true;
		thread.Name = "Limpieza de actualización";
		thread.Start();
	}

	private static Version Current()
	{
		return new Version(AppInfo.Version.Major, AppInfo.Version.Minor, Math.Max(0, AppInfo.Version.Build));
	}

	private static bool TryParse(string tag, out Version version)
	{
		version = new Version(0, 0, 0);
		string text = tag.Trim().TrimStart(new char[2] { 'v', 'V' });
		int num = text.IndexOfAny(new char[3] { '-', '+', ' ' });
		if (num >= 0)
		{
			text = text.Substring(0, num);
		}
		if (!Version.TryParse(text, out Version result))
		{
			return false;
		}
		version = new Version(result.Major, result.Minor, Math.Max(0, result.Build));
		return true;
	}

	private static bool TryDelete(string path)
	{
		try
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
			return true;
		}
		catch
		{
			return false;
		}
	}
}
