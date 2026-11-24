using System;
using System.Collections;
using System.Drawing;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ChestCounter;

[SupportedOSPlatform("windows")]
internal static class OnlineEntry
{
	private const string Tag = "online-entry";

	private static bool _installed;

	public static void Install()
	{
		if (_installed)
		{
			return;
		}
		_installed = true;
		Application.Idle += (object? _, EventArgs _) =>
		{
			foreach (SettingsForm form in ((IEnumerable)Application.OpenForms).OfType<SettingsForm>())
			{
				if (!form.Controls.OfType<PillButton>().Any((PillButton b) => b.Tag as string == "online-entry"))
				{
					PillButton pillButton = new PillButton
					{
						Text = L.T("Conexión online", "Online connection"),
						Location = new Point(110, 9),
						Size = new Size(160, 26),
						Radius = 9,
						Font = Theme.Text(8.5f, FontStyle.Bold),
						Tag = "online-entry"
					};
					pillButton.Click += (object? obj, EventArgs e) =>
					{
						OnlineForm.ShowFor(form);
					};
					form.Controls.Add(pillButton);
					pillButton.BringToFront();
				}
			}
		};
	}
}
