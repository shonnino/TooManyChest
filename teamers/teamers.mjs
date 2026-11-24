const json = (value, status = 200) => Response.json(value, {
  status, headers: { 'cache-control': 'no-store' },
});
const error = (message, status) => json({ error: message }, status);
const usernamePattern = /^[A-Za-z0-9_. -]{3,20}$/;
const playerKeyPattern = /^[0-9a-f]{64}$/;
const sessionPattern = /^[0-9a-f]{32}$/;
const id = () => crypto.randomUUID();
const validCount = (value, limit) => Number.isSafeInteger(value) && value >= 0 && value <= limit;
const one = (db, sql, ...values) => db.prepare(sql).bind(...values).first();
const rows = async (db, sql, ...values) => (await db.prepare(sql).bind(...values).all()).results;
const countFields = ['ammoBoxes', 'foodBoxes', 'lootCannons', 'startingChests', 'manualChests'];
const defaultCounting = 24;
const validCounting = value => validCount(value, (1 << countFields.length) - 1);

function packParts(parts, max) {
  if (!parts || !['chests', ...countFields].every(field => validCount(parts[field], max))) return null;
  return JSON.stringify(Object.fromEntries(['chests', ...countFields].map(field => [field, parts[field]])));
}

function countedTotal(parts, options) {
  const breakdown = JSON.parse(parts);
  return countFields.reduce((sum, field, index) => sum + ((options & (1 << index)) ? breakdown[field] : 0), breakdown.chests);
}

function countedSql(parts, legacy, options) {
  const extras = countFields.map((field, index) =>
    `CASE WHEN (${options} & ${1 << index}) != 0 THEN json_extract(${parts}, '$.${field}') ELSE 0 END`);
  return `CASE WHEN ${parts} IS NULL THEN ${legacy} ELSE json_extract(${parts}, '$.chests') + ${extras.join(' + ')} END`;
}

function packEntryParts(parts) {
  return packParts(parts && { ...parts, startingChests: 0 }, 50000000);
}

async function saveTeamCounting(db, profileId, input) {
  if (!validCounting(input.options) || !validCount(input.starting, 50000000))
    return error('Inserisci opzioni valide e un saldo iniziale da 0 a 50.000.000.', 400);
  const updated = await db.prepare(`UPDATE teamers_snapshots SET count_options=?, team_starting=?
    WHERE profile_id=? AND parts IS NOT NULL AND NOT EXISTS (
      SELECT 1 FROM teamers_members WHERE profile_id=? AND initial_parts IS NULL
    )`).bind(input.options | 8, input.starting, profileId, profileId).run();
  if (!updated.meta.changes) return error('Apri la versione aggiornata di ChestCounter e attendi la sincronizzazione.', 409);
  return dashboard(db, profileId);
}

const membership = (db, profileId) => one(db, `SELECT t.id, t.name, t.owner_id, m.player_key
  FROM teamers_members m JOIN teamers_teams t ON t.id=m.team_id WHERE m.profile_id=?`, profileId);

async function dashboard(db, me) {
  const profile = await one(db, 'SELECT COALESCE(epic_username, username) AS username FROM profiles WHERE id=?', me);
  const team = await membership(db, me);
  const invitations = await rows(db, `SELECT i.id, t.name AS teamName, COALESCE(p.epic_username, p.username) AS fromUsername
    FROM teamers_invitations i JOIN teamers_teams t ON t.id=i.team_id JOIN profiles p ON p.id=t.owner_id
    WHERE i.recipient_id=? AND i.status='pending' ORDER BY i.created_at DESC LIMIT 20`, me);
  const snapshot = await snapshotFor(db, me);
  const personal = snapshot ? {
    total: snapshot.parts ? countedTotal(snapshot.parts, snapshot.appOptions ?? defaultCounting) : snapshot.initialChests,
    ready: !!snapshot.parts,
    activity: snapshot.activity ? JSON.parse(snapshot.activity) : null,
  } : null;
  const counting = snapshot ? { ready: !!snapshot.parts, options: snapshot.countOptions ?? defaultCounting,
    starting: snapshot.teamStarting ?? 0 } : null;
  if (!team) return json({ username: profile?.username, personal, counting, team: null, invitations });

  const options = `COALESCE(s.count_options, ${defaultCounting})`;
  const starting = `COALESCE(s.team_starting, CASE WHEN (${options} & 8) != 0
    THEN json_extract(m.initial_parts,'$.startingChests') END, 0)`;
  const [stats, activity] = await Promise.all([rows(db, `WITH eligible AS (${eligibleMatches}), contributions AS (
      SELECT entry.profile_id, SUM(${countedSql('entry.parts', 'entry.chests', options)}) AS chests
      FROM eligible entry LEFT JOIN teamers_snapshots s ON s.profile_id=entry.profile_id GROUP BY entry.profile_id
    )
    SELECT COALESCE(p.epic_username, p.username) AS username, m.player_key AS playerKey, m.joined_at AS joinedAt,
           ${countedSql('m.initial_parts', 'm.initial_chests', `(${options} & ~8)`)} + ${starting} AS initialChests,
           ${starting} AS starting, m.initial_parts IS NOT NULL AS initialReady, COALESCE(c.chests,0) AS sharedChests
    FROM teamers_members m JOIN profiles p ON p.id=m.profile_id LEFT JOIN contributions c ON c.profile_id=m.profile_id
    LEFT JOIN teamers_snapshots s ON s.profile_id=m.profile_id
    WHERE m.team_id=? ORDER BY m.joined_at, m.profile_id`, team.id, team.id), sharedActivity(db, team.id, options)]);
  if (counting) {
    const member = stats.find(row => row.playerKey === team.player_key);
    counting.ready = counting.ready && !!member?.initialReady;
    counting.starting = member?.starting ?? counting.starting;
  }
  const outgoing = team.owner_id === me ? await rows(db, `SELECT COALESCE(p.epic_username, p.username) AS username, i.status
    FROM teamers_invitations i JOIN profiles p ON p.id=i.recipient_id WHERE i.team_id=?
    ORDER BY i.created_at DESC LIMIT 20`, team.id) : [];

  return json({ username: profile?.username, personal, counting, invitations, team: {
    id: team.id, name: team.name, canInvite: team.owner_id === me,
    playerKey: team.player_key, members: stats.map(({ starting, initialReady, ...member }) => ({ ...member, initialReady: !!initialReady })), outgoing,
    initialChests: stats.reduce((sum, member) => sum + member.initialChests, 0),
    sharedChests: stats.reduce((sum, member) => sum + member.sharedChests, 0),
    sharedMatches: activity.matches, activity,
  } });
}

export async function handleTeamers(req, env, url, me) {
  const db = env.DB;
  const route = req.method + ' ' + url.pathname;
  if (route === 'GET /api/teamers') return dashboard(db, me);
  if (req.method !== 'POST') return error('Teamers route not found.', 404);
  const input = await req.json().catch(() => null);
  if (!input || typeof input !== 'object') return error('Invalid Teamers request.', 400);
  const now = Date.now();
  if (route === 'POST /api/teamers/counting') return saveTeamCounting(db, me, input);

  if (route === 'POST /api/teamers/teams' || route === 'POST /api/teamers/respond') {
    const creating = route.endsWith('/teams');
    if (!creating && (typeof input.invitationId !== 'string' || typeof input.accept !== 'boolean'))
      return error('Choose an invitation and a response.', 400);
    if (!creating && !input.accept) {
      const response = await db.prepare("UPDATE teamers_invitations SET status='declined' WHERE id=? AND recipient_id=? AND status='pending'")
        .bind(input.invitationId, me).run();
      return response.meta.changes ? dashboard(db, me) : error('Invitation is no longer pending.', 409);
    }
    if (typeof input.playerKey !== 'string' || !playerKeyPattern.test(input.playerKey) || !validCount(input.initialChests, 50000000))
      return error('A replay profile and its personal total are required.', 400);
    const initialParts = packEntryParts(input.parts);
    const epicProfile = await one(db, 'SELECT epic_player_key FROM profiles WHERE id=?', me);
    if (epicProfile?.epic_player_key && epicProfile.epic_player_key !== input.playerKey)
      return error('Seleziona lo stesso profilo Epic che hai collegato a Teamers.', 409);
    if (!(await one(db, 'SELECT username FROM profiles WHERE id=?', me))?.username) return error('Set your ChestCounter username first.', 409);
    if (await membership(db, me)) return error('You already belong to a team.', 409);
    if (await one(db, 'SELECT 1 FROM teamers_members WHERE player_key=?', input.playerKey)) return error('This replay profile already belongs to a team.', 409);
    if (creating) {
      const name = typeof input.name === 'string' ? input.name.trim() : '';
      if (!name || name.length > 40) return error('Enter a team name of up to 40 characters.', 400);
      const teamId = id();
      try {
        await db.batch([
          db.prepare('INSERT INTO teamers_teams (id, owner_id, name) VALUES (?,?,?)').bind(teamId, me, name),
          db.prepare('INSERT INTO teamers_members (profile_id, team_id, player_key, joined_at, initial_chests, initial_parts) VALUES (?,?,?,?,?,?)')
            .bind(me, teamId, input.playerKey, now, input.initialChests, initialParts),
        ]);
      } catch (failure) {
        if (String(failure).includes('UNIQUE')) return error('Account or replay profile already on a team.', 409);
        throw failure;
      }
    } else {
      const invitation = await one(db, "SELECT team_id FROM teamers_invitations WHERE id=? AND recipient_id=? AND status='pending'", input.invitationId, me);
      if (!invitation) return error('Invitation is no longer pending.', 409);
      const result = await db.batch([
        db.prepare(`INSERT OR IGNORE INTO teamers_members (profile_id, team_id, player_key, joined_at, initial_chests, initial_parts)
          SELECT recipient_id, team_id, ?, ?, ?, ? FROM teamers_invitations WHERE id=? AND recipient_id=? AND status='pending'
            AND (SELECT COUNT(*) FROM teamers_members WHERE team_id=?) < 4`)
          .bind(input.playerKey, now, input.initialChests, initialParts, input.invitationId, me, invitation.team_id),
        db.prepare(`UPDATE teamers_invitations SET status='accepted' WHERE id=? AND recipient_id=? AND status='pending'
          AND EXISTS (SELECT 1 FROM teamers_members WHERE profile_id=? AND team_id=? AND player_key=?)`)
          .bind(input.invitationId, me, me, invitation.team_id, input.playerKey),
      ]);
      if (!result[0].meta.changes) return error('Team full or this account already belongs to a team.', 409);
    }
    return dashboard(db, me);
  }

  const team = await membership(db, me);
  if (!team) return error('Join a team first.', 403);
  if (route === 'POST /api/teamers/invite') {
    if (team.owner_id !== me) return error('Only the team owner can invite players.', 403);
    const username = typeof input.username === 'string' ? input.username.trim() : '';
    if (!usernamePattern.test(username)) return error('Enter a valid ChestCounter username.', 400);
    const candidates = await rows(db, 'SELECT id FROM profiles WHERE username = ? COLLATE NOCASE LIMIT 2', username);
    if (candidates.length !== 1) return error('Username not found or ambiguous.', 404);
    const recipient = candidates[0].id;
    if (recipient === me || await membership(db, recipient)) return error('This player already belongs to a team.', 409);
    const pending = await one(db, "SELECT COUNT(*) AS count FROM teamers_invitations WHERE team_id=? AND status='pending'", team.id);
    if (pending.count >= 12) return error('Too many pending invitations.', 409);
    const result = await db.prepare(`INSERT OR IGNORE INTO teamers_invitations (id, team_id, recipient_id, status, created_at)
      SELECT ?, ?, ?, 'pending', ? WHERE (SELECT COUNT(*) FROM teamers_members WHERE team_id=?) < 4
        AND (SELECT COUNT(*) FROM teamers_invitations WHERE team_id=? AND status='pending') < 12
        AND NOT EXISTS (SELECT 1 FROM teamers_members WHERE profile_id=?)`)
      .bind(id(), team.id, recipient, now, team.id, team.id, recipient).run();
    if (!result.meta.changes) return error('Invitation already recorded or team full.', 409);
    return dashboard(db, me);
  }

  if (route === 'POST /api/teamers/matches') {
    if (input.playerKey !== team.player_key || !Array.isArray(input.matches) || input.matches.length > 20)
      return error('Invalid Teamers match batch.', 400);
    const members = await rows(db, 'SELECT profile_id, player_key, joined_at FROM teamers_members WHERE team_id=? ORDER BY profile_id', team.id);
    const submitted = new Map();
    for (const match of input.matches) {
      if (!match || typeof match.sessionId !== 'string' || !sessionPattern.test(match.sessionId) ||
          !Number.isSafeInteger(match.startedAt) || match.startedAt > now || !validCount(match.chests, 4000) ||
          !Array.isArray(match.teammates) || match.teammates.length > 3) continue;
      const playing = members.filter(member => member.joined_at <= match.startedAt &&
        (member.profile_id === me || match.teammates.includes(member.player_key)));
      if (playing.length < 2 || !playing.some(member => member.profile_id === me)) continue;
      const parts = packParts(match.parts, 4000);
      const activity = parts ? packMatchActivity(match.activity, match.parts.chests) : null;
      const roster = JSON.stringify(playing.map(member => member.profile_id));
      submitted.set(match.sessionId, db.prepare(`INSERT INTO teamers_matches
        (team_id, session_id, profile_id, started_at, roster, member_count, chests, parts, activity) VALUES (?,?,?,?,?,?,?,?,?)
        ON CONFLICT(team_id,session_id,profile_id) DO UPDATE SET started_at=excluded.started_at, roster=excluded.roster,
          member_count=excluded.member_count, chests=excluded.chests, parts=excluded.parts,
          activity=COALESCE(excluded.activity, teamers_matches.activity)
        WHERE excluded.parts IS NOT NULL AND (teamers_matches.parts IS NULL OR teamers_matches.parts<>excluded.parts
          OR teamers_matches.roster<>excluded.roster OR teamers_matches.started_at<>excluded.started_at
          OR (excluded.activity IS NOT NULL AND COALESCE(teamers_matches.activity,'')<>excluded.activity))`)
        .bind(team.id, match.sessionId, me, match.startedAt, roster, playing.length, match.chests, parts, activity));
    }
    if (submitted.size) await db.batch([...submitted.values()]);
    return json({ recorded: [...submitted.keys()] });
  }
  return error('Teamers route not found.', 404);
}
