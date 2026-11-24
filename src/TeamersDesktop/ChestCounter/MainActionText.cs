using System;
using System.Collections.Generic;
using System.Linq;

namespace ChestCounter;

internal static class MainActionText
{
	private static readonly string[] Codes = new string[9] { "es", "pt", "ru", "fr", "de", "it", "pl", "tr", "ja" };

	private static readonly Dictionary<string, string[]> Texts = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
	{
		["Toggle game overlay"] = new string[9] { "Mostrar/ocultar superposición", "Mostrar/ocultar sobreposição", "Показать/скрыть оверлей", "Afficher/masquer la superposition", "Spiel-Overlay umschalten", "Mostra/nascondi sovrapposizione", "Pokaż/ukryj nakładkę", "Oyun yer paylaşımını aç/kapat", "ゲームオーバーレイの切り替え" },
		["Compact overlay (Ctrl+Shift+K)"] = new string[9] { "Superposición compacta (Ctrl+Shift+K)", "Sobreposição compacta (Ctrl+Shift+K)", "Компактный оверлей (Ctrl+Shift+K)", "Superposition compacte (Ctrl+Shift+K)", "Kompaktes Overlay (Ctrl+Shift+K)", "Sovrapposizione compatta (Ctrl+Shift+K)", "Kompaktowa nakładka (Ctrl+Shift+K)", "Kompakt yer paylaşımı (Ctrl+Shift+K)", "コンパクト表示 (Ctrl+Shift+K)" },
		["Weekly / import / manual"] = new string[9] { "Semanal / importar / manual", "Semanal / importar / manual", "Неделя / импорт / вручную", "Semaine / import / manuel", "Woche / Import / manuell", "Settimana / importa / manuale", "Tydzień / import / ręcznie", "Haftalık / içe aktar / elle", "週間・インポート・手動入力" },
		["Weekly report"] = new string[9] { "Informe semanal", "Relatório semanal", "Недельный отчёт", "Rapport hebdomadaire", "Wochenbericht", "Resoconto settimanale", "Raport tygodniowy", "Haftalık rapor", "週間レポート" },
		["Open classic report"] = new string[9] { "Abrir informe clásico", "Abrir relatório clássico", "Открыть классический отчёт", "Ouvrir le rapport classique", "Klassischen Bericht öffnen", "Apri resoconto classico", "Otwórz klasyczny raport", "Klasik raporu aç", "従来のレポートを開く" },
		["F8  Weekly tools"] = new string[9] { "F8  Herramientas semanales", "F8  Ferramentas semanais", "F8  Недельные инструменты", "F8  Outils hebdomadaires", "F8  Wochenwerkzeuge", "F8  Strumenti settimanali", "F8  Narzędzia tygodniowe", "F8  Haftalık araçlar", "F8  週間ツール" },
		["Personal source build\nOriginal parser retained"] = new string[9] { "Compilación personal del código\nAnalizador original conservado", "Compilação pessoal do código\nAnalisador original mantido", "Личная сборка из исходников\nИсходный парсер сохранён", "Compilation personnelle du code\nAnalyseur d’origine conservé", "Persönlicher Quellcode-Build\nOriginalparser beibehalten", "Build personale dai sorgenti\nParser originale mantenuto", "Osobista kompilacja źródeł\nOryginalny parser zachowany", "Kişisel kaynak derlemesi\nÖzgün ayrıştırıcı korundu", "個人用ソースビルド\n元のパーサーを維持" },
		["Imports are integrated. Manual additions affect totals and daily charts, not match averages. Edit them in Weekly tools."] = new string[9] { "Las importaciones están integradas. Las adiciones manuales afectan a los totales y gráficos diarios, no a las medias por partida. Edítalas en las herramientas semanales.", "As importações estão integradas. As adições manuais afetam os totais e gráficos diários, não as médias por partida. Edite-as nas ferramentas semanais.", "Импортированные данные учтены. Ручные добавления влияют на итоги и дневные графики, но не на средние значения за матч. Редактируйте их в недельных инструментах.", "Les importations sont intégrées. Les ajouts manuels modifient les totaux et les graphiques quotidiens, pas les moyennes par partie. Modifiez-les dans les outils hebdomadaires.", "Importe sind integriert. Manuelle Ergänzungen beeinflussen Summen und Tagesdiagramme, nicht den Matchdurchschnitt. Bearbeite sie in den Wochenwerkzeugen.", "Le importazioni sono integrate. Le aggiunte manuali incidono sui totali e sui grafici giornalieri, non sulle medie per partita. Modificale negli strumenti settimanali.", "Importowane dane są uwzględnione. Ręczne wpisy wpływają na sumy i wykresy dzienne, ale nie na średnie na mecz. Edytuj je w narzędziach tygodniowych.", "İçe aktarılan veriler dahildir. Elle eklemeler toplamları ve günlük grafikleri etkiler, maç ortalamalarını etkilemez. Haftalık araçlarda düzenleyin.", "インポートしたデータは統合されています。手動追加は合計と日別グラフに反映されますが、マッチ平均には反映されません。週間ツールで編集してください。" },
		["No matches yet. They appear here once a replay has been analysed."] = new string[9] { "Aún no hay partidas. Aparecerán aquí cuando se analice una repetición.", "Ainda não há partidas. Elas aparecem aqui após a análise de um replay.", "Матчей пока нет. Они появятся здесь после анализа повтора.", "Aucune partie pour le moment. Elles apparaîtront ici après l’analyse d’une rediffusion.", "Noch keine Matches. Sie erscheinen hier, sobald ein Replay analysiert wurde.", "Nessuna partita ancora. Compariranno qui dopo l’analisi di un replay.", "Nie ma jeszcze meczów. Pojawią się tutaj po analizie powtórki.", "Henüz maç yok. Bir tekrar analiz edildiğinde burada görünecek.", "まだマッチがありません。リプレイの解析後にここに表示されます。" }
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
