CREATE TABLE profiles (
  id TEXT PRIMARY KEY,
  site_identity TEXT NOT NULL UNIQUE,
  username TEXT NOT NULL COLLATE NOCASE UNIQUE
);
--> statement-breakpoint

CREATE TABLE teamers_devices (
  token_hash TEXT PRIMARY KEY,
  profile_id TEXT NOT NULL REFERENCES profiles(id) ON DELETE CASCADE
);
--> statement-breakpoint
CREATE INDEX teamers_devices_by_profile ON teamers_devices(profile_id);
--> statement-breakpoint

CREATE TABLE teamers_pair_codes (
  profile_id TEXT PRIMARY KEY REFERENCES profiles(id) ON DELETE CASCADE,
  code_hash TEXT NOT NULL UNIQUE,
  expires_at INTEGER NOT NULL
);
--> statement-breakpoint

CREATE TABLE teamers_snapshots (
  profile_id TEXT PRIMARY KEY REFERENCES profiles(id) ON DELETE CASCADE,
  player_key TEXT NOT NULL,
  initial_chests INTEGER NOT NULL CHECK(initial_chests >= 0),
  synchronized_at INTEGER NOT NULL
);
--> statement-breakpoint

CREATE TABLE IF NOT EXISTS teamers_teams (
  id TEXT PRIMARY KEY,
  owner_id TEXT NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
  name TEXT NOT NULL
);
--> statement-breakpoint

CREATE TABLE IF NOT EXISTS teamers_members (
  profile_id TEXT PRIMARY KEY REFERENCES profiles(id) ON DELETE CASCADE,
  team_id TEXT NOT NULL REFERENCES teamers_teams(id) ON DELETE CASCADE,
  player_key TEXT NOT NULL UNIQUE,
  joined_at INTEGER NOT NULL,
  initial_chests INTEGER NOT NULL CHECK (initial_chests >= 0)
);
--> statement-breakpoint
CREATE INDEX IF NOT EXISTS teamers_members_team ON teamers_members(team_id, joined_at);
--> statement-breakpoint

CREATE TABLE IF NOT EXISTS teamers_invitations (
  id TEXT PRIMARY KEY,
  team_id TEXT NOT NULL REFERENCES teamers_teams(id) ON DELETE CASCADE,
  recipient_id TEXT NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
  status TEXT NOT NULL CHECK (status IN ('pending', 'accepted', 'declined')),
  created_at INTEGER NOT NULL,
  UNIQUE(team_id, recipient_id)
);
--> statement-breakpoint
CREATE INDEX IF NOT EXISTS teamers_invites_inbox ON teamers_invitations(recipient_id, status);
--> statement-breakpoint

CREATE TABLE IF NOT EXISTS teamers_matches (
  team_id TEXT NOT NULL REFERENCES teamers_teams(id) ON DELETE CASCADE,
  session_id TEXT NOT NULL,
  profile_id TEXT NOT NULL REFERENCES teamers_members(profile_id) ON DELETE CASCADE,
  started_at INTEGER NOT NULL,
  roster TEXT NOT NULL,
  member_count INTEGER NOT NULL CHECK (member_count BETWEEN 2 AND 4),
  chests INTEGER NOT NULL CHECK (chests >= 0),
  PRIMARY KEY (team_id, session_id, profile_id)
);
