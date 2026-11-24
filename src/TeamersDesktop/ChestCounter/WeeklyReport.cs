using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace ChestCounter;

[SupportedOSPlatform("windows")]
public static class WeeklyReport
{
	private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	public static void Open(Store store, Monitor monitor, IWin32Window? owner = null)
	{
		try
		{
			Process.Start(new ProcessStartInfo(Write(store, monitor))
			{
				UseShellExecute = true
			})?.Dispose();
		}
		catch (Exception ex)
		{
			ErrorLog.Write("Weekly report", ex);
			MessageBox.Show(owner, ex.Message, "Report error");
		}
	}

	private static T? Read<T>(string path)
	{
		try
		{
			return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path)) : default(T);
		}
		catch
		{
			return default;
		}
	}

	private static DateTime AsUtc(DateTime d)
	{
		if (d.Kind != DateTimeKind.Local)
		{
			return DateTime.SpecifyKind(d, DateTimeKind.Utc);
		}
		return d.ToUniversalTime();
	}

	private static string Iso(DateTime d)
	{
		return AsUtc(d).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
	}

	private static long Epoch(DateTime d)
	{
		return new DateTimeOffset(AsUtc(d)).ToUnixTimeMilliseconds();
	}

	internal static string DataUri(Bitmap bitmap)
	{
		using MemoryStream memoryStream = new MemoryStream();
		bitmap.Save(memoryStream, ImageFormat.Png);
		return "data:image/png;base64," + Convert.ToBase64String(memoryStream.ToArray());
	}

	private static string Banner()
	{
		try
		{
			Image image = null;
			try
			{
				image = PersonalBrand.Banner();
			}
			catch
			{
			}
			if (image == null)
			{
				using Stream stream = typeof(WeeklyReport).Assembly.GetManifestResourceStream("ChestCounter.Assets.2xflaw-banner.jpg");
				if (stream == null)
				{
					return "";
				}
				MemoryStream memoryStream = new MemoryStream();
				stream.CopyTo(memoryStream);
				image = new Bitmap(memoryStream);
			}
			int num = Math.Min(1600, image.Width);
			int height = (int)Math.Round((double)image.Height * ((double)num / (double)image.Width));
			using Bitmap bitmap = new Bitmap(num, height);
			using (Graphics graphics = Graphics.FromImage(bitmap))
			{
				graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
				graphics.DrawImage(image, 0, 0, num, height);
			}
			ImageCodecInfo encoder = ImageCodecInfo.GetImageEncoders().First((ImageCodecInfo c) => c.FormatID == ImageFormat.Jpeg.Guid);
			using EncoderParameters encoderParameters = new EncoderParameters(1);
			encoderParameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 82L);
			using MemoryStream memoryStream2 = new MemoryStream();
			bitmap.Save(memoryStream2, encoder, encoderParameters);
			return "<img class=\"banner\" alt=\"\" src=\"data:image/jpeg;base64," + Convert.ToBase64String(memoryStream2.ToArray()) + "\">";
		}
		catch
		{
			return "";
		}
	}

	private static string Avatar()
	{
		try
		{
			Bitmap image = PersonalBrand.Avatar();
			using Bitmap bitmap = new Bitmap(96, 96);
			using (Graphics graphics = Graphics.FromImage(bitmap))
			{
				graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
				graphics.DrawImage(image, 0, 0, 96, 96);
			}
			return "<img class=\"avatar\" alt=\"\" src=\"" + DataUri(bitmap) + "\">";
		}
		catch
		{
			return "";
		}
	}

	internal static string FontFaces()
	{
		Assembly assembly = typeof(WeeklyReport).Assembly;
		StringBuilder stringBuilder = new StringBuilder();
		foreach (string item in from n in assembly.GetManifestResourceNames()
			where n.StartsWith("ChestCounter.Assets.fonts.", StringComparison.OrdinalIgnoreCase) && n.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)
			select n)
		{
			string value;
			int value2;
			if (item.Contains("Anton", StringComparison.OrdinalIgnoreCase))
			{
				value = "CC Display";
				value2 = 400;
			}
			else
			{
				if (!item.Contains("Inter", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				value = "CC Inter";
				value2 = (item.Contains("Bold", StringComparison.OrdinalIgnoreCase) ? 700 : 400);
			}
			using Stream stream = assembly.GetManifestResourceStream(item);
			if (stream != null)
			{
				using MemoryStream memoryStream = new MemoryStream();
				stream.CopyTo(memoryStream);
				stringBuilder.Append("@font-face{font-family:\"").Append(value).Append("\";font-weight:")
					.Append(value2)
					.Append(";src:url(data:font/ttf;base64,")
					.Append(Convert.ToBase64String(memoryStream.ToArray()))
					.Append(") format(\"truetype\");font-display:swap}\n");
			}
		}
		return stringBuilder.ToString();
	}

	public static string Write(Store store, Monitor monitor)
	{
		DateTime now = DateTime.UtcNow;
		bool showTeammates = monitor.Config.ShowTeammates;
		ChanceSnapshot chanceSnapshot = WeeklyChanceModel.Compute(store, monitor, now);
		TrackerData trackerData = PersonalReadCache.Load(store);
		Profile profile = Profiles.Current(trackerData, monitor.Config);
		string key = profile?.Key ?? "";
		PersonalLedger personalLedger = Read<PersonalLedger>(Path.Combine(store.Folder, "2xflaw-ledger.json")) ?? new PersonalLedger();
		PersonalOdds personalOdds = Read<PersonalOdds>(Path.Combine(store.Folder, "2xflaw-odds.json")) ?? PersonalOdds.Seed();
		DateTime start = chanceSnapshot.WeekStart;
		DateTime end = start.AddDays(7.0);
		List<MatchRecord> source = (from m in trackerData.Matches.Concat(personalLedger.Imports.Select((PersonalImport x) => x.Match))
			where Profiles.Key(m) == key && m.IsBattleRoyale
			group m by string.IsNullOrEmpty(m.SessionId) ? $"{m.ReplayFile}|{m.MatchStartUtc:O}" : m.SessionId into g
			select g.First()).ToList();
		List<MatchRecord> list = source.Where((MatchRecord m) =>
		{
			DateTime? matchStartUtc = m.MatchStartUtc;
			if (matchStartUtc.HasValue)
			{
				DateTime valueOrDefault = matchStartUtc.GetValueOrDefault();
				if (valueOrDefault < end)
				{
					return valueOrDefault.AddSeconds(m.DurationSeconds.GetValueOrDefault()) >= start;
				}
			}
			return false;
		}).ToList();
		List<PersonalEntry> list2 = personalLedger.Entries.Where((PersonalEntry e) => e.Profile == key && e.AtUtc >= start && e.AtUtc <= now).ToList();
		int[] daily = new int[7];
		foreach (MatchRecord item in list)
		{
			if (item.Chests.Count > 0)
			{
				foreach (ChestRecord chest in item.Chests)
				{
					AddAt(item.MatchStartUtc.Value.AddSeconds(chest.TimeSeconds), 1);
				}
			}
			else
			{
				AddAt(item.MatchStartUtc, PersonalWeek.PersonalCount(item, start, end));
			}
		}
		foreach (PersonalEntry item2 in list2)
		{
			AddAt(item2.AtUtc, item2.Mine);
		}
		var windows = (from w in (from w in personalOdds.Windows
				where w.EndUtc >= start
				orderby w.StartUtc
				select w).Take(10)
			select new
			{
				start = Iso(w.StartUtc),
				end = Iso(w.EndUtc),
				gold = w.Gold,
				shop = w.Shop,
				vehicle = w.Vehicle,
				unknown = TreasureHours.IsUnconfirmed(w)
			}).ToList();
		string folder = Path.Combine(store.Folder, "rewards");
		List<object> list3 = new List<object>();
		foreach (PrizeChance prize in chanceSnapshot.Prizes)
		{
			using Bitmap bitmap = RewardPictures.Load(folder, prize.Key, 150, 14);
			list3.Add(new
			{
				key = prize.Key,
				name = prize.Name,
				den = prize.Den,
				chance = prize.Chance,
				owned = prize.Owned,
				allTime = prize.AllTimeChests.HasValue,
				allChests = prize.AllTimeChests,
				img = DataUri(bitmap)
			});
		}
		string pick = null;
		try
		{
			string path = Path.Combine(store.Folder, "2xflaw-goal.txt");
			string text = (File.Exists(path) ? File.ReadAllText(path).Trim() : "");
			pick = ((text.Length == 0) ? null : text);
		}
		catch
		{
		}
		var matches = source.OrderByDescending((MatchRecord m) => m.MatchStartUtc).Take(100).Select((MatchRecord m) =>
		{
			DateTime? matchStartUtc = m.MatchStartUtc;
			object start2;
			if (matchStartUtc.HasValue)
			{
				DateTime valueOrDefault = matchStartUtc.GetValueOrDefault();
				start2 = Iso(valueOrDefault);
			}
			else
			{
				start2 = null;
			}
			return new
			{
				start = (string)start2,
				mode = Pretty.Playlist(m.Playlist),
				place = Pretty.Placement(m),
				win = (m.Placement == 1),
				chests = m.ChestsOpened,
				matesTotal = showTeammates ? m.Teammates?.Sum(tm => tm.ChestsSeen) ?? 0 : 0,
				mates = (showTeammates ? m.Teammates ?? Enumerable.Empty<TeammateRecord>() : Enumerable.Empty<TeammateRecord>()).Select((TeammateRecord tm) => new
				{
					name = tm.Name,
					chests = tm.ChestsSeen
				}).ToList(),
				treasures = m.TreasureFinds.Select((TreasureFind f) => f.DisplayName).ToList()
			};
		})
			.ToList();
		var manual = (from e in personalLedger.Entries
			where e.Profile == key
			orderby e.AtUtc descending
			select new
			{
				at = Iso(e.AtUtc),
				mine = e.Mine,
				squad = showTeammates ? e.Squad : 0,
				note = e.Note
			}).ToList();
		var value = new
		{
			profile = (profile?.Name ?? ""),
			generatedUtc = Epoch(now),
			resetUtc = Epoch(WeeklyReset.Next(now)),
			chests = chanceSnapshot.Chests,
			pick = pick,
			prizes = list3,
			windows = windows,
			daily = daily.Select((int c, int i) => new
			{
				start = Iso(start.AddDays(i)),
				chests = c
			}).ToList(),
			kpis = new
			{
				mine = chanceSnapshot.Chests,
				mates = showTeammates ? list.Sum(m => m.Teammates?.Sum(tm => tm.ChestsSeen) ?? 0) + list2.Sum(e => e.Squad) : 0,
				manual = list2.Sum((PersonalEntry e) => e.Mine)
			},
			matches = matches,
			manual = manual,
			odds = new
			{
				source = personalOdds.Source,
				captured = Iso(personalOdds.CapturedUtc),
				status = personalOdds.Status
			}
		};
		string contents = "<!doctype html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">\n<meta name=\"color-scheme\" content=\"dark\">\n<title>2xFlaw's Chest Counter - Weekly chances</title>\n<style>\n/*FONTS*/\n:root{\n  --bg:#0e0912;--bg2:#140b19;--card:#190f1e;--card2:#211428;--panel:#26172f;--hover:#31203d;\n  --ink:#ece6f2;--muted:#8f8499;--faint:#5b4d68;--line:#2b1b35;--line2:#3f2a52;\n  --acc:#ff66b2;--acc2:#d00080;--amber:#ffb84d;--mint:#3ddc97;--rose:#ff80aa;--coral:#ff6363;\n  --display:\"CC Display\",\"Anton\",\"Impact\",\"Arial Narrow\",sans-serif;\n  --body:\"CC Inter\",\"Inter\",\"Segoe UI Variable Text\",\"Segoe UI\",system-ui,-apple-system,sans-serif;\n  --shadow:0 1px 2px rgba(0,0,0,.4),0 14px 40px rgba(0,0,0,.32);\n}\n*{box-sizing:border-box}\nhtml{background:var(--bg);scroll-behavior:smooth}\nbody{margin:0;background:radial-gradient(1200px 560px at 12% -12%,rgba(208,0,128,.22),transparent 60%),radial-gradient(900px 480px at 100% 0,rgba(255,184,77,.08),transparent 55%),var(--bg);color:var(--ink);font:14px/1.5 var(--body);-webkit-font-smoothing:antialiased}\nmain{max-width:1180px;margin:0 auto;padding:22px 18px 70px}\nh1,h2,h3{margin:0;font-family:var(--display);font-weight:400;letter-spacing:.01em;text-transform:uppercase}\n.num{font-variant-numeric:tabular-nums}\n.muted{color:var(--muted)}\n.lbl{font-size:10.5px;font-weight:700;letter-spacing:.16em;text-transform:uppercase;color:var(--muted)}\nbutton{font:inherit;color:inherit}\n:focus-visible{outline:2px solid var(--acc);outline-offset:2px;border-radius:10px}\n\n/* ---------- chips and buttons ---------- */\n.chip{display:inline-flex;align-items:center;gap:7px;padding:5px 12px;border-radius:99px;font-size:10.5px;font-weight:800;letter-spacing:.14em;text-transform:uppercase;color:var(--c,var(--acc));background:color-mix(in srgb,var(--c,var(--acc)) 14%,transparent);border:1px solid color-mix(in srgb,var(--c,var(--acc)) 45%,transparent);white-space:nowrap}\n.chip.amber{--c:var(--amber)}.chip.mint{--c:var(--mint)}.chip.rose{--c:var(--rose)}.chip.mute{--c:var(--muted)}\n.chip .dot{width:7px;height:7px;border-radius:50%;background:currentColor;animation:pulse 1.8s ease-out infinite}\n@keyframes pulse{0%{box-shadow:0 0 0 0 color-mix(in srgb,currentColor 60%,transparent)}70%{box-shadow:0 0 0 8px transparent}100%{box-shadow:0 0 0 0 transparent}}\n.btn{appearance:none;cursor:pointer;border:1px solid var(--line2);background:linear-gradient(180deg,#2e1d3a,var(--panel));padding:9px 16px;border-radius:12px;font-weight:700;font-size:12.5px;transition:transform .12s,border-color .15s,background .15s,box-shadow .15s}\n.btn:hover{border-color:rgba(255,102,178,.75);box-shadow:0 0 0 3px rgba(255,102,178,.12)}\n.btn:active{transform:translateY(1px) scale(.99)}\n.btn.on{background:linear-gradient(180deg,var(--acc),var(--acc2));border-color:var(--acc);color:#fff}\n.btn.on:hover{box-shadow:0 0 0 3px rgba(255,102,178,.28)}\n\n/* ---------- cards ---------- */\n.card{position:relative;background:linear-gradient(180deg,rgba(255,255,255,.035),transparent 55%),var(--card);border:1px solid var(--line);border-radius:20px;box-shadow:var(--shadow)}\n.card::before{content:\"\";position:absolute;left:18px;right:18px;top:0;height:1px;background:linear-gradient(90deg,transparent,rgba(255,255,255,.22),transparent)}\nsection{margin-top:22px}\n.sh{display:flex;flex-wrap:wrap;align-items:baseline;justify-content:space-between;gap:6px 16px;margin:0 0 12px}\n.sh h2{font-size:22px}\n.sh .meta{font-size:12.5px;color:var(--muted)}\n\n/* ---------- hero ---------- */\n.hero{position:relative;overflow:hidden;border-radius:24px;border:1px solid var(--line);box-shadow:var(--shadow);min-height:250px;background:linear-gradient(120deg,#2a0f2c,#1b0f2e 55%,#0e0912)}\n.hero img.banner{position:absolute;inset:0;width:100%;height:100%;object-fit:cover;object-position:center 30%;opacity:.9}\n.hero .shade{position:absolute;inset:0;background:linear-gradient(90deg,rgba(14,9,18,.92) 0,rgba(14,9,18,.55) 55%,rgba(14,9,18,.15)),linear-gradient(0deg,rgba(14,9,18,.9),transparent 55%)}\n.hero .in{position:relative;display:flex;flex-wrap:wrap;justify-content:space-between;align-items:flex-end;gap:18px;padding:30px 30px 26px;min-height:250px}\n.hero h1{font-size:clamp(40px,7vw,74px);line-height:.95}\n.hero .eyebrow{margin-bottom:8px;color:var(--acc)}\n.hero .chips{display:flex;flex-wrap:wrap;gap:8px;margin-top:16px;align-items:center}\n.avatar{width:38px;height:38px;border-radius:50%;object-fit:cover;border:2px solid var(--acc);box-shadow:0 0 18px rgba(255,102,178,.45)}\n.resetbox{text-align:right}\n.resetbox .big{font:400 clamp(30px,4.6vw,46px)/1 var(--display);color:var(--acc);margin:6px 0 4px;letter-spacing:.02em;text-shadow:0 0 28px rgba(255,102,178,.45)}\n.sparkles{position:absolute;inset:0;pointer-events:none}\n.sparkles i{position:absolute;color:#fff;font-style:normal;opacity:0;animation:tw 3.4s ease-in-out infinite}\n@keyframes tw{0%,100%{opacity:0;transform:scale(.4) rotate(0)}45%,55%{opacity:.95;transform:scale(1) rotate(35deg)}}\n\n/* ---------- treasure hour strip ---------- */\n.hour{display:flex;flex-wrap:wrap;align-items:center;justify-content:space-between;gap:10px 18px;margin-top:14px;padding:14px 18px;border-radius:16px;border:1px solid var(--line);background:var(--card)}\n.hour.live{border-color:rgba(255,184,77,.6);background:linear-gradient(90deg,rgba(255,184,77,.16),var(--card));box-shadow:0 0 0 1px rgba(255,184,77,.15),0 0 38px rgba(255,184,77,.12)}\n.hour .t{font:400 22px/1 var(--display);letter-spacing:.02em;text-transform:uppercase}\n.hour .cd{font:400 30px/1 var(--display);color:var(--acc);letter-spacing:.03em}\n.hour.live .cd{color:var(--amber)}\n.bts{display:flex;flex-wrap:wrap;gap:8px;margin-top:10px}\n.bt{display:inline-flex;align-items:center;gap:7px;padding:4px 12px 4px 4px;border-radius:12px;background:var(--panel);border:1px solid var(--line2);font-weight:800;font-size:13px;color:var(--mint)}\n.bt img{width:30px;height:30px;border-radius:8px;display:block}\n.bt.unk{color:var(--muted)}\n.q{display:inline-grid;place-items:center;width:30px;height:30px;border-radius:8px;background:#3a3345;color:#c4bad1;font:400 20px/1 var(--display)}\n.unkbox{display:flex;align-items:center;gap:12px;color:var(--muted)}\n.unkbox .q{width:44px;height:44px;border-radius:12px;font-size:28px}\n.stale{display:none;margin-top:12px;padding:10px 16px;border-radius:14px;border:1px solid rgba(255,99,99,.5);background:rgba(255,99,99,.10);color:#ffb3b3;font-size:13px}\n\n/* ---------- goal ---------- */\n.goal{display:grid;grid-template-columns:230px 1fr;gap:26px;padding:24px;overflow:hidden;--acc2x:var(--acc)}\n.goal.boost{background:linear-gradient(135deg,rgba(255,184,77,.14),transparent 55%),var(--card);border-color:rgba(255,184,77,.45);--acc2x:var(--amber)}\n.goal:not(.boost){background:linear-gradient(135deg,rgba(255,102,178,.13),transparent 55%),var(--card)}\n.pic{position:relative;width:230px;height:230px}\n.pic img{width:100%;height:100%;border-radius:20px;display:block;border:2px solid color-mix(in srgb,var(--acc2x) 60%,transparent);box-shadow:0 14px 40px rgba(0,0,0,.5),0 0 40px color-mix(in srgb,var(--acc2x) 28%,transparent)}\n.pic i{position:absolute;font-style:normal;color:#fff;opacity:0;animation:tw 3.2s ease-in-out infinite;pointer-events:none}\n.goal.boost .pic i{color:var(--amber)}\n.gtop{display:flex;flex-wrap:wrap;gap:8px;align-items:center}\n.goal h3{font-size:clamp(26px,3.6vw,38px);line-height:1.02;margin:12px 0 2px}\n.gpct{font:400 clamp(52px,8vw,84px)/1 var(--display);color:var(--acc2x);letter-spacing:.01em;margin:2px 0 0;text-shadow:0 0 30px color-mix(in srgb,var(--acc2x) 35%,transparent)}\n.gstats{display:flex;flex-wrap:wrap;gap:4px 22px;margin-top:6px;color:var(--muted);font-size:13px}\n.gstats b{color:var(--ink);font-weight:700}\n.gproj{margin-top:6px;font-size:13px;font-weight:600}\n.path{position:relative;margin:26px 8px 34px}\n.track{height:10px;border-radius:99px;background:var(--panel);overflow:hidden}\n.pfill{height:100%;width:0;border-radius:99px;background:linear-gradient(90deg,var(--acc2),var(--acc),var(--rose));position:relative;transition:width .9s cubic-bezier(.2,.8,.2,1)}\n.pfill::after{content:\"\";position:absolute;inset:0;background:linear-gradient(100deg,transparent 30%,rgba(255,255,255,.55) 50%,transparent 70%);transform:translateX(-100%);animation:shim 2.6s ease-in-out infinite}\n@keyframes shim{to{transform:translateX(100%)}}\n.ms{position:absolute;top:50%;width:14px;height:14px;border-radius:50%;background:var(--card);border:2px solid var(--line2);transform:translate(-50%,-50%);transition:background .4s,border-color .4s}\n.ms.hit{background:var(--mint);border-color:var(--mint)}\n.ml{position:absolute;top:20px;transform:translateX(-50%);font-size:11px;color:var(--muted)}\n.ml.hit{color:var(--mint)}\n.mk{position:absolute;top:50%;width:18px;height:18px;border-radius:50%;background:var(--ink);border:3px solid var(--acc2x);transform:translate(-50%,-50%);box-shadow:0 0 0 6px color-mix(in srgb,var(--acc2x) 28%,transparent);transition:left .9s cubic-bezier(.2,.8,.2,1)}\n\n/* ---------- planner ---------- */\n.planner{padding:20px 24px}\n.ptop{display:flex;flex-wrap:wrap;justify-content:space-between;align-items:center;gap:8px 16px}\n.pgrid{display:grid;grid-template-columns:1fr auto 1fr;gap:20px;align-items:center;margin-top:16px}\n.chests{display:flex;flex-wrap:wrap;gap:8px;margin-top:8px}\n.pv{font:400 38px/1.05 var(--display);letter-spacing:.01em}\n.pv.hot{color:var(--amber)}\n.arrow{font-size:26px;color:var(--faint)}\n\n/* ---------- odds table ---------- */\n.rows{padding:6px 10px 10px}\n.rhead,.row{display:grid;grid-template-columns:minmax(220px,1.1fr) minmax(200px,1.6fr) 118px;gap:18px;align-items:center;padding:10px 14px}\n.rhead{padding-top:16px;padding-bottom:6px}\n.row{position:relative;border-radius:14px;cursor:pointer;border:1px solid transparent;transition:background .15s,border-color .15s}\n.row+.row{margin-top:2px}\n.row:hover{background:var(--hover)}\n.row.sel{background:rgba(255,102,178,.12);border-color:rgba(255,102,178,.5)}\n.row.gz{cursor:default}\n.row .who{display:flex;gap:14px;align-items:center;min-width:0}\n.row img{width:46px;height:46px;border-radius:12px;flex:none}\n.row .nm{font-weight:800;font-size:15px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}\n.row .sb{font-size:12px;color:var(--muted)}\n.row .sb .hot{color:var(--amber);font-weight:700}\n.bar{position:relative;height:9px;border-radius:99px;background:var(--panel)}\n.bar .gh{position:absolute;top:0;bottom:0;border-radius:99px;border:1px dashed var(--acc);background:rgba(255,102,178,.22);opacity:0;transition:opacity .2s,left .7s,width .7s}\n.bar .gh.hot{border-color:var(--amber);background:rgba(255,184,77,.30)}\n.row.sel .gh,.row:hover .gh{opacity:1}\n.bar .fl{position:absolute;left:0;top:0;bottom:0;border-radius:99px;background:linear-gradient(90deg,var(--acc2),var(--acc));width:0;transition:width .9s cubic-bezier(.2,.8,.2,1)}\n.pc{text-align:right}\n.pc .p{font:400 24px/1 var(--display);letter-spacing:.01em}\n.row.sel .pc .p{color:var(--acc)}\n.pc .q2{font-size:12px;color:var(--muted);height:16px;margin-top:3px;opacity:0;transition:opacity .2s}\n.row.sel .q2,.row:hover .q2{opacity:1}\n.empty{padding:28px;text-align:center;color:var(--mint);font:400 24px var(--display);text-transform:uppercase}\n\n/* ---------- schedule ---------- */\n.sched{display:grid;grid-template-columns:repeat(auto-fill,minmax(310px,1fr));gap:12px}\n.win{padding:16px 18px}\n.win .when{font-weight:800;font-size:15px}\n.win .row2{display:flex;justify-content:space-between;align-items:center;gap:10px;margin-top:12px}\n.win.live{border-color:rgba(255,184,77,.65);box-shadow:0 0 34px rgba(255,184,77,.14)}\n.win.past{opacity:.5}\n\n/* ---------- stats ---------- */\n.kpis{display:grid;grid-template-columns:repeat(4,1fr);gap:12px}\n.kpi{padding:18px 20px}\n.kpi .v{font:400 44px/1 var(--display);margin:8px 0 4px;color:var(--c,var(--ink))}\n.kpi .s{font-size:12.5px;color:var(--muted)}\n.days{display:flex;align-items:flex-end;gap:12px;height:180px;padding:22px 22px 14px}\n.day{flex:1;display:flex;flex-direction:column;align-items:center;justify-content:flex-end;gap:8px;height:100%}\n.day .b{width:100%;max-width:70px;border-radius:10px 10px 4px 4px;background:linear-gradient(180deg,var(--acc),var(--acc2));height:0;transition:height 1s cubic-bezier(.2,.8,.2,1);position:relative;min-height:3px}\n.day .b.zero{background:var(--line2)}\n.day .b span{position:absolute;top:-22px;left:50%;transform:translateX(-50%);font-size:12px;font-weight:800}\n.day .l{font-size:11.5px;color:var(--muted)}\n.day.today .b{background:linear-gradient(180deg,var(--amber),#ff8a3d)}\n\n/* ---------- tables ---------- */\n.tw{overflow-x:auto}\ntable{width:100%;border-collapse:collapse;font-variant-numeric:tabular-nums}\nth,td{text-align:left;padding:11px 16px;border-bottom:1px solid var(--line);vertical-align:top}\nth{font-size:10.5px;font-weight:800;letter-spacing:.14em;text-transform:uppercase;color:var(--muted);white-space:nowrap;position:sticky;top:0;background:var(--card2)}\ntbody tr:hover td{background:rgba(255,255,255,.03)}\ntr.win-row td{background:rgba(255,184,77,.08)}\n.tools{display:flex;gap:10px;flex-wrap:wrap;align-items:center}\ninput[type=search]{background:var(--panel);border:1px solid var(--line2);border-radius:12px;color:var(--ink);padding:9px 14px;font:inherit;min-width:230px}\ninput[type=search]:focus{outline:2px solid var(--acc);border-color:transparent}\n.note{margin-top:14px;font-size:12.5px;color:var(--muted);line-height:1.65}\nfooter{margin-top:36px;padding-top:20px;border-top:1px solid var(--line);color:var(--muted);font-size:12.5px;line-height:1.7}\nfooter h3{font-size:14px;letter-spacing:.12em;margin-bottom:6px;color:var(--ink)}\nfooter a{color:var(--acc)}\nfooter .made{margin-top:14px;font:400 18px var(--display);letter-spacing:.08em;text-transform:uppercase;color:var(--acc)}\n\n@media (max-width:900px){\n  .goal{grid-template-columns:1fr}.pic{margin:0 auto}\n  .pgrid{grid-template-columns:1fr}.arrow{transform:rotate(90deg);text-align:center}\n  .kpis{grid-template-columns:repeat(2,1fr)}\n  .rhead{display:none}.row{grid-template-columns:1fr 100px}.row .bar{grid-column:1 / -1;order:3}\n  .resetbox{text-align:left}\n}\n@media (prefers-reduced-motion:reduce){*{animation:none!important;transition:none!important}}\n@media print{body{background:#fff;color:#000}.btn,.sparkles,.pic i{display:none}.card,.hour{box-shadow:none;border-color:#bbb;background:#fff}}\n</style>\n</head>\n<body>\n<main>\n\n<header class=\"hero\">\n  @@BANNER@@\n  <div class=\"shade\"></div>\n  <div class=\"sparkles\" id=\"heroSparks\"></div>\n  <div class=\"in\">\n    <div>\n      <div class=\"lbl eyebrow\">2xFlaw's Chest Counter</div>\n      <h1>Weekly chances</h1>\n      <div class=\"chips\">\n        @@AVATAR@@\n        <span class=\"chip\" id=\"chipProfile\"></span>\n        <span class=\"chip mute\" id=\"chipGen\"></span>\n        <button class=\"btn\" id=\"btnTz\" type=\"button\" title=\"Show the times in GMT or in your own time zone\"></button>\n        <button class=\"btn\" id=\"btnCopy\" type=\"button\">Copy summary</button>\n      </div>\n    </div>\n    <div class=\"resetbox\">\n      <div class=\"lbl\">Weekly reset in</div>\n      <div class=\"big num\" id=\"resetCd\">--</div>\n      <div class=\"muted\" style=\"font-size:12.5px\">Every Sunday at 14:00 GMT &middot; <span id=\"resetLocal\"></span></div>\n    </div>\n  </div>\n</header>\n\n<div class=\"stale\" id=\"stale\"></div>\n<div class=\"hour\" id=\"hour\"></div>\n\n<section id=\"goalSec\">\n  <div class=\"sh\"><h2>Your goal</h2><span class=\"meta\">Defaults to the prize you are closest to. Click any prize in the table to aim for it.</span></div>\n  <div class=\"card goal\" id=\"goal\"></div>\n</section>\n\n<section>\n  <div class=\"sh\"><h2>Treasure Hour planner</h2><span class=\"meta\">What a number of chests is worth for your goal, with and without the Treasure Hour.</span></div>\n  <div class=\"card planner\" id=\"planner\"></div>\n</section>\n\n<section>\n  <div class=\"sh\"><h2>Odds this week</h2><span class=\"meta\" id=\"oddsMeta\"></span></div>\n  <div class=\"card rows\" id=\"rows\"></div>\n</section>\n\n<section>\n  <div class=\"sh\"><h2>Treasure Hours</h2><span class=\"meta\" id=\"schedMeta\"></span></div>\n  <div class=\"sched\" id=\"sched\"></div>\n  <p class=\"note\" id=\"schedNote\"></p>\n</section>\n\n<section>\n  <div class=\"sh\"><h2>This week</h2><span class=\"meta\">Since the last reset (Sunday 14:00 GMT).</span></div>\n  <div class=\"kpis\" id=\"kpis\"></div>\n  <div class=\"card days\" id=\"days\" style=\"margin-top:12px\"></div>\n</section>\n\n<section>\n  <div class=\"sh\"><h2>Matches</h2>\n    <div class=\"tools\"><input type=\"search\" id=\"q\" placeholder=\"Filter matches, treasures, teammates\"><span class=\"meta\" id=\"mcount\"></span></div>\n  </div>\n  <div class=\"card tw\"><table id=\"matches\"><thead><tr><th>Match</th><th>Mode</th><th>Place</th><th>Your chests</th><th>Teammates seen</th><th>Treasures</th></tr></thead><tbody></tbody></table></div>\n  <p class=\"note\">Treasure ownership of teammates is not stored in replays, so it is unavailable, not zero. Teammate chest counts are a minimum: Fortnite only records what happens near you.</p>\n</section>\n\n<section id=\"manualSec\">\n  <div class=\"sh\"><h2>Manual history</h2><span class=\"meta\">Entries added by hand in Weekly tools.</span></div>\n  <div class=\"card tw\"><table id=\"manual\"><thead><tr><th>When</th><th>Your chests</th><th>Teammates only</th><th>Note</th></tr></thead><tbody></tbody></table></div>\n</section>\n\n<footer>\n  <h3>How the numbers work</h3>\n  <p>For every chest you opened since the weekly reset, a prize has a 1-in-N chance. Chests opened inside a Treasure Hour use the boosted rate. The big percentage is the chance of at least one drop so far: <b>1 &minus; &prod;(1 &minus; rate)</b>, so it only rises when you open chests. The planner and the dashed bars show what extra chests would be worth. Except for Gizmos, you cannot get the same prize twice, so prizes you tick as owned in Weekly rewards are hidden. Pirate Shop Access uses all-time configured personal history and its owned tick is kept across weekly resets; other prizes remain weekly. Counts without exact opening times use base odds.</p>\n  <p>Odds are Epic's published per-chest rates (<span id=\"oddsSource\"></span>), constant except during Treasure Hours. <span id=\"oddsStatus\"></span> This is a probability, not a promise.</p>\n  <div class=\"made\">Made by 2xFlaw</div>\n</footer>\n</main>\n\n<script id=\"data\" type=\"application/json\">@@DATA@@</script>\n<script>\n(function(){\n\"use strict\";\nvar D=JSON.parse(document.getElementById(\"data\").textContent);\nfunction $(s){return document.querySelector(s)}\nfunction store(k,d){try{var v=localStorage.getItem(k);return v===null?d:JSON.parse(v)}catch(e){return d}}\nfunction save(k,v){try{localStorage.setItem(k,JSON.stringify(v))}catch(e){}}\nfunction esc(s){return String(s==null?\"\":s).replace(/[&<>\"']/g,function(c){return {\"&\":\"&amp;\",\"<\":\"&lt;\",\">\":\"&gt;\",\"\\\"\":\"&quot;\",\"'\":\"&#39;\"}[c]})}\nfunction n0(n){return Number(n).toLocaleString(\"en-US\",{maximumFractionDigits:0})}\nfunction pct(c){var v=c*100;if(v<=0)return \"0%\";return (v>=10?v.toFixed(1):v>=1?v.toFixed(2):v.toFixed(4))+\"%\"}\nfunction after(c,den,boost,n){var p=Math.min(boost/den,0.999999);return 1-(1-c)*Math.exp(n*Math.log1p(-p))}\nfunction toHalf(c,den){return c>=0.5?0:Math.log(0.5/(1-c))/Math.log1p(-1/den)}\nfunction pad(n){return String(n).padStart(2,\"0\")}\n\nvar pick=store(\"cc.pick\",D.pick||null), plan=store(\"cc.plan\",100), tz=store(\"cc.tz\",\"local\");\nvar W=D.windows.map(function(w){w.s=Date.parse(w.start);w.e=Date.parse(w.end);return w}).sort(function(a,b){return a.s-b.s});\nvar CHESTS=[25,50,100,200,400];\nvar IMG={}, NAME={};\nD.prizes.forEach(function(p){IMG[p.key]=p.img;NAME[p.key]=p.name});\nvar shownGoal=0, prevFill={}, state=\"\";\n\n// The boosts of the last two Treasure Hours are not announced: they count as normal odds (1x).\nfunction boostOf(w,k){return !w||w.unknown?1:k===\"gold-outfit\"?w.gold:k===\"pirate-shop-access\"?w.shop:k===\"vehicle-decal\"?w.vehicle:1}\nfunction windows(){var now=Date.now();return {live:W.find(function(w){return now>=w.s&&now<w.e})||null,next:W.find(function(w){return w.s>now})||null}}\nfunction fmt(ms,opt){var o=Object.assign({},opt);if(tz===\"utc\")o.timeZone=\"UTC\";return new Date(ms).toLocaleString(\"en-GB\",o)}\nfunction whenText(w){\n  var a=fmt(w.s,{weekday:\"short\",day:\"numeric\",month:\"short\",hour:\"2-digit\",minute:\"2-digit\",hour12:false});\n  var b=fmt(w.e,{hour:\"2-digit\",minute:\"2-digit\",hour12:false});\n  return a+\" \\u2013 \"+b+(tz===\"utc\"?\" GMT\":\"\");\n}\nfunction cd(ms){var s=Math.max(0,Math.floor(ms/1000));var d=Math.floor(s/86400),h=Math.floor(s%86400/3600),m=Math.floor(s%3600/60),x=s%60;return {d:d,h:h,m:m,s:x,total:s}}\nfunction badges(w){\n  if(w.unknown)return '<div class=\"bts\"><span class=\"bt unk\" title=\"Boosts are not announced yet\"><span class=\"q\">?</span>Boosts not announced yet</span></div>';\n  return '<div class=\"bts\">'+[[\"gold-outfit\",w.gold],[\"pirate-shop-access\",w.shop],[\"vehicle-decal\",w.vehicle]].map(function(x){\n    return '<span class=\"bt\" title=\"'+esc(NAME[x[0]]||x[0])+'\"><img alt=\"\" src=\"'+(IMG[x[0]]||\"\")+'\">'+x[1]+'&times;</span>'}).join(\"\")+'</div>';\n}\n\nfunction sparkle(host,count,seed){\n  host.innerHTML=\"\";\n  for(var i=0;i<count;i++){\n    var e=document.createElement(\"i\");e.textContent=\"\\u2726\";\n    var r=(Math.sin(seed*13.7+i*5.1)+1)/2, q=(Math.sin(seed*7.3+i*9.9)+1)/2;\n    e.style.left=(r*100)+\"%\";e.style.top=(q*100)+\"%\";\n    e.style.fontSize=(10+((i*7)%14))+\"px\";\n    e.style.animationDelay=(-(i*0.53)%3.2)+\"s\";e.style.animationDuration=(2.6+(i%4)*0.5)+\"s\";\n    host.appendChild(e);\n  }\n}\n\nfunction countUp(el,from,to,fmtFn){\n  if(window.matchMedia&&matchMedia(\"(prefers-reduced-motion: reduce)\").matches){el.textContent=fmtFn(to);return}\n  var t0=performance.now(),dur=900;\n  function step(t){var k=Math.min(1,(t-t0)/dur),e=1-Math.pow(1-k,3);el.textContent=fmtFn(from+(to-from)*e);if(k<1)requestAnimationFrame(step)}\n  requestAnimationFrame(step);\n}\n\nfunction ordered(live){\n  var P=D.prizes.map(function(p){var q=Object.assign({},p);q.boost=boostOf(live,p.key);return q});\n  var open=P.filter(function(p){return !p.owned});\n  var main=open.filter(function(p){return p.key!==\"gizmo\"}).sort(function(a,b){return (b.chance-a.chance)||(a.den-b.den)});\n  var giz=open.filter(function(p){return p.key===\"gizmo\"});\n  return {list:main.concat(giz),cands:main,hidden:P.length-open.length,all:P};\n}\n\nfunction render(){\n  var w=windows(), o=ordered(w.live);\n  var goal=pick?o.cands.find(function(p){return p.key===pick}):null;\n  var auto=!goal; if(!goal)goal=o.cands[0]||null;\n\n  // ----- hour strip -----\n  var hour=$(\"#hour\");\n  if(w.live){hour.className=\"hour live\";hour.innerHTML='<div><span class=\"chip amber\"><span class=\"dot\"></span>Treasure Hour live</span>'+(w.live.unknown?' <span class=\"chip rose\">Final</span>':'')+'<div class=\"t\" style=\"margin-top:8px\">'+(w.live.unknown?'Boosts not announced yet':'Boosted odds are on now')+'</div><div class=\"muted\">'+esc(whenText(w.live))+'</div>'+badges(w.live)+'</div><div style=\"text-align:right\"><div class=\"lbl\">Ends in</div><div class=\"cd num\" id=\"hourCd\"></div></div>'}\n  else if(w.next){hour.className=\"hour\";hour.innerHTML='<div><span class=\"chip\">Next Treasure Hour</span>'+(w.next.unknown?' <span class=\"chip rose\">Final</span>':'')+'<div class=\"t\" style=\"margin-top:8px\">'+esc(whenText(w.next))+'</div>'+badges(w.next)+'</div><div style=\"text-align:right\"><div class=\"lbl\">Starts in</div><div class=\"cd num\" id=\"hourCd\"></div></div>'}\n  else{hour.className=\"hour\";hour.innerHTML='<div><span class=\"chip mute\">No more Treasure Hours</span><div class=\"muted\" style=\"margin-top:8px\">The last Treasure Hour has ended.</div></div>'}\n\n  // ----- goal -----\n  var g=$(\"#goal\");\n  if(!goal){\n    g.className=\"card goal\";g.style.display=\"block\";\n    g.innerHTML='<div class=\"empty\">Everything is ticked as owned. Fresh prizes arrive at the reset.</div>';\n  }else{\n    var boosted=goal.boost>1;\n    g.className=\"card goal\"+(boosted?\" boost\":\"\");g.style.display=\"\";\n    var half=toHalf(goal.chance,goal.den);\n    var gainNow=after(goal.chance,goal.den,goal.boost,plan)-goal.chance;\n    var gainNorm=after(goal.chance,goal.den,1,plan)-goal.chance;\n    g.innerHTML=\n      '<div class=\"pic\"><img alt=\"\" src=\"'+goal.img+'\"><span id=\"picSp\"></span></div>'+\n      '<div><div class=\"gtop\"><span class=\"chip '+(auto?\"\":\"amber\")+'\">'+(auto?\"Next closest\":\"Your pick\")+'</span>'+(boosted?'<span class=\"chip amber\"><span class=\"dot\"></span>Treasure Hour '+goal.boost+'&times;</span>':'')+'<button class=\"btn '+(auto?\"on\":\"\")+'\" id=\"btnAuto\" type=\"button\">Auto: next closest</button></div>'+\n      '<h3>'+esc(goal.name)+'</h3>'+\n      '<div class=\"gpct num\" id=\"gpct\">0%</div><div class=\"muted\" style=\"margin-top:-2px\">cumulative chance '+(goal.allTime?\"all time\":\"this week\")+'</div>'+\n      '<div class=\"gstats\"><span><b>1 in '+n0(goal.den)+'</b> per chest</span><span><b>'+n0(goal.allTime?goal.allChests:D.chests)+'</b> chests '+(goal.allTime?\"all time\":\"opened this week\")+'</span><span>'+(half<=0?'<b style=\"color:var(--mint)\">Past the 50% mark</b>':'About <b>'+n0(Math.ceil(half))+'</b> more chests to reach 50%')+'</span></div>'+\n      '<div class=\"gproj\" style=\"color:'+(boosted?\"var(--amber)\":\"var(--muted)\")+'\">If you open '+n0(plan)+' more chests: +'+pct(gainNorm)+' normally'+(boosted?' &middot; <b>+'+pct(gainNow)+' during the Treasure Hour</b>':'')+'</div>'+\n      '<div class=\"path\" id=\"path\"><div class=\"track\"><div class=\"pfill\" id=\"pfill\"></div></div></div></div>';\n    var path=$(\"#path\");\n    [[0,\"0\"],[0.25,\"25%\"],[0.5,\"50%\"],[0.75,\"75%\"],[1,\"100%\"]].forEach(function(m){\n      var hit=m[0]>0&&goal.chance>=m[0];\n      var d=document.createElement(\"span\");d.className=\"ms\"+(hit?\" hit\":\"\");d.style.left=(m[0]*100)+\"%\";path.appendChild(d);\n      var l=document.createElement(\"span\");l.className=\"ml\"+(hit?\" hit\":\"\");l.style.left=(m[0]*100)+\"%\";l.textContent=m[1];path.appendChild(l);\n    });\n    if(goal.chance>0.003&&goal.chance<1){var mk=document.createElement(\"span\");mk.className=\"mk\";mk.style.left=\"0%\";mk.id=\"mk\";path.appendChild(mk)}\n    sparkle($(\"#picSp\"),7,goal.key.length);\n    var sp=$(\"#picSp\");sp.style.cssText=\"position:absolute;inset:-14px;display:block\";\n    requestAnimationFrame(function(){requestAnimationFrame(function(){\n      $(\"#pfill\").style.width=(goal.chance*100)+\"%\";\n      var mk2=$(\"#mk\");if(mk2)mk2.style.left=(goal.chance*100)+\"%\";\n    })});\n    countUp($(\"#gpct\"),shownGoal,goal.chance,pct);shownGoal=goal.chance;\n    $(\"#btnAuto\").onclick=function(){pick=null;save(\"cc.pick\",null);render()};\n  }\n\n  // ----- planner -----\n  var pl=$(\"#planner\");\n  if(!goal){pl.style.display=\"none\"}\n  else{\n    pl.style.display=\"\";\n    var win=w.live||w.next;\n    var b=boostOf(win,goal.key);\n    var nn=after(goal.chance,goal.den,1,plan), bb=after(goal.chance,goal.den,b,plan);\n    var statusChip=w.live?'<span class=\"chip amber\"><span class=\"dot\"></span>Live now</span>':(w.next?'<span class=\"chip\">Next Treasure Hour</span>':'<span class=\"chip mute\">No more Treasure Hours</span>');\n    var boostBlock=b>1\n      ?'<div><div class=\"lbl\" style=\"color:var(--amber)\">Treasure Hour '+b+'&times;</div><div class=\"pv hot num\" id=\"pvB\">0%</div><div style=\"color:var(--amber)\">+'+pct(bb-goal.chance)+'</div></div>'\n      :(win&&win.unknown\n        ?'<div class=\"unkbox\"><span class=\"q\">?</span><span>Boosts for this Treasure Hour are not announced yet.</span></div>'\n        :'<div><div class=\"muted\">No Treasure Hour boost for this prize.</div></div>');\n    pl.innerHTML=\n      '<div class=\"ptop\"><div>'+statusChip+'</div><div class=\"muted\">'+(win?esc(whenText(win))+(win.unknown?\" (final)\":\"\"):\"\")+'</div></div>'+\n      '<div class=\"pgrid\"><div><div class=\"lbl\">If you open this many chests</div><div class=\"chests\" id=\"chests\"></div></div>'+\n      '<div><div class=\"lbl\">Normal rate</div><div class=\"pv num\" id=\"pvN\">0%</div><div class=\"muted\">+'+pct(nn-goal.chance)+'</div></div>'+\n      boostBlock+'</div>';\n    var ch=$(\"#chests\");\n    CHESTS.forEach(function(n){var bt=document.createElement(\"button\");bt.type=\"button\";bt.className=\"btn\"+(n===plan?\" on\":\"\");bt.textContent=n0(n);bt.onclick=function(){plan=n;save(\"cc.plan\",n);render()};ch.appendChild(bt)});\n    countUp($(\"#pvN\"),goal.chance,nn,pct);\n    if(b>1)countUp($(\"#pvB\"),goal.chance,bb,pct);\n  }\n\n  // ----- odds table -----\n  var rows=$(\"#rows\");\n  var html='<div class=\"rhead lbl\"><span>Prize</span><span>Cumulative odds this week &middot; dashed = if you open '+n0(plan)+' chests</span><span style=\"text-align:right\">Chance</span></div>';\n  if(!o.list.length)html+='<div class=\"empty\">Everything is ticked as owned</div>';\n  o.list.forEach(function(p){\n    var sel=goal&&p.key===goal.key, gz=p.key===\"gizmo\", hot=p.boost>1;\n    var ah=after(p.chance,p.den,p.boost,plan);\n    var gl=Math.min(100,p.chance*100), gw=Math.max(0,Math.min(100,ah*100)-gl);\n    html+='<div class=\"row'+(sel?\" sel\":\"\")+(gz?\" gz\":\"\")+'\" data-k=\"'+p.key+'\" tabindex=\"'+(gz?-1:0)+'\" role=\"'+(gz?\"row\":\"button\")+'\">'+\n      '<div class=\"who\"><img alt=\"\" src=\"'+p.img+'\"><div style=\"min-width:0\"><div class=\"nm\">'+esc(p.name)+'</div><div class=\"sb\">1 in '+n0(p.den)+' per chest'+(gz?' &middot; can repeat':'')+(hot?' <span class=\"hot\">&middot; '+p.boost+'&times; now</span>':'')+'</div></div></div>'+\n      '<div class=\"bar\"><div class=\"gh'+(hot?\" hot\":\"\")+'\" style=\"left:'+gl+'%;width:'+gw+'%\"></div><div class=\"fl\" data-w=\"'+(p.chance*100)+'\" style=\"width:'+((prevFill[p.key]||0))+'%\"></div></div>'+\n      '<div class=\"pc\"><div class=\"p num\">'+pct(p.chance)+'</div><div class=\"q2 num\">\\u2192 '+pct(ah)+'</div></div></div>';\n  });\n  rows.innerHTML=html;\n  requestAnimationFrame(function(){requestAnimationFrame(function(){\n    rows.querySelectorAll(\".fl\").forEach(function(f){var k=f.parentNode.parentNode.getAttribute(\"data-k\");var v=parseFloat(f.getAttribute(\"data-w\"));f.style.width=v+\"%\";prevFill[k]=v})\n  })});\n  rows.querySelectorAll(\".row\").forEach(function(r){\n    var k=r.getAttribute(\"data-k\");if(k===\"gizmo\")return;\n    function choose(){pick=k;save(\"cc.pick\",k);render();$(\"#goalSec\").scrollIntoView({behavior:\"smooth\",block:\"start\"})}\n    r.onclick=choose;r.onkeydown=function(e){if(e.key===\"Enter\"||e.key===\" \"){e.preventDefault();choose()}};\n  });\n  $(\"#oddsMeta\").textContent=o.hidden>0?(o.hidden+\" owned prize\"+(o.hidden===1?\"\":\"s\")+\" hidden \\u00b7 untick in Weekly rewards to bring back\"):\"All prizes still to find\";\n  renderSchedule(w);\n  tick();\n}\n\nfunction renderSchedule(w){\n  var now=Date.now(), sc=$(\"#sched\"), h=\"\";\n  W.forEach(function(x){\n    var live=now>=x.s&&now<x.e, past=now>=x.e, nxt=w.next&&w.next===x;\n    h+='<div class=\"card win'+(live?\" live\":\"\")+(past?\" past\":\"\")+'\"><div class=\"when\">'+esc(whenText(x))+'</div>'+badges(x)+\n      '<div class=\"row2\"><span>'+(live?'<span class=\"chip amber\"><span class=\"dot\"></span>Live</span>':past?'<span class=\"chip mute\">Ended</span>':nxt?'<span class=\"chip\">Next</span>':'<span class=\"chip mute\">Upcoming</span>')+(x.unknown?' <span class=\"chip rose\">Final</span>':' <span class=\"chip mint\">Confirmed</span>')+'</span>'+\n      '<span class=\"muted num\" data-cd=\"'+(live?x.e:x.s)+'\" data-live=\"'+(live?1:0)+'\"></span></div></div>';\n  });\n  sc.innerHTML=h||'<div class=\"muted\">No Treasure Hours in the odds file.</div>';\n  var known=W.filter(function(x){return !x.unknown&&x.e>now}).length, last=W.filter(function(x){return x.unknown&&x.e>now}).length;\n  $(\"#schedMeta\").innerHTML=(known||last)?(known+\" with announced boosts\"+(last?\" \\u00b7 \"+last+\" final, boosts not announced\":\"\")+\" \\u00b7 times in <b>\"+(tz===\"utc\"?\"GMT\":\"your local time\")+\"</b>\"):(\"Times in <b>\"+(tz===\"utc\"?\"GMT\":\"your local time\")+\"</b>\");\n  $(\"#schedNote\").textContent=W.some(function(x){return x.unknown})?\"The Treasure Hours marked Final are confirmed to be the last two. What they boost is not announced yet, so the report counts them as normal odds (no boost) until it is.\":\"\";\n}\n\nfunction tick(){\n  var now=Date.now();\n  var r=cd(D.resetUtc-now);\n  $(\"#resetCd\").textContent=r.d+\"d \"+pad(r.h)+\"h \"+pad(r.m)+\"m \"+pad(r.s)+\"s\";\n  var w=windows();\n  var key=(w.live?\"L\"+w.live.s:\"\")+\"|\"+(w.next?\"N\"+w.next.s:\"\");\n  if(state&&key!==state){state=key;render();return}\n  state=key;\n  var hc=$(\"#hourCd\");\n  if(hc){var t=w.live?w.live.e-now:(w.next?w.next.s-now:0);var c=cd(t);hc.textContent=w.live?(pad(Math.floor(c.total/3600))+\":\"+pad(c.m)+\":\"+pad(c.s)):(c.d+\"d \"+pad(c.h)+\"h \"+pad(c.m)+\"m \"+pad(c.s)+\"s\")}\n  document.querySelectorAll(\"[data-cd]\").forEach(function(e){\n    var t=parseFloat(e.getAttribute(\"data-cd\"))-now;if(t<=0){e.textContent=\"\";return}\n    var c=cd(t);e.textContent=(e.getAttribute(\"data-live\")===\"1\"?\"ends in \":\"in \")+(c.d>0?c.d+\"d \":\"\")+pad(c.h)+\":\"+pad(c.m)+\":\"+pad(c.s);\n  });\n  var age=(now-D.generatedUtc)/60000, st=$(\"#stale\");\n  if(age>30){st.style.display=\"block\";st.textContent=\"This report was made \"+(age>=120?Math.floor(age/60)+\" hours\":Math.floor(age)+\" minutes\")+\" ago. Your chest counts may be out of date: open it again from the app (Weekly tools \\u203a Weekly report) to refresh.\"}\n  else st.style.display=\"none\";\n}\n\nfunction chrome(){\n  $(\"#chipProfile\").textContent=D.profile||\"Profile\";\n  $(\"#chipGen\").textContent=\"Updated \"+fmt(D.generatedUtc,{day:\"numeric\",month:\"short\",hour:\"2-digit\",minute:\"2-digit\",hour12:false});\n  $(\"#resetLocal\").textContent=fmt(D.resetUtc,{weekday:\"short\",day:\"numeric\",month:\"short\",hour:\"2-digit\",minute:\"2-digit\",hour12:false})+(tz===\"utc\"?\" GMT\":\" your time\");\n  $(\"#btnTz\").textContent=\"Times: \"+(tz===\"utc\"?\"GMT\":\"Local\");\n  sparkle($(\"#heroSparks\"),14,3);\n  var o=D.odds||{};\n  $(\"#oddsSource\").innerHTML=o.source?'<a href=\"'+esc(o.source)+'\" target=\"_blank\" rel=\"noopener\">fortnite.bot</a>':\"fortnite.bot\";\n  $(\"#oddsStatus\").textContent=(o.status||\"\")+(o.captured?\" Treasure Hour list captured \"+fmt(Date.parse(o.captured),{day:\"numeric\",month:\"short\",year:\"numeric\"})+\".\":\"\");\n}\n\nfunction renderStats(){\n  var k=D.kpis||{}, h=\"\";\n  [[\"Your chests this week\",k.mine,\"var(--acc)\",\"opened or destroyed by you\"],\n   [\"Teammates seen\",k.mates,\"var(--amber)\",\"minimum, limited to replay visibility\"],\n   [\"Squad total\",k.mine+k.mates,\"var(--mint)\",\"you + teammates seen\"],\n   [\"Manual entries\",k.manual,\"var(--rose)\",\"chests you added by hand\"]].forEach(function(x){\n    h+='<div class=\"card kpi\" style=\"--c:'+x[2]+'\"><div class=\"lbl\">'+x[0]+'</div><div class=\"v num\">'+n0(x[1]||0)+'</div><div class=\"s\">'+x[3]+'</div></div>'});\n  $(\"#kpis\").innerHTML=h;\n  var max=Math.max.apply(null,D.daily.map(function(d){return d.chests}).concat([1]));\n  var dh=\"\", todayIdx=-1, now=Date.now();\n  D.daily.forEach(function(d,i){if(now>=Date.parse(d.start))todayIdx=i});\n  D.daily.forEach(function(d,i){\n    var lab=new Date(Date.parse(d.start)).toLocaleDateString(\"en-GB\",{weekday:\"short\",timeZone:tz===\"utc\"?\"UTC\":undefined});\n    dh+='<div class=\"day'+(i===todayIdx?\" today\":\"\")+'\"><div class=\"b'+(d.chests?\"\":\" zero\")+'\" data-h=\"'+(d.chests/max*100)+'\">'+(d.chests?'<span>'+d.chests+'</span>':'')+'</div><div class=\"l\">'+lab+'</div></div>'});\n  $(\"#days\").innerHTML=dh;\n  requestAnimationFrame(function(){requestAnimationFrame(function(){document.querySelectorAll(\".day .b\").forEach(function(b){b.style.height=Math.max(3,parseFloat(b.getAttribute(\"data-h\"))*0.78)+\"%\"})})});\n}\n\nfunction renderMatches(){\n  var q=($(\"#q\").value||\"\").toLowerCase().trim(), tb=$(\"#matches tbody\"), h=\"\", shown=0;\n  D.matches.forEach(function(m){\n    var text=(m.mode+\" \"+m.place+\" \"+m.treasures.join(\" \")+\" \"+m.mates.map(function(t){return t.name}).join(\" \")).toLowerCase();\n    if(q&&text.indexOf(q)<0)return;shown++;\n    var when=m.start?fmt(Date.parse(m.start),{day:\"numeric\",month:\"short\",hour:\"2-digit\",minute:\"2-digit\",hour12:false}):\"\";\n    h+='<tr class=\"'+(m.win?\"win-row\":\"\")+'\"><td class=\"num\" style=\"white-space:nowrap\">'+esc(when)+'</td><td>'+esc(m.mode)+'</td><td>'+esc(m.place)+'</td><td class=\"num\"><b>'+m.chests+'</b></td>'+\n      '<td>'+(m.mates.length?'<b class=\"num\">\\u2265 '+m.matesTotal+'</b><div class=\"muted\">'+m.mates.map(function(t){return esc(t.name)+\": \"+t.chests}).join(\", \")+'</div>':'<span class=\"muted\">\\u2014</span>')+'</td>'+\n      '<td>'+(m.treasures.length?esc(m.treasures.join(\", \")):'<span class=\"muted\">\\u2014</span>')+'</td></tr>'});\n  tb.innerHTML=h||'<tr><td colspan=\"6\" class=\"muted\" style=\"text-align:center;padding:26px\">No matches found.</td></tr>';\n  $(\"#mcount\").textContent=shown+\" of \"+D.matches.length+\" shown\";\n  var mb=$(\"#manual tbody\"), mh=\"\";\n  D.manual.forEach(function(e){mh+='<tr><td class=\"num\" style=\"white-space:nowrap\">'+esc(fmt(Date.parse(e.at),{day:\"numeric\",month:\"short\",year:\"numeric\",hour:\"2-digit\",minute:\"2-digit\",hour12:false}))+'</td><td class=\"num\">'+e.mine+'</td><td class=\"num\">'+e.squad+'</td><td>'+esc(e.note)+'</td></tr>'});\n  mb.innerHTML=mh;\n  $(\"#manualSec\").style.display=D.manual.length?\"\":\"none\";\n}\n\nfunction summary(){\n  var w=windows(), o=ordered(w.live), goal=(pick?o.cands.find(function(p){return p.key===pick}):null)||o.cands[0];\n  var t=\"2xFlaw's Chest Counter \\u2013 weekly chances (\"+(D.profile||\"\")+\")\\n\";\n  t+=n0(D.chests)+\" chests opened since the reset.\\n\";\n  if(goal)t+=\"Goal: \"+goal.name+\" \\u2013 \"+pct(goal.chance)+(goal.allTime?\" all time\":\" so far\")+\" (1 in \"+n0(goal.den)+\" per chest).\\n\";\n  o.list.slice(0,6).forEach(function(p){t+=\"\\u2022 \"+p.name+\": \"+pct(p.chance)+\" (1 in \"+n0(p.den)+\")\\n\"});\n  if(w.live)t+=\"Treasure Hour is live now!\\n\";else if(w.next)t+=\"Next Treasure Hour: \"+whenText(w.next)+(w.next.unknown?\" (boosts not announced)\":\"\")+\"\\n\";\n  return t;\n}\n\n$(\"#btnTz\").onclick=function(){tz=tz===\"utc\"?\"local\":\"utc\";save(\"cc.tz\",tz);chrome();renderStats();renderMatches();render()};\n$(\"#btnCopy\").onclick=function(){\n  var b=$(\"#btnCopy\"), t=summary();\n  function done(){b.textContent=\"Copied \\u2713\";setTimeout(function(){b.textContent=\"Copy summary\"},1600)}\n  if(navigator.clipboard&&navigator.clipboard.writeText){navigator.clipboard.writeText(t).then(done,function(){fallback()})}else fallback();\n  function fallback(){var a=document.createElement(\"textarea\");a.value=t;document.body.appendChild(a);a.select();try{document.execCommand(\"copy\")}catch(e){}document.body.removeChild(a);done()}\n};\n$(\"#q\").addEventListener(\"input\",renderMatches);\n\nchrome();renderStats();renderMatches();render();\nsetInterval(tick,1000);\n})();\n</script>\n</body>\n</html>\n".Replace("/*FONTS*/", FontFaces()).Replace("@@BANNER@@", Banner()).Replace("@@AVATAR@@", Avatar())
			.Replace("@@DATA@@", JsonSerializer.Serialize(value, Json));
		string text2 = Path.Combine(store.Folder, "2xflaw-report.html");
		File.WriteAllText(text2, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		return text2;
		void AddAt(DateTime? when, int count)
		{
			if (when.HasValue)
			{
				DateTime valueOrDefault = when.GetValueOrDefault();
				if (!(valueOrDefault < start) && !(valueOrDefault >= end))
				{
					int num = Math.Clamp((int)Math.Floor((valueOrDefault - start).TotalDays), 0, 6);
					daily[num] += count;
				}
			}
		}
	}
}
