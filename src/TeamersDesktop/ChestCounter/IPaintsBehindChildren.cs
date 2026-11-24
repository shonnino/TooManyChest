using System.Drawing;

namespace ChestCounter;

public interface IPaintsBehindChildren
{
	void PaintSurface(Graphics g);
}
