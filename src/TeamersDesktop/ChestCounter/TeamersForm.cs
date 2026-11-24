using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace ChestCounter;

internal sealed class TeamersForm : Form
{
    private readonly TeamersClient client;
    private readonly Label total = new() { AutoSize = true, Font = Theme.Text(19f, FontStyle.Bold), ForeColor = Theme.Accent };
    private readonly Label detail = new() { AutoSize = true, ForeColor = Theme.Muted };
    private readonly Label personal = new() { AutoSize = true, ForeColor = Theme.Ink };
    private readonly Label contribution = new() { AutoSize = true, ForeColor = Theme.AccentSoft };
    private readonly TeamersDashboard dashboard = new();
    private readonly Button countingButton;
    private readonly Label status = new() { AutoSize = true, ForeColor = Theme.AccentSoft, MaximumSize = new Size(640, 0) };
    private readonly TextBox teamName = new() { Width = 220, MaxLength = 40, PlaceholderText = TeamersClient.Text("Nome del team", "Team name") };
    private readonly ListView members = new() { View = View.Details, FullRowSelect = true, HeaderStyle = ColumnHeaderStyle.Nonclickable, Height = 125, Width = 630 };
    private readonly ComboBox invites = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 305 };
    private readonly FlowLayoutPanel createRow;
    private readonly FlowLayoutPanel inviteRow;
    private readonly FlowLayoutPanel inboxRow;
    private readonly FlowLayoutPanel connectionRow;
    private readonly Label outgoing = new() { AutoSize = true, ForeColor = Theme.Muted, MaximumSize = new Size(630, 0) };
    private bool busy;
    private string? linkStatus;
    private string? activeInviteSignature;

    public TeamersForm(TeamersClient client)
    {
        this.client = client;
        base.Text = "Teamers";
        Icon = AppIcon.Load();
        BackColor = Theme.Bg;
        ForeColor = Theme.Ink;
        Font = Theme.Text();
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(870, 740);
        MinimumSize = new Size(710, 530);

        var content = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(20),
            FlowDirection = FlowDirection.TopDown, WrapContents = false
        };
        Controls.Add(content);
        var heading = new Label { Text = "Teamers", AutoSize = true, Font = Theme.Text(24f, FontStyle.Bold) };
        content.Controls.Add(heading);
        content.Controls.Add(total);
        content.Controls.Add(detail);
        content.Controls.Add(personal);
        content.Controls.Add(contribution);

        members.BackColor = Theme.Card;
        members.ForeColor = Theme.Ink;
        members.Columns.Add(TeamersClient.Text("Membro", "Member"), 282);
        members.Columns.Add(TeamersClient.Text("Conteggio all’ingresso", "Chests on joining"), 185);
        members.Columns.Add(TeamersClient.Text("Aggiunti insieme", "Added together"), 155);

        connectionRow = Row(Button(TeamersClient.Text("Collega al sito", "Link to website"), async () =>
        {
            using var connection = new TeamersConnectionForm(client);
            if (connection.ShowDialog(this) == DialogResult.OK) await client.RefreshAsync();
        }));
        createRow = Row(teamName, Button(TeamersClient.Text("Crea team", "Create team"), () => client.CreateAsync(teamName.Text.Trim())));
        inviteRow = Row(Button(TeamersClient.Text("Genera e copia link", "Generate and copy link"), CopyInviteLink));
        inboxRow = Row(invites,
            Button(TeamersClient.Text("Accetta", "Accept"), () => Respond(true)),
            Button(TeamersClient.Text("Rifiuta", "Decline"), () => Respond(false)));
        countingButton = Button(TeamersClient.Text("Opzioni Teamers", "Teamers options"), EditCounting);
        foreach (Control control in new Control[] { Row(countingButton, Button(TeamersClient.Text("Aggiorna", "Refresh"), client.RefreshAsync)),
                     status, dashboard, members, connectionRow, createRow, inviteRow, inboxRow, outgoing }) content.Controls.Add(control);
        content.ClientSizeChanged += (_, _) => dashboard.Width = Math.Max(620, content.ClientSize.Width - 52);
        dashboard.Width = content.ClientSize.Width - 52;

        client.Changed += Render;
        FormClosed += (_, _) => client.Changed -= Render;
        Shown += async (_, _) => await Run(client.RefreshAsync);
        Render();
    }

    private Task Respond(bool accept) => invites.SelectedItem is InvitationItem item
        ? client.RespondAsync(item.Invite.Id, accept) : Task.CompletedTask;

    private Task EditCounting()
    {
        if (client.State?.Counting is { Ready: true } settings)
        {
            using var editor = new TeamersCountingForm(client, settings);
            editor.ShowDialog(this);
        }
        return Task.CompletedTask;
    }

    private async Task CopyInviteLink()
    {
        string? link = await client.GenerateInviteLinkAsync();
        if (link == null) return;
        linkStatus = link;
        try
        {
            Clipboard.SetText(link);
            linkStatus = TeamersClient.Text("Link copiato. Vale 7 giorni e sostituisce il precedente.", "Link copied. It is valid for 7 days and replaces the previous one.");
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            using var dialog = new Form { Text = "Teamers", StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(600, 65) };
            dialog.Controls.Add(new TextBox { Dock = DockStyle.Top, ReadOnly = true, Text = link });
            dialog.ShowDialog(this);
        }
    }

    private async Task Run(Func<Task> action)
    {
        if (busy) return;
        busy = true;
        Render();
        try { await action(); }
        finally { busy = false; if (!IsDisposed) Render(); }
    }

    private void Render()
    {
        if (IsDisposed) return;
        var state = client.State;
        var team = state?.Team;
        personal.Visible = state?.Personal != null;
        if (state?.Personal is { } profile)
            personal.Text = TeamersClient.Text($"Totale personale · {profile.Total:N0}", $"Personal total · {profile.Total:N0}");
        var own = team?.Members.FirstOrDefault(member => member.PlayerKey == team.PlayerKey);
        contribution.Visible = own != null;
        if (own != null)
            contribution.Text = TeamersClient.Text($"La tua quota nella squadra · {own.InitialChests + own.SharedChests:N0}",
                $"Your team contribution · {own.InitialChests + own.SharedChests:N0}");
        countingButton.Enabled = !busy && state?.Counting?.Ready == true;
        dashboard.Update(state);
        status.Text = client.Error ?? (busy ? TeamersClient.Text("Aggiornamento…", "Updating…") : linkStatus ?? "");
        total.Text = team == null ? TeamersClient.Text("Nessun team", "No team yet")
            : $"{team.Name} · {team.InitialChests + team.SharedChests:N0}";
        detail.Text = team != null
            ? TeamersClient.Text($"Partenza {team.InitialChests:N0} · Insieme +{team.SharedChests:N0} · Partite {team.SharedMatches:N0}",
                $"Starting {team.InitialChests:N0} · Together +{team.SharedChests:N0} · Matches {team.SharedMatches:N0}")
            : !client.Linked ? TeamersClient.Text("Collega ChestCounter al tuo profilo Teamers.", "Link ChestCounter to your Teamers profile.")
            : state?.Username != null ? state.Username
            : "";

        members.Visible = team != null;
        members.BeginUpdate();
        members.Items.Clear();
        if (team != null)
            foreach (var member in team.Members)
                members.Items.Add(new ListViewItem(new[] { member.Username, member.InitialChests.ToString("N0"), member.SharedChests.ToString("N0") }));
        members.EndUpdate();

        var pending = state?.Invitations ?? Array.Empty<TeamersInvite>();
        string signature = string.Join("|", pending.Select(invite => invite.Id));
        if (signature != activeInviteSignature)
        {
            string? selected = (invites.SelectedItem as InvitationItem)?.Invite.Id;
            invites.Items.Clear();
            invites.Items.AddRange(pending.Select(invite => (object)new InvitationItem(invite)).ToArray());
            if (pending.Length > 0) invites.SelectedIndex = Math.Max(0, Array.FindIndex(pending, invite => invite.Id == selected));
            activeInviteSignature = signature;
        }
        inboxRow.Visible = pending.Length > 0;
        createRow.Visible = state?.Username != null && team == null;
        inviteRow.Visible = team?.CanInvite == true && team.Members.Length < 4;
        createRow.Enabled = inviteRow.Enabled = inboxRow.Enabled = connectionRow.Enabled = !busy;
        outgoing.Text = team?.Outgoing.Length > 0 ? TeamersClient.Text("Inviti ", "Invitations ") + string.Join(" · ",
            team.Outgoing.Select(invite => $"{invite.Username} ({InviteStatus(invite.Status)})")) : "";
        outgoing.Visible = outgoing.Text.Length > 0;
    }

    private static string InviteStatus(string status) => status switch
    {
        "accepted" => TeamersClient.Text("accettato", "accepted"),
        "declined" => TeamersClient.Text("rifiutato", "declined"),
        _ => TeamersClient.Text("in attesa", "pending"),
    };

    private Button Button(string label, Func<Task> action)
    {
        var button = new GlowButton { Text = label, AutoSize = true, MinimumSize = new Size(95, 34), Padding = new Padding(8, 0, 8, 0) };
        button.Click += async (_, _) => await Run(action);
        return button;
    }

    private static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel { WrapContents = false, AutoSize = true, Margin = new Padding(0, 8, 0, 0) };
        row.Controls.AddRange(controls);
        return row;
    }

    private sealed record InvitationItem(TeamersInvite Invite)
    {
        public override string ToString() => $"{Invite.TeamName} · {Invite.FromUsername}";
    }
}
