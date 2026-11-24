using System.Drawing;

namespace ChestCounter;

internal sealed class OddsRow
{
	public double Fill;

	public double Target;

	public double Ahead;

	public required PrizeChance Prize { get; init; }

	public required Bitmap Image { get; init; }
}
