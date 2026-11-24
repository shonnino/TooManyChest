using System;
using System.Collections.Generic;
using System.Linq;

namespace ChestCounter;

internal static class EnglishText
{
	private static readonly Dictionary<string, string> Fixes = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		["   Details in {0}. Usually caused by a Fortnite update: report it at {1}"] = "   Details are in {0}. This is usually caused by a Fortnite update; please report it at {1}",
		["   Match played on another account ({0}): it counts for its own profile. Profile shown: {1}."] = "   This match was played on another account ({0}), so it counts towards that profile. Profile shown: {1}.",
		["   Treasure(s) to name in {0}: {1}"] = "   Treasures to name in {0}: {1}",
		["And {0} with a single match (team fill), not shown."] = "Plus {0} with only one match together (team fill), not shown.",
		["Analyzing replays"] = "Analysing replays",
		["Analyzing your replays… {0} / {1}"] = "Analysing your replays… {0} / {1}",
		["Boosts unconfirmed - normal odds apply"] = "Boosts unconfirmed: normal odds apply",
		["Cannot write the report: {0}"] = "Couldn't write the report: {0}",
		["Closing with Fortnite — click here to keep open."] = "Closing with Fortnite. Click here to keep it open.",
		["Counts your matches on its own"] = "Counts your matches automatically",
		["Ctrl+Shift+O show, L lock, K compact"] = "Ctrl+Shift+O: show  ·  L: lock  ·  K: compact",
		["Current / previous match"] = "Current or last match",
		["previous"] = "last match",
		["Performance & behaviour"] = "Performance and behaviour",
		["VISUAL & PERFORMANCE OPTIONS"] = "VISUAL AND PERFORMANCE OPTIONS",
		["Overlay & goal"] = "Overlay and goal",
		["Previous"] = "Last match",
		["Delete selected entry"] = "Delete the selected entry",
		["Delete selected manual entry?"] = "Delete the selected manual entry?",
		["Update selected entry"] = "Update the selected entry",
		["Everything is ticked as owned. Fresh prizes arrive at the reset."] = "Everything is ticked as owned. New prizes arrive at the weekly reset.",
		["Fortnite is closed: the counter closes in 30 s."] = "Fortnite has closed. The counter will close in 30 s.",
		["Hide the dashboard banner to draw slightly less."] = "Hide the dashboard banner to save a little drawing work.",
		["Imported {0}: {1} personal chests. Stored in ledger and projected into dashboard totals."] = "Imported {0}: {1} personal chests. They're saved in your history and added to the dashboard totals.",
		["Imports are integrated. Manual additions affect totals and daily charts, not match averages. Edit them in Weekly tools."] = "Imports are included. Manual additions count towards totals and daily charts, but not match averages. Edit them in Weekly tools.",
		["Includes {0} manual / historical adjustments"] = "Includes {0} manual or historical adjustments",
		["It is optional and off by default. Press the button to open the website and approve this PC with one click. Only the match id, start time, mode and chest counts are uploaded. Never names, Epic ids, teammates or replay files."] = "It's optional and off by default. Press the button to open the website and approve this PC in one click. Only the match ID, start time, mode and chest counts are uploaded, never names, Epic IDs, teammates or replay files.",
		["Linking..."] = "Linking…",
		["Manual additions feed dashboard totals and daily charts. Dates below are UTC. Drop .replay files here."] = "Manual additions count towards dashboard totals and daily charts. Dates below are in UTC. Drop .replay files here to import them.",
		["New analysis version: the replays are read again (no duplicates)."] = "New analysis version: your replays are being read again (nothing is counted twice).",
		["Next Treasure Hour: {0:ddd dd MMM HH:mm} local"] = "Next Treasure Hour: {0:ddd dd MMM HH:mm} (local time)",
		["No matches analyzed yet. Play one and it will show up here."] = "No matches analysed yet. Play one and it will appear here.",
		["No team matches analyzed yet. They will show up here after your next duos, trios or squads match."] = "No team matches analysed yet. They'll appear here after your next Duos, Trios or Squads match.",
		["No treasures yet. They will show up here with the time and the chest they came from."] = "No treasures yet. They'll appear here with the time and the chest they came from.",
		["Not possible from here: open the counter with ChestCounter.exe."] = "This isn't possible from here. Open the counter with ChestCounter.exe.",
		["Notifications are optional. Uses your local odds schedule."] = "Notifications are optional and use your local odds schedule.",
		["Observed squad"] = "Squad total",
		["Observed squad ≥ {0}   |   You {1}   |   "] = "Squad total ≥ {0}   |   You {1}   |   ",
		["Open odds JSON"] = "Open odds file (JSON)",
		["Overlay hidden: Ctrl+Shift+O (from anywhere) shows it again."] = "Overlay hidden. Press Ctrl+Shift+O (from anywhere) to show it again.",
		["Overlay hidden: use the Overlay button in the window to show it again."] = "Overlay hidden. Use the Overlay button in the window to show it again.",
		["Overlay locked: the mouse goes through it. Click the padlock{0} to unlock it."] = "Overlay locked: clicks pass through it. Click the padlock{0} to unlock it.",
		["Overlay unlocked: drag it with the left mouse button; lock it again with the padlock."] = "Overlay unlocked: drag it with the left mouse button, then lock it again with the padlock.",
		["Parsing {0} - please wait"] = "Reading {0}, please wait…",
		["Personal source build\nOriginal parser retained"] = "Personal build\nOriginal replay parser",
		["Pirate shop"] = "Pirate Shop",
		["Settings → Game → Replays → Record Replays. Without it, Fortnite saves no replays and there is nothing to count."] = "Settings → Game → Replays → Record Replays. Without it, Fortnite doesn't save replays, so there's nothing to count.",
		["Squad data: enable teammate tracking in Settings. Counts are limited to replay visibility."] = "Squad data: turn on teammate tracking in Settings. Counts only include what your replay can see.",
		["TREASURE HOUR LIVE - ends in {0:00}:{1:00}:{2:00}"] = "TREASURE HOUR LIVE  ·  ends in {0:00}:{1:00}:{2:00}",
		["The chest counter is already open: look in the taskbar."] = "The chest counter is already open. Check your taskbar.",
		["The old version (ChestTracker.exe) is still open: close it, otherwise both count at the same time."] = "The old version (ChestTracker.exe) is still open. Close it, or both will count at the same time.",
		["Their chests: up to 1,087 m from you"] = "Their chests only count within 1,087 m of you",
		["This page updates by itself: the numbers below grow as the replays are read."] = "This page updates automatically: the numbers below grow as your replays are read.",
		["Total personal / configured"] = "Your total (with your settings)",
		["Unexpected error. The details are in errors.log, in the data folder."] = "Something went wrong. The details are in errors.log in your data folder.",
		["Weekly / import / manual"] = "Weekly, imports and manual",
		["Weekly prizes reset; Pirate Shop is permanent"] = "Weekly prizes reset every week; Pirate Shop access is permanent",
		["Weekly reset: {0:ddd dd MMM HH:mm} local · {1}d {2:00}h {3:00}m {4:00}s"] = "Weekly reset: {0:ddd dd MMM HH:mm} (local time) · {1}d {2:00}h {3:00}m {4:00}s",
		["Weekly report / chances"] = "Weekly report and chances",
		["Who you play with, matches together and wins."] = "Who you play with, how many matches you've played together and your wins.",
		["Your total, your average, your records and the overlay will also include your teammates' chests."] = "Your total, average, records and overlay will also include your teammates' chests.",
		["[{0:HH:mm:ss}] Analyzing {1} ({2} MB)…"] = "[{0:HH:mm:ss}] Analysing {1} ({2} MB)…",
		["[{0:HH:mm:ss}] Match in progress… (its chests will be counted at the end)"] = "[{0:HH:mm:ss}] Match in progress… (its chests are counted when it ends)",
		["[{0:HH:mm:ss}] Match over: analyzing the replay ({1} MB)…"] = "[{0:HH:mm:ss}] Match over: analysing the replay ({1} MB)…",
		["analyzed"] = "analysed",
		["replay analyzed"] = "replay analysed",
		["replays analyzed"] = "replays analysed",
		["brings ≥ {0} of the team"] = "adds ≥ {0} to the team",
		["not a Battle Royale match (Creative / other mode)"] = "not a Battle Royale match (Creative or another mode)",
		["of which destroyed instead of opened"] = "destroyed rather than opened",
		["other than from a chest"] = "not from a chest",
		["switch profile from the tool's settings"] = "switch profiles in Settings",
		["version prior to v43 (Chapter 7 Mini Season)"] = "version before v43 (Chapter 7 Mini Season)",
		["with a certain source"] = "with a confirmed source",
		["{0}. Ends {1:HH:mm} local."] = "{0}. Ends at {1:HH:mm} (local time).",
		["● Ready, play a match and it will appear here"] = "● Ready. Play a match and it will appear here",
		["● Ready, watching your replays"] = "● Ready and watching your replays",
		["● Replay folder not found, choose it in Settings"] = "● Replay folder not found. Choose it in Settings",
		["All your saved matches, chests and treasures will be deleted, as well as the chests before the counter. The counter starts from zero with your next match (the replays you already have are not counted again).\n\nA backup copy is saved in your data folder.\n\nReset the statistics?"] = "All your saved matches, chests and treasures will be deleted, along with the chests from before the counter. The counter starts from zero with your next match (replays you already have won't be counted again).\n\nA backup copy is saved in your data folder.\n\nReset your statistics?"
	};

	private static readonly Dictionary<string, string> AnyCase = BuildAnyCase();

	private static Dictionary<string, string> BuildAnyCase()
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (KeyValuePair<string, string> fix in Fixes)
		{
			dictionary.TryAdd(fix.Key, fix.Value);
		}
		return dictionary;
	}

	public static string Fix(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return text;
		}
		if (Fixes.TryGetValue(text, out string value))
		{
			return value;
		}
		if (!text.Any(char.IsLetter) || !(text == text.ToUpperInvariant()) || !AnyCase.TryGetValue(text, out value))
		{
			return text;
		}
		return value.ToUpperInvariant();
	}
}
