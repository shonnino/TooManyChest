using System;

namespace ChestCounter;

public sealed record Profile(string Key, string Name, int Matches, int Chests, DateTime LastPlayed);
