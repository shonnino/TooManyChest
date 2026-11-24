using System;
using System.Collections.Generic;

namespace ChestCounter;

internal static class StatsText
{
	private static readonly string[] Codes = new string[9] { "es", "pt", "ru", "fr", "de", "it", "pl", "tr", "ja" };

	private static readonly Dictionary<string, string[]> Texts = new Dictionary<string, string[]>(StringComparer.Ordinal)
	{
		["Just you"] = new string[9] { "Solo tú", "Só você", "Только вы", "Vous seul", "Nur du", "Solo tu", "Tylko ty", "Sadece siz", "自分のみ" },
		["With teammates"] = new string[9] { "Con compañeros", "Com a equipe", "С союзниками", "Avec coéquipiers", "Mit Team", "Con compagni", "Z drużyną", "Takımla", "チームメイト込み" },
		["By you"] = new string[9] { "Por ti", "Por você", "Ваши", "Par vous", "Von dir", "Da te", "Twoje", "Sizin", "自分" },
		["{0:N0} opened · {1:N0} broken"] = new string[9] { "{0:N0} abiertos · {1:N0} rotos", "{0:N0} abertos · {1:N0} quebrados", "Открыто: {0:N0} · Сломано: {1:N0}", "{0:N0} ouverts · {1:N0} cassés", "{0:N0} geöffnet · {1:N0} zerstört", "{0:N0} aperti · {1:N0} distrutti", "Otwarte: {0:N0} · Zniszczone: {1:N0}", "{0:N0} açıldı · {1:N0} kırıldı", "開封 {0:N0} · 破壊 {1:N0}" },
		["Teammate chests"] = new string[9] { "Cofres de compañeros", "Baús da equipe", "Сундуки союзников", "Coffres des coéquipiers", "Truhen des Teams", "Forzieri dei compagni", "Skrzynie drużyny", "Takım sandıkları", "チームメイトの宝箱" },
		["seen near you (at least)"] = new string[9] { "vistos cerca de ti (como mínimo)", "vistos perto de você (no mínimo)", "замечены рядом с вами (минимум)", "vus près de vous (au minimum)", "in deiner Nähe gesehen (mindestens)", "visti vicino a te (almeno)", "widziane w pobliżu (co najmniej)", "yakınında görüldü (en az)", "近くで確認（最低値）" },
		["Per match"] = new string[9] { "Por partida", "Por partida", "За матч", "Par partie", "Pro Match", "Per partita", "Na mecz", "Maç başına", "1マッチあたり" },
		["average"] = new string[9] { "media", "média", "в среднем", "moyenne", "Durchschnitt", "media", "średnio", "ortalama", "平均" },
		["Best match"] = new string[9] { "Mejor partida", "Melhor partida", "Лучший матч", "Meilleure partie", "Bestes Match", "Migliore partita", "Najlepszy mecz", "En iyi maç", "最高のマッチ" },
		["Per minute"] = new string[9] { "Por minuto", "Por minuto", "В минуту", "Par minute", "Pro Minute", "Al minuto", "Na minutę", "Dakika başına", "1分あたり" },
		["of match time"] = new string[9] { "de tiempo de partida", "de tempo de partida", "времени матча", "de temps de partie", "Matchzeit", "di tempo di gioco", "czasu meczu", "maç süresi", "試合時間あたり" },
		["Chests per match"] = new string[9] { "Cofres por partida", "Baús por partida", "Сундуки за матч", "Coffres par partie", "Truhen pro Match", "Forzieri per partita", "Skrzynie na mecz", "Maç başına sandık", "マッチごとの宝箱" },
		["Chests per day"] = new string[9] { "Cofres por día", "Baús por dia", "Сундуки за день", "Coffres par jour", "Truhen pro Tag", "Forzieri al giorno", "Skrzynie dziennie", "Günlük sandık", "日ごとの宝箱" },
		["Per day"] = new string[9] { "Por día", "Por dia", "За день", "Par jour", "Pro Tag", "Al giorno", "Dziennie", "Günlük", "1日あたり" },
		["No matches yet. They appear here once a replay has been analysed."] = new string[9] { "Aún no hay partidas. Aparecerán aquí cuando se analice una repetición.", "Ainda não há partidas. Elas aparecem aqui depois que um replay for analisado.", "Матчей пока нет. Они появятся здесь после анализа повтора.", "Aucune partie pour l’instant. Elles apparaissent ici après l’analyse d’un replay.", "Noch keine Matches. Sie erscheinen hier, sobald eine Wiederholung analysiert wurde.", "Ancora nessuna partita. Appariranno qui dopo l’analisi di un replay.", "Brak meczów. Pojawią się tu po przeanalizowaniu powtórki.", "Henüz maç yok. Bir tekrar analiz edildiğinde burada görünür.", "まだマッチがありません。リプレイが解析されるとここに表示されます。" },
		["Oldest to newest. The line marks your average of {0:0.0} per match."] = new string[9] { "De más antigua a más reciente. La línea marca tu media de {0:0.0} por partida.", "Da mais antiga para a mais recente. A linha marca sua média de {0:0.0} por partida.", "От старых к новым. Линия — ваше среднее: {0:0.0} за матч.", "De la plus ancienne à la plus récente. La ligne indique votre moyenne de {0:0.0} par partie.", "Von alt nach neu. Die Linie zeigt deinen Schnitt von {0:0.0} pro Match.", "Dalla più vecchia alla più recente. La linea indica la tua media di {0:0.0} per partita.", "Od najstarszych do najnowszych. Linia to twoja średnia: {0:0.0} na mecz.", "Eskiden yeniye. Çizgi maç başına {0:0.0} ortalamanızı gösterir.", "古い順。線は1マッチあたりの平均 {0:0.0} を示します。" },
		["Oldest to newest. The line marks your average of {0:0.0} per day."] = new string[9] { "De más antiguo a más reciente. La línea marca tu media de {0:0.0} por día.", "Do mais antigo para o mais recente. A linha marca sua média de {0:0.0} por dia.", "От старых к новым. Линия — ваше среднее: {0:0.0} за день.", "Du plus ancien au plus récent. La ligne indique votre moyenne de {0:0.0} par jour.", "Von alt nach neu. Die Linie zeigt deinen Schnitt von {0:0.0} pro Tag.", "Dal più vecchio al più recente. La linea indica la tua media di {0:0.0} al giorno.", "Od najstarszych do najnowszych. Linia to twoja średnia: {0:0.0} dziennie.", "Eskiden yeniye. Çizgi günlük {0:0.0} ortalamanızı gösterir.", "古い順。線は1日あたりの平均 {0:0.0} を示します。" },
		["You"] = new string[9] { "Tú", "Você", "Вы", "Vous", "Du", "Tu", "Ty", "Siz", "自分" },
		["Teammates"] = new string[9] { "Compañeros", "Equipe", "Союзники", "Coéquipiers", "Team", "Compagni", "Drużyna", "Takım arkadaşları", "チームメイト" },
		["Showing the latest {0:N0} of {1:N0}"] = new string[9] { "Mostrando las últimas {0:N0} de {1:N0}", "Mostrando as últimas {0:N0} de {1:N0}", "Показаны последние {0:N0} из {1:N0}", "Affichage des {0:N0} dernières sur {1:N0}", "Die letzten {0:N0} von {1:N0}", "Ultime {0:N0} di {1:N0}", "Ostatnie {0:N0} z {1:N0}", "Son {0:N0} / {1:N0} gösteriliyor", "最新 {0:N0} / {1:N0} 件を表示" },
		["Opened"] = new string[9] { "Abiertos", "Abertos", "Открыто", "Ouverts", "Geöffnet", "Aperti", "Otwarte", "Açılan", "開封" },
		["Broken"] = new string[9] { "Rotos", "Quebrados", "Сломано", "Cassés", "Zerstört", "Distrutti", "Zniszczone", "Kırılan", "破壊" },
		["Total"] = new string[9] { "Total", "Total", "Всего", "Total", "Gesamt", "Totale", "Razem", "Toplam", "合計" },
		["Avg {0:0.0}"] = new string[9] { "Media {0:0.0}", "Média {0:0.0}", "Среднее {0:0.0}", "Moy. {0:0.0}", "Ø {0:0.0}", "Media {0:0.0}", "Śr. {0:0.0}", "Ort. {0:0.0}", "平均 {0:0.0}" },
		["You're in the top {0}%"] = new string[9] { "Estás en el {0}% mejor", "Você está entre os {0}% melhores", "Вы входите в топ-{0}%", "Vous êtes dans le top {0} %", "Du bist in den Top {0} %", "Sei nel top {0}%", "Jesteś w czołowych {0}%", "İlk %{0} içindesiniz", "上位{0}%に入っています" },
		["Not enough players yet"] = new string[9] { "Aún no hay suficientes jugadores", "Ainda não há jogadores suficientes", "Пока недостаточно игроков", "Pas encore assez de joueurs", "Noch nicht genug Spieler", "Non ci sono ancora abbastanza giocatori", "Za mało graczy", "Henüz yeterli oyuncu yok", "まだプレイヤーが足りません" },
		["Player rankings"] = new string[9] { "Clasificación de jugadores", "Ranking de jogadores", "Рейтинг игроков", "Classement des joueurs", "Spielerrangliste", "Classifica giocatori", "Ranking graczy", "Oyuncu sıralaması", "プレイヤーランキング" },
		["{0:N0} players"] = new string[9] { "{0:N0} jugadores", "{0:N0} jogadores", "Игроков: {0:N0}", "{0:N0} joueurs", "{0:N0} Spieler", "{0:N0} giocatori", "Graczy: {0:N0}", "{0:N0} oyuncu", "{0:N0}人のプレイヤー" },
		["Off"] = new string[9] { "Desactivada", "Desativado", "Выкл.", "Désactivé", "Aus", "Disattivata", "Wyłączony", "Kapalı", "オフ" },
		["Connecting…"] = new string[9] { "Conectando…", "Conectando…", "Подключение…", "Connexion…", "Verbinden…", "Connessione…", "Łączenie…", "Bağlanıyor…", "接続中…" },
		["Chests per minute"] = new string[9] { "Cofres por minuto", "Baús por minuto", "Сундуки в минуту", "Coffres par minute", "Truhen pro Minute", "Forzieri al minuto", "Skrzynie na minutę", "Dakika başına sandık", "1分あたりの宝箱" },
		["Oldest to newest, only matches with a known length. The line marks your average of {0:0.00} per minute."] = new string[9] { "De más antigua a más reciente, solo partidas con duración conocida. La línea marca tu media de {0:0.00} por minuto.", "Da mais antiga para a mais recente, só partidas com duração conhecida. A linha marca sua média de {0:0.00} por minuto.", "От старых к новым, только матчи с известной длительностью. Линия — ваше среднее: {0:0.00} в минуту.", "De la plus ancienne à la plus récente, parties à durée connue uniquement. La ligne indique votre moyenne de {0:0.00} par minute.", "Von alt nach neu, nur Matches mit bekannter Dauer. Die Linie zeigt deinen Schnitt von {0:0.00} pro Minute.", "Dalla più vecchia alla più recente, solo partite di durata nota. La linea indica la tua media di {0:0.00} al minuto.", "Od najstarszych do najnowszych, tylko mecze o znanej długości. Linia to twoja średnia: {0:0.00} na minutę.", "Eskiden yeniye, yalnızca süresi bilinen maçlar. Çizgi dakika başına {0:0.00} ortalamanızı gösterir.", "古い順（試合時間がわかるマッチのみ）。線は1分あたりの平均 {0:0.00} を示します。" },
		["Bright bars are the matches that beat your average of {0:0.0}."] = new string[9] { "Las barras brillantes son las partidas que superan tu media de {0:0.0}.", "As barras claras são as partidas que superam sua média de {0:0.0}.", "Яркие столбцы — матчи выше вашего среднего ({0:0.0}).", "Les barres vives sont les parties qui dépassent votre moyenne de {0:0.0}.", "Helle Balken sind Matches über deinem Schnitt von {0:0.0}.", "Le barre luminose sono le partite che superano la tua media di {0:0.0}.", "Jasne słupki to mecze powyżej twojej średniej ({0:0.0}).", "Parlak çubuklar {0:0.0} ortalamanızı geçen maçlardır.", "明るいバーは平均 {0:0.0} を上回ったマッチです。" },
		["Your best match is pinned. Click the tile again to unpin it."] = new string[9] { "Tu mejor partida está fijada. Vuelve a hacer clic en el recuadro para quitarla.", "Sua melhor partida está fixada. Clique no bloco de novo para desafixar.", "Ваш лучший матч закреплён. Нажмите на плитку ещё раз, чтобы открепить.", "Votre meilleure partie est épinglée. Cliquez de nouveau sur la tuile pour la désépingler.", "Dein bestes Match ist angeheftet. Klicke erneut auf die Kachel, um es zu lösen.", "La tua migliore partita è fissata. Fai di nuovo clic sul riquadro per sbloccarla.", "Twój najlepszy mecz jest przypięty. Kliknij kafelek ponownie, aby go odpiąć.", "En iyi maçınız sabitlendi. Kaldırmak için kutucuğa tekrar tıklayın.", "最高のマッチを固定しています。もう一度タイルをクリックすると解除されます。" },
		["Showing {0:N0} of {1:N0}"] = new string[9] { "Mostrando {0:N0} de {1:N0}", "Mostrando {0:N0} de {1:N0}", "Показано {0:N0} из {1:N0}", "Affichage de {0:N0} sur {1:N0}", "{0:N0} von {1:N0}", "{0:N0} di {1:N0}", "{0:N0} z {1:N0}", "{0:N0} / {1:N0} gösteriliyor", "{1:N0} 件中 {0:N0} 件を表示" },
		["Chests"] = new string[9] { "Cofres", "Baús", "Сундуки", "Coffres", "Truhen", "Forzieri", "Skrzynie", "Sandıklar", "宝箱" },
		["Match time"] = new string[9] { "Duración", "Duração", "Длительность", "Durée", "Matchdauer", "Durata", "Czas meczu", "Maç süresi", "試合時間" },
		["Avg {0:0.00}"] = new string[9] { "Media {0:0.00}", "Média {0:0.00}", "Среднее {0:0.00}", "Moy. {0:0.00}", "Ø {0:0.00}", "Media {0:0.00}", "Śr. {0:0.00}", "Ort. {0:0.00}", "平均 {0:0.00}" },
		["No matches with a known length yet."] = new string[9] { "Aún no hay partidas con duración conocida.", "Ainda não há partidas com duração conhecida.", "Пока нет матчей с известной длительностью.", "Aucune partie à durée connue pour l’instant.", "Noch keine Matches mit bekannter Dauer.", "Ancora nessuna partita di durata nota.", "Brak meczów o znanej długości.", "Henüz süresi bilinen maç yok.", "試合時間がわかるマッチはまだありません。" },
		["See the leaderboard on chestcounter.io"] = new string[9] { "Ver la clasificación en chestcounter.io", "Ver o ranking em chestcounter.io", "Открыть таблицу лидеров на chestcounter.io", "Voir le classement sur chestcounter.io", "Rangliste auf chestcounter.io ansehen", "Vedi la classifica su chestcounter.io", "Zobacz ranking na chestcounter.io", "Lider tablosunu chestcounter.io'da gör", "chestcounter.io でランキングを見る" }
	};

	public static string T(string source)
	{
		int num = Array.IndexOf(Codes, L.Code);
		if (num < 0 || !Texts.TryGetValue(source, out string[] value))
		{
			return source;
		}
		return value[num];
	}

	public static string F(string source, params object?[] args)
	{
		return string.Format(L.Culture, T(source), args);
	}
}
