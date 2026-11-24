using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ChestCounter;

[SupportedOSPlatform("windows")]
public sealed class ModernSelect : Control
{
	private IList _items = new List<object>();

	private object? _selected;

	private bool _hover;

	private readonly Font _caption = Fonts.Body(7f, FontStyle.Bold);

	private readonly Font _value = Fonts.Body(10f, FontStyle.Bold);

	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public string DisplayMember { get; set; } = "";

	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public object? DataSource
	{
		get
		{
			return _items;
		}
		set
		{
			_items = (value as IList) ?? new List<object>();
			if (_selected != null && !_items.Contains(_selected))
			{
				_selected = null;
			}
			Invalidate();
		}
	}

	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public IEnumerable Items => _items;

	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public object? SelectedItem
	{
		get
		{
			return _selected;
		}
		set
		{
			if (!object.Equals(_selected, value))
			{
				_selected = value;
				Invalidate();
				SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
			}
		}
	}

	public event EventHandler? SelectedIndexChanged;

	public ModernSelect()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		Size = new Size(250, 44);
		Cursor = Cursors.Hand;
		BackColor = Theme.Panel;
		ForeColor = Theme.Ink;
	}

	private string TextOf(object? item)
	{
		if (item == null)
		{
			return "";
		}
		if (DisplayMember.Length > 0)
		{
			PropertyInfo property = item.GetType().GetProperty(DisplayMember);
			if (property != null)
			{
				return property.GetValue(item)?.ToString() ?? "";
			}
		}
		return item.ToString() ?? "";
	}

	protected override void OnMouseEnter(EventArgs e)
	{
		_hover = true;
		Invalidate();
		base.OnMouseEnter(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		_hover = false;
		Invalidate();
		base.OnMouseLeave(e);
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		base.OnMouseUp(e);
		if (e.Button == MouseButtons.Left && _items.Count > 1 && ClientRectangle.Contains(e.Location))
		{
			ShowMenu();
		}
	}

	private void ShowMenu()
	{
		ContextMenuStrip menu = new ContextMenuStrip
		{
			Renderer = new DarkMenuRenderer(),
			ShowImageMargin = false,
			ShowCheckMargin = false,
			BackColor = Theme.Panel,
			Font = Theme.Text(9.5f),
			Padding = new Padding(4),
			MinimumSize = new Size(Width, 0)
		};
		foreach (object item in _items.Cast<object>().ToList())
		{
			object captured = item;
			ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem(TextOf(item))
			{
				ForeColor = (object.Equals(item, _selected) ? Theme.Cyan : Theme.Ink),
				Padding = new Padding(4, 4, 18, 4)
			};
			toolStripMenuItem.Click += (object? _, EventArgs _) =>
			{
				SelectedItem = captured;
			};
			menu.Items.Add(toolStripMenuItem);
		}
		menu.Closed += (object? _, ToolStripDropDownClosedEventArgs _) =>
		{
			BeginInvoke(menu.Dispose);
		};
		menu.Show(this, new Point(0, Height + 4));
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
		e.Graphics.Clear(Parent?.BackColor ?? Theme.Bg);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		if (Width < 60 || Height < 20)
		{
			return;
		}
		Graphics graphics = e.Graphics;
		graphics.Clear(Parent?.BackColor ?? Theme.Bg);
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		using (GraphicsPath path = Shape.Rounded(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 12f))
		{
			using SolidBrush brush = new SolidBrush(_hover ? Theme.PanelHover : BackColor);
			using Pen pen = new Pen(_hover ? Color.FromArgb(160, Theme.Accent) : Theme.CardEdge, 1f);
			graphics.FillPath(brush, path);
			graphics.DrawPath(pen, path);
		}
		bool flag = _items.Count > 1;
		int width = Width - (flag ? 48 : 28);
		TextRenderer.DrawText(graphics, Fonts.Track("ACCOUNT"), _caption, new Rectangle(14, 5, width, 14), Theme.Muted, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
		TextRenderer.DrawText(graphics, TextOf(_selected), _value, new Rectangle(14, 19, width, Height - 24), ForeColor, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
		if (!flag)
		{
			return;
		}
		float num = (float)Width - 22f;
		float num2 = (float)Height / 2f;
		using SolidBrush brush2 = new SolidBrush(_hover ? Theme.Accent : Theme.Muted);
		graphics.FillPolygon(brush2, new PointF[3]
		{
			new PointF(num - 5f, num2 - 2f),
			new PointF(num + 5f, num2 - 2f),
			new PointF(num, num2 + 4f)
		});
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_caption.Dispose();
			_value.Dispose();
		}
		base.Dispose(disposing);
	}
}
