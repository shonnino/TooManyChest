using System;
using System.Collections.Generic;

namespace ChestCounter;

internal static class ClaimAllText
{
	private static readonly string[] Codes = new string[9] { "es", "pt", "ru", "fr", "de", "it", "pl", "tr", "ja" };

	private static readonly Dictionary<string, string[]> Texts = new Dictionary<string, string[]>(StringComparer.Ordinal)
	{
		["Pirate Shop Claim All"] = new string[9] { "Reclamar todo de la tienda pirata", "Resgatar tudo da loja pirata", "Забрать всё в Пиратском магазине", "Tout récupérer dans la boutique pirate", "Piratenshop: Alles abholen", "Riscatta tutto nel negozio pirata", "Odbierz wszystko w sklepie pirackim", "Korsan dükkânı: Hepsini al", "海賊ショップまとめて入手" },
		["Already own the Pirate Shop Access Token? Claim the whole Item Shop with one button!"] = new string[9] { "¿Ya tienes el token de acceso a la tienda pirata? ¡Reclama toda la tienda de objetos con un solo botón!", "Já tem o token de acesso à loja pirata? Resgate a Loja de Itens inteira com um só botão!", "Уже есть жетон доступа в Пиратский магазин? Заберите весь магазин предметов одной кнопкой!", "Vous avez déjà le jeton d’accès à la boutique pirate ? Récupérez toute la boutique d’objets en un seul clic !", "Du hast schon den Zugangstoken für den Piratenshop? Hol dir den ganzen Item-Shop mit einem Klick!", "Hai già il gettone di accesso al negozio pirata? Riscatta tutto il negozio oggetti con un solo pulsante!", "Masz już żeton dostępu do sklepu pirackiego? Odbierz cały sklep z przedmiotami jednym przyciskiem!", "Korsan dükkânı erişim jetonun zaten var mı? Tüm Eşya Dükkânı'nı tek tuşla al!", "海賊ショップへのアクセストークンをもう持っていますか？ボタンひとつでアイテムショップをまるごと入手しましょう！" },
		["Free for Chrome, Edge, Firefox and Safari. Never spends V-Bucks."] = new string[9] { "Gratis para Chrome, Edge, Firefox y Safari. Nunca gasta paVos.", "Grátis para Chrome, Edge, Firefox e Safari. Nunca gasta V-Bucks.", "Бесплатно для Chrome, Edge, Firefox и Safari. В-баксы никогда не тратятся.", "Gratuit pour Chrome, Edge, Firefox et Safari. Ne dépense jamais de V-bucks.", "Kostenlos für Chrome, Edge, Firefox und Safari. Gibt nie V-Bucks aus.", "Gratis per Chrome, Edge, Firefox e Safari. Non spende mai V-buck.", "Za darmo dla Chrome, Edge, Firefox i Safari. Nigdy nie wydaje V-dolców.", "Chrome, Edge, Firefox ve Safari için ücretsiz. Asla V-Papel harcamaz.", "Chrome、Edge、Firefox、Safari向けに無料。V-Bucksは一切使いません。" },
		["Claim All"] = new string[9] { "Reclamar todo", "Resgatar tudo", "Забрать всё", "Tout récupérer", "Alles abholen", "Riscatta tutto", "Odbierz wszystko", "Hepsini al", "まとめて入手" },
		["Opens chestcounter.io/claim in your browser"] = new string[9] { "Abre chestcounter.io/claim en tu navegador", "Abre chestcounter.io/claim no seu navegador", "Открывает chestcounter.io/claim в браузере", "Ouvre chestcounter.io/claim dans votre navigateur", "Öffnet chestcounter.io/claim in deinem Browser", "Apre chestcounter.io/claim nel browser", "Otwiera chestcounter.io/claim w przeglądarce", "chestcounter.io/claim sayfasını tarayıcında açar", "ブラウザーで chestcounter.io/claim を開きます" }
	};

	public static string PageUrl
	{
		get
		{
			if (Array.IndexOf(Codes, L.Code) < 0)
			{
				return "https://chestcounter.io/claim";
			}
			return "https://chestcounter.io/" + L.Code + "/claim";
		}
	}

	public static string T(string source)
	{
		int num = Array.IndexOf(Codes, L.Code);
		if (num < 0 || !Texts.TryGetValue(source, out string[] value))
		{
			return source;
		}
		return value[num];
	}
}
