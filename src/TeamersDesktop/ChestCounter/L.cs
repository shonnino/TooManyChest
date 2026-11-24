using System;
using System.Collections.Generic;
using System.Globalization;

namespace ChestCounter;

public static class L
{
	private static readonly string[] Supported = new string[10] { "es", "en", "pt", "ru", "fr", "de", "it", "pl", "tr", "ja" };

	private static readonly Dictionary<string, string> Cultures = new Dictionary<string, string>
	{
		["es"] = "es-ES",
		["en"] = "en-US",
		["pt"] = "pt-BR",
		["ru"] = "ru-RU",
		["fr"] = "fr-FR",
		["de"] = "de-DE",
		["it"] = "it-IT",
		["pl"] = "pl-PL",
		["tr"] = "tr-TR",
		["ja"] = "ja-JP"
	};

	private static readonly Dictionary<string, (string Es, string En)> Names = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
	{
		["Chest"] = ("Cofre", "Chest"),
		["Rare chest"] = ("Cofre raro", "Rare chest"),
		["Buried chest"] = ("Cofre enterrado", "Buried chest"),
		["Extraction / relic chest"] = ("Cofre de extracción / reliquias", "Extraction / relic chest"),
		["Spirit poster chest"] = ("Cofre de los carteles de espíritu", "Spirit poster chest"),
		["Other chest"] = ("Otro cofre", "Other chest"),
		["Loot cannon"] = ("Cañón de botín", "Loot cannon"),
		["Ammo box"] = ("Caja de munición", "Ammo box"),
		["Food box"] = ("Caja de comida", "Food box"),
		["Other container"] = ("Otro contenedor", "Other container"),
		["Treasure"] = ("Tesoro", "Treasure"),
		["Treasure (unknown type)"] = ("Tesoro (tipo desconocido)", "Treasure (unknown type)"),
		["Treasure gizmo"] = ("Artilugio de tesoro", "Treasure gizmo"),
		["Golden firework"] = ("Fuego artificial dorado", "Golden firework"),
		["Portable extractor"] = ("Extractor portátil", "Portable extractor"),
		["Spirit locator"] = ("Localizador de espíritu", "Spirit locator"),
		["Lucky locator"] = ("Localizador afortunado", "Lucky locator"),
		["Extraction accelerator"] = ("Acelerador de extracción", "Extraction accelerator"),
		["unknown"] = ("desconocido", "unknown")
	};

	private static readonly Dictionary<string, string> Legacy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		["Coffre"] = "Chest",
		["Coffre rare"] = "Rare chest",
		["Coffre enterré"] = "Buried chest",
		["Coffre d'extraction / reliques"] = "Extraction / relic chest",
		["Coffre des affiches d'esprit"] = "Spirit poster chest",
		["Autre coffre"] = "Other chest",
		["Canon à butin"] = "Loot cannon",
		["Caisse de munitions"] = "Ammo box",
		["Caisse de nourriture"] = "Food box",
		["Autre conteneur"] = "Other container",
		["Trésor"] = "Treasure",
		["Trésor (type inconnu)"] = "Treasure (unknown type)",
		["Gizmo de trésor"] = "Treasure gizmo",
		["Feu d'artifice doré"] = "Golden firework",
		["Extracteur portable"] = "Portable extractor",
		["Localisateur d'esprit"] = "Spirit locator",
		["Localisateur chanceux"] = "Lucky locator",
		["Accélérateur d'extraction"] = "Extraction accelerator",
		["inconnu"] = "unknown"
	};

	public static IReadOnlyList<(string Code, string Name)> Languages { get; } = new _003C_003Ez__ReadOnlyArray<(string, string)>(new (string, string)[10]
	{
		("es", "Español"),
		("en", "English"),
		("pt", "Português (Brasil)"),
		("ru", "Русский"),
		("fr", "Français"),
		("de", "Deutsch"),
		("it", "Italiano"),
		("pl", "Polski"),
		("tr", "Türkçe"),
		("ja", "日本語")
	});

	public static string Code { get; private set; } = Detect();

	private static int Slot = Array.IndexOf(Supported, Code) - 2;

	public static bool En => Code == "en";

	public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo(Cultures[Code]);

	public static string SystemCode => Detect();

	public static void Use(string? code)
	{
		string environmentVariable = Environment.GetEnvironmentVariable("CHESTCOUNTER_LANG");
		string text = (Code = (((environmentVariable != null && environmentVariable.Length > 0) || code == null || Array.IndexOf(Supported, code) < 0) ? Detect() : code));
		Culture = CultureInfo.GetCultureInfo(Cultures[text]);
		Slot = Array.IndexOf(Supported, text) - 2;
	}

	private static string Detect()
	{
		string environmentVariable = Environment.GetEnvironmentVariable("CHESTCOUNTER_LANG");
		string text = ((environmentVariable != null && environmentVariable.Length > 0) ? environmentVariable : "en");
		text = ((text.Length >= 2) ? text.Substring(0, 2).ToLowerInvariant() : "");
		if (Array.IndexOf(Supported, text) < 0)
		{
			return "en";
		}
		return text;
	}

	public static string T(string es, string en)
	{
		return Pick(es, es, en);
	}

	public static string Big(string id, string es, string en)
	{
		return Pick("#" + id, es, en);
	}

	public static string F(string es, string en, params object?[] args)
	{
		return string.Format(Culture, T(es, en), args);
	}

	private static string Pick(string key, string es, string en)
	{
		if (Slot == -2)
		{
			return es;
		}
		if (Slot == -1)
		{
			return EnglishText.Fix(en);
		}
		if (Translations.Table.TryGetValue(key, out string[] value))
		{
			string text = value[Slot];
			if (text != null && text.Length > 0)
			{
				return text;
			}
		}
		return en;
	}

	public static string N(int n)
	{
		return n.ToString("0", Culture);
	}

	public static string Count(int n, string esOne, string esMany, string enOne, string enMany)
	{
		return N(n) + " " + ((n == 1) ? T(esOne, enOne) : T(esMany, enMany));
	}

	public static string Ordinal(int n)
	{
		switch (Code)
		{
		case "es":
			return $"{n}.º";
		case "pt":
		case "it":
			return $"{n}º";
		case "fr":
			return (n == 1) ? "1er" : $"{n}e";
		case "ja":
			return $"{n}位";
		case "en":
			return n + EnglishSuffix(n);
		default:
			return $"{n}.";
		}
	}

	private static string EnglishSuffix(int n)
	{
		int num = n % 100;
		if ((uint)(num - 11) <= 2u)
		{
			return "th";
		}
		return (n % 10) switch
		{
			1 => "st",
			2 => "nd",
			3 => "rd",
			_ => "th",
		};
	}

	public static string DateTimeShort(DateTime d)
	{
		return d.ToString(En ? "d MMM HH:mm" : "dd/MM HH:mm", Culture);
	}

	public static string Upgrade(string stored)
	{
		if (!Legacy.TryGetValue(stored, out string value))
		{
			return stored;
		}
		return value;
	}

	public static string Name(string? stored)
	{
		if (string.IsNullOrEmpty(stored))
		{
			return stored ?? "";
		}
		if (!Names.TryGetValue(Upgrade(stored), out (string, string) value))
		{
			return stored;
		}
		return T(value.Item1, value.Item2);
	}
}
