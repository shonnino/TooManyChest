using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ChestCounter;

[SupportedOSPlatform("windows")]
public sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
	private sealed class Colors : ProfessionalColorTable
	{
		public override Color ToolStripDropDownBackground => Theme.Panel;

		public override Color MenuBorder => Theme.Line;

		public override Color MenuItemBorder => Color.Transparent;

		public override Color MenuItemSelected => Theme.PanelHover;

		public override Color ImageMarginGradientBegin => Theme.Panel;

		public override Color ImageMarginGradientMiddle => Theme.Panel;

		public override Color ImageMarginGradientEnd => Theme.Panel;

		public override Color SeparatorDark => Theme.Line;

		public override Color SeparatorLight => Theme.Panel;
	}

	public DarkMenuRenderer()
		: base(new Colors())
	{
		RoundedEdges = true;
	}

	protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
	{
		e.TextColor = e.Item.ForeColor;
		base.OnRenderItemText(e);
	}

	protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
	{
		if (!e.Item.Selected)
		{
			return;
		}
		e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
		using GraphicsPath path = Shape.Rounded(new RectangleF(2f, 1f, e.Item.Width - 4, e.Item.Height - 2), 6f);
		using SolidBrush brush = new SolidBrush(Theme.PanelHover);
		e.Graphics.FillPath(brush, path);
	}
}
