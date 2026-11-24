using System;
using System.Collections.Generic;

namespace ChestCounter;

internal static class MainStatusText
{
	private static readonly string[] Codes = new string[9] { "es", "pt", "ru", "fr", "de", "it", "pl", "tr", "ja" };

	private static readonly Dictionary<string, string[]> Texts = new Dictionary<string, string[]>(StringComparer.Ordinal)
	{
		["● Replay folder not found, choose it in Settings"] = new string[9] { "● No se encuentra la carpeta de repeticiones; selecciónala en Ajustes", "● Pasta de replays não encontrada; selecione-a em Configurações", "● Папка повторов не найдена; выберите её в настройках", "● Dossier des rediffusions introuvable ; choisissez-le dans les paramètres", "● Replay-Ordner nicht gefunden; wähle ihn in den Einstellungen", "● Cartella replay non trovata; selezionala nelle impostazioni", "● Nie znaleziono folderu powtórek; wybierz go w ustawieniach", "● Tekrar klasörü bulunamadı; Ayarlar’dan seçin", "● リプレイフォルダーが見つかりません。設定で選択してください" },
		["● Match in progress, counting live"] = new string[9] { "● Partida en curso; contando en directo", "● Partida em andamento; contagem ao vivo", "● Матч идёт, подсчёт в реальном времени", "● Partie en cours, comptage en direct", "● Match läuft, Live-Zählung aktiv", "● Partita in corso, conteggio in tempo reale", "● Mecz w toku, zliczanie na żywo", "● Maç sürüyor, canlı sayım yapılıyor", "● マッチ進行中、リアルタイムで集計中" },
		["● Ready, play a match and it will appear here"] = new string[9] { "● Listo; juega una partida y aparecerá aquí", "● Pronto; jogue uma partida e ela aparecerá aqui", "● Готово; сыграйте матч, и он появится здесь", "● Prêt ; jouez une partie, elle apparaîtra ici", "● Bereit; spiele ein Match, dann erscheint es hier", "● Pronto; gioca una partita e apparirà qui", "● Gotowe; rozegraj mecz, a pojawi się tutaj", "● Hazır; bir maç oynayın, burada görünsün", "● 準備完了。マッチをプレイするとここに表示されます" },
		["● Ready, watching your replays"] = new string[9] { "● Listo; supervisando tus repeticiones", "● Pronto; monitorando seus replays", "● Готово, отслеживание повторов", "● Prêt, surveillance des rediffusions", "● Bereit, Replays werden überwacht", "● Pronto, monitoraggio dei replay", "● Gotowe, monitorowanie powtórek", "● Hazır, tekrarlarınız izleniyor", "● 準備完了、リプレイを監視中" },
		["Waiting for your first completed replay"] = new string[9] { "Esperando tu primera repetición completada", "Aguardando seu primeiro replay concluído", "Ожидание первого завершённого повтора", "En attente de votre première rediffusion terminée", "Warten auf dein erstes abgeschlossenes Replay", "In attesa del primo replay completato", "Oczekiwanie na pierwszą ukończoną powtórkę", "İlk tamamlanmış tekrarınız bekleniyor", "最初の完了したリプレイを待っています" },
		["Squad data: enable teammate tracking in Settings. Counts are limited to replay visibility."] = new string[9] { "Datos del escuadrón: activa el seguimiento de compañeros en Ajustes. Los recuentos se limitan a lo visible en la repetición.", "Dados da equipe: ative o rastreamento de colegas em Configurações. As contagens se limitam ao que está visível no replay.", "Данные отряда: включите отслеживание товарищей по команде в настройках. Подсчёт ограничен видимостью в повторе.", "Données d’escouade : activez le suivi des coéquipiers dans les paramètres. Les décomptes se limitent à ce qui est visible dans la rediffusion.", "Truppdaten: Aktiviere die Teamkollegen-Erfassung in den Einstellungen. Gezählt wird nur, was im Replay sichtbar ist.", "Dati della squadra: attiva il tracciamento dei compagni nelle impostazioni. I conteggi si limitano a quanto è visibile nel replay.", "Dane drużyny: włącz śledzenie członków drużyny w ustawieniach. Zliczanie obejmuje tylko to, co widać w powtórce.", "Takım verileri: Ayarlar’dan takım arkadaşı takibini etkinleştirin. Sayımlar tekrar görünürlüğüyle sınırlıdır.", "スクワッドのデータ：設定でチームメイトの追跡を有効にしてください。集計はリプレイで見える範囲に限られます。" },
		["Closing with Fortnite — click here to keep open."] = new string[9] { "Se cerrará con Fortnite; haz clic aquí para mantenerlo abierto.", "Fechando com o Fortnite; clique aqui para manter aberto.", "Закрытие вместе с Fortnite — нажмите здесь, чтобы оставить открытым.", "Fermeture avec Fortnite — cliquez ici pour garder l’application ouverte.", "Wird mit Fortnite geschlossen — hier klicken, um geöffnet zu bleiben.", "Chiusura insieme a Fortnite — fai clic qui per mantenere aperto.", "Zamykanie razem z Fortnite — kliknij tutaj, aby pozostawić otwarte.", "Fortnite ile kapanıyor — açık tutmak için buraya tıklayın.", "Fortniteと一緒に終了します。開いたままにするにはここをクリックしてください。" },
		["Overlay on"] = new string[9] { "Superposición activada", "Sobreposição ativada", "Оверлей включён", "Superposition activée", "Overlay an", "Sovrapposizione attiva", "Nakładka włączona", "Yer paylaşımı açık", "オーバーレイ オン" },
		["Overlay off"] = new string[9] { "Superposición desactivada", "Sobreposição desativada", "Оверлей выключен", "Superposition désactivée", "Overlay aus", "Sovrapposizione disattivata", "Nakładka wyłączona", "Yer paylaşımı kapalı", "オーバーレイ オフ" },
		["Weekly tools error"] = new string[9] { "Error de herramientas semanales", "Erro nas ferramentas semanais", "Ошибка недельных инструментов", "Erreur des outils hebdomadaires", "Fehler der Wochenwerkzeuge", "Errore degli strumenti settimanali", "Błąd narzędzi tygodniowych", "Haftalık araç hatası", "週間ツールのエラー" },
		["Weekly chances error"] = new string[9] { "Error de probabilidades semanales", "Erro nas chances semanais", "Ошибка недельных шансов", "Erreur des chances hebdomadaires", "Fehler der wöchentlichen Chancen", "Errore delle probabilità settimanali", "Błąd szans tygodniowych", "Haftalık şanslar hatası", "毎週の確率のエラー" },
		["History error"] = new string[9] { "Error del historial", "Erro no histórico", "Ошибка истории", "Erreur de l’historique", "Verlaufsfehler", "Errore della cronologia", "Błąd historii", "Geçmiş hatası", "履歴のエラー" },
		["Date"] = new string[9] { "Fecha", "Data", "Дата", "Date", "Datum", "Data", "Data", "Tarih", "日時" },
		["Mode"] = new string[9] { "Modo", "Modo", "Режим", "Mode", "Modus", "Modalità", "Tryb", "Mod", "モード" },
		["Personal"] = new string[9] { "Personal", "Pessoal", "Личные", "Personnel", "Persönlich", "Personale", "Osobiste", "Kişisel", "個人" },
		["Counted (HUD)"] = new string[9] { "Total (HUD)", "Total (HUD)", "Итого (HUD)", "Total (HUD)", "Gesamt (HUD)", "Totale (HUD)", "Suma (HUD)", "Toplam (HUD)", "合計 (HUD)" },
		["Teammates seen"] = new string[9] { "Compañeros (vistos)", "Colegas (vistos)", "Союзники (видно)", "Coéquipiers (vus)", "Team (sichtbar)", "Compagni (visti)", "Drużyna (widoczne)", "Takım (görülen)", "味方 (確認分)" },
		["Observed squad"] = new string[9] { "Escuadrón observado", "Equipe observada", "Наблюдаемый отряд", "Escouade observée", "Beobachteter Trupp", "Squadra osservata", "Zaobserwowana drużyna", "Gözlemlenen takım", "観測したスクワッド" },
		["Place"] = new string[9] { "Puesto", "Colocação", "Место", "Position", "Platz", "Posizione", "Miejsce", "Sıralama", "順位" },
		["Eliminations"] = new string[9] { "Eliminaciones", "Eliminações", "Устранения", "Éliminations", "Eliminierungen", "Eliminazioni", "Eliminacje", "Elemeler", "撃破数" }
	};

	public static string T(string source)
	{
		int num = Array.IndexOf(Codes, L.Code);
		if (num < 0 || !Texts.TryGetValue(source, out string[] value))
		{
			return EnglishText.Fix(source);
		}
		return value[num];
	}

	public static string HistoryHeader(string property)
	{
		return property switch
		{
			"CountedTotal" => T("Counted (HUD)"),
			"TeammatesSeen" => T("Teammates seen"),
			_ => AppText.T(T(property))
		};
	}
}
