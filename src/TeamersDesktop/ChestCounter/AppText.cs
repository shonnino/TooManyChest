using System;
using System.Collections.Generic;
using System.Linq;

namespace ChestCounter;

internal static class AppText
{
	private static readonly string[] Codes = new string[9] { "es", "pt", "ru", "fr", "de", "it", "pl", "tr", "ja" };

	private static readonly Dictionary<string, string[]> Texts = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
	{
		["Dashboard"] = new string[9] { "Panel", "Painel", "Обзор", "Tableau de bord", "Übersicht", "Panoramica", "Panel", "Gösterge paneli", "ダッシュボード" },
		["Match history"] = new string[9] { "Historial de partidas", "Histórico de partidas", "История матчей", "Historique des parties", "Matchverlauf", "Cronologia partite", "Historia meczów", "Maç geçmişi", "マッチ履歴" },
		["Weekly chances"] = new string[9] { "Probabilidades semanales", "Chances semanais", "Недельные шансы", "Chances hebdomadaires", "Wöchentliche Chancen", "Probabilità settimanali", "Szanse tygodniowe", "Haftalık şanslar", "毎週の確率" },
		["Weekly rewards"] = new string[9] { "Recompensas semanales", "Recompensas semanais", "Недельные награды", "Récompenses hebdomadaires", "Wöchentliche Belohnungen", "Premi settimanali", "Nagrody tygodniowe", "Haftalık ödüller", "毎週の報酬" },
		["Overlay"] = new string[9] { "Superposición", "Sobreposição", "Оверлей", "Superposition", "Overlay", "Sovrapposizione", "Nakładka", "Yer paylaşımı", "オーバーレイ" },
		["Settings"] = new string[9] { "Ajustes", "Configurações", "Настройки", "Paramètres", "Einstellungen", "Impostazioni", "Ustawienia", "Ayarlar", "設定" },
		["Performance"] = new string[9] { "Rendimiento", "Desempenho", "Производительность", "Performances", "Leistung", "Prestazioni", "Wydajność", "Performans", "パフォーマンス" },
		["Your dashboard"] = new string[9] { "Tu panel", "Seu painel", "Ваш обзор", "Votre tableau de bord", "Deine Übersicht", "La tua panoramica", "Twój panel", "Gösterge paneliniz", "あなたのダッシュボード" },
		["Total personal / configured"] = new string[9] { "Total personal / configurado", "Total pessoal / configurado", "Личный / настроенный итог", "Total personnel / configuré", "Persönliche / konfigurierte Summe", "Totale personale / configurato", "Suma osobista / skonfigurowana", "Kişisel / yapılandırılmış toplam", "個人・設定込みの合計" },
		["Current / previous match"] = new string[9] { "Partida actual / anterior", "Partida atual / anterior", "Текущий / предыдущий матч", "Partie actuelle / précédente", "Aktuelles / vorheriges Match", "Partita attuale / precedente", "Bieżący / poprzedni mecz", "Mevcut / önceki maç", "現在・前回のマッチ" },
		["Today"] = new string[9] { "Hoy", "Hoje", "Сегодня", "Aujourd’hui", "Heute", "Oggi", "Dzisiaj", "Bugün", "今日" },
		["Average per match"] = new string[9] { "Media por partida", "Média por partida", "В среднем за матч", "Moyenne par partie", "Durchschnitt pro Match", "Media per partita", "Średnia na mecz", "Maç başına ortalama", "1マッチあたりの平均" },
		["Best match"] = new string[9] { "Mejor partida", "Melhor partida", "Лучший матч", "Meilleure partie", "Bestes Match", "Migliore partita", "Najlepszy mecz", "En iyi maç", "最高のマッチ" },
		["Treasures"] = new string[9] { "Tesoros", "Tesouros", "Сокровища", "Trésors", "Schätze", "Tesori", "Skarby", "Hazineler", "お宝" },
		["LAST 7 DAYS"] = new string[9] { "ÚLTIMOS 7 DÍAS", "ÚLTIMOS 7 DIAS", "ПОСЛЕДНИЕ 7 ДНЕЙ", "7 DERNIERS JOURS", "LETZTE 7 TAGE", "ULTIMI 7 GIORNI", "OSTATNIE 7 DNI", "SON 7 GÜN", "過去7日間" },
		["Refresh"] = new string[9] { "Actualizar", "Atualizar", "Обновить", "Actualiser", "Aktualisieren", "Aggiorna", "Odśwież", "Yenile", "更新" }
	};

	public static string T(string source)
	{
		int num = Array.IndexOf(Codes, L.Code);
		if (num < 0 || !Texts.TryGetValue(source, out string[] value))
		{
			return EnglishText.Fix(source);
		}
		string text = value[num];
		if (!source.Any(char.IsLetter) || !(source == source.ToUpperInvariant()))
		{
			return text;
		}
		return text.ToUpper(L.Culture);
	}
}
