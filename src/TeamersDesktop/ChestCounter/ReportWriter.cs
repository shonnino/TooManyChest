using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;

namespace ChestCounter;

public static class ReportWriter
{
	public static readonly DateTime SessionStartUtc = DateTime.UtcNow;

	public static volatile ScanProgress? Progress;

	private const string Style = ":root{--bg:#0e0912;--bg2:#0f131b;--card:#190f1e;--card2:#1a2030;--ink:#e6eaf2;--muted:#7a8496;--faint:#4a5468;--line:#232b3a;--line2:#2e3850;\n  --cyan:#d00080;--cyan-soft:rgba(208,0,128,.12);--amber:#ffb84d;--amber-soft:rgba(255,184,77,.13);--mint:#3ddc97;--mint-soft:rgba(61,220,151,.13);\n  --rose:#ff80aa;--rose-soft:rgba(255,128,170,.12);--blue:#a00060;--shadow:0 1px 2px rgba(0,0,0,.3),0 8px 24px rgba(0,0,0,.18)}\n@media (prefers-color-scheme:light){:root:not([data-theme=\"dark\"]){--bg:#f4f6fa;--bg2:#eef1f6;--card:#fff;--card2:#f7f9fc;--ink:#190f1e;--muted:#5d6678;--faint:#9aa3b5;\n  --line:#e3e7ef;--line2:#d4dae6;--cyan:#0b8fb0;--cyan-soft:rgba(11,143,176,.09);--amber:#b8730d;--amber-soft:rgba(184,115,13,.1);--mint:#13895a;--mint-soft:rgba(19,137,90,.1);\n  --rose:#c2346a;--rose-soft:rgba(194,52,106,.08);--blue:#2f6fd6;--shadow:0 1px 2px rgba(20,30,50,.06),0 6px 20px rgba(20,30,50,.06)}}\n:root[data-theme=\"light\"]{--bg:#f4f6fa;--bg2:#eef1f6;--card:#fff;--card2:#f7f9fc;--ink:#190f1e;--muted:#5d6678;--faint:#9aa3b5;\n  --line:#e3e7ef;--line2:#d4dae6;--cyan:#0b8fb0;--cyan-soft:rgba(11,143,176,.09);--amber:#b8730d;--amber-soft:rgba(184,115,13,.1);--mint:#13895a;--mint-soft:rgba(19,137,90,.1);\n  --rose:#c2346a;--rose-soft:rgba(194,52,106,.08);--blue:#2f6fd6;--shadow:0 1px 2px rgba(20,30,50,.06),0 6px 20px rgba(20,30,50,.06)}\n*{box-sizing:border-box}\nhtml{background:var(--bg)}\nbody{margin:0;background:var(--bg);color:var(--ink);font:14px/1.5 \"Segoe UI Variable Text\",\"Segoe UI\",system-ui,-apple-system,sans-serif;-webkit-font-smoothing:antialiased}\nmain{max-width:1120px;margin:0 auto;padding:28px 16px 56px}\n.num{font-variant-numeric:tabular-nums}\n.muted{color:var(--muted)}\n\n/* Cabecera */\n.masthead{display:flex;flex-wrap:wrap;align-items:center;justify-content:space-between;gap:14px 24px;padding-bottom:20px;margin-bottom:24px;border-bottom:1px solid var(--line)}\n.brand{display:flex;align-items:center;gap:14px;min-width:0}\n.logo{width:44px;height:44px;flex:none;border-radius:12px;display:grid;place-items:center;background:var(--cyan-soft);color:var(--cyan)}\n.eyebrow{font-size:11px;font-weight:700;letter-spacing:.14em;text-transform:uppercase;color:var(--cyan)}\nh1{margin:0;font-size:24px;line-height:1.2;font-weight:700;letter-spacing:-.015em;overflow-wrap:anywhere}\n.upd{display:inline-flex;align-items:center;gap:8px;padding:6px 12px;border:1px solid var(--line);border-radius:99px;background:var(--card);color:var(--muted);font-size:12.5px}\n.dot{width:8px;height:8px;border-radius:50%;background:var(--mint);box-shadow:0 0 0 0 var(--mint);animation:pulse 2s ease-out infinite}\n@keyframes pulse{0%{box-shadow:0 0 0 0 rgba(61,220,151,.5)}70%{box-shadow:0 0 0 7px rgba(61,220,151,0)}100%{box-shadow:0 0 0 0 rgba(61,220,151,0)}}\n.profiles{margin:-12px 0 22px;font-size:13px;color:var(--muted)}\n\n/* Cifras clave */\n.kpis{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:12px}\n.kpi{position:relative;background:var(--card);border:1px solid var(--line);border-radius:16px;padding:16px 18px;box-shadow:var(--shadow);min-width:0}\n.kpi .l{font-size:12px;font-weight:600;letter-spacing:.06em;text-transform:uppercase;color:var(--muted)}\n.kpi .v{margin-top:6px;font-size:30px;font-weight:700;line-height:1.1;letter-spacing:-.02em;font-variant-numeric:tabular-nums;overflow-wrap:anywhere}\n.kpi .s{margin-top:4px;font-size:12.5px;color:var(--muted)}\n.kpi.hero{grid-column:span 2;grid-row:span 2;display:flex;flex-direction:column;justify-content:space-between;overflow:hidden;\n  background:linear-gradient(135deg,var(--cyan-soft),transparent 65%),var(--card)}\n.kpi.hero .v{font-size:64px;color:var(--cyan);margin-top:10px}\n.kpi.hero .s{font-size:13.5px}\n.kpi .acc{color:var(--amber)} .kpi .ros{color:var(--rose)} .kpi .mnt{color:var(--mint)}\n.split{display:flex;gap:28px;margin-top:18px;padding-top:14px;border-top:1px solid var(--line)}\n.split b{display:block;font-size:20px;font-variant-numeric:tabular-nums}\n.split span{font-size:12px;color:var(--muted)}\n\n/* Secciones */\nsection{margin-top:28px}\n.sh{display:flex;flex-wrap:wrap;align-items:baseline;justify-content:space-between;gap:4px 16px;margin:0 0 12px}\nh2{margin:0;font-size:16px;font-weight:700;letter-spacing:-.005em}\n.sh .meta{font-size:12.5px;color:var(--muted)}\n.card{background:var(--card);border:1px solid var(--line);border-radius:16px;box-shadow:var(--shadow)}\n.pad{padding:18px 20px}\n.grid2{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:12px 16px}\n.grid2 section{margin-top:0}\n\n/* Gráfico de 30 días */\n.chart{position:relative;display:grid;grid-template-columns:auto 1fr;gap:0 10px}\n.yaxis{display:flex;flex-direction:column;justify-content:space-between;height:200px;font-size:11px;color:var(--faint);text-align:right;font-variant-numeric:tabular-nums}\n.yaxis span{transform:translateY(-50%)} .yaxis span:last-child{transform:translateY(50%)}\n.plot{position:relative;height:200px;display:flex;align-items:flex-end;gap:3px;\n  background:linear-gradient(var(--line) 1px,transparent 1px) 0 0/100% 50%;border-bottom:1px solid var(--line2)}\n.avg{position:absolute;left:0;right:0;z-index:2;border-top:1px dashed var(--amber);pointer-events:none}\n.avg span{position:absolute;left:0;top:-19px;font-size:11px;font-weight:600;color:var(--amber);background:var(--card);padding:0 4px;border-radius:4px}\n.col{flex:1;min-width:4px;height:100%;display:flex;align-items:flex-end;position:relative}\n.bar{width:100%;background:var(--blue);border-radius:4px 4px 1px 1px;transition:filter .12s,background .12s}\n.col.today .bar{background:var(--cyan)}\n.bar.zero{background:var(--line2);height:2px!important;border-radius:1px}\n.col:hover .bar{filter:brightness(1.25)}\n.tip{position:absolute;left:50%;transform:translateX(-50%);white-space:nowrap;background:var(--ink);color:var(--bg);\n  padding:4px 9px;border-radius:8px;font-size:12px;font-weight:600;opacity:0;pointer-events:none;transition:opacity .12s;z-index:3}\n.col:nth-child(-n+4) .tip{left:0;transform:none}\n.col:nth-last-child(-n+4) .tip{left:auto;right:0;transform:none}\n.col:hover .tip{opacity:1}\n.xaxis{grid-column:2;display:flex;gap:3px;margin-top:8px;font-size:11px;color:var(--faint)}\n.xaxis span{flex:1;min-width:4px;text-align:center;white-space:nowrap;overflow:visible}\n.xaxis span:empty{visibility:hidden}\n.xaxis .t{color:var(--cyan);font-weight:600}\n\n/* Listas con barra de proporción */\n.rows{display:flex;flex-direction:column}\n.row{display:grid;grid-template-columns:minmax(0,1fr) auto;gap:4px 12px;padding:10px 0;border-bottom:1px solid var(--line)}\n.row:last-child{border-bottom:none}\n.row .n{font-weight:700;font-variant-numeric:tabular-nums;text-align:right}\n.row .n small{font-weight:500;color:var(--muted);margin-left:6px}\n.track{grid-column:1/-1;height:6px;border-radius:3px;background:var(--bg2);overflow:hidden}\n.track i{display:block;height:100%;border-radius:3px;background:var(--cyan)}\n.row.sub{color:var(--muted)} .row.sub .n{color:var(--muted);font-weight:600}\n.row.warn .track i{background:var(--amber)}\n.note{margin:12px 0 0;font-size:12.5px;color:var(--muted)}\n\n/* Tablas */\n.tw{overflow-x:auto}\ntable{width:100%;border-collapse:collapse;font-variant-numeric:tabular-nums}\nth,td{text-align:left;padding:11px 16px;border-bottom:1px solid var(--line);vertical-align:middle}\nth{position:sticky;top:0;background:var(--card2);font-size:11.5px;font-weight:700;letter-spacing:.07em;text-transform:uppercase;color:var(--muted);white-space:nowrap}\nthead tr th:first-child{border-top-left-radius:15px} thead tr th:last-child{border-top-right-radius:15px}\ntbody tr:last-child td{border-bottom:none}\ntbody tr:hover td{background:var(--bg2)}\n.r{text-align:right} .nw{white-space:nowrap}\n.sm{font-size:12.5px;color:var(--muted)}\n.badge{display:inline-flex;align-items:center;gap:6px;padding:2px 10px;border-radius:99px;font-size:12.5px;font-weight:600;white-space:nowrap;background:var(--bg2);color:var(--muted)}\n.badge.win{background:var(--amber-soft);color:var(--amber)}\n.badge.top{background:var(--mint-soft);color:var(--mint)}\n.badge.mode{background:transparent;border:1px solid var(--line2);color:var(--ink);font-weight:500}\n.chests{display:flex;align-items:center;justify-content:flex-end;gap:10px}\n.chests .mini{width:70px;height:6px;border-radius:3px;background:var(--bg2);overflow:hidden}\n.chests .mini i{display:block;height:100%;background:var(--amber);border-radius:3px}\n.chests b{min-width:28px;text-align:right}\n.star{color:var(--rose);font-weight:600}\n.tlist{display:flex;flex-direction:column;gap:2px}\ntr.unusual td{background:var(--amber-soft)}\n.flag{display:flex;gap:12px;align-items:flex-start;margin-top:28px;padding:14px 18px;border:1px solid var(--amber);border-radius:14px;background:var(--amber-soft)}\n.flag .ic{color:var(--amber);font-size:18px;line-height:1.2}\n.empty{padding:28px 20px;text-align:center;color:var(--muted)}\n\n/* Carga */\n.loading{display:flex;gap:16px;align-items:center;background:var(--card);border:1px solid var(--cyan);border-radius:16px;padding:16px 18px;margin-bottom:22px;box-shadow:var(--shadow)}\n.spin{width:30px;height:30px;flex:none;border:3px solid var(--line2);border-top-color:var(--cyan);border-radius:50%;animation:spin .8s linear infinite}\n@keyframes spin{to{transform:rotate(360deg)}}\n.loading .grow{flex:1;min-width:0}\n.pbar{height:8px;background:var(--line);border-radius:4px;overflow:hidden;margin:8px 0 6px}\n.pbar i{display:block;height:100%;background:var(--cyan);border-radius:4px}\n\n/* Pie */\nfooter{margin-top:36px;padding-top:18px;border-top:1px solid var(--line);color:var(--muted);font-size:12.5px;line-height:1.65}\nfooter h3{margin:0 0 6px;font-size:12px;font-weight:700;letter-spacing:.08em;text-transform:uppercase;color:var(--muted)}\nfooter p{margin:0 0 6px;max-width:860px}\n\n/* Pestañas: barra fija arriba; sin JavaScript se ven todas las secciones seguidas */\n.tabs{position:sticky;top:0;z-index:20;display:flex;gap:4px;overflow-x:auto;scrollbar-width:none;margin:0 -16px 24px;padding:10px 16px;\n  background:color-mix(in srgb,var(--bg) 88%,transparent);-webkit-backdrop-filter:blur(12px);backdrop-filter:blur(12px);border-bottom:1px solid var(--line)}\n.tabs::-webkit-scrollbar{display:none}\nhtml:not(.js) .tabs{display:none}\n.tab{appearance:none;border:0;background:transparent;color:var(--muted);font:inherit;font-size:13.5px;font-weight:600;padding:8px 14px;border-radius:10px;\n  cursor:pointer;display:inline-flex;align-items:center;gap:8px;white-space:nowrap;transition:background .12s,color .12s}\n.tab:hover{color:var(--ink);background:var(--card)}\n.tab[aria-selected=\"true\"]{color:var(--ink);background:var(--card);box-shadow:inset 0 -2px 0 var(--cyan),0 0 0 1px var(--line)}\n.tab .c{font-size:11.5px;font-weight:700;padding:1px 7px;border-radius:99px;background:var(--bg2);color:var(--muted);font-variant-numeric:tabular-nums}\n.tab[aria-selected=\"true\"] .c{background:var(--cyan-soft);color:var(--cyan)}\n.tab:focus-visible,.more:focus-visible{outline:2px solid var(--cyan);outline-offset:2px}\n.panel>:first-child{margin-top:0}\nhtml:not(.js) .panel+.panel{margin-top:40px}\n.panel[hidden]{display:none}\n.more{font-size:13px;font-weight:600;color:var(--cyan);text-decoration:none;padding:4px 10px;border-radius:8px}\n.more:hover{background:var(--cyan-soft)}\n\n/* Piezas nuevas */\n.split b.acc{color:var(--amber)} .split b.mnt{color:var(--mint)}\n.row.rose .track i{background:var(--rose)}\n.row.off .track i{background:var(--line2)}\n.row .lbl{display:flex;flex-wrap:wrap;align-items:center;gap:6px 10px}\n.row .badge{font-size:11.5px;padding:1px 8px}\n.minis{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:12px;margin-bottom:28px}\n.mini-kpi{background:var(--card);border:1px solid var(--line);border-radius:14px;padding:14px 16px;box-shadow:var(--shadow);min-width:0}\n.mini-kpi b{display:block;font-size:24px;line-height:1.15;font-variant-numeric:tabular-nums}\n.mini-kpi span{font-size:12.5px;color:var(--muted)}\n.ros{color:var(--rose)} .acc{color:var(--amber)} .cy{color:var(--cyan)}\n.flag{margin:0 0 20px}\n.empty.big{padding:56px 24px;font-size:14.5px}\n.empty.big .star{display:block;font-size:30px;margin-bottom:8px}\ntable.matches tr.won td{background:var(--amber-soft)} table.matches tr.won td:first-child{box-shadow:inset 3px 0 0 var(--amber)}\n.team{margin-top:4px;max-width:340px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}\ntd.foot{text-align:center;padding:14px 16px}\n.callout{display:flex;gap:12px;align-items:flex-start;padding:13px 16px;margin-bottom:24px;border:1px solid var(--line2);border-radius:14px;background:var(--card);color:var(--muted);font-size:13px;line-height:1.55}\n.callout .ic{flex:none;width:20px;height:20px;border-radius:50%;display:grid;place-items:center;background:var(--cyan-soft);color:var(--cyan);font-weight:700;font-size:12px;font-style:italic;font-family:Georgia,serif}\n.mates{display:grid;grid-template-columns:repeat(auto-fill,minmax(270px,1fr));gap:12px}\n.mate{background:var(--card);border:1px solid var(--line);border-radius:16px;padding:16px 18px;box-shadow:var(--shadow);min-width:0}\n.who{display:flex;align-items:center;gap:12px;min-width:0}\n.who .grow{min-width:0}\n.av{width:40px;height:40px;flex:none;border-radius:50%;display:grid;place-items:center;font-weight:700;font-size:17px;color:#0e0912}\n.av.sm-av{width:26px;height:26px;font-size:12px}\n.who .nm{font-weight:700;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}\n.mate .nm{font-size:15.5px}\n.mate .st{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:10px;margin-top:14px;padding-top:12px;border-top:1px solid var(--line)}\n.mate .st b{display:block;font-size:20px;line-height:1.2;font-variant-numeric:tabular-nums}\n.mate .st b small{font-size:12px;font-weight:600;color:var(--muted);margin-right:2px}\n.mate .st span{display:block;font-size:11.5px;color:var(--muted);line-height:1.35}\n.boards{display:grid;grid-template-columns:repeat(auto-fit,minmax(250px,1fr));gap:12px}\n.board{background:var(--card);border:1px solid var(--line);border-radius:16px;padding:14px 16px 12px;box-shadow:var(--shadow);min-width:0}\n.board h3{margin:0 0 8px;font-size:12px;font-weight:700;letter-spacing:.07em;text-transform:uppercase;color:var(--muted)}\n.board ol{list-style:none;margin:0;padding:0}\n.board li{display:grid;grid-template-columns:22px 26px minmax(0,1fr) auto;align-items:center;gap:2px 8px;padding:7px 0;border-bottom:1px solid var(--line)}\n.board li:last-child{border-bottom:none}\n.board .rk{font-size:12px;font-weight:700;color:var(--faint);text-align:center;font-variant-numeric:tabular-nums}\n.board .rk.medal{font-size:16px}\n.board .who2{display:flex;flex-direction:column;min-width:0}\n.board .who2 .nm{font-weight:600;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}\n.board .who2 .sub{font-size:11.5px;color:var(--muted);white-space:nowrap;overflow:hidden;text-overflow:ellipsis}\n.board li b{font-size:15px;font-variant-numeric:tabular-nums;white-space:nowrap}\n.board li b small{font-size:11px;font-weight:600;color:var(--muted);margin-right:2px}\n.board li.first b{color:var(--amber);font-size:17px}\n.board .bar{grid-column:3/-1;height:4px;border-radius:2px;background:var(--bg2);overflow:hidden}\n.board .bar i{display:block;height:100%;background:var(--cyan);border-radius:2px}\n.board li.first .bar i{background:var(--amber)}\n.bfoot{margin-top:8px;font-size:12px;color:var(--muted)}\n.mates-line{display:flex;flex-wrap:wrap;gap:4px 6px;margin-top:5px}\n.chip{display:inline-flex;align-items:center;gap:5px;padding:2px 9px 2px 3px;border-radius:99px;background:var(--bg2);font-size:12px;white-space:nowrap;max-width:100%}\n.matches .chip{padding-left:9px}\n.chip .cn{color:var(--muted);overflow:hidden;text-overflow:ellipsis;max-width:130px}\n.chip b{font-variant-numeric:tabular-nums}\n.chip .sm-av{width:20px;height:20px;font-size:10.5px}\n.mini-kpi b small{font-size:13px;font-weight:600;color:var(--muted);margin-right:2px}\n.teambar{display:flex;align-items:center;justify-content:flex-end;gap:10px}\n.teambar .tb{position:relative;width:90px;height:6px;border-radius:3px;background:var(--bg2);overflow:hidden}\n.teambar .tb i{position:absolute;left:0;top:0;bottom:0;border-radius:3px;background:var(--cyan)}\n.teambar .tb i.me{background:var(--amber);z-index:1}\n.teambar b{min-width:44px;text-align:right}\n.teambar b small{font-size:11px;color:var(--muted);margin-right:2px}\n@media (max-width:520px){.teambar .tb{display:none}}\n@media (max-width:860px){.minis{grid-template-columns:repeat(2,minmax(0,1fr))}}\n@media (max-width:860px){.kpis{grid-template-columns:repeat(2,minmax(0,1fr))}.grid2{grid-template-columns:1fr}.kpi.hero .v{font-size:52px}}\n@media (max-width:520px){main{padding:20px 16px 40px}.kpis{grid-template-columns:1fr 1fr}.kpi.hero{grid-row:auto}.kpi .v{font-size:24px}\n  .kpi.hero .v{font-size:44px}th,td{padding:10px 12px}.chests .mini{display:none}.yaxis,.plot{height:150px}}\n@media (prefers-reduced-motion:reduce){.dot,.spin{animation:none}}";

	private const string TabsScript = "(function(){var d=document,tabs=[].slice.call(d.querySelectorAll('[data-tab]'));if(!tabs.length)return;d.documentElement.classList.add('js');\nfunction get(k,s){try{return (s?sessionStorage:localStorage).getItem(k)}catch(e){return null}}\nfunction put(k,v,s){try{(s?sessionStorage:localStorage).setItem(k,v)}catch(e){}}\nfunction show(id,save){var t=null;tabs.forEach(function(b){var on=b.getAttribute('data-tab')===id;b.setAttribute('aria-selected',on?'true':'false');b.tabIndex=on?0:-1;if(on)t=b});\n  if(!t)return false;var bar=t.parentNode;bar.scrollLeft=t.offsetLeft-(bar.clientWidth-t.offsetWidth)/2;[].forEach.call(d.querySelectorAll('.panel'),function(p){p.hidden=p.id!=='t-'+id});if(save)put('cc-tab',id,true);return true}\nvar h=location.hash.slice(1);if(!(h&&show(h,true))&&!show(get('cc-tab',true)||'',false))show(tabs[0].getAttribute('data-tab'),false);\nfunction go(id){if(show(id,true)){window.scrollTo(0,0);try{history.replaceState(null,'','#'+id)}catch(e){}}}\ntabs.forEach(function(b,i){b.addEventListener('click',function(){go(b.getAttribute('data-tab'))});\n  b.addEventListener('keydown',function(e){var k=e.key==='ArrowRight'?1:e.key==='ArrowLeft'?-1:0;if(!k)return;e.preventDefault();var n=tabs[(i+k+tabs.length)%tabs.length];go(n.getAttribute('data-tab'));n.focus()})});\n[].forEach.call(d.querySelectorAll('[data-go]'),function(a){a.addEventListener('click',function(e){e.preventDefault();go(a.getAttribute('data-go'))})});\nvar y=get('cc-y',true);if(y)window.scrollTo(0,+y);addEventListener('pagehide',function(){put('cc-y',String(window.scrollY),true)});\n})();";

	private const string ChestIcon = "<svg width=\"24\" height=\"24\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 10V8a4 4 0 0 1 4-4h8a4 4 0 0 1 4 4v2\"/><rect x=\"3\" y=\"10\" width=\"18\" height=\"10\" rx=\"2\"/><path d=\"M3 14h18\"/><rect x=\"10\" y=\"12\" width=\"4\" height=\"4\" rx=\"1\"/></svg>";

	private const string OverlayStyle = "html,body{margin:0;background:transparent;color:#f9ecf6;font:500 16px/1.25 \"Segoe UI\",system-ui,sans-serif}\n.box{position:relative;display:inline-block;min-width:230px;padding:10px 14px 10px 18px;border-radius:14px;overflow:hidden;\n  background:rgba(25,15,30,.94);border:1px solid #451c3c;box-shadow:0 6px 24px rgba(0,0,0,.35)}\n.box:before{content:\"\";position:absolute;left:0;top:0;bottom:0;width:4px;background:#ff7fb4}\n.box.live:before{background:#3ddc97}\n.head{display:flex;align-items:center;justify-content:space-between;gap:16px;font:700 12px \"Segoe UI\",sans-serif;letter-spacing:.12em;color:#7a8496}\n.pill{display:inline-flex;align-items:center;gap:6px;padding:3px 10px;border-radius:99px;background:#143a2c;color:#3ddc97;letter-spacing:.06em}\n.pill:before{content:\"\";width:8px;height:8px;border-radius:50%;background:#3ddc97;animation:pulse 1.6s ease-in-out infinite}\n@keyframes pulse{50%{opacity:.35}}\n.main{display:flex;align-items:center;justify-content:space-between;gap:16px;margin:6px 0 8px}\n.big{display:block;font-size:34px;font-weight:800;line-height:1.05;font-variant-numeric:tabular-nums}\n.total .big{color:#ff7fb4}\n.cap{display:block;font-size:11px;font-weight:700;letter-spacing:.1em;color:#7a8496;text-transform:uppercase}\n.chip{min-width:84px;padding:5px 12px;border-radius:11px;background:#1a202d;text-align:center}\n.chip.live .big,.chip.live .cap{color:#3ddc97}.chip.none .big{color:#505868}\n.stats{display:flex;justify-content:space-between;gap:14px;border-top:1px solid #222938;padding-top:7px}\n.stat .cap{font-size:10px}.stat b{display:block;font-size:16px;font-weight:700}\n.stat.t b{color:#ff80aa}.stat small{display:block;font-size:11px;font-weight:600;color:#7a8496}";

	private static CultureInfo Loc => L.Culture;

	public static bool IsThisSession(MatchRecord m)
	{
		DateTime value = m.MatchStartUtc ?? m.AnalyzedAtUtc;
		if (value.Kind == DateTimeKind.Unspecified)
		{
			value = DateTime.SpecifyKind(value, DateTimeKind.Utc);
		}
		return value.ToUniversalTime().AddSeconds(m.DurationSeconds.GetValueOrDefault()) >= SessionStartUtc.AddMinutes(-2.0);
	}

	public static void WriteAll(Store store, Config config, TrackerData data)
	{
		try
		{
			TrackerData trackerData = Profiles.View(data, config);
			WriteAtomic(store.ReportPath, ClassicSkin.Apply(Report(config, trackerData, Profiles.List(data, config), Profiles.InitialChests(data, config)), store));
			WriteAtomic(store.OverlayPath, Overlay(OverlayModel.Build(trackerData, null, inProgress: false, Profiles.InitialChests(data, config))));
			WriteAtomic(store.CsvPath, Csv(data));
		}
		catch (Exception ex)
		{
			Console.WriteLine(L.F("No se pudo escribir el informe: {0}", "Cannot write the report: {0}", ex.Message));
		}
	}

	private static void WriteAtomic(string path, string content)
	{
		string text = path + ".tmp";
		File.WriteAllText(text, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		File.Move(text, path, overwrite: true);
	}

	private static string H(string? s)
	{
		return WebUtility.HtmlEncode(s ?? "");
	}

	private static DateTime When(MatchRecord m)
	{
		return (m.MatchStartUtc ?? m.AnalyzedAtUtc).ToLocalTime();
	}

	public static string Report(Config config, TrackerData data, IReadOnlyList<Profile> profiles, int initialChests = 0)
	{
		ScanProgress progress = Progress;
		List<MatchRecord> matches = data.Matches.OrderByDescending(When).ToList();
		int num = matches.Sum((MatchRecord m) => m.ChestsOpened);
		int n = num + Math.Max(0, initialChests);
		double num2 = ((matches.Count > 0) ? ((double)num / (double)matches.Count) : 0.0);
		int n2 = ((matches.Count > 0) ? matches.Max((MatchRecord m) => m.ChestsOpened) : 0);
		int n3 = matches.Sum((MatchRecord m) => m.TreasureFinds.Count);
		List<MatchRecord> list = matches.Where(IsThisSession).ToList();
		int n4 = list.Sum((MatchRecord m) => m.ChestsOpened);
		int num3 = Enumerable.Count(matches, (MatchRecord m) => m.Placement == 1);
		string text = matches.Select((MatchRecord m) => m.PlayerName).FirstOrDefault((string value12) => !string.IsNullOrEmpty(value12)) ?? "";
		StringBuilder stringBuilder = new StringBuilder();
		string text2 = "2xFlaw's Chest Counter";
		string value = L.F("Actualizado {0} · en directo", "Updated {0} · live", DateTime.Now.ToString(L.En ? "MMM d, yyyy HH:mm" : "dd/MM/yyyy HH:mm", Loc));
		string value2 = ((text.Length > 0) ? H(text) : text2);
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(130779, 11, stringBuilder2);
		handler.AppendLiteral("<!doctype html><html lang=\"");
		handler.AppendFormatted(L.Code);
		handler.AppendLiteral("\"><head><meta charset=\"utf-8\">\n<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">\n<meta name=\"color-scheme\" content=\"dark light\">\n<meta http-equiv=\"refresh\" content=\"");
		handler.AppendFormatted((progress != null) ? 2 : 15);
		handler.AppendLiteral("\">\n<title>");
		handler.AppendFormatted(text2);
		handler.AppendFormatted((text.Length > 0) ? (" · " + H(text)) : "");
		handler.AppendLiteral("</title><style>");
		handler.AppendFormatted(":root{--bg:#0e0912;--bg2:#0f131b;--card:#190f1e;--card2:#1a2030;--ink:#e6eaf2;--muted:#7a8496;--faint:#4a5468;--line:#232b3a;--line2:#2e3850;\n  --cyan:#d00080;--cyan-soft:rgba(208,0,128,.12);--amber:#ffb84d;--amber-soft:rgba(255,184,77,.13);--mint:#3ddc97;--mint-soft:rgba(61,220,151,.13);\n  --rose:#ff80aa;--rose-soft:rgba(255,128,170,.12);--blue:#a00060;--shadow:0 1px 2px rgba(0,0,0,.3),0 8px 24px rgba(0,0,0,.18)}\n@media (prefers-color-scheme:light){:root:not([data-theme=\"dark\"]){--bg:#f4f6fa;--bg2:#eef1f6;--card:#fff;--card2:#f7f9fc;--ink:#190f1e;--muted:#5d6678;--faint:#9aa3b5;\n  --line:#e3e7ef;--line2:#d4dae6;--cyan:#0b8fb0;--cyan-soft:rgba(11,143,176,.09);--amber:#b8730d;--amber-soft:rgba(184,115,13,.1);--mint:#13895a;--mint-soft:rgba(19,137,90,.1);\n  --rose:#c2346a;--rose-soft:rgba(194,52,106,.08);--blue:#2f6fd6;--shadow:0 1px 2px rgba(20,30,50,.06),0 6px 20px rgba(20,30,50,.06)}}\n:root[data-theme=\"light\"]{--bg:#f4f6fa;--bg2:#eef1f6;--card:#fff;--card2:#f7f9fc;--ink:#190f1e;--muted:#5d6678;--faint:#9aa3b5;\n  --line:#e3e7ef;--line2:#d4dae6;--cyan:#0b8fb0;--cyan-soft:rgba(11,143,176,.09);--amber:#b8730d;--amber-soft:rgba(184,115,13,.1);--mint:#13895a;--mint-soft:rgba(19,137,90,.1);\n  --rose:#c2346a;--rose-soft:rgba(194,52,106,.08);--blue:#2f6fd6;--shadow:0 1px 2px rgba(20,30,50,.06),0 6px 20px rgba(20,30,50,.06)}\n*{box-sizing:border-box}\nhtml{background:var(--bg)}\nbody{margin:0;background:var(--bg);color:var(--ink);font:14px/1.5 \"Segoe UI Variable Text\",\"Segoe UI\",system-ui,-apple-system,sans-serif;-webkit-font-smoothing:antialiased}\nmain{max-width:1120px;margin:0 auto;padding:28px 16px 56px}\n.num{font-variant-numeric:tabular-nums}\n.muted{color:var(--muted)}\n\n/* Cabecera */\n.masthead{display:flex;flex-wrap:wrap;align-items:center;justify-content:space-between;gap:14px 24px;padding-bottom:20px;margin-bottom:24px;border-bottom:1px solid var(--line)}\n.brand{display:flex;align-items:center;gap:14px;min-width:0}\n.logo{width:44px;height:44px;flex:none;border-radius:12px;display:grid;place-items:center;background:var(--cyan-soft);color:var(--cyan)}\n.eyebrow{font-size:11px;font-weight:700;letter-spacing:.14em;text-transform:uppercase;color:var(--cyan)}\nh1{margin:0;font-size:24px;line-height:1.2;font-weight:700;letter-spacing:-.015em;overflow-wrap:anywhere}\n.upd{display:inline-flex;align-items:center;gap:8px;padding:6px 12px;border:1px solid var(--line);border-radius:99px;background:var(--card);color:var(--muted);font-size:12.5px}\n.dot{width:8px;height:8px;border-radius:50%;background:var(--mint);box-shadow:0 0 0 0 var(--mint);animation:pulse 2s ease-out infinite}\n@keyframes pulse{0%{box-shadow:0 0 0 0 rgba(61,220,151,.5)}70%{box-shadow:0 0 0 7px rgba(61,220,151,0)}100%{box-shadow:0 0 0 0 rgba(61,220,151,0)}}\n.profiles{margin:-12px 0 22px;font-size:13px;color:var(--muted)}\n\n/* Cifras clave */\n.kpis{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:12px}\n.kpi{position:relative;background:var(--card);border:1px solid var(--line);border-radius:16px;padding:16px 18px;box-shadow:var(--shadow);min-width:0}\n.kpi .l{font-size:12px;font-weight:600;letter-spacing:.06em;text-transform:uppercase;color:var(--muted)}\n.kpi .v{margin-top:6px;font-size:30px;font-weight:700;line-height:1.1;letter-spacing:-.02em;font-variant-numeric:tabular-nums;overflow-wrap:anywhere}\n.kpi .s{margin-top:4px;font-size:12.5px;color:var(--muted)}\n.kpi.hero{grid-column:span 2;grid-row:span 2;display:flex;flex-direction:column;justify-content:space-between;overflow:hidden;\n  background:linear-gradient(135deg,var(--cyan-soft),transparent 65%),var(--card)}\n.kpi.hero .v{font-size:64px;color:var(--cyan);margin-top:10px}\n.kpi.hero .s{font-size:13.5px}\n.kpi .acc{color:var(--amber)} .kpi .ros{color:var(--rose)} .kpi .mnt{color:var(--mint)}\n.split{display:flex;gap:28px;margin-top:18px;padding-top:14px;border-top:1px solid var(--line)}\n.split b{display:block;font-size:20px;font-variant-numeric:tabular-nums}\n.split span{font-size:12px;color:var(--muted)}\n\n/* Secciones */\nsection{margin-top:28px}\n.sh{display:flex;flex-wrap:wrap;align-items:baseline;justify-content:space-between;gap:4px 16px;margin:0 0 12px}\nh2{margin:0;font-size:16px;font-weight:700;letter-spacing:-.005em}\n.sh .meta{font-size:12.5px;color:var(--muted)}\n.card{background:var(--card);border:1px solid var(--line);border-radius:16px;box-shadow:var(--shadow)}\n.pad{padding:18px 20px}\n.grid2{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:12px 16px}\n.grid2 section{margin-top:0}\n\n/* Gráfico de 30 días */\n.chart{position:relative;display:grid;grid-template-columns:auto 1fr;gap:0 10px}\n.yaxis{display:flex;flex-direction:column;justify-content:space-between;height:200px;font-size:11px;color:var(--faint);text-align:right;font-variant-numeric:tabular-nums}\n.yaxis span{transform:translateY(-50%)} .yaxis span:last-child{transform:translateY(50%)}\n.plot{position:relative;height:200px;display:flex;align-items:flex-end;gap:3px;\n  background:linear-gradient(var(--line) 1px,transparent 1px) 0 0/100% 50%;border-bottom:1px solid var(--line2)}\n.avg{position:absolute;left:0;right:0;z-index:2;border-top:1px dashed var(--amber);pointer-events:none}\n.avg span{position:absolute;left:0;top:-19px;font-size:11px;font-weight:600;color:var(--amber);background:var(--card);padding:0 4px;border-radius:4px}\n.col{flex:1;min-width:4px;height:100%;display:flex;align-items:flex-end;position:relative}\n.bar{width:100%;background:var(--blue);border-radius:4px 4px 1px 1px;transition:filter .12s,background .12s}\n.col.today .bar{background:var(--cyan)}\n.bar.zero{background:var(--line2);height:2px!important;border-radius:1px}\n.col:hover .bar{filter:brightness(1.25)}\n.tip{position:absolute;left:50%;transform:translateX(-50%);white-space:nowrap;background:var(--ink);color:var(--bg);\n  padding:4px 9px;border-radius:8px;font-size:12px;font-weight:600;opacity:0;pointer-events:none;transition:opacity .12s;z-index:3}\n.col:nth-child(-n+4) .tip{left:0;transform:none}\n.col:nth-last-child(-n+4) .tip{left:auto;right:0;transform:none}\n.col:hover .tip{opacity:1}\n.xaxis{grid-column:2;display:flex;gap:3px;margin-top:8px;font-size:11px;color:var(--faint)}\n.xaxis span{flex:1;min-width:4px;text-align:center;white-space:nowrap;overflow:visible}\n.xaxis span:empty{visibility:hidden}\n.xaxis .t{color:var(--cyan);font-weight:600}\n\n/* Listas con barra de proporción */\n.rows{display:flex;flex-direction:column}\n.row{display:grid;grid-template-columns:minmax(0,1fr) auto;gap:4px 12px;padding:10px 0;border-bottom:1px solid var(--line)}\n.row:last-child{border-bottom:none}\n.row .n{font-weight:700;font-variant-numeric:tabular-nums;text-align:right}\n.row .n small{font-weight:500;color:var(--muted);margin-left:6px}\n.track{grid-column:1/-1;height:6px;border-radius:3px;background:var(--bg2);overflow:hidden}\n.track i{display:block;height:100%;border-radius:3px;background:var(--cyan)}\n.row.sub{color:var(--muted)} .row.sub .n{color:var(--muted);font-weight:600}\n.row.warn .track i{background:var(--amber)}\n.note{margin:12px 0 0;font-size:12.5px;color:var(--muted)}\n\n/* Tablas */\n.tw{overflow-x:auto}\ntable{width:100%;border-collapse:collapse;font-variant-numeric:tabular-nums}\nth,td{text-align:left;padding:11px 16px;border-bottom:1px solid var(--line);vertical-align:middle}\nth{position:sticky;top:0;background:var(--card2);font-size:11.5px;font-weight:700;letter-spacing:.07em;text-transform:uppercase;color:var(--muted);white-space:nowrap}\nthead tr th:first-child{border-top-left-radius:15px} thead tr th:last-child{border-top-right-radius:15px}\ntbody tr:last-child td{border-bottom:none}\ntbody tr:hover td{background:var(--bg2)}\n.r{text-align:right} .nw{white-space:nowrap}\n.sm{font-size:12.5px;color:var(--muted)}\n.badge{display:inline-flex;align-items:center;gap:6px;padding:2px 10px;border-radius:99px;font-size:12.5px;font-weight:600;white-space:nowrap;background:var(--bg2);color:var(--muted)}\n.badge.win{background:var(--amber-soft);color:var(--amber)}\n.badge.top{background:var(--mint-soft);color:var(--mint)}\n.badge.mode{background:transparent;border:1px solid var(--line2);color:var(--ink);font-weight:500}\n.chests{display:flex;align-items:center;justify-content:flex-end;gap:10px}\n.chests .mini{width:70px;height:6px;border-radius:3px;background:var(--bg2);overflow:hidden}\n.chests .mini i{display:block;height:100%;background:var(--amber);border-radius:3px}\n.chests b{min-width:28px;text-align:right}\n.star{color:var(--rose);font-weight:600}\n.tlist{display:flex;flex-direction:column;gap:2px}\ntr.unusual td{background:var(--amber-soft)}\n.flag{display:flex;gap:12px;align-items:flex-start;margin-top:28px;padding:14px 18px;border:1px solid var(--amber);border-radius:14px;background:var(--amber-soft)}\n.flag .ic{color:var(--amber);font-size:18px;line-height:1.2}\n.empty{padding:28px 20px;text-align:center;color:var(--muted)}\n\n/* Carga */\n.loading{display:flex;gap:16px;align-items:center;background:var(--card);border:1px solid var(--cyan);border-radius:16px;padding:16px 18px;margin-bottom:22px;box-shadow:var(--shadow)}\n.spin{width:30px;height:30px;flex:none;border:3px solid var(--line2);border-top-color:var(--cyan);border-radius:50%;animation:spin .8s linear infinite}\n@keyframes spin{to{transform:rotate(360deg)}}\n.loading .grow{flex:1;min-width:0}\n.pbar{height:8px;background:var(--line);border-radius:4px;overflow:hidden;margin:8px 0 6px}\n.pbar i{display:block;height:100%;background:var(--cyan);border-radius:4px}\n\n/* Pie */\nfooter{margin-top:36px;padding-top:18px;border-top:1px solid var(--line);color:var(--muted);font-size:12.5px;line-height:1.65}\nfooter h3{margin:0 0 6px;font-size:12px;font-weight:700;letter-spacing:.08em;text-transform:uppercase;color:var(--muted)}\nfooter p{margin:0 0 6px;max-width:860px}\n\n/* Pestañas: barra fija arriba; sin JavaScript se ven todas las secciones seguidas */\n.tabs{position:sticky;top:0;z-index:20;display:flex;gap:4px;overflow-x:auto;scrollbar-width:none;margin:0 -16px 24px;padding:10px 16px;\n  background:color-mix(in srgb,var(--bg) 88%,transparent);-webkit-backdrop-filter:blur(12px);backdrop-filter:blur(12px);border-bottom:1px solid var(--line)}\n.tabs::-webkit-scrollbar{display:none}\nhtml:not(.js) .tabs{display:none}\n.tab{appearance:none;border:0;background:transparent;color:var(--muted);font:inherit;font-size:13.5px;font-weight:600;padding:8px 14px;border-radius:10px;\n  cursor:pointer;display:inline-flex;align-items:center;gap:8px;white-space:nowrap;transition:background .12s,color .12s}\n.tab:hover{color:var(--ink);background:var(--card)}\n.tab[aria-selected=\"true\"]{color:var(--ink);background:var(--card);box-shadow:inset 0 -2px 0 var(--cyan),0 0 0 1px var(--line)}\n.tab .c{font-size:11.5px;font-weight:700;padding:1px 7px;border-radius:99px;background:var(--bg2);color:var(--muted);font-variant-numeric:tabular-nums}\n.tab[aria-selected=\"true\"] .c{background:var(--cyan-soft);color:var(--cyan)}\n.tab:focus-visible,.more:focus-visible{outline:2px solid var(--cyan);outline-offset:2px}\n.panel>:first-child{margin-top:0}\nhtml:not(.js) .panel+.panel{margin-top:40px}\n.panel[hidden]{display:none}\n.more{font-size:13px;font-weight:600;color:var(--cyan);text-decoration:none;padding:4px 10px;border-radius:8px}\n.more:hover{background:var(--cyan-soft)}\n\n/* Piezas nuevas */\n.split b.acc{color:var(--amber)} .split b.mnt{color:var(--mint)}\n.row.rose .track i{background:var(--rose)}\n.row.off .track i{background:var(--line2)}\n.row .lbl{display:flex;flex-wrap:wrap;align-items:center;gap:6px 10px}\n.row .badge{font-size:11.5px;padding:1px 8px}\n.minis{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:12px;margin-bottom:28px}\n.mini-kpi{background:var(--card);border:1px solid var(--line);border-radius:14px;padding:14px 16px;box-shadow:var(--shadow);min-width:0}\n.mini-kpi b{display:block;font-size:24px;line-height:1.15;font-variant-numeric:tabular-nums}\n.mini-kpi span{font-size:12.5px;color:var(--muted)}\n.ros{color:var(--rose)} .acc{color:var(--amber)} .cy{color:var(--cyan)}\n.flag{margin:0 0 20px}\n.empty.big{padding:56px 24px;font-size:14.5px}\n.empty.big .star{display:block;font-size:30px;margin-bottom:8px}\ntable.matches tr.won td{background:var(--amber-soft)} table.matches tr.won td:first-child{box-shadow:inset 3px 0 0 var(--amber)}\n.team{margin-top:4px;max-width:340px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}\ntd.foot{text-align:center;padding:14px 16px}\n.callout{display:flex;gap:12px;align-items:flex-start;padding:13px 16px;margin-bottom:24px;border:1px solid var(--line2);border-radius:14px;background:var(--card);color:var(--muted);font-size:13px;line-height:1.55}\n.callout .ic{flex:none;width:20px;height:20px;border-radius:50%;display:grid;place-items:center;background:var(--cyan-soft);color:var(--cyan);font-weight:700;font-size:12px;font-style:italic;font-family:Georgia,serif}\n.mates{display:grid;grid-template-columns:repeat(auto-fill,minmax(270px,1fr));gap:12px}\n.mate{background:var(--card);border:1px solid var(--line);border-radius:16px;padding:16px 18px;box-shadow:var(--shadow);min-width:0}\n.who{display:flex;align-items:center;gap:12px;min-width:0}\n.who .grow{min-width:0}\n.av{width:40px;height:40px;flex:none;border-radius:50%;display:grid;place-items:center;font-weight:700;font-size:17px;color:#0e0912}\n.av.sm-av{width:26px;height:26px;font-size:12px}\n.who .nm{font-weight:700;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}\n.mate .nm{font-size:15.5px}\n.mate .st{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:10px;margin-top:14px;padding-top:12px;border-top:1px solid var(--line)}\n.mate .st b{display:block;font-size:20px;line-height:1.2;font-variant-numeric:tabular-nums}\n.mate .st b small{font-size:12px;font-weight:600;color:var(--muted);margin-right:2px}\n.mate .st span{display:block;font-size:11.5px;color:var(--muted);line-height:1.35}\n.boards{display:grid;grid-template-columns:repeat(auto-fit,minmax(250px,1fr));gap:12px}\n.board{background:var(--card);border:1px solid var(--line);border-radius:16px;padding:14px 16px 12px;box-shadow:var(--shadow);min-width:0}\n.board h3{margin:0 0 8px;font-size:12px;font-weight:700;letter-spacing:.07em;text-transform:uppercase;color:var(--muted)}\n.board ol{list-style:none;margin:0;padding:0}\n.board li{display:grid;grid-template-columns:22px 26px minmax(0,1fr) auto;align-items:center;gap:2px 8px;padding:7px 0;border-bottom:1px solid var(--line)}\n.board li:last-child{border-bottom:none}\n.board .rk{font-size:12px;font-weight:700;color:var(--faint);text-align:center;font-variant-numeric:tabular-nums}\n.board .rk.medal{font-size:16px}\n.board .who2{display:flex;flex-direction:column;min-width:0}\n.board .who2 .nm{font-weight:600;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}\n.board .who2 .sub{font-size:11.5px;color:var(--muted);white-space:nowrap;overflow:hidden;text-overflow:ellipsis}\n.board li b{font-size:15px;font-variant-numeric:tabular-nums;white-space:nowrap}\n.board li b small{font-size:11px;font-weight:600;color:var(--muted);margin-right:2px}\n.board li.first b{color:var(--amber);font-size:17px}\n.board .bar{grid-column:3/-1;height:4px;border-radius:2px;background:var(--bg2);overflow:hidden}\n.board .bar i{display:block;height:100%;background:var(--cyan);border-radius:2px}\n.board li.first .bar i{background:var(--amber)}\n.bfoot{margin-top:8px;font-size:12px;color:var(--muted)}\n.mates-line{display:flex;flex-wrap:wrap;gap:4px 6px;margin-top:5px}\n.chip{display:inline-flex;align-items:center;gap:5px;padding:2px 9px 2px 3px;border-radius:99px;background:var(--bg2);font-size:12px;white-space:nowrap;max-width:100%}\n.matches .chip{padding-left:9px}\n.chip .cn{color:var(--muted);overflow:hidden;text-overflow:ellipsis;max-width:130px}\n.chip b{font-variant-numeric:tabular-nums}\n.chip .sm-av{width:20px;height:20px;font-size:10.5px}\n.mini-kpi b small{font-size:13px;font-weight:600;color:var(--muted);margin-right:2px}\n.teambar{display:flex;align-items:center;justify-content:flex-end;gap:10px}\n.teambar .tb{position:relative;width:90px;height:6px;border-radius:3px;background:var(--bg2);overflow:hidden}\n.teambar .tb i{position:absolute;left:0;top:0;bottom:0;border-radius:3px;background:var(--cyan)}\n.teambar .tb i.me{background:var(--amber);z-index:1}\n.teambar b{min-width:44px;text-align:right}\n.teambar b small{font-size:11px;color:var(--muted);margin-right:2px}\n@media (max-width:520px){.teambar .tb{display:none}}\n@media (max-width:860px){.minis{grid-template-columns:repeat(2,minmax(0,1fr))}}\n@media (max-width:860px){.kpis{grid-template-columns:repeat(2,minmax(0,1fr))}.grid2{grid-template-columns:1fr}.kpi.hero .v{font-size:52px}}\n@media (max-width:520px){main{padding:20px 16px 40px}.kpis{grid-template-columns:1fr 1fr}.kpi.hero{grid-row:auto}.kpi .v{font-size:24px}\n  .kpi.hero .v{font-size:44px}th,td{padding:10px 12px}.chests .mini{display:none}.yaxis,.plot{height:150px}}\n@media (prefers-reduced-motion:reduce){.dot,.spin{animation:none}}");
		handler.AppendLiteral("</style></head><body><main>\n<img src=\"data:image/jpeg;base64,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\" alt=\"2xFlaw banner\" style=\"width:100%;border-radius:14px;margin-bottom:18px\">\n<header class=\"masthead\">\n  <div class=\"brand\"><div class=\"logo\">");
		handler.AppendFormatted("<svg width=\"24\" height=\"24\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 10V8a4 4 0 0 1 4-4h8a4 4 0 0 1 4 4v2\"/><rect x=\"3\" y=\"10\" width=\"18\" height=\"10\" rx=\"2\"/><path d=\"M3 14h18\"/><rect x=\"10\" y=\"12\" width=\"4\" height=\"4\" rx=\"1\"/></svg>");
		handler.AppendLiteral("</div>\n    <div><div class=\"eyebrow\">");
		handler.AppendFormatted((text.Length > 0) ? text2 : L.T("Informe", "Report"));
		handler.AppendLiteral("</div><h1>");
		handler.AppendFormatted(value2);
		handler.AppendLiteral("</h1></div></div>\n  <div class=\"upd\"><span class=\"dot\"></span>");
		handler.AppendFormatted(value);
		handler.AppendLiteral("</div>\n</header>\n");
		handler.AppendFormatted(LoadingCard(progress));
		handler.AppendFormatted(ProfilesLine(profiles, text));
		stringBuilder3.Append(ref handler);
		IReadOnlyCollection<string> readOnlyCollection = config.ExtraKinds();
		List<(MatchRecord, TreasureFind)> list2 = matches.SelectMany((MatchRecord m) => m.TreasureFinds.Select((TreasureFind f) => (m: m, f: f))).ToList();
		(string, int)? tuple = (config.ShowTeammates ? new (string, int)?(TeammatesPanel(matches, readOnlyCollection, N)) : (((string, int)?)null));
		int n5 = Enumerable.Count(matches, (MatchRecord m) =>
		{
			int? placement = m.Placement;
			if (placement.HasValue)
			{
				int valueOrDefault2 = placement.GetValueOrDefault();
				if (valueOrDefault2 > 1)
				{
					return valueOrDefault2 <= 10;
				}
			}
			return false;
		});
		(DateTime, int) tuple2 = (from m in matches
			group m by When(m).Date into g
			select (Day: g.Key, Chests: g.Sum((MatchRecord m) => m.ChestsOpened)) into d
			orderby d.Chests descending
			select d).FirstOrDefault();
		List<(string, string, string)> list3 = new List<(string, string, string)>
		{
			("resumen", L.T("Resumen", "Overview"), null),
			("cofres", L.T("Cofres", "Chests"), null),
			("tesoros", L.T("Tesoros", "Treasures"), (list2.Count > 0) ? N(list2.Count) : null),
			("partidas", L.T("Partidas", "Matches"), N(matches.Count))
		};
		if (tuple.HasValue)
		{
			list3.Add(("companeros", L.T("Compañeros", "Teammates"), (tuple.Value.Item2 > 0) ? N(tuple.Value.Item2) : null));
		}
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder4 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(47, 1, stringBuilder2);
		handler.AppendLiteral("<nav class=\"tabs\" role=\"tablist\" aria-label=\"");
		handler.AppendFormatted(L.T("Secciones del informe", "Report sections"));
		handler.AppendLiteral("\">");
		stringBuilder4.Append(ref handler);
		foreach (var item10 in list3)
		{
			string item = item10.Item1;
			string item2 = item10.Item2;
			string item3 = item10.Item3;
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder5 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(95, 5, stringBuilder2);
			handler.AppendLiteral("<button class=\"tab\" role=\"tab\" type=\"button\" id=\"tab-");
			handler.AppendFormatted(item);
			handler.AppendLiteral("\" data-tab=\"");
			handler.AppendFormatted(item);
			handler.AppendLiteral("\" aria-controls=\"t-");
			handler.AppendFormatted(item);
			handler.AppendLiteral("\">");
			handler.AppendFormatted(item2);
			handler.AppendFormatted((item3 != null) ? ("<span class=\"c\">" + item3 + "</span>") : "");
			handler.AppendLiteral("</button>");
			stringBuilder5.Append(ref handler);
		}
		stringBuilder.Append("</nav>");
		stringBuilder.Append("<div class=\"panel\" id=\"t-resumen\" role=\"tabpanel\" aria-labelledby=\"tab-resumen\">");
		string text3 = ((initialChests > 0) ? L.F("Incluye {0} estimados de antes de usar la herramienta", "Includes {0} manual / historical adjustments", N(initialChests)) : L.T("Cofres abiertos o destruidos por ti, según tus replays", "Chests you opened or destroyed, from your replays"));
		int num4 = matches.Sum((MatchRecord m) => m.TeammatesAdded);
		if (config.ShowTeammates && num4 > 0)
		{
			text3 = text3 + " · " + L.F("incluye ≥ {0} de tus compañeros", "includes ≥ {0} from your teammates", N(num4));
		}
		double num5 = ((matches.Count > 0) ? (100.0 * (double)num3 / (double)matches.Count) : 0.0);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder6 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(675, 23, stringBuilder2);
		handler.AppendLiteral("<div class=\"kpis\">\n  <div class=\"kpi hero\">\n    <div><div class=\"l\">");
		handler.AppendFormatted(L.T("Cofres en total", "Total chests"));
		handler.AppendLiteral("</div><div class=\"v\">");
		handler.AppendFormatted(N(n));
		handler.AppendLiteral("</div><div class=\"s\">");
		handler.AppendFormatted(text3);
		handler.AppendLiteral("</div></div>\n    <div class=\"split\">\n      <div><b>");
		handler.AppendFormatted(N(n4));
		handler.AppendLiteral("</b><span>");
		handler.AppendFormatted(L.T("en esta sesión", "this session"));
		handler.AppendFormatted((list.Count > 0) ? (" · " + L.Count(list.Count, "partida", "partidas", "match", "matches")) : "");
		handler.AppendLiteral("</span></div>\n      <div><b class=\"acc\">");
		handler.AppendFormatted(N(num3));
		handler.AppendLiteral("</b><span>");
		handler.AppendFormatted(L.T((num3 == 1) ? "victoria real" : "victorias reales", (num3 == 1) ? "Victory Royale" : "Victory Royales"));
		handler.AppendFormatted((num3 > 0) ? (" · " + num5.ToString("0", Loc) + " %") : "");
		handler.AppendLiteral("</span></div>\n      <div><b class=\"mnt\">");
		handler.AppendFormatted(N(n5));
		handler.AppendLiteral("</b><span>");
		handler.AppendFormatted(L.T("top 10 sin ganar", "top 10 without winning"));
		handler.AppendLiteral("</span></div>\n    </div>\n  </div>\n  <div class=\"kpi\"><div class=\"l\">");
		handler.AppendFormatted(L.T("Partidas", "Matches"));
		handler.AppendLiteral("</div><div class=\"v\">");
		handler.AppendFormatted(N(matches.Count));
		handler.AppendLiteral("</div><div class=\"s\">");
		handler.AppendFormatted(L.T("analizadas", "analyzed"));
		handler.AppendLiteral("</div></div>\n  <div class=\"kpi\"><div class=\"l\">");
		handler.AppendFormatted(L.T("Media", "Average"));
		handler.AppendLiteral("</div><div class=\"v acc\">");
		handler.AppendFormatted(num2.ToString("0.0", Loc));
		handler.AppendLiteral("</div><div class=\"s\">");
		handler.AppendFormatted(L.T("cofres por partida", "chests per match"));
		handler.AppendLiteral("</div></div>\n  <div class=\"kpi\"><div class=\"l\">");
		handler.AppendFormatted(L.T("Récord", "Best match"));
		handler.AppendLiteral("</div><div class=\"v\">");
		handler.AppendFormatted(N(n2));
		handler.AppendLiteral("</div><div class=\"s\">");
		handler.AppendFormatted((tuple2.Item2 > 0) ? L.F("mejor día: {0} ({1})", "best day: {0} ({1})", N(tuple2.Item2), tuple2.Item1.ToString(L.En ? "MMM d" : "dd/MM", Loc)) : L.T("cofres en una partida", "chests in one match"));
		handler.AppendLiteral("</div></div>\n  <div class=\"kpi\"><div class=\"l\">");
		handler.AppendFormatted(L.T("Tesoros", "Treasures"));
		handler.AppendLiteral("</div><div class=\"v ros\">");
		handler.AppendFormatted(N(n3));
		handler.AppendLiteral("</div><div class=\"s\">");
		handler.AppendFormatted(L.T("encontrados", "found"));
		handler.AppendLiteral("</div></div>\n</div>");
		stringBuilder6.Append(ref handler);
		DateTime today = DateTime.Now.Date;
		List<DateTime> list4 = (from i in Enumerable.Range(0, 30)
			select today.AddDays(i - 29)).ToList();
		List<int> list5 = list4.Select((DateTime d) => matches.Where((MatchRecord m) => When(m).Date == d).Sum((MatchRecord m) => m.ChestsOpened)).ToList();
		int num6 = NiceMax(list5.Max());
		int num7 = list5.Sum();
		int num8 = Enumerable.Count(list5, (int v) => v > 0);
		double num9 = ((num8 > 0) ? ((double)num7 / (double)num8) : 0.0);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder7 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(201, 7, stringBuilder2);
		handler.AppendLiteral("<section><div class=\"sh\"><h2>");
		handler.AppendFormatted(L.T("Cofres por día", "Chests per day"));
		handler.AppendLiteral("</h2>\n<span class=\"meta\">");
		handler.AppendFormatted(L.T("Últimos 30 días", "Last 30 days"));
		handler.AppendLiteral(" · ");
		handler.AppendFormatted(N(num7));
		handler.AppendLiteral(" ");
		handler.AppendFormatted(L.T("cofres", "chests"));
		handler.AppendLiteral(" · ");
		handler.AppendFormatted(L.Count(num8, "día jugado", "días jugados", "day played", "days played"));
		handler.AppendLiteral("</span></div>\n<div class=\"card pad\"><div class=\"chart\">\n<div class=\"yaxis\"><span>");
		handler.AppendFormatted(N(num6));
		handler.AppendLiteral("</span><span>");
		handler.AppendFormatted(N(num6 / 2));
		handler.AppendLiteral("</span><span>0</span></div>\n<div class=\"plot\">");
		stringBuilder7.Append(ref handler);
		if (num9 > 0.0)
		{
			double num10 = 100.0 * num9 / (double)num6;
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder8 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(54, 3, stringBuilder2);
			handler.AppendLiteral("<div class=\"avg\" style=\"bottom:");
			handler.AppendFormatted(num10.ToString("0.##", CultureInfo.InvariantCulture));
			handler.AppendLiteral("%\"><span>");
			handler.AppendFormatted(L.T("media", "avg"));
			handler.AppendLiteral(" ");
			handler.AppendFormatted(num9.ToString("0", Loc));
			handler.AppendLiteral("</span></div>");
			stringBuilder8.Append(ref handler);
		}
		for (int num11 = 0; num11 < list4.Count; num11++)
		{
			int num12 = list5[num11];
			double val = 100.0 * (double)num12 / (double)num6;
			string s = list4[num11].ToString(L.En ? "ddd MMM d" : "ddd dd/MM", Loc) + " · " + L.Count(num12, "cofre", "cofres", "chest", "chests");
			string value3 = val.ToString("0.##", CultureInfo.InvariantCulture);
			string value4 = Math.Max(val, 1.5).ToString("0.##", CultureInfo.InvariantCulture);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder9 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(131, 6, stringBuilder2);
			handler.AppendLiteral("<div class=\"col");
			handler.AppendFormatted((num11 == list4.Count - 1) ? " today" : "");
			handler.AppendLiteral("\" aria-label=\"");
			handler.AppendFormatted(H(s));
			handler.AppendLiteral("\"><span class=\"tip\" style=\"bottom:calc(");
			handler.AppendFormatted(value4);
			handler.AppendLiteral("% + 6px)\">");
			handler.AppendFormatted(H(s));
			handler.AppendLiteral("</span><div class=\"bar");
			handler.AppendFormatted((num12 == 0) ? " zero" : "");
			handler.AppendLiteral("\" style=\"height:");
			handler.AppendFormatted(value3);
			handler.AppendLiteral("%\"></div></div>");
			stringBuilder9.Append(ref handler);
		}
		stringBuilder.Append("</div><div class=\"xaxis\">");
		for (int num13 = 0; num13 < list4.Count; num13++)
		{
			string value5;
			if (num13 == list4.Count - 1)
			{
				value5 = "<span class=\"t\">" + L.T("hoy", "today") + "</span>";
			}
			else
			{
				value5 = (((list4.Count - 1 - num13) % 7 == 0) ? ("<span>" + list4[num13].ToString(L.En ? "MMM d" : "dd/MM", Loc) + "</span>") : "<span></span>");
			}
			stringBuilder.Append(value5);
		}
		stringBuilder.Append("</div></div></div></section>");
		bool hasValue = tuple.HasValue;
		int chestMax = Math.Max(1, (from m in matches.Take(100)
			select m.ChestsOpened).DefaultIfEmpty(0).Max());
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder10 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(40, 2, stringBuilder2);
		handler.AppendLiteral("<section><div class=\"sh\"><h2>");
		handler.AppendFormatted(L.T("Últimas partidas", "Latest matches"));
		handler.AppendLiteral("</h2>");
		handler.AppendFormatted((matches.Count > 6) ? ("<a class=\"more\" href=\"#partidas\" data-go=\"partidas\">" + L.T("Ver todas", "See all") + " →</a>") : "");
		handler.AppendLiteral("</div>");
		stringBuilder10.Append(ref handler);
		AppendMatchTable(stringBuilder, matches.Take(6).ToList(), chestMax, hasValue, readOnlyCollection);
		stringBuilder.Append("</section></div>");
		stringBuilder.Append("<div class=\"panel\" id=\"t-cofres\" role=\"tabpanel\" aria-labelledby=\"tab-cofres\">");
		List<(string, int)> list6 = (from c in matches.SelectMany((MatchRecord m) => m.Chests)
			group c by c.Kind into g
			select (Kind: g.Key, Count: g.Count()) into g
			orderby g.Count descending
			select g).ToList();
		List<(string, int)> list7 = (from kv in matches.SelectMany((MatchRecord m) => (m.OtherInteractions.Count <= 0) ? new Dictionary<string, int>
			{
				["Ammo box"] = m.AmmoBoxesOpened,
				["Other container"] = m.OtherContainersOpened
			} : m.OtherInteractions)
			group kv by kv.Key into g
			select (Kind: g.Key, Count: g.Sum((KeyValuePair<string, int> kv) => kv.Value)) into x
			where x.Count > 0
			orderby x.Count descending
			select x).ToList();
		int num14 = Math.Max(1, list6.Sum(((string Kind, int Count) k) => k.Count));
		int num15 = Math.Max(1, list6.Select(((string Kind, int Count) k) => k.Count).DefaultIfEmpty(0).Max());
		stringBuilder.Append("<div class=\"grid2\">");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder11 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(106, 2, stringBuilder2);
		handler.AppendLiteral("<section><div class=\"sh\"><h2>");
		handler.AppendFormatted(L.T("Por tipo de cofre", "By chest type"));
		handler.AppendLiteral("</h2><span class=\"meta\">");
		handler.AppendFormatted(L.Count(list6.Sum(((string Kind, int Count) k) => k.Count), "cofre", "cofres", "chest", "chests"));
		handler.AppendLiteral("</span></div><div class=\"card pad\"><div class=\"rows\">");
		stringBuilder11.Append(ref handler);
		if (list6.Count == 0)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder12 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(25, 1, stringBuilder2);
			handler.AppendLiteral("<div class=\"empty\">");
			handler.AppendFormatted(L.T("Aún no hay cofres registrados.", "No chests recorded yet."));
			handler.AppendLiteral("</div>");
			stringBuilder12.Append(ref handler);
		}
		foreach (var item11 in list6)
		{
			string item4 = item11.Item1;
			int item5 = item11.Item2;
			double num16 = 100.0 * (double)item5 / (double)num14;
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder13 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(124, 4, stringBuilder2);
			handler.AppendLiteral("<div class=\"row\"><span>");
			handler.AppendFormatted(H(L.Name(item4)));
			handler.AppendLiteral("</span><span class=\"n\">");
			handler.AppendFormatted(N(item5));
			handler.AppendLiteral("<small>");
			handler.AppendFormatted(num16.ToString("0", Loc));
			handler.AppendLiteral(" %</small></span><div class=\"track\"><i style=\"width:");
			handler.AppendFormatted((100.0 * (double)item5 / (double)num15).ToString("0.##", CultureInfo.InvariantCulture));
			handler.AppendLiteral("%\"></i></div></div>");
			stringBuilder13.Append(ref handler);
		}
		int num17 = matches.Sum((MatchRecord m) => Enumerable.Count(m.Chests, (ChestRecord c) => c.Destroyed));
		if (num17 > 0)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder14 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(65, 2, stringBuilder2);
			handler.AppendLiteral("<div class=\"row sub\"><span>↳ ");
			handler.AppendFormatted(L.T("de ellos, destruidos en vez de abiertos", "of which destroyed instead of opened"));
			handler.AppendLiteral("</span><span class=\"n\">");
			handler.AppendFormatted(N(num17));
			handler.AppendLiteral("</span></div>");
			stringBuilder14.Append(ref handler);
		}
		stringBuilder.Append("</div></div></section>");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder15 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(106, 2, stringBuilder2);
		handler.AppendLiteral("<section><div class=\"sh\"><h2>");
		handler.AppendFormatted(L.T("Otros contenedores", "Other containers"));
		handler.AppendLiteral("</h2><span class=\"meta\">");
		handler.AppendFormatted(L.T("se activan en Ajustes", "turned on in Settings"));
		handler.AppendLiteral("</span></div><div class=\"card pad\"><div class=\"rows\">");
		stringBuilder15.Append(ref handler);
		if (list7.Count == 0)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder16 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(25, 1, stringBuilder2);
			handler.AppendLiteral("<div class=\"empty\">");
			handler.AppendFormatted(L.T("Aún no hay cajas ni cañones registrados.", "No boxes or cannons recorded yet."));
			handler.AppendLiteral("</div>");
			stringBuilder16.Append(ref handler);
		}
		int num18 = Math.Max(1, list7.Select(((string Kind, int Count) k) => k.Count).DefaultIfEmpty(0).Max());
		foreach (var item12 in list7)
		{
			string item6 = item12.Item1;
			int item7 = item12.Item2;
			bool flag = readOnlyCollection.Contains(item6);
			string value6 = (flag ? ("<span class=\"badge top\">" + L.T("suma al total", "added to the total") + "</span>") : ("<span class=\"badge\">" + L.T("fuera del total", "not in the total") + "</span>"));
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder17 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(120, 5, stringBuilder2);
			handler.AppendLiteral("<div class=\"row");
			handler.AppendFormatted(flag ? "" : " off");
			handler.AppendLiteral("\"><span class=\"lbl\">");
			handler.AppendFormatted(H(L.Name(item6)));
			handler.AppendLiteral(" ");
			handler.AppendFormatted(value6);
			handler.AppendLiteral("</span><span class=\"n\">");
			handler.AppendFormatted(N(item7));
			handler.AppendLiteral("</span><div class=\"track\"><i style=\"width:");
			handler.AppendFormatted((100.0 * (double)item7 / (double)num18).ToString("0.##", CultureInfo.InvariantCulture));
			handler.AppendLiteral("%\"></i></div></div>");
			stringBuilder17.Append(ref handler);
		}
		stringBuilder.Append("</div></div></section></div>");
		List<(string, int, int, int, int)> list8 = (from m in matches
			group m by Pretty.Playlist(m.Playlist) into g
			select (Mode: g.Key, Matches: g.Count(), Chests: g.Sum((MatchRecord m) => m.ChestsOpened), Best: g.Max((MatchRecord m) => m.ChestsOpened), Wins: g.Count((MatchRecord m) => m.Placement == 1)) into x
			orderby x.Matches descending
			select x).ToList();
		if (list8.Count > 0)
		{
			double num19 = Math.Max(0.1, list8.Max(((string Mode, int Matches, int Chests, int Best, int Wins) x) => (double)x.Chests / (double)x.Matches));
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder18 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(230, 8, stringBuilder2);
			handler.AppendLiteral("<section><div class=\"sh\"><h2>");
			handler.AppendFormatted(L.T("Por modo de juego", "By game mode"));
			handler.AppendLiteral("</h2><span class=\"meta\">");
			handler.AppendFormatted(L.Count(list8.Count, "modo", "modos", "mode", "modes"));
			handler.AppendLiteral("</span></div>\n<div class=\"card tw\"><table><thead><tr><th>");
			handler.AppendFormatted(L.T("Modo", "Mode"));
			handler.AppendLiteral("</th><th class=\"r\">");
			handler.AppendFormatted(L.T("Partidas", "Matches"));
			handler.AppendLiteral("</th><th class=\"r\">");
			handler.AppendFormatted(L.T("Media", "Average"));
			handler.AppendLiteral("</th><th class=\"r\">");
			handler.AppendFormatted(L.T("Récord", "Best"));
			handler.AppendLiteral("</th><th class=\"r\">");
			handler.AppendFormatted(L.T("Cofres", "Chests"));
			handler.AppendLiteral("</th><th class=\"r\">");
			handler.AppendFormatted(L.T("Victorias", "Wins"));
			handler.AppendLiteral("</th></tr></thead><tbody>");
			stringBuilder18.Append(ref handler);
			foreach (var item13 in list8)
			{
				double num20 = (double)item13.Item3 / (double)item13.Item2;
				string value7 = (100.0 * num20 / num19).ToString("0.##", CultureInfo.InvariantCulture);
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder19 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(217, 7, stringBuilder2);
				handler.AppendLiteral("<tr><td><span class=\"badge mode\">");
				handler.AppendFormatted(H(item13.Item1));
				handler.AppendLiteral("</span></td><td class=\"r\">");
				handler.AppendFormatted(N(item13.Item2));
				handler.AppendLiteral("</td><td><div class=\"chests\"><span class=\"mini\"><i style=\"width:");
				handler.AppendFormatted(value7);
				handler.AppendLiteral("%\"></i></span><b>");
				handler.AppendFormatted(num20.ToString("0.0", Loc));
				handler.AppendLiteral("</b></div></td><td class=\"r\">");
				handler.AppendFormatted(N(item13.Item4));
				handler.AppendLiteral("</td><td class=\"r\">");
				handler.AppendFormatted(N(item13.Item3));
				handler.AppendLiteral("</td><td class=\"r\">");
				handler.AppendFormatted((item13.Item5 > 0) ? ("<span class=\"badge win\">\ud83c\udfc6 " + N(item13.Item5) + "</span>") : "<span class=\"muted\">—</span>");
				handler.AppendLiteral("</td></tr>");
				stringBuilder19.Append(ref handler);
			}
			stringBuilder.Append("</tbody></table></div></section>");
		}
		stringBuilder.Append("</div>");
		stringBuilder.Append("<div class=\"panel\" id=\"t-tesoros\" role=\"tabpanel\" aria-labelledby=\"tab-tesoros\">");
		if (list2.Count > 0)
		{
			List<(MatchRecord, TreasureFind)> list9 = list2.Where(((MatchRecord m, TreasureFind f) r) => !r.f.SourceIsChest).ToList();
			if (list9.Count > 0)
			{
				string value8 = H(string.Join(", ", list9.Select(((MatchRecord m, TreasureFind f) r) => L.Name(r.f.SourceKind)).Distinct()));
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder20 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(72, 4, stringBuilder2);
				handler.AppendLiteral("<div class=\"flag\"><span class=\"ic\">⚑</span><div><b>");
				handler.AppendFormatted(L.Count(list9.Count, "tesoro obtenido", "tesoros obtenidos", "treasure obtained", "treasures obtained"));
				handler.AppendLiteral(" ");
				handler.AppendFormatted(L.T("de otra forma que con un cofre", "other than from a chest"));
				handler.AppendLiteral("</b>: ");
				handler.AppendFormatted(value8);
				handler.AppendLiteral(". ");
				handler.AppendFormatted(L.T("Resaltados en la tabla de abajo.", "Highlighted in the table below."));
				handler.AppendLiteral("</div></div>");
				stringBuilder20.Append(ref handler);
			}
			int n6 = Enumerable.Count(list2, ((MatchRecord m, TreasureFind f) r) => r.f.FromGoldenChest);
			int n7 = Enumerable.Count(list2, ((MatchRecord m, TreasureFind f) r) => r.f.SourceCertain);
			int num21 = list2.Select(((MatchRecord m, TreasureFind f) r) => r.m).Distinct().Count();
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder21 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(256, 8, stringBuilder2);
			handler.AppendLiteral("<div class=\"minis\">\n  <div class=\"mini-kpi\"><b class=\"ros\">");
			handler.AppendFormatted(N(list2.Count));
			handler.AppendLiteral("</b><span>");
			handler.AppendFormatted(L.T("tesoros en total", "treasures in total"));
			handler.AppendLiteral("</span></div>\n  <div class=\"mini-kpi\"><b class=\"acc\">");
			handler.AppendFormatted(N(n6));
			handler.AppendLiteral("</b><span>");
			handler.AppendFormatted(L.T("de cofres dorados", "from golden chests"));
			handler.AppendLiteral("</span></div>\n  <div class=\"mini-kpi\"><b>");
			handler.AppendFormatted(N(n7));
			handler.AppendLiteral("</b><span>");
			handler.AppendFormatted(L.T("con origen seguro", "with a certain source"));
			handler.AppendLiteral("</span></div>\n  <div class=\"mini-kpi\"><b>");
			handler.AppendFormatted((matches.Count > 0) ? (100.0 * (double)num21 / (double)matches.Count).ToString("0", Loc) : "0");
			handler.AppendLiteral(" %</b><span>");
			handler.AppendFormatted(L.T("de partidas con tesoro", "of matches with a treasure"));
			handler.AppendLiteral("</span></div>\n</div>");
			stringBuilder21.Append(ref handler);
			List<IGrouping<string, (MatchRecord, TreasureFind)>> list10 = list2.GroupBy(((MatchRecord m, TreasureFind f) r) => r.f.SourceLabel).OrderByDescending((IGrouping<string, (MatchRecord m, TreasureFind f)> g) => g.Count()).ToList();
			int num22 = Math.Max(1, list10.Max((IGrouping<string, (MatchRecord m, TreasureFind f)> g) => g.Count()));
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder22 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(106, 2, stringBuilder2);
			handler.AppendLiteral("<section><div class=\"sh\"><h2>");
			handler.AppendFormatted(L.T("De dónde vienen los tesoros", "Where the treasures come from"));
			handler.AppendLiteral("</h2><span class=\"meta\">");
			handler.AppendFormatted(L.T("seguros / total", "certain / total"));
			handler.AppendLiteral("</span></div><div class=\"card pad\"><div class=\"rows\">");
			stringBuilder22.Append(ref handler);
			foreach (IGrouping<string, (MatchRecord, TreasureFind)> item14 in list10)
			{
				bool sourceIsChest = item14.First().Item2.SourceIsChest;
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder23 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(124, 6, stringBuilder2);
				handler.AppendLiteral("<div class=\"row");
				handler.AppendFormatted(sourceIsChest ? " rose" : " warn");
				handler.AppendLiteral("\"><span>");
				handler.AppendFormatted(sourceIsChest ? "" : "⚑ ");
				handler.AppendFormatted(H(item14.Key));
				handler.AppendLiteral("</span><span class=\"n\">");
				handler.AppendFormatted(item14.Count(((MatchRecord m, TreasureFind f) r) => r.f.SourceCertain));
				handler.AppendLiteral("<small>/ ");
				handler.AppendFormatted(item14.Count());
				handler.AppendLiteral("</small></span><div class=\"track\"><i style=\"width:");
				handler.AppendFormatted((100.0 * (double)item14.Count() / (double)num22).ToString("0.##", CultureInfo.InvariantCulture));
				handler.AppendLiteral("%\"></i></div></div>");
				stringBuilder23.Append(ref handler);
			}
			stringBuilder.Append("</div></div></section>");
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder24 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(162, 6, stringBuilder2);
			handler.AppendLiteral("<section><div class=\"sh\"><h2>");
			handler.AppendFormatted(L.T("Tesoros obtenidos", "Treasures obtained"));
			handler.AppendLiteral("</h2><span class=\"meta\">");
			handler.AppendFormatted(L.Count(list2.Count, "tesoro", "tesoros", "treasure", "treasures"));
			handler.AppendLiteral("</span></div>\n<div class=\"card tw\"><table><thead><tr><th>");
			handler.AppendFormatted(L.T("Partida", "Match"));
			handler.AppendLiteral("</th><th>");
			handler.AppendFormatted(L.T("Tesoro", "Treasure"));
			handler.AppendLiteral("</th><th>");
			handler.AppendFormatted(L.T("Momento", "Time"));
			handler.AppendLiteral("</th><th>");
			handler.AppendFormatted(L.T("Origen", "Source"));
			handler.AppendLiteral("</th></tr></thead><tbody>");
			stringBuilder24.Append(ref handler);
			foreach (var item15 in list2)
			{
				MatchRecord item8 = item15.Item1;
				TreasureFind item9 = item15.Item2;
				string value9;
				if (item9.PrizeId != null)
				{
					value9 = "<div class=\"sm\">" + H(item9.PrizeId) + "</div>";
				}
				else
				{
					value9 = (item9.RawItem.StartsWith("WID_") ? ("<div class=\"sm\">" + H(item9.RawItem) + "</div>") : "");
				}
				float? obtainedAtSeconds = item9.ObtainedAtSeconds;
				object obj;
				if (obtainedAtSeconds.HasValue)
				{
					float valueOrDefault = obtainedAtSeconds.GetValueOrDefault();
					obj = (item9.TimeExact ? Pretty.Clock(valueOrDefault) : ("<span class=\"sm\">" + L.T("hacia", "around") + "</span> " + Pretty.Clock(valueOrDefault)));
				}
				else
				{
					obj = "<span class=\"muted\">—</span>";
				}
				string value10 = (string)obj;
				string value11 = (item9.SourceIsChest ? "" : "⚑ ") + H(item9.SourceLabel) + (item9.SourceCertain ? "" : (" <span class=\"sm\">(" + Pretty.Likely + ")</span>")) + ((!item9.SourceIsChest && !string.IsNullOrEmpty(item9.SourceBlueprint)) ? ("<div class=\"sm\">" + H(item9.SourceBlueprint) + "</div>") : "") + ((item9.AlsoPossible.Count > 0) ? ("<div class=\"sm\">" + L.T("también posible: ", "also possible: ") + H(string.Join(", ", item9.AlsoPossible.Select(L.Name))) + "</div>") : "");
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder25 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(120, 8, stringBuilder2);
				handler.AppendLiteral("<tr");
				handler.AppendFormatted(item9.SourceIsChest ? "" : " class=\"unusual\"");
				handler.AppendLiteral("><td class=\"nw\">");
				handler.AppendFormatted(L.DateTimeShort(When(item8)));
				handler.AppendLiteral("<div class=\"sm\">");
				handler.AppendFormatted(H(Pretty.Playlist(item8.Playlist)));
				handler.AppendLiteral("</div></td><td><span class=\"star\">");
				handler.AppendFormatted(item9.FromGoldenChest ? "★★" : "★");
				handler.AppendLiteral(" ");
				handler.AppendFormatted(H(item9.DisplayName));
				handler.AppendLiteral("</span>");
				handler.AppendFormatted(value9);
				handler.AppendLiteral("</td><td class=\"nw num\">");
				handler.AppendFormatted(value10);
				handler.AppendLiteral("</td><td>");
				handler.AppendFormatted(value11);
				handler.AppendLiteral("</td></tr>");
				stringBuilder25.Append(ref handler);
			}
			stringBuilder.Append("</tbody></table></div></section>");
		}
		else
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder26 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(78, 1, stringBuilder2);
			handler.AppendLiteral("<div class=\"card\"><div class=\"empty big\"><div class=\"star\">★</div>");
			handler.AppendFormatted(L.T("Todavía no has encontrado tesoros. Aparecerán aquí con el momento y el cofre de origen.", "No treasures yet. They will show up here with the time and the chest they came from."));
			handler.AppendLiteral("</div></div>");
			stringBuilder26.Append(ref handler);
		}
		stringBuilder.Append("</div>");
		List<MatchRecord> list11 = matches.Take(100).ToList();
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder27 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(149, 2, stringBuilder2);
		handler.AppendLiteral("<div class=\"panel\" id=\"t-partidas\" role=\"tabpanel\" aria-labelledby=\"tab-partidas\">\n<section><div class=\"sh\"><h2>");
		handler.AppendFormatted(L.T("Últimas partidas", "Latest matches"));
		handler.AppendLiteral("</h2><span class=\"meta\">");
		handler.AppendFormatted((matches.Count > list11.Count) ? L.F("las {0} más recientes de {1}", "latest {0} of {1}", list11.Count, N(matches.Count)) : L.Count(matches.Count, "partida", "partidas", "match", "matches"));
		handler.AppendLiteral("</span></div>");
		stringBuilder27.Append(ref handler);
		AppendMatchTable(stringBuilder, list11, chestMax, hasValue, readOnlyCollection);
		stringBuilder.Append("</section></div>");
		if (tuple.HasValue)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder28 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(92, 1, stringBuilder2);
			handler.AppendLiteral("<div class=\"panel\" id=\"t-companeros\" role=\"tabpanel\" aria-labelledby=\"tab-companeros\">");
			handler.AppendFormatted(tuple.Value.Item1);
			handler.AppendLiteral("</div>");
			stringBuilder28.Append(ref handler);
		}
		string text4 = L.Big("footer", "<footer><h3>Cómo se cuenta</h3>\n<p>Cuenta los cofres que abriste o destruiste tú mismo, según tus archivos de replay (analizados tras cada partida). Las cajas de munición, las cajas de comida y los cañones de botín solo cuentan si lo activas en Ajustes.</p>\n<p><span class=\"star\">★★</span> cofre dorado de la caza del tesoro (cosmético, acceso a la tienda…), detectado con seguridad. <span class=\"star\">★</span> tesoro recibido gracias a uno de tus cofres (momento exacto), con nombre cuando el objeto se puede identificar.</p>\n<p>Los nombres de los premios se pueden editar en <code>treasures.json</code>. Esta página se actualiza sola.</p></footer>\n</main></body></html>", "<footer><h3>How it counts</h3>\n<p>Counts the chests you opened or destroyed yourself, from your replay files (analyzed after each match). Ammo boxes, food boxes and loot cannons only count if you turn it on in Settings.</p>\n<p><span class=\"star\">★★</span> golden treasure hunt chest (cosmetic, shop access…), detected for sure. <span class=\"star\">★</span> treasure received from one of your own chests (exact moment), named when the item can be identified.</p>\n<p>Prize names can be edited in <code>treasures.json</code>. This page refreshes by itself.</p></footer>\n</main></body></html>");
		stringBuilder.Append(text4.Replace("</main></body></html>", "</main><script>(function(){var d=document,tabs=[].slice.call(d.querySelectorAll('[data-tab]'));if(!tabs.length)return;d.documentElement.classList.add('js');\nfunction get(k,s){try{return (s?sessionStorage:localStorage).getItem(k)}catch(e){return null}}\nfunction put(k,v,s){try{(s?sessionStorage:localStorage).setItem(k,v)}catch(e){}}\nfunction show(id,save){var t=null;tabs.forEach(function(b){var on=b.getAttribute('data-tab')===id;b.setAttribute('aria-selected',on?'true':'false');b.tabIndex=on?0:-1;if(on)t=b});\n  if(!t)return false;var bar=t.parentNode;bar.scrollLeft=t.offsetLeft-(bar.clientWidth-t.offsetWidth)/2;[].forEach.call(d.querySelectorAll('.panel'),function(p){p.hidden=p.id!=='t-'+id});if(save)put('cc-tab',id,true);return true}\nvar h=location.hash.slice(1);if(!(h&&show(h,true))&&!show(get('cc-tab',true)||'',false))show(tabs[0].getAttribute('data-tab'),false);\nfunction go(id){if(show(id,true)){window.scrollTo(0,0);try{history.replaceState(null,'','#'+id)}catch(e){}}}\ntabs.forEach(function(b,i){b.addEventListener('click',function(){go(b.getAttribute('data-tab'))});\n  b.addEventListener('keydown',function(e){var k=e.key==='ArrowRight'?1:e.key==='ArrowLeft'?-1:0;if(!k)return;e.preventDefault();var n=tabs[(i+k+tabs.length)%tabs.length];go(n.getAttribute('data-tab'));n.focus()})});\n[].forEach.call(d.querySelectorAll('[data-go]'),function(a){a.addEventListener('click',function(e){e.preventDefault();go(a.getAttribute('data-go'))})});\nvar y=get('cc-y',true);if(y)window.scrollTo(0,+y);addEventListener('pagehide',function(){put('cc-y',String(window.scrollY),true)});\n})();</script></body></html>"));
		return stringBuilder.ToString();
		static string N(int num23)
		{
			return num23.ToString("#,0", Loc);
		}
	}

	private static void AppendMatchTable(StringBuilder sb, List<MatchRecord> rows, int chestMax, bool withMates, IReadOnlyCollection<string> kinds)
	{
		bool flag = rows.Any((MatchRecord m) =>
		{
			int? eliminations2 = m.Eliminations;
			return eliminations2.HasValue && eliminations2.GetValueOrDefault() > 0;
		});
		int value = 5 + (flag ? 1 : 0) + (withMates ? 1 : 0);
		string value2 = (withMates ? L.T("Tú", "You") : L.T("Cofres", "Chests"));
		StringBuilder stringBuilder = sb;
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(130, 7, stringBuilder);
		handler.AppendLiteral("<div class=\"card tw\"><table class=\"matches\"><thead><tr><th>");
		handler.AppendFormatted(L.T("Fecha", "Date"));
		handler.AppendLiteral("</th><th>");
		handler.AppendFormatted(L.T("Modo", "Mode"));
		handler.AppendLiteral("</th><th>");
		handler.AppendFormatted(L.T("Puesto", "Placement"));
		handler.AppendLiteral("</th>");
		handler.AppendFormatted(flag ? ("<th class=\"r\">" + L.T("Elim.", "Elims") + "</th>") : "");
		handler.AppendLiteral("<th class=\"r\">");
		handler.AppendFormatted(value2);
		handler.AppendLiteral("</th>");
		handler.AppendFormatted(withMates ? ("<th class=\"r\">" + L.T("Equipo", "Team") + "</th>") : "");
		handler.AppendLiteral("<th>");
		handler.AppendFormatted(L.T("Tesoros", "Treasures"));
		handler.AppendLiteral("</th></tr></thead><tbody>");
		stringBuilder2.Append(ref handler);
		if (rows.Count == 0)
		{
			stringBuilder = sb;
			StringBuilder stringBuilder3 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(43, 2, stringBuilder);
			handler.AppendLiteral("<tr><td colspan=\"");
			handler.AppendFormatted(value);
			handler.AppendLiteral("\" class=\"empty\">");
			handler.AppendFormatted(L.T("Aún no hay partidas analizadas. Juega una y aparecerá aquí.", "No matches analyzed yet. Play one and it will show up here."));
			handler.AppendLiteral("</td></tr>");
			stringBuilder3.Append(ref handler);
		}
		foreach (MatchRecord row in rows)
		{
			string value3 = ((row.TreasureFinds.Count == 0) ? "<span class=\"muted\">—</span>" : string.Concat("<div class=\"tlist\">", string.Concat(row.TreasureFinds.Select((TreasureFind f) => $"<span class=\"star\">{(f.FromGoldenChest ? "★★" : "★")} {H(f.DisplayName)}</span>")), "</div>"));
			string text;
			switch (row.Placement)
			{
			case 1:
				text = " win";
				break;
			case 2:
			case 3:
			case 4:
			case 5:
			case 6:
			case 7:
			case 8:
			case 9:
			case 10:
				text = " top";
				break;
			default:
				text = "";
				break;
			}
			string value4 = text;
			string value5 = ((row.Placement == 1) ? ("\ud83c\udfc6 " + H(Pretty.Placement(row))) : H(Pretty.Placement(row)));
			int num = (withMates ? row.OwnChests : row.ChestsOpened);
			string value6 = (100.0 * (double)num / (double)Math.Max(1, chestMax)).ToString("0.##", CultureInfo.InvariantCulture);
			string text2;
			if (withMates)
			{
				List<TeammateRecord> teammates = row.Teammates;
				if (teammates != null && teammates.Count > 0)
				{
					text2 = string.Concat("<div class=\"mates-line\">", string.Concat(from x in teammates
						orderby MatchRecord.Seen(x, kinds) descending
						select $"<span class=\"chip\"><span class=\"cn\">{H(x.Name)}</span> <b>≥ {MatchRecord.Seen(x, kinds)}</b></span>"), "</div>");
					goto IL_03b1;
				}
			}
			text2 = "";
			goto IL_03b1;
			IL_03b1:
			string value7 = text2;
			object obj;
			if (withMates)
			{
				List<TeammateRecord> teammates2 = row.Teammates;
				obj = ((teammates2 != null && teammates2.Count > 0) ? ("<td class=\"r nw\"><span class=\"sm\">≥</span> <b class=\"cy\">" + N0(row.OwnChests + row.TeammatesSeen(kinds)) + "</b></td>") : "<td class=\"r\"><span class=\"muted\">—</span></td>");
			}
			else
			{
				obj = "";
			}
			string value8 = (string)obj;
			string text3;
			if (!flag)
			{
				text3 = "";
			}
			else
			{
				int? eliminations = row.Eliminations;
				string text4;
				if (eliminations.HasValue)
				{
					int valueOrDefault = eliminations.GetValueOrDefault();
					text4 = N0(valueOrDefault);
				}
				else
				{
					text4 = "<span class=\"muted\">—</span>";
				}
				text3 = "<td class=\"r\">" + text4 + "</td>";
			}
			string value9 = text3;
			stringBuilder = sb;
			StringBuilder stringBuilder4 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(206, 11, stringBuilder);
			handler.AppendLiteral("<tr");
			handler.AppendFormatted((row.Placement == 1) ? " class=\"won\"" : "");
			handler.AppendLiteral("><td class=\"nw\">");
			handler.AppendFormatted(L.DateTimeShort(When(row)));
			handler.AppendLiteral("</td><td><span class=\"badge mode\">");
			handler.AppendFormatted(H(Pretty.Playlist(row.Playlist)));
			handler.AppendLiteral("</span>");
			handler.AppendFormatted(value7);
			handler.AppendLiteral("</td><td><span class=\"badge");
			handler.AppendFormatted(value4);
			handler.AppendLiteral("\">");
			handler.AppendFormatted(value5);
			handler.AppendLiteral("</span></td>");
			handler.AppendFormatted(value9);
			handler.AppendLiteral("<td><div class=\"chests\"><span class=\"mini\"><i style=\"width:");
			handler.AppendFormatted(value6);
			handler.AppendLiteral("%\"></i></span><b>");
			handler.AppendFormatted(num);
			handler.AppendLiteral("</b></div></td>");
			handler.AppendFormatted(value8);
			handler.AppendLiteral("<td>");
			handler.AppendFormatted(value3);
			handler.AppendLiteral("</td></tr>");
			stringBuilder4.Append(ref handler);
		}
		sb.Append("</tbody></table></div>");
	}

	private static string N0(int n)
	{
		return n.ToString("#,0", Loc);
	}

	private static (string Html, int Count) TeammatesPanel(List<MatchRecord> matches, IReadOnlyCollection<string> extraKinds, Func<int, string> N)
	{
		List<(string, string, int, int, int, int, int, DateTime, int)> list = (from r in matches.Where((MatchRecord m) => m.Teammates != null).SelectMany((MatchRecord m) => m.Teammates.Select((TeammateRecord t) => (m: m, t: t))).GroupBy(((MatchRecord m, TeammateRecord t) r) => r.t.EpicId ?? r.t.Name, StringComparer.OrdinalIgnoreCase)
				.Select((IGrouping<string, (MatchRecord m, TeammateRecord t)> g) =>
				{
					(MatchRecord, TeammateRecord) tuple = g.OrderByDescending(((MatchRecord m, TeammateRecord t) r) => When(r.m)).First();
					List<MatchRecord> list5 = g.Select(((MatchRecord m, TeammateRecord t) r) => r.m).Distinct().ToList();
					return (Key: g.Key, Name: tuple.Item2.Name, Matches: list5.Count, Chests: g.Sum(((MatchRecord m, TeammateRecord t) r) => Seen(r.t)), Mine: list5.Sum((MatchRecord m) => m.OwnChests), Best: g.Max(((MatchRecord m, TeammateRecord t) r) => Seen(r.t)), Wins: Enumerable.Count(list5, (MatchRecord m) => m.Placement == 1), Last: When(tuple.Item1), Team: list5.Sum((MatchRecord m) => m.OwnChests + m.TeammatesSeen(extraKinds)));
				})
			orderby r.Matches descending, r.Chests descending
			select r).ToList();
		StringBuilder stringBuilder = new StringBuilder();
		int n = Enumerable.Count(matches, (MatchRecord m) =>
		{
			List<TeammateRecord> teammates = m.Teammates;
			return teammates != null && teammates.Count > 0;
		});
		string[] tones = new string[5] { "var(--cyan)", "var(--amber)", "var(--mint)", "var(--rose)", "var(--blue)" };
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(63, 1, stringBuilder2);
		handler.AppendLiteral("<div class=\"callout\"><span class=\"ic\">i</span><div>");
		handler.AppendFormatted(L.T("Se usa el recuento personal de cada compañero si importas su repetición de la misma partida. En las demás partidas se cuentan los cofres de compañeros que aparecen en tu repetición.", "An imported replay uses the teammate’s personal count for that match. Otherwise the report counts teammate chests visible in your replay."));
		handler.AppendLiteral("</div></div>");
		stringBuilder3.Append(ref handler);
		if (list.Count == 0)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder4 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(53, 1, stringBuilder2);
			handler.AppendLiteral("<div class=\"card\"><div class=\"empty big\">");
			handler.AppendFormatted(L.T("Aún no hay partidas en equipo analizadas. Aparecerán aquí tras tu próxima partida en dúos, tríos o escuadrones.", "No team matches analyzed yet. They will show up here after your next duos, trios or squads match."));
			handler.AppendLiteral("</div></div>");
			stringBuilder4.Append(ref handler);
			return (Html: stringBuilder.ToString(), Count: 0);
		}
		List<MatchRecord> list2 = matches.Where((MatchRecord m) =>
		{
			List<TeammateRecord> teammates = m.Teammates;
			return teammates != null && teammates.Count > 0;
		}).ToList();
		int num = list2.Sum((MatchRecord m) => m.OwnChests);
		int num2 = list2.Sum((MatchRecord m) => m.TeammatesSeen(extraKinds));
		int num3 = num + num2;
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder5 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(391, 10, stringBuilder2);
		handler.AppendLiteral("<section><div class=\"sh\"><h2>");
		handler.AppendFormatted(L.T("Cofres del equipo", "Team chests"));
		handler.AppendLiteral("</h2><span class=\"meta\">");
		handler.AppendFormatted(L.Count(list2.Count, "partida en equipo", "partidas en equipo", "team match", "team matches"));
		handler.AppendLiteral("</span></div>\n<div class=\"minis\">\n  <div class=\"mini-kpi\"><b class=\"cy\"><small>≥</small>");
		handler.AppendFormatted(N(num3));
		handler.AppendLiteral("</b><span>");
		handler.AppendFormatted(L.T("cofres del equipo en total", "team chests in total"));
		handler.AppendLiteral("</span></div>\n  <div class=\"mini-kpi\"><b class=\"cy\"><small>≥</small>");
		handler.AppendFormatted(Avg(num3, list2.Count));
		handler.AppendLiteral("</b><span>");
		handler.AppendFormatted(L.T("del equipo por partida", "per match for the team"));
		handler.AppendLiteral("</span></div>\n  <div class=\"mini-kpi\"><b class=\"acc\">");
		handler.AppendFormatted(Avg(num, list2.Count));
		handler.AppendLiteral("</b><span>");
		handler.AppendFormatted(L.T("tuyos por partida", "yours per match"));
		handler.AppendLiteral("</span></div>\n  <div class=\"mini-kpi\"><b><small>≥</small>");
		handler.AppendFormatted((num3 > 0) ? (100.0 * (double)num2 / (double)num3).ToString("0", Loc) : "0");
		handler.AppendLiteral(" %</b><span>");
		handler.AppendFormatted(L.T("aportan tus compañeros", "from your teammates"));
		handler.AppendLiteral("</span></div>\n</div></section>");
		stringBuilder5.Append(ref handler);
		string[] medals = new string[3] { "\ud83e\udd47", "\ud83e\udd48", "\ud83e\udd49" };
		int minAvg = ((Enumerable.Count(list, ((string Key, string Name, int Matches, int Chests, int Mine, int Best, int Wins, DateTime Last, int Team) r) => r.Matches >= 3) >= 3) ? 3 : 2);
		StringBuilder stringBuilder6 = new StringBuilder();
		Board(stringBuilder6, L.T("Más cofres vistos", "Most chests seen"), null, list.OrderByDescending(((string Key, string Name, int Matches, int Chests, int Mine, int Best, int Wins, DateTime Last, int Team) r) => r.Chests).ThenByDescending(((string Key, string Name, int Matches, int Chests, int Mine, int Best, int Wins, DateTime Last, int Team) r) => r.Matches).Take(5)
			.Select(((string Key, string Name, int Matches, int Chests, int Mine, int Best, int Wins, DateTime Last, int Team) r) => ((string Key, string Name, double, string, string))(Key: r.Key, Name: r.Name, r.Chests, "<small>≥</small>" + N(r.Chests), Games(r.Matches)))
			.ToList());
		Board(stringBuilder6, L.T("Mejor media por partida", "Best average per match"), L.F("Tu media en partidas en equipo: {0}", "Your average in team matches: {0}", Avg(num, list2.Count)), list.Where(((string Key, string Name, int Matches, int Chests, int Mine, int Best, int Wins, DateTime Last, int Team) r) => r.Matches >= minAvg).OrderByDescending(((string Key, string Name, int Matches, int Chests, int Mine, int Best, int Wins, DateTime Last, int Team) r) => (double)r.Chests / (double)r.Matches).Take(5)
			.Select(((string Key, string Name, int Matches, int Chests, int Mine, int Best, int Wins, DateTime Last, int Team) r) => (Key: r.Key, Name: r.Name, (double)r.Chests / (double)r.Matches, "<small>≥</small>" + ((double)r.Chests / (double)r.Matches).ToString("0.0", Loc), Games(r.Matches)))
			.ToList());
		Board(stringBuilder6, L.T("Récord en una partida", "Best single match"), null, list.Where(((string Key, string Name, int Matches, int Chests, int Mine, int Best, int Wins, DateTime Last, int Team) r) => r.Best > 0).OrderByDescending(((string Key, string Name, int Matches, int Chests, int Mine, int Best, int Wins, DateTime Last, int Team) r) => r.Best).ThenByDescending(((string Key, string Name, int Matches, int Chests, int Mine, int Best, int Wins, DateTime Last, int Team) r) => r.Matches)
			.Take(5)
			.Select(((string Key, string Name, int Matches, int Chests, int Mine, int Best, int Wins, DateTime Last, int Team) r) => ((string Key, string Name, double, string, string))(Key: r.Key, Name: r.Name, r.Best, "<small>≥</small>" + N(r.Best), L.T("cofres en una partida", "chests in one match")))
			.ToList());
		Board(stringBuilder6, L.T("Más victorias juntos", "Most wins together"), null, list.Where(((string Key, string Name, int Matches, int Chests, int Mine, int Best, int Wins, DateTime Last, int Team) r) => r.Wins > 0).OrderByDescending(((string Key, string Name, int Matches, int Chests, int Mine, int Best, int Wins, DateTime Last, int Team) r) => r.Wins).ThenBy(((string Key, string Name, int Matches, int Chests, int Mine, int Best, int Wins, DateTime Last, int Team) r) => r.Matches)
			.Take(5)
			.Select(((string Key, string Name, int Matches, int Chests, int Mine, int Best, int Wins, DateTime Last, int Team) r) => ((string Key, string Name, double, string, string))(Key: r.Key, Name: r.Name, r.Wins, "\ud83c\udfc6 " + N(r.Wins), L.F("{0} de {1}", "{0} of {1}", Pct(r.Wins, r.Matches), Games(r.Matches))))
			.ToList());
		if (stringBuilder6.Length > 0)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder7 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(102, 3, stringBuilder2);
			handler.AppendLiteral("<section><div class=\"sh\"><h2>");
			handler.AppendFormatted(L.T("Top de compañeros", "Top teammates"));
			handler.AppendLiteral("</h2><span class=\"meta\">");
			handler.AppendFormatted(L.T("cofres vistos: un mínimo", "chests seen: a minimum"));
			handler.AppendLiteral("</span></div><div class=\"boards\">");
			handler.AppendFormatted(stringBuilder6);
			handler.AppendLiteral("</div></section>");
			stringBuilder7.Append(ref handler);
		}
		List<MatchRecord> list3 = list2.Take(30).ToList();
		if (list3.Count > 0)
		{
			int num4 = Math.Max(1, list3.Max((MatchRecord m) => m.OwnChests + m.TeammatesSeen(extraKinds)));
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder8 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(191, 7, stringBuilder2);
			handler.AppendLiteral("<section><div class=\"sh\"><h2>");
			handler.AppendFormatted(L.T("Por partida", "Per match"));
			handler.AppendLiteral("</h2><span class=\"meta\">");
			handler.AppendFormatted(L.F("las {0} más recientes", "latest {0}", list3.Count));
			handler.AppendLiteral("</span></div>\n<div class=\"card tw\"><table><thead><tr><th>");
			handler.AppendFormatted(L.T("Fecha", "Date"));
			handler.AppendLiteral("</th><th>");
			handler.AppendFormatted(L.T("Puesto", "Placement"));
			handler.AppendLiteral("</th><th class=\"r\">");
			handler.AppendFormatted(L.T("Tú", "You"));
			handler.AppendLiteral("</th><th>");
			handler.AppendFormatted(L.T("Compañeros", "Teammates"));
			handler.AppendLiteral("</th><th class=\"r\">");
			handler.AppendFormatted(L.T("Equipo", "Team"));
			handler.AppendLiteral("</th></tr></thead><tbody>");
			stringBuilder8.Append(ref handler);
			foreach (MatchRecord item in list3)
			{
				string value = string.Concat(from t in item.Teammates
					orderby Seen(t) descending
					select $"<span class=\"chip\"><span class=\"av sm-av\" style=\"background:{Tone(t.EpicId ?? t.Name)}\">{Initial(t.Name)}</span><span class=\"cn\">{H(t.Name)}</span> <b>≥ {N(Seen(t))}</b></span>");
				int num5 = item.OwnChests + item.TeammatesSeen(extraKinds);
				string text;
				switch (item.Placement)
				{
				case 1:
					text = " win";
					break;
				case 2:
				case 3:
				case 4:
				case 5:
				case 6:
				case 7:
				case 8:
				case 9:
				case 10:
					text = " top";
					break;
				default:
					text = "";
					break;
				}
				string value2 = text;
				string value3 = ((item.Placement == 1) ? ("\ud83c\udfc6 " + H(Pretty.Placement(item))) : H(Pretty.Placement(item)));
				string value4 = (100.0 * (double)num5 / (double)num4).ToString("0.##", CultureInfo.InvariantCulture);
				string value5 = (100.0 * (double)item.OwnChests / (double)num4).ToString("0.##", CultureInfo.InvariantCulture);
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder9 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(315, 9, stringBuilder2);
				handler.AppendLiteral("<tr><td class=\"nw\">");
				handler.AppendFormatted(L.DateTimeShort(When(item)));
				handler.AppendLiteral("<div class=\"sm\">");
				handler.AppendFormatted(H(Pretty.Playlist(item.Playlist)));
				handler.AppendLiteral("</div></td><td><span class=\"badge");
				handler.AppendFormatted(value2);
				handler.AppendLiteral("\">");
				handler.AppendFormatted(value3);
				handler.AppendLiteral("</span></td>");
				handler.AppendLiteral("<td class=\"r\"><b class=\"acc\">");
				handler.AppendFormatted(N(item.OwnChests));
				handler.AppendLiteral("</b></td><td><div class=\"mates-line\">");
				handler.AppendFormatted(value);
				handler.AppendLiteral("</div></td>");
				handler.AppendLiteral("<td><div class=\"teambar\"><span class=\"tb\"><i class=\"me\" style=\"width:");
				handler.AppendFormatted(value5);
				handler.AppendLiteral("%\"></i><i style=\"width:");
				handler.AppendFormatted(value4);
				handler.AppendLiteral("%\"></i></span><b class=\"cy\"><small>≥</small>");
				handler.AppendFormatted(N(num5));
				handler.AppendLiteral("</b></div></td></tr>");
				stringBuilder9.Append(ref handler);
			}
			stringBuilder.Append("</tbody></table></div></section>");
		}
		List<(string, string, int, int, int, int, int, DateTime, int)> list4 = list.Where(((string Key, string Name, int Matches, int Chests, int Mine, int Best, int Wins, DateTime Last, int Team) r) => r.Matches >= 2).ToList();
		int num6 = list.Count - list4.Count;
		if (list4.Count == 0)
		{
			list4 = list.Take(20).ToList();
			num6 = Math.Max(0, list.Count - 20);
		}
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder10 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(88, 3, stringBuilder2);
		handler.AppendLiteral("<section><div class=\"sh\"><h2>");
		handler.AppendFormatted(L.T("Con quién más juegas", "Who you play with most"));
		handler.AppendLiteral("</h2><span class=\"meta\">");
		handler.AppendFormatted(L.Count(list.Count, "compañero", "compañeros", "teammate", "teammates"));
		handler.AppendLiteral(" · ");
		handler.AppendFormatted(L.Count(n, "partida en equipo", "partidas en equipo", "team match", "team matches"));
		handler.AppendLiteral("</span></div><div class=\"mates\">");
		stringBuilder10.Append(ref handler);
		foreach (var item2 in list4.Take(3))
		{
			double num7 = (double)item2.Item4 / (double)item2.Item3;
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder11 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(356, 13, stringBuilder2);
			handler.AppendLiteral("<div class=\"mate\">\n  <div class=\"who\"><div class=\"av\" style=\"background:");
			handler.AppendFormatted(Tone(item2.Item1));
			handler.AppendLiteral("\">");
			handler.AppendFormatted(Initial(item2.Item2));
			handler.AppendLiteral("</div>\n    <div class=\"grow\"><div class=\"nm\" title=\"");
			handler.AppendFormatted(H(item2.Item2));
			handler.AppendLiteral("\">");
			handler.AppendFormatted(H(item2.Item2));
			handler.AppendLiteral("</div><div class=\"sm\">");
			handler.AppendFormatted(L.Count(item2.Item3, "partida juntos", "partidas juntos", "match together", "matches together"));
			handler.AppendLiteral(" · ");
			handler.AppendFormatted(L.F("aporta ≥ {0} del equipo", "brings ≥ {0} of the team", Pct(item2.Item4, item2.Item9)));
			handler.AppendLiteral("</div></div></div>\n  <div class=\"st\">\n    <div><b class=\"acc\">");
			handler.AppendFormatted(N(item2.Item7));
			handler.AppendLiteral("</b><span>");
			handler.AppendFormatted(L.T("victorias", "wins"));
			handler.AppendLiteral(" · ");
			handler.AppendFormatted(Pct(item2.Item7, item2.Item3));
			handler.AppendLiteral("</span></div>\n    <div><b><small>≥</small>");
			handler.AppendFormatted(num7.ToString("0.0", Loc));
			handler.AppendLiteral("</b><span>");
			handler.AppendFormatted(L.T("cofres por partida", "chests per match"));
			handler.AppendLiteral("</span></div>\n    <div><b class=\"cy\">");
			handler.AppendFormatted(((double)item2.Item5 / (double)item2.Item3).ToString("0.0", Loc));
			handler.AppendLiteral("</b><span>");
			handler.AppendFormatted(L.T("tu media juntos", "your average together"));
			handler.AppendLiteral("</span></div>\n  </div>\n</div>");
			stringBuilder11.Append(ref handler);
		}
		stringBuilder.Append("</div></section>");
		double num8 = Math.Max(0.1, list4.Max(((string Key, string Name, int Matches, int Chests, int Mine, int Best, int Wins, DateTime Last, int Team) r) => (double)r.Chests / (double)r.Matches));
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder12 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(277, 11, stringBuilder2);
		handler.AppendLiteral("<section><div class=\"sh\"><h2>");
		handler.AppendFormatted(L.T("Todos tus compañeros", "All your teammates"));
		handler.AppendLiteral("</h2><span class=\"meta\">");
		handler.AppendFormatted(L.T("2 partidas o más", "2 matches or more"));
		handler.AppendLiteral("</span></div>\n<div class=\"card tw\"><table><thead><tr><th>");
		handler.AppendFormatted(L.T("Compañero", "Teammate"));
		handler.AppendLiteral("</th><th class=\"r\">");
		handler.AppendFormatted(L.T("Partidas juntos", "Matches together"));
		handler.AppendLiteral("</th><th class=\"r\">");
		handler.AppendFormatted(L.T("Victorias", "Wins"));
		handler.AppendLiteral("</th><th class=\"r\">");
		handler.AppendFormatted(L.T("Cofres vistos", "Chests seen"));
		handler.AppendLiteral("</th><th class=\"r\">");
		handler.AppendFormatted(L.T("Media", "Average"));
		handler.AppendLiteral("</th><th class=\"r\">");
		handler.AppendFormatted(L.T("Récord", "Best"));
		handler.AppendLiteral("</th><th class=\"r\">");
		handler.AppendFormatted(L.T("Aporta", "Share"));
		handler.AppendLiteral("</th><th class=\"r\">");
		handler.AppendFormatted(L.T("Tu media", "Your average"));
		handler.AppendLiteral("</th><th>");
		handler.AppendFormatted(L.T("Última partida", "Last match"));
		handler.AppendLiteral("</th></tr></thead><tbody>");
		stringBuilder12.Append(ref handler);
		foreach (var item3 in list4)
		{
			double num9 = (double)item3.Item4 / (double)item3.Item3;
			string value6 = (100.0 * num9 / num8).ToString("0.##", CultureInfo.InvariantCulture);
			string value7 = ((item3.Item7 > 0) ? $"<span class=\"badge win\">\ud83c\udfc6 {N(item3.Item7)}</span> <span class=\"sm\">{Pct(item3.Item7, item3.Item3)}</span>" : "<span class=\"muted\">—</span>");
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder13 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(411, 12, stringBuilder2);
			handler.AppendLiteral("<tr><td><div class=\"who\"><span class=\"av sm-av\" style=\"background:");
			handler.AppendFormatted(Tone(item3.Item1));
			handler.AppendLiteral("\">");
			handler.AppendFormatted(Initial(item3.Item2));
			handler.AppendLiteral("</span><span class=\"nm\">");
			handler.AppendFormatted(H(item3.Item2));
			handler.AppendLiteral("</span></div></td><td class=\"r\">");
			handler.AppendFormatted(N(item3.Item3));
			handler.AppendLiteral("</td><td class=\"r nw\">");
			handler.AppendFormatted(value7);
			handler.AppendLiteral("</td><td class=\"r nw\"><span class=\"sm\">≥</span> <b>");
			handler.AppendFormatted(N(item3.Item4));
			handler.AppendLiteral("</b></td>");
			handler.AppendLiteral("<td><div class=\"chests\"><span class=\"mini\"><i style=\"width:");
			handler.AppendFormatted(value6);
			handler.AppendLiteral("%\"></i></span><b>");
			handler.AppendFormatted(num9.ToString("0.0", Loc));
			handler.AppendLiteral("</b></div></td>");
			handler.AppendLiteral("<td class=\"r\">");
			handler.AppendFormatted(N(item3.Item6));
			handler.AppendLiteral("</td><td class=\"r nw\"><span class=\"sm\">≥</span> ");
			handler.AppendFormatted(Pct(item3.Item4, item3.Item9));
			handler.AppendLiteral("</td><td class=\"r\">");
			handler.AppendFormatted(((double)item3.Item5 / (double)item3.Item3).ToString("0.0", Loc));
			handler.AppendLiteral("</td><td class=\"nw sm\">");
			handler.AppendFormatted(L.DateTimeShort(item3.Item8));
			handler.AppendLiteral("</td></tr>");
			stringBuilder13.Append(ref handler);
		}
		if (num6 > 0)
		{
			string text2 = L.Count(num6, "compañero", "compañeros", "teammate", "teammates");
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder14 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(46, 1, stringBuilder2);
			handler.AppendLiteral("<tr><td colspan=\"9\" class=\"sm foot\">");
			handler.AppendFormatted((Enumerable.Count(list, ((string Key, string Name, int Matches, int Chests, int Mine, int Best, int Wins, DateTime Last, int Team) r) => r.Matches >= 2) > 0) ? L.F("Y {0} con una sola partida (relleno de equipo), no mostrados.", "And {0} with a single match (team fill), not shown.", text2) : L.F("Y {0} más.", "And {0} more.", text2));
			handler.AppendLiteral("</td></tr>");
			stringBuilder14.Append(ref handler);
		}
		stringBuilder.Append("</tbody></table></div></section>");
		return (Html: stringBuilder.ToString(), Count: list4.Count);
		static string Avg(int total, int count)
		{
			if (count <= 0)
			{
				return "—";
			}
			return ((double)total / (double)count).ToString("0.0", Loc);
		}
		void Board(StringBuilder b, string title, string? foot, List<(string Key, string Name, double Value, string Text, string Sub)> items)
		{
			if (items.Count != 0)
			{
				double num10 = Math.Max(0.0001, items.Max(((string Key, string Name, double Value, string Text, string Sub) i) => i.Value));
				StringBuilder stringBuilder15 = b;
				StringBuilder stringBuilder16 = stringBuilder15;
				StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(32, 1, stringBuilder15);
				handler2.AppendLiteral("<div class=\"board\"><h3>");
				handler2.AppendFormatted(title);
				handler2.AppendLiteral("</h3><ol>");
				stringBuilder16.Append(ref handler2);
				for (int num11 = 0; num11 < items.Count; num11++)
				{
					(string, string, double, string, string) tuple = items[num11];
					string value8 = ((num11 < medals.Length) ? ("<span class=\"rk medal\">" + medals[num11] + "</span>") : $"<span class=\"rk\">{num11 + 1}</span>");
					string value9 = (100.0 * tuple.Item3 / num10).ToString("0.##", CultureInfo.InvariantCulture);
					stringBuilder15 = b;
					StringBuilder stringBuilder17 = stringBuilder15;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(198, 9, stringBuilder15);
					handler2.AppendLiteral("<li");
					handler2.AppendFormatted((num11 == 0) ? " class=\"first\"" : "");
					handler2.AppendLiteral(">");
					handler2.AppendFormatted(value8);
					handler2.AppendLiteral("<span class=\"av sm-av\" style=\"background:");
					handler2.AppendFormatted(Tone(tuple.Item1));
					handler2.AppendLiteral("\">");
					handler2.AppendFormatted(Initial(tuple.Item2));
					handler2.AppendLiteral("</span>");
					handler2.AppendLiteral("<span class=\"who2\"><span class=\"nm\" title=\"");
					handler2.AppendFormatted(H(tuple.Item2));
					handler2.AppendLiteral("\">");
					handler2.AppendFormatted(H(tuple.Item2));
					handler2.AppendLiteral("</span><span class=\"sub\">");
					handler2.AppendFormatted(tuple.Item5);
					handler2.AppendLiteral("</span></span><b>");
					handler2.AppendFormatted(tuple.Item4);
					handler2.AppendLiteral("</b>");
					handler2.AppendLiteral("<span class=\"bar\"><i style=\"width:");
					handler2.AppendFormatted(value9);
					handler2.AppendLiteral("%\"></i></span></li>");
					stringBuilder17.Append(ref handler2);
				}
				b.Append("</ol>");
				if (foot != null)
				{
					stringBuilder15 = b;
					StringBuilder stringBuilder18 = stringBuilder15;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(25, 1, stringBuilder15);
					handler2.AppendLiteral("<div class=\"bfoot\">");
					handler2.AppendFormatted(foot);
					handler2.AppendLiteral("</div>");
					stringBuilder18.Append(ref handler2);
				}
				b.Append("</div>");
			}
		}
		static string Games(int n2)
		{
			return L.Count(n2, "partida", "partidas", "match", "matches");
		}
		static string Initial(string name)
		{
			return H((name.Trim().Length > 0) ? char.ToUpperInvariant(name.Trim()[0]).ToString() : "?");
		}
		static string Pct(int part, int whole)
		{
			if (whole <= 0)
			{
				return "—";
			}
			return (100.0 * (double)part / (double)whole).ToString("0", Loc) + " %";
		}
		int Seen(TeammateRecord t)
		{
			return MatchRecord.Seen(t, extraKinds);
		}
		string Tone(string key)
		{
			return tones[(int)((uint)StringComparer.OrdinalIgnoreCase.GetHashCode(key) % tones.Length)];
		}
	}

	private static int NiceMax(int value)
	{
		if (value <= 4)
		{
			return 4;
		}
		double num = Math.Pow(10.0, Math.Floor(Math.Log10(value)));
		double[] array = new double[5] { 1.0, 2.0, 2.5, 5.0, 10.0 };
		foreach (double num2 in array)
		{
			if (num2 * num >= (double)value)
			{
				int num3 = (int)Math.Round(num2 * num);
				if (num3 % 2 != 0)
				{
					return num3 + 1;
				}
				return num3;
			}
		}
		return value;
	}

	private static string LoadingCard(ScanProgress? p)
	{
		if (p == null)
		{
			return "";
		}
		int value = ((p.Total > 0) ? ((int)Math.Round(100.0 * (double)p.Done / (double)p.Total)) : 0);
		string value2 = L.F("Analizando tus replays… {0} / {1}", "Analyzing your replays… {0} / {1}", p.Done, p.Total);
		string value3 = L.T("Esta página se actualiza sola: las cifras de abajo aumentan a medida que se leen los replays.", "This page updates by itself: the numbers below grow as the replays are read.");
		return $"<div class=\"loading\"><div class=\"spin\"></div><div class=\"grow\"><b>{value2}</b><div class=\"pbar\"><i style=\"width:{value}%\"></i></div><span class=\"muted\">{value3}</span></div></div>";
	}

	private static string ProfilesLine(IReadOnlyList<Profile> profiles, string player)
	{
		List<string> list = (from p in profiles
			where p.Name != player
			select H(p.Name)).ToList();
		if (list.Count == 0)
		{
			return "";
		}
		return $"<div class=\"profiles\">{L.T("Otros perfiles en este PC: ", "Other profiles on this PC: ")}{string.Join(", ", list)} · {L.T("cambia de perfil desde los ajustes de la herramienta", "switch profile from the tool's settings")}</div>";
	}

	public static void WriteOverlay(Store store, OverlayView view)
	{
		try
		{
			WriteAtomic(store.OverlayPath, Overlay(view));
		}
		catch
		{
		}
	}

	public static string OverlayBox(OverlayView v)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (OverlayRow row in v.Rows)
		{
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(56, 3, stringBuilder2);
			handler.AppendLiteral("<div class=\"stat");
			handler.AppendFormatted(row.Treasure ? " t" : "");
			handler.AppendLiteral("\"><span class=\"cap\">");
			handler.AppendFormatted(H(row.Label));
			handler.AppendLiteral("</span><b>");
			handler.AppendFormatted(H(row.Value));
			handler.AppendLiteral("</b></div>");
			stringBuilder2.Append(ref handler);
		}
		string value = ((v.Badge != null) ? ("<span class=\"pill\">" + H(v.Badge.TrimStart(new char[2] { '●', ' ' })) + "</span>") : "");
		string value2;
		if (v.Current == null)
		{
			value2 = "none";
		}
		else
		{
			value2 = (v.CurrentLive ? "live" : "");
		}
		return $"<div class=\"box{(v.CurrentLive ? " live" : "")}\">\n<div class=\"head\"><span>{L.T("COFRES", "CHESTS")}</span>{value}</div>\n<div class=\"main\">\n  <div class=\"total\"><span class=\"big\">{H(v.Total)}</span><span class=\"cap\">total</span></div>\n  <div class=\"chip {value2}\"><span class=\"big\">{H(v.Current ?? "—")}</span><span class=\"cap\">{H(v.CurrentCaption)}</span></div>\n</div>\n<div class=\"stats\">{stringBuilder}</div>\n</div>";
	}

	public static string Overlay(OverlayView v)
	{
		return $"<!doctype html><html lang=\"{L.Code}\"><head><meta charset=\"utf-8\"><meta http-equiv=\"refresh\" content=\"10\">\n<title>{L.T("Overlay de cofres", "Chest overlay")}</title><style>{"html,body{margin:0;background:transparent;color:#f9ecf6;font:500 16px/1.25 \"Segoe UI\",system-ui,sans-serif}\n.box{position:relative;display:inline-block;min-width:230px;padding:10px 14px 10px 18px;border-radius:14px;overflow:hidden;\n  background:rgba(25,15,30,.94);border:1px solid #451c3c;box-shadow:0 6px 24px rgba(0,0,0,.35)}\n.box:before{content:\"\";position:absolute;left:0;top:0;bottom:0;width:4px;background:#ff7fb4}\n.box.live:before{background:#3ddc97}\n.head{display:flex;align-items:center;justify-content:space-between;gap:16px;font:700 12px \"Segoe UI\",sans-serif;letter-spacing:.12em;color:#7a8496}\n.pill{display:inline-flex;align-items:center;gap:6px;padding:3px 10px;border-radius:99px;background:#143a2c;color:#3ddc97;letter-spacing:.06em}\n.pill:before{content:\"\";width:8px;height:8px;border-radius:50%;background:#3ddc97;animation:pulse 1.6s ease-in-out infinite}\n@keyframes pulse{50%{opacity:.35}}\n.main{display:flex;align-items:center;justify-content:space-between;gap:16px;margin:6px 0 8px}\n.big{display:block;font-size:34px;font-weight:800;line-height:1.05;font-variant-numeric:tabular-nums}\n.total .big{color:#ff7fb4}\n.cap{display:block;font-size:11px;font-weight:700;letter-spacing:.1em;color:#7a8496;text-transform:uppercase}\n.chip{min-width:84px;padding:5px 12px;border-radius:11px;background:#1a202d;text-align:center}\n.chip.live .big,.chip.live .cap{color:#3ddc97}.chip.none .big{color:#505868}\n.stats{display:flex;justify-content:space-between;gap:14px;border-top:1px solid #222938;padding-top:7px}\n.stat .cap{font-size:10px}.stat b{display:block;font-size:16px;font-weight:700}\n.stat.t b{color:#ff80aa}.stat small{display:block;font-size:11px;font-weight:600;color:#7a8496}"}</style></head>\n<body>{OverlayBox(v)}</body></html>";
	}

	public static string OverlayLivePage(string box)
	{
		return $"<!doctype html><html lang=\"{L.Code}\"><head><meta charset=\"utf-8\">\n<title>{L.T("Overlay de cofres", "Chest overlay")}</title><style>{"html,body{margin:0;background:transparent;color:#f9ecf6;font:500 16px/1.25 \"Segoe UI\",system-ui,sans-serif}\n.box{position:relative;display:inline-block;min-width:230px;padding:10px 14px 10px 18px;border-radius:14px;overflow:hidden;\n  background:rgba(25,15,30,.94);border:1px solid #451c3c;box-shadow:0 6px 24px rgba(0,0,0,.35)}\n.box:before{content:\"\";position:absolute;left:0;top:0;bottom:0;width:4px;background:#ff7fb4}\n.box.live:before{background:#3ddc97}\n.head{display:flex;align-items:center;justify-content:space-between;gap:16px;font:700 12px \"Segoe UI\",sans-serif;letter-spacing:.12em;color:#7a8496}\n.pill{display:inline-flex;align-items:center;gap:6px;padding:3px 10px;border-radius:99px;background:#143a2c;color:#3ddc97;letter-spacing:.06em}\n.pill:before{content:\"\";width:8px;height:8px;border-radius:50%;background:#3ddc97;animation:pulse 1.6s ease-in-out infinite}\n@keyframes pulse{50%{opacity:.35}}\n.main{display:flex;align-items:center;justify-content:space-between;gap:16px;margin:6px 0 8px}\n.big{display:block;font-size:34px;font-weight:800;line-height:1.05;font-variant-numeric:tabular-nums}\n.total .big{color:#ff7fb4}\n.cap{display:block;font-size:11px;font-weight:700;letter-spacing:.1em;color:#7a8496;text-transform:uppercase}\n.chip{min-width:84px;padding:5px 12px;border-radius:11px;background:#1a202d;text-align:center}\n.chip.live .big,.chip.live .cap{color:#3ddc97}.chip.none .big{color:#505868}\n.stats{display:flex;justify-content:space-between;gap:14px;border-top:1px solid #222938;padding-top:7px}\n.stat .cap{font-size:10px}.stat b{display:block;font-size:16px;font-weight:700}\n.stat.t b{color:#ff80aa}.stat small{display:block;font-size:11px;font-weight:600;color:#7a8496}"}</style></head>\n<body><div id=\"o\">{box}</div>\n<script>\nlet last = document.getElementById(\"o\").innerHTML;\nsetInterval(async () => {{\n  try {{\n    const r = await fetch(\"/view\", {{ cache: \"no-store\" }});\n    if (!r.ok) return;\n    const html = await r.text();\n    if (html !== last) {{ document.getElementById(\"o\").innerHTML = html; last = html; }}\n  }} catch (e) {{ /* tool closed: keep the last figures */ }}\n}}, 2000);\n</script></body></html>";
	}

	public static string Csv(TrackerData data)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine(L.T("fecha;modo;jugador;puesto;jugadores;cofres;cajas_municion;tesoros;archivo", "date;mode;player;placement;players;chests;ammo_boxes;treasures;file"));
		foreach (MatchRecord item in data.Matches.Where(Profiles.IsSupportedVersion).OrderBy(When))
		{
			string value = string.Join(" + ", item.TreasureFinds.Select((TreasureFind f) => f.DisplayName));
			InlineArray9<object> buffer = default;
			buffer[0] = When(item).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
			buffer[1] = Cell(Pretty.Playlist(item.Playlist));
			buffer[2] = Cell(item.PlayerName);
			buffer[3] = item.Placement;
			buffer[4] = item.TotalPlayers;
			buffer[5] = item.ChestsOpened;
			buffer[6] = item.AmmoBoxesOpened;
			buffer[7] = Cell(value);
			buffer[8] = Cell(item.ReplayFile);
			stringBuilder.AppendLine(string.Join(";", (ReadOnlySpan<object?>)buffer));
		}
		return stringBuilder.ToString();
	}

	private static string Cell(string? value)
	{
		string text = (value ?? "").Replace(';', ',').Replace('\r', ' ').Replace('\n', ' ')
			.Replace('"', '\'');
		string text2 = text.TrimStart();
		bool flag = text2.Length > 0;
		if (flag)
		{
			bool flag2;
			switch (text2[0])
			{
			case '\t':
			case '+':
			case '-':
			case '=':
			case '@':
			case '|':
				flag2 = true;
				break;
			default:
				flag2 = false;
				break;
			}
			flag = flag2;
		}
		if (!flag)
		{
			return text;
		}
		return "'" + text;
	}
}
