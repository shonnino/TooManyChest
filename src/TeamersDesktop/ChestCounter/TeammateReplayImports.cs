using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace ChestCounter;

internal static class TeammateReplayImports
{
    private sealed record Import(string SessionId, string OwnerId, TeammateRecord Recorder);
    private sealed record Snapshot(string Path, List<Import> Imports)
    {
        public ILookup<(string SessionId, string OwnerId), Import> ByMatch { get; } =
            Imports.ToLookup(item => (item.SessionId, item.OwnerId.ToUpperInvariant()));
    }

    private static readonly object Gate = new();
    private static Snapshot? cached;
    private static int revision;

    public static int Revision => Volatile.Read(ref revision);

    public static TeammateRecord ImportReplay(Store store, string path, string? profileKey)
    {
        if (ReplayHeaderProbe.IsLive(path) != false)
            throw new InvalidDataException(L.T("Selecciona una repetición terminada.", "Select a completed replay."));

        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        MatchRecord replay = new ReplayAnalyzer().Analyze(stream, Path.GetFileName(path), null);
        if (!replay.IsReplayOwner || !replay.IsBattleRoyale || string.IsNullOrWhiteSpace(replay.SessionId) || string.IsNullOrWhiteSpace(replay.EpicId))
            throw new InvalidDataException(L.T("No se pudo identificar al jugador que grabó la repetición.", "Could not identify the player who recorded the replay."));

        var own = PersonalIntegration.Recorded(store.LoadData()).Matches.FirstOrDefault(match =>
            match.IsBattleRoyale && match.SessionId == replay.SessionId &&
            (profileKey == null || SamePlayer(Profiles.Key(match), profileKey)) &&
            !SamePlayer(match.EpicId, replay.EpicId) &&
            match.Teammates?.Any(mate => SamePlayer(mate.EpicId, replay.EpicId)) == true &&
            replay.Teammates?.Any(mate => SamePlayer(mate.EpicId, match.EpicId)) == true);
        if (own == null || string.IsNullOrWhiteSpace(own.EpicId))
            throw new InvalidDataException(L.T("No se encontró esta partida con ese compañero en tu historial.", "This teammate and match could not be verified against your history."));

        var counts = new Dictionary<string, int>(replay.OtherInteractions, StringComparer.Ordinal);
        if (counts.Count == 0 && replay.AmmoBoxesOpened > 0) counts["Ammo box"] = replay.AmmoBoxesOpened;
        foreach (var group in replay.Chests.GroupBy(chest => chest.Kind)) counts[group.Key] = group.Count();
        var recorder = new TeammateRecord
        {
            EpicId = replay.EpicId,
            Name = replay.PlayerName ?? L.T("Compañero", "Teammate"),
            ChestsSeen = replay.ChestsOpened,
            Kinds = counts
        };
        string ledgerPath = Path.Combine(store.Folder, "teammate-replays.json");
        lock (Gate)
        {
            var imports = new List<Import>(Load(ledgerPath).Imports);
            int existing = imports.FindIndex(item => item.SessionId == own.SessionId &&
                SamePlayer(item.OwnerId, own.EpicId) && SamePlayer(item.Recorder.EpicId, recorder.EpicId));
            if (existing >= 0)
            {
                var previous = imports[existing].Recorder;
                if (previous.ChestsSeen == recorder.ChestsSeen && previous.Name == recorder.Name &&
                    previous.Kinds.Count == counts.Count && counts.All(kind => previous.Kinds.TryGetValue(kind.Key, out int count) && count == kind.Value))
                    return previous;
                imports[existing] = new Import(own.SessionId, own.EpicId, recorder);
            }
            else
            {
                imports.Add(new Import(own.SessionId, own.EpicId, recorder));
            }

            string temporaryPath = ledgerPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(imports));
            File.Move(temporaryPath, ledgerPath, overwrite: true);
            cached = new Snapshot(ledgerPath, imports);
            Interlocked.Increment(ref revision);
        }
        return recorder;
    }

    public static TrackerData Project(TrackerData data, string folder)
    {
        Snapshot snapshot;
        string path = Path.Combine(folder, "teammate-replays.json");
        lock (Gate)
        {
            try { snapshot = Load(path); }
            catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException)
            {
                ErrorLog.Write("Teammate replay data", error);
                return data;
            }
        }
        if (snapshot.Imports.Count == 0) return data;
        return new TrackerData
        {
            Version = data.Version,
            AnalyzerVersion = data.AnalyzerVersion,
            CreatedUtc = data.CreatedUtc,
            ProcessedFiles = data.ProcessedFiles,
            Matches = data.Matches.Select(match => Apply(match, snapshot)).ToList()
        };
    }

    private static Snapshot Load(string path)
    {
        if (cached?.Path == path) return cached;
        var imports = File.Exists(path)
            ? JsonSerializer.Deserialize<List<Import>>(File.ReadAllText(path)) ?? throw new InvalidDataException("Invalid teammate replay data.")
            : new List<Import>();
        return cached = new Snapshot(path, imports);
    }

    private static MatchRecord Apply(MatchRecord match, Snapshot snapshot)
    {
        if (match.EpicId == null || match.Teammates == null) return match;
        var imports = snapshot.ByMatch[(match.SessionId, match.EpicId.ToUpperInvariant())];
        if (!imports.Any()) return match;
        var teammates = match.Teammates.Select(mate => imports.FirstOrDefault(item =>
            SamePlayer(item.Recorder.EpicId, mate.EpicId))?.Recorder ?? mate).ToList();
        return match.WithTeammateReplays(teammates);
    }

    private static bool SamePlayer(string? first, string? second) =>
        !string.IsNullOrWhiteSpace(first) && string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
}
