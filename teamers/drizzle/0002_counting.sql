ALTER TABLE teamers_snapshots ADD COLUMN parts TEXT;
--> statement-breakpoint
ALTER TABLE teamers_snapshots ADD COLUMN app_options INTEGER;
--> statement-breakpoint
ALTER TABLE teamers_snapshots ADD COLUMN count_options INTEGER;
--> statement-breakpoint
ALTER TABLE teamers_members ADD COLUMN initial_parts TEXT;
--> statement-breakpoint
ALTER TABLE teamers_matches ADD COLUMN parts TEXT;
