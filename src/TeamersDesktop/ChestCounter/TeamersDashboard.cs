using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ChestCounter;

internal sealed class TeamersDashboard : UserControl
{
    private readonly ComboBox source = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly CheckBox perDay = new() { AutoSize = true, ForeColor = Theme.Ink };
    private readonly Label[] values = new Label[8];
    private readonly ActivityChart chart = new() { Dock = DockStyle.Fill };
    private readonly Label empty = new() { Dock = DockStyle.Bottom, Height = 26, ForeColor = Theme.Muted };
    private TeamersActivity? personal;
    private TeamersActivity? team;
    private bool choseSource;
    private bool updating;

    public TeamersDashboard()
    {
        BackColor = Theme.Card;
        Padding = new Padding(14);
        Height = 424;
        Width = 770;
        var header = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 45, WrapContents = false };
        header.Controls.Add(new Label { Text = "Dashboard Teamers", AutoSize = true, Font = Theme.Text(12, FontStyle.Bold),
            ForeColor = Theme.Ink, Margin = new Padding(0, 2, 15, 0) });
        source.Items.AddRange(new object[] { T("Partite della squadra", "Team matches"), T("I tuoi replay", "Your replays") });
        source.SelectedIndex = 0;
        perDay.Text = T("Per giorno", "By day");
        perDay.Margin = new Padding(14, 5, 0, 0);
        header.Controls.AddRange(new Control[] { source, perDay });
        var metrics = new TableLayoutPanel { Dock = DockStyle.Top, Height = 155, ColumnCount = 4, RowCount = 2 };
        var captions = new[] { T("Aperte · distrutte", "Opened · broken"), T("Media per partita", "Average per match"),
            T("Partita migliore", "Best match"), T("Chest al minuto", "Chests per minute"), T("Ultima partita", "Last match"),
            T("Oggi", "Today"), T("Tesori", "Treasures"), T("Partite", "Matches") };
        for (int column = 0; column < 4; column++) metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        for (int row = 0; row < 2; row++) metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        for (int index = 0; index < captions.Length; index++)
        {
            var cell = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false,
                BackColor = Theme.Panel, Padding = new Padding(8), Margin = new Padding(0,0,8,8) };
            values[index] = new Label { Text = "—", AutoSize = true, ForeColor = Theme.Ink, Font = Theme.Text(14, FontStyle.Bold) };
            cell.Controls.Add(values[index]);
            cell.Controls.Add(new Label { Text = captions[index], AutoSize = true, ForeColor = Theme.Muted });
            metrics.Controls.Add(cell, index % 4, index / 4);
        }
        Controls.Add(chart);
        Controls.Add(empty);
        Controls.Add(metrics);
        Controls.Add(header);
        source.SelectionChangeCommitted += (_, _) => { if (!updating) choseSource = true; Render(); };
        perDay.CheckedChanged += (_, _) => Render();
    }

    public void Update(TeamersState? state)
    {
        personal = state?.Personal?.Activity;
        team = state?.Team?.Activity;
        Visible = state?.Personal != null || state?.Team != null;
        if (!choseSource)
        {
            updating = true;
            source.SelectedIndex = team?.Matches > 0 || personal == null ? 0 : 1;
            updating = false;
        }
        Render();
    }

    private void Render()
    {
        var activity = source.SelectedIndex == 1 ? personal : team;
        var recent = activity?.Recent ?? Array.Empty<TeamersPoint>();
        var ordered = perDay.Checked ? recent.GroupBy(point => point.LocalTime.Date).Select(day =>
            new TeamersPoint(new DateTimeOffset(day.Key).ToUnixTimeMilliseconds(), day.Sum(point => point.Opened),
                day.Sum(point => point.Broken), day.Sum(point => point.Extra))).ToArray() : recent;
        var numbers = new[] { activity == null || activity.Matches == 0 ? "—" : $"{activity.Opened:N0} · {activity.Broken:N0}",
            activity?.Average?.ToString("N1"), activity?.Best?.ToString("N0"), activity?.PerMinute?.ToString("N1"),
            activity?.Last?.ToString("N0"), activity == null ? null : recent.Where(point => point.LocalTime.Date == DateTime.Today)
                .Sum(point => point.Total).ToString("N0"), activity?.Treasures?.ToString("N0"), activity?.Matches.ToString("N0") };
        for (int index = 0; index < numbers.Length; index++) values[index].Text = numbers[index] ?? "—";
        empty.Text = ordered.Length == 0 ? T("Nessuna partita disponibile per questa vista.", "No matches available for this view.")
            : T("Aperte · Distrutte · Extra   /   Ultime 100 partite", "Opened · Broken · Extras   /   Latest 100 matches");
        chart.SetPoints(ordered, perDay.Checked ? null : activity?.Average);
    }

    private static string T(string italian, string english) => TeamersClient.Text(italian, english);

    private sealed class ActivityChart : Control
    {
        private TeamersPoint[] points = Array.Empty<TeamersPoint>();
        private double? average;
        private readonly ToolTip tooltip = new();
        private int hovered = -1;

        public ActivityChart()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Theme.Bg;
            AccessibleName = T("Grafico delle chest", "Chest activity chart");
        }

        public void SetPoints(TeamersPoint[] next, double? mean)
        {
            points = next;
            average = mean;
            hovered = -1;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (points.Length == 0 || ClientSize.Width <= 30) return;
            int index = Math.Clamp((int)((e.X - 14) / ((ClientSize.Width - 28.0) / points.Length)), 0, points.Length - 1);
            if (index == hovered) return;
            hovered = index;
            var point = points[index];
            tooltip.SetToolTip(this, $"{point.LocalTime:g} · {point.Total:N0} ({point.Opened:N0} + {point.Broken:N0} + {point.Extra:N0})");
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (points.Length == 0 || Width <= 35 || Height <= 30) return;
            float plot = Height - 30;
            float floor = Height - 18;
            double peak = Math.Max(1, Math.Max(points.Max(point => point.Total), average ?? 0));
            float slot = (Width - 28f) / points.Length;
            using var openBrush = new SolidBrush(Theme.Accent);
            using var brokenBrush = new SolidBrush(Theme.AccentSoft);
            using var extraBrush = new SolidBrush(Theme.Mint);
            using var line = new Pen(Theme.Line);
            e.Graphics.DrawLine(line, 12, floor, Width - 12, floor);
            if (average is double mean) e.Graphics.DrawLine(line, 12, floor - (float)(plot * mean / peak), Width - 12, floor - (float)(plot * mean / peak));
            for (int index = 0; index < points.Length; index++)
            {
                var point = points[index];
                float y = floor;
                float x = 14 + slot * index;
                Draw(point.Opened, openBrush);
                Draw(point.Broken, brokenBrush);
                Draw(point.Extra, extraBrush);

                void Draw(int count, Brush brush)
                {
                    if (count == 0) return;
                    float size = (float)(count * plot / peak);
                    y -= size;
                    e.Graphics.FillRectangle(brush, x, y, Math.Clamp(slot - 2f, 1, 48), size);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) tooltip.Dispose();
            base.Dispose(disposing);
        }
    }
}
