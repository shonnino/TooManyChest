using System.Collections.Generic;
using System.Linq;

namespace ChestCounter;

public sealed record TeamSummary(bool Live, int Mine, IReadOnlyList<TeamMember> Mates, bool Unknown)
{
	public int MatesTotal => Mates.Sum((TeamMember m) => m.Chests);

	public int Total => Mine + MatesTotal;
}
