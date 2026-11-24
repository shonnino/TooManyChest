using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChestCounter;

public sealed class Store
{
	private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
	{
		WriteIndented = true,
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
	};

	public string Folder { get; }

	public string ConfigPath => Path.Combine(Folder, "config.json");

	public string DataPath => Path.Combine(Folder, "data.json");

	public string ReportPath => Path.Combine(Folder, "report.html");

	public string OverlayPath => Path.Combine(Folder, "overlay.html");

	public string CsvPath => Path.Combine(Folder, "matches.csv");

	public string PrizeNamesPath => Path.Combine(Folder, "treasures.json");

	public string ErrorLogPath => Path.Combine(Folder, "errors.log");

	public static Dictionary<string, string> DefaultPrizeNames
	{
		get
		{
			if (!(L.Code != "es"))
			{
				return new Dictionary<string, string>
				{
					["DelilahPrize:dpid_item1"] = "Guanteletes de A$AP Rocky de oro macizo (pico) ?",
					["DelilahPrize:dpid_item2"] = "Plunder in Paradise (pantalla de carga)",
					["DelilahPrize:dpid_item3"] = "Mercedes-AMG G 63 (vehículo) ?",
					["DelilahPrize:dpid_item4"] = "No Trespassing (pista musical)"
				};
			}
			return new Dictionary<string, string>
			{
				["DelilahPrize:dpid_item1"] = "A$AP Rocky solid gold gauntlets (pickaxe) ?",
				["DelilahPrize:dpid_item2"] = "Plunder in Paradise (loading screen)",
				["DelilahPrize:dpid_item3"] = "Mercedes-AMG G 63 (vehicle) ?",
				["DelilahPrize:dpid_item4"] = "No Trespassing (music track)"
			};
		}
	}

	public static string DefaultFolder()
	{
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
		string text = Path.Combine(folderPath, "ChestCounter");
		string text2 = Path.Combine(folderPath, "ChestTracker");
		try
		{
			if (!Directory.Exists(text) && File.Exists(Path.Combine(text2, "data.json")))
			{
				Directory.Move(text2, text);
			}
		}
		catch
		{
		}
		return text;
	}

	public void LoadPrizeNames()
	{
		Dictionary<string, string> dictionary = Load<Dictionary<string, string>>(PrizeNamesPath);
		if (dictionary == null)
		{
			dictionary = DefaultPrizeNames;
			Save(PrizeNamesPath, dictionary);
		}
		TreasureNames.PrizeNames.Clear();
		foreach (KeyValuePair<string, string> item in dictionary)
		{
			TreasureNames.PrizeNames[item.Key] = item.Value;
		}
	}

	public List<string> RegisterPrizeIds(IEnumerable<string> ids)
	{
		List<string> list = ids.Where((string id) => !TreasureNames.PrizeNames.ContainsKey(id)).Distinct().ToList();
		if (list.Count == 0)
		{
			return list;
		}
		Dictionary<string, string> dictionary = Load<Dictionary<string, string>>(PrizeNamesPath) ?? DefaultPrizeNames;
		foreach (string item in list)
		{
			dictionary[item] = "";
			TreasureNames.PrizeNames[item] = "";
		}
		Save(PrizeNamesPath, dictionary);
		return list;
	}

	public Store(string folder)
	{
		Folder = folder;
		PersonalIntegration.Bind(folder);
		Directory.CreateDirectory(folder);
		MigrateFileNames();
	}

	private void MigrateFileNames()
	{
		try
		{
			string text = Path.Combine(Folder, "tresors.json");
			if (File.Exists(text) && !File.Exists(PrizeNamesPath))
			{
				File.Move(text, PrizeNamesPath);
			}
			string[] array = new string[2] { "rapport.html", "parties.csv" };
			foreach (string path in array)
			{
				string path2 = Path.Combine(Folder, path);
				if (File.Exists(path2))
				{
					File.Delete(path2);
				}
			}
		}
		catch
		{
		}
	}

	public Config? LoadConfig()
	{
		return Load<Config>(ConfigPath);
	}

	public void SaveConfig(Config config)
	{
		Save(ConfigPath, config);
	}

	public TrackerData LoadData()
	{
		return Load<TrackerData>(DataPath) ?? new TrackerData
		{
			AnalyzerVersion = 13
		};
	}

	public void SaveData(TrackerData data)
	{
		Save(DataPath, data);
	}

	private static T? Load<T>(string path) where T : class
	{
		if (!File.Exists(path))
		{
			return null;
		}
		try
		{
			return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json);
		}
		catch (Exception error)
		{
			string destFileName = path + $".corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
			File.Copy(path, destFileName, overwrite: true);
			ErrorLog.Write("Archivo ilegible: " + path, error);
			return null;
		}
	}

	private static void Save<T>(string path, T value)
	{
		string text = path + ".tmp";
		File.WriteAllText(text, JsonSerializer.Serialize(value, Json));
		File.Move(text, path, overwrite: true);
	}
}
