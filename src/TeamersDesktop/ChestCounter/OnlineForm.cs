using System;
using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ChestCounter;

[SupportedOSPlatform("windows")]
internal sealed class OnlineForm : Form
{
	private readonly TextBox _code = new TextBox
	{
		Width = 200,
		CharacterCasing = CharacterCasing.Upper,
		MaxLength = 12
	};

	private readonly TextBox _user = new TextBox
	{
		Width = 200,
		MaxLength = 20
	};

	private readonly CheckBox _history = new CheckBox
	{
		AutoSize = true,
		Text = L.T("Subir también mi historial de partidas", "Also upload my existing match history")
	};

	private readonly CheckBox _sync = new CheckBox
	{
		AutoSize = true,
		Text = L.T("Subir cada partida al terminar", "Upload each finished match")
	};

	private readonly Label _status = new Label
	{
		AutoSize = false,
		Width = 460,
		Height = 54
	};

	private readonly Button _auto = new Button
	{
		AutoSize = true,
		Text = L.T("Abrir la web y vincular este PC", "Open website and link this PC")
	};

	private readonly Button _open = new Button
	{
		AutoSize = true,
		Text = L.T("Abrir mis estadísticas en la web", "Open my stats on the website")
	};

	private readonly Button _link = new Button
	{
		AutoSize = true,
		Text = L.T("Vincular con código", "Link with code")
	};

	private readonly Button _unlink = new Button
	{
		AutoSize = true,
		Text = L.T("Desvincular este PC", "Unlink this PC")
	};

	private readonly Button _saveUser = new Button
	{
		AutoSize = true,
		Text = L.T("Guardar nombre", "Save username")
	};

	private bool _loading;

	public static void ShowFor(IWin32Window? owner)
	{
		using OnlineForm onlineForm = new OnlineForm();
		onlineForm.ShowDialog(owner);
	}

	private OnlineForm()
	{
		Text = L.T("Conexión online", "Online connection");
		StartPosition = FormStartPosition.CenterParent;
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MaximizeBox = false;
		MinimizeBox = false;
		ShowInTaskbar = false;
		BackColor = Theme.Bg;
		ForeColor = Theme.Ink;
		Font = Theme.Text();
		ClientSize = new Size(500, 560);
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false,
			Padding = new Padding(18)
		};
		Label value = new Label
		{
			AutoSize = false,
			Width = 460,
			Height = 92,
			Text = L.T("Es opcional y está desactivado por defecto. Pulsa el botón para abrir la web y aprobar este PC con un clic. Solo se suben el id de la partida, la hora, el modo y los recuentos de cofres. Nunca nombres, ids de Epic, compañeros ni replays.", "It is optional and off by default. Press the button to open the website and approve this PC with one click. Only the match id, start time, mode and chest counts are uploaded. Never names, Epic ids, teammates or replay files.")
		};
		flowLayoutPanel.Controls.Add(value);
		flowLayoutPanel.Controls.Add(_history);
		flowLayoutPanel.Controls.Add(_auto);
		flowLayoutPanel.Controls.Add(_open);
		flowLayoutPanel.Controls.Add(new Label
		{
			AutoSize = true,
			Margin = new Padding(3, 14, 3, 3),
			Text = L.T("Código de la web (opcional)", "Code from the website (optional)"),
			ForeColor = Theme.Muted
		});
		flowLayoutPanel.Controls.Add(_code);
		flowLayoutPanel.Controls.Add(_link);
		flowLayoutPanel.Controls.Add(_sync);
		flowLayoutPanel.Controls.Add(_unlink);
		flowLayoutPanel.Controls.Add(new Label
		{
			AutoSize = true,
			Margin = new Padding(3, 14, 3, 3),
			Text = L.T("Nombre público (clasificación)", "Public username (leaderboard)"),
			ForeColor = Theme.Muted
		});
		flowLayoutPanel.Controls.Add(_user);
		flowLayoutPanel.Controls.Add(_saveUser);
		flowLayoutPanel.Controls.Add(_status);
		Controls.Add(flowLayoutPanel);
		_auto.Click += async (object? _, EventArgs _) =>
		{
			_auto.Enabled = false;
			_status.ForeColor = Theme.Muted;
			_status.Text = L.T("Abriendo la web. Aprueba este PC allí y vuelve aquí.", "Opening the website. Approve this PC there, then come back here.");
			string text = await OnlineSync.StartLinkAsync(_history.Checked);
			if (text != null)
			{
				_auto.Enabled = true;
				_status.ForeColor = Theme.Amber;
				_status.Text = text;
			}
			else
			{
				Refresh2();
			}
		};
		_open.Click += async (object? _, EventArgs _) =>
		{
			_open.Enabled = false;
			string text = await OnlineSync.OpenWebAsync("/stats.html");
			_open.Enabled = true;
			if (text != null)
			{
				_status.ForeColor = Theme.Amber;
				_status.Text = text;
			}
		};
		_link.Click += async (object? _, EventArgs _) =>
		{
			_link.Enabled = false;
			_status.Text = L.T("Vinculando…", "Linking...");
			string text = await OnlineSync.LinkAsync(_code.Text, _history.Checked);
			_link.Enabled = true;
			if (text != null)
			{
				_status.Text = text;
			}
			else
			{
				_code.Clear();
				Refresh2();
			}
		};
		_unlink.Click += (object? _, EventArgs _) =>
		{
			OnlineSync.Unlink();
			Refresh2();
		};
		_sync.CheckedChanged += (object? _, EventArgs _) =>
		{
			if (!_loading)
			{
				OnlineSync.SetEnabled(_sync.Checked);
				Refresh2();
			}
		};
		_saveUser.Click += async (object? _, EventArgs _) =>
		{
			_saveUser.Enabled = false;
			string text = await OnlineUsername.SetAsync(_user.Text);
			_saveUser.Enabled = true;
			_status.ForeColor = ((text != null) ? Theme.Amber : Theme.Muted);
			_status.Text = text ?? L.T("Nombre guardado.", "Username saved.");
		};
		Refresh2();
	}

	private void Refresh2()
	{
		_loading = true;
		OnlineSettings onlineSettings = OnlineSync.Load();
		bool flag = !string.IsNullOrEmpty(onlineSettings.Token);
		_sync.Enabled = flag;
		_sync.Checked = onlineSettings.Enabled;
		_unlink.Enabled = flag;
		_auto.Enabled = !flag;
		_open.Enabled = flag;
		_code.Enabled = !flag;
		_history.Enabled = !flag;
		_link.Enabled = !flag;
		_user.Enabled = flag;
		_saveUser.Enabled = flag;
		_status.ForeColor = ((onlineSettings.LastError != null) ? Theme.Amber : Theme.Muted);
		Label status = _status;
		string text = onlineSettings.LastError;
		if (text == null)
		{
			if (flag)
			{
				if (!onlineSettings.Enabled)
				{
					text = L.T("Vinculado, pero la subida está desactivada.", "Linked, but uploading is off.");
				}
				else
				{
					DateTime? lastSyncUtc = onlineSettings.LastSyncUtc;
					if (lastSyncUtc.HasValue)
					{
						DateTime valueOrDefault = lastSyncUtc.GetValueOrDefault();
						text = L.T("Activado. Última subida: ", "On. Last upload: ") + valueOrDefault.ToLocalTime().ToString("g");
					}
					else
					{
						text = L.T("Activado. Esperando la próxima partida.", "On. Waiting for the next match.");
					}
				}
			}
			else
			{
				text = L.T("Este PC no está vinculado. No se sube nada.", "This PC is not linked. Nothing is uploaded.");
			}
		}
		status.Text = text;
		_loading = false;
	}
}
