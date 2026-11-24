const eligibleMatches = `SELECT entry.* FROM teamers_matches entry WHERE entry.team_id=? AND EXISTS (
  SELECT 1 FROM teamers_matches mate WHERE mate.team_id=entry.team_id AND mate.session_id=entry.session_id
    AND mate.profile_id<>entry.profile_id AND ABS(mate.started_at-entry.started_at)<=120000
    AND EXISTS (SELECT 1 FROM json_each(entry.roster) WHERE value=mate.profile_id)
    AND EXISTS (SELECT 1 FROM json_each(mate.roster) WHERE value=entry.profile_id)
)`;

function packMatchActivity(activity, chests) {
  if (!activity || !validCount(activity.broken, chests) || !validCount(activity.treasures, 4000) ||
      (activity.durationSeconds != null && !validCount(activity.durationSeconds, 86400))) return null;
  const { broken, treasures, durationSeconds } = activity;
  return JSON.stringify({ broken, treasures, durationSeconds: durationSeconds ?? null });
}

function packPersonalActivity(text) {
  if (typeof text !== 'string' || text.length > 16000) return null;
  let activity;
  try { activity = JSON.parse(text); } catch { return null; }
  if (!activity || !Array.isArray(activity.recent) || activity.recent.length > 100) return null;
  const { matches, opened, broken, extra, average, best, bestAt, perMinute, last, treasures, recent } = activity;
  if (![matches, opened, broken, extra, treasures].every(value => validCount(value, 50000000)) ||
      ![best, last].every(value => value == null || validCount(value, 50000000)) ||
      ![average, perMinute].every(value => value == null || (Number.isFinite(value) && value >= 0)) ||
      (bestAt != null && !validCount(bestAt, 8640000000000000)) ||
      !recent.every(point => point && validCount(point.at, 8640000000000000) &&
        [point.opened, point.broken, point.extra].every(value => validCount(value, 50000000)))) return null;
  return JSON.stringify({ matches, opened, broken, extra, average, best, bestAt, perMinute, last, treasures,
    recent: recent.map(({ at, opened, broken, extra }) => ({ at, opened, broken, extra })) });
}

async function sharedActivity(db, teamId, options) {
  const result = await one(db, `WITH eligible AS (${eligibleMatches}), entries AS (
    SELECT entry.session_id, entry.started_at, ${countedSql('entry.parts', 'entry.chests', options)} AS total,
      CASE WHEN entry.activity IS NOT NULL AND entry.parts IS NOT NULL
        THEN json_extract(entry.parts, '$.chests')-json_extract(entry.activity, '$.broken') ELSE 0 END AS opened,
      COALESCE(json_extract(entry.activity, '$.broken'),0) AS broken,
      json_extract(entry.activity, '$.treasures') AS treasures,
      NULLIF(json_extract(entry.activity, '$.durationSeconds'),0) AS seconds
    FROM eligible entry LEFT JOIN teamers_snapshots s ON s.profile_id=entry.profile_id
  ), activity AS (
    SELECT session_id, MIN(started_at) AS at, SUM(total) AS total, SUM(opened) AS opened, SUM(broken) AS broken,
      SUM(total-opened-broken) AS extra, SUM(treasures) AS treasures, MAX(seconds) AS seconds
    FROM entries GROUP BY session_id
  ), recent AS (
    SELECT * FROM activity ORDER BY at DESC, session_id DESC LIMIT 100
  ), best AS (
    SELECT * FROM activity ORDER BY total DESC, at DESC, session_id DESC LIMIT 1
  )
  SELECT COUNT(*) AS matches, COALESCE(SUM(opened),0) AS opened, COALESCE(SUM(broken),0) AS broken,
    COALESCE(SUM(extra),0) AS extra, AVG(total) AS average, SUM(treasures) AS treasures,
    (SELECT total FROM best) AS best, (SELECT at FROM best) AS bestAt,
    (SELECT total FROM recent LIMIT 1) AS last, 60.0*SUM(CASE WHEN seconds IS NOT NULL THEN total END)/SUM(seconds) AS perMinute,
    (SELECT json_group_array(json_object('at',at,'opened',opened,'broken',broken,'extra',extra)) FROM recent) AS recent
  FROM activity`, teamId);
  return { ...result, recent: JSON.parse(result.recent).reverse() };
}
