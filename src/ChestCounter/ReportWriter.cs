using System.Globalization;
using System.Net;
using System.Text;

namespace ChestCounter;

public static class Pretty
{
    public static string Playlist(string? playlist)
    {
        if (string.IsNullOrEmpty(playlist))
        {
            return L.T("mode inconnu", "unknown mode", "modo desconocido");
        }

        var p = playlist.ToLowerInvariant();
        var parts = new List<string>();
        if (p.Contains("habanero") || p.Contains("ranked")) parts.Add(L.T("Classée", "Ranked", "Clasificatoria"));
        if (p.Contains("nobuild")) parts.Add(L.T("Zéro construction", "Zero Build", "Cero construcción"));
        if (p.Contains("solo")) parts.Add(L.T("Solo", "Solo", "Solitario"));
        else if (p.Contains("duo")) parts.Add(L.T("Duo", "Duos", "Dúos"));
        else if (p.Contains("trio")) parts.Add(L.T("Trio", "Trios", "Tríos"));
        else if (p.Contains("squad")) parts.Add(L.T("Section", "Squads", "Escuadrones"));
        return parts.Count > 0 ? string.Join(" · ", parts) : playlist.Replace("Playlist_", "");
    }

    public static string Placement(MatchRecord m)
    {
        if (m.Placement is not int place || place <= 0)
        {
            return L.T("place inconnue", "unknown placement", "puesto desconocido");
        }
        if (place == 1)
        {
            return L.T("Top 1 !", "Victory Royale!", "¡Victoria magistral!");
        }
        return m.TotalPlayers is int total && total > 0 ? $"{L.Ordinal(place)} / {total}" : L.Ordinal(place);
    }

    public static string Clock(float seconds)
    {
        var s = (int)seconds;
        return $"{s / 60}:{s % 60:00}";
    }

    /// <summary>"à 7:16 · Coffre rare" (certain) or "vers 5:04 · Coffre rare (probable)".</summary>
    public static string Obtained(TreasureFind f)
    {
        if (f.ObtainedAtSeconds is not float t)
        {
            return L.T("moment et source inconnus", "time and source unknown", "momento y origen desconocidos");
        }
        var when = f.TimeExact ? $"{L.T("à", "at", "a los")} {Clock(t)}" : $"{L.T("vers", "around", "hacia los")} {Clock(t)}";
        return f.SourceCertain ? $"{when} · {f.SourceLabel}" : $"{when} · {f.SourceLabel} ({Likely})";
    }

    public static string Likely => L.T("probable", "likely", "probable");

    public static string ItemName(string item)
    {
        var name = item;
        foreach (var prefix in new[] { "WID_", "Athena", "AmmoData" })
        {
            name = name.Replace(prefix, "");
        }
        return name.Replace("_", " ").Trim();
    }
}

/// <summary>Replays analyzed so far / to analyze, shown as a loading card in the report.</summary>
public sealed record ScanProgress(int Done, int Total);

public static class ReportWriter
{
    public static readonly DateTime SessionStartUtc = DateTime.UtcNow;

    /// <summary>Set while several replays are being analyzed: the report shows a loading card and refreshes faster.</summary>
    public static volatile ScanProgress? Progress;

    /// <summary>A match belongs to this session if it ended after the tool was started (backlog replays do not count).</summary>
    public static bool IsThisSession(MatchRecord m)
    {
        var start = m.MatchStartUtc ?? m.AnalyzedAtUtc;
        if (start.Kind == DateTimeKind.Unspecified)
        {
            start = DateTime.SpecifyKind(start, DateTimeKind.Utc);
        }
        var end = start.ToUniversalTime().AddSeconds(m.DurationSeconds ?? 0);
        return end >= SessionStartUtc.AddMinutes(-2);
    }
    private static CultureInfo Fr => L.Culture;

    public static void WriteAll(Store store, Config config, TrackerData data)
    {
        try
        {
            var view = Profiles.View(data, config);
            WriteAtomic(store.ReportPath, Report(config, view, Profiles.List(data)));
            // The OBS page keeps what the overlay shows (a match in progress stays "live"), in the chosen style.
            OverlayStyle = config.OverlayStyle;
            WriteOverlay(store, LastOverlay ?? OverlayModel.Build(view, null, false, config.ShowSquadTotal));
            WriteAtomic(store.CsvPath, Csv(data));
        }
        catch (Exception ex)
        {
            Console.WriteLine(L.T($"Impossible d'écrire le rapport : {ex.Message}", $"Cannot write the report: {ex.Message}", $"No se puede escribir el informe: {ex.Message}"));
        }
    }

    private static void WriteAtomic(string path, string content)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content, new UTF8Encoding(false));
        File.Move(tmp, path, overwrite: true);
    }

    private static string H(string? s) => WebUtility.HtmlEncode(s ?? "");

    /// <summary>Start of a match as shown (local time or UTC, from the settings).</summary>
    private static DateTime When(MatchRecord m) => L.Shown(m.MatchStartUtc ?? m.AnalyzedAtUtc);

    /// <summary>A short date and time ("Nov 8, 6:00 AM"), shown in local time or UTC.</summary>
    private static string ShortWhen(DateTime utc) => L.Shown(utc).ToString(L.T("d MMM ", "MMM d, ", "d MMM ") + L.TimePattern(), Fr) + L.Zone;

    private const string Style = """
        :root{color-scheme:dark;--bg:#164786;--ink:#f5f9ff;--ink2:#fff;--muted:#d4e7ff;--title:#fff;--card:#1b4a89;--cink:#f5f9ff;--cmuted:#d3e7ff;--line:#5486bf;--track:#245d9d;--gold:#9edcff;--star:#e0bcff;--team:#83e7d8;--warn:#ffb9a7;--warnbg:#623a58;--ui:"Heading Bold","Chest Digits","Segoe UI",system-ui,sans-serif;--display:"Heading Now","Chest Digits","Heading Bold","Segoe UI",sans-serif;scrollbar-width:none}
        *{box-sizing:border-box}
        body{margin:0;background:radial-gradient(ellipse at 53% 39%,#2082cb,transparent 76%) fixed,linear-gradient(145deg,#164585 8%,#155cb0 61%,#184486) fixed;color:var(--ink);font:15px/1.5 var(--ui)}
        main{max-width:1120px;margin:0 auto;padding:36px 16px 56px;display:flex;flex-direction:column;gap:28px}
        .heading,.pn,.tile .k,.wt,.hname .k,.ciname,.tsw,.pager button,.odds .k{font-family:var(--display);font-weight:800;letter-spacing:.065em;text-transform:uppercase}
        .num{font-family:"Chest Digits",var(--ui);font-variant-numeric:tabular-nums}
        header{display:grid;justify-items:center;text-align:center;gap:9px;padding-bottom:16px}
        h1.heading{margin:0;font:clamp(36px,8vw,74px)/1 "Wordmark",var(--ui);letter-spacing:.048em;color:var(--title);-webkit-text-stroke:7px #05050b;paint-order:stroke fill}
        .who{font-size:15px;color:var(--muted);margin-top:4px}.who b{color:var(--ink2)}
        .muted{color:var(--muted)}
        h2{margin:0 0 10px;font-size:26px;line-height:1.2;color:var(--title);text-align:center}
        .tiles{display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1.9fr);gap:12px}
        .stats{min-width:0;display:grid;gap:10px}
        .metrics{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:10px}
        .extras{display:flex;flex-wrap:wrap;gap:10px}
        .tile{min-width:0;container-type:inline-size;background:var(--card);color:var(--cink);border:1px solid var(--line);border-radius:13px;padding:12px;display:flex;flex-direction:column;justify-content:center;align-items:center;gap:8px;text-align:center}
        .tile .v{font-size:min(var(--number-size,3.65rem),calc(137cqi / max(3,var(--digits))));font-weight:700;line-height:1.04;letter-spacing:-.018em}
        .tile .k{font-size:clamp(11px,9.5cqi,17px);line-height:1.2;color:#e4f0ff}
        .metrics .tile{min-height:144px;background:linear-gradient(156deg,#245d99,#1a4480 82%)}
        .metrics .k{display:grid;place-items:center;min-height:2.4em}
        .extras .tile{--number-size:2.125rem;flex:1 1 156px;min-height:97px;gap:3px}
        .extras .tile .k{font-size:13px}
        .tile .l{font-size:13px;color:var(--cmuted)}
        .tile .s{font-size:12px;color:var(--cmuted)}
        .tile.hero{--number-size:5.3rem;position:relative;min-height:180px;padding:36px 16px 24px;gap:12px;background:linear-gradient(145deg,#23528e,#2466ae 61%,#2a87cc);color:#eef8ff;border-color:#8dc6f6}
        .editbtn{display:none;position:absolute;top:10px;right:10px;width:36px;height:36px;padding:0;border-radius:13px;border:1px solid #8bd4ff;background:transparent;color:#fff;cursor:pointer;place-items:center}
        .editbtn:hover{background:rgba(147,216,255,.2)}.editbtn:focus-visible{outline:3px solid #fff;outline-offset:2px}
        html.incounter .editbtn{display:grid}
        .tile.hero .k{font-size:clamp(14px,6.6cqi,20px);letter-spacing:.09em;padding-inline:24px}
        .tile.hero .l{color:#e8f4ff}
        @media(max-width:720px){.tiles{grid-template-columns:1fr}.metrics .tile{min-height:114px}}
        @media(max-width:410px){.metrics{gap:6px}.metrics .tile{padding-inline:5px}}
        .card{background:var(--card);color:var(--cink);border:1px solid var(--line);border-radius:13px;padding:6px 16px;overflow-x:auto}
        .two{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:20px}
        @media (max-width:820px){.two{grid-template-columns:1fr}}
        table{width:100%;border-collapse:collapse;font-variant-numeric:tabular-nums}
        th,td{text-align:left;padding:9px 8px;border-bottom:1px solid var(--line);vertical-align:middle}
        th{white-space:nowrap;font:600 12px var(--display);text-transform:uppercase;letter-spacing:.065em;color:var(--cmuted)}
        tr:last-child td{border-bottom:none}
        td.n{text-align:right;font-weight:700}
        td.nw{white-space:nowrap}
        .big{font-size:20px}
        .items{color:var(--cmuted);font-size:13px}
        /* Your chances */
        .ch-intro{margin:0 auto 12px;color:var(--muted);font-size:14px;max-width:760px;text-align:center}
        .chances2{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:12px}
        @media (max-width:820px){.chances2{grid-template-columns:1fr}}
        .hcard{background:var(--card);color:var(--cink);border:1px solid var(--line);border-radius:13px;padding:14px 16px;display:flex;flex-direction:column;align-items:center;text-align:center;gap:6px}
        .hcard.got{border-color:var(--team);box-shadow:inset 6px 0 0 var(--team);padding-left:22px}
        .hcard.boosted{border-color:#e2702f;box-shadow:inset 6px 0 0 #e2702f;padding-left:22px}
        .htop{display:flex;flex-direction:column;align-items:center;gap:6px}
        .cthumb{width:44px;height:44px;border-radius:6px;object-fit:cover;background:var(--track);flex:none}
        .hname{flex:1;min-width:0}
        .hname .k{font-size:12px;letter-spacing:.12em;text-transform:uppercase;color:var(--cmuted)}
        .ciname{font-weight:700;font-size:15px}
        .hval{display:flex;align-items:center;justify-content:center;gap:10px;flex-wrap:wrap}
        .hval .big{font-size:40px;font-weight:700;line-height:1.05}
        .hexp{font-size:14px}
        .hcard .s{font-size:13px;color:var(--cmuted)}
        .cfoot{font-size:13px;color:var(--cmuted);border-top:1px dashed var(--line);padding-top:6px;margin-top:2px}
        .halfway{margin-top:2px}.hrow{display:flex;justify-content:space-between;gap:8px;font-size:13px;color:var(--cmuted)}
        .tag{display:inline-block;padding:2px 9px;border-radius:999px;font:700 11px var(--ui);letter-spacing:.08em;text-transform:uppercase;flex:none}
        .tag.weekly{background:#2c5d8a;color:#fff}
        .tag.season{background:#6c3fb5;color:#fff}
        .chances2 .gotbadge,.ctable .gotbadge{display:none;padding:2px 8px;border-radius:999px;background:var(--team);color:#fff;font:700 11px var(--ui);margin-left:8px;white-space:nowrap}
        .got .gotbadge{display:inline-block!important}
        .vchip{display:inline-block;padding:2px 9px;border-radius:999px;font:600 12px var(--ui);white-space:nowrap}
        .vchip.vunlikely{background:#2a4a7c;color:#c2d8f6}
        .vchip.unlikely{background:#f0d9a8;color:#7a5410}
        .vchip.possible{background:#f6cf86;color:#6d4300}
        .vchip.likely{background:#cfe3c0;color:#2f5a1c}
        .vchip.vlikely{background:#b7dcae;color:#1f4d14}
        .vchip.certain{background:#2f7d4f;color:#fff}
        .cbar{display:block;height:6px;background:var(--track);border-radius:3px;overflow:hidden;margin-top:5px}
        .cbar.wide{width:100%}
        .cbar i{display:block;height:6px;border-radius:3px}
        .cbar i.vunlikely,.cbar i.unlikely{background:#c9a24f}.cbar i.possible{background:#d68a1c}
        .cbar i.likely,.cbar i.vlikely{background:#5f9c4a}.cbar i.certain{background:#2f7d4f}.cbar i.gold{background:#b8f67b}
        .cgroup{margin-top:18px}
        .ghead{display:flex;align-items:center;gap:10px;margin:0 0 8px}
        .ghead .gt{font-weight:700;font-size:16px;color:var(--ink2)}
        .ghead .gs{font-size:13px;color:var(--muted)}
        .cdbox{margin-left:auto;text-align:right;flex:none}
        .cdbox .cdl{display:block;font-size:11px;letter-spacing:.1em;text-transform:uppercase;color:var(--muted)}
        .cdbox .cd{font:700 18px var(--ui);color:var(--ink2)}
        .cd{font-variant-numeric:tabular-nums}.cd.soon{color:#f0a04b}
        .ctable:not(.mini){table-layout:fixed;width:100%}
        .ctable:not(.mini) th:nth-child(1){width:38%}
        .ctable:not(.mini) th:nth-child(2){width:16%}
        .ctable:not(.mini) th:nth-child(3){width:32%}
        .ctable:not(.mini) th:nth-child(4){width:14%}
        .ctable th.r,.ctable td.n{text-align:right}
        .ctable td.n{white-space:nowrap;font-weight:700}
        .ctable td{vertical-align:middle}
        .tflex{display:flex;align-items:center;gap:10px}
        .thumb{width:36px;height:36px;border-radius:6px;object-fit:cover;background:var(--track);flex:none}
        .tk{font-weight:600}
        .iname{display:block;font-size:12px;color:var(--cmuted)}
        .tchance{min-width:0}
        .tcv{display:flex;align-items:center;justify-content:space-between;gap:8px}
        tr.got .tk{color:var(--team)}
        tr.boosted td:first-child{box-shadow:inset 4px 0 0 #e2702f;padding-left:14px}
        .cmore{margin-top:14px;color:var(--ink)}
        .cmore summary{cursor:pointer;font-weight:600;color:var(--ink2)}
        .cmore p,.cmore ul{color:var(--muted);font-size:14px;max-width:760px}
        .cmore li{margin:4px 0}
        .cmore a{color:var(--title)}
        .ctable.mini td,.ctable.mini th{padding-top:6px;padding-bottom:6px}
        time[title]{border-bottom:1px dotted currentColor;cursor:help}
        .mo{display:none}
        @media (max-width:600px){
          .ctable th:nth-child(2),.ctable td.pc{display:none}
          .ctable:not(.mini) th:nth-child(1){width:42%}
          .ctable:not(.mini) th:nth-child(3){width:38%}
          .ctable:not(.mini) th:nth-child(4){width:20%}
          .iname.mo{display:block}
          .thumb{display:none}
          .ctable th{white-space:normal;letter-spacing:.04em}
          .tcard{padding:4px 10px}
          /* Word and percentage do not fit side by side: the percentage goes under the word */
          .tcv{flex-wrap:wrap;justify-content:flex-start;gap:2px 8px}
          .ctable td.n{white-space:normal}
          .tk{display:flex;flex-wrap:wrap;align-items:center;gap:4px}.tk .gotbadge{margin-left:0}
          .hval .big{font-size:34px}
          .ghead{align-items:flex-start;flex-wrap:wrap}
          .cdbox{margin-left:0;text-align:left;width:100%;padding-left:2px}
        }
        /* A treasure the counter ticked by itself (seen in the replays): checked, not greyed, cannot be unticked */
        .prize input[data-auto]{cursor:default}
        .star{color:var(--star);font-weight:600}
        .gold{color:#b8f67b}
        .sw{display:inline-block;width:10px;height:10px;border-radius:2px;margin-right:10px;vertical-align:-1px}
        .meter{display:block;width:140px;height:6px;background:var(--track);border-radius:3px;overflow:hidden}
        .meter i{display:block;height:6px}
        .flag{display:flex;gap:12px;align-items:flex-start;background:#452c4f;color:#ffe5d2;border:1px solid #c8412f;border-radius:13px;padding:12px 14px;font-size:14px;margin-top:12px}
        tr.unusual td{background:var(--warnbg)}
        .warn{color:var(--warn)}
        .chart{padding:18px 18px 12px}
        .bars{display:flex;align-items:stretch;gap:4px;height:150px;padding-top:26px}
        .col{flex:1;min-width:5px;display:flex;align-items:flex-end;position:relative;cursor:default}
        .bar{width:100%;background:var(--gold);border-radius:3px 3px 0 0;transition:filter .12s}
        .bar.top{background:#c8412f}
        .bar.zero{background:var(--line);height:3px!important}
        .col:hover .bar{filter:brightness(1.15)}
        .tip{position:absolute;left:50%;transform:translateX(-50%);white-space:nowrap;background:var(--cink);color:var(--card);
             padding:3px 8px;border-radius:6px;font-size:12px;font-weight:600;opacity:0;pointer-events:none;transition:opacity .12s;z-index:2}
        .col:nth-child(-n+3) .tip{left:0;transform:none}
        .col:nth-last-child(-n+3) .tip{left:auto;right:0;transform:none}
        .col:hover .tip{opacity:1}
        .axis{display:flex;justify-content:space-between;gap:8px;color:var(--cmuted);font-size:12px;margin-top:8px;border-top:1px dashed var(--line);padding-top:6px}
        .head{display:flex;flex-direction:column;align-items:center;justify-content:center;gap:7px;margin-bottom:10px}
        .head h2{margin:0}
        .tsw{position:relative;display:inline-flex;align-items:center;gap:10px;min-height:36px;font-size:14px;color:var(--ink2);cursor:pointer;user-select:none}
        .tsw input{position:absolute;opacity:0;width:1px;height:1px;margin:0}
        .tsw .trk{position:relative;width:38px;height:22px;border-radius:11px;background:#476597;flex:none;transition:background .15s}
        .tsw .trk:after{content:"";position:absolute;top:3px;left:3px;width:16px;height:16px;border-radius:50%;background:#fff;transition:transform .15s}
        .tsw input:checked+.trk{background:var(--team)}.tsw input:checked+.trk:after{transform:translateX(16px)}
        .tsw input:focus-visible+.trk{outline:3px solid var(--gold);outline-offset:2px}
        .tsw .badge{display:inline-block;margin-left:2px;padding:1px 8px;border-radius:999px;background:#542d51;color:var(--warn);font-size:12px;font-weight:700}
        .statsbar{display:flex;justify-content:center;margin:-21px 0 -12px}
        .squadtile,.sq{display:none}
        body.showsquad .squadtile{display:flex}body.showsquad .sq{display:table-cell}
        .fold .head h2{display:flex;justify-content:center;align-items:center;gap:4px;min-width:0}
        .foldbtn{display:inline-flex;justify-content:center;align-items:center;gap:10px;background:none;border:0;padding:4px;margin:0;font:inherit;color:inherit;letter-spacing:inherit;text-transform:inherit;cursor:pointer;text-align:center}
        .foldbtn:focus-visible{outline:3px solid var(--gold);outline-offset:4px;border-radius:4px}
        .chev{width:10px;height:10px;border-right:3px solid currentColor;border-bottom:3px solid currentColor;transform:rotate(45deg);margin:0 4px 6px 2px;transition:transform .15s}
        .fold.closed .chev{transform:rotate(-45deg);margin-bottom:0}
        .fold.closed .foldbody{display:none}
        .fold.closed .head{margin-bottom:0}
        .fold.closed .head>:not(h2){display:none}
        .team{display:none}
        body.showteam tr.team{display:table-row}
        body.showteam span.team{display:inline}
        tr.team td{opacity:.8}
        .by{white-space:nowrap}
        .by svg{vertical-align:-3px;margin-right:6px}
        footer{font-size:13px;text-align:center;color:var(--muted);border-top:1px dashed var(--gold);padding-top:14px}
        .loading{display:flex;gap:16px;align-items:center;background:var(--card);color:var(--cink);border:2px solid var(--gold);border-radius:13px;padding:16px 18px}
        .loading .muted{color:var(--cmuted)}
        .spin{width:30px;height:30px;flex:none;border:3px solid var(--line);border-top-color:#b8f67b;border-radius:50%;animation:spin .8s linear infinite}
        @keyframes spin{to{transform:rotate(360deg)}}
        .loading .grow{flex:1;min-width:0}
        .pbar{height:8px;background:var(--line);border-radius:4px;overflow:hidden;margin:8px 0 6px}
        .pbar i{display:block;height:100%;background:#b8f67b}
        .profiles{margin-top:-16px;font-size:13px}
        .embed,.embed body{background:transparent;overflow:hidden}.embed main{padding:0;max-width:none}.embed header,.embed footer,.embed .profiles,.embed .notice{display:none}
        .info{position:relative;display:inline-flex;align-items:center;justify-content:center;width:17px;height:17px;margin-left:6px;border-radius:50%;border:1.5px solid var(--cmuted);color:var(--cmuted);font:700 11px/1 var(--ui);font-style:italic;cursor:help;vertical-align:1px;outline:none}
        .info:hover,.info:focus{border-color:#b8f67b;color:#b8f67b}
        .infotip{display:none;position:absolute;z-index:5;top:24px;right:-12px;width:300px;max-width:80vw;background:var(--cink);color:var(--card);padding:10px 12px;border-radius:13px;font:400 13px/1.45 var(--ui);font-style:normal;text-align:left;box-shadow:0 6px 18px rgba(0,0,0,.35)}
        .info:hover .infotip,.info:focus .infotip{display:block}
        h2 .info{border-color:var(--title);color:var(--title);vertical-align:6px}
        h2 .infotip{left:-12px;right:auto}
        tr.pg{display:none!important}
        .pager{display:flex;flex-wrap:wrap;justify-content:center;align-items:center;gap:8px;padding:10px 0 8px;border-top:1px solid var(--line)}
        .pager[hidden],.pager button[hidden]{display:none}
        .pager .count{color:var(--cmuted);font-size:13px}
        .pager button{min-height:36px;padding:6px 14px;border-radius:6px;border:1px solid var(--gold);background:transparent;color:var(--cink);font-size:13px;cursor:pointer}
        .pager button:hover{background:var(--track);border-color:white}.prize:hover,.prize:focus-within,.foldbtn:hover,.editbtn:hover{outline:4px solid white;outline-offset:2px}
        .notice{background:var(--card);color:var(--cink);border:1px solid var(--line);border-left:6px solid var(--gold);border-radius:13px;padding:12px 16px;white-space:pre-line}
        .notice.warning{border-left-color:#c8412f;background:#4c3157}
        .exper{display:inline-block;font:700 12px var(--ui);letter-spacing:.04em;text-transform:uppercase;padding:2px 8px;border-radius:999px;background:#542d51;color:var(--warn);vertical-align:6px}
        .thour{background:var(--card);color:var(--cink);border:1px solid var(--line);border-left:6px solid var(--gold);border-radius:13px;padding:10px 14px;margin-bottom:12px;text-align:center}
        .thour.live{border-left-color:#c8412f;background:#4c3157}
        .thour .tt{font-weight:700}.thour .exp{font-weight:400;color:var(--cmuted)}
        /* Treasure Hour live: the boosted odds, in orange like the banner */
        .boost{display:inline-block;margin-left:8px;padding:1px 8px;border-radius:999px;background:#5d3658;color:#ffd7b4;font:700 12px var(--ui);letter-spacing:0;text-transform:none;vertical-align:1px}
        .boost[hidden]{display:none}
        .odds.boosted{border-color:#e2702f;box-shadow:inset 6px 0 0 #e2702f;padding-left:22px}
        tr.boosted td:first-child{box-shadow:inset 4px 0 0 #e2702f;padding-left:14px}
        .boostv{color:#ffc99d;font-weight:700}
        .thour .tb{display:flex;justify-content:center;flex-wrap:wrap;gap:6px 18px;font-size:14px;margin-top:4px}
        .chances{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:12px}
        @media (max-width:820px){.chances{grid-template-columns:1fr}}
        .odds{background:var(--card);color:var(--cink);border:1px solid var(--line);border-radius:13px;padding:14px 16px;display:flex;flex-direction:column;gap:4px}
        .odds .k{font-size:12px;letter-spacing:.12em;text-transform:uppercase;color:var(--cmuted);display:flex;justify-content:space-between;gap:8px}
        .odds .v{font-size:40px;font-weight:700;line-height:1.1}
        .odds .l,.odds .s{font-size:13px;color:var(--cmuted)}
        .meter.wide{width:100%;margin:6px 0 2px}.meter.wide i{background:#b8f67b}
        .gotbadge{display:none;color:var(--team);letter-spacing:0;text-transform:none;font-weight:700}
        .odds.got{border-color:var(--team)}.odds.got .gotbadge{display:inline}
        .collection{padding:10px 16px 14px}
        .wk{padding:10px 0;text-align:center}.wk+.wk{border-top:1px solid var(--line)}
        .wt{margin-bottom:10px}.wk.now .wt{color:#b8f67b}
        .wt .items{display:block;font:13px var(--ui);letter-spacing:normal;text-transform:none}
        .prizes{display:flex;justify-content:center;flex-wrap:wrap;gap:12px}
        .prize{position:relative;flex:0 0 min(144px,100%);min-width:0;display:flex;flex-direction:column;gap:2px;padding:8px;border:2px solid var(--line);border-radius:13px;cursor:pointer;background:var(--card)}
        .prize input{position:absolute;top:10px;left:10px;width:20px;height:20px;accent-color:var(--team);cursor:pointer;z-index:1}
        .prize .pimg{display:block;aspect-ratio:1;border-radius:6px;background:var(--track);overflow:hidden;margin-bottom:4px}
        .prize img{width:100%;height:100%;object-fit:cover;display:block;filter:grayscale(.65);opacity:.75}
        .prize.vehicle .pimg{padding:9px}
        .prize.vehicle img,.cthumb.vehicle{object-fit:contain}
        .cthumb.vehicle{padding:3px}
        .prize .pn{font-size:13px;font-weight:600;line-height:1.3}
        .prize .pk{font-size:12px;color:var(--cmuted)}
        .prize.got{border-color:var(--team)}
        .prize.got img{filter:none;opacity:1}
        """;

    private const string TeamIcon = """<svg width="16" height="16" viewBox="0 0 24 24" aria-hidden="true"><circle cx="9" cy="8" r="3.2" fill="none" stroke="#86eadb" stroke-width="2"/><path d="M3 20c0-3.5 2.7-6 6-6s6 2.5 6 6" fill="none" stroke="#86eadb" stroke-width="2"/><circle cx="17" cy="9" r="2.6" fill="none" stroke="#86eadb" stroke-width="2"/><path d="M16 14c3 0 5 2.2 5 5" fill="none" stroke="#86eadb" stroke-width="2"/></svg>""";
    private const string FlagIcon = """<svg width="18" height="18" viewBox="0 0 24 24" aria-hidden="true" style="flex:none;margin-top:2px"><path d="M5 21V4M5 4h11l-2 4 2 4H5" fill="none" stroke="#ff8a6a" stroke-width="2" stroke-linejoin="round"/></svg>""";

    private static readonly Dictionary<string, string> KindColor = new()
    {
        [ChestKinds.Normal] = "#c9a24f",
        [ChestKinds.Rare] = "#2c7fb8",
        [ChestKinds.Buried] = "#8a5a2b",
        [ChestKinds.Extraction] = "#86eadb",
        [ChestKinds.PosterChest] = "#7a4fc2",
        [ChestKinds.Other] = "#9a8f7a",
    };

    public static string Report(Config config, TrackerData data, IReadOnlyList<Profile> profiles)
    {
        var progress = Progress;
        var matches = data.Matches.OrderByDescending(When).ToList();
        var totalChests = matches.Sum(m => m.ChestsOpened);
        // Shown as the total: counted chests + those added by hand. Averages and odds stay on the counted ones.
        var grandTotal = data.TotalChests;
        var totalText = grandTotal.ToString("N0", Fr);
        // The squad total is shown only when asked, with the switch above the tiles (it is inaccurate: teammates far from
        // the player are not in the replays).
        var teamChests = data.TeamChests;
        var avg = matches.Count > 0 ? (double)totalChests / matches.Count : 0;
        var best = matches.Count > 0 ? matches.Max(m => m.ChestsOpened) : 0;
        var treasures = matches.Sum(m => m.TreasureFinds.Count);
        var teamTreasures = matches.Sum(m => m.TeamTreasureFinds.Count);
        var session = matches.Where(IsThisSession).Sum(m => m.ChestsOpened);
        var player = matches.Select(m => m.PlayerName).FirstOrDefault(n => !string.IsNullOrEmpty(n)) ?? "";
        // Own treasures only: the team's are found with chests that are not counted here.
        var every = treasures > 0 ? (int)Math.Round((double)totalChests / treasures) : 0;

        var sb = new StringBuilder();
        var title = Brand.Name.ToUpperInvariant();
        var updated = L.T(
            $"Mis à jour le {L.Shown(DateTime.UtcNow).ToString("dd/MM/yyyy", Fr)} à {L.Clock(L.Shown(DateTime.UtcNow), seconds: true)} · actualisation auto",
            $"Updated {L.Shown(DateTime.UtcNow).ToString("d MMM yyyy", Fr)} at {L.Clock(L.Shown(DateTime.UtcNow), seconds: true)} · auto refresh",
            $"Actualizado el {L.Shown(DateTime.UtcNow).ToString("dd/MM/yyyy", Fr)} a las {L.Clock(L.Shown(DateTime.UtcNow), seconds: true)} · actualización automática");
        string Tile(string value, string label, string? sub = null, bool squad = false) =>
            $"""<div class="tile{(squad ? " squadtile" : "")}" style="--digits:{value.Length}"><div class="v num">{value}</div><div class="k">{label}</div>{(!string.IsNullOrEmpty(sub) ? $"<div class=\"s\">{sub}</div>" : "")}</div>""";

        sb.Append($"""
            <!doctype html><html lang="{L.Code}"><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
            <meta http-equiv="refresh" content="{(progress != null ? 2 : 15)}">
            <title>{title}</title><style>{Brand.FontFace.Value}{Style}</style></head><body data-showsquad="{(config.ShowSquadInReport ? 1 : 0)}" data-showteam="{(config.ShowTeamTreasuresInReport ? 1 : 0)}"><main>
            <header><div><h1 class="heading">{title}</h1>{(player.Length > 0 ? $"<div class=\"who\"><b>{H(player)}</b></div>" : "")}</div>
            <span class="muted" style="font-size:13px">{updated}</span></header>
            {LoadingCard(progress)}{NoticeCards()}{ProfilesLine(profiles, player)}
            <section class="tiles">
              <div class="tile hero" style="--digits:{totalText.Length}"><button type="button" class="editbtn" id="editchests" title="{H(L.T("Modifier mon nombre de coffres", "Edit my number of chests", "Modificar mi número de cofres"))}" aria-label="{H(L.T("Modifier mon nombre de coffres", "Edit my number of chests", "Modificar mi número de cofres"))}">{PencilIcon}</button><div class="k">{L.T("Coffres au total", "Chests in total", "Cofres en total")}</div><div class="v num">{totalText}</div><div class="l">{L.T($"ouverts ou détruits par toi · {session.ToString("N0", Fr)} depuis le lancement", $"opened or destroyed by you · {session.ToString("N0", Fr)} since the tool started", $"abiertos o destruidos por ti · {session.ToString("N0", Fr)} desde que se abrió")}{(data.ManualAdjust != 0 ? "<br>" + L.T($"dont {data.ManualAdjust.ToString("+#,0;-#,0", Fr)} ajoutés à la main", $"{data.ManualAdjust.ToString("+#,0;-#,0", Fr)} of them added by hand", $"de ellos, {data.ManualAdjust.ToString("+#,0;-#,0", Fr)} añadidos a mano") : "")}</div></div>
              <div class="stats">
                <div class="metrics">
                  {Tile(matches.Count.ToString("N0", Fr), L.T("parties analysées", "matches analyzed", "partidas analizadas"))}
                  {Tile(avg.ToString("0.0", Fr), L.T("coffres par partie", "chests per match", "cofres por partida"))}
                  {Tile(best.ToString("N0", Fr), L.T("record en une partie", "best match", "récord en una partida"))}
                </div>
                <div class="extras">
                  {Tile(treasures.ToString("N0", Fr), L.T("trésors trouvés", "treasures found", "tesoros encontrados"),
                      (every > 0 ? L.T($"1 tous les {every.ToString("N0", Fr)} coffres", $"1 every {every.ToString("N0", Fr)} chests", $"1 cada {every.ToString("N0", Fr)} cofres") : null)
                      + (teamTreasures > 0 ? (every > 0 ? "<br>" : "") + L.T($"+{teamTreasures} par l'équipe (estimation)", $"+{teamTreasures} by the team (estimate)", $"+{teamTreasures} del equipo (estimación)") : ""))}
                  {(teamChests > 0 ? Tile((grandTotal + teamChests).ToString("N0", Fr), L.T("total de l'équipe", "squad total", "total del equipo") + SquadInfo, L.T($"toi + {teamChests.ToString("N0", Fr)} par tes coéquipiers", $"you + {teamChests.ToString("N0", Fr)} by your teammates", $"tú + {teamChests.ToString("N0", Fr)} de tus compañeros"), squad: true) : "")}
                </div>
              </div>
            </section>
            {(teamChests > 0 ? $"<div class=\"statsbar\">{Switch("showsquad", L.T("Afficher le total d'équipe", "Show the squad total", "Mostrar el total del equipo") + $" <span class=\"badge\">{L.T("imprécis", "inaccurate", "impreciso")}</span>")}</div>" : "")}
            """);

        // Last 30 days, one bar per day (the best day stands out).
        var today = L.Shown(DateTime.UtcNow).Date;
        var days = Enumerable.Range(0, 30).Select(i => today.AddDays(i - 29)).ToList();
        var perDay = days.Select(d => matches.Where(m => When(m).Date == d).Sum(m => m.ChestsOpened)).ToList();
        var max = Math.Max(1, perDay.Max());
        sb.Append($"<section><h2 class=\"heading\">{L.T("Coffres par jour", "Chests per day", "Cofres por día")}</h2><div class=\"card chart\"><div class=\"bars\">");
        for (var i = 0; i < days.Count; i++)
        {
            var v = perDay[i];
            var h = (int)Math.Round(100.0 * v / max);
            var tip = $"{days[i].ToString(L.T("ddd dd/MM", "ddd d MMM", "ddd dd/MM"), Fr)}{L.T(" : ", ": ", ": ")}{L.Count(v, "coffre", "chest", "cofre")}";
            var cls = v == 0 ? " zero" : v == max ? " top" : "";
            sb.Append($"<div class=\"col\" aria-label=\"{H(tip)}\"><span class=\"tip\" style=\"bottom:calc({Math.Max(h, 3)}% + 6px)\">{H(tip)}</span><div class=\"bar{cls}\" style=\"height:{h}%\"></div></div>");
        }
        var bestDay = perDay[perDay.IndexOf(perDay.Max())];
        var bestDate = days[perDay.IndexOf(perDay.Max())];
        sb.Append($"</div><div class=\"axis\"><span>{days[0].ToString(L.T("dd/MM", "d MMM", "dd/MM"), Fr)}</span>"
            + (bestDay > 0 ? $"<span>{L.T("meilleur jour", "best day", "mejor día")} : {bestDate.ToString(L.T("dd/MM", "d MMM", "dd/MM"), Fr)} · {L.Count(bestDay, "coffre", "chest", "cofre")}</span>" : "")
            + $"<span>{L.T("aujourd'hui", "today", "hoy")}</span></div></div></section>");

        // By chest type (with the other interactions, not in the total) | where the player's treasures come from.
        var byKind = matches.SelectMany(m => m.Chests).GroupBy(c => ChestKinds.Canonical(c.Kind)).OrderByDescending(g => g.Count()).ToList();
        var kindMax = Math.Max(1, byKind.Count > 0 ? byKind.Max(g => g.Count()) : 1);
        var otherKinds = matches
            .SelectMany(m => m.OtherInteractions.Count > 0
                ? m.OtherInteractions
                : new Dictionary<string, int> { [ChestKinds.AmmoBox] = m.AmmoBoxesOpened, [ChestKinds.OtherContainer] = m.OtherContainersOpened })
            .GroupBy(kv => kv.Key)
            .Select(g => (Kind: g.Key, Count: g.Sum(kv => kv.Value)))
            .Where(x => x.Count > 0)
            .OrderByDescending(x => x.Count)
            .ToList();
        sb.Append($"<div class=\"two\"><section><h2 class=\"heading\">{L.T("Par type de coffre", "By chest type", "Por tipo de cofre")}</h2><div class=\"card\"><table>");
        foreach (var g in byKind)
        {
            var color = KindColor.GetValueOrDefault(g.Key, "#9a8f7a");
            var w = Math.Max(2, (int)Math.Round(100.0 * g.Count() / kindMax));
            sb.Append($"<tr><td><span class=\"sw\" style=\"background:{color}\"></span>{H(L.Name(g.Key))}</td><td style=\"width:150px\"><span class=\"meter\"><i style=\"width:{w}%;background:{color}\"></i></span></td><td class=\"n num big\">{g.Count().ToString("N0", Fr)}</td></tr>");
        }
        var destroyedTotal = matches.Sum(m => m.Chests.Count(c => c.Destroyed));
        var notes = new List<string>();
        if (destroyedTotal > 0)
        {
            notes.Add(L.T($"dont {destroyedTotal.ToString("N0", Fr)} détruits (cassés au lieu d'être ouverts)", $"of which {destroyedTotal.ToString("N0", Fr)} destroyed (broken instead of opened)", $"de ellos, {destroyedTotal.ToString("N0", Fr)} destruidos (rotos en lugar de abiertos)"));
        }
        foreach (var (kind, count) in otherKinds)
        {
            sb.Append($"<tr class=\"items\"><td>{H(L.Name(kind))} {L.T("(hors total)", "(not in the total)", "(fuera del total)")}</td><td></td><td class=\"n num\">{count.ToString("N0", Fr)}</td></tr>");
        }
        sb.Append("</table>");
        if (notes.Count > 0)
        {
            sb.Append($"<div class=\"items\" style=\"padding:10px 0 8px\">{string.Join(" · ", notes)}</div>");
        }
        sb.Append("</div></section>");

        var treasureRows = matches.SelectMany(m => m.TreasureFinds.Select(f => (m, f, Team: false))).ToList();
        var teamRows = matches.SelectMany(m => m.TeamTreasureFinds.Select(f => (m, f, Team: true))).ToList();
        sb.Append($"<section><h2 class=\"heading\">{L.T("D'où viennent tes trésors", "Where your treasures come from", "De dónde vienen tus tesoros")}</h2><div class=\"card\"><table>");
        if (treasureRows.Count == 0)
        {
            sb.Append($"<tr><td class=\"items\">{L.T("Pas encore de trésor.", "No treasure yet.", "Todavía ningún tesoro.")}</td></tr>");
        }
        foreach (var g in treasureRows.GroupBy(r => r.f.SourceLabel).OrderByDescending(g => g.Count()))
        {
            var isChest = g.First().f.SourceIsChest;
            sb.Append(isChest
                ? $"<tr><td>{H(g.Key)}</td><td class=\"n num big\">{g.Count()}</td></tr>"
                : $"<tr class=\"unusual\"><td class=\"warn\">{H(g.Key)} <span style=\"font-size:12px\">· {L.T("fiabilité faible", "low reliability", "fiabilidad baja")}</span></td><td class=\"n num big warn\">{g.Count()}</td></tr>");
        }
        sb.Append("</table></div>");
        var unusual = treasureRows.Where(r => !r.f.SourceIsChest).ToList();
        if (unusual.Count > 0)
        {
            var count = L.Count(unusual.Count, "trésor obtenu", "trésors obtenus", "treasure", "treasures", "tesoro obtenido", "tesoros obtenidos");
            sb.Append($"<div class=\"flag\">{FlagIcon}<span>" + L.T(
                $"<b>{count} autrement qu'avec un coffre.</b> Fiabilité faible : il s'agit très probablement d'une erreur dans la façon dont les replays sont analysés.",
                $"<b>{count} obtained other than from a chest.</b> Low reliability: most likely a mistake in the way the replays are analyzed.",
                $"<b>{count} de otra forma que con un cofre.</b> Fiabilidad baja: muy probablemente un error en la forma de analizar las repeticiones.") + "</span></div>");
        }
        sb.Append("</section></div>");

        sb.Append(ChancesSection(data));
        sb.Append(SeenSection(matches));
        sb.Append(CollectionSection(matches, data.ObtainedIds));

        // Treasures: the player's own, then (hidden unless asked) those found by teammates, shared with the whole team.
        if (treasureRows.Count > 0 || teamRows.Count > 0)
        {
            var namesInfo = L.T(
                "Pourquoi certains trésors ont un nom et d'autres non ? Un trésor majeur (tenue, pioche, piste, écran, véhicule, accès à la boutique pirate) sort d'un coffre doré qui porte son identifiant exact : il est nommé à coup sûr (★★), à condition que ce coffre soit dans ton replay, donc à moins d'environ 150 m de toi. Un gizmo est nommé quand il apparaît dans tes mains. Sinon le replay dit seulement qu'un trésor a été reçu, sans dire lequel : « Trésor (type inconnu) ». C'est toujours le cas pour un trésor trouvé par un coéquipier loin de toi.",
                "Why do some treasures have a name and others not? A major treasure (outfit, pickaxe, track, loading screen, vehicle, Pirate Shop Access) comes out of a golden chest that carries its exact id: it is named for sure (★★), as long as that chest is in your replay, that is within about 150 m of you. A gizmo is named when it shows up in your hands. Otherwise the replay only says that a treasure was received, not which one: \"Treasure (unknown type)\". That is always the case for a treasure found by a teammate far from you.",
                "¿Por qué unos tesoros tienen nombre y otros no? Un tesoro mayor (traje, pico, pista, pantalla de carga, vehículo, acceso a la tienda pirata) sale de un cofre dorado que lleva su identificador exacto: se nombra con seguridad (★★), siempre que ese cofre esté en tu repetición, es decir a menos de unos 150 m de ti. Un artilugio se nombra cuando aparece en tus manos. Si no, la repetición solo dice que se recibió un tesoro, no cuál: «Tesoro (tipo desconocido)». Siempre es el caso de un tesoro encontrado por un compañero lejos de ti.");
            var teamSwitch = teamRows.Count > 0
                ? Switch("showteam", L.T($"Afficher aussi l'équipe (+{teamRows.Count}, estimation)", $"Also show the team (+{teamRows.Count}, estimate)", $"Mostrar también el equipo (+{teamRows.Count}, estimación)"))
                : "";
            sb.Append(FoldStart("treasures", $"{L.T("Trésors trouvés", "Treasures found", "Tesoros encontrados")} · <span class=\"num\">{treasures}</span>", open: false, Info(namesInfo), teamSwitch));
            sb.Append($"<div class=\"card\"><table id=\"t-treasures\"><tr><th>{L.T("Partie", "Match", "Partida")}</th><th>{L.T("Trésor", "Treasure", "Tesoro")}</th><th>{L.T("Obtenu à", "Obtained at", "Momento")}</th><th>{L.T("Source", "Source", "Origen")}</th><th>{L.T("Par", "By", "Por")}</th></tr>");
            foreach (var (m, f, team) in treasureRows.Concat(teamRows).OrderByDescending(r => When(r.m)).ThenBy(r => r.f.ObtainedAtSeconds ?? 0))
            {
                var when = f.ObtainedAtSeconds is float t ? (f.TimeExact ? Pretty.Clock(t) : $"{L.T("vers", "around", "hacia los")} {Pretty.Clock(t)}") : "?";
                string source;
                if (team && string.IsNullOrEmpty(f.Teammate))
                {
                    source = $"<span class=\"items\">{L.T("coffre hors de ta vue", "chest out of your sight", "cofre fuera de tu vista")}</span>";
                }
                else if (!f.SourceIsChest)
                {
                    source = $"<span class=\"warn\">{H(f.SourceLabel)} · {L.T("fiabilité faible", "low reliability", "fiabilidad baja")}</span>";
                }
                else
                {
                    source = H(f.SourceLabel) + (f.SourceCertain ? "" : $" <span class=\"items\">({Pretty.Likely})</span>");
                }
                var who = !team ? L.T("toi", "you", "tú")
                    : TeamIcon + (string.IsNullOrEmpty(f.Teammate) ? L.T("un coéquipier", "a teammate", "un compañero") : H(f.Teammate));
                var cls = team ? "r team" : f.SourceIsChest ? "r" : "r unusual";
                var stars = f.FromGoldenChest ? "<span class=\"gold\">★★</span>" : "<span class=\"star\">★</span>";
                sb.Append($"<tr class=\"{cls}\"><td class=\"nw num items\">{L.DateTimeShort(When(m))}</td><td>{stars} <b>{H(f.DisplayName)}</b></td><td class=\"nw num\">{when}</td><td>{source}</td><td class=\"by\">{who}</td></tr>");
            }
            sb.Append($"</table>{Pager("t-treasures")}</div>{FoldEnd}");
        }

        // Latest matches.
        sb.Append(FoldStart("matches", $"{L.T("Dernières parties", "Latest matches", "Últimas partidas")} · <span class=\"num\">{matches.Count.ToString("N0", Fr)}</span>", open: false));
        sb.Append($"<div class=\"card\"><table id=\"t-matches\"><tr><th>{L.T("Date", "Date", "Fecha")}</th><th>{L.T("Mode", "Mode", "Modo")}</th><th>{L.T("Place", "Placement", "Puesto")}</th><th style=\"text-align:right\">{L.T("Coffres", "Chests", "Cofres")}</th>{(teamChests > 0 ? $"<th class=\"sq\" style=\"text-align:right\">{H(OverlayModel.SquadLabel)}</th>" : "")}<th>{L.T("Trésor", "Treasure", "Tesoro")}</th></tr>");
        foreach (var m in matches)
        {
            var treasure = string.Join("<br>", m.TreasureFinds.Select(f => $"<span class=\"{(f.FromGoldenChest ? "gold" : "star")}\">{(f.FromGoldenChest ? "★★" : "★")} {H(f.DisplayName)}</span>"))
                + string.Concat(m.TeamTreasureFinds.Select((f, i) => $"<span class=\"team\">{(i == 0 && m.TreasureFinds.Count == 0 ? "" : "<br>")}{TeamIcon} ★ {H(f.DisplayName)}</span>"));
            var win = m.Placement == 1;
            var warning = m.ReplayChestRecordingErrors > 0 ? L.T(
                $"Le journal de Fortnite signale une erreur d’enregistrement sur {L.N(m.ReplayChestRecordingErrors)} coffre(s) de cette partie. Le total peut être incomplet.",
                $"Fortnite’s log reports recording errors for {L.N(m.ReplayChestRecordingErrors)} chest actor(s) in this match. The total may be incomplete.",
                $"El registro de Fortnite indica errores de grabación para {L.N(m.ReplayChestRecordingErrors)} cofre(s) en esta partida. El total puede estar incompleto.")
                : m.VehicleChestsMissing ? VehicleWarning : null;
            sb.Append($"<tr class=\"r\"><td class=\"nw num items\">{L.DateTimeShort(When(m))}</td><td>{H(Pretty.Playlist(m.Playlist))}</td><td class=\"nw num\"{(win ? " style=\"color:#b8f67b;font-weight:700\"" : "")}>{H(Pretty.Placement(m))}</td><td class=\"n num big\">{m.ChestsOpened}{(warning != null ? $" <span class=\"warn\" title=\"{H(warning)}\">⚠</span>" : "")}</td>{(teamChests > 0 ? $"<td class=\"n num sq\" title=\"{H(string.Join(", ", m.TeamChests.Where(x => x.Chests > 0).Select(x => $"{x.Name} {x.Chests}")))}\">{(m.TeamChests.Count > 0 ? "+" + m.TeamChestsOpened : "")}</td>" : "")}<td class=\"items\">{treasure}</td></tr>");
        }
        sb.Append($"</table>{Pager("t-matches")}</div>");
        var blind = matches.Count(m => m.VehicleChestsMissing);
        if (blind > 0)
        {
            sb.Append($"<div class=\"flag\">{FlagIcon}<span><b>⚠ {L.Count(blind, "partie", "parties", "match", "matches", "partida", "partidas")}</b> · {H(VehicleWarning)}</span></div>");
        }
        sb.Append(FoldEnd);

        // Page script: team rows shown or not, long tables folded ("show more"), treasures ticked as obtained.
        // Everything is remembered in this browser only (the page is rewritten every few seconds by the counter).
        const string script = """
            <script>(function(){
            function get(k){try{return localStorage.getItem(k)}catch(e){return null}}
            function put(k,v){try{localStorage.setItem(k,v)}catch(e){}}
            var pagers=[];
            [].forEach.call(document.querySelectorAll('.pager'),function(box){
              var t=document.getElementById(box.getAttribute('data-for'));if(!t)return;
              var step=+box.getAttribute('data-step')||10,key='chestcounter-rows-'+t.id,n=parseInt(get(key),10);if(!(n>=step))n=step;
              var more=box.querySelector('.more'),all=box.querySelector('.all'),less=box.querySelector('.less'),info=box.querySelector('.count');
              function rows(){return [].filter.call(t.querySelectorAll('tr.r'),function(r){return !r.classList.contains('team')||document.body.classList.contains('showteam')})}
              function apply(){
                [].forEach.call(t.querySelectorAll('tr.r'),function(r){r.classList.remove('pg')});
                var v=rows();v.forEach(function(r,i){if(i>=n)r.classList.add('pg')});
                var shown=Math.min(n,v.length);
                info.textContent=info.getAttribute('data-fmt').replace('{0}',shown).replace('{1}',v.length);
                more.hidden=all.hidden=shown>=v.length;less.hidden=n<=step;box.hidden=v.length<=step;
              }
              more.onclick=function(){n+=step*2;put(key,n);apply()};
              all.onclick=function(){n=1e9;put(key,n);apply()};
              less.onclick=function(){n=step;put(key,n);apply()};
              apply();pagers.push(apply);
            });
            if(window.self!==window.top)document.documentElement.classList.add('embed');
            // Opened in the counter's window (?t=...): choices are saved by the counter itself (config, data.json).
            var tok=new URLSearchParams(location.search).get('t');
            // In the counter's window, the pencil of "chests in total" opens the window's edit box.
            if(tok&&window.self!==window.top){document.documentElement.classList.add('incounter');var eb=document.getElementById('editchests');if(eb)eb.onclick=function(){try{window.parent.postMessage({cc:'editChests'},location.origin)}catch(e){}}}
            function post(action,value){if(!tok)return;try{fetch('/api/action',{method:'POST',headers:{'X-Token':tok,'Content-Type':'application/json'},body:JSON.stringify({action:action,value:value})})}catch(e){}}
            // Switches: the counter's setting, or (report opened as a file) what this browser remembers.
            function toggle(id,cls,action){
              var b=document.getElementById(id),key='chestcounter-'+cls,v=document.body.getAttribute('data-'+cls)==='1';
              if(!tok){var saved=get(key);if(saved!==null)v=saved==='1'}
              function set(x){document.body.classList.toggle(cls,x);if(b)b.checked=x;pagers.forEach(function(p){p()})}
              set(v);
              if(b)b.addEventListener('change',function(){set(b.checked);put(key,b.checked?'1':'0');post(action,b.checked)});
            }
            toggle('showteam','showteam','showTeamInReport');
            toggle('showsquad','showsquad','showSquadInReport');
            // Fit the visible vehicle rather than its transparent margins. Read only a small sample,
            // and let the browser render the original image inside the fitted view box.
            if(CSS.supports('object-view-box','inset(1%)')){
              const frames=new Map(), canvas=document.createElement('canvas'), ctx=canvas.getContext('2d',{willReadFrequently:true});
              function vehicleFrame(image){
                const scale=Math.min(1,256/Math.max(image.naturalWidth,image.naturalHeight));
                const w=canvas.width=Math.max(1,Math.round(image.naturalWidth*scale));
                const h=canvas.height=Math.max(1,Math.round(image.naturalHeight*scale));
                ctx.drawImage(image,0,0,w,h);
                let pixels;
                try{pixels=ctx.getImageData(0,0,w,h).data}catch(e){return 'none'}
                let left=w,right=-1,top=h,bottom=-1;
                for(let y=0;y<h;y++)for(let x=0;x<w;x++){
                  if(pixels[(y*w+x)*4+3]<=16)continue;
                  left=Math.min(left,x);right=Math.max(right,x);top=Math.min(top,y);bottom=Math.max(bottom,y);
                }
                if(right<left)return 'none';
                const inset=(n,size)=>(100*Math.max(0,n-2)/size).toFixed(2)+'%';
                return `inset(${inset(top,h)} ${inset(w-1-right,w)} ${inset(h-1-bottom,h)} ${inset(left,w)})`;
              }
              if(ctx)document.querySelectorAll('.prize.vehicle img,.cthumb.vehicle').forEach(image=>{
                function fit(){
                  if(!image.naturalWidth)return;
                  const src=image.currentSrc;
                  if(!frames.has(src))frames.set(src,vehicleFrame(image));
                  image.style.objectViewBox=frames.get(src);
                }
                image.addEventListener('load',fit,{once:true});
                if(image.complete)fit();
              });
            }
            // Folding sections: the title opens or closes the whole section.
            [].forEach.call(document.querySelectorAll('section.fold'),function(s){
              var key='chestcounter-fold-'+s.getAttribute('data-fold'),btn=s.querySelector('.foldbtn'),open=s.getAttribute('data-open')==='1';
              var saved=get(key);if(saved!==null)open=saved==='1';
              function set(o){s.classList.toggle('closed',!o);btn.setAttribute('aria-expanded',o?'true':'false')}
              set(open);
              btn.addEventListener('click',function(){var o=s.classList.contains('closed');set(o);put(key,o?'1':'0')});
            });
            var ok={};try{ok=JSON.parse(get('chestcounter-obtained')||'{}')||{}}catch(e){}
            function send(id,on){post('obtained',{id:id,on:on})}
            var boxes=document.querySelectorAll('input[data-prize]');
            function refresh(){
              var n=0;[].forEach.call(boxes,function(c){c.parentNode.classList.toggle('got',c.checked);if(c.checked)n++});
              var cc=document.getElementById('colcount');if(cc)cc.textContent=n+'/'+boxes.length;
              [].forEach.call(document.querySelectorAll('[data-items]'),function(card){
                var got=card.getAttribute('data-items').split(' ').some(function(id){var c=document.querySelector('input[data-prize="'+id+'"]');return c&&c.checked});
                card.classList.toggle('got',got);
              });
            }
            [].forEach.call(boxes,function(c){
              if(c.hasAttribute('data-auto'))return;if(!c.hasAttribute('data-saved'))c.checked=!!ok[c.getAttribute('data-prize')];
              c.addEventListener('change',function(){var id=c.getAttribute('data-prize');if(c.checked)ok[id]=1;else delete ok[id];put('chestcounter-obtained',JSON.stringify(ok));send(id,c.checked);refresh()});
            });
            refresh();
            })();</script>
            """;
        sb.Append(L.T(
            "<footer>Compte les coffres que tu as ouverts ou détruits toi-même, d'après tes replays (« rediffusions » dans Fortnite), analysés après chaque partie. Les caisses de munitions et de nourriture ne sont pas comptées. ★★ = coffre doré de la chasse au trésor, détecté de façon certaine. ★ = trésor reçu grâce à un de tes coffres (moment exact), nommé quand l'objet est identifiable. L'icône équipe marque un trésor trouvé par un coéquipier (estimation : déduit des trésors que tu reçois sans avoir ouvert de coffre, car la chasse au trésor les partage avec toute l'équipe) ; il n'entre pas dans tes totaux. Le total de l'équipe ajoute aux tiens les coffres ouverts ou détruits par tes coéquipiers, comptés de la même façon dans tes replays ; un coffre ouvert à plus de 150 m de toi peut manquer, car le replay ne l'enregistre pas.</footer>",
            "<footer>Counts the chests you opened or destroyed yourself, from your replays, analyzed after each match. Ammo boxes and food boxes are not counted. ★★ = golden treasure hunt chest, detected for sure. ★ = treasure received from one of your own chests (exact moment), named when the item can be identified. The team icon marks a treasure found by a teammate (estimate: deduced from the treasures you receive without opening a chest, as the treasure hunt shares them with the whole team); it is not in your totals. The squad total adds the chests opened or destroyed by your teammates, counted the same way in your replays; a chest opened more than 150 m away from you can be missing, as the replay does not record it.</footer>",
            "<footer>Cuenta los cofres que has abierto o destruido tú mismo, a partir de tus repeticiones, analizadas después de cada partida. Las cajas de munición y de comida no se cuentan. ★★ = cofre dorado de la búsqueda del tesoro, detectado con seguridad. ★ = tesoro recibido de uno de tus cofres (momento exacto), con nombre cuando el objeto se puede identificar. El icono de equipo marca un tesoro encontrado por un compañero (estimación: deducido de los tesoros que recibes sin abrir un cofre, ya que la búsqueda del tesoro los comparte con todo el equipo); no cuenta en tus totales. El total del equipo añade a los tuyos los cofres abiertos o destruidos por tus compañeros, contados igual en tus repeticiones; un cofre abierto a más de 150 m de ti puede faltar, porque la repetición no lo graba.</footer>"));
        sb.Append(script).Append("</main></body></html>");
        return sb.ToString();
    }

    /// <summary>
    /// Start of a section the player can fold: its title is a button (with a chevron), <paramref name="right"/> sits at the
    /// right of the title. The page remembers the state in the browser. Ends with <see cref="FoldEnd"/>.
    /// </summary>
    private static string FoldStart(string key, string title, bool open, string info = "", string right = "") =>
        $"""<section class="fold{(open ? "" : " closed")}" data-fold="{key}" data-open="{(open ? 1 : 0)}"><div class="head"><h2 class="heading"><button type="button" class="foldbtn" aria-expanded="{(open ? "true" : "false")}"><span class="chev" aria-hidden="true"></span>{title}</button>{info}</h2>{right}</div><div class="foldbody">""";

    private const string FoldEnd = "</div></section>";

    /// <summary>A small pencil (edit), drawn with the current text color.</summary>
    public const string PencilIcon = """<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M4 20h4L19 9l-4-4L4 16z"/><path d="M14 6l4 4"/></svg>""";

    /// <summary>A small on/off switch (same look as the settings of the counter's window).</summary>
    private static string Switch(string id, string label) =>
        $"""<label class="tsw"><input type="checkbox" id="{id}"><span class="trk" aria-hidden="true"></span><span>{label}</span></label>""";

    /// <summary>Folded table controls: the page shows the first rows, "show more" adds some, "show all" / "fold" (script).</summary>
    private static string Pager(string tableId) =>
        $"""<div class="pager" data-for="{tableId}" data-step="10" hidden><span class="count" data-fmt="{H(L.T("{0} sur {1}", "{0} of {1}", "{0} de {1}"))}"></span><button type="button" class="more">{L.T("Afficher 20 de plus", "Show 20 more", "Mostrar 20 más")}</button><button type="button" class="all">{L.T("Tout afficher", "Show all", "Mostrar todo")}</button><button type="button" class="less" hidden>{L.T("Réduire", "Show less", "Reducir")}</button></div>""";

    /// <summary>A small percentage with enough decimals to be meaningful (0.0064 %, 2.8 %, 63 %).</summary>
    private static string Percent(double p)
    {
        var v = p * 100;
        if (v >= 99.95)
        {
            // Would round to 100: at least once over many chests is never certain.
            var most = 99.9.ToString("F1", Fr);
            return L.Code == "en" ? $">{most}%" : $"> {most} %";
        }
        var decimals = v >= 10 ? 1 : v <= 0 ? 0 : Math.Clamp((int)Math.Ceiling(-Math.Log10(v)) + 1, 1, 8);
        var text = v.ToString("F" + decimals, Fr);
        return L.Code == "en" ? text + "%" : text + " %";
    }

    private static string Info(string text) =>
        $"""<span class="info" tabindex="0" role="note" aria-label="{H(text)}">i<span class="infotip">{H(text)}</span></span>""";

    private static string ChancesSection(TrackerData data)
    {
        var now = DateTime.UtcNow;
        var week = TreasureCatalog.WeekOf(now);
        var rates = WeekendOdds.RewardKinds.Select(kind => TreasureCatalog.Odds.FirstOrDefault(o => o.Kind == kind))
            .OfType<TreasureOdds>();
        var hours = WeekendOdds.Schedule();
        var weeklyTimes = week is null ? [] : WeekendOdds.ChestTimes(data.Matches, now)
            .Where(t => t >= week.From && t < week.To).ToArray();
        var weeklyTotal = weeklyTimes.Length;
        var sb = new StringBuilder(FoldStart("chances", L.T("Chances de la semaine", "Current week odds", "Probabilidades de esta semana"), open: true));
        if (week is not null)
        {
            // Older entries can have a chest total without individual timestamps.
            weeklyTotal += data.Matches.Where(m => m.MatchStartUtc >= week.From && m.MatchStartUtc <= now && m.MatchStartUtc < week.To)
                .Sum(m => Math.Max(0, m.ChestsOpened - m.Chests.Count));
            weeklyTotal = Math.Max(0, weeklyTotal + data.ManualEntries.Where(e => e.WeekFromUtc == week.From).Sum(e => e.Chests));
            var weeklyHours = hours.Where(h => h.StartUtc < week.To && h.EndUtc > week.From);
            sb.Append(TreasureHourBanner(weeklyHours));
            sb.Append($"<p class=\"ch-intro\">{H(ShortWhen(week.From))} – {H(ShortWhen(week.To))} · <b>{L.N(weeklyTotal)}</b> {L.T("coffres personnels pour ces récompenses", "personal chests for these rewards", "cofres personales para estas recompensas")}</p>");
        }
        sb.Append("<div class=\"chances2\">");
        foreach (var odds in rates)
        {
            var token = odds.Kind == TreasureCatalog.PirateShop;
            if (!token && week is null) continue;
            var chance = token ? OverlayModel.TokenChance(data, null, now)
                : WeekendOdds.CumulativePercent(WeekendOdds.AtLeastOne(odds, weeklyTotal, weeklyTimes, hours));
            var prize = token ? TreasureCatalog.PirateShopAccess : week?.Prizes.FirstOrDefault(p => p.Kind == odds.Kind);
            var label = TreasureCatalog.KindName(odds.Kind);
            var next = WeekendOdds.PerChest(odds, now, hours);
            var vehicle = odds.Kind == TreasureCatalog.VehicleDecal ? " vehicle" : "";
            var icon = TreasureImages.Src(prize?.Image) is { } src ? $"<img class=\"cthumb{vehicle}\" src=\"{H(src)}\" alt=\"\">" : "";
            var scope = token
                ? L.T("Tous les coffres personnels", "All personal chests", "Todos los cofres personales")
                : L.T("Coffres personnels de la semaine", "This week’s personal chests", "Cofres personales de esta semana");
            sb.Append($"""
                <div class="hcard" data-kind="{H(odds.Kind)}">
                  <div class="htop">{icon}<div class="hname"><div class="k">{H(label)}<span class="boost" hidden></span></div><div class="ciname">{H(prize?.Name ?? "")}</div></div></div>
                  <div class="hval"><span class="big num"{(token ? " data-token-chance" : "")}>{H(chance)}</span></div>
                  <div class="hexp">{L.T("Chance cumulée", "Cumulative chance", "Probabilidad acumulada")} · {scope}</div>
                  <div class="s sline">{L.T("Prochain coffre", "Next chest", "Próximo cofre")} · <b data-next-odds>{H(WeekendOdds.Percent(next))}</b></div>
                </div>
                """);
        }
        sb.Append($"</div><p class=\"ch-intro\"><a href=\"{H(TreasureCatalog.OddsSource)}\" target=\"_blank\" rel=\"noopener\">Fortnite Status</a></p>");
        sb.Append(FoldEnd);
        return sb.ToString();
    }
    /// <summary>The collection of this season's treasures, to tick what was obtained (folded by default).</summary>
    private static string CollectionSection(IReadOnlyList<MatchRecord> matches, IReadOnlyList<string> obtained)
    {
        var now = DateTime.UtcNow;
        if (now >= TreasureCatalog.Weeks[^1].To.AddDays(14))
        {
            return ""; // the hunt is over
        }
        var sb = new StringBuilder();
        // Golden chests seen in the replays tick their prize by themselves.
        var ticked = obtained.ToHashSet();
        // A teammate's treasure is the player's too (the hunt gives it to the whole team).
        var seen = matches.SelectMany(m => m.TreasureFinds.Concat(m.TeamTreasureFinds)).Select(f => f.PrizeId).Where(id => id != null).Select(id => id!.ToLowerInvariant()).ToHashSet();
        string Prize(TreasurePrize p)
        {
            var auto = p.PrizeIds.Any(id => seen.Contains(id.ToLowerInvariant()));
            var saved = !auto && ticked.Contains(p.Id);
            var src = TreasureImages.Src(p.Image);
            var img = src != null ? $"""<img src="{H(src)}" alt="" loading="lazy" decoding="async" onerror="this.remove()">""" : "";
            var vehicle = p.Kind == TreasureCatalog.VehicleDecal ? " vehicle" : "";
            return $"""<label class="prize{vehicle}{(auto ? " got" : "")}"><input type="checkbox" data-prize="{p.Id}"{(auto ? $" checked data-auto=\"1\" onclick=\"return false\" title=\"{H(L.T("Coché tout seul : vu dans tes replays", "Ticked by itself: seen in your replays", "Marcado solo: visto en tus repeticiones"))}\"" : saved ? " checked data-saved=\"1\"" : "")}><span class="pimg">{img}</span><span class="pn">{H(p.Name)}</span><span class="pk">{H(TreasureCatalog.KindName(p.Kind))}{(auto ? " · " + L.T("vu dans tes replays", "seen in your replays", "visto en tus repeticiones") : "")}</span></label>""";
        }
        var collectionInfo = L.T(
            "Coche ce que tu as déjà obtenu : c'est enregistré dans ce navigateur seulement. Un trésor sorti d'un coffre doré est coché tout seul quand le compteur le reconnaît. À part les gizmos, on ne peut pas obtenir deux fois le même trésor. Images : fortnite.bot, téléchargées une seule fois par le compteur et gardées dans son dossier de données.",
            "Tick what you already obtained: it is saved in this browser only. A treasure from a golden chest is ticked by itself when the counter recognizes it. Apart from gizmos, the same treasure cannot be obtained twice. Images: fortnite.bot, downloaded once by the counter and kept in its data folder.",
            "Marca lo que ya has conseguido: se guarda solo en este navegador. Un tesoro de un cofre dorado se marca solo cuando el contador lo reconoce. Salvo los artilugios, no se puede conseguir dos veces el mismo tesoro. Imágenes: fortnite.bot, descargadas una sola vez por el contador y guardadas en su carpeta de datos.");
        sb.Append(FoldStart("collection", $"{L.T("Ta collection", "Your collection", "Tu colección")} · <span class=\"num\" id=\"colcount\"></span>", open: false, Info(collectionInfo)));
        sb.Append("<div class=\"card collection\">");
        sb.Append($"<div class=\"wk\"><div class=\"wt\">{L.T("Toute la saison", "All season", "Toda la temporada")}</div><div class=\"prizes\">{Prize(TreasureCatalog.PirateShopAccess)}</div></div>");
        foreach (var week in TreasureCatalog.Weeks)
        {
            var current = now >= week.From && now < week.To;
            var dates = $"{ShortWhen(week.From)} – {ShortWhen(week.To)}";
            sb.Append($"<div class=\"wk{(current ? " now" : "")}\"><div class=\"wt\">{L.T("Semaine", "Week", "Semana")} {week.Number} · {H(week.Title)} <span class=\"items\">{dates}{(current ? " · " + L.T("cette semaine", "this week", "esta semana") : "")}</span></div>");
            sb.Append(week.Prizes.Count > 0
                ? $"<div class=\"prizes\">{string.Concat(week.Prizes.Select(Prize))}</div></div>"
                : $"<div class=\"items\" style=\"padding:4px 0 8px\">{L.T("Récompenses pas encore dévoilées : elles arriveront avec une mise à jour du compteur.", "Prizes not revealed yet: they will come with an update of the counter.", "Recompensas aún no reveladas: llegarán con una actualización del contador.")}</div></div>");
        }
        sb.Append("</div>").Append(FoldEnd);
        return sb.ToString();
    }

    /// <summary>
    /// The Treasure Hour banner: drawn by the page script from the schedule, against the clock of the PC, so that it
    /// switches by itself when an hour starts or ends ("live now, until 22:00 · ends in 37m" / "next one: Saturday
    /// 20:00 · in 2d 4h"). Times follow the settings (local time or UTC, 12 or 24 h).
    /// The same script draws every countdown of the page (<see cref="Countdown"/>) each second.
    /// </summary>
    private static string TreasureHourBanner(IEnumerable<TreasureHour> schedule)
    {
        var hours = schedule.Select(h => new
        {
            s = new DateTimeOffset(h.StartUtc).ToUnixTimeMilliseconds(),
            e = new DateTimeOffset(h.EndUtc).ToUnixTimeMilliseconds(),
            b = h.Boosts.Where(kv => WeekendOdds.RewardKinds.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value),
            c = h.Confirmed,
        });
        var odds = TreasureCatalog.Odds.ToDictionary(o => o.Kind, o => o.OneIn);
        var names = WeekendOdds.RewardKinds.ToDictionary(kind => kind, TreasureCatalog.KindName);
        var text = new
        {
            live = L.T("Heure du trésor en cours, jusqu'à {0}", "Treasure Hour live now, until {0}", "Hora del tesoro en curso, hasta las {0}"),
            next = L.T("Prochaine heure du trésor : {0}", "Next Treasure Hour: {0}", "Próxima hora del tesoro: {0}"),
            endsIn = L.T("se termine dans {0}", "ends in {0}", "termina en {0}"),
            startsIn = L.T("dans {0}", "in {0}", "en {0}"),
            expected = L.T("prévue, pas encore annoncée", "expected, not announced yet", "prevista, aún no anunciada"),
            perChest = L.T("1 sur {0} par coffre", "1 in {0} per chest", "1 entre {0} por cofre"),
            oneIn = L.T("1 sur {0}", "1 in {0}", "1 entre {0}"),
            boostPerChest = L.T("1 sur {0} à chaque coffre pendant l'heure du trésor", "1 in {0} per chest during the Treasure Hour", "1 entre {0} por cofre durante la hora del tesoro"),
            boostHalf = L.T(" · une chance sur deux dans {0} coffres de plus à ce rythme", " · one chance in two within {0} more chests at this rate", " · una de cada dos en {0} cofres más a este ritmo"),
            units = L.T("j h min s", "d h m s", "d h m s").Split(' '),
            now = L.T("maintenant", "now", "ahora"),
            locale = L.Culture.Name,
        };
        // Time settings: UTC or local time, 12- or 24-hour clock (the same as the times written by the counter).
        var clock = new { utc = L.UtcTimes, h12 = !L.Clock24 };
        // The default encoder escapes < > & as <...: the JSON cannot close the <script> element it sits in.
        var json = System.Text.Json.JsonSerializer.Serialize(new { hours, odds, names, text, clock });
        return $$"""
            <div class="thour" id="thour" hidden></div>
            <script type="application/json" id="thdata">{{json}}</script>
            <script>(function(){
            var d=JSON.parse(document.getElementById('thdata').textContent),el=document.getElementById('thour');
            function f(n){return Math.round(n).toLocaleString(d.text.locale)}
            function t(ms,withDay){var o=withDay?{weekday:'long',day:'numeric',month:'short',hour:'numeric',minute:'2-digit'}:{hour:'numeric',minute:'2-digit'};o.hour12=d.clock.h12;if(d.clock.utc)o.timeZone='UTC';return new Date(ms).toLocaleString(d.text.locale,o)+(d.clock.utc?' UTC':'')}
            function iso(ms){return new Date(ms).toISOString().slice(0,19)+'Z'}
            function cd(ms){return '<span class="cd" data-end="'+iso(ms)+'"></span>'}
            function boosts(h,live){return Object.keys(h.b).map(function(k){return '<span><b>'+d.names[k]+' ×'+h.b[k].toLocaleString(d.text.locale)+'</b>'+(live?' · '+d.text.perChest.replace('{0}',f(d.odds[k]/h.b[k])):'')+'</span>'}).join('')}
            function draw(){
              var now=Date.now(),cur=d.hours.filter(function(h){return now>=h.s&&now<h.e})[0],nxt=d.hours.filter(function(h){return h.s>now}).sort(function(a,b){return a.s-b.s})[0];
              if(cur){el.className='thour live';el.innerHTML='<div class="tt">⚡ '+d.text.live.replace('{0}',t(cur.e,false))+' · '+d.text.endsIn.replace('{0}',cd(cur.e))+(cur.c?'':' <span class="exp">('+d.text.expected+')</span>')+'</div><div class="tb">'+boosts(cur,true)+'</div>';el.hidden=false}
              else if(nxt){el.className='thour';el.innerHTML='<div class="tt">'+d.text.next.replace('{0}',t(nxt.s,true))+' · '+d.text.startsIn.replace('{0}',cd(nxt.s))+(nxt.c?'':' <span class="exp">('+d.text.expected+')</span>')+'</div><div class="tb">'+boosts(nxt,false)+'</div>';el.hidden=false}
              else el.hidden=true;
              boost(cur);tick();
            }
            function boost(cur){
              document.querySelectorAll('[data-next-odds]').forEach(rate=>{
                const card=rate.closest('[data-kind]'), k=card.dataset.kind, multiplier=cur?.b[k]??1;
                const badge=card.querySelector('.boost');
                rate.textContent=(100*multiplier/d.odds[k]).toLocaleString(d.text.locale,{maximumFractionDigits:8})+'%';
                card.classList.toggle('boosted',multiplier>1);
                badge.hidden=multiplier<=1;
                badge.textContent=multiplier>1?'⚡ ×'+multiplier.toLocaleString(d.text.locale):'';
              });
            }
            // Every countdown of the page: "3d 11h 58m", with seconds in the last hour, orange in the last day.
            function tick(){
              var now=Date.now(),u=d.text.units;
              [].forEach.call(document.querySelectorAll('.cd'),function(x){
                var ms=new Date(x.getAttribute('data-end'))-now;
                if(ms<=0){x.textContent=d.text.now;x.classList.add('done');return}
                var s=Math.floor(ms/1000),dd=Math.floor(s/86400),h=Math.floor(s%86400/3600),m=Math.floor(s%3600/60),sec=s%60;
                x.textContent=dd>0?dd+u[0]+' '+h+u[1]+' '+m+u[2]:h>0?h+u[1]+' '+m+u[2]+' '+sec+u[3]:m+u[2]+' '+sec+u[3];
                x.classList.toggle('soon',ms<86400000);
              });
            }
            draw();document.addEventListener('DOMContentLoaded',draw);setInterval(draw,1000);
            })();</script>
            """;
    }

    /// <summary>
    /// The rarest treasures (Pirate Shop Access, gold outfits) that other players got in the player's matches, from
    /// golden chests opened within reach of the replay. Experimental: always shown, even empty, to be checked with
    /// friends before it is trusted.
    /// </summary>
    private static string SeenSection(IReadOnlyList<MatchRecord> matches)
    {
        var rows = matches
            .SelectMany(m => m.SeenPrizes.Select(s => (m, s, Prize: TreasureCatalog.Find(s.PrizeId))))
            .Where(r => r.Prize?.Kind is TreasureCatalog.PirateShop or TreasureCatalog.GoldOutfit)
            .OrderByDescending(r => When(r.m)).ThenBy(r => r.s.TimeSeconds)
            .ToList();
        var info = L.T(
            "Un coffre doré est envoyé à tous les joueurs à environ 150 m, avec le trésor et le joueur qui l'a ouvert : ces trésors-là ont été vus dans tes replays. Un joueur plus loin de toi n'y figure pas.",
            "A golden chest is sent to every player within about 150 m, with the treasure and the player who opened it: these were seen in your replays. A player farther away from you is not in them.",
            "Un cofre dorado se envía a todos los jugadores a unos 150 m, con el tesoro y el jugador que lo abrió: estos se vieron en tus repeticiones. Un jugador más lejos de ti no aparece.");
        var sb = new StringBuilder();
        sb.Append(FoldStart("seen", $"{L.T("Ils l'ont eu dans tes parties", "They got it in your matches", "Lo consiguieron en tus partidas")} · <span class=\"num\">{rows.Count}</span>", open: false,
            $" <span class=\"exper\">{L.T("expérimental", "experimental", "experimental")}</span>{Info(info)}"));
        sb.Append("<div class=\"card\">");
        if (rows.Count == 0)
        {
            sb.Append($"<p class=\"items\" style=\"margin:8px 0\">{L.T("Aucun pour l'instant : quand un joueur à moins de 150 m de toi décroche l'accès à la boutique pirate ou une tenue en or, il apparaît ici avec la partie, son pseudo et le moment.", "None yet: when a player within 150 m of you gets the Pirate Shop Access or a gold outfit, they show up here with the match, their name and the moment.", "Ninguno por ahora: cuando un jugador a menos de 150 m de ti consigue el acceso a la tienda pirata o un traje de oro, aparece aquí con la partida, su nombre y el momento.")}</p></div>{FoldEnd}");
            return sb.ToString();
        }
        sb.Append($"<table id=\"t-seen\"><tr><th>{L.T("Partie", "Match", "Partida")}</th><th>{L.T("Joueur", "Player", "Jugador")}</th><th>{L.T("Trésor", "Treasure", "Tesoro")}</th><th>{L.T("Moment", "Time", "Momento")}</th></tr>");
        foreach (var (m, s, prize) in rows)
        {
            var name = prize!.Kind == TreasureCatalog.PirateShop ? TreasureCatalog.KindName(prize.Kind) : prize.Name;
            sb.Append($"<tr class=\"r\"><td class=\"nw num items\">{L.DateTimeShort(When(m))}</td><td><b>{H(s.Player)}</b></td><td><span class=\"gold\">★★</span> {H(name)}</td><td class=\"nw num\">{Pretty.Clock(s.TimeSeconds)}</td></tr>");
        }
        sb.Append($"</table>{Pager("t-seen")}</div>{FoldEnd}");
        return sb.ToString();
    }

    /// <summary>"i" next to the squad total: why the teammates' count is lower than what they really opened.</summary>
    private static string SquadInfo
    {
        get
        {
            var text = L.T(
                "Pourquoi ce nombre est bas ? Il vient de TES replays. Fortnite n'y enregistre que ce qui se passe à environ 150 m autour de toi : un coffre ouvert par un coéquipier plus loin n'y figure pas et ne peut pas être compté. Rien non plus après ton départ de la partie (élimination puis retour au salon). Plus vous lootez ensemble, plus le nombre est juste. Les coffres comptés, eux, le sont avec les mêmes règles que les tiens.",
                "Why is this number low? It comes from YOUR replays. Fortnite only records what happens within about 150 m of you: a chest a teammate opens farther away is not in it and cannot be counted. Nothing either once you leave the match (eliminated, back to the lobby). The more you loot together, the closer it gets. The chests that are counted are counted with the same rules as yours.",
                "¿Por qué este número es bajo? Viene de TUS repeticiones. Fortnite solo graba lo que pasa a unos 150 m a tu alrededor: un cofre que un compañero abre más lejos no está en ella y no se puede contar. Tampoco nada después de que salgas de la partida (eliminado, de vuelta al lobby). Cuanto más looteéis juntos, más exacto es. Los cofres contados se cuentan con las mismas reglas que los tuyos.");
            return $"""<span class="info" tabindex="0" role="note" aria-label="{H(text)}">i<span class="infotip">{H(text)}</span></span>""";
        }
    }

    private static string VehicleWarning => L.T(
        "La rediffusion montre du butin près du trajet de ton véhicule sans permettre de confirmer quel coffre a été détruit ni par qui. Le total peut être incomplet, même avec les rediffusions de haute qualité désactivées.",
        "The replay shows loot along your vehicle's route without enough information to confirm which chest was destroyed or by whom. Your total may be incomplete even with high quality replays turned off.",
        "La repetición muestra botín a lo largo del trayecto de tu vehículo sin información suficiente para confirmar qué cofre se destruyó ni quién lo hizo. El total puede estar incompleto incluso con las repeticiones de alta calidad desactivadas.");

    /// <summary>Messages of content.json for these days (text only, escaped).</summary>
    private static string NoticeCards() => string.Concat(TreasureCatalog.ActiveNotices(DateTime.UtcNow)
        .Select(n => $"<div class=\"notice{(n.Level == "warning" ? " warning" : "")}\">{(n.TitleIn(L.Code) is { Length: > 0 } title ? $"<b>{H(title)}</b><br>" : "")}{H(n.TextIn(L.Code))}{(n.Link != null ? $"<br><a href=\"{H(n.Link)}\" target=\"_blank\" rel=\"noopener\">{L.T("En savoir plus", "More info", "Más información")}</a>" : "")}</div>"));

    private static string LoadingCard(ScanProgress? p)
    {
        if (p == null)
        {
            return "";
        }
        var pct = p.Total > 0 ? (int)Math.Round(100.0 * p.Done / p.Total) : 0;
        return L.T(
            $"""<div class="loading"><div class="spin"></div><div class="grow"><b>Analyse de tes replays… {p.Done} / {p.Total}</b><div class="pbar"><i style="width:{pct}%"></i></div><span class="muted">Cette page se met à jour toute seule : les chiffres ci-dessous augmentent au fil de la lecture des replays.</span></div></div>""",
            $"""<div class="loading"><div class="spin"></div><div class="grow"><b>Analyzing your replays… {p.Done} / {p.Total}</b><div class="pbar"><i style="width:{pct}%"></i></div><span class="muted">This page updates by itself: the numbers below grow as the replays are read.</span></div></div>""",
            $"""<div class="loading"><div class="spin"></div><div class="grow"><b>Analizando tus repeticiones… {p.Done} / {p.Total}</b><div class="pbar"><i style="width:{pct}%"></i></div><span class="muted">Esta página se actualiza sola: las cifras de abajo aumentan a medida que se leen las repeticiones.</span></div></div>""");
    }

    private static string ProfilesLine(IReadOnlyList<Profile> profiles, string player)
    {
        var others = profiles.Where(p => p.Name != player).Select(p => H(p.Name)).ToList();
        if (others.Count == 0)
        {
            return "";
        }
        return $"""<div class="profiles muted">{L.T("Autres profils sur ce PC : ", "Other profiles on this PC: ", "Otros perfiles en este PC: ")}{string.Join(", ", others)} · {L.T("« Changer de profil » dans le compteur", "\"Switch profile\" in the counter", "«Cambiar de perfil» en el contador")}</div>""";
    }

    /// <summary>Style of the OBS page: the one chosen for the overlay ("hud" or "classic").</summary>
    public static volatile string OverlayStyle = "hud";

    /// <summary>What the overlay shows right now (also written by <see cref="WriteAll"/>, so that the OBS page never goes back).</summary>
    private static volatile OverlayView? LastOverlay;
    private static (string Path, string Style, string Language, OverlayView View)? WrittenOverlay;

    internal static string? CurrentTokenChance => LastOverlay?.TokenChance;

    /// <summary>Rewrites only the OBS overlay page (called on every live update).</summary>
    public static void WriteOverlay(Store store, OverlayView view)
    {
        LastOverlay = view;
        try
        {
            var path = store.OverlayPath;
            var style = OverlayStyle;
            var language = L.Code;
            if (WrittenOverlay is { } previous && previous.Path == path && previous.Style == style
                && previous.Language == language && previous.View.SameContent(view) && File.Exists(path))
                return;

            WriteAtomic(path, Overlay(view));
            WrittenOverlay = (path, style, language, view);
        }
        catch
        {
            // the page is a convenience: never stop the tracker for it
        }
    }

    public static string Overlay(OverlayView v) => OverlayStyle == "classic" ? OverlayClassic(v) : OverlayHud(v);

    /// <summary>
    /// The HUD overlay as a web page, for OBS ("Browser" source, local file): transparent, figures outlined like the
    /// game's, same rows as the overlay on screen. Refreshes itself every 5 seconds.
    /// </summary>
    private static string OverlayHud(OverlayView v)
    {
        // The last finished match stays during the next one; the red dot next to the total says a match is in progress.
        var state = Row(L.T("Dernière", "Last", "Última"), v.Last ?? "—", "");
        var squad = v.Squad != null ? Row(OverlayModel.SquadLabel, v.Squad, "") : "";
        static string Row(string label, string value, string cls) =>
            $"<div class=\"r {cls}\"><span>{H(label)}</span>{(value.Length > 0 ? $"<span class=\"sep\">•</span><b>{H(value)}</b>" : "")}</div>";
        return $$"""
            <!doctype html><html lang="{{L.Code}}"><head><meta charset="utf-8"><meta http-equiv="refresh" content="5">
            <title>{{L.T("Overlay coffres", "Chest overlay", "Overlay de cofres")}}</title>
            <style>
            {{Brand.BodyFontFace.Value}}
            html,body{margin:0;background:transparent}
            body{padding:10px 14px;display:inline-block;color:#fff;font-family:"Heading Bold","Chest Digits","Segoe UI",sans-serif;font-weight:700;text-align:right;
                 -webkit-text-stroke:4px #071337;paint-order:stroke fill;letter-spacing:.01em}
            .total{display:flex;align-items:center;justify-content:flex-end;gap:10px;font-size:46px;font-weight:700;font-stretch:condensed;line-height:1.05}
            .total.live{color:#bafc82}
            .total img{width:41px;height:41px;flex:none}
            .r{display:flex;align-items:center;justify-content:flex-end;gap:8px;font-size:21px;font-stretch:semi-condensed;line-height:1.3}
            .r.gold b{color:#bafc82}
            .sep{opacity:.9}
            .dot{width:13px;height:13px;border-radius:50%;background:#ff3b30;box-shadow:0 0 0 2px #071337;flex:none}
            .t{color:#d6beff}.t .star{color:#c49bff}
            </style></head><body>
            <div class="total{{(v.CurrentLive && v.Current is { } c && c != "…" ? " live" : "")}}">{{(v.CurrentLive ? "<i class=\"dot\"></i>" : "")}}<img src="{{Brand.IconSrc.Value}}" alt="">{{H(v.Total)}}</div>
            {{state}}
            {{Row(L.T("Session", "Session", "Sesión"), v.Session, "")}}
            {{squad}}
            <div class="r t"><span class="star">★</span><b>{{H(v.Treasures)}}</b></div>
            {{Row(OverlayModel.TokenLabel, v.TokenChance, "")}}
            </body></html>
            """;
    }

    /// <summary>The classic overlay (dark card) as a web page, for OBS.</summary>
    private static string OverlayClassic(OverlayView v)
    {
        var rows = new StringBuilder();
        foreach (var r in v.Rows)
        {
            rows.Append($"<div class=\"row{(r.Treasure ? " t" : "")}\"><span>{H(r.Label)}</span><b>{H(r.Value)}{(r.Detail != null ? $" <small>{H(r.Detail)}</small>" : "")}</b></div>");
        }
        return $$"""
            <!doctype html><html lang="{{L.Code}}"><head><meta charset="utf-8"><meta http-equiv="refresh" content="5">
            <title>{{L.T("Overlay coffres", "Chest overlay", "Overlay de cofres")}}</title>
            <style>
            {{Brand.BodyFontFace.Value}}
            html,body{margin:0;background:transparent;color:#e2e2e8;font:500 16px/1.3 "Heading Bold","Chest Digits","Segoe UI",sans-serif}
            .box{display:inline-block;min-width:260px;padding:14px 18px 12px;border-radius:16px;background:rgba(10,32,76,.92);border:1px solid #4d78b8;text-shadow:0 1px 3px rgba(0,0,0,.6)}
            .head{display:flex;justify-content:space-between;gap:16px;font:700 12px "Heading Bold","Segoe UI",sans-serif;letter-spacing:.08em;color:#bfd3f2}
            .live{color:#9be6d8}
            .counters{display:flex;align-items:stretch;gap:18px;margin:4px 0 10px}
            .counters>div{display:flex;flex-direction:column}
            .sep{width:1px;background:#355692;margin:8px 0 2px}
            .big{font-size:40px;font-weight:800;line-height:1.1;font-variant-numeric:tabular-nums}
            .total{display:flex;align-items:center;gap:7px;color:#9cdcff}.total img{width:35px;height:35px}.cur{color:#6ee096}.none{color:#50505c}
            .cap{font-size:11px;font-weight:600;color:#b7d0ee}
            .rows{border-top:1px solid #355692;padding-top:8px;display:grid;grid-template-columns:auto 1fr;gap:4px 12px}
            .row{display:contents}.row span{font-size:13px;font-weight:600;color:#b7d0ee}.row b{font-weight:500}
            .row.t b{color:#c4a0ff}.row small{font-size:11px;font-weight:600;color:#b7d0ee}
            </style></head><body><div class="box">
            <div class="head"><span>{{Brand.Name}}</span>{{(v.Badge != null ? $"<span class=\"live\">{H(v.Badge)}</span>" : "")}}</div>
            <div class="counters">
              <div><span class="big total"><img src="{{Brand.IconSrc.Value}}" alt="">{{H(v.Total)}}</span><span class="cap">total</span></div>
              <div class="sep"></div>
              <div><span class="big {{(v.Current == null ? "none" : v.CurrentLive ? "cur" : "")}}">{{H(v.Current ?? "—")}}</span><span class="cap">{{H(v.CurrentCaption)}}</span></div>
            </div>
            <div class="rows">{{rows}}</div>
            </div></body></html>
            """;
    }

    public static string Csv(TrackerData data)
    {
        var sb = new StringBuilder();
        sb.AppendLine(L.T("date;mode;joueur;place;joueurs;coffres;coffres_coequipiers;caisses_munitions;tresors;fichier", "date;mode;player;placement;players;chests;teammate_chests;ammo_boxes;treasures;file", "fecha;modo;jugador;puesto;jugadores;cofres;cofres_companeros;cajas_municion;tesoros;archivo"));

        foreach (var m in Profiles.Counted(data).OrderBy(When))
        {
            var t = string.Join(" + ", m.TreasureFinds.Select(f => f.DisplayName));
            sb.AppendLine(string.Join(";",
                When(m).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                Pretty.Playlist(m.Playlist), m.PlayerName, m.Placement, m.TotalPlayers,
                m.ChestsOpened, m.TeamChestsOpened, m.AmmoBoxesOpened, t, m.ReplayFile));
        }
        return sb.ToString();
    }
}
