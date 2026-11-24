using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ChestCounter;

internal sealed record TeamersMember(string Username, string PlayerKey, long JoinedAt, int InitialChests, int SharedChests, bool InitialReady);
internal sealed record TeamersInvite(string Id, string TeamName, string FromUsername);
internal sealed record TeamersSentInvite(string Username, string Status);
internal sealed record TeamersTeam(string Id, string Name, bool CanInvite, string PlayerKey, TeamersMember[] Members,
    TeamersSentInvite[] Outgoing, int InitialChests, int SharedChests, int SharedMatches, TeamersActivity? Activity);
internal sealed record TeamersPersonal(int Total, bool Ready, TeamersActivity? Activity);
internal sealed record TeamersCounting(bool Ready, int Options, int Starting);
internal sealed record TeamersState(string? Username, TeamersTeam? Team, TeamersInvite[] Invitations,
    TeamersPersonal? Personal, TeamersCounting? Counting);

internal sealed class TeamersClient(Store store, Config config)
{
    internal const string SiteOrigin = "https://chestcounter.lissomepika2.chatgpt.site";
    internal const string SitePage = SiteOrigin + "/teamers";

    private sealed record MatchSubmission(string SessionId, long StartedAt, int Chests, string[] Teammates,
        ChestCounts Parts, TeamersMatchActivity Activity);
    private sealed record MatchResponse(string[] Recorded);
    private sealed record ErrorResponse(string Error);
    private sealed record Connection(string ApiBase, string? Token = null);
    private sealed record LoginResponse(string Token);
    private sealed record InviteLink(string Link);
    internal sealed record ChestCounts(int Chests, int AmmoBoxes, int FoodBoxes, int LootCannons, int StartingChests, int ManualChests)
    {
        public int Total(int options) => Chests + ((options & 1) != 0 ? AmmoBoxes : 0) + ((options & 2) != 0 ? FoodBoxes : 0)
            + ((options & 4) != 0 ? LootCannons : 0) + ((options & 8) != 0 ? StartingChests : 0) + ((options & 16) != 0 ? ManualChests : 0);

        public static ChestCounts FromMatch(MatchRecord match) => new(match.OwnChests,
            match.Interactions("Ammo box"), match.Interactions("Food box"), match.Interactions("Loot cannon"), 0, 0);

        public static ChestCounts FromMatches(IEnumerable<MatchRecord> matches, int starting, int manual)
        {
            int chests = 0, ammo = 0, food = 0, cannons = 0;
            foreach (var match in matches)
            {
                chests += match.OwnChests;
                ammo += match.Interactions("Ammo box");
                food += match.Interactions("Food box");
                cannons += match.Interactions("Loot cannon");
            }
            return new ChestCounts(chests, ammo, food, cannons, starting, manual);
        }
    }

    internal sealed record LocalProfile(string EpicUsername, string PlayerKey, ChestCounts Parts, int AppOptions, string Activity)
    {
        public int InitialChests => Parts.Total(AppOptions);
    }

    private static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(10)
    };
    private static readonly JsonSerializerOptions ActivityJson = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly HashSet<string> reported = new(StringComparer.OrdinalIgnoreCase);
    private string? reportingTeam;
    private LocalProfile? synchronizedSnapshot;
    private string ConnectionPath => Path.Combine(store.Folder, "teamers-connection.json");

    public TeamersState? State { get; private set; }
    public string? Error { get; private set; }
    public bool Linked => LoadConnection()?.Token is { Length: > 0 };
    public event Action? Changed;

    public async Task RefreshAsync()
    {
        if (!await gate.WaitAsync(0)) return;
        try
        {
            Connection? account = LoadConnection();
            State = account?.Token is { Length: > 0 } ? await Send<TeamersState>(account, "") : null;
            Error = null;
            if (account != null && State != null)
            {
                State = await SynchronizeSnapshot(account, State);
                if (State.Team is { } team && await UploadSharedMatches(account, team))
                    State = await Send<TeamersState>(account, "");
            }
        }
        catch (Exception failure) { Error = failure.Message; }
        finally { gate.Release(); }
        Changed?.Invoke();
    }

    public async Task<bool> PairAsync(string code, string server = SiteOrigin) =>
        await PairProfileAsync(code, server, await Task.Run(TryPersonalSnapshot));

    internal async Task<bool> PairProfileAsync(string code, string server, LocalProfile? personal)
    {
        await gate.WaitAsync();
        try
        {
            if (personal == null) throw new InvalidOperationException(MissingProfile);
            var root = ServerUri(server);
            string origin = root.GetLeftPart(UriPartial.Authority);
            Connection? previous = LoadConnection();
            var connection = previous?.ApiBase == origin ? previous : new Connection(origin);
            var login = await Send<LoginResponse>(connection, "/pair",
                new { code, personal.EpicUsername, personal.PlayerKey, personal.InitialChests, personal.Parts, personal.AppOptions, personal.Activity },
                authenticate: connection.Token != null);
            connection = connection with { Token = login.Token };
            var state = await Send<TeamersState>(connection, "");
            string temporaryPath = ConnectionPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, JsonSerializer.Serialize(connection));
                File.Move(temporaryPath, ConnectionPath, overwrite: true);
            }
            finally { File.Delete(temporaryPath); }
            reportingTeam = null;
            synchronizedSnapshot = personal;
            State = state;
            Error = null;
            return true;
        }
        catch (Exception failure) { Error = failure.Message; return false; }
        finally { gate.Release(); Changed?.Invoke(); }
    }

    private async Task<TeamersState> SynchronizeSnapshot(Connection connection, TeamersState state)
    {
        var member = state.Team?.Members.FirstOrDefault(item => item.PlayerKey == state.Team.PlayerKey);
        long? joinedAt = member is { InitialReady: false } ? member.JoinedAt : null;
        var (personal, entryParts) = await Task.Run(() => BuildPersonalSnapshot(joinedAt));
        if (personal == null || (member != null && member.PlayerKey != personal.PlayerKey) ||
            (synchronizedSnapshot == personal && joinedAt == null && state.Personal is { Ready: true })) return state;
        var updated = await Send<TeamersState>(connection, "/snapshot",
            new { personal.PlayerKey, personal.InitialChests, personal.Parts, personal.AppOptions, personal.Activity, entryParts, joinedAt });
        synchronizedSnapshot = personal;
        return updated;
    }

    public Task CreateAsync(string name) => RunAction("/teams", async () =>
    {
        var personal = await Task.Run(PersonalSnapshot);
        return new { name, personal.PlayerKey, personal.InitialChests, personal.Parts };
    });

    public Task SaveCountingAsync(int options, int starting) =>
        RunAction("/counting", () => Task.FromResult<object>(new { options, starting }));

    public async Task<string?> GenerateInviteLinkAsync()
    {
        await gate.WaitAsync();
        try
        {
            var account = LoadConnection() ?? throw new InvalidOperationException(Text("Collega l’app a Teamers.", "Link the app to Teamers."));
            var invitation = await Send<InviteLink>(account, "/invite-link", new { });
            Error = null;
            return invitation.Link;
        }
        catch (Exception failure) { Error = failure.Message; return null; }
        finally { gate.Release(); Changed?.Invoke(); }
    }

    public Task RespondAsync(string invitationId, bool accept) => RunAction("/respond", async () =>
    {
        if (!accept) return new { invitationId, accept };
        var personal = await Task.Run(PersonalSnapshot);
        return new { invitationId, accept, personal.PlayerKey, personal.InitialChests, personal.Parts };
    });

    private async Task RunAction(string suffix, Func<Task<object>> request)
    {
        await gate.WaitAsync();
        try
        {
            var account = LoadConnection() ?? throw new InvalidOperationException(Text("Accedi al server Teamers.", "Sign in to the Teamers server."));
            State = await Send<TeamersState>(account, suffix, await request());
            Error = null;
        }
        catch (Exception failure) { Error = failure.Message; }
        finally { gate.Release(); }
        Changed?.Invoke();
    }

    internal static string MissingProfile => Text(
        "Seleziona un profilo Epic con almeno un replay completato.", "Select an Epic profile with a completed replay.");
    private LocalProfile PersonalSnapshot() => TryPersonalSnapshot() ?? throw new InvalidOperationException(MissingProfile);

    internal LocalProfile? TryPersonalSnapshot() => BuildPersonalSnapshot(null).Profile;

    private int AppCountingOptions => 24 | (config.CountAmmoBoxes ? 1 : 0) | (config.CountFoodBoxes ? 2 : 0) | (config.CountLootCannons ? 4 : 0);

    private (LocalProfile? Profile, ChestCounts? EntryParts) BuildPersonalSnapshot(long? joinedAt)
    {
        var data = PersonalReadCache.Load(store);
        string? profile = Profiles.Current(data, config)?.Key;
        if (!IsEpicId(profile)) return (null, null);

        var matches = Profiles.RecordedForProfile(data, config, profile).ToArray();
        string? epicName = matches.OrderByDescending(match => match.MatchStartUtc ?? match.AnalyzedAtUtc)
            .Select(match => match.PlayerName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
        if (epicName == null) return (null, null);
        int starting = Profiles.InitialChests(config, profile);
        var manual = PersonalIntegration.Manual(profile, config);
        var current = ChestCounts.FromMatches(matches, starting, manual.Sum(entry => Math.Max(0, entry.Mine)));
        ChestCounts? atEntry = null;
        if (joinedAt is long joined)
        {
            var boundary = DateTimeOffset.FromUnixTimeMilliseconds(joined).UtcDateTime;
            atEntry = ChestCounts.FromMatches(matches.Where(match => (match.MatchStartUtc ?? match.AnalyzedAtUtc).ToUniversalTime() < boundary),
                starting, manual.Where(entry => entry.AtUtc.ToUniversalTime() < boundary).Sum(entry => Math.Max(0, entry.Mine)));
        }
        int options = AppCountingOptions;
        string activity = JsonSerializer.Serialize(TeamersActivity.FromMatches(matches, options), ActivityJson);
        return (new LocalProfile(epicName.Trim(), PlayerKey(profile!), current, options, activity), atEntry);
    }

    private async Task<bool> UploadSharedMatches(Connection account, TeamersTeam team)
    {
        if (reportingTeam != team.Id)
        {
            reported.Clear();
            reportingTeam = team.Id;
        }
        if (team.Members.Length < 2) return false;

        var matches = await Task.Run(() => BuildSubmissions(team));
        if (matches.Count == 0) return false;
        var result = await Send<MatchResponse>(account, "/matches", new { playerKey = team.PlayerKey, matches });
        reported.UnionWith(result.Recorded);
        return result.Recorded.Length > 0;
    }

    private List<MatchSubmission> BuildSubmissions(TeamersTeam team)
    {
        var matches = new List<MatchSubmission>();
        var profileKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int options = AppCountingOptions;
        foreach (var match in PersonalReadCache.Load(store).Matches.OrderByDescending(match => match.MatchStartUtc))
        {
            if (!match.IsReplayOwner || !match.IsBattleRoyale || !IsEpicId(match.SessionId) || !IsEpicId(match.EpicId) ||
                match.MatchStartUtc is not DateTime start || reported.Contains(match.SessionId) || match.Teammates == null) continue;
            if (KeyOf(match.EpicId!) != team.PlayerKey) continue;

            long timestamp = new DateTimeOffset(start.ToUniversalTime()).ToUnixTimeMilliseconds();
            var expected = team.Members.Where(member => member.JoinedAt <= timestamp).ToArray();
            if (expected.Length < 2 || !expected.Any(member => member.PlayerKey == team.PlayerKey)) continue;
            var mates = match.Teammates.Where(mate => IsEpicId(mate.EpicId)).Select(mate => KeyOf(mate.EpicId!)).ToHashSet();
            var playingTogether = expected.Where(member => member.PlayerKey != team.PlayerKey && mates.Contains(member.PlayerKey))
                .Select(member => member.PlayerKey).ToArray();
            if (playingTogether.Length == 0) continue;
            var counts = ChestCounts.FromMatch(match);
            matches.Add(new MatchSubmission(match.SessionId.ToLowerInvariant(), timestamp, counts.Total(options), playingTogether,
                counts, TeamersMatchActivity.FromMatch(match)));
            if (matches.Count == 20) break;
        }
        return matches;

        string KeyOf(string epicId)
        {
            if (!profileKeys.TryGetValue(epicId, out var hash)) profileKeys[epicId] = hash = PlayerKey(epicId);
            return hash;
        }
    }

    private Connection? LoadConnection()
    {
        try
        {
            return File.Exists(ConnectionPath) ? JsonSerializer.Deserialize<Connection>(File.ReadAllText(ConnectionPath)) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    private static Uri ServerUri(string server)
    {
        if (!Uri.TryCreate(server.Trim(), UriKind.Absolute, out var root) || root.UserInfo.Length != 0 ||
            root.AbsolutePath != "/" || root.Query.Length != 0 || root.Fragment.Length != 0 ||
            (root.Scheme != Uri.UriSchemeHttps && !(root.IsLoopback && root.Scheme == Uri.UriSchemeHttp)))
            throw new InvalidOperationException(Text("Usa l’indirizzo HTTPS del server o l’indirizzo locale del PC host.", "Use the server's HTTPS address or the host PC's local address."));
        return root;
    }

    private static async Task<T> Send<T>(Connection account, string suffix, object? body = null, bool authenticate = true)
    {
        if (authenticate && string.IsNullOrEmpty(account.Token)) throw new InvalidOperationException(Text("Accedi al server Teamers.", "Sign in to the Teamers server."));
        var root = ServerUri(account.ApiBase);
        using var request = new HttpRequestMessage(body == null ? HttpMethod.Get : HttpMethod.Post, new Uri(root, "/api/teamers" + suffix));
        if (authenticate) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.Token);
        if (body != null) request.Content = JsonContent.Create(body);
        using var response = await Http.SendAsync(request);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<T>() ?? throw new InvalidOperationException("No Teamers response.");

        ErrorResponse? detail = null;
        if (response.Content.Headers.ContentType?.MediaType == "application/json")
            detail = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        if ((response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed && (body == null || detail == null)) ||
            detail?.Error.StartsWith("No profile.", StringComparison.Ordinal) == true)
            throw new InvalidOperationException(Text("Questo indirizzo non ospita il server privato Teamers.", "This address does not host the private Teamers server."));
        throw new InvalidOperationException(detail?.Error ?? $"Teamers HTTP {(int)response.StatusCode}.");
    }

    private static bool IsEpicId(string? value) => value?.Length == 32 && value.All(Uri.IsHexDigit);
    internal static string PlayerKey(string epicId) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("chestcounter-teamers-v1:" + epicId.ToLowerInvariant())));
    internal static string Text(string italian, string english) => L.Code == "it" || CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "it" ? italian : english;
}
