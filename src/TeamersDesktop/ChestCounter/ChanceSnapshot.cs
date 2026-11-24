using System;
using System.Collections.Generic;

namespace ChestCounter;

public sealed record ChanceSnapshot(IReadOnlyList<PrizeChance> Prizes, int Chests, TreasureWindow? Live, TreasureWindow? Next, DateTime WeekStart, bool LiveUnknown, bool NextUnknown);
