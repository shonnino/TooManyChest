using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using FortniteReplayReader;
using FortniteReplayReader.Models;
using FortniteReplayReader.Models.NetFieldExports;
using FortniteReplayReader.Models.NetFieldExports.Items;
using FortniteReplayReader.Models.NetFieldExports.Weapons;
using Microsoft.Extensions.Logging;
using Unreal.Core;
using Unreal.Core.Contracts;
using Unreal.Core.Models;
using Unreal.Core.Models.Enums;

namespace ChestCounter;

public sealed class ReplayAnalyzer : ReplayReader
{
	private sealed record SearchEvent(float Time, string ActorKey, string Label, uint? SearcherPawn, bool Destroyed = false);

	private sealed record TossEvent(float Time, string Item);

	private sealed record EquipEvent(float Time, string Item, uint Pawn);

	private sealed record PrizeEvent(float Time, string PrizeId, uint? Pawn, string Container);

	private sealed record GrantEvent(float Time, uint PlayerStateActor, int ElementId, string Handler);

	private sealed class StderrLogger : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull
		{
			return null;
		}

		public bool IsEnabled(LogLevel logLevel)
		{
			return logLevel >= LogLevel.Information;
		}

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			Console.Error.WriteLine($"LOG {logLevel} {Now,7:F1}s {formatter(state, exception)}");
		}
	}

	private static readonly Regex PrizeIdPattern;

	private static readonly Regex TreasureActor;

	private static readonly Regex UaidSuffix;

	private static readonly Regex TreasureItem;

	private readonly Dictionary<uint, string> _channelKey = new Dictionary<uint, string>();

	private readonly Dictionary<uint, string> _channelLabel = new Dictionary<uint, string>();

	private readonly Dictionary<uint, uint> _lastSearcher = new Dictionary<uint, uint>();

	private readonly HashSet<string> _searched = new HashSet<string>();

	private readonly List<SearchEvent> _searches = new List<SearchEvent>();

	private readonly List<TossEvent> _tosses = new List<TossEvent>();

	private readonly List<EquipEvent> _equips = new List<EquipEvent>();

	private readonly List<PrizeEvent> _prizes = new List<PrizeEvent>();

	private readonly Dictionary<string, float> _firstExport = new Dictionary<string, float>();

	private static readonly Regex TrackedExport;

	private readonly HashSet<uint> _prizeChannelsSeen = new HashSet<uint>();

	private readonly HashSet<uint> _equipChannelsSeen = new HashSet<uint>();

	private readonly Dictionary<uint, uint> _pawnToPlayerState = new Dictionary<uint, uint>();

	private readonly Dictionary<uint, int> _playerStateToPlayerId = new Dictionary<uint, int>();

	private readonly Dictionary<uint, string> _treasureHandlerGuids = new Dictionary<uint, string>();

	private readonly List<GrantEvent> _grants = new List<GrantEvent>();

	public const int CurrentVersion = 13;

	private static readonly bool ProbeDebug;

	internal static float Now => ReplayReader<FortniteReplay>.CurrentTime;

	static ReplayAnalyzer()
	{
		PrizeIdPattern = new Regex("[A-Za-z][A-Za-z0-9_]*:[A-Za-z0-9_.\\-]+", RegexOptions.Compiled);
		TreasureActor = new Regex("FlameCeleb|RelicExtractor|RelicFinder|LuckyLocator|SmugglerExtractionKey|ExtractionAccelerat", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		UaidSuffix = new Regex("_UAID_.*$", RegexOptions.Compiled);
		TreasureItem = new Regex("RelicExtractor|RelicFinder|LuckyLocator|FlameCeleb|SmugglerExtractionKey|ExtractionAccelerat|Treasure|PirateShop|DelilahShop", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		TrackedExport = new Regex("TreasureHandler|WID_(FlameCeleb|RelicExtractor|RelicFinder|LuckyLocator|SmugglerExtractionKey)", RegexOptions.Compiled);
		ProbeDebug = Environment.GetEnvironmentVariable("CHESTCOUNTER_DEBUG") == "4";
		NetFieldParser.AliasResolver = (string path) =>
		{
			if (path.Contains(':') || path.EndsWith("_ClassNetCache", StringComparison.Ordinal))
			{
				return (string?)null;
			}
			string text = path.Substring(path.LastIndexOf('/') + 1);
			if (text.StartsWith("B_", StringComparison.Ordinal) && TreasureActor.IsMatch(text))
			{
				return "__ChestCounter.GenericWeapon";
			}
			return (!path.Contains("/Containers/", StringComparison.OrdinalIgnoreCase) && !text.Contains("Chest", StringComparison.OrdinalIgnoreCase)) ? null : "__ChestCounter.GenericContainer";
		};
		NetFieldParser.ClassNetCacheAliasResolver = (string path) => (!path.Contains("LootCannon", StringComparison.OrdinalIgnoreCase) || !path.EndsWith("_ClassNetCache", StringComparison.Ordinal)) ? null : "__ChestCounter.LootCannon_ClassNetCache";
	}

	public ReplayAnalyzer()
		: base((Environment.GetEnvironmentVariable("CHESTCOUNTER_DEBUG") == "2") ? new StderrLogger() : null, Enum.TryParse<ParseMode>(Environment.GetEnvironmentVariable("CHESTCOUNTER_MODE"), out var result) ? result : ParseMode.Normal)
	{
		ReplayAnalyzer replayAnalyzer = this;
		Regex debugFilter = null;
		if (ProbeDebug)
		{
			string environmentVariable = Environment.GetEnvironmentVariable("CHESTCOUNTER_PROBE");
			debugFilter = new Regex((environmentVariable != null && environmentVariable.Length > 0) ? environmentVariable : "Treasure|Prize|Magpie|Inventory|Quest|Reward|Hunt|PlayerController|PlayerState|Notif", RegexOptions.IgnoreCase);
			NetGuidCache.KeepOnCleanup = true;
		}
		RawProbe.Filter = (string path) => path.Contains("FortAbilitySystemComponent", StringComparison.Ordinal) || (debugFilter?.IsMatch(path) ?? false);
		RawProbe.Sink = (uint channel, string group, string field, byte[] data, int bits) =>
		{
			if (group.EndsWith("AbilitySystemComponent_ClassNetCache", StringComparison.Ordinal) && replayAnalyzer._treasureHandlerGuids.Count > 0)
			{
				replayAnalyzer.ReadAbilityGrants(channel, data, bits);
			}
			Regex regex = debugFilter;
			if (regex != null && regex.IsMatch(group + "." + field))
			{
				replayAnalyzer.ProbeSink(channel, group, field, data, bits);
			}
		};
	}

	private void ReadAbilityGrants(uint channel, byte[] data, int bits)
	{
		int pos = 1;
		try
		{
			Read(32);
			Read(32);
			uint num = Read(32);
			uint num2 = Read(32);
			if (num > 512 || num2 > 512 || bits < 129)
			{
				return;
			}
			pos += (int)(num * 32);
			for (int i = 0; i < num2; i++)
			{
				int elementId = (int)Read(32);
				while (ReadPacked() != 0)
				{
					int num3 = (int)ReadPacked();
					if (num3 < 0 || pos + num3 > bits)
					{
						return;
					}
					bool flag;
					switch (num3)
					{
					case 8:
					case 16:
					case 24:
					case 32:
					case 40:
						flag = true;
						break;
					default:
						flag = false;
						break;
					}
					if (flag)
					{
						int num4 = pos;
						uint num5 = 0u;
						int num6 = 0;
						while (pos < num4 + num3)
						{
							uint num7 = Read(8);
							num5 |= num7 >> 1 << num6;
							if ((num7 & 1) == 0)
							{
								break;
							}
							num6 += 7;
						}
						pos = num4 + num3;
						if (_treasureHandlerGuids.TryGetValue(num5, out string value))
						{
							uint actor = (Channels[channel]?.ActorId).GetValueOrDefault();
							if (!_grants.Any((GrantEvent g) => g.PlayerStateActor == actor && g.ElementId == elementId))
							{
								_grants.Add(new GrantEvent(Now, actor, elementId, value));
							}
						}
					}
					else
					{
						pos += num3;
					}
				}
			}
		}
		catch (Exception ex) when ((ex is IndexOutOfRangeException || ex is InvalidDataException) ? true : false)
		{
			if (ProbeDebug)
			{
				Console.Error.WriteLine($"GRANTFAIL {Now,7:F1}s ch{channel} bits={bits} pos={pos} {ex.GetType().Name}");
			}
		}
		int Bit()
		{
			if (pos >= bits)
			{
				throw new IndexOutOfRangeException();
			}
			return (data[pos >> 3] >> (pos & 7)) & 1;
		}
		uint Read(int n)
		{
			uint num8 = 0u;
			for (int j = 0; j < n; j++)
			{
				num8 |= (uint)(Bit() << j);
				pos++;
			}
			return num8;
		}
		uint ReadPacked()
		{
			uint num8 = 0u;
			for (int j = 0; j < 35; j += 7)
			{
				uint num9 = Read(8);
				num8 |= num9 >> 1 << j;
				if ((num9 & 1) == 0)
				{
					return num8;
				}
			}
			throw new InvalidDataException();
		}
	}

	private void ProbeSink(uint channel, string group, string field, byte[] data, int bits)
	{
		HashSet<string> hashSet = new HashSet<string>();
		for (int i = 0; i < 8; i++)
		{
			StringBuilder stringBuilder = new StringBuilder();
			for (int j = 0; j + 1 <= (bits - i) / 8; j++)
			{
				int num = j * 8 + i;
				char c = (char)(((data[num / 8] >> num % 8) | ((num / 8 + 1 < data.Length) ? (data[num / 8 + 1] << 8 - num % 8) : 0)) & 0xFF);
				bool flag = char.IsAsciiLetterOrDigit(c);
				if (!flag)
				{
					bool flag2;
					switch (c)
					{
					case ' ':
					case '-':
					case '.':
					case '/':
					case ':':
					case '_':
						flag2 = true;
						break;
					default:
						flag2 = false;
						break;
					}
					flag = flag2;
				}
				if (flag)
				{
					stringBuilder.Append(c);
					continue;
				}
				if (stringBuilder.Length >= 5)
				{
					hashSet.Add(stringBuilder.ToString().Trim());
				}
				stringBuilder.Clear();
			}
			if (stringBuilder.Length >= 5)
			{
				hashSet.Add(stringBuilder.ToString().Trim());
			}
		}
		string text = "";
		if (bits <= 48)
		{
			uint num2 = 0u;
			int num3 = 0;
			byte[] array = data;
			foreach (byte b in array)
			{
				num2 |= (uint)(b >> 1 << num3);
				num3 += 7;
				if ((b & 1) == 0 || num3 > 28)
				{
					break;
				}
			}
			string text2 = PathOf(num2);
			if (!string.IsNullOrEmpty(text2))
			{
				text = $" guid={num2}:{CleanName(text2)}";
			}
		}
		if (bits > 48 && Environment.GetEnvironmentVariable("CHESTCOUNTER_PROBE_GUIDS") == "1")
		{
			List<string> list = new List<string>();
			for (int l = 0; l + 8 <= bits; l++)
			{
				uint num4 = 0u;
				int num5 = l;
				for (int m = 0; m < 5; m++)
				{
					if (num5 + 8 > bits)
					{
						break;
					}
					int num6 = 0;
					for (int n = 0; n < 8; n++)
					{
						num6 |= Bit(num5++) << n;
					}
					num4 |= (uint)(num6 >> 1 << 7 * m);
					if ((num6 & 1) == 0)
					{
						break;
					}
				}
				if (num4 > 64)
				{
					string text3 = PathOf(num4);
					if (!string.IsNullOrEmpty(text3))
					{
						list.Add($"@{l}:{num4}:{CleanName(text3)}");
					}
				}
			}
			if (list.Count > 0)
			{
				text = text + " refs=[" + string.Join(" ", list) + "]";
			}
		}
		string value = ((bits <= ((Environment.GetEnvironmentVariable("CHESTCOUNTER_PROBE_HEX") == "1") ? 100000 : 256)) ? (" hex=" + Convert.ToHexString(data)) : "");
		string valueOrDefault = _channelLabel.GetValueOrDefault(channel, "?");
		Console.Error.WriteLine($"PRB {Now,7:F1}s ch{channel} [{valueOrDefault}] {CleanName(group)}.{field} bits={bits}{text}{value}{((hashSet.Count > 0) ? (" txt=[" + string.Join(" | ", hashSet.Where((string s) => s.Length >= 5)) + "]") : "")}");
		int Bit(int pos)
		{
			if (pos >= bits)
			{
				return 0;
			}
			return (data[pos >> 3] >> (pos & 7)) & 1;
		}
	}

	protected override void OnNetDeltaRead(uint channelIndex, NetDeltaUpdate update)
	{
		base.OnNetDeltaRead(channelIndex, update);
		GrantedAbility grantedAbility = update.Export as GrantedAbility;
		bool flag = grantedAbility != null;
		if (flag)
		{
			string environmentVariable = Environment.GetEnvironmentVariable("CHESTCOUNTER_DEBUG");
			bool flag2 = ((environmentVariable == "1" || environmentVariable == "2") ? true : false);
			flag = flag2;
		}
		if (flag)
		{
			Console.Error.WriteLine($"DBG ability {Now,7:F1}s ch{channelIndex} idx={update.ElementIndex} del={update.Deleted} ability={CleanName(PathOf(grantedAbility.Ability))} src={CleanName(PathOf(grantedAbility.SourceObject))}");
		}
	}

	private static string Safe(Func<string> f)
	{
		try
		{
			return f();
		}
		catch
		{
			return "!";
		}
	}

	private string PathOf(uint? guid)
	{
		if (!guid.HasValue || guid == 0)
		{
			return "";
		}
		if (!_netGuidCache.TryGetPathName(guid.Value, out string pathName))
		{
			return "";
		}
		return pathName;
	}

	private static string CleanName(string path)
	{
		string text = path.Substring(path.LastIndexOf('/') + 1);
		int num = text.LastIndexOf('.');
		if (num >= 0)
		{
			text = text.Substring(num + 1);
		}
		return UaidSuffix.Replace(text, "");
	}

	protected override void OnNetGuidExported(uint netGuid, string pathName)
	{
		if (Environment.GetEnvironmentVariable("CHESTCOUNTER_DEBUG") == "3")
		{
			Console.Error.WriteLine($"EXP {Now,7:F1}s {pathName}");
		}
		if (TrackedExport.IsMatch(pathName))
		{
			string text = CleanName(pathName);
			_firstExport.TryAdd(text, Now);
			if (text.Contains("TreasureHandler", StringComparison.Ordinal))
			{
				_treasureHandlerGuids[netGuid] = text.Replace("Default__", "");
			}
		}
	}

	protected override void OnChannelOpened(uint channelIndex, NetworkGUID? actor)
	{
		base.OnChannelOpened(channelIndex, actor);
		UChannel uChannel = Channels[channelIndex];
		string text = PathOf(uChannel?.ArchetypeId);
		string text2 = PathOf(uChannel?.ActorId);
		_channelKey[channelIndex] = ((!string.IsNullOrEmpty(text2)) ? text2 : $"guid:{uChannel?.ActorId}");
		_channelLabel[channelIndex] = CleanName((!string.IsNullOrEmpty(text)) ? text : text2);
		_lastSearcher.Remove(channelIndex);
		_equipChannelsSeen.Remove(channelIndex);
		_prizeChannelsSeen.Remove(channelIndex);
	}

	private static string? ReadPrizeId(DebuggingObject? raw)
	{
		if (raw == null || raw.TotalBits == 0)
		{
			return null;
		}
		string input = new string(raw.Bytes.Select((byte b) => (char)((b < 32 || b >= 127) ? 32 : b)).ToArray());
		Match match = PrizeIdPattern.Match(input);
		if (!match.Success)
		{
			return null;
		}
		return match.Value;
	}

	protected override void OnExportRead(uint channelIndex, INetFieldExportGroup? exportGroup)
	{
		base.OnExportRead(channelIndex, exportGroup);
		if (!(exportGroup is BaseContainer { SearchingPawn: var searchingPawn } baseContainer))
		{
			if (!(exportGroup is GrantedAbility grantedAbility))
			{
				if (!(exportGroup is LootCannonFired { InteractingPawn: var interactingPawn }))
				{
					if (!(exportGroup is TreasureHuntChest treasureHuntChest))
					{
						if (!(exportGroup is GenericWeapon { Instigator: var instigator } genericWeapon))
						{
							if (!(exportGroup is FortPickup { bTossedFromContainer: var bTossedFromContainer } fortPickup))
							{
								if (!(exportGroup is PlayerPawn { PlayerState: var playerState }))
								{
									if (exportGroup is FortPlayerState fortPlayerState)
									{
										uint? num = Channels[channelIndex]?.ActorId;
										int? num2 = ((!fortPlayerState.PlayerId.HasValue) ? fortPlayerState.PlayerID : ((int?)fortPlayerState.PlayerId));
										if (num.HasValue && num2.HasValue)
										{
											_playerStateToPlayerId[num.Value] = num2.Value;
										}
									}
								}
								else
								{
									if (!playerState.HasValue)
									{
										return;
									}
									uint valueOrDefault = playerState.GetValueOrDefault();
									if (valueOrDefault != 0)
									{
										uint? num3 = Channels[channelIndex]?.ActorId;
										if (num3.HasValue)
										{
											_pawnToPlayerState[num3.Value] = valueOrDefault;
										}
									}
								}
							}
							else if (bTossedFromContainer == true && fortPickup.ItemDefinition != null)
							{
								string text = fortPickup.ItemDefinition.Name;
								if (string.IsNullOrEmpty(text))
								{
									text = CleanName(PathOf(fortPickup.ItemDefinition.Value));
								}
								if (!string.IsNullOrEmpty(text))
								{
									_tosses.Add(new TossEvent(Now, text));
								}
							}
							return;
						}
						uint? num4 = instigator ?? genericWeapon.Owner?.Value;
						if (!num4.HasValue)
						{
							return;
						}
						uint valueOrDefault2 = num4.GetValueOrDefault();
						if (valueOrDefault2 != 0 && _equipChannelsSeen.Add(channelIndex))
						{
							string text2 = genericWeapon.WeaponData?.Name;
							if (string.IsNullOrEmpty(text2))
							{
								text2 = _channelLabel.GetValueOrDefault(channelIndex, "?");
							}
							_equips.Add(new EquipEvent(Now, text2, valueOrDefault2));
						}
					}
					else
					{
						string text3 = ReadPrizeId(treasureHuntChest.PrizeTemplateId);
						if (text3 != null && treasureHuntChest.bSuccess != false && _prizeChannelsSeen.Add(channelIndex))
						{
							_prizes.Add(new PrizeEvent(Now, text3, treasureHuntChest.OpeningPawn ?? treasureHuntChest.Instigator, PathOf(treasureHuntChest.SourceContainer)));
						}
					}
				}
				else
				{
					if (!interactingPawn.HasValue)
					{
						return;
					}
					uint valueOrDefault3 = interactingPawn.GetValueOrDefault();
					if (valueOrDefault3 != 0)
					{
						string text4 = _channelKey.GetValueOrDefault(channelIndex, $"ch:{channelIndex}") + $"#fire{Now:F0}";
						if (_searched.Add(text4))
						{
							_searches.Add(new SearchEvent(Now, text4, _channelLabel.GetValueOrDefault(channelIndex, "LootCannon"), valueOrDefault3));
						}
					}
				}
			}
			else
			{
				string environmentVariable = Environment.GetEnvironmentVariable("CHESTCOUNTER_DEBUG");
				if ((environmentVariable == "1" || environmentVariable == "2") ? true : false)
				{
					Console.Error.WriteLine($"DBG abilityexp {Now,7:F1}s ch{channelIndex} owner={grantedAbility.OwnerActor} ability={CleanName(PathOf(grantedAbility.Ability))} src={CleanName(PathOf(grantedAbility.SourceObject))}");
				}
			}
			return;
		}
		if (searchingPawn.HasValue)
		{
			uint valueOrDefault4 = searchingPawn.GetValueOrDefault();
			if (valueOrDefault4 != 0)
			{
				_lastSearcher[channelIndex] = valueOrDefault4;
			}
		}
		string valueOrDefault5 = _channelKey.GetValueOrDefault(channelIndex, $"ch:{channelIndex}");
		if (baseContainer.bAlreadySearched == true)
		{
			if (_searched.Add(valueOrDefault5))
			{
				_searches.Add(new SearchEvent(Now, valueOrDefault5, _channelLabel.GetValueOrDefault(channelIndex, "?"), _lastSearcher.TryGetValue(channelIndex, out var value) ? new uint?(value) : ((uint?)null)));
			}
		}
		else
		{
			if (!baseContainer.bDestroyed)
			{
				return;
			}
			uint? searchingPawn2 = baseContainer.SearchingPawn;
			if (searchingPawn2.HasValue)
			{
				uint valueOrDefault6 = searchingPawn2.GetValueOrDefault();
				if (valueOrDefault6 != 0 && _searched.Add(valueOrDefault5))
				{
					_searches.Add(new SearchEvent(Now, valueOrDefault5, _channelLabel.GetValueOrDefault(channelIndex, "?"), valueOrDefault6, Destroyed: true));
				}
			}
		}
	}

	public MatchRecord Analyze(Stream stream, string file, string? username)
	{
		FortniteReplay fortniteReplay = ReadReplay(stream);
		if (Environment.GetEnvironmentVariable("CHESTCOUNTER_DEBUG") == "4")
		{
			Regex groupFilter = new Regex(Environment.GetEnvironmentVariable("CHESTCOUNTER_PROBE_GROUPS") ?? "AbilitySpec", RegexOptions.IgnoreCase);
			foreach (var (value, netFieldExportGroup2) in _netGuidCache.NetFieldExportGroupMap.Where((KeyValuePair<string, NetFieldExportGroup> kv) => groupFilter.IsMatch(kv.Key)))
			{
				Console.Error.WriteLine($"GRP {value} ({netFieldExportGroup2.NetFieldExportsLength})");
				for (int num = 0; num < netFieldExportGroup2.NetFieldExports.Length; num++)
				{
					NetFieldExport netFieldExport = netFieldExportGroup2.NetFieldExports[num];
					if (netFieldExport != null)
					{
						Console.Error.WriteLine($"GRP   [{num}] {netFieldExport.Name}");
					}
				}
			}
		}
		List<PlayerData> players = fortniteReplay.PlayerData?.ToList() ?? new List<PlayerData>();
		PlayerData target = null;
		bool flag = false;
		string[] names = (username ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (names.Length != 0)
		{
			target = players.FirstOrDefault((PlayerData p) => names.Any((string n) => string.Equals(p.PlayerName?.Trim(), n, StringComparison.OrdinalIgnoreCase)));
			flag = target != null;
		}
		uint? num2 = fortniteReplay.GameData?.RecorderId;
		PlayerData playerData = null;
		if (num2.HasValue && _playerStateToPlayerId.TryGetValue(num2.Value, out var recorderPlayerId))
		{
			playerData = players.FirstOrDefault((PlayerData p) => p.Id == recorderPlayerId);
		}
		bool flag2 = names.Length != 0 && target == null;
		if (target == null && !flag2)
		{
			target = playerData;
		}
		HashSet<uint> targetPlayerStates = new HashSet<uint>();
		PlayerData playerData2 = target;
		if (playerData2 != null && playerData2.Id.HasValue)
		{
			foreach (KeyValuePair<uint, int> item2 in _playerStateToPlayerId.Where((KeyValuePair<uint, int> kv) => kv.Value == target.Id))
			{
				targetPlayerStates.Add(item2.Key);
			}
		}
		if (targetPlayerStates.Count == 0 && num2.HasValue && !flag && !flag2)
		{
			targetPlayerStates.Add(num2.Value);
		}
		bool flag3 = num2.HasValue && targetPlayerStates.Contains(num2.Value);
		HashSet<uint> targetPawns = (from kv in _pawnToPlayerState
			where targetPlayerStates.Contains(kv.Value)
			select kv.Key).ToHashSet();
		List<(float, string, string, string, bool)> list = (from s in _searches.Where((SearchEvent s) =>
			{
				uint? searcherPawn2 = s.SearcherPawn;
				if (searcherPawn2.HasValue)
				{
					uint valueOrDefault9 = searcherPawn2.GetValueOrDefault();
					return targetPawns.Contains(valueOrDefault9);
				}
				return false;
			})
			orderby s.Time
			select (Time: s.Time, Kind: ChestKinds.Classify(s.Label), Label: s.Label, ActorKey: s.ActorKey, Destroyed: s.Destroyed)).ToList();
		List<ChestRecord> list2 = new List<ChestRecord>();
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		foreach (var search in list)
		{
			if (!ChestKinds.IsChest(search.Item2))
			{
				dictionary[search.Item2] = dictionary.GetValueOrDefault(search.Item2) + 1;
				continue;
			}
			List<string> list3 = (from t in _tosses
				where t.Time >= search.Item1 - 0.25f && t.Time <= search.Item1 + 0.6f
				select t.Item).ToList();
			list2.Add(new ChestRecord
			{
				TimeSeconds = MathF.Round(search.Item1, 1),
				Kind = search.Item2,
				Blueprint = search.Item3,
				Destroyed = search.Item5,
				Items = list3,
				TreasureItems = list3.Where((string i) => TreasureItem.IsMatch(i)).ToList()
			});
		}
		List<TeammateRecord> list4 = new List<TeammateRecord>();
		int? num3 = target?.TeamIndex;
		if (num3.HasValue)
		{
			int team = num3.GetValueOrDefault();
			foreach (PlayerData mate in (from p in players
				where p.TeamIndex == team && p.Id != target.Id && !p.IsBot
				group p by p.EpicId ?? p.PlayerName ?? $"pid{p.Id}" into g
				select g.First()).ToList())
			{
				HashSet<uint> states = (from kv in _playerStateToPlayerId
					where kv.Value == mate.Id
					select kv.Key).ToHashSet();
				HashSet<uint> pawns = (from kv in _pawnToPlayerState
					where states.Contains(kv.Value)
					select kv.Key).ToHashSet();
				Dictionary<string, int> dictionary2 = (from s in _searches.Where((SearchEvent s) =>
					{
						uint? searcherPawn2 = s.SearcherPawn;
						if (searcherPawn2.HasValue)
						{
							uint valueOrDefault9 = searcherPawn2.GetValueOrDefault();
							return pawns.Contains(valueOrDefault9);
						}
						return false;
					})
					group s by ChestKinds.Classify(s.Label)).ToDictionary((IGrouping<string, SearchEvent> g) => g.Key, (IGrouping<string, SearchEvent> g) => g.Count());
				list4.Add(new TeammateRecord
				{
					Name = ((!string.IsNullOrWhiteSpace(mate.PlayerName)) ? mate.PlayerName.Trim() : L.T("Compañero", "Teammate")),
					EpicId = mate.EpicId,
					ChestsSeen = dictionary2.Where((KeyValuePair<string, int> k) => ChestKinds.IsChest(k.Key)).Sum((KeyValuePair<string, int> k) => k.Value),
					Kinds = dictionary2
				});
			}
			list4.Sort((TeammateRecord a, TeammateRecord b) => b.ChestsSeen.CompareTo(a.ChestsSeen));
		}
		if (Environment.GetEnvironmentVariable("CHESTCOUNTER_DEBUG") == "1")
		{
			foreach (TeammateRecord item3 in list4)
			{
				Console.Error.WriteLine($"DBG teammate {item3.Name} chests={item3.ChestsSeen}");
			}
			foreach (EquipEvent equip in _equips)
			{
				Console.Error.WriteLine($"DBG equip {equip.Time,7:F1}s {equip.Item} by {NameOfPawn(equip.Pawn)}{(targetPawns.Contains(equip.Pawn) ? " (TARGET)" : "")}");
			}
			foreach (KeyValuePair<string, float> item4 in _firstExport)
			{
				Console.Error.WriteLine($"DBG export {item4.Value,7:F1}s {item4.Key}");
			}
			foreach (KeyValuePair<uint, string> treasureHandlerGuid in _treasureHandlerGuids)
			{
				Console.Error.WriteLine($"DBG handlerguid {treasureHandlerGuid.Key} {treasureHandlerGuid.Value}");
			}
			foreach (GrantEvent gr in _grants)
			{
				string value2 = ((!_playerStateToPlayerId.TryGetValue(gr.PlayerStateActor, out var gpid)) ? $"ps{gr.PlayerStateActor}" : (players.FirstOrDefault((PlayerData p) => p.Id == gpid)?.PlayerName ?? $"pid{gpid}"));
				Console.Error.WriteLine($"DBG grant {gr.Time,7:F1}s {gr.Handler} elem={gr.ElementId} to {value2}{(targetPlayerStates.Contains(gr.PlayerStateActor) ? " (TARGET)" : "")}");
				foreach (SearchEvent item5 in (from x in _searches
					where x.Time >= gr.Time - 3f && x.Time <= gr.Time + 2f
					orderby Math.Abs(x.Time - gr.Time)
					select x).Take(2))
				{
					TextWriter error = Console.Error;
					DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(17, 3);
					defaultInterpolatedStringHandler.AppendLiteral("DBG   near ");
					defaultInterpolatedStringHandler.AppendFormatted(item5.Time, 7, "F1");
					defaultInterpolatedStringHandler.AppendLiteral("s ");
					defaultInterpolatedStringHandler.AppendFormatted(CleanName(item5.Label));
					defaultInterpolatedStringHandler.AppendLiteral(" by ");
					uint? searcherPawn = item5.SearcherPawn;
					string value3;
					if (searcherPawn.HasValue)
					{
						uint valueOrDefault = searcherPawn.GetValueOrDefault();
						value3 = NameOfPawn(valueOrDefault);
					}
					else
					{
						value3 = "?";
					}
					defaultInterpolatedStringHandler.AppendFormatted(value3);
					error.WriteLine(defaultInterpolatedStringHandler.ToStringAndClear());
				}
			}
			foreach (KeyValuePair<string, float> ex in _firstExport.Where((KeyValuePair<string, float> kv) => kv.Key.Contains("TreasureHandler")).Take(1))
			{
				using IEnumerator<SearchEvent> enumerator10 = (from x in _searches
					where x.Time >= ex.Value - 6f && x.Time <= ex.Value + 3f
					orderby x.Time
					select x).GetEnumerator();
				DefaultInterpolatedStringHandler defaultInterpolatedStringHandler2;
				string value5;
				TextWriter error2;
				for (; enumerator10.MoveNext(); defaultInterpolatedStringHandler2.AppendFormatted(value5), error2.WriteLine(defaultInterpolatedStringHandler2.ToStringAndClear()))
				{
					SearchEvent current7 = enumerator10.Current;
					error2 = Console.Error;
					defaultInterpolatedStringHandler2 = new DefaultInterpolatedStringHandler(26, 6);
					defaultInterpolatedStringHandler2.AppendLiteral("DBG nearhandler ");
					defaultInterpolatedStringHandler2.AppendFormatted(current7.Time, 7, "F1");
					defaultInterpolatedStringHandler2.AppendLiteral("s (Δ");
					defaultInterpolatedStringHandler2.AppendFormatted(current7.Time - ex.Value, "+0.0;-0.0");
					defaultInterpolatedStringHandler2.AppendLiteral(") ");
					defaultInterpolatedStringHandler2.AppendFormatted(CleanName(current7.Label));
					defaultInterpolatedStringHandler2.AppendFormatted(current7.Destroyed ? " DESTROYED" : "");
					defaultInterpolatedStringHandler2.AppendLiteral(" by ");
					uint? searcherPawn = current7.SearcherPawn;
					string value4;
					if (searcherPawn.HasValue)
					{
						uint valueOrDefault2 = searcherPawn.GetValueOrDefault();
						value4 = NameOfPawn(valueOrDefault2);
					}
					else
					{
						value4 = "?";
					}
					defaultInterpolatedStringHandler2.AppendFormatted(value4);
					searcherPawn = current7.SearcherPawn;
					if (searcherPawn.HasValue)
					{
						uint valueOrDefault3 = searcherPawn.GetValueOrDefault();
						if (targetPawns.Contains(valueOrDefault3))
						{
							value5 = " (ME)";
							continue;
						}
					}
					value5 = "";
				}
			}
			using (List<PrizeEvent>.Enumerator enumerator12 = _prizes.GetEnumerator())
			{
				DefaultInterpolatedStringHandler defaultInterpolatedStringHandler3;
				string value7;
				PrizeEvent current8;
				TextWriter error3;
				for (; enumerator12.MoveNext(); defaultInterpolatedStringHandler3.AppendFormatted(value7), defaultInterpolatedStringHandler3.AppendLiteral(" chest="), defaultInterpolatedStringHandler3.AppendFormatted(CleanName(current8.Container)), error3.WriteLine(defaultInterpolatedStringHandler3.ToStringAndClear()))
				{
					current8 = enumerator12.Current;
					error3 = Console.Error;
					defaultInterpolatedStringHandler3 = new DefaultInterpolatedStringHandler(23, 5);
					defaultInterpolatedStringHandler3.AppendLiteral("DBG prize ");
					defaultInterpolatedStringHandler3.AppendFormatted(current8.Time, 7, "F1");
					defaultInterpolatedStringHandler3.AppendLiteral("s ");
					defaultInterpolatedStringHandler3.AppendFormatted(current8.PrizeId);
					defaultInterpolatedStringHandler3.AppendLiteral(" by ");
					uint? searcherPawn = current8.Pawn;
					string value6;
					if (searcherPawn.HasValue)
					{
						uint valueOrDefault4 = searcherPawn.GetValueOrDefault();
						value6 = NameOfPawn(valueOrDefault4);
					}
					else
					{
						value6 = "?";
					}
					defaultInterpolatedStringHandler3.AppendFormatted(value6);
					searcherPawn = current8.Pawn;
					if (searcherPawn.HasValue)
					{
						uint valueOrDefault5 = searcherPawn.GetValueOrDefault();
						if (targetPawns.Contains(valueOrDefault5))
						{
							value7 = " (TARGET)";
							continue;
						}
					}
					value7 = "";
				}
			}
			foreach (SearchEvent item6 in _searches.Where((SearchEvent s) => s.Label.Contains("Cannon", StringComparison.OrdinalIgnoreCase)))
			{
				TextWriter error4 = Console.Error;
				DefaultInterpolatedStringHandler defaultInterpolatedStringHandler4 = new DefaultInterpolatedStringHandler(17, 3);
				defaultInterpolatedStringHandler4.AppendLiteral("DBG cannon ");
				defaultInterpolatedStringHandler4.AppendFormatted(item6.Time, 7, "F1");
				defaultInterpolatedStringHandler4.AppendLiteral("s ");
				defaultInterpolatedStringHandler4.AppendFormatted(item6.Label);
				defaultInterpolatedStringHandler4.AppendLiteral(" by ");
				uint? searcherPawn = item6.SearcherPawn;
				string value8;
				if (searcherPawn.HasValue)
				{
					uint valueOrDefault6 = searcherPawn.GetValueOrDefault();
					value8 = NameOfPawn(valueOrDefault6);
				}
				else
				{
					value8 = "?";
				}
				defaultInterpolatedStringHandler4.AppendFormatted(value8);
				error4.WriteLine(defaultInterpolatedStringHandler4.ToStringAndClear());
			}
			foreach (var item7 in list)
			{
				Console.Error.WriteLine($"DBG interact {item7.Item1,7:F1}s {item7.Item2} {item7.Item3}{(item7.Item5 ? " (DESTROYED)" : "")}");
			}
		}
		List<TreasureFind> list5 = new List<TreasureFind>();
		foreach (PrizeEvent pz in _prizes.Where((PrizeEvent p) =>
		{
			uint? pawn = p.Pawn;
			if (pawn.HasValue)
			{
				uint valueOrDefault9 = pawn.GetValueOrDefault();
				return targetPawns.Contains(valueOrDefault9);
			}
			return false;
		}))
		{
			string text2 = CleanName(pz.Container);
			string text3 = (string.IsNullOrEmpty(text2) ? "unknown" : ChestKinds.Classify(text2));
			list5.Add(new TreasureFind
			{
				Item = TreasureNames.Friendly(pz.PrizeId),
				RawItem = pz.PrizeId,
				PrizeId = pz.PrizeId,
				FromGoldenChest = true,
				TimeExact = true,
				SourceDestroyed = list.Any(((float Time, string Kind, string Label, string ActorKey, bool Destroyed) i) => i.Destroyed && i.ActorKey == pz.Container),
				ObtainedAtSeconds = MathF.Round(pz.Time, 1),
				SourceKind = text3,
				SourceBlueprint = text2,
				SourceIsChest = ChestKinds.IsChest(text3),
				SourceCertain = true
			});
		}
		List<EquipEvent> source = (from e in _equips
			where targetPawns.Contains(e.Pawn)
			orderby e.Time
			select e).ToList();
		foreach (ChestRecord item8 in list2.Where((ChestRecord c) => c.TreasureItems.Count > 0))
		{
			foreach (string item in item8.TreasureItems.Select(TreasureNames.Friendly).Distinct())
			{
				if (list5.All((TreasureFind f) => f.Item != item))
				{
					list5.Add(new TreasureFind
					{
						Item = item,
						RawItem = item,
						ObtainedAtSeconds = item8.TimeSeconds,
						SourceKind = item8.Kind,
						SourceBlueprint = item8.Blueprint,
						SourceIsChest = true,
						SourceCertain = true
					});
				}
			}
		}
		List<float> list6 = (from g in _grants
			where targetPlayerStates.Contains(g.PlayerStateActor)
			select g.Time into t
			orderby t
			select t).ToList();
		if ((list6.Count == 0) & flag3)
		{
			float? num4 = _firstExport.Where((KeyValuePair<string, float> kv) => kv.Key.Contains("TreasureHandler")).Select((Func<KeyValuePair<string, float>, float?>)((KeyValuePair<string, float> kv) => kv.Value)).FirstOrDefault();
			if (num4.HasValue)
			{
				float valueOrDefault7 = num4.GetValueOrDefault();
				list6.Add(valueOrDefault7);
			}
		}
		for (int num5 = 0; num5 < list6.Count; num5++)
		{
			float h = list6[num5];
			float nextHandler = ((num5 + 1 < list6.Count) ? list6[num5 + 1] : float.MaxValue);
			SearchEvent searchEvent = (from x in _searches
				where x.Time >= h - 3f && x.Time <= h + 2f
				orderby Math.Abs(x.Time - h)
				select x).FirstOrDefault();
			int num6;
			if (searchEvent != null)
			{
				uint? searcherPawn = searchEvent.SearcherPawn;
				if (searcherPawn.HasValue)
				{
					uint valueOrDefault8 = searcherPawn.GetValueOrDefault();
					num6 = (targetPawns.Contains(valueOrDefault8) ? 1 : 0);
					goto IL_16b3;
				}
			}
			num6 = 0;
			goto IL_16b3;
			IL_16b3:
			TreasureFind treasureFind = list5.FirstOrDefault((TreasureFind f) =>
			{
				if (f.FromGoldenChest)
				{
					float? obtainedAtSeconds = f.ObtainedAtSeconds;
					if (obtainedAtSeconds.HasValue)
					{
						float valueOrDefault9 = obtainedAtSeconds.GetValueOrDefault();
						return Math.Abs(valueOrDefault9 - h) <= 5f;
					}
				}
				return false;
			});
			if (num6 != 0 && treasureFind == null)
			{
				string text4 = ChestKinds.Classify(searchEvent.Label);
				string text5 = (from kv in _firstExport
					where kv.Key.StartsWith("WID_") && Math.Abs(kv.Value - h) <= 1f
					select kv.Key).FirstOrDefault();
				if (text5 == null)
				{
					List<string> later = (from e in source
						where e.Time >= h && e.Time < nextHandler
						select TreasureNames.Friendly(e.Item) into n
						where !n.StartsWith("B_")
						select n).Distinct().ToList();
					if (later.Count == 1)
					{
						text5 = source.First((EquipEvent e) => e.Time >= h && e.Time < nextHandler && TreasureNames.Friendly(e.Item) == later[0]).Item;
					}
				}
				string text6 = ((text5 != null) ? TreasureNames.Friendly(text5) : null);
				DateTime value9 = DateTime.SpecifyKind(fortniteReplay.GameData?.UtcTimeStartedMatch ?? fortniteReplay.Info?.Timestamp ?? DateTime.UtcNow, DateTimeKind.Utc).ToLocalTime();
				float num7 = MathF.Round(h, 1);
				string prizeId = ((text5 == null) ? $"{"Unknown:"}{value9:yyyy-MM-dd_HH.mm}@{(int)num7 / 60}m{(int)num7 % 60:00}s" : null);
				list5.Add(new TreasureFind
				{
					Item = (text6 ?? "Treasure gizmo"),
					RawItem = (text5 ?? "GA_TreasureHandler"),
					PrizeId = prizeId,
					ObtainedAtSeconds = num7,
					TimeExact = true,
					SourceKind = text4,
					SourceBlueprint = searchEvent.Label,
					SourceIsChest = ChestKinds.IsChest(text4),
					SourceCertain = true,
					SourceDestroyed = searchEvent.Destroyed,
					FromPrizeHandler = true
				});
			}
		}
		list5.Sort((TreasureFind a, TreasureFind b) => (a.ObtainedAtSeconds ?? float.MaxValue).CompareTo(b.ObtainedAtSeconds ?? float.MaxValue));
		ReplayInfo info = fortniteReplay.Info;
		ReplayHeader header = fortniteReplay.Header;
		bool flag4 = num2.HasValue && targetPlayerStates.Contains(num2.Value);
		int? placement = target?.Placement ?? ((!flag4) ? ((int?)null) : ((int?)fortniteReplay.TeamStats?.Position));
		return new MatchRecord
		{
			SessionId = (fortniteReplay.GameData?.GameSessionId ?? $"{info?.Timestamp:O}-{Path.GetFileName(file)}"),
			ReplayFile = Path.GetFileName(file),
			AnalyzedAtUtc = DateTime.UtcNow,
			MatchStartUtc = (fortniteReplay.GameData?.UtcTimeStartedMatch ?? info?.Timestamp),
			DurationSeconds = ((info != null) ? new int?((int)(info.LengthInMs / 1000)) : ((int?)null)),
			Playlist = fortniteReplay.GameData?.CurrentPlaylist,
			GameVersion = header?.Branch,
			PlayerName = target?.PlayerName,
			EpicId = target?.EpicId,
			TrackedByUsername = flag,
			NotTrackedOwner = ((!flag2) ? null : (playerData?.PlayerName ?? "unknown")),
			Placement = placement,
			TotalPlayers = (int?)fortniteReplay.TeamStats?.TotalPlayers,
			IsReplayOwner = flag4,
			Eliminations = ((!flag4) ? ((int?)null) : ((int?)fortniteReplay.Stats?.Eliminations)),
			ChestsOpened = list2.Count,
			AmmoBoxesOpened = dictionary.GetValueOrDefault("Ammo box"),
			OtherContainersOpened = dictionary.Where((KeyValuePair<string, int> kv) => kv.Key != "Ammo box").Sum((KeyValuePair<string, int> kv) => kv.Value),
			OtherInteractions = dictionary,
			AnalyzerVersion = 13,
			IsBattleRoyale = (!string.IsNullOrEmpty(fortniteReplay.GameData?.CurrentPlaylist) || list2.Count > 0),
			Chests = list2,
			TreasureFinds = list5,
			Teammates = list4
		};
		string NameOfPawn(uint pawn)
		{
			if (_pawnToPlayerState.TryGetValue(pawn, out var value10) && _playerStateToPlayerId.TryGetValue(value10, out var pid))
			{
				return players.FirstOrDefault((PlayerData p) => p.Id == pid)?.PlayerName ?? $"pid{pid}";
			}
			return $"pawn{pawn}";
		}
	}
}
