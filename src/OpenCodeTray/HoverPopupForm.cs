using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace OpenCodeTray;

/// <summary>
/// A lightweight, owner-drawn popup that appears when the user hovers over the
/// tray icon. It has no controls so it can be rebuilt cheaply on every update.
/// </summary>
public sealed class HoverPopupForm : Form
{
    private const int PopupWidth = 330;
    private const int Pad = 14;
    private const int RowHeight = 60;

    private static readonly CultureInfo Pl = CultureInfo.GetCultureInfo("pl-PL");

    private static readonly Color Surface = Color.FromArgb(30, 30, 46);
    private static readonly Color Border = Color.FromArgb(69, 71, 90);
    private static readonly Color TextPrimary = Color.FromArgb(205, 214, 244);
    private static readonly Color TextMuted = Color.FromArgb(147, 153, 178);
    private static readonly Color Track = Color.FromArgb(49, 50, 68);
    private static readonly Color Green = Color.FromArgb(166, 227, 161);
    private static readonly Color Yellow = Color.FromArgb(249, 226, 175);
    private static readonly Color Red = Color.FromArgb(243, 139, 168);

    private readonly Font _titleFont = new("Segoe UI", 11f, FontStyle.Bold);
    private readonly Font _bodyFont = new("Segoe UI", 9.5f);
    private readonly Font _smallFont = new("Segoe UI", 8.25f);

    private UsageSnapshot? _snapshot;
    private string? _error;

    public HoverPopupForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Surface;
        DoubleBuffered = true;
        Width = PopupWidth;
        Height = 200;
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
            cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
            return cp;
        }
    }

    public void UpdateContent(UsageSnapshot? snapshot, string? error)
    {
        _snapshot = snapshot;
        _error = error;
        Height = ComputeHeight();
        Invalidate();
    }

    private int ComputeHeight()
    {
        if (_error is not null && _snapshot is null)
        {
            var textSize = TextRenderer.MeasureText(
                _error, _bodyFont, new Size(PopupWidth - Pad * 2, int.MaxValue), TextFormatFlags.WordBreak);
            return Pad + 24 + 20 + textSize.Height + Pad;
        }

        return Pad + 24 + 20 + RowHeight * 3 + Pad - 8;
    }

    public void ShowNearCursor()
    {
        var cursor = Cursor.Position;
        var screen = Screen.FromPoint(cursor);
        var area = screen.WorkingArea;

        int x = cursor.X - Width / 2;
        int y = cursor.Y - Height - 12;
        if (y < area.Top)
        {
            y = cursor.Y + 24;
        }

        x = Math.Max(area.Left + 4, Math.Min(x, area.Right - Width - 4));
        y = Math.Max(area.Top + 4, Math.Min(y, area.Bottom - Height - 4));
        Location = new Point(x, y);

        if (!Visible)
        {
            Show();
        }
        else
        {
            BringToFront();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        using (var borderPen = new Pen(Border))
        {
            g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
        }

        int y = Pad;
        TextRenderer.DrawText(
            g, "OpenCode Go", _titleFont,
            new Rectangle(Pad, y, Width - Pad * 2, 22), TextPrimary, TextFormatFlags.Left);
        y += 24;

        var updatedText = _snapshot is not null
            ? $"Zaktualizowano {_snapshot.FetchedAt.ToString("HH:mm:ss", Pl)}"
            : "Oczekiwanie na dane…";
        TextRenderer.DrawText(
            g, updatedText, _smallFont,
            new Rectangle(Pad, y, Width - Pad * 2, 16), TextMuted, TextFormatFlags.Left);
        y += 20;

        if (_error is not null && _snapshot is null)
        {
            TextRenderer.DrawText(
                g, _error, _bodyFont,
                new Rectangle(Pad, y, Width - Pad * 2, Height - y - Pad), Red,
                TextFormatFlags.WordBreak | TextFormatFlags.Left);
            return;
        }

        if (_snapshot is null)
        {
            return;
        }

        DrawRow(g, ref y, "Okno 5-godzinne", _snapshot.Rolling);
        DrawRow(g, ref y, "Tydzień", _snapshot.Weekly);
        DrawRow(g, ref y, "Miesiąc", _snapshot.Monthly);
    }

    private void DrawRow(Graphics g, ref int y, string name, UsageWindow window)
    {
        TextRenderer.DrawText(
            g, name, _bodyFont,
            new Rectangle(Pad, y, 190, 18), TextPrimary, TextFormatFlags.Left);

        var valueText = window.IsRateLimited ? "limit osiągnięty" : $"{Math.Round(window.Percent)}%";
        var valueColor = window.IsRateLimited
            ? Red
            : window.Percent >= 90 ? Red
            : window.Percent >= 60 ? Yellow
            : TextPrimary;
        TextRenderer.DrawText(
            g, valueText, _bodyFont,
            new Rectangle(Width - Pad - 180, y, 180, 18), valueColor, TextFormatFlags.Right);
        y += 21;

        var bar = new Rectangle(Pad, y, Width - Pad * 2, 7);
        using (var trackPath = Rounded(bar, 4))
        using (var trackBrush = new SolidBrush(Track))
        {
            g.FillPath(trackBrush, trackPath);
        }

        int fillWidth = (int)Math.Round(bar.Width * (window.Percent / 100.0));
        if (fillWidth > 0)
        {
            var filled = new Rectangle(bar.X, bar.Y, Math.Max(fillWidth, 4), bar.Height);
            using var fillPath = Rounded(filled, 4);
            using var fillBrush = new SolidBrush(FillColor(window));
            g.FillPath(fillBrush, fillPath);
        }

        y += 12;

        TextRenderer.DrawText(
            g, FormatReset(window.ResetsAt), _smallFont,
            new Rectangle(Pad, y, Width - Pad * 2, 16), TextMuted, TextFormatFlags.Left);
        y += 16 + 11;
    }

    private static Color FillColor(UsageWindow window)
    {
        if (window.IsRateLimited || window.Percent >= 90)
        {
            return Red;
        }

        return window.Percent >= 60 ? Yellow : Green;
    }

    private static string FormatReset(DateTimeOffset resetsAt)
    {
        var local = resetsAt.ToLocalTime();
        var span = local - DateTimeOffset.Now;
        var absolute = local.ToString("dddd HH:mm", Pl);

        if (span <= TimeSpan.Zero)
        {
            return $"Reset: od {absolute}";
        }

        string relative;
        if (span.TotalDays >= 1)
        {
            relative = $"za {(int)span.TotalDays} d {span.Hours} godz.";
        }
        else if (span.TotalHours >= 1)
        {
            relative = $"za {(int)span.TotalHours} godz. {span.Minutes} min";
        }
        else
        {
            relative = $"za {Math.Max(1, span.Minutes)} min";
        }

        return $"Reset {relative} ({absolute})";
    }

    private static GraphicsPath Rounded(Rectangle rectangle, int radius)
    {
        int diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
