using System;
using System.Drawing;

namespace ChestCounter;

internal static class Fx
{
	public static Color Mix(Color a, Color b, float t)
	{
		t = Math.Clamp(t, 0f, 1f);
		return Color.FromArgb((int)((float)(int)a.A + (float)(b.A - a.A) * t), (int)((float)(int)a.R + (float)(b.R - a.R) * t), (int)((float)(int)a.G + (float)(b.G - a.G) * t), (int)((float)(int)a.B + (float)(b.B - a.B) * t));
	}
}
