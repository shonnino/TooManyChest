using System;
using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ChestCounter;

[SupportedOSPlatform("windows")]
public sealed class FitLabel : Label
{
	private Font? _fit;

	private Font? _fitBase;

	private string? _fitKey;

	public FitLabel()
	{
		SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, value: true);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		string text = Text ?? "";
		if (text.Length != 0)
		{
			TextFormatFlags flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | Align(TextAlign);
			Font font = FitFont(e.Graphics, text);
			TextRenderer.DrawText(e.Graphics, text, font, ClientRectangle, Enabled ? ForeColor : SystemColors.GrayText, flags);
		}
	}

	private Font FitFont(IDeviceContext dc, string text)
	{
		string text2 = $"{text}|{ClientSize}";
		if (text2 == _fitKey && _fitBase == Font)
		{
			return _fit ?? Font;
		}
		float num = Font.Size;
		Size clientSize = ClientSize;
		Font font = Font;
		for (int i = 0; i < 12; i++)
		{
			Size size = TextRenderer.MeasureText(dc, text, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
			if ((size.Width <= clientSize.Width && size.Height <= clientSize.Height) || num <= 5f)
			{
				break;
			}
			float val = Math.Min((float)clientSize.Width / (float)Math.Max(1, size.Width), (float)clientSize.Height / (float)Math.Max(1, size.Height));
			num = Math.Max(5f, num * Math.Min(val, 0.97f));
			if (font != Font)
			{
				font.Dispose();
			}
			font = new Font(Font.FontFamily, num, Font.Style, Font.Unit);
		}
		_fit?.Dispose();
		_fit = ((font == Font) ? null : font);
		_fitBase = Font;
		_fitKey = text2;
		return font;
	}

	private static TextFormatFlags Align(ContentAlignment a)
	{
		return a switch
		{
			ContentAlignment.TopLeft => TextFormatFlags.Default,
			ContentAlignment.TopCenter => TextFormatFlags.HorizontalCenter,
			ContentAlignment.TopRight => TextFormatFlags.Right,
			ContentAlignment.MiddleLeft => TextFormatFlags.VerticalCenter,
			ContentAlignment.MiddleCenter => TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter,
			ContentAlignment.MiddleRight => TextFormatFlags.Right | TextFormatFlags.VerticalCenter,
			ContentAlignment.BottomLeft => TextFormatFlags.Bottom,
			ContentAlignment.BottomCenter => TextFormatFlags.Bottom | TextFormatFlags.HorizontalCenter,
			_ => TextFormatFlags.Bottom | TextFormatFlags.Right,
		};
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_fit?.Dispose();
			_fit = null;
		}
		base.Dispose(disposing);
	}
}
