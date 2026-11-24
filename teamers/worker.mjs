const basic = {
  'x-content-type-options': 'nosniff',
  'referrer-policy': 'no-referrer',
  'content-security-policy': "default-src 'none'; script-src 'self'; style-src 'self'; connect-src 'self'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'",
};
const makeHex = bytes => Array.from(bytes, byte => byte.toString(16).padStart(2, '0')).join('');
const randomHex = size => makeHex(crypto.getRandomValues(new Uint8Array(size)));
const sha = async value => makeHex(new Uint8Array(await crypto.subtle.digest('SHA-256', new TextEncoder().encode(value))));
const bind = (db, sql, ...values) => db.prepare(sql).bind(...values);
const snapshotFor = (db, profileId) => one(db, `SELECT player_key AS playerKey, initial_chests AS initialChests, synchronized_at AS synchronizedAt,
    parts, app_options AS appOptions, count_options AS countOptions, team_starting AS teamStarting, activity
  FROM teamers_snapshots WHERE profile_id=?`, profileId);

async function readSiteJson(request) {
  if (request.headers.get('content-type')?.split(';')[0] !== 'application/json' || !request.body) return null;
  const reader = request.body.getReader();
  const decoder = new TextDecoder();
  let size = 0, data = '';
  try {
    for (;;) {
      const { done, value } = await reader.read();
      if (done) return JSON.parse(data + decoder.decode());
      size += value.byteLength;
      if (size > 32768) { await reader.cancel(); return null; }
      data += decoder.decode(value, { stream: true });
    }
  } catch {
    return null;
  }
}

async function sessionView(db, profile) {
  if (!profile) return json({ state: null });
  const state = await dashboard(db, profile.id).then(response => response.json());
  const snapshot = await snapshotFor(db, profile.id);
  const device = await one(db, 'SELECT 1 FROM teamers_devices WHERE profile_id=?', profile.id);
  return json({ state, linked: !!device, synchronizedAt: snapshot?.synchronizedAt ?? null });
}

async function webTeamers(request, env, url) {
  const action = url.pathname.slice('/api/web-teamers/'.length);
  const db = env.DB;
  if (request.method === 'GET' && action === 'session') return sessionView(db, await browserProfile(request, db));
  if (request.method !== 'POST' || request.headers.get('origin') !== url.origin) {
    return error('Apri Teamers e riprova da questa pagina.', 403);
  }
  if (action === 'connect/start') return startConnection(request, db, url);
  if (action === 'connect/finish') return finishConnection(request, db, url);
  if (action === 'disconnect') {
    const token = readCookie(request, sessionCookie, sessionPatternBrowser);
    if (token) await bind(db, 'DELETE FROM teamers_browser_sessions WHERE token_hash=?', await sha(token)).run();
    return withCookie(json({ state: null }), sessionCookie, '', 0, url);
  }
  const profile = await browserProfile(request, db);
  if (!profile) return error('Collega ChestCounter per continuare.', 401);
  if (action === 'invite-link') return makeJoinLink(db, profile, url);
  const input = await readSiteJson(request);
  if (!input || typeof input !== 'object' || Array.isArray(input)) return error('Richiesta non valida.', 400);
  if (action === 'counting') {
    const updated = await saveTeamCounting(db, profile.id, input);
    return updated.ok ? sessionView(db, profile) : updated;
  }
  if (action === 'join') return joinFromLink(db, profile, input.code);
  if (action !== 'teams' && action !== 'respond') return error('Pagina non trovata.', 404);
  let data = input;
  if (action === 'teams' || (action === 'respond' && input.accept === true)) {
    const snapshot = await snapshotFor(db, profile.id);
    if (!snapshot) return error('Collega ChestCounter e sincronizza un replay completato prima di entrare in una squadra.', 409);
    data = { ...input, playerKey: snapshot.playerKey, initialChests: snapshot.initialChests,
      parts: snapshot.parts ? JSON.parse(snapshot.parts) : null };
  }
  const endpoint = new URL('/api/teamers/' + action, url);
  const result = await handleTeamers(new Request(endpoint, { method: 'POST', body: JSON.stringify(data) }), env, endpoint, profile.id);
  return result.ok ? sessionView(db, profile) : result;
}

async function desktopTeamers(request, env, url) {
  const db = env.DB;
  const action = url.pathname.slice('/api/teamers'.length);
  if (request.method === 'POST' && action === '/pair') return approveConnection(request, db);
  const bearer = request.headers.get('authorization')?.match(/^Bearer ([0-9a-f]{64})$/);
  const device = bearer && await one(db, `SELECT d.profile_id, p.epic_player_key FROM teamers_devices d
    JOIN profiles p ON p.id=d.profile_id WHERE d.token_hash=?`, await sha(bearer[1]));
  if (!device) return error('Collega ChestCounter dal sito Teamers.', 401);
  if (request.method === 'GET') return handleTeamers(request, env, url, device.profile_id);
  if (request.method !== 'POST') return error('Richiesta non valida.', 405);
  const input = await readSiteJson(request);
  if (!input || typeof input !== 'object' || Array.isArray(input)) return error('Richiesta non valida.', 400);
  if (action === '/invite-link') {
    if (!device.epic_player_key) return error('Collega l’app a Teamers con il tuo profilo Epic.', 409);
    return makeJoinLink(db, { id: device.profile_id }, url);
  }
  if (action === '/snapshot') {
    if (typeof input.playerKey !== 'string' || !playerKeyPattern.test(input.playerKey) || !validCount(input.initialChests, 50000000)) {
      return error('Serve un profilo Fortnite con un replay completato.', 400);
    }
    if (device.epic_player_key && device.epic_player_key !== input.playerKey) {
      return error('Seleziona lo stesso profilo Epic che hai collegato a Teamers.', 409);
    }
    const parts = packParts(input.parts, 50000000);
    if (input.parts != null && (!parts || !validCounting(input.appOptions))) return error('Dettagli del conteggio non validi.', 400);
    const activity = packPersonalActivity(input.activity);
    if (input.activity != null && !activity) return error('Statistiche personali non valide.', 400);
    const commands = [bind(db, `INSERT INTO teamers_snapshots (profile_id, player_key, initial_chests, synchronized_at, parts, app_options, activity)
      VALUES (?,?,?,?,?,?,?) ON CONFLICT(profile_id) DO UPDATE SET player_key=excluded.player_key,
      initial_chests=excluded.initial_chests, synchronized_at=excluded.synchronized_at,
      parts=excluded.parts, app_options=excluded.app_options, activity=COALESCE(excluded.activity, teamers_snapshots.activity)
      WHERE excluded.parts IS NOT NULL OR (teamers_snapshots.parts IS NULL
        AND NOT EXISTS (SELECT 1 FROM teamers_members WHERE profile_id=excluded.profile_id))`,
    device.profile_id, input.playerKey, input.initialChests, Date.now(), parts, parts ? input.appOptions : null, activity)];
    const initialParts = packParts(input.entryParts, 50000000);
    if (initialParts && Number.isSafeInteger(input.joinedAt)) {
      commands.push(bind(db, `UPDATE teamers_members SET initial_parts=? WHERE profile_id=? AND player_key=? AND joined_at=? AND initial_parts IS NULL`,
        initialParts, device.profile_id, input.playerKey, input.joinedAt));
    }
    await db.batch(commands);
    return parts ? dashboard(db, device.profile_id) : json({ synchronized: true });
  }
  if (action === '/disconnect') {
    await bind(db, 'DELETE FROM teamers_devices WHERE token_hash=?', await sha(bearer[1])).run();
    return json({ disconnected: true });
  }
  return handleTeamers(new Request(url, { method: 'POST', body: JSON.stringify(input) }), env, url, device.profile_id);
}

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    try {
      if (request.method === 'GET') {
        if (url.pathname === '/' || url.pathname === '/teamers/') return Response.redirect(new URL('/teamers', url), 302);
        const file = {
          '/teamers': ['html', 'text/html; charset=utf-8'],
          '/teamers.css': ['css', 'text/css; charset=utf-8'],
          '/teamers.js': ['client', 'text/javascript; charset=utf-8'],
          '/teamers-charts.js': ['charts', 'text/javascript; charset=utf-8'],
        }[url.pathname];
        if (file || /^\/teamers\/join\/[a-f0-9]{32}\/?$/.test(url.pathname)) {
          const [key, type] = file ?? ['html', 'text/html; charset=utf-8'];
          return new Response(assets[key], { headers: { ...basic, 'content-type': type } });
        }
        if (url.pathname === '/health') {
          if (await one(env.DB, 'SELECT COUNT(*) AS count FROM profiles')) return json({ service: 'Teamers', ready: true });
        }
      }
      if (url.pathname.startsWith('/api/web-teamers/')) return await webTeamers(request, env, url);
      if (url.pathname === '/api/teamers' || url.pathname.startsWith('/api/teamers/')) return await desktopTeamers(request, env, url);
      return error('Pagina non trovata.', 404);
    } catch (failure) {
      console.error('Teamers:', failure.message);
      return error('Teamers non è disponibile. Riprova tra poco.', 500);
    }
  },
};
