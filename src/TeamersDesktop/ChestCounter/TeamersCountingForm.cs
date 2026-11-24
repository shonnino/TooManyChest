using System;
using System.Drawing;
using System.Windows.Forms;

namespace ChestCounter;

internal sealed class TeamersCountingForm : Form
{
    private static string T(string italian, string english) => TeamersClient.Text(italian, english);

    public TeamersCountingForm(TeamersClient client, TeamersCounting current)
    {
        Text = T("La tua quota nella squadra", "Your team contribution");
        Icon = AppIcon.Load();
        Font = Theme.Text(10);
        BackColor = Theme.Bg;
        ForeColor = Theme.Ink;
        ClientSize = new Size(460, 330);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;

        var fields = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true, Padding = new Padding(18) };
        var ammo = Option("Ammo Boxes", 1);
        var food = Option("Food Boxes", 2);
        var cannon = Option("Loot Cannons", 4);
        var manual = Option(T("Aggiunte manuali all’ingresso", "Manual additions on joining"), 16);
        var starting = new NumericUpDown { Width = 175, Minimum = 0, Maximum = 50000000,
            Value = current.Starting, ThousandsSeparator = true, BackColor = Theme.Card, ForeColor = Theme.Ink };
        var save = new GlowButton { Text = T("Salva", "Save"), AutoSize = true, MinimumSize = new Size(100, 36) };
        var error = new Label { AutoSize = true, MaximumSize = new Size(400, 0), ForeColor = Theme.AccentSoft };
        fields.Controls.AddRange(new Control[] { ammo, food, cannon, manual,
            new Label { Text = T("Saldo iniziale Teamers", "Teamers starting balance"), AutoSize = true, Margin = new Padding(3,10,3,4) },
            starting, save, error });
        Controls.Add(fields);
        AcceptButton = save;
        save.Click += async (_, _) =>
        {
            int mask = (ammo.Checked ? 1 : 0) | (food.Checked ? 2 : 0) | (cannon.Checked ? 4 : 0) | (manual.Checked ? 16 : 0);
            fields.Enabled = false;
            try
            {
                await client.SaveCountingAsync(mask, (int)starting.Value);
                if (IsDisposed) return;
                if (client.Error == null) DialogResult = DialogResult.OK;
                else error.Text = client.Error;
            }
            finally { if (!IsDisposed) fields.Enabled = true; }
        };

        CheckBox Option(string label, int mask) => new() { Text = label, Checked = (current.Options & mask) != 0, AutoSize = true };
    }
}
