using System;
using System.Collections.Generic;

namespace ChestCounter;

internal static class DashboardText
{
	private static readonly string[] Codes = new string[9] { "es", "pt", "ru", "fr", "de", "it", "pl", "tr", "ja" };

	private static readonly Dictionary<string, string[]> Texts = new Dictionary<string, string[]>(StringComparer.Ordinal)
	{
		["{0:N0} saved matches · {1} · {2}"] = new string[9] { "{0:N0} partidas guardadas · {1} · {2}", "{0:N0} partidas salvas · {1} · {2}", "Сохранено матчей: {0:N0} · {1} · {2}", "{0:N0} parties enregistrées · {1} · {2}", "{0:N0} gespeicherte Matches · {1} · {2}", "{0:N0} partite salvate · {1} · {2}", "Zapisane mecze: {0:N0} · {1} · {2}", "{0:N0} kayıtlı maç · {1} · {2}", "保存済みマッチ：{0:N0} · {1} · {2}" },
		["Last completed match   {0} chests · {1} treasures · {2} eliminations"] = new string[9] { "Última partida completada   {0} cofres · {1} tesoros · {2} eliminaciones", "Última partida concluída   {0} baús · {1} tesouros · {2} eliminações", "Последний завершённый матч   Сундуки: {0} · Сокровища: {1} · Устранения: {2}", "Dernière partie terminée   {0} coffres · {1} trésors · {2} éliminations", "Letztes abgeschlossenes Match   {0} Truhen · {1} Schätze · {2} Eliminierungen", "Ultima partita completata   {0} forzieri · {1} tesori · {2} eliminazioni", "Ostatni ukończony mecz   Skrzynie: {0} · Skarby: {1} · Eliminacje: {2}", "Son tamamlanan maç   {0} sandık · {1} hazine · {2} eleme", "直近の完了マッチ   宝箱：{0} · お宝：{1} · 撃破数：{2}" },
		["Observed squad ≥ {0}   |   You {1}   |   "] = new string[9] { "Escuadrón observado ≥ {0}   |   Tú {1}   |   ", "Equipe observada ≥ {0}   |   Você {1}   |   ", "Наблюдаемый отряд ≥ {0}   |   Вы: {1}   |   ", "Escouade observée ≥ {0}   |   Vous {1}   |   ", "Beobachteter Trupp ≥ {0}   |   Du {1}   |   ", "Squadra osservata ≥ {0}   |   Tu {1}   |   ", "Zaobserwowana drużyna ≥ {0}   |   Ty {1}   |   ", "Gözlemlenen takım ≥ {0}   |   Siz {1}   |   ", "観測したスクワッド ≥ {0}   |   あなた {1}   |   " },
		["Goal reached  {0:N0} / {1:N0} chests"] = new string[9] { "Objetivo alcanzado  {0:N0} / {1:N0} cofres", "Meta atingida  {0:N0} / {1:N0} baús", "Цель достигнута  {0:N0} / {1:N0} сундуков", "Objectif atteint  {0:N0} / {1:N0} coffres", "Ziel erreicht  {0:N0} / {1:N0} Truhen", "Obiettivo raggiunto  {0:N0} / {1:N0} forzieri", "Cel osiągnięty  {0:N0} / {1:N0} skrzyń", "Hedefe ulaşıldı  {0:N0} / {1:N0} sandık", "目標達成  宝箱 {0:N0} / {1:N0}" },
		["Chest goal   {0:N0} / {1:N0}   ·   {2:N0} to go"] = new string[9] { "Objetivo de cofres   {0:N0} / {1:N0}   ·   Faltan {2:N0}", "Meta de baús   {0:N0} / {1:N0}   ·   Faltam {2:N0}", "Цель по сундукам   {0:N0} / {1:N0}   ·   Осталось {2:N0}", "Objectif de coffres   {0:N0} / {1:N0}   ·   Encore {2:N0}", "Truhenziel   {0:N0} / {1:N0}   ·   Noch {2:N0}", "Obiettivo forzieri   {0:N0} / {1:N0}   ·   Ne mancano {2:N0}", "Cel skrzyń   {0:N0} / {1:N0}   ·   Pozostało {2:N0}", "Sandık hedefi   {0:N0} / {1:N0}   ·   Kalan {2:N0}", "宝箱の目標   {0:N0} / {1:N0}   ·   あと{2:N0}" },
		["Weekly reset: {0:ddd dd MMM HH:mm} local · {1}d {2:00}h {3:00}m {4:00}s"] = new string[9] { "Reinicio semanal: {0:ddd dd MMM HH:mm} hora local · {1}d {2:00}h {3:00}m {4:00}s", "Reinício semanal: {0:ddd dd MMM HH:mm} hora local · {1}d {2:00}h {3:00}m {4:00}s", "Недельный сброс: {0:ddd dd MMM HH:mm} местное время · {1}д {2:00}ч {3:00}м {4:00}с", "Réinitialisation hebdomadaire : {0:ddd dd MMM HH:mm} heure locale · {1}j {2:00}h {3:00}min {4:00}s", "Wöchentlicher Reset: {0:ddd dd MMM HH:mm} Ortszeit · {1}T {2:00}Std {3:00}Min {4:00}Sek", "Reset settimanale: {0:ddd dd MMM HH:mm} ora locale · {1}g {2:00}h {3:00}m {4:00}s", "Reset tygodniowy: {0:ddd dd MMM HH:mm} czas lokalny · {1}d {2:00}godz {3:00}min {4:00}s", "Haftalık sıfırlama: {0:ddd dd MMM HH:mm} yerel saat · {1}g {2:00}sa {3:00}dk {4:00}sn", "週次リセット：{0:ddd dd MMM HH:mm} 現地時間 · {1}日 {2:00}時間 {3:00}分 {4:00}秒" }
	};

	public static string F(string source, params object?[] args)
	{
		int num = Array.IndexOf(Codes, L.Code);
		string format = ((num >= 0 && Texts.TryGetValue(source, out string[] value)) ? value[num] : EnglishText.Fix(source));
		return string.Format(L.Culture, format, args);
	}
}
