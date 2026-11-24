ALTER TABLE profiles ADD COLUMN epic_player_key TEXT;
--> statement-breakpoint
ALTER TABLE profiles ADD COLUMN epic_username TEXT;
--> statement-breakpoint
CREATE UNIQUE INDEX profiles_epic_player_key ON profiles(epic_player_key);
--> statement-breakpoint
CREATE TABLE teamers_connections (
  browser_hash TEXT PRIMARY KEY,
  code_hash TEXT NOT NULL UNIQUE,
  expires_at INTEGER NOT NULL,
  profile_id TEXT REFERENCES profiles(id) ON DELETE CASCADE,
  approved_by TEXT
);
--> statement-breakpoint
CREATE INDEX teamers_connections_expiration ON teamers_connections(expires_at);
--> statement-breakpoint
CREATE TABLE teamers_browser_sessions (
  token_hash TEXT PRIMARY KEY,
  profile_id TEXT NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
  expires_at INTEGER NOT NULL
);
--> statement-breakpoint
CREATE INDEX teamers_browser_expiration ON teamers_browser_sessions(expires_at);
--> statement-breakpoint
CREATE TABLE teamers_join_links (
  team_id TEXT PRIMARY KEY REFERENCES teamers_teams(id) ON DELETE CASCADE,
  code_hash TEXT NOT NULL UNIQUE,
  expires_at INTEGER NOT NULL
);
