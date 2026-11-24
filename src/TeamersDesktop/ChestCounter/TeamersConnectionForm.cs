using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace ChestCounter;

internal sealed class TeamersConnectionForm : Form
{
    public TeamersConnectionForm(TeamersClient client, string? browserCode = null)
    {
        Text = T("Collega ChestCounter a Teamers", "Link ChestCounter to Teamers");
        Font = Theme.Text();
        Icon = AppIcon.Load();
        BackColor = Theme.Bg;
        ForeColor = Theme.Ink;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(500, 250);

        var fields = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(20) };
        var personal = client.TryPersonalSnapshot();
        var help = new Label { AutoSize = true, MaximumSize = new Size(440, 0), Text = T(
            "Apri Teamers e scegli «Collega all’app». Incolla qui il codice se il browser non apre ChestCounter.",
            "Open Teamers and choose Link App. Paste the code here if the browser cannot open ChestCounter.") };
        var identity = new Label { AutoSize = true, MaximumSize = new Size(440, 0), Text = personal == null
            ? TeamersClient.MissingProfile
            : T($"Profilo Epic da collegare: {personal.EpicUsername}", $"Epic profile to link: {personal.EpicUsername}") };
        var code = new TextBox { Width = 435, MaxLength = 64, Text = browserCode ?? "", PlaceholderText = T("Codice monouso", "One-time code") };
        var status = new Label { AutoSize = true, ForeColor = Theme.AccentSoft, MaximumSize = new Size(435, 0) };
        var actions = new FlowLayoutPanel { WrapContents = false, AutoSize = true, Margin = new Padding(0, 10, 0, 0) };
        var website = ActionButton(T("Apri Teamers", "Open Teamers"));
        var connect = ActionButton(T("Conferma collegamento", "Confirm connection"));
        connect.Enabled = personal != null;
        website.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(TeamersClient.SitePage) { UseShellExecute = true }); }
            catch (Exception) { status.Text = TeamersClient.SitePage; }
        };
        connect.Click += async (_, _) =>
        {
            actions.Enabled = code.Enabled = false;
            status.Text = T("Collegamento…", "Linking…");
            try
            {
                bool linked = await client.PairProfileAsync(code.Text.Trim(), TeamersClient.SiteOrigin, personal);
                if (IsDisposed) return;
                if (linked) DialogResult = DialogResult.OK;
                else status.Text = client.Error ?? "";
            }
            finally { if (!IsDisposed) actions.Enabled = code.Enabled = true; }
        };
        if (browserCode == null)
        {
            fields.Controls.AddRange(new Control[] { help, identity, code });
            actions.Controls.Add(website);
        }
        else
        {
            fields.Controls.Add(identity);
            fields.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(440, 0), Text = T(
                "Conferma se hai appena richiesto il collegamento su Teamers.",
                "Confirm if you just requested the connection on Teamers.") });
        }
        actions.Controls.Add(connect);
        fields.Controls.AddRange(new Control[] { actions, status });
        Controls.Add(fields);
        if (browserCode == null) AcceptButton = connect;
    }

    private static Button ActionButton(string caption) =>
        new GlowButton { Text = caption, AutoSize = true, MinimumSize = new Size(110, 34), Padding = new Padding(8, 0, 8, 0) };

    private static string T(string italian, string english) => TeamersClient.Text(italian, english);
}
