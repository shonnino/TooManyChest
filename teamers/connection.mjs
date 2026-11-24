const handoffCookie = 'teamers_connection';
const sessionCookie = 'teamers_session';
const handoffLifetime = 600;
const sessionLifetime = 30 * 86400;
const inviteLifetime = 7 * 86400 * 1000;
const codePattern = /^[a-f0-9]{32}$/;
const sessionPatternBrowser = /^[a-f0-9]{64}$/;

function readCookie(request, name, pattern) {
  const value = request.headers.get('cookie')?.split(';').map(value => value.trim())
    .find(value => value.startsWith(name + '='))?.slice(name.length + 1);
  return value && pattern.test(value) ? value : null;
}

function withCookie(result, name, value, seconds, url) {
  // Loopback uses HTTP only for the local app integration fixture.
  const secure = url.protocol === 'https:' ? '; Secure' : '';
  result.headers.append('set-cookie', `${name}=${value}; Path=/; Max-Age=${seconds}; HttpOnly; SameSite=Strict${secure}`);
  return result;
}

async function browserProfile(request, db) {
  const token = readCookie(request, sessionCookie, sessionPatternBrowser);
  if (!token) return null;
  return one(db, `SELECT p.id, p.epic_player_key FROM teamers_browser_sessions s
    JOIN profiles p ON p.id=s.profile_id WHERE s.token_hash=? AND s.expires_at>?`, await sha(token), Date.now());
}

async function startConnection(request, db, url) {
  const owner = randomHex(32), code = randomHex(16), now = Date.now();
  const previous = readCookie(request, handoffCookie, sessionPatternBrowser);
  const writes = [
    bind(db, 'DELETE FROM teamers_connections WHERE expires_at<=?', now),
    bind(db, 'DELETE FROM teamers_browser_sessions WHERE expires_at<=?', now),
  ];
  if (previous) writes.push(bind(db, 'DELETE FROM teamers_connections WHERE browser_hash=?', await sha(previous)));
  writes.push(bind(db, 'INSERT INTO teamers_connections (browser_hash, code_hash, expires_at) VALUES (?,?,?)',
    await sha(owner), await sha(code), now + handoffLifetime * 1000));
  await db.batch(writes);
  return withCookie(json({ code, expiresAt: now + handoffLifetime * 1000 }), handoffCookie, owner, handoffLifetime, url);
}

async function finishConnection(request, db, url) {
  const owner = readCookie(request, handoffCookie, sessionPatternBrowser);
  if (!owner) return error('Collegamento scaduto. Genera un nuovo codice.', 410);
  const ownerHash = await sha(owner), now = Date.now();
  const pending = await one(db, 'SELECT profile_id FROM teamers_connections WHERE browser_hash=? AND expires_at>?', ownerHash, now);
  if (!pending) return error('Collegamento scaduto. Genera un nuovo codice.', 410);
  if (!pending.profile_id) return json({ waiting: true });
  const token = randomHex(32);
  const [inserted] = await db.batch([
    bind(db, `INSERT INTO teamers_browser_sessions (token_hash, profile_id, expires_at)
      SELECT ?, profile_id, ? FROM teamers_connections WHERE browser_hash=? AND expires_at>? AND profile_id IS NOT NULL`,
    await sha(token), now + sessionLifetime * 1000, ownerHash, now),
    bind(db, 'DELETE FROM teamers_connections WHERE browser_hash=? AND profile_id IS NOT NULL', ownerHash),
  ]);
  if (!inserted.meta.changes) return error('Collegamento già completato. Ricarica la pagina.', 409);
  const result = await sessionView(db, { id: pending.profile_id });
  withCookie(result, handoffCookie, '', 0, url);
  return withCookie(result, sessionCookie, token, sessionLifetime, url);
}

function validEpicName(input) {
  const value = typeof input === 'string' ? input.trim() : '';
  return value && value.length <= 80 && !/[\u0000-\u001f\u007f]/u.test(value) ? value : null;
}

async function approveConnection(request, db) {
  const input = await readSiteJson(request);
  const code = typeof input?.code === 'string' ? input.code.trim().toLowerCase() : '';
  if (!codePattern.test(code)) return error('Il codice di collegamento non è valido.', 400);
  const name = validEpicName(input.epicUsername);
  const playerKey = input.playerKey;
  if (!name || typeof playerKey !== 'string' || !playerKeyPattern.test(playerKey) || !validCount(input.initialChests, 50000000)) {
    return error('Aggiorna ChestCounter e seleziona un profilo Epic con un replay completato.', 400);
  }
  const parts = packParts(input.parts, 50000000);
  if (input.parts != null && (!parts || !validCounting(input.appOptions))) return error('Dettagli del conteggio non validi.', 400);
  const activity = packPersonalActivity(input.activity);
  if (input.activity != null && !activity) return error('Statistiche personali non valide.', 400);
  const codeHash = await sha(code), now = Date.now();
  if (!await one(db, 'SELECT 1 FROM teamers_connections WHERE code_hash=? AND expires_at>? AND profile_id IS NULL', codeHash, now)) {
    return error('Codice scaduto o già usato. Generane un altro sul sito.', 401);
  }
  const provided = request.headers.get('authorization')?.match(/^Bearer ([0-9a-f]{64})$/)?.[1];
  const device = provided && await one(db, `SELECT p.id, p.epic_player_key, m.player_key AS member_key,
    s.player_key AS snapshot_key FROM teamers_devices d JOIN profiles p ON d.profile_id=p.id
    LEFT JOIN teamers_members m ON m.profile_id=p.id LEFT JOIN teamers_snapshots s ON s.profile_id=p.id WHERE d.token_hash=?`, await sha(provided));
  if (provided && !device) return error('Il collegamento dell’app è scaduto.', 401);
  const previousKey = device && (device.epic_player_key ?? device.member_key ?? device.snapshot_key);
  if (previousKey && previousKey !== playerKey) return error('Seleziona il profilo Epic già collegato a questa app.', 409);
  const claim = await one(db, `SELECT id FROM profiles WHERE epic_player_key=?
    UNION SELECT profile_id FROM teamers_members WHERE player_key=?`, playerKey, playerKey);
  if (claim && claim.id !== device?.id) return error('Apri ChestCounter sul PC già associato a questo profilo Epic.', 409);

  const profileId = device?.id ?? id(), approval = randomHex(16), token = device ? provided : randomHex(32);
  const tokenHash = await sha(token);
  const pending = 'EXISTS (SELECT 1 FROM teamers_connections WHERE code_hash=? AND expires_at>? AND profile_id IS NULL)';
  const profile = device
    ? bind(db, `UPDATE OR IGNORE profiles SET epic_player_key=?, epic_username=? WHERE id=? AND ${pending}`, playerKey, name, profileId, codeHash, now)
    : bind(db, `INSERT OR IGNORE INTO profiles (id, site_identity, username, epic_player_key, epic_username)
         SELECT ?, ?, ?, ?, ? WHERE ${pending}`, profileId, 'app:' + profileId, profileId, playerKey, name, codeHash, now);
  const [changed, approved] = await db.batch([
    profile,
    bind(db, `UPDATE teamers_connections SET profile_id=?, approved_by=? WHERE code_hash=? AND expires_at>? AND profile_id IS NULL
      AND EXISTS (SELECT 1 FROM profiles WHERE id=? AND epic_player_key=?)`, profileId, approval, codeHash, now, profileId, playerKey),
    bind(db, `INSERT OR IGNORE INTO teamers_devices (token_hash, profile_id)
      SELECT ?, profile_id FROM teamers_connections WHERE code_hash=? AND approved_by=?`, tokenHash, codeHash, approval),
    bind(db, `INSERT INTO teamers_snapshots (profile_id, player_key, initial_chests, synchronized_at, parts, app_options, activity)
      SELECT profile_id, ?, ?, ?, ?, ?, ? FROM teamers_connections WHERE code_hash=? AND approved_by=?
      ON CONFLICT(profile_id) DO UPDATE SET player_key=excluded.player_key, initial_chests=excluded.initial_chests,
      synchronized_at=excluded.synchronized_at, parts=excluded.parts, app_options=excluded.app_options,
      activity=COALESCE(excluded.activity, teamers_snapshots.activity)
      WHERE excluded.parts IS NOT NULL OR teamers_snapshots.parts IS NULL`,
    playerKey, input.initialChests, now, parts, parts ? input.appOptions : null, activity, codeHash, approval),
  ]);
  if (!changed.meta.changes || !approved.meta.changes) return error('Il codice o il profilo è già stato usato.', 409);
  return json({ token });
}

async function makeJoinLink(db, profile, url) {
  const team = await membership(db, profile.id);
  if (team?.owner_id !== profile.id) return error('Solo chi ha creato la squadra può generare il link.', 403);
  const count = await one(db, 'SELECT COUNT(*) AS count FROM teamers_members WHERE team_id=?', team.id);
  if (count.count >= 4) return error('La squadra è al completo.', 409);
  const code = randomHex(16);
  const expiresAt = Date.now() + inviteLifetime;
  await bind(db, `INSERT INTO teamers_join_links(team_id, code_hash, expires_at) VALUES (?,?,?)
    ON CONFLICT(team_id) DO UPDATE SET code_hash=excluded.code_hash, expires_at=excluded.expires_at`,
  team.id, await sha(code), expiresAt).run();
  return json({ link: new URL('/teamers/join/' + code, url.origin).href, expiresAt });
}

async function joinFromLink(db, profile, code) {
  if (typeof code !== 'string' || !codePattern.test(code)) return error('Il link d’invito non è valido.', 400);
  const hash = await sha(code), now = Date.now();
  const target = await one(db, 'SELECT team_id AS id FROM teamers_join_links WHERE code_hash=? AND expires_at>?', hash, now);
  if (!target) return error('Questo link è scaduto o è stato sostituito. Chiedi un nuovo invito.', 410);
  const current = await membership(db, profile.id);
  if (current?.id === target.id) return sessionView(db, profile);
  if (current) return error('Fai già parte di una squadra.', 409);
  const snapshot = await snapshotFor(db, profile.id);
  if (!snapshot || snapshot.playerKey !== profile.epic_player_key) return error('Collega ChestCounter con il profilo Epic selezionato.', 409);
  const [joined] = await db.batch([
    bind(db, `INSERT OR IGNORE INTO teamers_members (profile_id, team_id, player_key, joined_at, initial_chests, initial_parts)
      SELECT ?, team_id, ?, ?, ?, ? FROM teamers_join_links l WHERE code_hash=? AND expires_at>?
      AND (SELECT COUNT(*) FROM teamers_members WHERE team_id=l.team_id)<4`,
    profile.id, snapshot.playerKey, now, snapshot.initialChests, snapshot.parts ? packEntryParts(JSON.parse(snapshot.parts)) : null, hash, now),
    bind(db, `UPDATE teamers_invitations SET status='accepted' WHERE team_id=? AND recipient_id=? AND status='pending'
      AND EXISTS (SELECT 1 FROM teamers_members WHERE team_id=? AND profile_id=?)`, target.id, profile.id, target.id, profile.id),
  ]);
  if (!joined.meta.changes) {
    if ((await membership(db, profile.id))?.id === target.id) return sessionView(db, profile);
    return error('La squadra è al completo o il link non è più valido.', 409);
  }
  return sessionView(db, profile);
}
